using UnityEngine;

// Un misil teledirigido (lo crea MissileLauncher). Sale hacia arriba, gira hacia su ave y la persigue
// girando un máximo de grados por segundo; si el ave muere busca otra por delante. Explota al tocarla
// (daño + un poco a las aves cercanas) o al acabarse su tiempo. No tiene colisionador: nunca golpea al jugador.
public class HomingMissile : MonoBehaviour
{
    MissileLauncher launcher;
    PlayerShooting shooting;
    BirdAI target;
    Vector3 dir;
    float speed = 12f;
    float age;
    bool done;
    TrailRenderer trail;
    Transform flame;

    public void Setup(MissileLauncher owner, PlayerShooting shooter, BirdAI bird, Vector3 direction,
                      Material bodyMaterial, Material trailMaterial)
    {
        launcher = owner;
        shooting = shooter;
        target = bird;
        dir = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;

        // Cuerpo: cápsula naranja alargada (sin colisionador)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Cuerpo";
        Collider col = body.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        body.transform.SetParent(transform, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.14f, 0.22f, 0.14f);
        MeshRenderer br = body.GetComponent<MeshRenderer>();
        if (bodyMaterial != null) br.sharedMaterial = bodyMaterial;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Llama del motor: un punto brillante que mira a la cámara
        flame = FXBillboard.Create("Llama", transform, trailMaterial, 0.45f, 0.2f).transform;
        flame.localPosition = new Vector3(0f, 0f, -0.25f);

        // Estela de humo cian
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.45f;
        trail.minVertexDistance = 0.15f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(1f, 0f));
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(0.55f, 0.9f, 1f), 0.35f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        if (done || launcher == null) { if (!done) Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;
        if (age >= launcher.lifetime)
        {
            Explode(null);
            return;
        }

        Vector3 pos = transform.position;
        if ((target == null || !target.IsAlive) && age > 0.2f) target = launcher.Retarget(pos, dir);

        speed = Mathf.MoveTowards(speed, launcher.speed, 45f * dt);
        if (target != null && age > 0.12f)
        {
            Vector3 aim = target.transform.position;
            Rigidbody trb = target.GetComponent<Rigidbody>();
            if (trb != null)
            {
                float lead = Mathf.Clamp(Vector3.Distance(pos, aim) / Mathf.Max(1f, speed), 0f, 0.6f);
                aim += trb.linearVelocity * lead;
            }
            Vector3 desired = aim - pos;
            if (desired.sqrMagnitude > 0.0001f)
            {
                float turn = launcher.turnRate * Mathf.Deg2Rad * dt * (age < 0.5f ? 0.6f : 1f);
                dir = Vector3.RotateTowards(dir, desired.normalized, turn, 0f).normalized;
            }
        }
        else if (age > 0.35f)
        {
            // Sin ave: se nivela poco a poco
            Vector3 flat = new Vector3(dir.x, Mathf.Min(dir.y, 0.1f), dir.z);
            if (flat.sqrMagnitude > 0.001f) dir = Vector3.RotateTowards(dir, flat.normalized, 1.5f * dt, 0f).normalized;
        }

        pos += dir * speed * dt;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));

        // ¿Tocó un ave? (cualquiera que se cruce, no solo su objetivo)
        foreach (BirdAI b in BirdAI.All)
        {
            if (b == null || !b.IsAlive) continue;
            Collider c = b.GetComponent<Collider>();
            if (c == null) continue;
            if ((c.ClosestPoint(pos) - pos).sqrMagnitude < 0.6f * 0.6f)
            {
                Explode(b);
                return;
            }
        }
    }

    void Explode(BirdAI hit)
    {
        if (done) return;
        done = true;
        Vector3 pos = transform.position;

        GameObject fx = shooting != null ? (shooting.chargedImpactPrefab != null ? shooting.chargedImpactPrefab : shooting.impactEffectPrefab) : null;
        FXFactory.SpawnOneShot(fx, pos, Quaternion.LookRotation(-dir), hit != null ? 0.9f : 0.6f, 3f);
        float surface;
        if (LakeWater.TryGetSurface(pos, out surface) && Mathf.Abs(pos.y - surface) < 1.5f)
            LakeWater.Splash(new Vector3(pos.x, surface, pos.z), 1f, false);

        if (hit != null)
        {
            bool killed = Damage(hit, launcher.damage, pos);
            if (shooting != null) shooting.NotifyHit(killed);
        }
        // Onda de la explosión: daño menor a las aves cercanas
        int splash = Mathf.RoundToInt(launcher.damage * launcher.splashFraction);
        if (splash > 0)
        {
            foreach (BirdAI b in BirdAI.All)
            {
                if (b == null || b == hit || !b.IsAlive) continue;
                if ((b.transform.position - pos).sqrMagnitude <= launcher.splashRadius * launcher.splashRadius) Damage(b, splash, pos);
            }
        }

        // La estela se queda flotando un momento
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.1f);
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            Destroy(this);
        }
        else Destroy(gameObject);
    }

    static bool Damage(BirdAI bird, int amount, Vector3 point)
    {
        Damageable d = bird.GetComponent<Damageable>();
        return d != null && !d.IsDead && d.TakeDamage(amount, point);
    }
}
