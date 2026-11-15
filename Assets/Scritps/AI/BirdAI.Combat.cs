using UnityEngine;
using FishGame.AI;

// Cómo pelea cada especie contra el jugador (además de cómo pesca, en BirdAI.Abilities.cs):
//  - Gaviota        HOSTIGADORA: rota entre pasadas de bombardeo, "regalos" lanzados desde lejos y picadas en
//                   zigzag; si te dan en primera persona ensucian el visor.
//  - Martín pesc.   RAYO: se detiene, te apunta y cruza en línea recta a velocidad altísima.
//  - Charrán        ENJAMBRE: embestidas rápidas; cuando uno se lanza, los de alrededor lo siguen uno tras otro.
//  - Garza          ESTOCADA: se acerca, levanta el pico y lanza una estocada larga. Blindada: los disparos no la empujan.
//  - Cormorán       TORPEDO: embestidas rectas muy rápidas, también bajo el agua.
//  - Serreta        A DISTANCIA: se queda lejos, te rodea y escupe chorros de agua en ráfagas.
//  - Pelícano       APLASTADOR: embestida pesada que empuja mucho. Blindado: los disparos no lo empujan.
//  - Águila pesc.   PICADA: sube muy alto sobre ti y cae en picada, rapidísima.
// Las aves livianas (martín pescador, charrán) salen volando con cada disparo; las pesadas ni se mueven.
// Además todas esquivan, zigzaguean, viran de lado y huyen heridas (BirdAI.Evasion.cs).
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
    bool comboUsed;             // ya encadenó su segundo ataque
    int volley;                 // serreta: alterna ráfaga y abanico
    float cannonTimer = 3f;     // pelícano: bolas de agua desde lejos
    float alertCooldown;        // no avisa a la bandada a cada rato
    bool windup;                // preparando un ataque: aletea nervioso
    int gullTactic = -1, lastGullTactic = -1;   // gaviota: táctica en curso
    Vector3 passDir;            // gaviota: dirección de la pasada de bombardeo
    float strafeFlipTimer = 2f; // serreta: cambia el sentido en que te rodea
    float diveHeight = 16f;     // águila: altura de la próxima picada

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
        if (alertCooldown > 0f) alertCooldown -= dt;
        if (cannonTimer > 0f) cannonTimer -= dt;
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

        // Pelícano: de lejos te lanza bolas de agua en arco (y de cerca te aplasta)
        if (combatRole == CombatRole.Aplastador && cannonTimer <= 0f && Vector3.Distance(transform.position, ppos) > 10f)
        {
            cannonTimer = Random.Range(2.5f, 3.5f);
            Vector3 from = transform.position + transform.forward * bodyRadius;
            Vector3 aim = player.transform.position + player.Velocity * 1f;
            EnemyProjectile.Fire(EnemyProjectile.Kind.BolaDeAgua, from,
                EnemyProjectile.ArcVelocity(from, aim, 1f, EnemyProjectile.GravityOf(EnemyProjectile.Kind.BolaDeAgua)), damage * 0.7f, player);
            Sfx.Play(Sonido.Escupitajo, transform.position, 1f, 0.6f, 10f);
        }

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
        NewBreakSide();
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
            // Se coloca zigzagueando (no se queda quieta para que le apunten)
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed + Weave(to.normalized, 5f, 3.5f);
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
            // El rayo cruza recto; el charrán y el cormorán llegan zigzagueando
            if (combatRole != CombatRole.Rayo)
                desiredVelocity += AttackWeave(attackDir, Vector3.Distance(transform.position, ppos), combatRole == CombatRole.Enjambre ? 7f : 5f);
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
            // Vira hacia un costado y vuelve a colocarse. El martín pescador a veces rebota y vuelve a cruzar enseguida
            desiredVelocity = BreakAway(attackDir, chaseSpeed);
            if (combatRole == CombatRole.Rayo && !comboUsed && combatTimer > 0.25f && Random.value < 0.5f)
            {
                comboUsed = true;
                combatPhase = 1;
                combatTimer = 0.25f;   // aviso más corto
                return BTStatus.Running;
            }
            if (combatTimer > 0.45f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                comboUsed = false;
                combatCooldown = combatRole == CombatRole.Rayo ? Random.Range(0.8f, 1.4f) : Random.Range(1f, 1.8f);
            }
        }
        return BTStatus.Running;
    }

    // Avisa a las aves cercanas (que no lleven peces) para que se sumen al ataque
    public void AlertFlock()
    {
        if (alertCooldown > 0f || IsBoss) return;
        alertCooldown = 4f;
        int k = 0;
        foreach (BirdAI b in All)
        {
            if (b == this || !b.IsAlive || b.IsBoss || b.arriving || b.carried.Count > 0 || b.IsAggro) continue;
            if (Vector3.Distance(b.transform.position, transform.position) > 30f) continue;
            b.SetAggro(5f);
            b.alertCooldown = 4f;   // no reenvía el aviso: así no se suma todo el lago
            if (++k >= 3) break;
        }
        if (k > 0) Call(1f);
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
        // El alcance ya viene del archivo Ave (agrandado junto con el modelo): aquí no se vuelve a multiplicar
        float reach = attackReach * Mathf.Max(1f, visualScale * 0.75f);
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
            // A veces encadena una segunda estocada casi sin aviso
            if (!comboUsed && combatTimer > 0.3f && Random.value < 0.4f)
            {
                comboUsed = true;
                combatPhase = 1;
                combatTimer = 0.35f;
                return BTStatus.Running;
            }
            if (combatTimer > 0.6f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                comboUsed = false;
                combatCooldown = Random.Range(0.8f, 1.4f);
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
        // Cambia el sentido en que te rodea cada pocos segundos (como un caza que no se deja fijar)
        strafeFlipTimer -= Time.fixedDeltaTime;
        if (strafeFlipTimer <= 0f)
        {
            strafeFlipTimer = Random.Range(1.8f, 3.6f);
            orbitDirection = -orbitDirection;
        }
        orbitAngle += Time.fixedDeltaTime * 0.6f * orbitDirection;
        float range = d < 7f ? 16f : 14f;   // demasiado cerca: se aleja
        Vector3 goal = new Vector3(ppos.x + Mathf.Cos(orbitAngle) * range, Mathf.Max(ppos.y + 3f, lake.SurfaceY + 2.5f),
                                   ppos.z + Mathf.Sin(orbitAngle) * range);
        desiredVelocity = (goal - transform.position).normalized * chaseSpeed * 0.9f + Weave(to.normalized, 5f, 3f);
        lookOverride = to;

        if (shotTimer <= 0f)
        {
            if (shotsLeft <= 0)
            {
                shotsLeft = 3;
                volley++;
            }
            if (volley % 2 == 0)
            {
                // Abanico: tres chorros a la vez
                for (int i = -1; i <= 1; i++) FireAtPlayer(EnemyProjectile.Kind.Escupitajo, 26f, damage * 0.6f, i * 9f);
                shotsLeft = 0;
            }
            else
            {
                FireAtPlayer(EnemyProjectile.Kind.Escupitajo, 28f, damage * 0.8f);
                shotsLeft--;
            }
            Sfx.Play(Sonido.Escupitajo, transform.position, 0.85f, CallPitch, 6f);
            shotTimer = shotsLeft > 0 ? 0.18f : Random.Range(1f, 1.4f);
        }
        return BTStatus.Running;
    }

    // ---- Gaviota: hostigadora. Rota entre tres tácticas (nunca repite la anterior) ----
    //  0 PASADA: cruza por encima en línea recta soltando hasta 3 "regalos" y vira hacia un costado
    //  1 DESDE LEJOS: te rodea a media distancia zigzagueando y te lanza "regalos" en arco
    //  2 PICADA EN ZIGZAG: avisa, se lanza zigzagueando a picotearte y se aleja de lado
    BTStatus BomberAttack(Vector3 ppos)
    {
        float dt = Time.fixedDeltaTime;
        combatTimer += dt;
        Vector3 to = ppos - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        Vector3 flatDir = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
        float d = to.magnitude;

        if (combatPhase == 0)
        {
            // Se reacomoda alrededor del jugador, a media distancia y zigzagueando, y elige la próxima táctica
            tree.MarkActive("Hostigar: reacomodarse");
            orbitAngle += dt * 0.9f * orbitDirection;
            Vector3 goal = new Vector3(ppos.x + Mathf.Cos(orbitAngle) * 13f, Mathf.Max(ppos.y, lake.SurfaceY) + 5f,
                                       ppos.z + Mathf.Sin(orbitAngle) * 13f);
            desiredVelocity = (goal - transform.position).normalized * chaseSpeed + Weave(flatDir, 5f, 3.5f);
            lookOverride = to;
            if (combatCooldown <= 0f && combatTimer > 0.35f) StartGullTactic(to, flatDir);
        }
        else if (combatPhase == 3)
        {
            // Viraje lateral después de cada táctica
            tree.MarkActive("Virar de lado");
            desiredVelocity = BreakAway(attackDir, chaseSpeed * 1.1f);
            if (combatTimer > 0.55f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                combatCooldown = Random.Range(0.2f, 0.6f);
            }
        }
        else if (gullTactic == 0) GullPass(ppos);
        else if (gullTactic == 1) GullLob(ppos, to, d, flatDir);
        else GullZigzag(ppos, to, d);

        // Si te le pegas, picotazo
        if (combatPhase != 2 && attackTimer <= 0f && d < attackReach) HitPlayer(0.8f, 0.8f, 0.5f);
        return BTStatus.Running;
    }

    void StartGullTactic(Vector3 to, Vector3 flatDir)
    {
        int t = Random.Range(0, 3);
        if (t == lastGullTactic) t = (t + 1 + Random.Range(0, 2)) % 3;
        gullTactic = t;
        lastGullTactic = t;
        combatPhase = 1;
        combatTimer = 0f;
        shotsLeft = 0;
        shotTimer = 0f;
        passDir = flatDir;
        attackDir = to.normalized;
        NewBreakSide();
        if (t == 1 && Random.value < 0.5f) orbitDirection = -orbitDirection;
    }

    void GullEnd()
    {
        combatPhase = 3;
        combatTimer = 0f;
    }

    // Pasada en línea recta por encima: apunta más allá del jugador y suelta cuando la caída coincide con él
    void GullPass(Vector3 ppos)
    {
        tree.MarkActive("Pasada de bombardeo");
        Vector3 goal = ppos + passDir * 22f + Vector3.up * 6.5f;
        desiredVelocity = (goal - transform.position).normalized * chaseSpeed * 1.35f;
        attackDir = passDir;

        float height = transform.position.y - ppos.y;
        if (height > 2.5f && shotsLeft < 3 && shotTimer <= 0f)
        {
            float fall = Mathf.Sqrt(2f * height / EnemyProjectile.GravityOf(EnemyProjectile.Kind.Caca));
            Vector3 v = rb.linearVelocity;
            Vector3 inherit = new Vector3(v.x, 0f, v.z) * 0.6f;
            Vector3 land = transform.position + inherit * fall;
            Vector3 target = ppos + player.Velocity * fall;
            if (new Vector2(land.x - target.x, land.z - target.z).magnitude < 3.5f)
            {
                EnemyProjectile.Fire(EnemyProjectile.Kind.Caca, transform.position + Vector3.down * 0.5f, inherit, damage * 0.7f, player);
                Sfx.Play(Sonido.Caca, transform.position, 0.7f, 1f, 5f);
                shotsLeft++;
                shotTimer = 0.16f;
            }
        }
        if (Vector3.Dot(transform.position - ppos, passDir) > 14f || combatTimer > 4f)
        {
            if (shotsLeft > 0 && Random.value < 0.4f) Call(0.9f);
            GullEnd();
        }
    }

    // Te rodea a media distancia, zigzagueando, y lanza "regalos" en arco hacia donde vas a estar
    void GullLob(Vector3 ppos, Vector3 to, float d, Vector3 flatDir)
    {
        tree.MarkActive("Lanzar regalos desde lejos");
        float dt = Time.fixedDeltaTime;
        orbitAngle += dt * 0.85f * orbitDirection;
        float r = d < 8f ? 16f : 14f;
        Vector3 goal = new Vector3(ppos.x + Mathf.Cos(orbitAngle) * r, Mathf.Max(ppos.y, lake.SurfaceY) + 4.5f,
                                   ppos.z + Mathf.Sin(orbitAngle) * r);
        desiredVelocity = (goal - transform.position).normalized * chaseSpeed + Weave(flatDir, 6f, 4f);
        lookOverride = to;
        attackDir = to.normalized;

        if (shotTimer <= 0f && d > 6f)
        {
            Vector3 from = transform.position + transform.forward * (bodyRadius * 0.6f);
            float flight = Mathf.Clamp(d / 16f, 0.6f, 1.1f);
            Vector3 aim = player.transform.position + player.Velocity * flight;
            Vector3 vel = EnemyProjectile.ArcVelocity(from, aim, flight, EnemyProjectile.GravityOf(EnemyProjectile.Kind.Caca));
            EnemyProjectile.Fire(EnemyProjectile.Kind.Caca, from, vel, damage * 0.6f, player);
            Sfx.Play(Sonido.Caca, transform.position, 0.7f, 1.15f, 6f);
            shotsLeft++;
            shotTimer = 0.45f;
        }
        if (shotsLeft >= 4 || combatTimer > 3.4f) GullEnd();
    }

    // Aviso corto y picada zigzagueando; el zigzag se cierra al llegar para que el picotazo conecte
    void GullZigzag(Vector3 ppos, Vector3 to, float d)
    {
        if (combatPhase == 1)
        {
            tree.MarkActive("Preparar picada en zigzag");
            windup = true;
            desiredVelocity = Vector3.ClampMagnitude(-to.normalized * 2f, 2f);
            lookOverride = to;
            poseTarget = 14f;
            if (combatTimer >= 0.28f)
            {
                combatPhase = 2;
                combatTimer = 0f;
                attackDir = (ppos + player.Velocity * 0.2f - transform.position).normalized;
                Sfx.Play(Sonido.Embestida, transform.position, 0.9f, CallPitch, 6f);
            }
            return;
        }
        tree.MarkActive("Picada en zigzag");
        burst = true;
        Vector3 lead = ppos + player.Velocity * 0.2f;
        Vector3 dir = (lead - transform.position).normalized;
        desiredVelocity = dir * chaseSpeed * 2.1f + AttackWeave(dir, d, 9f);
        poseTarget = -12f;
        if (attackTimer <= 0f && d < attackReach + 0.4f) HitPlayer(1f, 1f, 0.5f);
        if (Vector3.Dot(transform.position - ppos, attackDir) > 3f || combatTimer > 1.4f) GullEnd();
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
            Vector3 goal = new Vector3(ppos.x + Mathf.Cos(orbitAngle) * 8f, Mathf.Max(ppos.y, lake.SurfaceY) + diveHeight,
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
            {
                HitPlayer(1.4f, 1f, 1f);
                // Te atrapa con las garras y te lanza por los aires
                if (!player.isDead) player.ApplyKnockback(Vector3.up * 17f + attackDir * 6f, 0.8f);
            }
            bool passed = transform.position.y < ppos.y - 1.5f || transform.position.y < lake.SurfaceY + 0.8f;
            if (passed || combatTimer > 2.2f)
            {
                combatPhase = 3;
                combatTimer = 0f;
            }
        }
        else
        {
            // Sale de la picada virando hacia un costado (y vuelve a subir a una altura distinta)
            desiredVelocity = (BreakAway(transform.forward, 1f) + Vector3.up * 0.8f).normalized * patrolSpeed * 1.3f;
            if (combatTimer > 1f)
            {
                combatPhase = 0;
                combatTimer = 0f;
                combatCooldown = Random.Range(1.2f, 2f);
                diveHeight = Random.Range(10f, 17f);
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
