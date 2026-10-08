using UnityEngine;

// Pon este componente en cualquier objeto que deba salpicar al entrar o salir del agua.
// El jugador, los peces, las aves y las balas lo reciben automáticamente.
public class WaterInteractor : MonoBehaviour
{
    [Tooltip("Tamaño aproximado del objeto: escala la salpicadura")]
    public float size = 1f;
    [Tooltip("Velocidad mínima (m/s) para que salpique al cruzar la superficie")]
    public float minSpeed = 1.2f;
    [Tooltip("Hace ondas mientras se mueve rozando la superficie")]
    public bool wake = true;
    public float wakeInterval = 0.3f;
    [Tooltip("Velocidad horizontal mínima para dejar ondas")]
    public float wakeMinSpeed = 2.5f;

    public bool IsUnderwater { get; private set; }

    Rigidbody rb;
    Vector3 lastPos;
    bool initialized;
    float wakeTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        initialized = false;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 pos = rb != null ? rb.position : transform.position;
        Vector3 vel;
        if (rb != null && !rb.isKinematic) vel = rb.linearVelocity;
        else vel = initialized ? (pos - lastPos) / dt : Vector3.zero;

        float surface;
        bool over = LakeWater.TryGetSurface(pos, out surface);
        bool under = over && pos.y < surface;

        if (initialized && over && under != IsUnderwater)
        {
            float speed = Mathf.Abs(vel.y) + new Vector2(vel.x, vel.z).magnitude * 0.25f;
            if (speed >= minSpeed)
            {
                float strength = Mathf.Clamp01(speed / 18f) * Mathf.Clamp(size, 0.2f, 2f);
                LakeWater.Splash(new Vector3(pos.x, surface, pos.z), Mathf.Clamp(strength, 0.12f, 1f), under);
            }
        }

        // Ondas al moverse rozando la superficie
        if (wake && initialized && over)
        {
            float hs = new Vector2(vel.x, vel.z).magnitude;
            if (Mathf.Abs(pos.y - surface) < size * 0.6f && hs > wakeMinSpeed)
            {
                wakeTimer -= dt;
                if (wakeTimer <= 0f)
                {
                    wakeTimer = wakeInterval;
                    LakeWater.Wake(new Vector3(pos.x, surface, pos.z), Mathf.Clamp01(hs / 15f) * size * 0.5f);
                }
            }
        }

        IsUnderwater = under;
        lastPos = pos;
        initialized = true;
    }
}
