using UnityEngine;

// Esfera de munición que flota en el lago (o un poco sumergida). Al tocarla, el cargador se llena entero.
// Se construye por código y se ve como uno de los disparos de agua del pez, pero grande, brillante y celeste
// (energía para disparar):
//  - la esfera de agua con el mismo material del disparo (Bullet.prefab), cubierta por una capa de energía celeste
//    que late;
//  - un halo de luz alrededor y una luz celeste;
//  - tres chispas de energía que giran en órbitas inclinadas dejando una estela;
//  - chispas que suben.
// En cooperativo se agarra desde un poco más lejos.
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
    // Cooperativo: número de la cápsula (lo da el anfitrión)
    [System.NonSerialized] public int NetId = -1;

    // Cooperativo: la recogió otro jugador
    public void NetRemove(bool effect)
    {
        if (effect) FXFactory.SpawnOneShot(collectPrefab, transform.position, Quaternion.identity, 0.5f, 3f);
        Destroy(gameObject);
    }

    // Mejora "Imán de cápsulas": desde pickupRadius × MagnetMultiplier la cápsula vuela hacia el jugador
    public static float MagnetMultiplier = 1f;
    public float magnetSpeed = 12f;
    static PlayerShooting magnetTarget;
    public bool IsUnderwater { get; set; }

    const float ShellSize = 0.8f;
    const float OrbitRadius = 0.72f;

    Transform shell;
    Transform aura;
    Transform halo;
    readonly Transform[] sparks = new Transform[3];
    readonly Vector3[] sparkAxis = new Vector3[3];
    readonly float[] sparkSpeed = new float[3];
    Light glow;
    Vector3 basePos;
    float phase;

    // El material del disparo de agua del pez (se busca una vez)
    static Material shotMaterial;
    static bool shotSearched;

    static Material ShotMaterial()
    {
        if (shotSearched) return shotMaterial;
        shotSearched = true;
        PlayerShooting s = FindFirstObjectByType<PlayerShooting>();
        if (s != null && s.projectilePrefab != null)
        {
            MeshRenderer mr = s.projectilePrefab.GetComponentInChildren<MeshRenderer>();
            if (mr != null) shotMaterial = mr.sharedMaterial;
        }
        return shotMaterial;
    }

    public void Build(Material template)
    {
        basePos = transform.position;
        phase = Random.Range(0f, 10f);

        Material energy = FXFactory.ParticleMaterial(template, FXFactory.White, new Color(color.r, color.g, color.b, 0.5f));
        Material bright = FXFactory.ParticleMaterial(template, FXFactory.White, new Color(0.8f, 1f, 1f, 1f));
        Material glowMat = FXFactory.ParticleMaterial(template, FXFactory.SoftDot, new Color(color.r, color.g, color.b, 0.9f));

        // La esfera de agua (como el disparo) y su capa de energía celeste
        Material water = ShotMaterial();
        shell = CreatePrimitive(PrimitiveType.Sphere, "Esfera", water != null ? water : energy, Vector3.one * ShellSize);
        aura = CreatePrimitive(PrimitiveType.Sphere, "Energia", energy, Vector3.one * ShellSize * 1.08f);
        if (water == null)
        {
            // Sin el material del disparo: un núcleo blanco adentro de la energía
            CreatePrimitive(PrimitiveType.Sphere, "Nucleo", bright, Vector3.one * ShellSize * 0.55f);
        }

        // Halo de luz alrededor (se ve por fuera de la esfera)
        halo = FXBillboard.Create("Halo", transform, glowMat, 2.6f, 0f).transform;
        FXBillboard.Create("Resplandor", transform, glowMat, 4.2f, 0f);

        // Tres chispas de energía en órbitas inclinadas, con estela
        for (int i = 0; i < sparks.Length; i++)
        {
            Transform sp = CreatePrimitive(PrimitiveType.Sphere, "Chispa" + i, bright, Vector3.one * 0.12f);
            TrailRenderer tr = sp.gameObject.AddComponent<TrailRenderer>();
            tr.sharedMaterial = energy;
            tr.time = 0.35f;
            tr.startWidth = 0.09f;
            tr.endWidth = 0f;
            tr.minVertexDistance = 0.03f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            sparks[i] = sp;
            sparkAxis[i] = Quaternion.Euler(Random.Range(-60f, 60f), i * 120f, Random.Range(-60f, 60f)) * Vector3.up;
            sparkSpeed[i] = Random.Range(160f, 240f) * (i % 2 == 0 ? 1f : -1f);
        }

        GameObject lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(transform, false);
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 7f;
        glow.intensity = 3.5f;
        glow.shadows = LightShadows.None;

        // Chispas que suben
        Material sparkMat = FXFactory.ParticleMaterial(template, FXFactory.SoftDot, Color.white);
        ParticleSystem ps = FXFactory.CreateParticleSystem("Chispas", transform, sparkMat);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new Color(0.7f, 1f, 1f, 0.9f);
        main.gravityModifier = -0.15f;
        var e = ps.emission;
        e.rateOverTime = 12f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        ps.Play();

        if (CoopSession.Active) pickupRadius *= 1.4f;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        MagnetMultiplier = 1f;
        magnetTarget = null;
        shotMaterial = null;
        shotSearched = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        phase += dt;
        if (MagnetMultiplier > 1.01f) Attract(dt);
        transform.position = basePos + Vector3.up * Mathf.Sin(phase * bobSpeed) * bobAmplitude;

        // La esfera gira despacio y la energía late
        if (shell != null) shell.Rotate(0f, spinSpeed * 0.5f * dt, 0f, Space.World);
        float beat = Mathf.Sin(phase * 4f);
        if (aura != null) aura.localScale = Vector3.one * ShellSize * (1.08f + beat * 0.035f);
        if (halo != null) halo.localScale = Vector3.one * (1f + beat * 0.08f);
        if (glow != null) glow.intensity = 3.2f + beat * 0.9f;

        // Las chispas dan vueltas alrededor
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null) continue;
            Vector3 axis = sparkAxis[i];
            Vector3 start = Vector3.Cross(axis, i == 1 ? Vector3.forward : Vector3.right).normalized * OrbitRadius;
            sparks[i].localPosition = Quaternion.AngleAxis(phase * sparkSpeed[i], axis) * start;
        }
    }

    void Attract(float dt)
    {
        if (magnetTarget == null) magnetTarget = FindFirstObjectByType<PlayerShooting>();
        if (magnetTarget == null || !magnetTarget.enabled || !magnetTarget.WantsAmmo) return;
        Vector3 target = magnetTarget.transform.position;
        float range = pickupRadius * MagnetMultiplier;
        if ((target - basePos).sqrMagnitude > range * range) return;
        // Vuela hacia el jugador (el disparador de siempre la recoge al llegar)
        basePos = Vector3.MoveTowards(basePos, target, magnetSpeed * dt);
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerShooting shooting = other.GetComponentInParent<PlayerShooting>();
        if (shooting == null || !shooting.enabled) return;
        if (!shooting.WantsAmmo) return;

        // Llena el cargador entero (en todos los modos)
        shooting.AddAmmo(shooting.maxAmmo);
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
