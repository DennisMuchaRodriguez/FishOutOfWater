using UnityEngine;

// Proyectiles de las aves y los jefes. No usan física: avanzan solos (con gravedad si se pide) y miran si
// tocan al jugador, el agua o el terreno.
//  - Escupitajo  (serreta, pelícano jefe): chorro de agua rápido y recto
//  - Caca        (gaviota): cae desde arriba; si te da en primera persona te ensucia el visor
//  - Pluma       (garza y reina): dardo rápido y afilado
//  - BolaDeAgua  (pelícano jefe): bola grande en arco que explota (empuja, y en el lago aturde peces)
// El escudo de burbuja del traje los absorbe como cualquier golpe (PlayerController_Base.TakeHit).
public class EnemyProjectile : MonoBehaviour
{
    public enum Kind { Escupitajo, Caca, Pluma, BolaDeAgua }

    const float PlayerRadius = 0.75f;
    const float Lifetime = 6f;

    Kind kind;
    Vector3 velocity;
    float gravity;
    float damage;
    float knockback;
    float radius;
    float blastRadius;
    float age;
    bool done;
    PlayerController_Base player;
    Transform visual;     // lo que se ve (se oculta al chocar mientras la estela se desvanece)
    Transform body;       // pluma: se orienta según la velocidad
    TrailRenderer trail;
    static readonly RaycastHit[] hits = new RaycastHit[8];

    static Material template;
    static Material waterMat, poopMat, featherMat, trailMat;

    // Dispara un proyectil desde 'from' con la velocidad indicada
    public static EnemyProjectile Fire(Kind kind, Vector3 from, Vector3 velocity, float damage, PlayerController_Base target)
    {
        GameObject go = new GameObject("Proyectil_" + kind);
        go.transform.position = from;
        EnemyProjectile p = go.AddComponent<EnemyProjectile>();
        p.Setup(kind, velocity, damage, target);
        return p;
    }

    // Velocidad para llegar a 'to' en 'time' segundos con gravedad g (tiro en arco)
    public static Vector3 ArcVelocity(Vector3 from, Vector3 to, float time, float g)
    {
        time = Mathf.Max(0.1f, time);
        return (to - from) / time + Vector3.up * (0.5f * g * time);
    }

    public static float GravityOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Caca: return 16f;
            case Kind.BolaDeAgua: return 13f;
            case Kind.Escupitajo: return 3f;
        }
        return 0f;
    }

    void Setup(Kind k, Vector3 v, float dmg, PlayerController_Base target)
    {
        kind = k;
        velocity = v;
        damage = dmg;
        player = target;
        gravity = GravityOf(k);
        MakeMaterials();

        switch (kind)
        {
            case Kind.Escupitajo:
                radius = 0.35f;
                knockback = 6f;
                Billboard(waterMat, 0.5f, 0.25f);
                Trail(new Color(0.75f, 0.95f, 1f, 0.8f), 0.28f, 0.18f);
                break;
            case Kind.Caca:
                radius = 0.55f;
                knockback = 2f;
                Billboard(poopMat, 0.55f, 0.18f);
                break;
            case Kind.Pluma:
                radius = 0.3f;
                knockback = 5f;
                body = Primitive(PrimitiveType.Capsule, featherMat, new Vector3(0.07f, 0.28f, 0.07f));
                Trail(new Color(1f, 1f, 1f, 0.55f), 0.07f, 0.12f);
                break;
            case Kind.BolaDeAgua:
                radius = 0.9f;
                knockback = 13f;
                blastRadius = 3.6f;
                Billboard(waterMat, 1.4f, 0.22f);
                Trail(new Color(0.6f, 0.9f, 1f, 0.7f), 0.8f, 0.3f);
                break;
        }
        Orient();
    }

    void Update()
    {
        if (done) return;
        float dt = Time.deltaTime;
        age += dt;
        if (age > Lifetime) { Finish(); return; }

        Vector3 from = transform.position;
        velocity += Vector3.down * gravity * dt;
        Vector3 to = from + velocity * dt;

        // ¿Tocó al jugador?
        if (player != null && !player.isDead)
        {
            Vector3 c = player.transform.position + Vector3.up * 0.5f;
            if (SegmentDistance(from, to, c) < radius + PlayerRadius)
            {
                transform.position = to;
                HitPlayer(c);
                return;
            }
        }

        // ¿El agua?
        float surface;
        if (LakeWater.TryGetSurface(to, out surface) && from.y >= surface && to.y < surface)
        {
            transform.position = new Vector3(to.x, surface, to.z);
            HitWater();
            return;
        }

        // ¿El terreno o una roca?
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len > 0.0001f)
        {
            int n = Physics.RaycastNonAlloc(from, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.attachedRigidbody != null) continue;   // aves, peces, el jugador
                transform.position = hits[i].point;
                HitGround(hits[i].point);
                return;
            }
        }

        transform.position = to;
        Orient();
    }

    void Orient()
    {
        if (body != null && velocity.sqrMagnitude > 0.01f)
            body.rotation = Quaternion.LookRotation(velocity.normalized) * Quaternion.Euler(90f, 0f, 0f);
    }

    // ======================= IMPACTOS =======================

    void HitPlayer(Vector3 at)
    {
        if (kind == Kind.BolaDeAgua) { Blast(transform.position, false); return; }
        Vector3 push = velocity;
        push.y = 0f;
        push = (push.sqrMagnitude > 0.01f ? push.normalized : Vector3.zero) * knockback + Vector3.up * knockback * 0.3f;
        player.TakeHit(damage, push);
        if (kind == Kind.Caca)
        {
            Sfx.Play(Sonido.Plaf, at, 1f, 1f, 4f);
            if (!player.isThirdPerson) HelmetVisorHUD.SplatGlass();
        }
        else
        {
            Sfx.Play(kind == Kind.Pluma ? Sonido.GolpeTraje : Sonido.Chapuzon, at, 0.8f, 1.2f, 4f);
        }
        Finish();
    }

    void HitWater()
    {
        Vector3 p = transform.position;
        if (kind == Kind.BolaDeAgua) { Blast(p, true); return; }
        LakeWater.Splash(p, kind == Kind.Caca ? 0.25f : 0.35f, true);
        Finish();
    }

    void HitGround(Vector3 p)
    {
        if (kind == Kind.BolaDeAgua) { Blast(p, false); return; }
        if (kind == Kind.Caca) Sfx.Play(Sonido.Plaf, p, 0.6f, 1.1f, 3f);
        Finish();
    }

    // Bola de agua del pelícano: explota; empuja al jugador y, si cae en el lago, aturde a los peces de alrededor
    void Blast(Vector3 p, bool inLake)
    {
        LakeWater.Splash(p, 1f, true);
        Sfx.Play(Sonido.Chapuzon, p, 1f, 0.7f, 10f);
        if (player != null && !player.isDead)
        {
            Vector3 d = player.transform.position + Vector3.up * 0.5f - p;
            if (d.magnitude < blastRadius + PlayerRadius)
            {
                float k = 1f - Mathf.Clamp01(d.magnitude / (blastRadius + PlayerRadius)) * 0.5f;
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                Vector3 push = (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.zero) * knockback * k + Vector3.up * knockback * 0.45f;
                player.TakeHit(damage * k, push);
            }
        }
        if (inLake)
        {
            float r2 = (blastRadius + 1.5f) * (blastRadius + 1.5f);
            foreach (FishAI f in FishAI.All)
                if (f != null && f.IsCatchable && (f.transform.position - p).sqrMagnitude < r2) f.Stun(3f);
        }
        Finish();
    }

    void Finish()
    {
        done = true;
        if (visual != null) visual.gameObject.SetActive(false);
        if (trail != null)
        {
            // La estela termina de desvanecerse sola
            trail.emitting = false;
            Destroy(gameObject, trail.time + 0.1f);
        }
        else Destroy(gameObject);
    }

    static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(a + ab * t, p);
    }

    // ======================= ASPECTO =======================

    static void MakeMaterials()
    {
        if (waterMat != null) return;
        if (template == null)
        {
            PlayerShooting ps = FindFirstObjectByType<PlayerShooting>();
            if (ps != null) template = ps.fxMaterialTemplate;
        }
        waterMat = FXFactory.ParticleMaterial(template, FXFactory.SoftDot, new Color(0.55f, 0.9f, 1f, 1f));
        poopMat = FXFactory.ParticleMaterialAlpha(template, FXFactory.SoftDot, new Color(0.95f, 0.93f, 0.85f, 1f));
        featherMat = FXFactory.ParticleMaterialAlpha(template, FXFactory.White, new Color(0.45f, 0.5f, 0.58f, 1f));
        trailMat = FXFactory.ParticleMaterial(template, FXFactory.SoftDot, Color.white);
    }

    void Billboard(Material mat, float size, float wobble)
    {
        // El plano mira solo hacia la cámara
        visual = FXBillboard.Create("Cuerpo", transform, mat, size, wobble).transform;
    }

    Transform Primitive(PrimitiveType type, Material mat, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = "Cuerpo";
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visual = go.transform;
        return go.transform;
    }

    void Trail(Color color, float width, float time)
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMat;
        trail.time = time;
        trail.minVertexDistance = 0.1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
