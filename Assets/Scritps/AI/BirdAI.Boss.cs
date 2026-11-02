using UnityEngine;
using FishGame.AI;

// Jefes del modo Historia (oleada 3 de los niveles 5, 10, 15 y 20). Reglas del GDD:
//  - Barra de vida visible (GameHUD), 3 fases según la vida y un rugido al cambiar de fase.
//  - Después de cada ataque fuerte, una pausa vulnerable en la que recibe más daño.
// Los jefes no cazan como las demás aves: siempre están cerca del jugador (ACOSO: lo rodean a pocos metros
// y lanzan ataques rápidos) y cada pocos segundos hacen uno de sus ATAQUES FUERTES, que además son una
// amenaza enorme para los peces del lago:
//  - Garza Gris Gigante:      Estocada (la ola aturde peces) · Pisotón en el agua (lanza peces a la orilla) ·
//                             Lluvia de dardos (desde la fase 2)
//  - Pelícano Saco Sin Fondo: Remolino (arrastra a los peces y se traga el centro) · Cañonazo de agua (bolas que
//                             explotan y aturden peces) · Bocado al cardumen (desde la fase 2)
//  - Cormorán Rey:            Torpedos (2-4 embestidas seguidas, arriba y abajo del agua) · Onda expansiva
//                             submarina (aturde a todos los peces cercanos y se los lleva)
//  - Reina Águila Marina:     Picadas desde el cielo (1-3) · Ráfaga de plumas · Vendaval (te empuja y saca a los
//                             peces del agua; desde la fase 2). Roba presas y llama refuerzos al cambiar de fase.
// Si el jugador se esconde en lo hondo (y el jefe no bucea), el jefe ataca a los peces más seguido.
public partial class BirdAI
{
    float phaseChangeTimer;
    float vulnerableTimer;
    bool applyingBonus;
    int bossPhase = 1;
    float bossAttackCooldown = 4f;
    bool bossAttacking;
    float bossAttackTimer;
    int bossAttackStep;
    int bossDivesLeft;
    int bossAttackKind;
    int bossAttackCount;
    float bossTick;              // marca de tiempo dentro del ataque (ráfagas, salpicaduras)
    Vector3 bossAttackPoint;
    AudioSource bossLoop;

    // Acoso entre ataques fuertes
    float harassTimer = 2f;
    bool harassDashing;
    float harassDashTimer;
    Vector3 harassDir;

    public bool IsBoss { get { return BossType != BossKind.None; } }
    public int BossPhase { get { return bossPhase; } }
    public bool IsVulnerable { get { return vulnerableTimer > 0f && !dead; } }
    public string BossName { get { return Type != null && !string.IsNullOrEmpty(Type.name) ? Type.name : "Jefe"; } }

    public event System.Action<BirdAI> BossPhaseChanged;

    void SetupBoss()
    {
        bossPhase = 1;
        bossAttackCooldown = 3.5f;
        aggroMemory = 30f;
        loseRange = 300f;
        huntRange = Mathf.Max(huntRange, 200f);
        knockbackForce *= 1.4f;
        chaseSpeed *= 1.15f;
        patrolSpeed *= 1.1f;
        acceleration *= 1.2f;
        harassTimer = 2.5f;
        if (BossType == BossKind.BottomlessPelican) carryTime = Mathf.Max(carryTime, 6f);
    }

    void TickBoss(float dt)
    {
        if (dead) return;
        if (phaseChangeTimer > 0f) phaseChangeTimer -= dt;
        if (vulnerableTimer > 0f) vulnerableTimer -= dt;
        if (!bossAttacking && !arriving && bossAttackCooldown > 0f)
        {
            // Si te escondes en lo hondo (y no te puede seguir), se desquita antes con los peces
            bossAttackCooldown -= dt * (PlayerHiddenDeep() && !IsDiver ? 1.6f : 1f);
        }

        float h = Health01;
        int phase = h > 0.66f ? 1 : (h > 0.33f ? 2 : 3);
        if (phase > bossPhase)
        {
            bossPhase = phase;
            phaseChangeTimer = 1.6f;
            vulnerableTimer = 0f;
            EndBossAttack(false);
            OnBossPhaseChanged();
        }
    }

    bool PlayerHiddenDeep()
    {
        return player != null && player.isInWater && lake != null && lake.SurfaceY - (player.transform.position.y + 0.5f) > 1.5f;
    }

    void OnBossPhaseChanged()
    {
        GameDirector gd = GameDirector.Instance;
        if (gd != null) gd.ShowToast(BossName.ToUpper() + "  //  FASE " + bossPhase, GameDirector.Danger);
        if (BossType == BossKind.EagleQueen && Type != null && Type.reinforcements != null && gd != null)
            gd.SpawnReinforcements(Type.reinforcements.bird, Mathf.Max(1, Type.reinforcementCount), transform.position);
        if (lake != null && lake.IsInsideXZ(transform.position, 0f))
            LakeWater.Splash(new Vector3(transform.position.x, lake.SurfaceY, transform.position.z), 1.5f, false);
        Sfx.Play(Sonido.RugidoJefe, transform.position, 1f, 0.85f, 30f, 0.05f, 0.5f);
        BossPhaseChanged?.Invoke(this);
    }

    void ClearBossState()
    {
        bossAttacking = false;
        vulnerableTimer = 0f;
        phaseChangeTimer = 0f;
        harassDashing = false;
        StopBossLoop();
    }

    void StopBossLoop()
    {
        if (bossLoop != null) Sfx.StopLoop(bossLoop);
        bossLoop = null;
    }

    static void Toast(string text)
    {
        GameDirector gd = GameDirector.Instance;
        if (gd != null) gd.ShowToast(text, GameDirector.Danger);
    }

    // ======================= RUGIDO Y PAUSA =======================

    BTStatus BossRoar()
    {
        tree.MarkActive("Rugir");
        desiredVelocity = Vector3.up * 1.5f;
        poseTarget = 25f; // pico arriba
        if (player != null)
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) lookOverride = to;
        }
        return BTStatus.Running;
    }

    BTStatus BossVulnerablePause()
    {
        tree.MarkActive("Pausa vulnerable");
        // Agotado: baja cerca del agua y se queda casi quieto, jadeando
        float y = Mathf.Max(lake.SurfaceY + hoverHeight + 1f, groundCacheY + hoverHeight);
        desiredVelocity = new Vector3(0f, (y - transform.position.y) * 1.5f, 0f) + Vector3.up * Mathf.Sin(Time.time * 4f) * 0.5f;
        poseTarget = -15f;
        return BTStatus.Running;
    }

    // ======================= ACOSO (entre ataques fuertes) =======================

    float HarassRadius
    {
        get
        {
            switch (BossType)
            {
                case BossKind.GiantHeron: return 8f;
                case BossKind.BottomlessPelican: return 11f;
                case BossKind.CormorantKing: return 7f;
                default: return 12f;
            }
        }
    }

    float HarassHeight
    {
        get
        {
            switch (BossType)
            {
                case BossKind.GiantHeron: return 2.5f;
                case BossKind.BottomlessPelican: return 5f;
                case BossKind.CormorantKing: return 2f;
                default: return 7f;
            }
        }
    }

    float BossReach { get { return attackReach * Mathf.Max(1f, visualScale * 0.6f); } }

    // Lo rodea de cerca, mirándolo, y cada poco lanza un ataque rápido
    BTStatus BossHarass()
    {
        tree.MarkActive("Acosar al jugador");
        if (BossType == BossKind.EagleQueen && WantsSteal()) return StealFish();

        float dt = Time.fixedDeltaTime;
        Vector3 p = PlayerTarget();
        Vector3 to = p - transform.position;
        bool chaseUnder = IsDiver && player.isInWater && p.y < lake.SurfaceY;

        if (harassDashing)
        {
            harassDashTimer -= dt;
            burst = true;
            desiredVelocity = harassDir * chaseSpeed * 2.3f;
            if (chaseUnder || transform.position.y < lake.SurfaceY) KeepDiving();
            if (attackTimer <= 0f && to.magnitude < BossReach) BumpPlayer(1f);
            if (harassDashTimer <= 0f) harassDashing = false;
            return BTStatus.Running;
        }

        orbitAngle += dt * (0.55f + 0.15f * bossPhase) * orbitDirection;
        float r = HarassRadius;
        float baseY = Mathf.Max(p.y, lake.SurfaceY) + HarassHeight;
        if (chaseUnder) baseY = p.y;
        else if (PlayerHiddenDeep()) baseY = lake.SurfaceY + HarassHeight + 2f;
        Vector3 goal = new Vector3(p.x + Mathf.Cos(orbitAngle) * r, baseY, p.z + Mathf.Sin(orbitAngle) * r);
        Vector3 toGoal = goal - transform.position;
        float speed = toGoal.magnitude > r * 2f ? chaseSpeed * 1.4f : chaseSpeed;
        speed = Mathf.Min(speed, toGoal.magnitude * 3f);
        desiredVelocity = toGoal.normalized * speed;
        lookOverride = BossType == BossKind.GiantHeron ? new Vector3(to.x, 0f, to.z) : to;
        if (chaseUnder) KeepDiving();

        harassTimer -= dt;
        if (harassTimer <= 0f)
        {
            HarassStrike(p, to);
            harassTimer = Random.Range(2.2f, 3.2f) / (1f + 0.25f * (bossPhase - 1));
        }
        return BTStatus.Running;
    }

    void HarassStrike(Vector3 p, Vector3 to)
    {
        bool hidden = PlayerHiddenDeep() && !IsDiver;
        if (hidden) return;
        switch (BossType)
        {
            case BossKind.GiantHeron:
                if (bossPhase >= 2 && Random.value < 0.4f) FeatherFan(3, 24f, 32f, 0.4f);
                else StartHarassDash(p);
                break;
            case BossKind.BottomlessPelican:
                if (to.magnitude < 7f) StartHarassDash(p);
                else
                {
                    for (int i = 0; i < 3; i++) FireAtPlayer(EnemyProjectile.Kind.Escupitajo, 24f, damage * 0.4f, (i - 1) * 7f);
                    Sfx.Play(Sonido.Escupitajo, transform.position, 1f, 0.7f, 10f);
                }
                break;
            case BossKind.CormorantKing:
                StartHarassDash(p);
                break;
            case BossKind.EagleQueen:
                if (Random.value < 0.55f) FeatherFan(3, 20f, 34f, 0.4f);
                else StartHarassDash(p);
                break;
        }
    }

    void StartHarassDash(Vector3 p)
    {
        harassDashing = true;
        harassDashTimer = 0.5f;
        harassDir = (p + player.Velocity * 0.2f - transform.position).normalized;
        Sfx.Play(Sonido.Embestida, transform.position, 1f, 0.7f, 12f);
    }

    // Abanico de plumas afiladas hacia el jugador
    void FeatherFan(int count, float spreadDeg, float speed, float dmgMul)
    {
        for (int i = 0; i < count; i++)
        {
            float a = count > 1 ? Mathf.Lerp(-spreadDeg * 0.5f, spreadDeg * 0.5f, i / (count - 1f)) : 0f;
            FireAtPlayer(EnemyProjectile.Kind.Pluma, speed, damage * dmgMul, a);
        }
        Sfx.Play(Sonido.Plumas, transform.position, 1f, 0.9f, 12f);
    }

    // ======================= ATAQUE FUERTE =======================

    bool BossWantsAttack()
    {
        if (!IsBoss || dead || player == null || player.isDead)
        {
            if (bossAttacking) EndBossAttack(false);
            return false;
        }
        if (bossAttacking) return true;
        if (bossAttackCooldown > 0f || carried.Count > 0) return false;
        bossAttacking = true;
        bossAttackTimer = 0f;
        bossAttackStep = 0;
        bossTick = 0f;
        harassDashing = false;
        bossAttackKind = ChooseBossAttack();
        bossDivesLeft = bossPhase;
        if (lurking) StopLurking();
        ReleaseTarget();
        return true;
    }

    // Rota entre sus ataques; si el jugador se esconde en lo hondo, va por los peces
    int ChooseBossAttack()
    {
        int options = bossPhase >= 2 ? 3 : 2;
        if (BossType == BossKind.CormorantKing) options = 2;
        int kind = bossAttackCount % options;
        bossAttackCount++;
        if (PlayerHiddenDeep() && !IsDiver)
        {
            switch (BossType)
            {
                case BossKind.GiantHeron: kind = 1; break;                           // pisotón
                case BossKind.BottomlessPelican: kind = 0; break;                    // remolino
                case BossKind.EagleQueen: kind = bossPhase >= 2 ? 2 : 0; break;      // vendaval
            }
        }
        return kind;
    }

    BTStatus BossAttackAct()
    {
        tree.MarkActive("Ataque del jefe");
        bossAttackTimer += Time.fixedDeltaTime;
        switch (BossType)
        {
            case BossKind.GiantHeron:
                if (bossAttackKind == 0) return HeronStab();
                if (bossAttackKind == 1) return HeronStomp();
                return FeatherRain(bossPhase >= 3 ? 4 : 3, 5, 40f);
            case BossKind.BottomlessPelican:
                if (bossAttackKind == 0) return PelicanWhirlpool();
                if (bossAttackKind == 1) return PelicanCannon();
                return PelicanGulp();
            case BossKind.CormorantKing:
                return bossAttackKind == 0 ? KingTorpedoes() : KingShockwave();
            case BossKind.EagleQueen:
                if (bossAttackKind == 0) return QueenSkyDive();
                if (bossAttackKind == 1) return FeatherRain(bossPhase >= 3 ? 3 : 2, 7, 60f);
                return QueenGale();
        }
        EndBossAttack(false);
        return BTStatus.Failure;
    }

    void EndBossAttack(bool tired)
    {
        if (!bossAttacking) return;
        bossAttacking = false;
        diving = false;
        StopBossLoop();
        if (tired && Type != null) vulnerableTimer = Mathf.Max(0.5f, Type.vulnerableTime);
        bossAttackCooldown = bossPhase >= 3 ? 2.5f : (bossPhase == 2 ? 3.5f : 4.5f);
    }

    void NextStep()
    {
        bossAttackStep++;
        bossAttackTimer = 0f;
        bossTick = 0f;
    }

    Vector3 PlayerTarget()
    {
        return player.transform.position + Vector3.up * 0.4f;
    }

    // Punto del lago más cercano al jugador (para atacar el agua aunque esté en tierra)
    Vector3 LakePointNearPlayer()
    {
        Vector3 p = player.transform.position;
        if (lake.IsInsideXZ(p, 2f)) return new Vector3(p.x, lake.SurfaceY, p.z);
        Vector3 c = lake.Center;
        Vector3 d = new Vector3(p.x - c.x, 0f, p.z - c.z);
        float r = Mathf.Min(d.magnitude, lake.Radius * 0.8f);
        Vector3 q = c + (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * r;
        for (int i = 0; i < 6 && !lake.IsInsideXZ(q, 2f); i++) q = Vector3.Lerp(q, c, 0.35f);
        return new Vector3(q.x, lake.SurfaceY, q.z);
    }

    // ---- Garza: estocada que levanta una ola (aturde a los peces de alrededor) ----
    BTStatus HeronStab()
    {
        Vector3 target = PlayerTarget();
        Vector3 to = target - transform.position;
        lookOverride = new Vector3(to.x, 0f, to.z);
        if (bossAttackStep == 0)
        {
            lowFlight = true;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 goal = target - flat.normalized * 8f;
            goal.y = Mathf.Max(target.y + 1.5f, lake.SurfaceY + hoverHeight);
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed;
            if (flat.magnitude < 10f || bossAttackTimer > 5f) NextStep();
        }
        else if (bossAttackStep == 1)
        {
            // Carga: levanta el pico (aviso)
            windup = true;
            desiredVelocity = Vector3.zero;
            poseTarget = 35f;
            if (bossAttackTimer > 0.7f)
            {
                NextStep();
                bossAttackPoint = target + player.Velocity * 0.2f;
                Sfx.Play(Sonido.Estocada, transform.position, 1f, 0.7f, 14f);
            }
        }
        else
        {
            poseTarget = -25f;
            burst = true;
            desiredVelocity = (bossAttackPoint - transform.position).normalized * chaseSpeed * 2.4f;
            KeepDiving();
            if (attackTimer <= 0f && Vector3.Distance(transform.position, target) < BossReach) BumpPlayer(1.5f);
            if (bossAttackTimer > 0.8f || Vector3.Distance(transform.position, bossAttackPoint) < 1.5f)
            {
                MakeWave(transform.position, 10f);
                int n = StunFishAround(transform.position, 9f, 3.5f, 3.5f);
                if (n >= 3) Toast("¡LA OLA DE LA GARZA ATURDIÓ A " + n + " PECES!");
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Garza: pisotón en el agua que lanza a los peces fuera (dos seguidos en la fase 3) ----
    BTStatus HeronStomp()
    {
        if (bossAttackStep == 0)
        {
            // Sube sobre el agua junto al jugador
            if (bossAttackTimer <= Time.fixedDeltaTime * 1.5f) bossAttackPoint = LakePointNearPlayer();
            Vector3 above = bossAttackPoint + Vector3.up * 10f;
            desiredVelocity = (above - transform.position).normalized * chaseSpeed * 1.2f;
            if (FlatDistance(above, transform.position) < 3f || bossAttackTimer > 4f) NextStep();
        }
        else if (bossAttackStep == 1)
        {
            windup = true;
            desiredVelocity = Vector3.up * 0.8f;
            poseTarget = 30f;
            if (bossAttackTimer > 0.5f)
            {
                NextStep();
                Call(1f);
            }
        }
        else
        {
            // Cae con todo su peso
            burst = true;
            KeepDiving();
            Vector3 down = bossAttackPoint - transform.position;
            desiredVelocity = new Vector3(down.x * 2f, -diveSpeed * 1.4f, down.z * 2f);
            poseTarget = -20f;
            if (transform.position.y < lake.SurfaceY + 1f || bossAttackTimer > 1.4f)
            {
                Vector3 c = new Vector3(transform.position.x, lake.SurfaceY, transform.position.z);
                float radius = bossPhase >= 3 ? 18f : 14f;
                LakeWater.Splash(c, 1f, true);
                Sfx.Play(Sonido.Pisoton, c, 1f, 0.9f, 30f, 0.05f, 0.2f);
                Shockwave(c, radius, 0.9f, 15f);
                int n = LaunchFishAround(c, radius, 3.5f, 8.5f);
                if (n > 0) Toast("¡EL PISOTÓN LANZÓ " + n + (n == 1 ? " PEZ" : " PECES") + " FUERA DEL AGUA!");
                bossDivesLeft--;
                if (bossDivesLeft > 1 && bossPhase >= 3)   // fase 3: dos pisotones
                {
                    bossAttackStep = 0;
                    bossAttackTimer = 0f;
                    return BTStatus.Running;
                }
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Garza / Reina: lluvia de plumas (varias ráfagas en abanico) ----
    BTStatus FeatherRain(int volleys, int perVolley, float spread)
    {
        Vector3 p = PlayerTarget();
        Vector3 to = p - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        Vector3 goal = p - (flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward) * 13f;
        goal.y = Mathf.Max(p.y, lake.SurfaceY) + 5f;
        desiredVelocity = (goal - transform.position).normalized * Mathf.Min(chaseSpeed, (goal - transform.position).magnitude * 3f);
        lookOverride = to;
        windup = true;
        if (bossAttackTimer - bossTick > 0.6f && bossAttackTimer > 0.5f)
        {
            bossTick = bossAttackTimer;
            FeatherFan(perVolley, spread, 34f, 0.45f);
            bossAttackStep++;
            if (bossAttackStep >= volleys)
            {
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Pelícano: remolino que arrastra a los peces y se traga el centro ----
    BTStatus PelicanWhirlpool()
    {
        if (bossAttackStep == 0)
        {
            if (bossAttackTimer <= Time.fixedDeltaTime * 1.5f) bossAttackPoint = DensestFishPoint();
            Vector3 above = bossAttackPoint + Vector3.up * 4.5f;
            desiredVelocity = (above - transform.position).normalized * chaseSpeed * 1.1f;
            if (FlatDistance(above, transform.position) < 4f || bossAttackTimer > 5f)
            {
                NextStep();
                bossLoop = Sfx.Loop(Sonido.Remolino, transform, 1f, 14f);
                Toast("¡REMOLINO! EL PELÍCANO ARRASTRA A LOS PECES");
            }
        }
        else if (bossAttackStep == 1)
        {
            // Gira sobre el centro batiendo las alas: el agua da vueltas y lo arrastra todo hacia él
            float dt = Time.fixedDeltaTime;
            float spin = bossAttackTimer * 2.4f;
            Vector3 c = bossAttackPoint;
            Vector3 hover = c + new Vector3(Mathf.Cos(spin) * 2f, 4.5f, Mathf.Sin(spin) * 2f);
            desiredVelocity = (hover - transform.position) * 3f;
            windup = true;
            const float radius = 18f;
            foreach (FishAI f in FishAI.All)
            {
                if (f == null || !f.IsCatchable) continue;
                Vector3 d = c - f.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m > radius || m < 0.3f) continue;
                Vector3 inward = d / m;
                Vector3 tangent = new Vector3(-inward.z, 0f, inward.x);
                f.Push((inward * 14f + tangent * 9f + Vector3.up * 2f) * dt);
            }
            if (player != null && player.isInWater)
            {
                Vector3 d = c - player.transform.position;
                d.y = 0f;
                if (d.magnitude < radius && d.magnitude > 1f)
                {
                    Vector3 inward = d.normalized;
                    player.AddCurrent((inward * 9f + new Vector3(-inward.z, 0f, inward.x) * 5f) * dt);
                }
            }
            if (bossAttackTimer - bossTick > 0.12f)
            {
                bossTick = bossAttackTimer;
                float a = spin * 1.7f;
                float rr = Random.Range(3f, 12f);
                LakeWater.Splash(c + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr), 0.3f, false);
            }
            if (bossAttackTimer > (bossPhase >= 3 ? 4.2f : 3.2f)) NextStep();
        }
        else
        {
            // Se zambulle en el centro con el saco abierto
            KeepDiving();
            burst = true;
            Vector3 c = bossAttackPoint;
            Vector3 grab = talons != null ? talons.position : transform.position;
            desiredVelocity = (c + Vector3.down * 0.5f - grab).normalized * diveSpeed;
            if (grab.y < lake.SurfaceY + 0.8f || bossAttackTimer > 2f)
            {
                LakeWater.Splash(c, 1f, true);
                StopBossLoop();
                FishAI first = NearestCatchableFish(c, 7f, 4f);
                if (first != null)
                {
                    if (first.Hunter != null && first.Hunter != this) first.Hunter = null;
                    Catch(first);
                    if (carried.Count > 1) Toast("¡EL PELÍCANO SE TRAGÓ " + carried.Count + " PECES!  DISPÁRALE AL SACO");
                }
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Pelícano: cañonazo de bolas de agua en arco (explotan; en el lago aturden peces) ----
    BTStatus PelicanCannon()
    {
        Vector3 p = PlayerTarget();
        Vector3 to = p - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        Vector3 goal = p - (flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward) * 14f;
        goal.y = Mathf.Max(p.y, lake.SurfaceY) + 6f;
        desiredVelocity = (goal - transform.position).normalized * Mathf.Min(chaseSpeed, (goal - transform.position).magnitude * 3f);
        lookOverride = to;
        windup = true;
        int shots = bossPhase >= 3 ? 5 : 3;
        if (bossAttackTimer - bossTick > 0.7f && bossAttackTimer > 0.6f)
        {
            bossTick = bossAttackTimer;
            Vector3 from = transform.position + transform.forward * bodyRadius;
            Vector3 aim = player.transform.position + player.Velocity * 1.1f;
            if (PlayerHiddenDeep()) aim = DensestFishPoint();   // escondido: le dispara al cardumen
            Vector3 v = EnemyProjectile.ArcVelocity(from, aim, 1.1f, EnemyProjectile.GravityOf(EnemyProjectile.Kind.BolaDeAgua));
            EnemyProjectile.Fire(EnemyProjectile.Kind.BolaDeAgua, from, v, damage * 0.8f, player);
            Sfx.Play(Sonido.Escupitajo, transform.position, 1f, 0.55f, 14f);
            bossAttackStep++;
            if (bossAttackStep >= shots)
            {
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Pelícano: se lanza sobre el cardumen más apretado y traga todo lo que puede ----
    BTStatus PelicanGulp()
    {
        if (bossAttackStep == 0)
        {
            FishAI best = null;
            int bestN = -1;
            foreach (FishAI f in FishAI.All)
            {
                if (f == null || !f.IsCatchable) continue;
                int n = CountFishNear(f.transform.position, 7f);
                if (n > bestN)
                {
                    bestN = n;
                    best = f;
                }
            }
            if (best == null)
            {
                EndBossAttack(false);
                return BTStatus.Failure;
            }
            if (best.Hunter != null && best.Hunter != this) best.Hunter = null;
            TargetFish = best;
            best.Hunter = this;
            NextStep();
        }
        if (TargetFish == null || !TargetFish.IsCatchable)
        {
            EndBossAttack(carried.Count > 0);
            return BTStatus.Failure;
        }
        Vector3 fish = TargetFish.transform.position;
        Vector3 grab = talons != null ? talons.position : transform.position;
        if (bossAttackStep == 1)
        {
            Vector3 above = new Vector3(fish.x, lake.SurfaceY + 12f, fish.z);
            desiredVelocity = (above - transform.position).normalized * patrolSpeed * 1.4f;
            if (FlatDistance(above, transform.position) < 6f || bossAttackTimer > 5f) NextStep();
        }
        else
        {
            KeepDiving();
            Vector3 aim = fish;
            aim.y = Mathf.Max(aim.y, lake.SurfaceY - catchDepth * 0.5f);
            desiredVelocity = (aim - grab).normalized * diveSpeed;
            float reach = catchRadius * Mathf.Max(1f, visualScale * 0.6f);
            if (Vector3.Distance(grab, fish) < reach && TargetFish.Depth <= catchDepth + 0.8f)
            {
                Catch(TargetFish);
                EndBossAttack(true);
                return BTStatus.Success;
            }
            if (bossAttackTimer > 3.5f)
            {
                EndBossAttack(true);
                return BTStatus.Failure;
            }
        }
        return BTStatus.Running;
    }

    // ---- Cormorán Rey: torpedos (fase 1: 2, fase 2: 3, fase 3: 4), arriba y abajo del agua ----
    BTStatus KingTorpedoes()
    {
        Vector3 p = PlayerTarget();
        Vector3 to = p - transform.position;
        bool under = player.isInWater && p.y < lake.SurfaceY;
        if (bossAttackStep == 0)
        {
            // Se aleja un poco para tomar impulso, a la altura (o profundidad) del jugador
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 goal = p - (flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward) * 12f;
            goal.y = under ? p.y : Mathf.Max(p.y, lake.SurfaceY + 1.5f);
            if (under) KeepDiving();
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed;
            lookOverride = to;
            if ((goal - transform.position).magnitude < 4f || bossAttackTimer > 2.5f) NextStep();
        }
        else if (bossAttackStep == 1)
        {
            windup = true;
            if (under) KeepDiving();
            desiredVelocity = Vector3.zero;
            lookOverride = to;
            if (bossAttackTimer > 0.35f)
            {
                NextStep();
                bossAttackPoint = (p + player.Velocity * 0.25f - transform.position).normalized;
                Sfx.Play(Sonido.Embestida, transform.position, 1f, 0.6f, 14f);
            }
        }
        else
        {
            burst = true;
            if (under || transform.position.y < lake.SurfaceY) KeepDiving();
            desiredVelocity = bossAttackPoint * chaseSpeed * 2.8f;
            if (attackTimer <= 0f && to.magnitude < BossReach) BumpPlayer(1.3f);
            bool passed = Vector3.Dot(transform.position - p, bossAttackPoint) > 5f;
            if (passed || bossAttackTimer > 1f)
            {
                // bossDivesLeft empieza en la fase: fase + 1 torpedos en total
                bossDivesLeft--;
                if (bossDivesLeft >= 0)
                {
                    bossAttackStep = 0;
                    bossAttackTimer = 0f;
                    return BTStatus.Running;
                }
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Cormorán Rey: onda expansiva bajo el agua (aturde a los peces y luego se los lleva) ----
    BTStatus KingShockwave()
    {
        if (bossAttackStep == 0)
        {
            if (bossAttackTimer <= Time.fixedDeltaTime * 1.5f)
            {
                Vector3 c = DensestFishPoint();
                float ground = lake.GroundHeight(c.x, c.z);
                bossAttackPoint = new Vector3(c.x, Mathf.Max(lake.SurfaceY - 3f, ground + 1.2f), c.z);
            }
            KeepDiving();
            Vector3 to = bossAttackPoint - transform.position;
            desiredVelocity = to.normalized * chaseSpeed * 1.1f;
            if ((submerged && to.magnitude < 4f) || bossAttackTimer > 5f) NextStep();
        }
        else if (bossAttackStep == 1)
        {
            // Carga: burbujas y temblor
            KeepDiving();
            windup = true;
            desiredVelocity = (bossAttackPoint - transform.position) * 2f;
            if (bossAttackTimer - bossTick > 0.15f)
            {
                bossTick = bossAttackTimer;
                Vector3 jitter = Random.insideUnitSphere * 2f;
                LakeWater.Splash(new Vector3(transform.position.x + jitter.x, lake.SurfaceY, transform.position.z + jitter.z), 0.2f, false);
            }
            if (bossAttackTimer > 0.9f)
            {
                Vector3 c = transform.position;
                float radius = bossPhase >= 3 ? 19f : 15f;
                Sfx.Play(Sonido.OndaSubacuatica, c, 1f, 1f, 30f, 0.05f, 0.2f);
                LakeWater.Splash(new Vector3(c.x, lake.SurfaceY, c.z), 1f, false);
                int n = StunFishAround(c, radius, 4.5f, 100f);
                if (player != null && player.isInWater) Shockwave(c, 11f, 1f, 12f);
                Toast(n > 0 ? "¡ONDA EXPANSIVA! " + n + (n == 1 ? " PEZ ATURDIDO" : " PECES ATURDIDOS") : "¡ONDA EXPANSIVA!");
                NextStep();
            }
        }
        else
        {
            // Se lleva a los peces aturdidos que tenga más cerca
            KeepDiving();
            FishAI prey = NearestStunnedFish(transform.position, 16f);
            if (prey == null || bossAttackTimer > 3f)
            {
                EndBossAttack(true);
                return BTStatus.Success;
            }
            Vector3 grab = talons != null ? talons.position : transform.position;
            desiredVelocity = (prey.transform.position - grab).normalized * chaseSpeed;
            if (Vector3.Distance(grab, prey.transform.position) < catchRadius * Mathf.Max(1f, visualScale * 0.6f))
            {
                if (prey.Hunter != null && prey.Hunter != this) prey.Hunter = null;
                Catch(prey);
                if (carried.Count > 0) Toast("¡EL CORMORÁN REY SE LLEVA PECES ATURDIDOS!");
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Reina Águila: picadas desde el cielo (1-3 según la fase) ----
    BTStatus QueenSkyDive()
    {
        Vector3 target = PlayerTarget();
        if (bossAttackStep == 0)
        {
            Vector3 up = new Vector3(target.x + 6f, Mathf.Max(target.y, lake.SurfaceY) + 24f, target.z + 6f);
            desiredVelocity = (up - transform.position).normalized * patrolSpeed * 1.7f;
            if ((up - transform.position).magnitude < 6f || bossAttackTimer > 3.5f) NextStep();
        }
        else if (bossAttackStep == 1)
        {
            windup = true;
            desiredVelocity = Vector3.up;
            lookOverride = target - transform.position;
            poseTarget = -30f;
            if (bossAttackTimer > 0.35f)
            {
                NextStep();
                Sfx.Play(Sonido.Picada, transform.position, 1f, 0.7f, 30f, 0.05f, 0.2f);
            }
        }
        else
        {
            KeepDiving();
            burst = true;
            Vector3 lead = target + player.Velocity * 0.35f;
            desiredVelocity = (lead - transform.position).normalized * diveSpeed * 1.7f;
            poseTarget = -30f;
            bool hit = attackTimer <= 0f && Vector3.Distance(transform.position, target) < BossReach;
            if (hit) BumpPlayer(1.5f);
            bool intoWater = transform.position.y < lake.SurfaceY + 0.8f;
            bool passed = transform.position.y < target.y - 2f || intoWater;
            if (hit || passed || bossAttackTimer > 3f)
            {
                if (intoWater && lake.IsInsideXZ(transform.position, 0f))
                {
                    // Al chocar con el agua levanta una ola y saca peces volando
                    Vector3 c = new Vector3(transform.position.x, lake.SurfaceY, transform.position.z);
                    MakeWave(c, 8f);
                    LaunchFishAround(c, 7f, 2.5f, 6f);
                }
                bossDivesLeft--;
                if (bossDivesLeft > 0)
                {
                    bossAttackStep = 0;
                    bossAttackTimer = 0f;
                    return BTStatus.Running;
                }
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // ---- Reina Águila: vendaval (te empuja lejos y saca a los peces del agua) ----
    BTStatus QueenGale()
    {
        Vector3 p = PlayerTarget();
        Vector3 focus = PlayerHiddenDeep() ? DensestFishPoint() : p;
        Vector3 to = focus - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        Vector3 dir = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
        if (bossAttackStep == 0)
        {
            Vector3 goal = focus - dir * 11f;
            goal.y = Mathf.Max(focus.y, lake.SurfaceY) + 3.5f;
            desiredVelocity = (goal - transform.position).normalized * Mathf.Min(chaseSpeed * 1.2f, (goal - transform.position).magnitude * 3f);
            lookOverride = flat;
            if ((goal - transform.position).magnitude < 3f || bossAttackTimer > 4f)
            {
                NextStep();
                Sfx.Play(Sonido.Vendaval, transform.position, 1f, 1f, 25f, 0.05f, 0.2f);
            }
        }
        else if (bossAttackStep == 1)
        {
            // Carga: aletazos enormes
            windup = true;
            desiredVelocity = -dir * 1.5f;
            lookOverride = flat;
            poseTarget = 20f;
            if (bossAttackTimer > 1.1f)
            {
                NextStep();
                Gust(dir);
            }
        }
        else
        {
            desiredVelocity = -dir * 2f + Vector3.up;
            lookOverride = flat;
            if (bossAttackTimer > 0.8f)
            {
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    void Gust(Vector3 dir)
    {
        Vector3 origin = transform.position;
        const float range = 28f;
        const float cone = 0.8f;   // coseno: unos 37° a cada lado
        if (player != null && !player.isDead)
        {
            Vector3 d = player.transform.position - origin;
            Vector3 df = new Vector3(d.x, 0f, d.z);
            if (df.magnitude < range && Vector3.Dot(df.normalized, dir) > cone)
            {
                player.TakeHit(damage * 0.4f, Vector3.zero);
                player.ApplyKnockback(dir * 22f + Vector3.up * 6f, 0.6f);
            }
        }
        int launched = 0;
        foreach (FishAI f in FishAI.All.ToArray())
        {
            if (f == null || !f.IsCatchable || f.Depth > 3f) continue;
            Vector3 d = f.transform.position - origin;
            Vector3 df = new Vector3(d.x, 0f, d.z);
            if (df.magnitude > range - 2f || Vector3.Dot(df.normalized, dir) < cone) continue;
            if (f.Launch(dir * Random.Range(6f, 9f) + Vector3.up * Random.Range(7f, 10f))) launched++;
        }
        // Espuma a lo largo del viento
        for (int i = 1; i <= 6; i++)
        {
            Vector3 q = origin + dir * (i * 4f);
            if (lake.IsInsideXZ(q, 0f)) LakeWater.Splash(new Vector3(q.x, lake.SurfaceY, q.z), 0.35f, false);
        }
        Toast(launched > 0 ? "¡VENDAVAL! " + launched + (launched == 1 ? " PEZ SALIÓ VOLANDO" : " PECES SALIERON VOLANDO") : "¡VENDAVAL!");
    }

    // ======================= EFECTOS SOBRE PECES Y JUGADOR =======================

    // Ola: salpicadura grande y empujón si el jugador está cerca del agua
    void MakeWave(Vector3 at, float radius)
    {
        if (lake == null) return;
        Vector3 p = new Vector3(at.x, lake.SurfaceY, at.z);
        if (lake.IsInsideXZ(p, 0f)) LakeWater.Splash(p, 1f, true);
        Sfx.Play(Sonido.Chapuzon, p, 1f, 0.6f, 16f, 0.05f, 0.1f);
        if (player == null || player.isDead) return;
        Vector3 d = player.transform.position - p;
        float h = player.transform.position.y - lake.SurfaceY;
        d.y = 0f;
        if (d.magnitude < radius && h < 3f)
        {
            Vector3 push = (d.sqrMagnitude > 0.01f ? d.normalized : transform.forward) * knockbackForce * 0.8f + Vector3.up * knockbackUp;
            player.ApplyKnockback(push, 0.5f);
        }
    }

    // Onda de choque: daño y empujón hacia fuera si el jugador está dentro del radio (y cerca en altura)
    void Shockwave(Vector3 c, float radius, float damageMul, float push)
    {
        if (player == null || player.isDead) return;
        Vector3 d = player.transform.position - c;
        if (Mathf.Abs(d.y) > 6f) return;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        float m = flat.magnitude;
        if (m > radius) return;
        float k = 1f - 0.5f * (m / radius);
        Vector3 dir = m > 0.01f ? flat / m : transform.forward;
        player.TakeHit(damage * damageMul * k, dir * push * k + Vector3.up * push * 0.6f);
    }

    int StunFishAround(Vector3 c, float radius, float seconds, float maxDepth)
    {
        int n = 0;
        float r2 = radius * radius;
        foreach (FishAI f in FishAI.All)
        {
            if (f == null || !f.IsCatchable || f.Depth > maxDepth) continue;
            if ((f.transform.position - c).sqrMagnitude > r2) continue;
            f.Stun(seconds);
            n++;
        }
        return n;
    }

    // Lanza a los peces cercanos fuera del agua, hacia afuera del golpe (pueden caer en la orilla)
    int LaunchFishAround(Vector3 c, float radius, float maxDepth, float power)
    {
        int n = 0;
        float r2 = radius * radius;
        foreach (FishAI f in FishAI.All.ToArray())
        {
            if (f == null || !f.IsCatchable || f.Depth > maxDepth) continue;
            Vector3 d = f.transform.position - c;
            d.y = 0f;
            if (d.sqrMagnitude > r2) continue;
            Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            float k = Random.Range(0.8f, 1.2f);
            if (f.Launch(dir * power * k + Vector3.up * power * 1.2f * k)) n++;
        }
        return n;
    }

    // Donde hay más peces juntos (prefiere los cercanos al jugador); el lago junto al jugador si no hay peces
    Vector3 DensestFishPoint()
    {
        FishAI best = null;
        int bestN = -1;
        foreach (FishAI f in FishAI.All)
        {
            if (f == null || !f.IsCatchable) continue;
            int n = CountFishNear(f.transform.position, 8f);
            if (player != null && Vector3.Distance(f.transform.position, player.transform.position) < 30f) n += 2;
            if (n > bestN)
            {
                bestN = n;
                best = f;
            }
        }
        if (best == null) return LakePointNearPlayer();
        Vector3 p = best.transform.position;
        return new Vector3(p.x, lake.SurfaceY, p.z);
    }

    static FishAI NearestStunnedFish(Vector3 p, float radius)
    {
        FishAI best = null;
        float bestD = radius * radius;
        foreach (FishAI f in FishAI.All)
        {
            if (f == null || !f.IsStunned) continue;
            float d = (f.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = f; }
        }
        return best;
    }

    // ======================= GOLPES Y DAÑO =======================

    // Golpe de jefe: más empujón y no se queda aturdido (sigue con su patrón)
    void BumpPlayer(float mul = 1f)
    {
        if (attackTimer > 0f || player == null || player.isDead) return;
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();
        player.TakeHit(damage * mul, (dir * knockbackForce + Vector3.up * knockbackUp * 1.3f) * Mathf.Sqrt(mul));
        attackTimer = 1.2f;
        aggroUntil = Time.time + aggroMemory;
    }

    void OnBossDamaged(int amount)
    {
        arriving = false;
        aggroUntil = Time.time + aggroMemory;

        // Pausa vulnerable: el disparo hace daño extra
        if (vulnerableTimer > 0f && health != null && !health.IsDead && Type != null && Type.vulnerableDamageMultiplier > 1f)
        {
            int extra = Mathf.RoundToInt(amount * (Type.vulnerableDamageMultiplier - 1f));
            if (extra > 0)
            {
                applyingBonus = true;
                health.TakeDamage(extra, transform.position);
                applyingBonus = false;
            }
        }

        // El saco del pelícano: cada impacto le saca un pez. Los demás jefes a veces sueltan uno
        if (carried.Count > 0 && (BossType == BossKind.BottomlessPelican || Random.value < 0.35f)) DropOneFish();
    }
}
