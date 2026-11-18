using UnityEngine;

// Pez defensor (aparece desde el nivel 10 y se suman 2 cada 3 niveles; lo crea el GameDirector).
// Es un pez de otro color con un traje gris y blanco (Traje_Defensor) y un cañón de agua en la espalda:
//  - No vuela: nada rápido cerca de la superficie, sin hundirse mucho, patrullando su sector del lago. Si un ave
//    está cazando a un pez cerca, va hacia ese pez para defenderlo (FishAI > "Patrullar y defender").
//  - Dispara a las aves que lo siguen o que cazan peces cerca (apunta adelante del ave, como el jugador).
//  - Lo pueden atrapar, pero la descarga del traje hace que el ave lo suelte al instante, salga despedida y quede
//    aturdida un momento. Cada vez el traje pierde vida; cuando se rompe, queda como un pez normal (huye y se
//    puede comer como los demás).
[RequireComponent(typeof(FishAI))]
public class DefenderFish : MonoBehaviour
{
    [Header("Traje")]
    public float suitHealth = 100f;
    [Tooltip("Traje que pierde cada vez que un ave lo atrapa")]
    public float damagePerCatch = 34f;

    [Header("Patrulla")]
    [Tooltip("Velocidad de nado (los peces normales pasean a unos 3 m/s)")]
    public float patrolSpeed = 8.5f;
    [Tooltip("Profundidad (min, max) a la que nada: cerca de la superficie")]
    public Vector2 patrolDepth = new Vector2(0.35f, 0.7f);
    [Tooltip("Distancia a la que va a defender a un pez que estén cazando")]
    public float escortRange = 20f;

    [Header("Disparo")]
    public float range = 26f;
    public float fireInterval = 0.6f;
    public int damage = 6;
    public float shotSpeed = 90f;
    public Color shotColor = new Color(0.55f, 1f, 0.95f, 1f);

    public bool SuitIntact { get; private set; } = true;
    public float Suit01 { get { return maxSuit > 0f ? Mathf.Clamp01(suitHealth / maxSuit) : 0f; } }
    // Aviso al GameDirector (para el mensaje en pantalla)
    public event System.Action<DefenderFish> SuitBroken;

    FishAI fish;
    LakeVolume lake;
    GameObject projectilePrefab, impactPrefab;
    Material trailMaterial, sparkMaterial;
    Transform suit;
    float bodyScale = 1f;
    float maxSuit;
    int sector, sectorCount = 1;

    Vector3 patrolPoint;
    float patrolTimer;
    BirdAI target;
    float retargetTimer;
    float fireTimer;
    ParticleSystem sparks;
    Light zapLight;

    // Lo llama el GameDirector justo después de crear el pez
    public void Setup(GameObject suitModel, float scale, GameObject projectile, GameObject impact, Material fxTemplate,
                      int sectorIndex, int sectors)
    {
        fish = GetComponent<FishAI>();
        fish.Defender = this;
        bodyScale = Mathf.Max(0.1f, scale);
        projectilePrefab = projectile;
        impactPrefab = impact;
        sector = sectorIndex;
        sectorCount = Mathf.Max(1, sectors);
        maxSuit = suitHealth;
        trailMaterial = FXFactory.ParticleMaterial(fxTemplate, FXFactory.SoftDot, Color.white);
        sparkMaterial = FXFactory.ParticleMaterial(fxTemplate, FXFactory.SoftDot, Color.white);
        AttachSuit(suitModel);
        fireTimer = Random.Range(0.3f, 1f);
    }

    // El traje se arma alrededor del pez de referencia (PezNormal con escala 28): va dentro del "Visual" del pez,
    // así sigue sus giros (por ejemplo, panza arriba cuando lo aturden)
    void AttachSuit(GameObject model)
    {
        if (model == null) return;
        GameObject inst = Instantiate(model, transform, false);
        inst.name = "TrajeDefensor";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * bodyScale;
        foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
        if (fish.visual != null) inst.transform.SetParent(fish.visual, true);
        suit = inst.transform;
    }

    void Start()
    {
        lake = LakeVolume.Instance;
    }

    // ======================= PATRULLA =======================

    // A dónde nada ahora: hacia un pez que estén cazando cerca o a un punto de su sector
    public Vector3 PatrolPoint()
    {
        if (lake == null) lake = LakeVolume.Instance;
        Vector3 pos = transform.position;
        if (lake == null) return pos + transform.forward * 3f;

        if (target != null && target.IsAlive && target.TargetFish != null && target.TargetFish != fish)
        {
            Vector3 prey = target.TargetFish.transform.position;
            if ((prey - pos).sqrMagnitude < escortRange * escortRange)
            {
                prey.y = Mathf.Min(prey.y, lake.SurfaceY - patrolDepth.x);
                return prey;
            }
        }

        patrolTimer -= Time.fixedDeltaTime;
        if (patrolTimer <= 0f || (patrolPoint - pos).sqrMagnitude < 4f)
        {
            patrolTimer = Random.Range(5f, 8f);
            float a0 = sector / (float)sectorCount * Mathf.PI * 2f;
            float a1 = (sector + 1) / (float)sectorCount * Mathf.PI * 2f;
            Vector3 p = pos;
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(a0, a1);
                float r = lake.Radius * Random.Range(0.25f, 0.85f);
                Vector3 guess = lake.Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (lake.TryGetSwimPoint(guess, 6f, patrolDepth.x, patrolDepth.y, 0.6f, out p)) break;
                p = pos;
            }
            patrolPoint = p;
        }
        return patrolPoint;
    }

    // ======================= DISPARO =======================

    void Update()
    {
        if (!SuitIntact || fish == null || fish.State != FishAI.FishState.Swimming || lake == null || PauseMenu.IsPaused) return;
        float dt = Time.deltaTime;

        retargetTimer -= dt;
        if (retargetTimer <= 0f)
        {
            retargetTimer = 0.25f;
            target = FindTarget();
        }

        fireTimer -= dt;
        if (target != null && fireTimer <= 0f && !GameDirector.InCinematic)
        {
            Fire(target);
            fireTimer = fireInterval * Random.Range(0.85f, 1.15f);
        }

        if (zapLight != null) zapLight.intensity = Mathf.MoveTowards(zapLight.intensity, 0f, dt * 40f);
    }

    // Prefiere a las aves que lo cazan a él, después a las que cazan peces cerca y a las que vuelan bajo y cerca
    BirdAI FindTarget()
    {
        Vector3 pos = transform.position;
        BirdAI best = null;
        float bestScore = float.MaxValue;
        foreach (BirdAI b in BirdAI.All)
        {
            if (b == null || !b.IsAlive || b.IsArriving) continue;
            Vector3 bp = b.transform.position;
            float dist = Vector3.Distance(pos, bp);
            if (dist > range) continue;
            bool huntsMe = b.TargetFish == fish;
            bool huntsNear = b.TargetFish != null && (b.TargetFish.transform.position - pos).sqrMagnitude < escortRange * escortRange;
            bool low = bp.y - lake.SurfaceY < 14f;
            if (!huntsMe && !huntsNear && !(low && dist < range * 0.6f)) continue;
            float score = dist - (huntsMe ? 15f : 0f) - (huntsNear ? 8f : 0f);
            if (score < bestScore)
            {
                bestScore = score;
                best = b;
            }
        }
        return best;
    }

    Vector3 Muzzle()
    {
        // Boca del cañón: sobre la espalda, adelante (en el pez de referencia: 0,6 m arriba y 0,5 m adelante)
        Vector3 p = transform.position + transform.up * 0.6f * bodyScale + transform.forward * 0.5f * bodyScale;
        if (lake != null) p.y = Mathf.Max(p.y, lake.SurfaceY + 0.25f);   // dispara desde la superficie
        return p;
    }

    void Fire(BirdAI bird)
    {
        if (projectilePrefab == null) return;
        Vector3 muzzle = Muzzle();
        Vector3 aim = Lead(bird, muzzle);
        Vector3 dir = aim - muzzle;
        if (dir.sqrMagnitude < 0.01f) return;
        dir.Normalize();

        GameObject shot = Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(dir));
        shot.transform.localScale *= 0.6f;
        Projectile proj = shot.GetComponent<Projectile>();
        if (proj != null) proj.Setup(null, damage, impactPrefab, shotColor, trailMaterial, false);
        Rigidbody rb = shot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = dir * shotSpeed;
        }

        Sfx.Play(Sonido.Disparo, muzzle, 0.45f, 1.35f, 6f, 0.08f, 0.05f);
        EmitSparks(muzzle, dir, 6, shotColor, 3f);
    }

    // Dónde va a estar el ave cuando llegue la bala
    Vector3 Lead(BirdAI bird, Vector3 from)
    {
        Vector3 pos = bird.transform.position;
        Rigidbody brb = bird.GetComponent<Rigidbody>();
        if (brb == null) return pos;
        float t = Vector3.Distance(from, pos) / Mathf.Max(10f, shotSpeed);
        return pos + brb.linearVelocity * Mathf.Min(t, 1f);
    }

    // ======================= LO ATRAPAN =======================

    // Lo llama FishAI.Grab: true = se soltó (la descarga del traje hace que el ave lo suelte)
    public bool ShakeOff(Transform talons)
    {
        if (!SuitIntact) return false;
        suitHealth -= damagePerCatch;
        BirdAI bird = talons != null ? talons.GetComponentInParent<BirdAI>() : null;
        if (bird != null) bird.Repel(transform.position, 0.9f);
        Vector3 at = transform.position + Vector3.up * 0.3f;
        EmitSparks(at, Vector3.up, 26, new Color(0.6f, 0.95f, 1f, 1f), 7f);
        Flash(at, new Color(0.5f, 0.9f, 1f), 9f);
        Sfx.Play(Sonido.GolpeBlindado, at, 0.9f, 1.25f, 8f, 0.06f, 0.1f);
        if (suitHealth <= 0f) BreakSuit();
        return true;
    }

    // El traje se rompe: salta en pedazos y queda como un pez normal
    void BreakSuit()
    {
        SuitIntact = false;
        target = null;
        Vector3 at = suit != null ? suit.position : transform.position;
        EmitSparks(at, Vector3.up, 40, new Color(0.85f, 0.88f, 0.9f, 1f), 6f);
        Flash(at, new Color(1f, 0.95f, 0.85f), 12f);
        Sfx.Play(Sonido.Explosion, at, 0.6f, 1.6f, 8f, 0.05f, 0.1f);
        LakeWater.Splash(new Vector3(at.x, lake != null ? lake.SurfaceY : at.y, at.z), 0.6f, false);
        if (suit != null) Destroy(suit.gameObject);
        suit = null;
        SuitBroken?.Invoke(this);
    }

    // ======================= EFECTOS =======================

    void EmitSparks(Vector3 at, Vector3 dir, int count, Color color, float speed)
    {
        if (sparks == null)
        {
            sparks = FXFactory.CreateParticleSystem("ChispasDefensor", transform, sparkMaterial);
            var main = sparks.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.gravityModifier = 0.5f;
            main.maxParticles = 300;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            sparks.Play();
        }
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = at;
            ep.velocity = (dir.normalized + Random.insideUnitSphere * 0.9f) * speed * Random.Range(0.5f, 1.2f);
            ep.startColor = Color.Lerp(color, Color.white, Random.value * 0.5f);
            sparks.Emit(ep, 1);
        }
    }

    void Flash(Vector3 at, Color color, float intensity)
    {
        if (zapLight == null)
        {
            GameObject go = new GameObject("DescargaTraje");
            go.transform.SetParent(transform, false);
            zapLight = go.AddComponent<Light>();
            zapLight.type = LightType.Point;
            zapLight.range = 6f;
            zapLight.shadows = LightShadows.None;
        }
        zapLight.transform.position = at;
        zapLight.color = color;
        zapLight.intensity = intensity;
    }
}
