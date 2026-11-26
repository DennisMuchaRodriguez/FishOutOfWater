using UnityEngine;

// Cinemática de INICIO de nivel: el pez está en el fondo del lago y sale disparado hacia el cielo con el propulsor.
//   1. Bajo el agua: la cámara al lado del pez, que sube despacio.
//   2. Enciende el propulsor y acelera hacia la superficie; la cámara lo sigue y cruza el agua con él (salpicadura).
//   3. Desde casi a ras del agua, la cámara lo mira subir hasta el cielo; ahí se le devuelve el control al jugador,
//      en el aire y todavía subiendo (la cámara vuelve sola a su vista normal).
// Como WaveCinematic, NO toca cámaras: calcula una pose que la cámara del jugador mezcla (PlayerController_Base).
// Mientras dura, GameDirector.InCinematic es true (el HUD se esconde). Espacio o Enter la saltan.
// La lanza GameDirector al empezar la Historia y la Supervivencia (no en cooperativo).
public class LaunchCinematic : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Metros bajo la superficie donde empieza el pez")]
        public float depth = 3.5f;
        [Tooltip("Metros sobre el agua hasta donde sube")]
        public float height = 9f;
        [Tooltip("Segundos bajo el agua antes de encender el propulsor")]
        public float underwaterTime = 1.1f;
        [Tooltip("Segundos hasta romper la superficie")]
        public float riseTime = 0.8f;
        [Tooltip("Segundos subiendo hacia el cielo")]
        public float climbTime = 1.4f;
        [Tooltip("Segundos en que la cámara vuelve a la vista del jugador")]
        public float blendOut = 0.9f;
        [Tooltip("Velocidad hacia arriba con la que el jugador recibe el control (m/s)")]
        public float exitSpeed = 3f;
    }

    static LaunchCinematic active;

    public static bool IsPlaying { get { return active != null && !active.finished; } }
    public bool Done { get { return finished; } }

    // Pose que la cámara del jugador debe mezclar con la suya (false = no hay cinemática)
    public static bool TryGetPose(out Vector3 position, out Quaternion rotation, out float fov, out float blend)
    {
        if (!IsPlaying || active.weight <= 0f)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            fov = 60f;
            blend = 0f;
            return false;
        }
        position = active.posePos;
        rotation = active.poseRot;
        fov = active.poseFov;
        blend = active.weight;
        return true;
    }

    public static LaunchCinematic Play(PlayerController_Base player, LakeVolume lake, Settings settings)
    {
        if (player == null || lake == null) return null;
        if (active != null) Destroy(active.gameObject);
        GameObject go = new GameObject("CinematicaSalida");
        LaunchCinematic c = go.AddComponent<LaunchCinematic>();
        active = c;
        c.player = player;
        c.lake = lake;
        c.s = settings ?? new Settings();
        c.Begin();
        return c;
    }

    PlayerController_Base player;
    Rigidbody rb;
    LakeVolume lake;
    Settings s;
    bool finished;
    float t;                 // tiempo de la cinemática (no avanza en pausa)
    float surface;
    Vector3 start, top;
    Quaternion facing;
    Vector3 camDir;          // de qué lado mira la cámara (horizontal, desde el pez)
    bool jetOn, splashed;
    float weight = 1f;
    Vector3 posePos;
    Quaternion poseRot = Quaternion.identity;
    float poseFov = 55f;

    float T1 { get { return s.underwaterTime; } }
    float T2 { get { return T1 + s.riseTime; } }
    float T3 { get { return T2 + s.climbTime; } }
    float TEnd { get { return T3 + s.blendOut; } }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void Begin()
    {
        player.SetCinematicLock(true);
        rb = player.GetComponent<Rigidbody>();
        surface = lake.SurfaceY;

        // Un punto con agua honda cerca de donde está el pez
        Vector3 p = player.transform.position;
        if (lake.WaterDepthAt(p.x, p.z) < s.depth + 1.2f)
        {
            Vector3 point;
            if (lake.TryGetSwimPoint(p, 30f, s.depth + 1.2f, 80f, 1.2f, out point, 20)) p = point;
        }
        float ground = lake.GroundHeight(p.x, p.z);
        start = new Vector3(p.x, Mathf.Max(ground + 1.2f, surface - s.depth), p.z);
        top = new Vector3(p.x, surface + s.height, p.z);

        // Mira hacia el centro del lago (así la cámara, del lado de afuera, ve el lago detrás)
        Vector3 toCenter = lake.Center - start;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 1f) toCenter = player.transform.forward;
        toCenter.y = 0f;
        facing = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
        camDir = (facing * new Vector3(0.75f, 0f, -0.66f)).normalized;   // al costado y un poco por delante

        Place(start);
        UpdatePose();
    }

    void Place(Vector3 pos)
    {
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = facing;
        }
        player.transform.SetPositionAndRotation(pos, facing);
    }

    // Dónde está el pez en el tiempo 'time'
    Vector3 FishAt(float time)
    {
        float y;
        if (time < T1) y = start.y + 0.35f * Smooth(time / T1);
        else if (time < T2)
        {
            float k = (time - T1) / Mathf.Max(0.01f, s.riseTime);
            y = Mathf.Lerp(start.y + 0.35f, surface + 0.6f, k * k);                     // acelera
        }
        else if (time < T3)
        {
            float k = (time - T2) / Mathf.Max(0.01f, s.climbTime);
            y = Mathf.Lerp(surface + 0.6f, top.y, 1f - (1f - k) * (1f - k));          // frena al llegar arriba
        }
        else y = top.y + Mathf.Sin((time - T3) * 3f) * 0.15f;
        return new Vector3(start.x, y, start.z);
    }

    void FixedUpdate()
    {
        if (finished) return;
        float dt = Time.fixedDeltaTime;
        if (t > 0.3f && (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) && t < T3) t = T3;   // saltar
        t += dt;

        Vector3 pos = FishAt(t);
        if (rb != null) rb.MovePosition(pos);
        else player.transform.position = pos;

        if (!jetOn && t >= T1 - 0.1f)
        {
            jetOn = true;
            player.SetCinematicJet(true, 1f);
        }
        if (!splashed && pos.y >= surface)
        {
            splashed = true;
            LakeWater.Splash(new Vector3(pos.x, surface, pos.z), 1f, false);
            Sfx.Play(Sonido.SalidaAgua, pos, 1f);
        }
        if (t >= TEnd) End();
    }

    void Update()
    {
        if (finished) return;
        UpdatePose();
    }

    void UpdatePose()
    {
        Vector3 fish = player.transform.position;
        // Distancia: cerca bajo el agua, más lejos cuando sube al cielo
        float dist = Mathf.Lerp(5.5f, 10f, Smooth((t - T1) / (T3 - T1)));
        // Altura: bajo el agua junto al pez; cruza la superficie justo detrás de él; después, casi a ras del agua
        float camY;
        if (t < T2) camY = Mathf.Min(fish.y - 0.3f, surface - 0.9f);
        else camY = Mathf.Lerp(surface - 0.9f, surface + 0.8f, Smooth((t - T2) / 0.35f));
        Vector3 cam = new Vector3(fish.x, camY, fish.z) + camDir * dist;
        float ground = lake.GroundHeight(cam.x, cam.z);
        cam.y = Mathf.Max(cam.y, ground + 1f);

        Vector3 look = fish + Vector3.up * 0.8f - cam;
        posePos = cam;
        poseRot = Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look.normalized : Vector3.forward, Vector3.up);
        poseFov = Mathf.Lerp(52f, 62f, Smooth((t - T1) / s.riseTime));
        weight = 1f - Smooth((t - T3) / Mathf.Max(0.05f, s.blendOut));
    }

    void End()
    {
        if (finished) return;
        finished = true;
        if (active == this) active = null;
        Place(top);
        player.SetCinematicLock(false);
        player.SetCinematicJet(false);
        if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.up * s.exitSpeed;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (finished) return;
        // Si se corta (cambio de escena), el pez nunca queda bloqueado
        finished = true;
        if (active == this) active = null;
        if (player != null)
        {
            player.SetCinematicLock(false);
            player.SetCinematicJet(false);
        }
    }
}
