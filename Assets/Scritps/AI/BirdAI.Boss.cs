using UnityEngine;
using FishGame.AI;

// Jefes del modo Historia (oleada 3 de los niveles 5, 10, 15 y 20). Reglas del GDD:
//  - Barra de vida visible (GameHUD), 3 fases según la vida y un rugido al cambiar de fase.
//  - Después de cada ataque fuerte, una pausa vulnerable en la que recibe más daño.
//  - Garza Gris Gigante: acecha en la orilla y lanza estocadas largas que levantan olas.
//  - Pelícano Saco Sin Fondo: traga cardúmenes enteros; cada disparo al saco le saca un pez.
//  - Cormorán Rey: pelea arriba y abajo del agua y te persigue bajo la superficie.
//  - Reina Águila Marina: roba presas a otras aves, llama refuerzos al cambiar de fase y hace picadas
//    largas desde el cielo (dos seguidas en la última fase).
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
    Vector3 bossAttackPoint;

    public bool IsBoss { get { return BossType != BossKind.None; } }
    public int BossPhase { get { return bossPhase; } }
    public bool IsVulnerable { get { return vulnerableTimer > 0f && !dead; } }
    public string BossName { get { return Type != null && !string.IsNullOrEmpty(Type.name) ? Type.name : "Jefe"; } }

    public event System.Action<BirdAI> BossPhaseChanged;

    void SetupBoss()
    {
        bossPhase = 1;
        bossAttackCooldown = 5f;
        aggroMemory = 30f;
        loseRange = 250f;
        huntRange = Mathf.Max(huntRange, 200f);
        knockbackForce *= 1.4f;
        if (BossType == BossKind.BottomlessPelican) carryTime = Mathf.Max(carryTime, 6f);
    }

    void TickBoss(float dt)
    {
        if (dead) return;
        if (phaseChangeTimer > 0f) phaseChangeTimer -= dt;
        if (vulnerableTimer > 0f) vulnerableTimer -= dt;
        if (!bossAttacking && !arriving && bossAttackCooldown > 0f) bossAttackCooldown -= dt;

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

    void OnBossPhaseChanged()
    {
        GameDirector gd = GameDirector.Instance;
        if (gd != null) gd.ShowToast(BossName.ToUpper() + "  //  FASE " + bossPhase, GameDirector.Danger);
        if (BossType == BossKind.EagleQueen && Type != null && Type.reinforcements != null && gd != null)
            gd.SpawnReinforcements(Type.reinforcements.bird, Mathf.Max(1, Type.reinforcementCount), transform.position);
        if (lake != null && lake.IsInsideXZ(transform.position, 0f))
            LakeWater.Splash(new Vector3(transform.position.x, lake.SurfaceY, transform.position.z), 1.5f, false);
        BossPhaseChanged?.Invoke(this);
    }

    void ClearBossState()
    {
        bossAttacking = false;
        vulnerableTimer = 0f;
        phaseChangeTimer = 0f;
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
        bossDivesLeft = bossPhase >= 3 ? 2 : 1;
        if (lurking) StopLurking();
        ReleaseTarget();
        return true;
    }

    BTStatus BossAttackAct()
    {
        tree.MarkActive("Ataque del jefe");
        bossAttackTimer += Time.fixedDeltaTime;
        switch (BossType)
        {
            case BossKind.GiantHeron: return HeronStab();
            case BossKind.BottomlessPelican: return PelicanGulp();
            case BossKind.CormorantKing: return KingHunt();
            case BossKind.EagleQueen: return QueenSkyDive();
        }
        EndBossAttack(false);
        return BTStatus.Failure;
    }

    void EndBossAttack(bool tired)
    {
        if (!bossAttacking) return;
        bossAttacking = false;
        diving = false;
        if (tired && Type != null) vulnerableTimer = Mathf.Max(0.5f, Type.vulnerableTime);
        bossAttackCooldown = bossPhase >= 3 ? 3f : (bossPhase == 2 ? 4.5f : 6f);
    }

    Vector3 PlayerTarget()
    {
        return player.transform.position + Vector3.up * 0.4f;
    }

    // Garza Gris Gigante: se acerca bajo, levanta el pico (punto débil) y lanza una estocada larga que levanta una ola
    BTStatus HeronStab()
    {
        Vector3 target = PlayerTarget();
        Vector3 to = target - transform.position;
        lookOverride = new Vector3(to.x, 0f, to.z);
        if (bossAttackStep == 0)
        {
            // Acercarse a unos metros del jugador, volando bajo
            lowFlight = true;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 goal = target - flat.normalized * 8f;
            goal.y = Mathf.Max(target.y + 1.5f, lake.SurfaceY + hoverHeight);
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed;
            if (flat.magnitude < 10f || bossAttackTimer > 6f)
            {
                bossAttackStep = 1;
                bossAttackTimer = 0f;
            }
        }
        else if (bossAttackStep == 1)
        {
            // Carga: levanta el pico
            desiredVelocity = Vector3.zero;
            poseTarget = 35f;
            if (bossAttackTimer > 0.7f)
            {
                bossAttackStep = 2;
                bossAttackTimer = 0f;
                bossAttackPoint = target;
            }
        }
        else
        {
            // Estocada
            poseTarget = -25f;
            desiredVelocity = (bossAttackPoint - transform.position).normalized * chaseSpeed * 2.2f;
            KeepDiving();
            if (attackTimer <= 0f && Vector3.Distance(transform.position, target) < attackReach * Mathf.Max(1f, visualScale * 0.6f))
                BumpPlayer();
            if (bossAttackTimer > 0.8f || Vector3.Distance(transform.position, bossAttackPoint) < 1.5f)
            {
                MakeWave(transform.position, 9f);
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // Ola de la estocada: salpicadura grande y empujón si el jugador está cerca del agua
    void MakeWave(Vector3 at, float radius)
    {
        if (lake == null) return;
        Vector3 p = new Vector3(at.x, lake.SurfaceY, at.z);
        if (lake.IsInsideXZ(p, 0f)) LakeWater.Splash(p, 2.5f, true);
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

    // Pelícano Saco Sin Fondo: se lanza sobre el cardumen más apretado y traga todo lo que puede
    BTStatus PelicanGulp()
    {
        if (bossAttackStep == 0)
        {
            // Elegir el pez con más vecinos
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
            bossAttackStep = 1;
            bossAttackTimer = 0f;
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
            // Sube sobre el cardumen
            Vector3 above = new Vector3(fish.x, lake.SurfaceY + 12f, fish.z);
            desiredVelocity = (above - transform.position).normalized * patrolSpeed * 1.3f;
            if (FlatDistance(above, transform.position) < 6f || bossAttackTimer > 5f)
            {
                bossAttackStep = 2;
                bossAttackTimer = 0f;
            }
        }
        else
        {
            // Bocado: cae con el saco abierto
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

    // Cormorán Rey: persigue al jugador arriba y abajo del agua
    BTStatus KingHunt()
    {
        Vector3 target = PlayerTarget();
        float speed = chaseSpeed * 1.15f;
        if (player.isInWater && target.y < lake.SurfaceY && lake.IsInsideXZ(target, 1f))
        {
            // Se zambulle tras él (bajo el agua Move limita la velocidad)
            KeepDiving();
            if (submerged) speed = patrolSpeed * 1.2f;
        }
        desiredVelocity = (target - transform.position).normalized * speed;
        if (attackTimer <= 0f && Vector3.Distance(transform.position, target) < attackReach * Mathf.Max(1f, visualScale * 0.5f))
        {
            BumpPlayer();
            EndBossAttack(true);
            return BTStatus.Success;
        }
        if (bossAttackTimer > 6f)
        {
            EndBossAttack(true);
            return BTStatus.Failure;
        }
        return BTStatus.Running;
    }

    // Reina Águila Marina: sube muy alto sobre el jugador y cae en picada (dos veces en la última fase)
    BTStatus QueenSkyDive()
    {
        Vector3 target = PlayerTarget();
        if (bossAttackStep == 0)
        {
            Vector3 up = new Vector3(target.x + 6f, Mathf.Max(target.y, lake.SurfaceY) + 26f, target.z + 6f);
            desiredVelocity = (up - transform.position).normalized * patrolSpeed * 1.6f;
            if ((up - transform.position).magnitude < 6f || bossAttackTimer > 4f)
            {
                bossAttackStep = 1;
                bossAttackTimer = 0f;
            }
        }
        else
        {
            KeepDiving();
            Vector3 lead = target + player.Velocity * 0.35f;
            desiredVelocity = (lead - transform.position).normalized * diveSpeed * 1.6f;
            poseTarget = -30f;
            bool hit = attackTimer <= 0f && Vector3.Distance(transform.position, target) < attackReach * Mathf.Max(1f, visualScale * 0.5f);
            if (hit) BumpPlayer();
            bool passed = transform.position.y < target.y - 2f || transform.position.y < lake.SurfaceY + 0.8f;
            if (hit || passed || bossAttackTimer > 3f)
            {
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

    // ======================= GOLPES Y DAÑO =======================

    // Golpe de jefe: más empujón y no se queda aturdido (sigue con su patrón)
    void BumpPlayer()
    {
        if (attackTimer > 0f || player == null || player.isDead) return;
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();
        player.TakeHit(damage, dir * knockbackForce + Vector3.up * knockbackUp * 1.3f);
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
