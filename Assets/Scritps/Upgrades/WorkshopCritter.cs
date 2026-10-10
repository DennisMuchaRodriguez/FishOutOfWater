using UnityEngine;

// Personajes del Taller que se mueven solos:
//  - Mecánico: se mece despacio en su sitio; durante la instalación da saltitos y mira el traje.
//  - Alevín: nada en círculos alrededor del traje, subiendo y bajando.
public class WorkshopCritter : MonoBehaviour
{
    public enum Kind { Mechanic, Fry }
    public Kind kind = Kind.Fry;

    [Header("Alevín")]
    public Vector3 center;
    public float radius = 1.5f;
    public float height = 1.5f;
    public float speed = 0.5f;
    [Tooltip("1 = antihorario visto desde arriba, -1 = horario")]
    public float direction = 1f;

    [Header("Mecánico")]
    public Transform lookTarget;

    // 0..1: más rápido y contento (instalación)
    public float excitement;

    Vector3 basePosition;
    Quaternion baseRotation;
    float phase, angle;
    Animator anim;

    void Start()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;
        phase = Random.Range(0f, 10f);
        angle = Random.Range(0f, Mathf.PI * 2f);
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time + phase;
        if (kind == Kind.Fry) Swim(t, dt);
        else Bob(t, dt);
        if (anim != null) anim.speed = 1f + excitement * 1.2f;
    }

    void Swim(float t, float dt)
    {
        angle += dt * speed * direction * (1f + excitement * 1.5f);
        Vector3 p = OrbitPoint(angle, t);
        Vector3 ahead = OrbitPoint(angle + 0.15f * direction, t + 0.15f);
        transform.position = p;
        Vector3 fwd = ahead - p;
        if (fwd.sqrMagnitude > 0.000001f)
        {
            // Se inclina un poco hacia dentro de la curva, como un pez de verdad
            Quaternion look = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            transform.rotation = look * Quaternion.Euler(0f, 0f, -12f * direction + Mathf.Sin(t * 3f) * 6f);
        }
    }

    Vector3 OrbitPoint(float a, float t)
    {
        float r = radius + Mathf.Sin(t * 0.7f) * 0.25f;
        float y = height + Mathf.Sin(t * 1.3f) * 0.22f + Mathf.Sin(a * 2f) * 0.15f;
        return center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
    }

    void Bob(float t, float dt)
    {
        float hop = Mathf.Abs(Mathf.Sin(t * 7f)) * 0.08f * excitement;
        transform.position = basePosition + Vector3.up * (Mathf.Sin(t * 1.6f) * 0.035f + hop);

        Quaternion target = baseRotation;
        if (lookTarget != null && excitement > 0.01f)
        {
            Vector3 to = lookTarget.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                target = Quaternion.Slerp(baseRotation, Quaternion.LookRotation(to.normalized, Vector3.up), Mathf.Clamp01(excitement));
        }
        Quaternion sway = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 3f, Mathf.Sin(t * 0.6f) * 7f, Mathf.Sin(t * 1.3f) * 2.5f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target * sway, 1f - Mathf.Exp(-6f * dt));
    }
}
