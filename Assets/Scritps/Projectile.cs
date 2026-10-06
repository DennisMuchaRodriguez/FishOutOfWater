using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 10;
    public float lifeTime = 3f;

    [Header("Efectos - NUEVO")]
    public GameObject impactEffectPrefab;
    public GameObject waterSplashPrefab;
    [Tooltip("Al entrar al agua el proyectil se frena a este factor")]
    public float waterSlowdown = 0.55f;

    private PlayerShooting owner;
    private bool isCharged;
    private bool hasHit;
    private bool inWater;
    private Color color = new Color(0.35f, 0.9f, 1f, 1f);
    private Rigidbody rb;
    private Vector3 baseScale;

    // Lo llama PlayerShooting justo después de instanciar
    public void Setup(PlayerShooting shooter, int dmg, GameObject impactPrefab, GameObject splashPrefab,
                      Color shotColor, Material trailMaterial, bool charged)
    {
        owner = shooter;
        damage = dmg;
        if (impactPrefab != null) impactEffectPrefab = impactPrefab;
        if (splashPrefab != null) waterSplashPrefab = splashPrefab;
        color = shotColor;
        isCharged = charged;

        if (trailMaterial != null && GetComponent<TrailRenderer>() == null)
        {
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = charged ? 0.35f : 0.18f;
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            float w = (charged ? 0.45f : 0.18f);
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, w), new Keyframe(1f, 0f));
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(shotColor, 0.3f), new GradientColorKey(shotColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);

        baseScale = transform.localScale;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    void Update()
    {
        // Pequeño pulso de tamaño para que el disparo cargado se vea vivo
        if (isCharged)
        {
            float s = 1f + Mathf.Sin(Time.time * 30f) * 0.08f;
            transform.localScale = baseScale * s;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        ContactPoint contact = collision.GetContact(0);
        Damageable damageable = collision.collider.GetComponentInParent<Damageable>();
        if (damageable != null)
        {
            bool killed = damageable.TakeDamage(damage, contact.point);
            if (owner != null) owner.NotifyHit(killed);
        }

        // Empuje físico al objetivo
        Rigidbody other = collision.rigidbody;
        if (other != null && rb != null)
        {
            other.AddForceAtPosition(rb.linearVelocity.normalized * (isCharged ? 8f : 2f), contact.point, ForceMode.Impulse);
        }

        FXFactory.SpawnOneShot(impactEffectPrefab, contact.point + contact.normal * 0.05f,
                               Quaternion.LookRotation(contact.normal), isCharged ? 1.2f : 0.6f, 3f);

        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (inWater || !other.CompareTag("Water")) return;
        inWater = true;

        // Solo salpica si entra desde arriba (no cuando se dispara bajo el agua)
        if (transform.position.y < other.bounds.max.y - 0.5f) return;

        FXFactory.SpawnOneShot(waterSplashPrefab, transform.position, Quaternion.identity, isCharged ? 0.8f : 0.4f, 3f);
        if (rb != null) rb.linearVelocity *= waterSlowdown;
    }
}
