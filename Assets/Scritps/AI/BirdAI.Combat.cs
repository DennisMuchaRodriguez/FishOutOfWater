using UnityEngine;
using FishGame.AI;

// Cómo pelea cada especie contra el jugador (además de cómo pesca, en BirdAI.Abilities.cs):
//  - Gaviota        BOMBARDERA: vuela encima de ti y suelta "regalos"; si te dan en primera persona ensucian el visor.
//  - Martín pesc.   RAYO: se detiene, te apunta y cruza en línea recta a velocidad altísima.
//  - Charrán        ENJAMBRE: embestidas rápidas; cuando uno se lanza, los de alrededor lo siguen uno tras otro.
//  - Garza          ESTOCADA: se acerca, levanta el pico y lanza una estocada larga. Blindada: los disparos no la empujan.
//  - Cormorán       TORPEDO: embestidas rectas muy rápidas, también bajo el agua.
//  - Serreta        A DISTANCIA: se queda lejos, te rodea y escupe chorros de agua en ráfagas.
//  - Pelícano       APLASTADOR: embestida pesada que empuja mucho. Blindado: los disparos no lo empujan.
//  - Águila pesc.   PICADA: sube muy alto sobre ti y cae en picada, rapidísima.
// Las aves livianas (martín pescador, charrán) salen volando con cada disparo; las pesadas ni se mueven.
public partial class BirdAI
{
    enum CombatRole { Embestida, Bombardero, Rayo, Enjambre, Estocada, Torpedo, Escupidor, Aplastador, Picado }

    CombatRole combatRole = CombatRole.Embestida;
    int combatPhase;            // 0 colocarse, 1 preparar (aviso), 2 atacar, 3 recuperarse
    float combatTimer;
    float combatCooldown;
    float lastFightTime = -10f;
    float shotTimer;
    int shotsLeft;
    float chainDelay = -1f;     // charrán: espera para seguir la embestida de otro
    float callTimer;
    Vector3 attackDir;
    bool burst;                 // embestida: acelera de golpe
    bool windup;                // preparando un ataque: aletea nervioso

    // Cuánto lo empujan los disparos (0 = nada, blindado)
    public float ProjectilePushScale { get; private set; } = 1f;
    public bool IsArmored { get { return ProjectilePushScale <= 0.05f; } }

    void ConfigureCombat()
    {
        switch (Ability)
        {
            case BirdAbility.SurfaceSnatch: combatRole = CombatRole.Bombardero; ProjectilePushScale = 1.2f; break;
            case BirdAbility.HoverDive: combatRole = CombatRole.Rayo; ProjectilePushScale = 1.8f; break;
            case BirdAbility.ChainDive: combatRole = CombatRole.Enjambre; ProjectilePushScale = 1.8f; break;
            case BirdAbility.ShoreSpear: combatRole = CombatRole.Estocada; ProjectilePushScale = 0f; break;
            case BirdAbility.UnderwaterHunter: combatRole = CombatRole.Torpedo; ProjectilePushScale = 0.5f; break;
            case BirdAbility.GroupDive: combatRole = CombatRole.Escupidor; ProjectilePushScale = 1f; break;
            case BirdAbility.Scoop: combatRole = CombatRole.Aplastador; ProjectilePushScale = 0f; break;
            case BirdAbility.HighDive: combatRole = CombatRole.Picado; ProjectilePushScale = 0.3f; break;
            default: combatRole = CombatRole.Embestida; ProjectilePushScale = 1f; break;
        }
        if (combatRole == CombatRole.Aplastador) knockbackForce *= 1.6f;
        if (IsBoss) ProjectilePushScale = 0f;
        callTimer = Random.Range(3f, 9f);
    }

    // Graznido de vez en cuando (más grave cuanto más grande)
    string CallSound { get { return Ability == BirdAbility.SurfaceSnatch && !IsBoss ? Sonido.Gaviota : Sonido.Graznido; } }
    float CallPitch { get { return Mathf.Clamp(1.45f - bodyRadius * 0.35f, 0.45f, 1.5f); } }   // bodyRadius ya está en metros

    public void Call(float volume = 1f)
    {
        Sfx.Play(CallSound, transform.position, volume, CallPitch, IsBoss ? 18f : 9f, 0.08f, 0.15f);
    }

    void TickCombat(float dt)
    {
        if (combatCooldown > 0f) combatCooldown -= dt;
        if (shotTimer > 0f) shotTimer -= dt;
        if (chainDelay > 0f)
        {
            chainDelay -= dt;
            if (chainDelay <= 0f && combatPhase == 0 && !dead) StartWindup();
        }
        callTimer -= dt;
        if (callTimer <= 0f && !dead && !arriving)
        {
            callTimer = Random.Range(7f, 16f);
            if (IsAggro || TargetFish != null) Call(0.8f);
        }
    }

    // ======================= PELEA =======================

    BTStatus FightPlayer()
    {
        // Si dejó de pelear un rato, empieza desde el principio
        if (Time.time - lastFightTime > 0.6f)
        {
            combatPhase = 0;
            combatTimer = 0f;
        }
        lastFightTime = Time.time;

        // Jugador hondo bajo el agua: los que no bucean esperan encima y los buceadores lo siguen (ChasePlayer).
        // Si nada en la superficie, cada especie sigue atacando a su manera
        Vector3 ppos = player.transform.position + Vector3.up * 0.4f;
        if (player.isInWater && lake.SurfaceY - (player.transform.position.y + 0.5f) > 1f) return ChasePlayer();

        switch (combatRole)
        {
            case CombatRole.Bombardero: return BomberAttack(ppos);
            case CombatRole.Rayo:
            case CombatRole.Enjambre:
            case CombatRole.Torpedo: return DashAttack(ppos);
            case CombatRole.Escupidor: return SpitAttack(ppos);
            case CombatRole.Estocada: return LungeAttack(ppos);
            case CombatRole.Picado: return DiveBombAttack(ppos);
        }
        return ChasePlayer();
    }

    float DashDistance
    {
        get { return combatRole == CombatRole.Rayo ? 12f : (combatRole == CombatRole.Enjambre ? 9f : 10f); }
    }

    void StartWindup()
    {
        combatPhase = 1;
        combatTimer = 0f;
    }

    // ---- Rayo / Enjambre / Torpedo: colocarse, avisar y cruzar en línea recta ----
    BTStatus DashAttack(Vector3 ppos)
    {
        tree.MarkActive("Embestida en línea recta");
        float dt = Time.fixedDeltaTime;
        combatTimer += dt;
        Vector3 to = ppos - transform.position;

        if (combatPhase == 0)
        {
            Vector3 away = -to;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            Vector3 goal = ppos + away * DashDistance + Vector3.up * 1.5f;
            goal.y = Mathf.Max(goal.y, lake.SurfaceY + 1.2f);
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed;
            lookOverride = to;
            float d = to.magnitude;
            if (combatCooldown <= 0f && attackTimer <= 0f && d < DashDistance + 4f && d > 3f && chainDelay <= 0f) StartWindup();
        }
        else if (combatPhase == 1)
        {
            // Aviso: se frena en el aire, mirando al jugador
            windup = true;
            desiredVelocity = Vector3.ClampMagnitude(-to.normalized * 2f, 2f);
            lookOverride = to;
            poseTarget = 12f;
            float wind = combatRole == CombatRole.Rayo ? 0.45f : (combatRole == CombatRole.Enjambre ? 0.25f : 0.35f);
            if (combatTimer >= wind)
            {
                Vector3 lead = ppos + player.Velocity * 0.22f;
                attackDir = (lead - transform.position).normalized;
                combatPhase = 2;
                combatTimer = 0f;
                Sfx.Play(Sonido.Embestida, transform.position, 0.9f, CallPitch * 1.1f, 6f);
                if (combatRole == CombatRole.Enjambre) RallySwarm();
            }
        }
        else if (combatPhase == 2)
        {
            // Cruza a toda velocidad
            burst = true;
            float mul = combatRole == CombatRole.Rayo ? 3f : (combatRole == CombatRole.Enjambre ? 2.4f : 2.6f);
            desiredVelocity = attackDir * chaseSpeed * mul;
            poseTarget = -10f;
            if (attackTimer <= 0f && Vector3.Distance(transform.position, ppos) < attackReach + 0.4f)
                HitPlayer(1f, 1f, 0.8f);
            bool passed = Vector3.Dot(transform.position - ppos, attackDir) > 4f;
            if (passed || combatTimer > 0.9f)
            {
                combatPhase = 3;
                combatTimer = 0f;
            }
        }
        else
        {
            // Remonta y vuelve a colocarse
            desiredVelocity = (attackDir * 0.5f + Vector3.up * 0.6f).normalized * patrolSpeed;
            if (combatTimer > 0.6f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                combatCooldown = combatRole == CombatRole.Rayo ? Random.Range(1.2f, 2f) : Random.Range(1.6f, 2.6f);
            }
        }
        return BTStatus.Running;
    }

    // Charrán: los compañeros cercanos siguen la embestida, uno detrás de otro
    void RallySwarm()
    {
        int k = 0;
        foreach (BirdAI b in All)
        {
            if (b == this || !b.IsAlive || b.combatRole != CombatRole.Enjambre || b.carried.Count > 0) continue;
            if (b.combatPhase != 0 || b.chainDelay > 0f) continue;
            if (Vector3.Distance(b.transform.position, transform.position) > 28f) continue;
            k++;
            b.SetAggro(6f);
            b.lastFightTime = Time.time;
            b.combatCooldown = 0f;
            b.chainDelay = 0.3f * k + Random.Range(0f, 0.15f);
            if (k >= 4) break;
        }
    }

    // ---- Garza: estocada larga ----
    BTStatus LungeAttack(Vector3 ppos)
    {
        tree.MarkActive("Estocada");
        float dt = Time.fixedDeltaTime;
        combatTimer += dt;
        Vector3 to = ppos - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        float reach = attackReach * Mathf.Max(1f, visualScale);
        lookOverride = flat;

        if (combatPhase == 0)
        {
            Vector3 goal = ppos - (flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward) * (reach + 1f);
            goal.y = Mathf.Max(ppos.y + 0.6f, lake.SurfaceY + hoverHeight);
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed;
            if (combatCooldown <= 0f && attackTimer <= 0f && to.magnitude < reach + 3f) StartWindup();
        }
        else if (combatPhase == 1)
        {
            windup = true;
            desiredVelocity = Vector3.zero;
            poseTarget = 32f;   // levanta el pico
            if (combatTimer >= 0.55f)
            {
                attackDir = (ppos + player.Velocity * 0.15f - transform.position).normalized;
                combatPhase = 2;
                combatTimer = 0f;
                Sfx.Play(Sonido.Estocada, transform.position, 1f, CallPitch, 6f);
            }
        }
        else if (combatPhase == 2)
        {
            burst = true;
            poseTarget = -28f;
            desiredVelocity = attackDir * chaseSpeed * 3.2f;
            if (attackTimer <= 0f && Vector3.Distance(transform.position, ppos) < reach * 1.15f)
                HitPlayer(1.3f, 1.2f, 1.2f);
            if (combatTimer > 0.3f)
            {
                combatPhase = 3;
                combatTimer = 0f;
            }
        }
        else
        {
            desiredVelocity = -attackDir * patrolSpeed * 0.6f + Vector3.up * 2f;
            if (combatTimer > 0.8f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                combatCooldown = Random.Range(1.2f, 2f);
            }
        }
        return BTStatus.Running;
    }

    // ---- Serreta: a distancia, ráfagas de escupitajos ----
    BTStatus SpitAttack(Vector3 ppos)
    {
        tree.MarkActive("Escupir desde lejos");
        Vector3 to = ppos - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        float d = flat.magnitude;
        orbitAngle += Time.fixedDeltaTime * 0.5f * orbitDirection;
        float range = d < 7f ? 16f : 14f;   // demasiado cerca: se aleja
        Vector3 goal = new Vector3(ppos.x + Mathf.Cos(orbitAngle) * range, Mathf.Max(ppos.y + 3f, lake.SurfaceY + 2.5f),
                                   ppos.z + Mathf.Sin(orbitAngle) * range);
        desiredVelocity = (goal - transform.position).normalized * chaseSpeed * 0.9f;
        lookOverride = to;

        if (shotTimer <= 0f)
        {
            if (shotsLeft <= 0) shotsLeft = 2;
            FireAtPlayer(EnemyProjectile.Kind.Escupitajo, 26f, damage * 0.8f);
            Sfx.Play(Sonido.Escupitajo, transform.position, 0.85f, CallPitch, 6f);
            shotsLeft--;
            shotTimer = shotsLeft > 0 ? 0.2f : Random.Range(1.4f, 2f);
        }
        return BTStatus.Running;
    }

    // ---- Gaviota: bombardeo desde arriba ----
    BTStatus BomberAttack(Vector3 ppos)
    {
        tree.MarkActive("Bombardear desde arriba");
        const float height = 7f;
        float fall = Mathf.Sqrt(2f * height / EnemyProjectile.GravityOf(EnemyProjectile.Kind.Caca));
        Vector3 myVel = rb.linearVelocity;
        // Se coloca para que la caída (que hereda parte de su velocidad) caiga donde va a estar el jugador
        orbitAngle += Time.fixedDeltaTime * 1.6f * orbitDirection;
        Vector3 wobble = new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * 2.5f;
        Vector3 goal = ppos + player.Velocity * fall - new Vector3(myVel.x, 0f, myVel.z) * 0.6f * fall + Vector3.up * height + wobble;
        desiredVelocity = (goal - transform.position).normalized * chaseSpeed * 1.1f;

        Vector3 flat = ppos - transform.position;
        float above = -flat.y;
        flat.y = 0f;
        if (shotTimer <= 0f && flat.magnitude < 4f && above > 3f)
        {
            Vector3 v = new Vector3(myVel.x, 0f, myVel.z) * 0.6f;
            EnemyProjectile.Fire(EnemyProjectile.Kind.Caca, transform.position + Vector3.down * 0.5f, v, damage * 0.7f, player);
            Sfx.Play(Sonido.Caca, transform.position, 0.7f, 1f, 5f);
            if (Random.value < 0.35f) Call(0.9f);
            shotTimer = Random.Range(0.7f, 1.2f);
        }
        // Si te le acercas mucho, picotazo
        if (attackTimer <= 0f && Vector3.Distance(transform.position, ppos) < attackReach) HitPlayer(0.8f, 0.8f, 1f);
        return BTStatus.Running;
    }

    // ---- Águila pescadora: picada desde muy alto ----
    BTStatus DiveBombAttack(Vector3 ppos)
    {
        tree.MarkActive("Picada sobre el jugador");
        float dt = Time.fixedDeltaTime;
        combatTimer += dt;
        if (combatPhase == 0)
        {
            orbitAngle += dt * 0.7f * orbitDirection;
            Vector3 goal = new Vector3(ppos.x + Mathf.Cos(orbitAngle) * 8f, Mathf.Max(ppos.y, lake.SurfaceY) + 16f,
                                       ppos.z + Mathf.Sin(orbitAngle) * 8f);
            desiredVelocity = (goal - transform.position).normalized * patrolSpeed * 1.4f;
            if (combatCooldown <= 0f && attackTimer <= 0f && ((goal - transform.position).magnitude < 4f || combatTimer > 5f)) StartWindup();
        }
        else if (combatPhase == 1)
        {
            windup = true;
            desiredVelocity = Vector3.up * 1.5f;
            lookOverride = ppos - transform.position;
            poseTarget = -30f;
            if (combatTimer >= 0.4f)
            {
                combatPhase = 2;
                combatTimer = 0f;
                Sfx.Play(Sonido.Picada, transform.position, 1f, CallPitch, 12f);
            }
        }
        else if (combatPhase == 2)
        {
            burst = true;
            KeepDiving();
            Vector3 lead = ppos + player.Velocity * 0.3f;
            desiredVelocity = (lead - transform.position).normalized * diveSpeed * 1.5f;
            poseTarget = -30f;
            if (attackTimer <= 0f && Vector3.Distance(transform.position, ppos) < attackReach + 0.6f)
                HitPlayer(1.4f, 1.5f, 1f);
            bool passed = transform.position.y < ppos.y - 1.5f || transform.position.y < lake.SurfaceY + 0.8f;
            if (passed || combatTimer > 2.2f)
            {
                combatPhase = 3;
                combatTimer = 0f;
            }
        }
        else
        {
            desiredVelocity = (transform.forward * 0.4f + Vector3.up).normalized * patrolSpeed * 1.3f;
            if (combatTimer > 1f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                combatCooldown = Random.Range(2f, 3f);
            }
        }
        return BTStatus.Running;
    }

    // Dispara un proyectil recto hacia donde va a estar el jugador
    void FireAtPlayer(EnemyProjectile.Kind kind, float speed, float dmg, float spreadDeg = 0f)
    {
        Vector3 from = transform.position + transform.forward * (bodyRadius * 0.9f);
        Vector3 target = player.transform.position + Vector3.up * 0.5f;
        float t = Vector3.Distance(from, target) / Mathf.Max(1f, speed);
        Vector3 aim = target + player.Velocity * t * 0.8f + Vector3.up * (0.5f * EnemyProjectile.GravityOf(kind) * t * t);
        Vector3 dir = (aim - from).normalized;
        if (spreadDeg != 0f) dir = Quaternion.AngleAxis(spreadDeg, Vector3.up) * dir;
        EnemyProjectile.Fire(kind, from, dir * speed, dmg, player);
    }
}
