using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Cinemática de llegada de una oleada (estilo Star Fox: plano por encima del hombro
// siguiendo a la escuadrilla que llega desde el cielo).
//
// Es un objeto independiente con su propia cámara temporal:
//  - La cámara del jugador nunca se mueve; solo se apaga mientras dura.
//  - Al destruirse (termine bien, se salte, falle algo o se cambie de escena)
//    SIEMPRE devuelve la cámara y los controles al jugador.
//  - Tiene un tope de tiempo real: nunca puede quedarse pegada.
public class WaveCinematic : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    PlayerController_Base player;
    Camera playerCam;
    Camera cam;
    List<BirdAI> birds;
    LakeVolume lake;
    System.Action<bool> letterbox;

    float duration;
    float t;
    float back = -1f;
    float realTime;
    float maxRealTime;
    Vector3 startPos, shotPos, toFlock, fromPos;
    Quaternion startRot, fromRot;
    float startFov, fromFov;
    bool restored;

    const float ReturnTime = 0.7f;

    public static WaveCinematic Play(PlayerController_Base player, List<BirdAI> birds, LakeVolume lake,
                                     float duration, System.Action<bool> letterbox)
    {
        if (player == null || player.PlayerCamera == null) return null;
        GameObject go = new GameObject("CinematicaOleada");
        WaveCinematic c = go.AddComponent<WaveCinematic>();
        try
        {
            c.Init(player, birds, lake, duration, letterbox);
        }
        catch (System.Exception e)
        {
            // Si algo falla al empezar, no hay cinemática (y se devuelve todo al jugador)
            Debug.LogException(e);
            Destroy(go);
            return null;
        }
        return c;
    }

    // Por si el proyecto entra a Play sin recargar el dominio
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsPlaying = false; }

    void Init(PlayerController_Base p, List<BirdAI> wave, LakeVolume l, float d, System.Action<bool> lb)
    {
        player = p;
        playerCam = p.PlayerCamera;
        birds = wave;
        lake = l;
        duration = Mathf.Max(0.5f, d);
        letterbox = lb;
        maxRealTime = duration + ReturnTime + 4f;

        IsPlaying = true;
        player.SetCinematicLock(true);

        // Cámara propia (copia los ajustes de la del jugador)
        cam = gameObject.AddComponent<Camera>();
        cam.CopyFrom(playerCam);
        cam.depth = playerCam.depth + 1f;
        try
        {
            var src = playerCam.GetUniversalAdditionalCameraData();
            var dst = cam.GetUniversalAdditionalCameraData();
            dst.renderPostProcessing = src.renderPostProcessing;
            dst.antialiasing = src.antialiasing;
            dst.volumeLayerMask = src.volumeLayerMask;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("WaveCinematic: no se pudieron copiar los ajustes de URP: " + e.Message);
        }
        playerCam.enabled = false;

        startPos = cam.transform.position;
        startRot = cam.transform.rotation;
        startFov = cam.fieldOfView;

        Vector3 flock = FlockCenter();
        toFlock = flock - player.transform.position;
        toFlock.y = 0f;
        toFlock = toFlock.sqrMagnitude > 0.01f ? toFlock.normalized : player.transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toFlock);
        shotPos = player.transform.position - toFlock * 5f + side * 2.5f + Vector3.up * 3.5f;
        if (lake != null)
            shotPos.y = Mathf.Max(shotPos.y, lake.SurfaceY + 1.5f, lake.GroundHeight(shotPos.x, shotPos.z) + 1.5f);

        if (letterbox != null) letterbox(true);
    }

    void LateUpdate()
    {
        // Tope de seguridad en tiempo real (no cuenta mientras el juego está en pausa)
        if (!PauseMenu.IsPaused) realTime += Time.unscaledDeltaTime;
        if (realTime > maxRealTime || player == null || playerCam == null || cam == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        Transform ct = cam.transform;

        if (back < 0f)
        {
            // --- Plano siguiendo a la escuadrilla ---
            t += dt;
            Vector3 flock = FlockCenter();
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.9f));
            Vector3 pos = Vector3.Lerp(startPos, shotPos + toFlock * t * 0.4f, blend);
            Vector3 lookDir = flock - pos;
            Quaternion look = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir.normalized, Vector3.up) : ct.rotation;
            ct.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, look, blend));
            float frameFov = Mathf.Clamp(2f * Mathf.Atan(14f / Mathf.Max(1f, lookDir.magnitude)) * Mathf.Rad2Deg, 22f, 60f);
            cam.fieldOfView = Mathf.Lerp(startFov, frameFov, blend);

            bool skip = t > 0.6f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));
            if (t >= duration || skip)
            {
                back = 0f;
                fromPos = ct.position;
                fromRot = ct.rotation;
                fromFov = cam.fieldOfView;
            }
        }
        else
        {
            // --- Regreso suave a la vista del jugador (que se sigue actualizando por debajo) ---
            back += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(back / ReturnTime));
            ct.SetPositionAndRotation(Vector3.Lerp(fromPos, playerCam.transform.position, k),
                                      Quaternion.Slerp(fromRot, playerCam.transform.rotation, k));
            cam.fieldOfView = Mathf.Lerp(fromFov, playerCam.fieldOfView, k);
            if (back >= ReturnTime) Destroy(gameObject);
        }
    }

    Vector3 FlockCenter()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        if (birds != null)
        {
            foreach (BirdAI b in birds)
            {
                if (b == null) continue;
                sum += b.transform.position;
                n++;
            }
        }
        if (n > 0) return sum / n;
        return lake != null ? lake.Center + Vector3.up * 20f : transform.position + Vector3.forward * 50f;
    }

    void Restore()
    {
        if (restored) return;
        restored = true;
        IsPlaying = false;
        if (playerCam != null) playerCam.enabled = true;
        if (player != null) player.SetCinematicLock(false);
        if (letterbox != null) letterbox(false);
    }

    void OnDisable() { Restore(); }
    void OnDestroy() { Restore(); }
}
