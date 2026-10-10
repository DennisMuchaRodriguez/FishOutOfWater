using System.Collections.Generic;
using UnityEngine;
using FishGame.AI;

// Ataques especiales de cada especie (BirdAbility en ActorTypes.cs). Cada ave copia cómo pesca el ave real:
//  - Gaviota (SurfaceSnatch): pasada rasante y rápida; solo le interesan los peces de la superficie.
//  - Martín pescador (HoverDive): se queda quieto en el aire sobre el pez y cae en vertical.
//  - Charrán (ChainDive): cuando uno se lanza, los cercanos lo siguen uno tras otro.
//  - Garza (ShoreSpear): se planta en la orilla y arponea a los peces que pasan cerca.
//  - Cormorán (UnderwaterHunter) y serreta (GroupDive): se meten al agua y persiguen peces nadando;
//    las serretas eligen el mismo cardumen y bucean a la vez desde lados distintos.
//  - Pelícano (Scoop): de un bocado se lleva varios peces.
//  - Águila pescadora (HighDive): acecha muy alto y cae desde lejos, rápida y precisa.
public partial class BirdAI
{
    // ---- Estado de las habilidades ----
    bool submerged;          // buceando bajo la superficie
    bool lurking;            // garza quieta en la orilla
    bool hovering;           // martín pescador cernido sobre el pez
    bool inGroup;            // serreta coordinada con otras
    bool groupGo;            // otra serreta del grupo ya se lanzó
    bool chainFollower;      // charrán que sigue la picada de otro (no vuelve a avisar)
    bool chainPending;       // charrán al que le toca lanzarse
    bool lowFlight;          // vuela bajo a propósito (orilla): menos altura mínima sobre el terreno
    bool hasShore;
    Vector3 shorePoint;
    float surfaceCooldown;   // buceadores: espera antes de volver a zambullirse
    float underwaterTimer;   // segundos bajo el agua en esta zambullida
    float chainDiveTimer;    // charrán: cuenta atrás para seguir la picada de otro
    float chainPendingTimer;
    float shoreTimer;        // garza: tiempo plantada en este punto
    float shoreCooldown;     // garza: espera antes de volver a la orilla
    float spearCooldown;
    float stealCooldown;     // reina: espera entre robos
    float groupStart;
    BirdAI stealTarget;

    // Ajustes de la picada según la especie (ConfigureAbility)
    float stalkRadius = 7f;
    float diveSpeedMul = 1f;
    float diveLeadMax = 1.2f;
    float diveTimeout = 3.5f;
    float diveFloor = 1f;       // cuánto puede bajar bajo la superficie durante la picada
    float diveAccelMul = 1.6f;

    const float ShoreSpearRange = 4.5f;

    // Cormorán, serreta y el Cormorán Rey: el agua no es refugio para los peces ni para el jugador
    public bool IsDiver
    {
        get { return Ability == BirdAbility.UnderwaterHunter || Ability == BirdAbility.GroupDive || BossType == BossKind.CormorantKing; }
    }

    // Cuántos peces puede llevar de un bocado
    int ScoopMax
    {
        get
        {
            if (BossType == BossKind.BottomlessPelican) return 5;
            if (Ability == BirdAbility.Scoop && Type != null) return Mathf.Max(1, Type.scoopCount);
            return 1;
        }
    }

    void ConfigureAbility()
    {
        stalkRadius = 7f;
        diveSpeedMul = 1f;
        diveLeadMax = 1.2f;
        diveTimeout = 3.5f;
        diveFloor = 1f;
        diveAccelMul = 1.6f;

        switch (Ability)
        {
            case BirdAbility.SurfaceSnatch:
                stalkRadius = 10f;
                diveSpeedMul = 1.1f;
                diveTimeout = 4f;
                diveFloor = 0.4f;
                maxDiveDistance = 30f;
                stalkTime = new Vector2(0.8f, 1.8f);
                break;
            case BirdAbility.HoverDive:
                diveSpeedMul = 1.35f;
                diveLeadMax = 0.5f;
                diveFloor = 1.6f;
                diveAccelMul = 2.5f;
                stalkTime = new Vector2(0.8f, 1.5f);
                break;
            case BirdAbility.ChainDive:
                stalkRadius = 6f;
                diveSpeedMul = 1.15f;
                diveCooldown = new Vector2(1.2f, 2.2f);
                stalkTime = new Vector2(0.8f, 1.6f);
                break;
            case BirdAbility.ShoreSpear:
                stalkRadius = 9f;
                diveSpeedMul = 0.8f;
                break;
            case BirdAbility.UnderwaterHunter:
            case BirdAbility.GroupDive:
                stalkRadius = 8f;
                diveTimeout = 4.5f;
                break;
            case BirdAbility.Scoop:
                stalkRadius = 9f;
                diveSpeedMul = 0.85f;
                diveTimeout = 4.5f;
                diveFloor = 0.8f;
                break;
            case BirdAbility.HighDive:
                stalkRadius = 6f;
                diveSpeedMul = 1.6f;
                diveLeadMax = 2f;
                diveTimeout = 5f;
                diveAccelMul = 2.4f;
                diveFloor = 0.8f;
                maxDiveDistance = 45f;
                huntRange = Mathf.Max(huntRange, 110f);
                stalkTime = new Vector2(1.5f, 2.5f);
                break;
        }
        if (IsDiver) diveFloor = 3f;
    }

    void TickAbilities(float dt)
    {
        if (surfaceCooldown > 0f) surfaceCooldown -= dt;
        if (shoreCooldown > 0f) shoreCooldown -= dt;
        if (spearCooldown > 0f) spearCooldown -= dt;
        if (stealCooldown > 0f) stealCooldown -= dt;

        // Charrán: le llega el turno en la cadena de picadas
        if (chainDiveTimer > 0f)
        {
            chainDiveTimer -= dt;
            if (chainDiveTimer <= 0f)
            {
                chainPending = true;
                chainFollower = true;
                chainPendingTimer = 3f;
                retargetTimer = 0f;
                if (TargetFish != null)
                {
                    stalkTimer = stalkDuration;
                    diveCooldownTimer = 0f;
                }
            }
        }
        if (chainPending)
        {
            chainPendingTimer -= dt;
            if (chainPendingTimer <= 0f) chainPending = false;
        }
    }

    // ======================= ELEGIR PRESA =======================

    // Penalización del pez según la especie (menos = mejor presa)
    float FishScore(FishAI f)
    {
        float depthPenalty = Mathf.Max(0f, f.Depth - catchDepth) * 10f;
        switch (Ability)
        {
            case BirdAbility.SurfaceSnatch:
                return f.Depth * 12f;
            case BirdAbility.HighDive:
                return depthPenalty * 0.6f - 10f;
            case BirdAbility.Scoop:
                return depthPenalty - CountFishNear(f.transform.position, 6f) * 4f;
        }
        if (IsDiver) return f.Depth * 0.5f;
        return depthPenalty;
    }

    static int CountFishNear(Vector3 p, float radius)
    {
        int n = 0;
        float r2 = radius * radius;
        foreach (FishAI f in FishAI.All)
            if (f != null && f.IsCatchable && (f.transform.position - p).sqrMagnitude < r2) n++;
        return n;
    }

    static FishAI NearestCatchableFish(Vector3 p, float radius, float maxDepth)
    {
        FishAI best = null;
        float bestD = radius * radius;
        foreach (FishAI f in FishAI.All)
        {
            if (f == null || !f.IsCatchable || f.Depth > maxDepth) continue;
            float d = (f.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = f; }
        }
        return best;
    }

    void OnTargetAcquired()
    {
        hovering = false;
        if (chainPending)
        {
            stalkTimer = stalkDuration;
            diveCooldownTimer = 0f;
        }
        if (Ability == BirdAbility.GroupDive) JoinGroup();
    }

    // ======================= SERRETAS EN GRUPO =======================

    void JoinGroup()
    {
        int slot = 0;
        foreach (BirdAI b in All)
        {
            if (b == this || !b.IsAlive || !b.inGroup || b.TargetFish == null) continue;
            if (b.TargetFish.schoolId != TargetFish.schoolId) continue;
            slot++;
        }
        inGroup = true;
        groupGo = false;
        groupStart = Time.time;
        // Cada una se coloca en un lado distinto del cardumen
        orbitAngle += slot * 2.1f;
    }

    bool GroupReady()
    {
        if (stalkTimer < stalkDuration * 0.6f) return false;
        return groupGo || Time.time - groupStart > 2.5f;
    }

    // ======================= MARTÍN PESCADOR =======================

    BTStatus HoverStalk()
    {
        tree.MarkActive("Cernirse sobre el pez");
        Vector3 fish = TargetFish.transform.position;
        Vector3 above = new Vector3(fish.x, lake.SurfaceY + stalkAltitude, fish.z);
        Vector3 to = above - transform.position;
        float flat = new Vector2(to.x, to.z).magnitude;
        if (flat < 2.5f && Mathf.Abs(to.y) < 2.5f)
        {
            // Quieto en el aire, corrigiendo la posición; mira hacia abajo
            hovering = true;
            desiredVelocity = Vector3.ClampMagnitude(to * 2f, patrolSpeed * 0.5f);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f) lookOverride = fwd;
            poseTarget = -35f;
            stalkTimer += Time.fixedDeltaTime;
        }
        else
        {
            hovering = false;
            desiredVelocity = to.normalized * patrolSpeed * 1.2f;
        }
        return BTStatus.Running;
    }

    // ======================= PICADAS EN CADENA / EN GRUPO =======================

    void OnDiveStarted()
    {
        chainPending = false;
        if (Ability == BirdAbility.ChainDive && !chainFollower)
        {
            int k = 0;
            foreach (BirdAI b in All)
            {
                if (b == this || !b.IsAlive || b.Ability != BirdAbility.ChainDive) continue;
                if (b.diving || b.carried.Count > 0 || b.IsAggro || b.chainDiveTimer > 0f) continue;
                if (FlatDistance(b.transform.position, transform.position) > 25f) continue;
                k++;
                b.chainDiveTimer = 0.4f * k + Random.Range(0f, 0.3f);
            }
        }
        else if (Ability == BirdAbility.GroupDive && inGroup && TargetFish != null)
        {
            int school = TargetFish.schoolId;
            foreach (BirdAI b in All)
            {
                if (b == this || !b.IsAlive || !b.inGroup || b.TargetFish == null) continue;
                if (b.TargetFish.schoolId == school) b.groupGo = true;
            }
        }
    }

    // ======================= PELÍCANO =======================

    // El mismo bocado se lleva a los peces de alrededor
    void ScoopAround(Vector3 at)
    {
        int max = ScoopMax;
        float r = catchRadius * 1.8f * Mathf.Max(1f, visualScale * 0.6f);
        float r2 = r * r;
        for (int i = FishAI.All.Count - 1; i >= 0 && carried.Count < max; i--)
        {
            FishAI f = FishAI.All[i];
            if (f == null || !f.IsCatchable) continue;
            if ((f.transform.position - at).sqrMagnitude > r2) continue;
            if (!submerged && f.Depth > catchDepth + 0.8f) continue;
            if (f.Hunter != null && f.Hunter != this) f.Hunter = null;
            GrabFish(f);
        }
    }

    // ======================= REINA ÁGUILA: ROBAR PRESAS =======================

    bool WantsSteal()
    {
        if (BossType != BossKind.EagleQueen || carried.Count > 0 || stealCooldown > 0f)
        {
            stealTarget = null;
            return false;
        }
        if (stealTarget != null && stealTarget.IsAlive && stealTarget.carried.Count > 0) return true;
        stealTarget = null;
        float best = 60f;
        foreach (BirdAI b in All)
        {
            if (b == this || !b.IsAlive || b.carried.Count == 0) continue;
            float d = Vector3.Distance(b.transform.position, transform.position);
            if (d < best)
            {
                best = d;
                stealTarget = b;
            }
        }
        return stealTarget != null;
    }

    BTStatus StealFish()
    {
        tree.MarkActive("Robar la presa");
        if (stealTarget == null || !stealTarget.IsAlive || stealTarget.carried.Count == 0)
        {
            stealTarget = null;
            return BTStatus.Failure;
        }
        Vector3 to = stealTarget.transform.position - transform.position;
        desiredVelocity = to.normalized * chaseSpeed * 1.3f;
        if (to.magnitude < 3f + bodyRadius)
        {
            // Le arrebata los peces en el aire (siguen atrapados: pasan a sus garras)
            List<FishAI> taken = stealTarget.HandOverCarried();
            foreach (FishAI f in taken)
            {
                if (f == null || f.State != FishAI.FishState.Grabbed) continue;
                f.transform.SetParent(talons != null ? talons : transform, true);
                f.transform.localPosition = CarryOffset(carried.Count);
                carried.Add(f);
            }
            if (carried.Count > 0)
            {
                carryTimer = carryTime;
                if (GameDirector.Instance != null)
                    GameDirector.Instance.ShowToast("¡LA REINA LE ROBÓ LA PRESA A OTRA AVE!", GameDirector.Danger);
            }
            stealCooldown = 12f;
            stealTarget = null;
            return BTStatus.Success;
        }
        return BTStatus.Running;
    }

    // Entrega los peces que lleva (sin soltarlos) a quien se los roba
    List<FishAI> HandOverCarried()
    {
        List<FishAI> list = new List<FishAI>(carried);
        carried.Clear();
        recoverTimer = 1.5f;
        return list;
    }

    // ======================= GARZA EN LA ORILLA =======================

    bool WantsShore()
    {
        if (Ability != BirdAbility.ShoreSpear && BossType != BossKind.GiantHeron) return false;
        if (carried.Count > 0 || shoreCooldown > 0f || IsAggro)
        {
            if (lurking) StopLurking();
            return false;
        }
        if (!hasShore && !FindShorePoint()) return false;
        return true;
    }

    bool FindShorePoint()
    {
        Vector3 c = lake.Center;
        float maxR = lake.Radius * 1.6f + 10f;
        for (int t = 0; t < 10; t++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 last = c;
            bool wasWater = false;
            for (float r = 2f; r < maxR; r += 1.5f)
            {
                Vector3 p = c + dir * r;
                if (lake.WaterDepthAt(p.x, p.z) > 0.25f)
                {
                    wasWater = true;
                    last = p;
                    continue;
                }
                if (!wasWater) continue;
                // Primer punto seco después del agua: la orilla
                Vector3 shore = Vector3.Lerp(last, p, 0.5f);
                float ground = lake.GroundHeight(shore.x, shore.z);
                shorePoint = new Vector3(shore.x, Mathf.Max(ground, lake.SurfaceY) + hoverHeight, shore.z);
                hasShore = true;
                shoreTimer = 0f;
                return true;
            }
        }
        return false;
    }

    BTStatus ShoreHunt()
    {
        tree.MarkActive("Arponear desde la orilla");
        lowFlight = true;
        Vector3 to = shorePoint - transform.position;
        if (to.magnitude > 2.5f)
        {
            if (lurking) StopLurking();
            desiredVelocity = to.normalized * patrolSpeed;
            return BTStatus.Running;
        }

        // Plantada: quieta, mirando al agua; arponea al pez que pase cerca
        lurking = true;
        shoreTimer += Time.fixedDeltaTime;
        desiredVelocity = Vector3.ClampMagnitude(to * 2f, 2f);
        Vector3 toLake = lake.Center - transform.position;
        toLake.y = 0f;
        if (toLake.sqrMagnitude > 0.01f) lookOverride = toLake;

        if (spearCooldown <= 0f)
        {
            float reach = ShoreSpearRange * Mathf.Max(1f, visualScale);
            FishAI prey = NearestCatchableFish(talons != null ? talons.position : transform.position, reach, Mathf.Max(1.6f, catchDepth));
            if (prey != null)
            {
                spearCooldown = 1.2f;
                poseTarget = -40f;
                StopLurking();
                shoreCooldown = 6f;
                hasShore = false;
                if (prey.Hunter != null && prey.Hunter != this) prey.Hunter = null;
                Catch(prey);
                return BTStatus.Success;
            }
        }
        // Mucho rato sin suerte: cambia de sitio
        if (shoreTimer > 14f)
        {
            StopLurking();
            hasShore = false;
            shoreCooldown = 2f;
        }
        return BTStatus.Running;
    }

    void StopLurking()
    {
        lurking = false;
        shoreTimer = 0f;
    }

    void CancelSpecialHunt()
    {
        if (lurking) StopLurking();
        hovering = false;
        chainPending = false;
        stealTarget = null;
    }

    float GroundClearance()
    {
        if (lowFlight || lurking) return Mathf.Min(minAltitudeAboveGround, hoverHeight * 0.8f);
        return minAltitudeAboveGround;
    }

    // ======================= BUCEADORES =======================

    void UpdateSubmerged(float dt)
    {
        if (!IsDiver)
        {
            submerged = false;
            return;
        }
        float surface = lake.SurfaceY;
        Vector3 p = transform.position;
        bool inside = lake.IsInsideXZ(p, 0.5f);
        if (!submerged)
        {
            if (inside && p.y < surface - 0.4f)
            {
                submerged = true;
                underwaterTimer = 0f;
                LakeWater.Splash(new Vector3(p.x, surface, p.z), 1f, true);
            }
        }
        else
        {
            underwaterTimer += dt;
            if (p.y > surface + 0.2f || !inside)
            {
                submerged = false;
                surfaceCooldown = Mathf.Max(surfaceCooldown, 2.5f);
                recoverTimer = Mathf.Max(recoverTimer, 1f);
                LakeWater.Splash(new Vector3(p.x, surface, p.z), 0.8f, false);
            }
        }
    }

    BTStatus Underwater()
    {
        tree.MarkActive("Nadar bajo el agua");
        float limit = Type != null ? Mathf.Max(1f, Type.underwaterTime) : 4f;
        if (BossType == BossKind.CormorantKing) limit = Mathf.Max(limit, 6f);
        bool timeUp = underwaterTimer > limit;
        float swim = patrolSpeed * 0.9f;
        Vector3 goal;

        if (!timeUp && IsAggro && player != null && !player.isDead && player.isInWater)
        {
            // Persigue al jugador bajo el agua
            goal = player.transform.position + Vector3.up * 0.3f;
            if (attackTimer <= 0f && Vector3.Distance(transform.position, goal) < attackReach) HitPlayer();
        }
        else if (!timeUp && TargetFish != null && TargetFish.IsCatchable)
        {
            // Persigue al pez nadando
            goal = TargetFish.transform.position;
            Vector3 grab = talons != null ? talons.position : transform.position;
            if (Vector3.Distance(grab, goal) < catchRadius) Catch(TargetFish);
        }
        else
        {
            // Se acabó el aire (o la presa): sale a la superficie y despega
            goal = transform.position + transform.forward * 4f;
            goal.y = lake.SurfaceY + 3f;
            swim = patrolSpeed * 1.2f;
        }
        desiredVelocity = (goal - transform.position).normalized * swim;
        return BTStatus.Running;
    }

    // Bajo el agua todo es más lento
    void KeepUnderwater(ref Vector3 v)
    {
        float maxSpeed = patrolSpeed * 1.3f;
        if (v.sqrMagnitude > maxSpeed * maxSpeed) v = v.normalized * maxSpeed;
    }
}
