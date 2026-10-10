using System.Collections.Generic;
using UnityEngine;
using FishGame.AI;

// IA de los pájaros depredadores (árbol de comportamiento).
//
// Prioridades (de mayor a menor):
//  1. Muerto                    -> cae girando
//  2. Llegando                  -> vuela desde el cielo hasta el lago (cinemática)
//  3. Aturdido                  -> tras embestir al jugador se queda quieto 2.5 s
//  4. Jefe: furia / vulnerable  -> ruge al cambiar de fase; pausa vulnerable tras un ataque fuerte
//  5. Lleva peces               -> sube y se aleja; si nadie lo detiene, se los come
//  6. Jefe: ataque fuerte       -> el patrón propio de cada jefe (BirdAI.Boss.cs)
//  7. Bajo el agua              -> buceadores: persiguen peces (o al jugador) nadando y luego salen
//  8. Pelear con el jugador     -> si lo ve cerca o si le disparó (aunque estuviera cazando)
//  9. Recuperándose             -> remonta tras una picada fallida
// 10. Caza especial             -> la reina roba presas, la garza se planta en la orilla
// 11. Cazar peces               -> acechar sobre un pez y lanzarse (cada especie a su manera)
// 12. Patrullar                 -> vuela en círculos sobre el lago
// Los ataques especiales de cada especie están en BirdAI.Abilities.cs y los jefes en BirdAI.Boss.cs.
[RequireComponent(typeof(Rigidbody))]
public partial class BirdAI : MonoBehaviour
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
    [Tooltip("Si el jugador está más hondo que esto bajo el agua, no lo ve (los buceadores sí)")]
    public float playerHiddenDepth = 1.5f;

    [Header("Visual")]
    public Transform visual;
    [Tooltip("Aleteo por código (se apaga si el modelo trae su propia animación)")]
    public bool proceduralFlap = true;
    public float flapSpeed = 7f;
    public float flapAngle = 28f;
    [Tooltip("Huesos de las alas separados por coma (el lado se detecta solo)")]
    public string wingBoneNames = "LeftArm,RightArm,L_wing,R_wing";
    [Tooltip("Huesos de las garras separados por coma (el pez cuelga en su punto medio)")]
    public string talonBoneNames = "LeftFoot,RightFoot";
    [Tooltip("Punto donde cuelga el pez si no se encuentran las garras (local)")]
    public Vector3 catchPointOffset = new Vector3(0f, -1.1f, 0f);

    [Header("Efectos")]
    public GameObject hitPrefab;
    public GameObject deathPrefab;

    public bool IsAlive { get { return !dead; } }
    public bool IsArriving { get { return arriving; } }
    public bool IsHunting { get { return TargetFish != null && CarriedFish == null && !IsAggro; } }
    public bool IsDiving { get { return diving; } }
    public bool IsStunned { get { return stunTimer > 0f; } }
    public bool IsAggro { get { return player != null && !player.isDead && Time.time < aggroUntil; } }
    // Buceando bajo la superficie (cormorán, serreta, Cormorán Rey): los peces huyen en horizontal
    public bool IsSubmerged { get { return submerged && !dead; } }
    // Garza quieta en la orilla: los peces no la ven venir
    public bool IsLurking { get { return lurking && !dead; } }
    public FishAI TargetFish { get; private set; }
    // Primer pez que lleva (el pelícano puede llevar varios: CarriedFishes)
    public FishAI CarriedFish { get { return carried.Count > 0 ? carried[0] : null; } }
    public IReadOnlyList<FishAI> CarriedFishes { get { return carried; } }
    public int CarriedCount { get { return carried.Count; } }
    public float Health01 { get { return health != null ? health.Health01 : (dead ? 0f : 1f); } }
    public BirdType Type { get; private set; }
    public BirdAbility Ability { get; private set; }
    public BossKind BossType { get; private set; }
    public string CurrentBehaviour { get { return tree != null ? tree.ActiveAction : ""; } }

    public event System.Action<BirdAI> Died;
    // Se lanzan una vez por cada pez (un bocado del pelícano lanza varios)
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
    bool diveTicked;
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
    float bodyRadius = 1.3f;
    float hoverHeight = 1.4f;

    // Peces que lleva (normalmente uno; el pelícano varios)
    readonly List<FishAI> carried = new List<FishAI>();
    readonly List<FishAI> carriedTemp = new List<FishAI>();
    static readonly System.Predicate<FishAI> NotHeld = f => f == null || f.State != FishAI.FishState.Grabbed;

    // Lo que piden las acciones en este tick (se limpia antes de cada tick)
    Vector3 lookOverride;
    float poseTarget;

    // Aleteo procedural (huesos de las alas)
    readonly List<Transform> wingBones = new List<Transform>();
    readonly List<Quaternion> wingRest = new List<Quaternion>();
    readonly List<float> wingSide = new List<float>();
    float flapPhase;
    Vector3 visualBasePos;
    Quaternion visualBaseRot = Quaternion.identity;
    float posePitch;
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

    // Ajustes del tipo de ave (lo llama el GameDirector al crearla)
    public void ApplyType(BirdType type)
    {
        if (type == null) return;
        Type = type;
        float speed = Mathf.Max(0.1f, type.speedMultiplier);
        arrivalSpeed *= speed;
        patrolSpeed *= speed;
        chaseSpeed *= speed;
        diveSpeed *= speed;
        carrySpeed *= speed;
        damage = type.damage;
        detectRange = type.detectRange;
        carryTime = Mathf.Max(0.5f, type.carryTime);
        attackReach = Mathf.Max(0.5f, type.attackReach);
        catchRadius = Mathf.Max(0.5f, type.catchRadius);
        catchDepth = Mathf.Max(0f, type.catchDepth);
        stalkAltitude = Mathf.Max(1f, type.stalkAltitude);
        bodyRadius = Mathf.Max(0.2f, type.colliderRadius);
        hoverHeight = Mathf.Max(1.4f, bodyRadius * 1.1f);
        wingBoneNames = type.wingBones;
        talonBoneNames = type.talonBones;
        catchPointOffset = type.catchPointOffset;
        proceduralFlap = type.proceduralWingFlap && type.animatorController == null;
        SetVisualScale(type.model != null ? type.modelScale : 1f);

        Ability = type.ability;
        BossType = type.boss;
        ConfigureAbility();
        if (IsBoss) SetupBoss();

        // Si el modelo ya trae animación (controlador), no se pisa con el aleteo por código
        if (visual != null)
        {
            Animator anim = visual.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null) proceduralFlap = false;
        }
    }

    static bool NameInList(string name, string list)
    {
        if (string.IsNullOrEmpty(list)) return false;
        foreach (string part in list.Split(','))
            if (part.Trim() == name) return true;
        return false;
    }

    void SetupVisual()
    {
        if (visual == null) return;
        visualBasePos = visual.localPosition;
        visualBaseRot = visual.localRotation;

        // Huesos de alas del modelo (si existen)
        if (proceduralFlap)
        {
            foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (!NameInList(t.name, wingBoneNames)) continue;
                wingBones.Add(t);
                wingRest.Add(t.localRotation);
                float side = Vector3.Dot(t.position - transform.position, transform.right);
                wingSide.Add(side < 0f ? -1f : 1f);
            }
        }

        // Garras: punto medio de los huesos indicados, o el punto configurado
        Vector3 sum = Vector3.zero;
        int found = 0;
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
        {
            if (!NameInList(t.name, talonBoneNames)) continue;
            sum += t.position;
            found++;
        }
        GameObject t2 = new GameObject("Garras");
        talons = t2.transform;
        talons.SetParent(transform, false);
        if (found > 0) talons.position = sum / found;
        else talons.localPosition = catchPointOffset;
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
                new BTSequence("Furia",
                    new BTCondition("¿Jefe cambiando de fase?", () => IsBoss && phaseChangeTimer > 0f),
                    new BTAction("Rugir", BossRoar)),
                new BTSequence("Vulnerable",
                    new BTCondition("¿Jefe agotado?", () => IsBoss && vulnerableTimer > 0f),
                    new BTAction("Pausa vulnerable", BossVulnerablePause)),
                new BTSequence("LlevarPresa",
                    new BTCondition("¿Lleva peces?", () => carried.Count > 0),
                    new BTAction("Escapar con la presa", CarryAway)),
                new BTSequence("AtaqueJefe",
                    new BTCondition("¿Ataque fuerte?", BossWantsAttack),
                    new BTAction("Ataque del jefe", BossAttackAct)),
                new BTSequence("BajoElAgua",
                    new BTCondition("¿Bajo el agua?", () => submerged),
                    new BTAction("Nadar bajo el agua", Underwater)),
                new BTSequence("Combate",
                    new BTCondition("¿Pelear con el jugador?", () => !IsBoss && ShouldFightPlayer()),
                    new BTAction("Perseguir y embestir", ChasePlayer)),
                new BTSequence("Remontar",
                    new BTCondition("¿Picada fallida?", () => recoverTimer > 0f),
                    new BTAction("Remontar vuelo", Climb)),
                new BTSequence("RobarPresa",
                    new BTCondition("¿Otra ave lleva un pez?", WantsSteal),
                    new BTAction("Robar la presa", StealFish)),
                new BTSequence("Orilla",
                    new BTCondition("¿Cazar desde la orilla?", WantsShore),
                    new BTAction("Arponear desde la orilla", ShoreHunt)),
                new BTSequence("Cazar",
                    new BTCondition("¿Hay presa?", AcquireFish),
                    new BTSelector("Ataque",
                        new BTSequence("Picada",
                            new BTCondition("¿Listo para lanzarse?", () => diving || ReadyToDive()),
                            new BTAction("Lanzarse en picada", Dive)),
                        new BTAction("Acechar", Stalk))),
                new BTAction("Patrullar el lago", Patrol)));
    }

    void FixedUpdate()
    {
        if (lake == null) { lake = LakeVolume.Instance; if (lake == null) return; }
        float dt = Time.fixedDeltaTime;

        if (attackTimer > 0f) attackTimer -= dt;
        if (diveCooldownTimer > 0f) diveCooldownTimer -= dt;
        if (recoverTimer > 0f) recoverTimer -= dt;
        if (carried.Count > 0) carried.RemoveAll(NotHeld);
        TickAbilities(dt);
        if (IsBoss) TickBoss(dt);

        lookOverride = Vector3.zero;
        poseTarget = 0f;
        lowFlight = false;
        diveTicked = false;
        tree.Tick();
        // Si otra prioridad interrumpió la picada, se cancela limpia
        if (diving && !diveTicked) diving = false;

        if (!dead)
        {
            Move(dt);
            UpdateSubmerged(dt);
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
        CancelSpecialHunt();
    }

    bool CanSeePlayer()
    {
        // Escondido en lo profundo: no lo ve (los buceadores sí: el agua no es refugio)
        if (!IsDiver && player.isInWater && lake.SurfaceY - (player.transform.position.y + 0.5f) > playerHiddenDepth) return false;

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
                // Prefiere peces cercanos, a su alcance (según la especie) y que nadie más esté cazando
                float score = dist + FishScore(f) + (f.Hunter != null ? 50f : 0f);
                if (score < bestScore) { bestScore = score; best = f; }
            }
            if (best == null) return false;
            if (best.Hunter != null && best.Hunter != this) return false;

            TargetFish = best;
            TargetFish.Hunter = this;
            stalkTimer = 0f;
            stalkDuration = Random.Range(stalkTime.x, stalkTime.y);
            orbitAngle = Mathf.Atan2(pos.z - best.transform.position.z, pos.x - best.transform.position.x);
            OnTargetAcquired();
        }
        return TargetFish != null;
    }

    bool ReadyToDive()
    {
        if (TargetFish == null || diveCooldownTimer > 0f) return false;
        if (Ability == BirdAbility.GroupDive && inGroup) return GroupReady();
        if (stalkTimer < stalkDuration) return false;
        if (Ability == BirdAbility.HoverDive && !hovering) return false;
        if (IsDiver && surfaceCooldown > 0f) return false;
        Vector3 d = TargetFish.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude < maxDiveDistance && (IsDiver || TargetFish.Depth <= catchDepth + 0.4f);
    }

    void ReleaseTarget()
    {
        if (TargetFish != null && TargetFish.Hunter == this) TargetFish.Hunter = null;
        TargetFish = null;
        diving = false;
        hovering = false;
        inGroup = false;
        chainFollower = false;
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
            recoverTimer = 1f;
            hasShore = false;
            EatAll();
            return BTStatus.Success;
        }
        return BTStatus.Running;
    }

    // Se come todo lo que lleva (un evento por pez para que el GameDirector cuente bien)
    void EatAll()
    {
        carriedTemp.Clear();
        carriedTemp.AddRange(carried);
        carried.Clear();
        foreach (FishAI fish in carriedTemp)
        {
            if (fish == null) continue;
            FishEaten?.Invoke(this, fish);
            fish.Kill(true);
        }
        carriedTemp.Clear();
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
            if (IsDiver && surfaceCooldown <= 0f && lake.IsInsideXZ(lead, 1f))
            {
                // Buceador: se zambulle tras el jugador (bajo el agua lo sigue la rama "Bajo el agua")
                KeepDiving();
                if (Time.time - diveStartTime > 3f) surfaceCooldown = 3f; // no logra entrar: espera arriba
            }
            else
            {
                // El jugador está bajo el agua: espera encima, dando vueltas
                orbitAngle += Time.fixedDeltaTime * 1.2f * orbitDirection;
                aim = new Vector3(lead.x + Mathf.Cos(orbitAngle) * 5f, minY + 3f, lead.z + Mathf.Sin(orbitAngle) * 5f);
                speed = patrolSpeed;
            }
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
        if (Ability == BirdAbility.HoverDive) return HoverStalk();

        tree.MarkActive("Acechar en círculos");
        Vector3 fish = TargetFish.transform.position;
        // La serreta del grupo se queda en su lado del cardumen (gira muy despacio)
        if (inGroup) orbitAngle += Time.fixedDeltaTime * 0.25f * orbitDirection;
        else orbitAngle += Time.fixedDeltaTime * 0.9f * orbitDirection;
        Vector3 point = new Vector3(fish.x + Mathf.Cos(orbitAngle) * stalkRadius, lake.SurfaceY + stalkAltitude, fish.z + Mathf.Sin(orbitAngle) * stalkRadius);
        desiredVelocity = (point - transform.position).normalized * patrolSpeed * 1.15f;

        Vector3 flat = fish - transform.position;
        flat.y = 0f;
        if (flat.magnitude < stalkRadius * 2f) stalkTimer += Time.fixedDeltaTime;
        return BTStatus.Running;
    }

    BTStatus Dive()
    {
        tree.MarkActive("Lanzarse en picada");
        diveTicked = true;
        if (!diving)
        {
            diving = true;
            diveStartTime = Time.time;
            OnDiveStarted();
        }

        FishAI fish = TargetFish;
        Vector3 talonPos = talons != null ? talons.position : transform.position;
        float surface = lake.SurfaceY;
        float speed = diveSpeed * diveSpeedMul;

        // Apunta a donde va a estar el pez
        float t = Mathf.Clamp(Vector3.Distance(talonPos, fish.transform.position) / speed, 0f, diveLeadMax);
        Vector3 predicted = fish.transform.position + fish.Velocity * t;
        if (!IsDiver) predicted.y = Mathf.Max(predicted.y, surface - catchDepth * 0.5f);
        if (Ability == BirdAbility.SurfaceSnatch)
        {
            // Pasada rasante: baja en curva suave, casi horizontal, sin zambullirse
            Vector3 flatTo = predicted - talonPos;
            flatTo.y = 0f;
            predicted.y = Mathf.Max(predicted.y, surface - 0.5f) + Mathf.Clamp(flatTo.magnitude * 0.2f, 0f, 3f);
        }
        desiredVelocity = (predicted - talonPos).normalized * speed;

        // ¡Atrapado!
        if (Vector3.Distance(talonPos, fish.transform.position) < catchRadius && (IsDiver || fish.Depth <= catchDepth + 0.3f))
        {
            Catch(fish);
            return BTStatus.Success;
        }

        // Falla si el pez se escondió en lo hondo, si tardó demasiado o si ya se metió al agua
        // (los buceadores sí entran: bajo el agua sigue la rama "Bajo el agua")
        bool fishTooDeep = !IsDiver && fish.Depth > catchDepth + 0.4f && transform.position.y < surface + 3f;
        bool tooLong = Time.time - diveStartTime > diveTimeout;
        bool underWater = !IsDiver && transform.position.y < surface - Mathf.Max(diveFloor, 0.5f) - 0.1f;
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
        hovering = false;
        inGroup = false;
        chainFollower = false;
        // La salpicadura al tocar el agua la hace WaterInteractor
        // A veces cambia de presa
        if (Random.value < 0.4f) ReleaseTarget();
    }

    // Sigue (o empieza) una picada desde una acción que no es Dive (jefes, buceo tras el jugador)
    void KeepDiving()
    {
        if (!diving) diveStartTime = Time.time;
        diving = true;
        diveTicked = true;
    }

    void Catch(FishAI fish)
    {
        diving = false;
        hovering = false;
        inGroup = false;
        chainFollower = false;
        if (TargetFish != null && TargetFish.Hunter == this) TargetFish.Hunter = null;
        TargetFish = null;
        carryTimer = carryTime;
        Vector3 at = fish.transform.position;
        if (!GrabFish(fish)) return;
        // Las garras golpean el agua: salpicadura y anillos aunque el cuerpo no se sumerja
        if (!submerged) LakeWater.Splash(new Vector3(fish.transform.position.x, lake.SurfaceY, fish.transform.position.z), 0.7f, false);
        // Pelícano: el mismo bocado se lleva a los peces de alrededor
        if (ScoopMax > 1) ScoopAround(at);
    }

    bool GrabFish(FishAI fish)
    {
        if (fish == null || !fish.IsCatchable) return false;
        if (!fish.Grab(talons != null ? talons : transform, CarryOffset(carried.Count))) return false;
        carried.Add(fish);
        FishCaught?.Invoke(this, fish);
        return true;
    }

    // Varios peces en las garras / el saco: se reparten un poco para que no se encimen
    Vector3 CarryOffset(int index)
    {
        if (index <= 0) return Vector3.zero;
        float s = Mathf.Max(1f, visualScale * 0.7f);
        float side = index % 2 == 1 ? -1f : 1f;
        return new Vector3(side * 0.32f * ((index + 1) / 2), -0.18f * index, 0.25f * (index / 2)) * s;
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
        float accel = acceleration * (diving ? diveAccelMul : 1f);

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

        // Altura mínima: sobre el agua (salvo en la picada o buceando) y sobre el terreno
        groundCacheTimer -= dt;
        if (groundCacheTimer <= 0f)
        {
            groundCacheY = GroundBelow();
            groundCacheTimer = 0.2f;
        }
        float surface = lake.SurfaceY;
        float minY;
        if (submerged) minY = groundCacheY + 0.6f;
        else if (diving) minY = surface - diveFloor;
        else minY = surface + 0.6f;
        if (!arriving && !submerged) minY = Mathf.Max(minY, groundCacheY + GroundClearance());
        if (transform.position.y < minY && v.y < 0f) v.y = Mathf.Max(v.y, (minY - transform.position.y) * 4f);

        if (submerged) KeepUnderwater(ref v);

        // Esquiva obstáculos al frente (árboles, rocas)
        if (v.sqrMagnitude > 1f && !diving && !submerged)
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
        Vector3 look = stunTimer > 0f && player != null ? (player.transform.position - transform.position)
                     : (lookOverride.sqrMagnitude > 0.01f ? lookOverride : v);
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

        // Aleteo: rápido al subir o perseguir, planeo en picada, alas recogidas buceando, quieto si está muerto
        float flapRate = flapSpeed;
        float amp = flapAngle;
        if (diving) { amp = 6f; flapRate *= 0.5f; }
        else if (submerged) { amp = 10f; flapRate *= 0.6f; }
        else if (lurking) { amp = 5f; flapRate *= 0.4f; }
        else if (hovering) flapRate *= 2.2f;
        else if (carried.Count > 0 || stunTimer > 0f) flapRate *= 1.5f;
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

        // Postura (levantar el pico, mirar abajo antes de una picada...): cabeceo de todo el modelo
        posePitch = Mathf.MoveTowards(posePitch, dead ? 0f : poseTarget, dt * 140f);
        visual.localRotation = Quaternion.Euler(-posePitch, 0f, 0f) * visualBaseRot;
    }

    float visualScale = 1f;
    public void SetVisualScale(float s) { visualScale = s; }

    // ======================= JUGADOR =======================

    void HitPlayer()
    {
        if (player == null || player.isDead || stunTimer > 0f || dead) return;
        if (IsBoss)
        {
            BumpPlayer();
            return;
        }
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
        // El daño extra de la pausa vulnerable no cuenta como otro disparo
        if (dead || applyingBonus) return;
        hitPunch = 1f;
        FXFactory.SpawnOneShot(hitPrefab, point, Quaternion.identity, 0.8f, 2f);

        if (IsBoss)
        {
            OnBossDamaged(amount);
            return;
        }

        // Si le disparan, va por el jugador (aunque estuviera cazando)
        arriving = false;
        aggroUntil = Time.time + aggroMemory;
        ReleaseTarget();
        CancelSpecialHunt();

        // Del susto suelta al pez (o a todos)
        if (carried.Count > 0) DropFish();
    }

    void DropFish()
    {
        carriedTemp.Clear();
        carriedTemp.AddRange(carried);
        carried.Clear();
        foreach (FishAI fish in carriedTemp) ReleaseFish(fish);
        carriedTemp.Clear();
    }

    // Suelta solo el último pez (el saco del Pelícano jefe)
    void DropOneFish()
    {
        int last = carried.Count - 1;
        if (last < 0) return;
        FishAI fish = carried[last];
        carried.RemoveAt(last);
        ReleaseFish(fish);
    }

    void ReleaseFish(FishAI fish)
    {
        if (fish == null) return;
        fish.Release();
        FishDropped?.Invoke(this, fish);
    }

    void OnDied()
    {
        if (dead) return;
        dead = true;
        diving = false;
        submerged = false;
        lurking = false;
        CancelSpecialHunt();
        ClearBossState();
        ReleaseTarget();
        if (carried.Count > 0) DropFish();

        gameObject.tag = "Untagged";
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
        FXFactory.SpawnOneShot(deathPrefab, transform.position, Quaternion.identity, 1.2f, 3f);

        Died?.Invoke(this);
        Destroy(gameObject, 6f);
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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
        if (hasShore)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shorePoint, 1f);
        }
    }
}
