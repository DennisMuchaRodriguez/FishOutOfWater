using UnityEngine;

// Cápsula de munición que flota en el lago (o un poco sumergida).
// Se construye por código: núcleo brillante, anillo que gira, luz y burbujas.
public class AmmoPickup : MonoBehaviour
{
    public int ammo = 10;
    public float bobAmplitude = 0.25f;
    public float bobSpeed = 1.6f;
    public float spinSpeed = 90f;
    public float pickupRadius = 1.8f;
    public Color color = new Color(0.35f, 0.95f, 1f, 1f);
    public GameObject collectPrefab;

    public System.Action<AmmoPickup> Collected;
    public bool IsUnderwater { get; set; }

    Transform core;
    Transform ring;
    Light glow;
    Vector3 basePos;
    float phase;

    public void Build(Material template)
    {
        basePos = transform.position;
        phase = Random.Range(0f, 10f);

        Material coreMat = FXFactory.ParticleMaterial(template, FXFactory.SoftDot, color);
        Material ringMat = FXFactory.ParticleMaterial(template, FXFactory.White, new Color(color.r, color.g, color.b, 0.6f));

        // Núcleo: dos esferas (interior blanco y halo cian)
        core = CreatePrimitive(PrimitiveType.Sphere, "Nucleo", coreMat, Vector3.one * 0.6f);
        Transform halo = CreatePrimitive(PrimitiveType.Sphere, "Halo", coreMat, Vector3.one * 1.3f);
        halo.SetParent(core, true);

        // Anillo
        ring = CreatePrimitive(PrimitiveType.Cylinder, "Anillo", ringMat, new Vector3(1.4f, 0.02f, 1.4f));
        ring.localRotation = Quaternion.Euler(75f, 0f, 0f);

        GameObject lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(transform, false);
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 6f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        // Burbujas / chispas alrededor
        Material sparkMat = FXFactory.ParticleMaterial(template, FXFactory.Bubble, Color.white);
        ParticleSystem ps = FXFactory.CreateParticleSystem("Burbujas", transform, sparkMat);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(color.r, color.g, color.b, 0.8f);
        main.gravityModifier = -0.1f;
        var e = ps.emission;
        e.rateOverTime = 8f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;
        ps.Play();

        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = pickupRadius;
    }

    Transform CreatePrimitive(PrimitiveType type, string name, Material mat, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    void Update()
    {
        phase += Time.deltaTime;
        transform.position = basePos + Vector3.up * Mathf.Sin(phase * bobSpeed) * bobAmplitude;
        if (ring != null) ring.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        if (core != null) core.localScale = Vector3.one * (0.6f + Mathf.Sin(phase * 4f) * 0.05f);
        if (glow != null) glow.intensity = 2.5f + Mathf.Sin(phase * 4f) * 0.8f;
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerShooting shooting = other.GetComponentInParent<PlayerShooting>();
        if (shooting == null || !shooting.enabled) return;
        if (shooting.currentAmmo >= shooting.maxAmmo) return;

        shooting.AddAmmo(ammo);
        FXFactory.SpawnOneShot(collectPrefab, transform.position, Quaternion.identity, 0.8f, 3f);
        Collected?.Invoke(this);
        Destroy(gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        // Si entraste con el cargador lleno y luego disparaste, también la recoge
        if (Time.frameCount % 10 == 0) OnTriggerEnter(other);
    }
}
