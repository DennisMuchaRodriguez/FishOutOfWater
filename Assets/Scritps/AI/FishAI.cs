using System.Collections.Generic;
using UnityEngine;
using FishGame.AI;

// IA de los peces del lago (árbol de comportamiento).
//
// Prioridades (de mayor a menor):
//  1. Atrapado / muerto            -> no hace nada (lo controla el pájaro)
//  2. Fuera del agua               -> aletea hacia el agua; si tarda mucho, se asfixia
//  3. Amenaza cerca                -> HUIR (solo por el agua)
//        a) con energía: escapar hacia lo profundo, en zigzag y lejos del pájaro
//        b) agotado: huir en horizontal cerca de la superficie (vulnerable)
//  4. Agotado y muy profundo       -> subir a recuperar aliento
//  5. Nada de lo anterior          -> nadar en cardumen por el lago
[RequireComponent(typeof(Rigidbody))]
public class FishAI : MonoBehaviour
{
    public static readonly List<FishAI> All = new List<FishAI>();
    public static Vector3[] SchoolAnchors;

    public enum FishState { Swimming, Grabbed, Falling, Dead }

    [Header("Nado")]
    public float wanderSpeed = 3.2f;
    public float fleeSpeed = 9.5f;
    public float acceleration = 6f;
    public float turnSpeed = 5f;
    [Tooltip("Profundidad normal (min, max) bajo la superficie")]
    public Vector2 cruiseDepth = new Vector2(0.5f, 1.6f);
    [Tooltip("Distancia mínima al fondo")]
    public float bottomClearance = 0.6f;

    [Header("Huida")]
    public float threatRadius = 22f;
    [Tooltip("Profundidad (min, max) a la que intenta escapar")]
    public Vector2 panicDepth = new Vector2(3.5f, 6f);
    [Tooltip("Segundos que aguanta escondido en lo profundo antes de agotarse")]
    public float panicStamina = 6f;
    public float exhaustedTime = 4f;
    public float staminaRecovery = 0.8f;
    public float alarmRadius = 14f;

    [Header("Cardumen")]
    public int schoolId = 0;
    public float schoolRadius = 14f;
    public float separationRadius = 2.2f;

    [Header("Fuera del agua")]
    public float suffocateTime = 6f;

    [Header("Visual")]
    public Transform visual;
    [Tooltip("Coleteo por código (se apaga si el modelo trae su propia animación)")]
    public bool proceduralWiggle = true;
    public float wiggleAmount = 14f;

    public FishState State { get; private set; }
    public bool IsAlive { get { return State != FishState.Dead; } }
    public bool IsCatchable { get { return State == FishState.Swimming; } }
    public float Depth { get { return lake != null ? lake.SurfaceY - transform.position.y : 0f; } }
    public Vector3 Velocity { get { return velocity; } }
    public float Stamina01 { get { return panicStamina > 0f ? stamina / panicStamina : 0f; } }
    public bool IsFleeing { get; private set; }
    public string CurrentBehaviour { get { return tree != null ? tree.ActiveAction : ""; } }

    // Pájaro que lo está cazando (para que dos pájaros no persigan el mismo pez)
    [System.NonSerialized] public BirdAI Hunter;

    public event System.Action<FishAI> Died;
    public event System.Action<FishAI> Rescued;

    LakeVolume lake;
    Rigidbody rb;
    SphereCollider col;
    BehaviorTree tree;

    Vector3 velocity;
    Vector3 desiredVelocity;
    Vector3 target;
    float retargetTimer;
    float fleeRetargetTimer;
    float stamina;
    float exhaustedTimer;
    float alarmTimer;
    Vector3 threatPosition;
    bool underwaterThreat;
    float speedVariation;
    float outOfWaterTimer;
    float flopTimer;
    float wigglePhase;
    float groundCacheY;
    float groundCacheTimer;
    Quaternion visualBaseRotation = Quaternion.identity;
    Animator swimAnim;
    bool wasGrabbed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.None;

        col = GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        if (col.radius < 0.1f) col.radius = 0.7f;

        speedVariation = Random.Range(0.85f, 1.15f);
        wigglePhase = Random.Range(0f, 10f);
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        lake = LakeVolume.Instance;
        stamina = panicStamina;
        if (visual != null) visualBaseRotation = visual.localRotation;
        BuildTree();
    }

    // Ajustes del tipo de pez (lo llama el GameDirector al crearlo)
    public void ApplyType(FishType type)
    {
        if (type == null) return;
        wanderSpeed *= Mathf.Max(0.1f, type.speedMultiplier);
        fleeSpeed *= Mathf.Max(0.1f, type.speedMultiplier);
        panicStamina *= Mathf.Max(0.1f, type.staminaMultiplier);
        wiggleAmount = type.wiggleAmount;
        proceduralWiggle = type.proceduralWiggle;

        // Si el modelo ya trae animación (controlador), no se mezcla con el coleteo por código
        if (visual != null)
        {
            Animator anim = visual.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                proceduralWiggle = false;
                swimAnim = anim;
            }
        }
    }

    // ======================= ÁRBOL =======================

    void BuildTree()
    {
        tree = new BehaviorTree(
            new BTSelector("Pez",
                new BTSequence("Inactivo",
                    new BTCondition("¿Atrapado o muerto?", () => State == FishState.Grabbed || State == FishState.Dead),
                    new BTAction("Esperar", Idle)),
                new BTSequence("FueraDelAgua",
                    new BTCondition("¿Cayendo / en tierra?", () => State == FishState.Falling),
                    new BTAction("Aletear hacia el agua", Flop)),
                new BTSequence("Huir",
                    new BTCondition("¿Amenaza cerca?", SenseThreat),
                    new BTSelector("ModoHuida",
                        new BTSequence("EscapeProfundo",
                            new BTCondition("¿Tiene energía?", () => exhaustedTimer <= 0f && stamina > 0.5f),
                            new BTAction("Huir a lo profundo", () => Flee(true))),
                        new BTAction("Huir agotado", () => Flee(false)))),
                new BTSequence("Recuperarse",
                    new BTCondition("¿Agotado y hondo?", () => exhaustedTimer > 0f && Depth > cruiseDepth.y + 0.4f),
                    new BTAction("Subir a respirar", Ascend)),
                new BTAction("Nadar en cardumen", SchoolWander)));
    }

    void FixedUpdate()
    {
        if (lake == null) { lake = LakeVolume.Instance; if (lake == null) return; }
        float dt = Time.fixedDeltaTime;

        if (alarmTimer > 0f) alarmTimer -= dt;
        UpdateStamina(dt);

        IsFleeing = false;
        tree.Tick();

        if (State == FishState.Swimming) Move(dt);
    }

    void Update()
    {
        AnimateVisual(Time.deltaTime);
    }

    // ======================= CONDICIONES =======================

    bool SenseThreat()
    {
        float best = float.MaxValue;
        bool found = false;
        Vector3 pos = transform.position;
        underwaterThreat = false;

        foreach (BirdAI bird in BirdAI.All)
        {
            if (bird == null || !bird.IsAlive || bird.IsArriving) continue;
            float dist = Vector3.Distance(pos, bird.transform.position);
            // La garza quieta en la orilla pasa desapercibida hasta que está muy cerca
            if (bird.IsLurking && dist > 3f) continue;
            bool huntingMe = bird.TargetFish == this && bird.IsHunting;
            bool low = bird.transform.position.y - lake.SurfaceY < 12f;
            float radius = huntingMe ? threatRadius * 1.5f : threatRadius;
            if (dist < radius && (huntingMe || low) && dist < best)
            {
                best = dist;
                threatPosition = bird.transform.position;
                underwaterThreat = bird.IsSubmerged;
                found = true;
            }
        }

        if (!found && alarmTimer > 0f) found = true;
        return found;
    }

    void UpdateStamina(float dt)
    {
        if (State != FishState.Swimming) return;

        if (exhaustedTimer > 0f)
        {
            exhaustedTimer -= dt;
            if (exhaustedTimer <= 0f) stamina = panicStamina * 0.35f;
            return;
        }

        if (Depth > cruiseDepth.y + 0.6f)
        {
            stamina -= dt;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhaustedTimer = exhaustedTime;
            }
        }
        else
        {
            stamina = Mathf.Min(panicStamina, stamina + staminaRecovery * dt);
        }
    }

    // ======================= ACCIONES =======================

    BTStatus Idle()
    {
        tree.MarkActive("Esperar");
        return BTStatus.Running;
    }

    BTStatus SchoolWander()
    {
        tree.MarkActive("Nadar en cardumen");
        retargetTimer -= Time.fixedDeltaTime;
        if (retargetTimer <= 0f || (target - transform.position).sqrMagnitude < 2.25f)
        {
            Vector3 anchor = lake.Center;
            if (SchoolAnchors != null && SchoolAnchors.Length > 0)
                anchor = SchoolAnchors[Mathf.Abs(schoolId) % SchoolAnchors.Length];

            if (!lake.TryGetSwimPoint(anchor, schoolRadius, cruiseDepth.x, cruiseDepth.y, bottomClearance, out target))
                lake.TryGetSwimPoint(transform.position, 8f, cruiseDepth.x, cruiseDepth.y, bottomClearance, out target);
            retargetTimer = Random.Range(4f, 9f);
        }

        SteerTowards(target, wanderSpeed * speedVariation, true);
        return BTStatus.Running;
    }

    BTStatus Flee(bool deep)
    {
        tree.MarkActive(deep ? "Huir a lo profundo" : "Huir agotado");
        IsFleeing = true;

        fleeRetargetTimer -= Time.fixedDeltaTime;
        if (fleeRetargetTimer <= 0f || (target - transform.position).sqrMagnitude < 4f)
        {
            Vector3 away = transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();

            // Zigzag: cada tramo se desvía un poco a un lado distinto.
            // Si el cazador bucea (cormorán, serreta), lo hondo no sirve: huye en horizontal
            Vector2 depthRange = deep && !underwaterThreat ? panicDepth : cruiseDepth;
            bool ok = false;
            float[] angles = { Random.Range(-45f, 45f), 75f, -75f, 120f, -120f };
            foreach (float a in angles)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * away;
                Vector3 desiredPoint = transform.position + dir * Random.Range(10f, 16f);
                if (lake.TryGetSwimPoint(desiredPoint, 4f, depthRange.x, depthRange.y, bottomClearance, out target, 4))
                {
                    ok = true;
                    break;
                }
            }
            if (!ok)
            {
                // Acorralado: hacia el centro del lago
                lake.TryGetSwimPoint(lake.Center, lake.Radius * 0.5f, depthRange.x, depthRange.y, bottomClearance, out target);
            }
            fleeRetargetTimer = Random.Range(0.8f, 1.5f);
        }

        SteerTowards(target, fleeSpeed * speedVariation, false);
        return BTStatus.Running;
    }

    BTStatus Ascend()
    {
        tree.MarkActive("Subir a respirar");
        Vector3 up = transform.position + transform.forward * 3f;
        up.y = lake.SurfaceY - cruiseDepth.y;
        SteerTowards(up, wanderSpeed * 1.2f, false);
        return BTStatus.Running;
    }

    BTStatus Flop()
    {
        tree.MarkActive("Aletear hacia el agua");
        float dt = Time.fixedDeltaTime;
        Vector3 pos = transform.position;

        // ¿Volvió al agua?
        if (lake.IsInWater(pos) && pos.y < lake.SurfaceY - 0.2f)
        {
            ReturnToWater();
            return BTStatus.Success;
        }

        outOfWaterTimer += dt;
        if (outOfWaterTimer >= suffocateTime)
        {
            Kill(false);
            return BTStatus.Failure;
        }

        // En tierra da saltitos hacia el agua
        flopTimer -= dt;
        if (flopTimer <= 0f && rb.linearVelocity.sqrMagnitude < 1f)
        {
            Vector3 toLake = lake.Center - pos;
            toLake.y = 0f;
            rb.AddForce(toLake.normalized * 2.5f + Vector3.up * 3.5f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            flopTimer = Random.Range(0.5f, 0.9f);
        }
        return BTStatus.Running;
    }

    // ======================= MOVIMIENTO =======================

    void SteerTowards(Vector3 point, float speed, bool useSeparation)
    {
        Vector3 to = point - transform.position;
        float dist = to.magnitude;
        float arrive = Mathf.Clamp01(dist / 3f);
        Vector3 desired = dist > 0.01f ? to / dist * speed * Mathf.Max(arrive, 0.35f) : Vector3.zero;

        if (useSeparation)
        {
            Vector3 push = Vector3.zero;
            foreach (FishAI other in All)
            {
                if (other == this || other.State != FishState.Swimming) continue;
                Vector3 d = transform.position - other.transform.position;
                float m = d.magnitude;
                if (m < separationRadius && m > 0.001f) push += d / m * (separationRadius - m);
            }
            desired += push * 1.5f;
        }

        desiredVelocity = desired;
    }

    void Move(float dt)
    {
        float accel = acceleration * (IsFleeing ? 2.2f : 1f);
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, accel * dt);

        Vector3 pos = rb.position;
        Vector3 next = pos + velocity * dt;
        float surface = lake.SurfaceY;

        // Nunca sale por arriba
        if (next.y > surface - 0.35f)
        {
            next.y = surface - 0.35f;
            if (velocity.y > 0f) velocity.y = 0f;
        }

        // Ni se mete en el fondo / la orilla
        groundCacheTimer -= dt;
        if (groundCacheTimer <= 0f)
        {
            groundCacheY = lake.GroundHeight(next.x + velocity.x * 0.3f, next.z + velocity.z * 0.3f);
            groundCacheTimer = 0.15f;
        }
        bool blocked = !lake.IsInsideXZ(next, 1f) || groundCacheY > surface - 0.7f;
        if (blocked)
        {
            velocity = -velocity * 0.3f;
            next = pos;
            retargetTimer = 0f;
            fleeRetargetTimer = 0f;
        }
        else if (next.y < groundCacheY + bottomClearance)
        {
            next.y = groundCacheY + bottomClearance;
            if (velocity.y < 0f) velocity.y = 0f;
        }

        rb.MovePosition(next);

        Vector3 look = velocity;
        if (look.sqrMagnitude > 0.05f)
        {
            // Limita el cabeceo para que no nade "de punta"
            Vector3 flat = new Vector3(look.x, 0f, look.z);
            float pitch = Mathf.Clamp(Mathf.Atan2(look.y, flat.magnitude) * Mathf.Rad2Deg, -35f, 35f);
            Quaternion targetRot = Quaternion.LookRotation(flat.sqrMagnitude > 0.001f ? flat : transform.forward) * Quaternion.Euler(-pitch, 0f, 0f);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt)));
        }
    }

    void AnimateVisual(float dt)
    {
        float speed01 = State == FishState.Swimming ? Mathf.Clamp01(velocity.magnitude / fleeSpeed) : 1f;
        if (swimAnim != null)
        {
            // La animación de nado va más rápida cuanto más rápido nada (y frenética si lo atrapan)
            swimAnim.speed = State == FishState.Dead ? 0f : State == FishState.Grabbed ? 2.6f : Mathf.Lerp(0.7f, 1.9f, speed01);
        }
        if (visual == null || !proceduralWiggle) return;
        float freq = State == FishState.Grabbed ? 22f : Mathf.Lerp(5f, 16f, speed01);
        float amp = State == FishState.Grabbed ? wiggleAmount * 2f : wiggleAmount * Mathf.Lerp(0.5f, 1.3f, speed01);
        if (State == FishState.Dead) amp = 0f;
        wigglePhase += dt * freq;
        visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(wigglePhase) * amp, 0f) * visualBaseRotation;
    }

    // ======================= EVENTOS EXTERNOS =======================

    public static void RaiseAlarm(Vector3 position, float radius)
    {
        foreach (FishAI f in All)
        {
            if (f.State != FishState.Swimming) continue;
            if ((f.transform.position - position).sqrMagnitude < radius * radius)
            {
                f.alarmTimer = 3f;
                f.threatPosition = position;
                f.fleeRetargetTimer = 0f;
            }
        }
    }

    // Lo agarra un pájaro: queda colgando de sus garras (o del pico)
    public void Grab(Transform talons)
    {
        Grab(talons, Vector3.zero);
    }

    // offset: separación local para que varios peces atrapados (pelícano) no se encimen
    public bool Grab(Transform talons, Vector3 offset)
    {
        if (State != FishState.Swimming || talons == null) return false;
        State = FishState.Grabbed;
        wasGrabbed = true;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        col.enabled = false;
        transform.SetParent(talons, true);
        transform.localPosition = offset;
        transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        RaiseAlarm(transform.position, alarmRadius);
        return true;
    }

    // El pájaro lo suelta (porque le dispararon o murió): cae
    public void Release()
    {
        if (State != FishState.Grabbed) return;
        transform.SetParent(null, true);
        State = FishState.Falling;
        outOfWaterTimer = 0f;
        flopTimer = 0.5f;
        col.enabled = true;
        col.isTrigger = false;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.down * 2f;
    }

    void ReturnToWater()
    {
        velocity = rb.linearVelocity * 0.3f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        col.isTrigger = true;
        State = FishState.Swimming;
        exhaustedTimer = 0f;
        stamina = panicStamina;
        alarmTimer = 2f;
        retargetTimer = 0f;
        Hunter = null;
        if (wasGrabbed)
        {
            wasGrabbed = false;
            Rescued?.Invoke(this);
        }
    }

    // El pájaro se lo comió (o se asfixió en tierra)
    public void Kill(bool eaten)
    {
        if (State == FishState.Dead) return;
        State = FishState.Dead;
        Hunter = null;
        Died?.Invoke(this);
        if (eaten)
        {
            Destroy(gameObject);
        }
        else
        {
            transform.SetParent(null, true);
            rb.isKinematic = false;
            rb.useGravity = true;
            Destroy(gameObject, 4f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsFleeing ? Color.red : Color.cyan;
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawWireSphere(target, 0.3f);
    }
}
