using System.Collections.Generic;
using UnityEngine;

// Cinemática de llegada de una oleada (estilo Star Fox: plano por encima del hombro
// siguiendo a la escuadrilla que llega desde el cielo).
//
// NO crea, apaga ni cambia cámaras. Solo calcula cada frame una "pose" (posición,
// rotación, FOV) y un peso 0..1. La cámara del jugador (PlayerController_Base.LateUpdate)
// mezcla su vista normal con esa pose según el peso:
//   peso 0 -> vista normal del jugador     peso 1 -> plano de la cinemática
// Si este objeto desaparece por cualquier motivo (termina, se salta, se destruye,
// cambia la escena, hay un error...), en el siguiente frame la cámara vuelve sola a la
// vista normal: no hay nada que "restaurar" y no se puede quedar trabada.
public class WaveCinematic : MonoBehaviour
{
    static WaveCinematic active;

    // True mientras hay una cinemática en curso (el HUD se oculta y el jugador no se mueve).
    // Si por lo que sea la cinemática deja de actualizarse medio segundo, se da por terminada.
    public static bool IsPlaying { get { return active != null && Time.unscaledTime - active.lastTick < 0.5f; } }

    PlayerController_Base player;
    List<BirdAI> birds;
    LakeVolume lake;
    System.Action<bool> letterbox;

    float duration;
    float t;          // tiempo del plano (no avanza en pausa)
    float back = -1f; // >= 0: volviendo a la vista del jugador
    float life;       // tiempo real total (tope de seguridad)
    float weight;
    float lastTick;
    Vector3 shotPos, toFlock, posePos;
    Quaternion poseRot = Quaternion.identity;
    float poseFov = 60f;
    bool finished;

    const float BlendIn = 0.9f;
    const float BlendOut = 0.7f;

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
                                     float duration, System.Action<bool> letterbox)
    {
        if (player == null) return null;
        if (active != null) Destroy(active.gameObject);

        GameObject go = new GameObject("CinematicaOleada");
        WaveCinematic c = go.AddComponent<WaveCinematic>();
        try
        {
            c.Init(player, birds, lake, duration, letterbox);
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

    void Init(PlayerController_Base p, List<BirdAI> wave, LakeVolume l, float d, System.Action<bool> lb)
    {
        player = p;
        birds = wave;
        lake = l;
        duration = Mathf.Max(0.5f, d);
        letterbox = lb;

        Vector3 flock = FlockCenter();
        toFlock = flock - player.transform.position;
        toFlock.y = 0f;
        toFlock = toFlock.sqrMagnitude > 0.01f ? toFlock.normalized : player.transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toFlock);
        shotPos = player.transform.position - toFlock * 5f + side * 2.5f + Vector3.up * 3.5f;
        if (lake != null)
            shotPos.y = Mathf.Max(shotPos.y, lake.SurfaceY + 1.5f, lake.GroundHeight(shotPos.x, shotPos.z) + 1.5f);
        ComputePose(flock);

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
        ComputePose(FlockCenter());

        if (back < 0f)
        {
            weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / BlendIn));
            bool skip = t > 0.6f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));
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

    void ComputePose(Vector3 flock)
    {
        posePos = shotPos + toFlock * t * 0.4f;
        Vector3 lookDir = flock - posePos;
        if (lookDir.sqrMagnitude > 0.01f) poseRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        // Zoom que encuadra a la bandada (más cerrado cuando están lejos)
        poseFov = Mathf.Clamp(2f * Mathf.Atan(14f / Mathf.Max(1f, lookDir.magnitude)) * Mathf.Rad2Deg, 22f, 60f);
    }

    Vector3 FlockCenter()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
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
        if (n > 0) return sum / n;
        if (lake != null) return lake.Center + Vector3.up * 20f;
        return player != null ? player.transform.position + player.transform.forward * 50f + Vector3.up * 20f : Vector3.zero;
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
