using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 10;
    public float lifeTime = 3f;
    [Tooltip("Aves que atraviesa antes de destruirse (mejora Disparo perforante)")]
    public int pierceCount = 0;

    [Header("Efectos - NUEVO")]
    public GameObject impactEffectPrefab;
    [Tooltip("Al entrar al agua el proyectil se frena a este factor")]
    public float waterSlowdown = 0.55f;

    private PlayerShooting owner;
    private bool isCharged;
    private bool isBubble;
    private bool hasHit;
    private bool inWater;
    private Color color = new Color(0.35f, 0.9f, 1f, 1f);
    private Rigidbody rb;
    private Collider ownCollider;
    private Vector3 baseScale;
    private Vector3 lastVelocity;
    private ParticleSystem bubbleTrail;
    private float bubbleEmit;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
    }

    // Lo llama PlayerShooting justo después de instanciar
    public void Setup(PlayerShooting shooter, int dmg, GameObject impactPrefab,
                      Color shotColor, Material trailMaterial, bool charged)
    {
        owner = shooter;
        damage = dmg;
        if (impactPrefab != null) impactEffectPrefab = impactPrefab;
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

    // Metralleta de burbujas: la bala se vuelve una burbuja chica y rápida que deja burbujitas.
    // Lo llama PlayerShooting después de Setup.
    public void MakeBubble(Material bubbleMaterial, ParticleSystem trailBubbles)
    {
        isBubble = true;
        bubbleTrail = trailBubbles;

        // Sin la bola de agua, la luz ni el chapoteo del prefab (salen diez por segundo)
        foreach (MeshRenderer r in GetComponents<MeshRenderer>()) r.enabled = false;
        for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(false);

        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.time = 0.09f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(1f, 0f));
        }

        if (bubbleMaterial != null) FXBillboard.Create("Burbuja", transform, bubbleMaterial, 1.3f, 0.12f);
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);

        baseScale = transform.localScale;
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            lastVelocity = rb.linearVelocity;
        }

        // Salpica y hace anillos al cruzar la superficie
        WaterInteractor wi = gameObject.AddComponent<WaterInteractor>();
        wi.size = isCharged ? 0.7f : (isBubble ? 0.2f : 0.35f);
        wi.minSpeed = 1f;
        wi.wake = false;

        // Si se dispara bajo el agua no cuenta como "entrar"
        inWater = LakeWater.IsUnderwater(transform.position);
    }

    void Update()
    {
        // Pequeño pulso de tamaño para que el disparo cargado se vea vivo
        if (isCharged)
        {
            float s = 1f + Mathf.Sin(Time.time * 30f) * 0.08f;
            transform.localScale = baseScale * s;
        }

        // Estela de burbujitas (un solo sistema de partículas compartido por todas las balas)
        if (isBubble && bubbleTrail != null)
        {
            bubbleEmit += Time.deltaTime * 24f;
            while (bubbleEmit >= 1f)
            {
                bubbleEmit -= 1f;
                EmitBubble(transform.position + Random.insideUnitSphere * 0.08f, Random.insideUnitSphere * 0.5f);
            }
        }
    }

    void EmitBubble(Vector3 position, Vector3 velocity)
    {
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = position;
        ep.velocity = velocity;
        bubbleTrail.Emit(ep, 1);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        ContactPoint contact = collision.GetContact(0);
        Damageable damageable = collision.collider.GetComponentInParent<Damageable>();

        // Disparo perforante: atraviesa el ave, sigue con su velocidad y ya no choca con ella
        if (damageable != null && pierceCount > 0)
        {
            if (!damageable.IsDead)
            {
                bool hitKilled = damageable.TakeDamage(damage, contact.point);
                if (owner != null) owner.NotifyHit(hitKilled);
                pierceCount--;
                if (collision.rigidbody != null)
                {
                    collision.rigidbody.AddForceAtPosition(lastVelocity.normalized * 4f, contact.point, ForceMode.Impulse);
                }
                FXFactory.SpawnOneShot(impactEffectPrefab, contact.point + contact.normal * 0.05f,
                                       Quaternion.LookRotation(contact.normal), 0.9f, 3f);
            }
            if (ownCollider != null)
            {
                foreach (Collider c in damageable.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(ownCollider, c);
            }
            if (rb != null && lastVelocity.sqrMagnitude > 0.01f) rb.linearVelocity = lastVelocity;
            return;
        }

        hasHit = true;

        if (damageable != null)
        {
            bool killed = damageable.TakeDamage(damage, contact.point);
            if (owner != null) owner.NotifyHit(killed);
        }

        // Empuje físico al objetivo
        Rigidbody other = collision.rigidbody;
        if (other != null && rb != null)
        {
            other.AddForceAtPosition(rb.linearVelocity.normalized * (isCharged ? 8f : (isBubble ? 0.8f : 2f)), contact.point, ForceMode.Impulse);
        }

        FXFactory.SpawnOneShot(impactEffectPrefab, contact.point + contact.normal * 0.05f,
                               Quaternion.LookRotation(contact.normal), isCharged ? 1.2f : (isBubble ? 0.3f : 0.6f), 3f);

        // La burbuja revienta en burbujitas
        if (isBubble && bubbleTrail != null)
        {
            for (int i = 0; i < 8; i++)
            {
                EmitBubble(contact.point + contact.normal * 0.1f, (contact.normal + Random.insideUnitSphere) * 2.5f);
            }
        }

        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Al entrar al agua desde arriba se frena
        if (!inWater && LakeWater.IsUnderwater(transform.position))
        {
            inWater = true;
            rb.linearVelocity *= waterSlowdown;
        }

        // Velocidad antes del paso de física (la usa el disparo perforante para seguir de largo)
        lastVelocity = rb.linearVelocity;
    }
}
