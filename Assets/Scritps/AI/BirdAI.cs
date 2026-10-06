using System.Collections.Generic;
using UnityEngine;
using FishGame.AI;

// IA de los pájaros depredadores (árbol de comportamiento).
//
// Prioridades (de mayor a menor):
//  1. Muerto                    -> cae girando
//  2. Llegando                  -> vuela desde el cielo hasta el lago (cinemática)
//  3. Aturdido                  -> tras embestir al jugador se queda quieto 2.5 s
//  4. Lleva un pez              -> sube y se aleja; si nadie lo detiene, se lo come
//  5. Pelear con el jugador     -> si lo ve cerca o si le disparó (aunque estuviera cazando)
//  6. Recuperándose             -> remonta tras una picada fallida
//  7. Cazar peces               -> acechar en círculos sobre un pez y lanzarse en picada
//  8. Patrullar                 -> vuela en círculos sobre el lago
[RequireComponent(typeof(Rigidbody))]
public class BirdAI : MonoBehaviour
{
    public static readonly List<BirdAI> All = new List<BirdAI>();

    [Header("Vuelo")]
    public float arrivalSpeed = 22f;
    public float patrolSpeed = 10f;
    public float chaseSpeed = 15f;
    public float diveSpeed = 24f;
    public float carrySpeed = 8f;
    public float acceleration = 18f;
    public float turnSpeed = 4f;
    [Tooltip("Altura de patrulla sobre la superficie (min, max)")]
    public Vector2 patrolAltitude = new Vector2(12f, 20f);
    public float stalkAltitude = 9f;
    public float minAltitudeAboveGround = 3f;

    [Header("Caza")]
    public float huntRange = 75f;
    [Tooltip("Segundos acechando antes de lanzarse (min, max)")]
    public Vector2 stalkTime = new Vector2(1.5f, 3f);
    public float maxDiveDistance = 22f;
    public float catchRadius = 2.4f;
    [Tooltip("Profundidad máxima a la que alcanza a atrapar un pez")]
    public float catchDepth = 2f;
    [Tooltip("Segundos que tarda en comerse el pez (tiempo para rescatarlo)")]
    public float carryTime = 3.5f;
    public Vector2 diveCooldown = new Vector2(2.5f, 4.5f);

    [Header("Jugador")]
    [Tooltip("Si el jugador está a esta distancia y lo ve, lo ataca")]
    public float detectRange = 26f;
    [Tooltip("Si el jugador se aleja más que esto, pierde el interés")]
    public float loseRange = 80f;
    [Tooltip("Segundos que te persigue después de que le dispares")]
    public float aggroMemory = 10f;
    public float attackReach = 2.6f;
    public float damage = 15f;
    public float knockbackForce = 16f;
    public float knockbackUp = 6f;
    public float stunDuration = 2.5f;
    [Tooltip("Si el jugador está más hondo que esto bajo el agua, no lo ve")]
    public float playerHiddenDepth = 1.5f;

    [Header("Visual")]
    public Transform visual;
    public float flapSpeed = 7f;
    public float flapAngle = 28f;

    [Header("Efectos")]
    public GameObject splashPrefab;
    public GameObject hitPrefab;
    public GameObject deathPrefab;

    public bool IsAlive { get { return !dead; } }
    public bool IsArriving { get { return arriving; } }
    public bool IsHunting { get { return TargetFish != null && CarriedFish == null && !IsAggro; } }
    public bool IsDiving { get { return diving; } }
    public bool IsStunned { get { return stunTimer > 0f; } }
    public bool IsAggro { get { return player != null && !player.isDead && Time.time < aggroUntil; } }
    public FishAI TargetFish { get; private set; }
    public FishAI CarriedFish { get; private set; }
    public string CurrentBehaviour { get { return tree != null ? tree.ActiveAction : ""; } }

    public event System.Action<BirdAI> Died;
    public event System.Action<BirdAI, FishAI> FishCaught;
    public event System.Action<BirdAI, FishAI> FishEaten;
    public event System.Action<BirdAI, FishAI> FishDropped;

    Rigidbody rb;
    Damageable health;
    LakeVolume lake;
    PlayerController_Base player;
    BehaviorTree tree;
    Transform talons;

    Vector3 desiredVelocity;
    Vector3 arrivalPoint;
    bool arriving;
    bool dead;
    bool diving;
    float diveStartTime;
    float stalkTimer;
    float stalkDuration;
    float diveCooldownTimer;
    float carryTimer;
    float stunTimer;
    float recoverTimer;
    float attackTimer;
    float aggroUntil = -100f;
    float retargetTimer;
    float orbitAngle;
    float orbitDirection = 1f;
    float patrolAngle;
    float patrolHeight;
    float groundCacheY = float.NegativeInfinity;
    float groundCacheTimer;
    float sightTimer;
    bool canSeePlayer;
    bool touchedWater;

    // Aleteo procedural (huesos de las alas)
    readonly List<Transform> wingBones = new List<Transform>();
    readonly List<Quaternion> wingRest = new List<Quaternion>();
    readonly List<float> wingSide = new List<float>();
    float flapPhase;
    Vector3 visualBasePos;
    float hitPunch;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        health = GetComponent<Damageable>();
        patrolAngle = Random.Range(0f, 360f);
        orbitDirection = Random.value > 0.5f ? 1f : -1f;
        patrolHeight = Random.Range(patrolAltitude.x, patrolAltitude.y);
        flapPhase = Random.Range(0f, 10f);
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        if (lake == null) lake = LakeVolume.Instance;
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
        SetupVisual();
        BuildTree();
    }

    // Lo llama el GameDirector al crear el pájaro
    public void BeginArrival(Vector3 point, LakeVolume lakeVolume, PlayerController_Base playerRef)
    {
        lake = lakeVolume;
        player = playerRef;
        arrivalPoint = point;
        arriving = true;
        Vector3 dir = (point - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(dir);
        rb.linearVelocity = dir * arrivalSpeed;
    }

    void SetupVisual()
    {
        if (visual == null) return;
        visualBasePos = visual.localPosition;

        // Huesos de alas del modelo (si existen)
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n == "LeftArm" || n == "RightArm" || n == "L_wing" || n == "R_wing")
            {
                wingBones.Add(t);
                wingRest.Add(t.localRotation);
                float side = Vector3.Dot(t.position - transform.position, transform.right);
                wingSide.Add(side < 0f ? -1f : 1f);
            }
        }

        // Garras: punto medio entre las patas, o un punto bajo el cuerpo
        Transform lf = FindChild(visual, "LeftFoot");
        Transform rf = FindChild(visual, "RightFoot");
        GameObject t2 = new GameObject("Garras");
        talons = t2.transform;
        talons.SetParent(transform, false);
        talons.position = (lf != null && rf != null) ? (lf.position + rf.position) * 0.5f : transform.position - transform.up * 1.1f;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // ======================= ÁRBOL =======================

    void BuildTree()
    {
        tree = new BehaviorTree(
            new BTSelector("Pájaro",
                new BTSequence("Muerto",
                    new BTCondition("¿Muerto?", () => dead),
                    new BTAction("Caer", Fall)),
                new BTSequence("Llegada",
                    new BTCondition("¿Llegando?", () => arriving),
                    new BTAction("Volar al lago", Arrive)),
                new BTSequence("Aturdido",
                    new BTCondition("¿Aturdido?", () => stunTimer > 0f),
                    new BTAction("Quedarse quieto", Hover)),
                new BTSequence("LlevarPresa",
                    new BTCondition("¿Lleva un pez?", () => CarriedFish != null),
                    new BTAction("Escapar con la presa", CarryAway)),
                new BTSequence("Combate",
                    new BTCondition("¿Pelear con el jugador?", ShouldFightPlayer),
                    new BTAction("Perseguir y embestir", ChasePlayer)),
                new BTSequence("Remontar",
                    new BTCondition("¿Picada fallida?", () => recoverTimer > 0f),
                    new BTAction("Remontar vuelo", Climb)),
                new BTSequence("Cazar",
                    new BTCondition("¿Hay presa?", AcquireFish),
                    new BTSelector("Ataque",
                        new BTSequence("Picada",
                            new BTCondition("¿Listo para lanzarse?", () => diving || ReadyToDive()),
                            new BTAction("Lanzarse en picada", Dive)),
                        new BTAction("Acechar en círculos", Stalk))),
                new BTAction("Patrullar el lago", Patrol)));
    }

    void FixedUpdate()
    {
        if (lake == null) { lake = LakeVolume.Instance; if (lake == null) return; }
        float dt = Time.fixedDeltaTime;

        if (attackTimer > 0f) attackTimer -= dt;
        if (diveCooldownTimer > 0f) diveCooldownTimer -= dt;
        if (recoverTimer > 0f) recoverTimer -= dt;

        tree.Tick();

        if (!dead) Move(dt);
        else if (!touchedWater && lake.IsInWater(transform.position))
        {
            touchedWater = true;
            FXFactory.SpawnOneShot(splashPrefab, new Vector3(transform.position.x, lake.SurfaceY, transform.position.z), Quaternion.identity, 1.2f, 3f);
        }
    }

    // ======================= CONDICIONES =======================

    bool ShouldFightPlayer()
    {
        if (player == null || player.isDead) return false;
        float dist = Vector3.Distance(transform.position, player.transform.position);

        sightTimer -= Time.fixedDeltaTime;
        if (sightTimer <= 0f)
        {
            canSeePlayer = dist < loseRange && CanSeePlayer();
            sightTimer = 0.25f;
        }

        // Lo ve dentro del rango: ataca (y lo sigue mientras siga cerca)
        if (dist < detectRange && canSeePlayer)
        {
            SetAggro(4f);
        }
        else if (IsAggro && dist < detectRange * 1.6f && canSeePlayer)
        {
            SetAggro(2f);
        }

        if (IsAggro && dist > loseRange) aggroUntil = Mathf.Min(aggroUntil, Time.time + 1.5f);
        return IsAggro;
    }

    void SetAggro(float seconds)
    {
        aggroUntil = Mathf.Max(aggroUntil, Time.time + seconds);
        if (TargetFish != null) ReleaseTarget();
        diving = false;
    }

    bool CanSeePlayer()
    {
        // Escondido en lo profundo: no lo ve
        if (player.isInWater && lake.SurfaceY - (player.transform.position.y + 0.5f) > playerHiddenDepth) return false;

        Vector3 from = transform.position;
        Vector3 to = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = to - from;
        RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody != null) continue; // el jugador, peces, otros pájaros
            return false; // terreno o rocas en medio
        }
        return true;
    }

    bool AcquireFish()
    {
        if (TargetFish != null && (!TargetFish.IsCatchable || (TargetFish.Hunter != null && TargetFish.Hunter != this)))
        {
            ReleaseTarget();
        }

        if (TargetFish == null)
        {
            retargetTimer -= Time.fixedDeltaTime;
            if (retargetTimer > 0f) return false;
            retargetTimer = 0.5f;

            FishAI best = null;
            float bestScore = float.MaxValue;
            Vector3 pos = transform.position;
            foreach (FishAI f in FishAI.All)
            {
                if (!f.IsCatchable) continue;
                Vector3 d = f.transform.position - pos;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > huntRange) continue;
                // Prefiere peces cercanos, poco profundos y que nadie más esté cazando
                float score = dist + Mathf.Max(0f, f.Depth - catchDepth) * 10f + (f.Hunter != null ? 50f : 0f);
                if (score < bestScore) { bestScore = score; best = f; }
            }
            if (best == null) return false;
            if (best.Hunter != null && best.Hunter != this) return false;

            TargetFish = best;
            TargetFish.Hunter = this;
            stalkTimer = 0f;
            stalkDuration = Random.Range(stalkTime.x, stalkTime.y);
            orbitAngle = Mathf.Atan2(pos.z - best.transform.position.z, pos.x - best.transform.position.x);
        }
        return TargetFish != null;
    }

    bool ReadyToDive()
    {
        if (TargetFish == null || diveCooldownTimer > 0f || stalkTimer < stalkDuration) return false;
        Vector3 d = TargetFish.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude < maxDiveDistance && TargetFish.Depth <= catchDepth + 0.4f;
    }

    void ReleaseTarget()
    {
        if (TargetFish != null && TargetFish.Hunter == this) TargetFish.Hunter = null;
        TargetFish = null;
        diving = false;
    }

    // ======================= ACCIONES =======================

    BTStatus Fall()
    {
        tree.MarkActive("Caer");
        return BTStatus.Running;
    }

    BTStatus Arrive()
    {
        tree.MarkActive("Volar al lago");
        Vector3 to = arrivalPoint - transform.position;
        if (to.magnitude < 8f)
        {
            arriving = false;
            return BTStatus.Success;
        }
        desiredVelocity = to.normalized * arrivalSpeed;
        return BTStatus.Running;
    }

    BTStatus Hover()
    {
        tree.MarkActive("Quedarse quieto");
        stunTimer -= Time.fixedDeltaTime;
        desiredVelocity = Vector3.up * Mathf.Sin(Time.time * 3f) * 0.4f;
        return BTStatus.Running;
    }

    BTStatus CarryAway()
    {
        tree.MarkActive("Escapar con la presa");
        carryTimer -= Time.fixedDeltaTime;

        // Sube y se aleja del jugador
        Vector3 away = transform.forward;
        if (player != null)
        {
            away = transform.position - player.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();
        }
        Vector3 goal = transform.position + away * 10f;
        goal.y = lake.SurfaceY + patrolAltitude.y + 6f;
        desiredVelocity = (goal - transform.position).normalized * carrySpeed;

        if (carryTimer <= 0f)
        {
            FishAI fish = CarriedFish;
            CarriedFish = null;
            recoverTimer = 1f;
            if (fish != null)
            {
                FishEaten?.Invoke(this, fish);
                fish.Kill(true);
            }
            return BTStatus.Success;
        }
        return BTStatus.Running;
    }

    BTStatus ChasePlayer()
    {
        tree.MarkActive("Perseguir y embestir");
        Vector3 ppos = player.transform.position + Vector3.up * 0.4f;
        Vector3 lead = ppos + player.Velocity * 0.25f;
        float minY = lake.SurfaceY + 0.8f;

        Vector3 aim = lead;
        float speed = chaseSpeed;
        if (lead.y < minY)
        {
            // El jugador está bajo el agua: espera encima, dando vueltas
            orbitAngle += Time.fixedDeltaTime * 1.2f * orbitDirection;
            aim = new Vector3(lead.x + Mathf.Cos(orbitAngle) * 5f, minY + 3f, lead.z + Mathf.Sin(orbitAngle) * 5f);
            speed = patrolSpeed;
        }
        else if (attackTimer <= 0f)
        {
            speed = chaseSpeed * 1.2f; // embestida
        }

        desiredVelocity = (aim - transform.position).normalized * speed;

        // Golpe por proximidad (más fiable que esperar la colisión física)
        if (attackTimer <= 0f && Vector3.Distance(transform.position, ppos) < attackReach)
        {
            HitPlayer();
        }
        return BTStatus.Running;
    }

    BTStatus Climb()
    {
        tree.MarkActive("Remontar vuelo");
        Vector3 goal = transform.position + transform.forward * 8f;
        goal.y = lake.SurfaceY + stalkAltitude + 4f;
        desiredVelocity = (goal - transform.position).normalized * patrolSpeed;
        return BTStatus.Running;
    }

    BTStatus Stalk()
    {
        tree.MarkActive("Acechar en círculos");
        Vector3 fish = TargetFish.transform.position;
        orbitAngle += Time.fixedDeltaTime * 0.9f * orbitDirection;
        Vector3 point = new Vector3(fish.x + Mathf.Cos(orbitAngle) * 7f, lake.SurfaceY + stalkAltitude, fish.z + Mathf.Sin(orbitAngle) * 7f);
        desiredVelocity = (point - transform.position).normalized * patrolSpeed * 1.15f;

        Vector3 flat = fish - transform.position;
        flat.y = 0f;
        if (flat.magnitude < 14f) stalkTimer += Time.fixedDeltaTime;
        return BTStatus.Running;
    }

    BTStatus Dive()
    {
        tree.MarkActive("Lanzarse en picada");
        if (!diving)
        {
            diving = true;
            diveStartTime = Time.time;
        }

        FishAI fish = TargetFish;
        Vector3 talonPos = talons != null ? talons.position : transform.position;
        float surface = lake.SurfaceY;

        // Apunta a donde va a estar el pez
        float t = Mathf.Clamp(Vector3.Distance(talonPos, fish.transform.position) / diveSpeed, 0f, 1.2f);
        Vector3 predicted = fish.transform.position + fish.Velocity * t;
        predicted.y = Mathf.Max(predicted.y, surface - catchDepth * 0.5f);
        desiredVelocity = (predicted - talonPos).normalized * diveSpeed;

        // ¡Atrapado!
        if (Vector3.Distance(talonPos, fish.transform.position) < catchRadius && fish.Depth <= catchDepth + 0.3f)
        {
            Catch(fish);
            return BTStatus.Success;
        }

        // Falla si el pez se escondió en lo hondo, si tardó demasiado o si ya se metió al agua
        bool fishTooDeep = fish.Depth > catchDepth + 0.4f && transform.position.y < surface + 3f;
        bool tooLong = Time.time - diveStartTime > 3.5f;
        bool underWater = transform.position.y < surface - 1.1f;
        if (fishTooDeep || tooLong || underWater)
        {
            EndDive();
            return BTStatus.Failure;
        }
        return BTStatus.Running;
    }

    void EndDive()
    {
        diving = false;
        recoverTimer = 1.4f;
        stalkTimer = 0f;
        stalkDuration = Random.Range(stalkTime.x, stalkTime.y);
        diveCooldownTimer = Random.Range(diveCooldown.x, diveCooldown.y);
        if (transform.position.y < lake.SurfaceY + 1f)
            FXFactory.SpawnOneShot(splashPrefab, new Vector3(transform.position.x, lake.SurfaceY, transform.position.z), Quaternion.identity, 0.7f, 3f);
        // A veces cambia de presa
        if (Random.value < 0.4f) ReleaseTarget();
    }

    void Catch(FishAI fish)
    {
        diving = false;
        if (TargetFish != null && TargetFish.Hunter == this) TargetFish.Hunter = null;
        TargetFish = null;
        CarriedFish = fish;
        carryTimer = carryTime;
        fish.Grab(talons != null ? talons : transform);
        FXFactory.SpawnOneShot(splashPrefab, new Vector3(transform.position.x, lake.SurfaceY, transform.position.z), Quaternion.identity, 1f, 3f);
        FishCaught?.Invoke(this, fish);
    }

    BTStatus Patrol()
    {
        tree.MarkActive("Patrullar el lago");
        patrolAngle += Time.fixedDeltaTime * 12f * orbitDirection;
        float radius = lake.Radius * 0.6f;
        float a = patrolAngle * Mathf.Deg2Rad;
        Vector3 center = lake.Center;
        Vector3 point = new Vector3(center.x + Mathf.Cos(a) * radius, lake.SurfaceY + patrolHeight, center.z + Mathf.Sin(a) * radius);
        desiredVelocity = (point - transform.position).normalized * patrolSpeed;
        return BTStatus.Running;
    }

    // ======================= MOVIMIENTO =======================

    void Move(float dt)
    {
        Vector3 v = rb.linearVelocity;
        float accel = acceleration * (diving ? 1.6f : 1f);

        // Separación entre pájaros
        Vector3 push = Vector3.zero;
        foreach (BirdAI other in All)
        {
            if (other == this || other.dead) continue;
            Vector3 d = transform.position - other.transform.position;
            float m = d.magnitude;
            if (m < 5f && m > 0.01f) push += d / m * (5f - m);
        }
        Vector3 desired = desiredVelocity + push * 2f;

        v = Vector3.MoveTowards(v, desired, accel * dt);

        // Altura mínima: sobre el agua (salvo en la picada) y sobre el terreno
        groundCacheTimer -= dt;
        if (groundCacheTimer <= 0f)
        {
            groundCacheY = GroundBelow();
            groundCacheTimer = 0.2f;
        }
        float minY = diving ? lake.SurfaceY - 1f : lake.SurfaceY + 0.6f;
        if (!arriving) minY = Mathf.Max(minY, groundCacheY + minAltitudeAboveGround);
        if (transform.position.y < minY && v.y < 0f) v.y = Mathf.Max(v.y, (minY - transform.position.y) * 4f);

        // Esquiva obstáculos al frente (árboles, rocas)
        if (v.sqrMagnitude > 1f && !diving)
        {
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, 0.8f, v.normalized, out hit, Mathf.Max(4f, v.magnitude * 0.6f), ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.attachedRigidbody == null)
            {
                v += Vector3.up * accel * dt * 2f;
            }
        }

        rb.linearVelocity = v;

        // Mirar hacia donde vuela (o al jugador si está aturdido), con alabeo en las curvas
        Vector3 look = stunTimer > 0f && player != null ? (player.transform.position - transform.position) : v;
        if (look.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(look.normalized);
            Vector3 localTurn = transform.InverseTransformDirection(desired - v);
            float bank = Mathf.Clamp(-localTurn.x * 3f, -40f, 40f);
            targetRot *= Quaternion.Euler(0f, 0f, bank);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt)));
        }
    }

    float GroundBelow()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 2f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best;
    }

    void LateUpdate()
    {
        if (visual == null) return;
        float dt = Time.deltaTime;

        // Aleteo: rápido al subir o perseguir, planeo en picada, quieto si está muerto
        float flapRate = flapSpeed;
        float amp = flapAngle;
        if (diving) { amp = 6f; flapRate *= 0.5f; }
        else if (CarriedFish != null || stunTimer > 0f) flapRate *= 1.5f;
        if (dead) amp = 0f;
        flapPhase += dt * flapRate;
        float flap = Mathf.Sin(flapPhase) * amp;

        for (int i = 0; i < wingBones.Count; i++)
        {
            Transform b = wingBones[i];
            b.localRotation = wingRest[i];
            b.rotation = Quaternion.AngleAxis(flap * wingSide[i], transform.forward) * b.rotation;
        }

        // Sube y baja con cada aleteo + "golpe" al recibir daño
        hitPunch = Mathf.MoveTowards(hitPunch, 0f, dt * 4f);
        visual.localPosition = visualBasePos + Vector3.up * Mathf.Cos(flapPhase) * 0.12f * (amp / Mathf.Max(1f, flapAngle));
        visual.localScale = Vector3.one * visualScale * (1f + hitPunch * 0.15f);
    }

    float visualScale = 1f;
    public void SetVisualScale(float s) { visualScale = s; }

    // ======================= JUGADOR =======================

    void HitPlayer()
    {
        if (player == null || player.isDead || stunTimer > 0f || dead) return;
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();
        player.TakeHit(damage, dir * knockbackForce + Vector3.up * knockbackUp);

        // Se queda quieto 2.5 s y luego vuelve a perseguir
        stunTimer = stunDuration;
        attackTimer = stunDuration + 0.5f;
        rb.linearVelocity = -dir * 3f;
        SetAggro(stunDuration + 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (dead) return;
        if (collision.collider.GetComponentInParent<PlayerController_Base>() != null && attackTimer <= 0f)
        {
            HitPlayer();
        }
    }

    // ======================= DAÑO =======================

    void OnDamaged(int amount, Vector3 point)
    {
        if (dead) return;
        hitPunch = 1f;
        FXFactory.SpawnOneShot(hitPrefab, point, Quaternion.identity, 0.8f, 2f);

        // Si le disparan, va por el jugador (aunque estuviera cazando)
        arriving = false;
        aggroUntil = Time.time + aggroMemory;
        ReleaseTarget();

        // Del susto suelta al pez
        if (CarriedFish != null) DropFish();
    }

    void DropFish()
    {
        FishAI fish = CarriedFish;
        CarriedFish = null;
        if (fish != null)
        {
            fish.Release();
            FishDropped?.Invoke(this, fish);
        }
    }

    void OnDied()
    {
        if (dead) return;
        dead = true;
        diving = false;
        ReleaseTarget();
        if (CarriedFish != null) DropFish();

        gameObject.tag = "Untagged";
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
        FXFactory.SpawnOneShot(deathPrefab, transform.position, Quaternion.identity, 1.2f, 3f);

        Died?.Invoke(this);
        Destroy(gameObject, 6f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackReach);
        if (TargetFish != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, TargetFish.transform.position);
        }
    }
}
