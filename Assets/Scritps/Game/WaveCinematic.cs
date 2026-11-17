using System.Collections.Generic;
using UnityEngine;

// Cinemática de llegada de una oleada: tres planos seguidos, como en una película.
//   1. PLANO GENERAL: a ras del agua junto al jugador, girando despacio, con la bandada asomando en el cielo.
//   2. BANDADA: la cámara vuela por delante de las aves y las ve venir de frente (abajo sale qué aves son).
//   3. SOBRE EL HOMBRO: detrás del jugador, acercándose y mirando a la bandada; de ahí vuelve a la vista normal.
//
// NO crea, apaga ni cambia cámaras. Solo calcula cada frame una "pose" (posición, rotación, FOV) y un peso 0..1.
// La cámara del jugador (PlayerController_Base.LateUpdate) mezcla su vista normal con esa pose según el peso:
//   peso 0 -> vista normal del jugador     peso 1 -> plano de la cinemática
// Si este objeto desaparece por cualquier motivo (termina, se salta, se destruye, cambia la escena, hay un
// error...), en el siguiente frame la cámara vuelve sola a la vista normal: no hay nada que "restaurar" y no se
// puede quedar trabada.
//
// Los valores de cada plano están en el GameDirector (Cinemática de llegada > Planos) y se pueden ver sin dar Play:
// con el GameDirector seleccionado se dibujan las tres cámaras en la vista de escena, y su menú ⋮ tiene
// "Cinemática: ver plano ..." para poner la vista de escena justo en ese encuadre.
public class WaveCinematic : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Cuánto dura cada plano, repartiendo el total: x = general, y = bandada, z = sobre el hombro")]
        public Vector3 shotWeights = new Vector3(0.34f, 0.3f, 0.36f);

        [Header("1 · Plano general (a ras del agua)")]
        [Tooltip("Distancia de la cámara al jugador")]
        public float wideDistance = 13f;
        [Tooltip("Altura sobre el agua")]
        public float wideHeight = 1.3f;
        [Tooltip("Ángulo alrededor del jugador al empezar (0 = justo detrás, mirando hacia las aves)")]
        public float wideAngle = 38f;
        [Tooltip("Grados que gira alrededor del jugador durante el plano")]
        public float wideOrbit = 16f;
        public float wideFov = 54f;

        [Header("2 · Seguimiento de la bandada")]
        [Tooltip("Metros por delante de las aves al empezar (se acercan a la cámara durante el plano)")]
        public float flockAhead = 18f;
        public float flockSide = 8f;
        public float flockUp = 2f;
        public float flockFov = 48f;
        [Tooltip("Inclinación lateral de la cámara (grados)")]
        public float flockRoll = 6f;

        [Header("3 · Sobre el hombro")]
        public float shoulderBack = 5.5f;
        public float shoulderSide = 2.4f;
        public float shoulderUp = 3.2f;
        [Tooltip("Metros que se acerca durante el plano")]
        public float shoulderPush = 2.5f;

        [Header("General")]
        [Tooltip("Temblor de cámara en mano (grados)")]
        [Range(0f, 2f)] public float handheld = 0.35f;
        [Tooltip("Muestra abajo qué aves llegan")]
        public bool showCaption = true;
    }

    // Lo que necesita un plano para calcularse (también lo usa la vista previa del GameDirector)
    public struct Anchor
    {
        public Vector3 player;      // posición del jugador
        public Vector3 toFlock;     // dirección horizontal del jugador hacia la bandada
        public Vector3 flock;       // centro de la bandada
        public Vector3 flockDir;    // hacia dónde vuela la bandada
        public float flockRadius;   // qué tan abierta va
        public float sideSign;      // de qué lado se pone la cámara (+1 / -1)
        public LakeVolume lake;
    }

    public const int ShotCount = 3;
    public static readonly string[] ShotNames = { "Plano general", "Bandada", "Sobre el hombro" };

    static WaveCinematic active;

    // True mientras hay una cinemática en curso (el HUD se oculta y el jugador no se mueve).
    // Si por lo que sea la cinemática deja de actualizarse medio segundo, se da por terminada.
    public static bool IsPlaying { get { return active != null && Time.unscaledTime - active.lastTick < 0.5f; } }
    // Para el HUD: rótulo de las aves que llegan, su opacidad y si ya se puede saltar
    public static string Caption { get { return IsPlaying ? active.caption : ""; } }
    public static string CaptionTitle { get { return IsPlaying ? active.captionTitle : ""; } }
    public static float CaptionAlpha { get { return IsPlaying ? active.captionAlpha : 0f; } }
    public static bool CanSkip { get { return IsPlaying && active.back < 0f && active.t > SkipDelay; } }

    PlayerController_Base player;
    List<BirdAI> birds;
    LakeVolume lake;
    System.Action<bool> letterbox;
    Settings settings;

    float duration;
    float t;          // tiempo del plano (no avanza en pausa)
    float back = -1f; // >= 0: volviendo a la vista del jugador
    float life;       // tiempo real total (tope de seguridad)
    float weight;
    float lastTick;
    float sideSign = 1f;
    float noiseSeed;
    Vector3 anchorPlayer, toFlock;
    Vector3 flockDir;
    Vector3 posePos;
    Quaternion poseRot = Quaternion.identity;
    float poseFov = 60f;
    readonly float[] shotEnd = new float[ShotCount];
    string caption = "", captionTitle = "";
    float captionAlpha;
    bool finished;

    const float BlendIn = 0.9f;
    const float BlendOut = 0.7f;
    const float SkipDelay = 0.6f;

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

    public static WaveCinematic Play(PlayerController_Base player, List<BirdAI> birds, LakeVolume lake,
                                     float duration, System.Action<bool> letterbox, Settings settings = null)
    {
        if (player == null) return null;
        if (active != null) Destroy(active.gameObject);

        GameObject go = new GameObject("CinematicaOleada");
        WaveCinematic c = go.AddComponent<WaveCinematic>();
        try
        {
            c.Init(player, birds, lake, duration, letterbox, settings ?? new Settings());
        }
        catch (System.Exception e)
        {
            // Si algo falla al empezar, simplemente no hay cinemática
            Debug.LogException(e);
            Destroy(go);
            return null;
        }
        return c;
    }

    // Por si el proyecto entra a Play sin recargar el dominio
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { active = null; }

    void Init(PlayerController_Base p, List<BirdAI> wave, LakeVolume l, float d, System.Action<bool> lb, Settings s)
    {
        player = p;
        birds = wave;
        lake = l;
        duration = Mathf.Max(0.5f, d);
        letterbox = lb;
        settings = s;
        sideSign = Random.value < 0.5f ? -1f : 1f;
        noiseSeed = Random.Range(0f, 100f);

        float radius;
        Vector3 flock = FlockCenter(out radius);
        anchorPlayer = player.transform.position;
        toFlock = flock - anchorPlayer;
        toFlock.y = 0f;
        toFlock = toFlock.sqrMagnitude > 0.01f ? toFlock.normalized : player.transform.forward;
        flockDir = FlockDirection(flock);

        // Reparto del tiempo entre planos (sin aves no hay plano de bandada; si es muy corta, tampoco)
        Vector3 w = new Vector3(Mathf.Max(0f, s.shotWeights.x), Mathf.Max(0f, s.shotWeights.y), Mathf.Max(0f, s.shotWeights.z));
        if (CountBirds() == 0 || duration < 2.5f) w.y = 0f;
        float sum = w.x + w.y + w.z;
        if (sum <= 0.001f)
        {
            w = new Vector3(0f, 0f, 1f);
            sum = 1f;
        }
        shotEnd[0] = duration * w.x / sum;
        shotEnd[1] = shotEnd[0] + duration * w.y / sum;
        shotEnd[2] = duration;

        BuildCaption();
        ComputePose();

        lastTick = Time.unscaledTime;
        active = this;
        player.SetCinematicLock(true);
        if (letterbox != null) letterbox(true);
    }

    void Update()
    {
        // Tope de seguridad en tiempo real (cuenta aunque el juego esté en pausa)
        lastTick = Time.unscaledTime;
        life += Time.unscaledDeltaTime;
        if (player == null || life > duration + BlendOut + 15f)
        {
            Destroy(gameObject);
            return;
        }
        if (PauseMenu.IsPaused) return;

        // Tiempo real: no depende de Time.timeScale
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        t += dt;
        ComputePose();
        UpdateCaption(dt);

        if (back < 0f)
        {
            weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / BlendIn));
            bool skip = t > SkipDelay && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));
            if (t >= duration || skip)
            {
                // La cámara regresa a la vista del jugador; los controles vuelven al terminar
                back = 0f;
                if (letterbox != null && active == this) letterbox(false);
            }
        }
        else
        {
            back += dt;
            weight = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(back / BlendOut))) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / BlendIn));
            if (back >= BlendOut) Destroy(gameObject);
        }
    }

    // ======================= PLANOS =======================

    int CurrentShot(out float u)
    {
        float start = 0f;
        for (int i = 0; i < ShotCount; i++)
        {
            float end = shotEnd[i];
            if (end - start > 0.01f && (t < end || i == ShotCount - 1))
            {
                u = Mathf.Clamp01((t - start) / (end - start));
                return i;
            }
            start = end;
        }
        u = 1f;
        return ShotCount - 1;
    }

    void ComputePose()
    {
        float radius;
        Vector3 flock = FlockCenter(out radius);
        // La dirección de vuelo se suaviza para que el plano de seguimiento no dé tirones cuando las aves giran
        flockDir = Vector3.Slerp(flockDir, FlockDirection(flock), 0.08f);

        Anchor a = new Anchor
        {
            player = anchorPlayer,
            toFlock = toFlock,
            flock = flock,
            flockDir = flockDir,
            flockRadius = radius,
            sideSign = sideSign,
            lake = lake
        };
        float u;
        int shot = CurrentShot(out u);
        Evaluate(settings, shot, u, a, out posePos, out poseRot, out poseFov);

        // Cámara en mano: un temblor suave y lento (más vivo en el plano de la bandada)
        float hand = settings.handheld * (shot == 1 ? 1.6f : 1f);
        if (hand > 0f)
        {
            float nt = t * 0.55f + noiseSeed;
            float pitch = (Mathf.PerlinNoise(nt, 0.3f) - 0.5f) * 2f * hand;
            float yaw = (Mathf.PerlinNoise(0.7f, nt) - 0.5f) * 2f * hand;
            poseRot = poseRot * Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    // Pose de un plano en el momento u (0..1 dentro del plano). No guarda nada: la usa también la vista previa
    // del GameDirector en el editor.
    public static void Evaluate(Settings s, int shot, float u, Anchor a, out Vector3 pos, out Quaternion rot, out float fov)
    {
        if (s == null) s = new Settings();
        Vector3 up = Vector3.up;
        Vector3 side = Vector3.Cross(up, a.toFlock).normalized * a.sideSign;
        Vector3 look;
        float roll = 0f;

        if (shot == 0)
        {
            // 1. Plano general: a ras del agua, de lado y algo por detrás del jugador, girando despacio a su alrededor
            float ang = (s.wideAngle + s.wideOrbit * u) * a.sideSign;
            Vector3 dir = Quaternion.AngleAxis(ang, up) * -a.toFlock;
            pos = a.player + dir * s.wideDistance;
            pos.y = (a.lake != null ? a.lake.SurfaceY : a.player.y) + s.wideHeight;
            pos = KeepAboveGround(pos, a.lake, 1f);
            // Encuadra al jugador abajo y a la bandada arriba
            Vector3 toPlayer = (a.player + up * 0.8f - pos).normalized;
            Vector3 toBirds = (a.flock - pos).normalized;
            look = toPlayer * 0.42f + toBirds * 0.58f;
            fov = s.wideFov - 4f * u;
        }
        else if (shot == 1)
        {
            // 2. Bandada: por delante de las aves y a un lado, viéndolas venir; se van acercando a la cámara
            Vector3 fd = a.flockDir.sqrMagnitude > 0.01f ? a.flockDir.normalized : -a.toFlock;
            Vector3 fside = Vector3.Cross(up, fd);
            fside = (fside.sqrMagnitude > 0.001f ? fside.normalized : side) * a.sideSign;
            float ahead = Mathf.Lerp(s.flockAhead, s.flockAhead * 0.4f, u);
            pos = a.flock + fd * ahead + fside * s.flockSide + up * s.flockUp;
            pos = KeepAboveGround(pos, a.lake, 3f);
            look = a.flock + fd * 3f - pos;
            float dist = Mathf.Max(1f, look.magnitude);
            float frame = 2f * Mathf.Atan((a.flockRadius + 4f) / dist) * Mathf.Rad2Deg;
            fov = Mathf.Clamp(frame, s.flockFov * 0.75f, s.flockFov * 1.3f);
            roll = s.flockRoll * a.sideSign * (1f - u * 0.5f);
        }
        else
        {
            // 3. Sobre el hombro: detrás del jugador mirando a la bandada, acercándose
            float b = s.shoulderBack - s.shoulderPush * u;
            pos = a.player - a.toFlock * b + side * s.shoulderSide + up * s.shoulderUp;
            if (a.lake != null) pos.y = Mathf.Max(pos.y, a.lake.SurfaceY + 1.5f);
            pos = KeepAboveGround(pos, a.lake, 1.5f);
            look = a.flock - pos;
            // Zoom que encuadra a la bandada (más cerrado cuando están lejos)
            fov = Mathf.Clamp(2f * Mathf.Atan(14f / Mathf.Max(1f, look.magnitude)) * Mathf.Rad2Deg, 24f, 60f);
        }

        rot = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look.normalized, up) : Quaternion.LookRotation(a.toFlock, up);
        if (roll != 0f) rot = rot * Quaternion.Euler(0f, 0f, roll);
    }

    static Vector3 KeepAboveGround(Vector3 p, LakeVolume lake, float margin)
    {
        if (lake == null) return p;
        p.y = Mathf.Max(p.y, lake.GroundHeight(p.x, p.z) + margin);
        return p;
    }

    // ======================= BANDADA =======================

    int CountBirds()
    {
        int n = 0;
        if (birds != null)
            for (int i = 0; i < birds.Count; i++)
                if (birds[i] != null) n++;
        return n;
    }

    Vector3 FlockCenter(out float radius)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        radius = 4f;
        if (birds != null)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                BirdAI b = birds[i];
                if (b == null) continue;
                sum += b.transform.position;
                n++;
            }
        }
        if (n > 0)
        {
            Vector3 c = sum / n;
            float r = 0f;
            for (int i = 0; i < birds.Count; i++)
                if (birds[i] != null) r = Mathf.Max(r, (birds[i].transform.position - c).magnitude);
            radius = Mathf.Max(3f, r);
            return c;
        }
        if (lake != null) return lake.Center + Vector3.up * 20f;
        return player != null ? player.transform.position + player.transform.forward * 50f + Vector3.up * 20f : Vector3.zero;
    }

    // Hacia dónde vuela la bandada (promedio de velocidades); si están quietas, hacia el jugador
    Vector3 FlockDirection(Vector3 flock)
    {
        Vector3 v = Vector3.zero;
        if (birds != null)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                if (birds[i] == null) continue;
                Rigidbody brb = birds[i].GetComponent<Rigidbody>();
                if (brb != null) v += brb.linearVelocity;
            }
        }
        if (v.sqrMagnitude > 1f) return v.normalized;
        Vector3 to = anchorPlayer - flock;
        return to.sqrMagnitude > 0.01f ? to.normalized : -toFlock;
    }

    // ======================= RÓTULO =======================

    // "4 × GAVIOTA  ·  2 × CHARRÁN" (o el nombre del jefe)
    void BuildCaption()
    {
        caption = "";
        captionTitle = "";
        if (!settings.showCaption || birds == null) return;
        List<string> order = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>();
        string boss = null;
        for (int i = 0; i < birds.Count; i++)
        {
            BirdAI b = birds[i];
            if (b == null) continue;
            string n = b.Type != null && !string.IsNullOrEmpty(b.Type.name) ? b.Type.name.ToUpper() : "AVE";
            if (b.IsBoss)
            {
                boss = n;
                continue;
            }
            if (!counts.ContainsKey(n))
            {
                counts[n] = 0;
                order.Add(n);
            }
            counts[n]++;
        }
        List<string> parts = new List<string>();
        foreach (string n in order) parts.Add(counts[n] + " × " + n);
        if (boss != null)
        {
            captionTitle = "JEFE DETECTADO";
            caption = boss + (parts.Count > 0 ? "   +   " + string.Join("  ·  ", parts.ToArray()) : "");
        }
        else if (parts.Count > 0)
        {
            captionTitle = "AVES DETECTADAS";
            caption = string.Join("  ·  ", parts.ToArray());
        }
    }

    // Aparece con el plano de la bandada y se va a mitad del plano sobre el hombro
    void UpdateCaption(float dt)
    {
        bool show = !string.IsNullOrEmpty(caption) && back < 0f && t >= shotEnd[0] && t < Mathf.Lerp(shotEnd[1], shotEnd[2], 0.5f);
        captionAlpha = Mathf.MoveTowards(captionAlpha, show ? 1f : 0f, dt * 4f);
    }

    // Devuelve los controles y quita las barras (la cámara ya está en la vista normal)
    void Finish()
    {
        if (finished) return;
        finished = true;
        // Si ya empezó otra cinemática, es ella la que manda
        if (active != null && active != this) return;
        if (player != null) player.SetCinematicLock(false);
        if (letterbox != null) letterbox(false);
    }

    void OnDestroy()
    {
        if (active == this) active = null;
        Finish();
    }
}
