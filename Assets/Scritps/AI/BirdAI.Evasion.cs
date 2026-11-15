using UnityEngine;
using FishGame.AI;

// Las aves pelean como naves pequeñas: difíciles de acertar y nunca predecibles.
//  - ESQUIVA: cuando disparas y la bala va hacia ella, reacciona con un tonel (giro completo) hacia un lado y sale
//    de la línea de fuego. Tiene recarga: con fuego continuo terminas acertando.
//  - ZIGZAG: si la sigues con la mira un rato, empieza a moverse en zigzag; las embestidas también llegan
//    zigzagueando (el zigzag se cierra al final para que el golpe conecte).
//  - VIRAJE: después de atacar se aleja hacia un costado (no siempre hacia arriba).
//  - HUIDA: con poca vida, muchas especies escapan unos segundos soltando plumas y vuelven furiosas.
// Cada especie tiene su agilidad (Agility): las livianas esquivan mucho; las blindadas y pesadas, poco.
public partial class BirdAI
{
    // Esquiva
    float dodgeTimer;            // > 0: está esquivando
    float dodgeCooldown;
    float dodgeDelay = -1f;      // reacción pendiente (>= 0)
    Vector3 dodgeDir, pendingDodgeDir;
    float dodgeSpeed;
    float dodgeDuration = 0.32f;
    float dodgeRoll;             // tonel visual (grados)
    float dodgeRollSign = 1f;
    // Zigzag
    float jinkTimer;
    float weaveClock;
    float weaveSeed;
    float aimTrack, aimCheckTimer;
    // Viraje y huida
    float breakSide = 1f;
    float fleeTimer;
    bool fledOnce;
    float fleeFeatherTimer;
    bool strikeCommitted;        // atacando de lleno este tick (no esquiva a mitad de una embestida)
    PlayerShooting shooter;

    static bool shotsHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShotHook()
    {
        PlayerShooting.ShotFired -= OnAnyShot;
        shotsHooked = false;
    }

    static void HookShots()
    {
        if (shotsHooked) return;
        PlayerShooting.ShotFired += OnAnyShot;
        shotsHooked = true;
    }

    static void OnAnyShot(Vector3 origin, Vector3 dir, float speed)
    {
        foreach (BirdAI b in All) b.ConsiderDodge(origin, dir, speed);
    }

    // ======================= PERFIL DE CADA ESPECIE =======================

    // 0.3 = torpe, 1 = normal, 1.4 = muy ágil
    float Agility
    {
        get
        {
            if (IsBoss) return BossType == BossKind.GiantHeron ? 0.8f : 0.6f;
            switch (Ability)
            {
                case BirdAbility.SurfaceSnatch: return 1.05f;   // gaviota
                case BirdAbility.HoverDive: return 1.4f;        // martín pescador
                case BirdAbility.ChainDive: return 1.3f;        // charrán
                case BirdAbility.ShoreSpear: return 0.4f;       // garza (blindada)
                case BirdAbility.UnderwaterHunter: return 0.8f; // cormorán
                case BirdAbility.GroupDive: return 1f;          // serreta
                case BirdAbility.Scoop: return 0.3f;            // pelícano (blindado)
                case BirdAbility.HighDive: return 0.9f;         // águila pescadora
                default: return 0.9f;
            }
        }
    }

    float DodgeChance
    {
        get
        {
            float c = 0.42f * Agility;
            if (IsBoss) c = (BossType == BossKind.GiantHeron ? 0.3f : 0.2f) + 0.05f * (bossPhase - 1);
            if (fleeTimer > 0f) c += 0.25f;
            return Mathf.Clamp(c, 0.05f, 0.75f);
        }
    }

    float FleeChance
    {
        get
        {
            switch (Ability)
            {
                case BirdAbility.SurfaceSnatch: return 0.7f;
                case BirdAbility.HoverDive: return 0.8f;
                case BirdAbility.ChainDive: return 0.6f;
                case BirdAbility.ShoreSpear: return 0.2f;
                case BirdAbility.UnderwaterHunter: return 0.5f;
                case BirdAbility.GroupDive: return 0.6f;
                case BirdAbility.Scoop: return 0.25f;
                case BirdAbility.HighDive: return 0.45f;
                default: return 0.5f;
            }
        }
    }

    bool CanEvade
    {
        get
        {
            if (dead || arriving || stunTimer > 0f || submerged || lurking || diving || strikeCommitted) return false;
            if (IsBoss && (bossAttacking || vulnerableTimer > 0f || phaseChangeTimer > 0f || harassDashing)) return false;
            return true;
        }
    }

    // ======================= TICK =======================

    // Antes del árbol: relojes, reacción pendiente y si el jugador la sigue con la mira
    void TickEvasion(float dt)
    {
        if (weaveSeed == 0f) weaveSeed = Random.Range(1f, 100f);
        weaveClock += dt;
        if (dodgeCooldown > 0f) dodgeCooldown -= dt;
        if (jinkTimer > 0f) jinkTimer -= dt;
        if (dodgeTimer > 0f)
        {
            dodgeTimer -= dt;
            dodgeRoll += dodgeRollSign * 360f * dt / Mathf.Max(0.05f, dodgeDuration);
            if (dodgeTimer <= 0f) dodgeRoll = 0f;
        }
        if (dodgeDelay >= 0f)
        {
            dodgeDelay -= dt;
            if (dodgeDelay < 0f && CanEvade) StartDodge(pendingDodgeDir);
        }

        // ¿Lo sigue la mira? Si la tiene encima un rato, empieza a zigzaguear
        aimCheckTimer -= dt;
        if (aimCheckTimer <= 0f && player != null && !player.isDead && !dead && !arriving)
        {
            aimCheckTimer = 0.15f;
            if (shooter == null) shooter = player.GetComponent<PlayerShooting>();
            if (shooter != null && IsAimedAt(shooter.AimRay)) aimTrack += 0.15f;
            else aimTrack = Mathf.Max(0f, aimTrack - 0.3f);
            float react = 0.55f / Mathf.Max(0.3f, Agility);
            if (aimTrack > react && CanEvade) jinkTimer = Mathf.Max(jinkTimer, 1.4f);
        }
    }

    bool IsAimedAt(Ray ray)
    {
        Vector3 to = transform.position - ray.origin;
        float dist = to.magnitude;
        if (dist < 2f || dist > 70f) return false;
        float ang = Vector3.Angle(ray.direction, to);
        float size = Mathf.Atan2(bodyRadius * 1.6f + 0.8f, dist) * Mathf.Rad2Deg;
        return ang < size + 1.5f;
    }

    // Después del árbol: la esquiva y el zigzag se suman a lo que pidió la acción
    void ApplyEvasion(float dt)
    {
        if (dead) return;
        strikeCommitted = burst || windup;

        if (dodgeTimer > 0f)
        {
            float k = dodgeTimer / Mathf.Max(0.05f, dodgeDuration);
            desiredVelocity = desiredVelocity * 0.25f + dodgeDir * dodgeSpeed * (0.6f + 0.4f * k);
            burst = true;
            // Sigue mirando hacia donde miraba: se desliza de lado como una nave
            if (lookOverride.sqrMagnitude < 0.01f) lookOverride = transform.forward;
            return;
        }

        bool weave = jinkTimer > 0f || fleeTimer > 0f;
        if (weave && !strikeCommitted && !submerged && !lurking && stunTimer <= 0f && !arriving)
        {
            Vector3 fwd = desiredVelocity.sqrMagnitude > 0.5f ? desiredVelocity.normalized : transform.forward;
            float amp = (fleeTimer > 0f ? 7f : 5.5f) * Mathf.Clamp(Agility, 0.3f, 1.4f);
            desiredVelocity += Weave(fwd, 6.5f, amp);
        }
    }

    // ======================= ESQUIVA =======================

    void ConsiderDodge(Vector3 origin, Vector3 dir, float speed)
    {
        if (!CanEvade || dodgeCooldown > 0f || dodgeDelay >= 0f || dodgeTimer > 0f || player == null) return;
        Vector3 to = transform.position - origin;
        float along = Vector3.Dot(to, dir);
        if (along < 2f || along > 70f) return;
        Vector3 off = to - dir * along;
        if (off.magnitude > bodyRadius * 1.5f + 1.2f) return;   // la bala pasa lejos: ni se inmuta

        if (Random.value > DodgeChance)
        {
            dodgeCooldown = 0.3f;   // con la metralleta no tira los dados con cada burbuja
            return;
        }
        float travel = along / Mathf.Max(10f, speed);
        dodgeDelay = Mathf.Clamp(travel * 0.35f, 0.04f, 0.16f) + Random.Range(0f, 0.05f);

        // Hacia el lado contrario de la bala (perpendicular a su trayectoria), con algo de altura al azar
        Vector3 side = off.sqrMagnitude > 0.01f ? off.normalized : Vector3.Cross(dir, Vector3.up).normalized * (Random.value < 0.5f ? -1f : 1f);
        side -= Vector3.Project(side, dir);
        if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(dir, Vector3.up);
        pendingDodgeDir = (side.normalized + Vector3.up * Random.Range(-0.25f, 0.45f)).normalized;
    }

    void StartDodge(Vector3 dir)
    {
        // Sin meterse al agua ni al suelo
        if (lake != null && transform.position.y < lake.SurfaceY + 2.5f && dir.y < 0.1f) dir.y = 0.25f;
        dodgeDir = dir.normalized;
        dodgeDuration = IsBoss ? 0.38f : 0.32f;
        dodgeTimer = dodgeDuration;
        dodgeSpeed = Mathf.Max(chaseSpeed * (IsBoss ? 1.5f : 1.6f), IsBoss ? 18f : 22f);
        dodgeCooldown = IsBoss ? Random.Range(2.4f, 3.4f) : Random.Range(1.1f, 2.1f) / Mathf.Max(0.6f, Agility);
        dodgeRollSign = Vector3.Dot(dodgeDir, transform.right) >= 0f ? -1f : 1f;
        dodgeRoll = 0f;
        jinkTimer = Mathf.Max(jinkTimer, 0.8f);
        if (rb != null) rb.linearVelocity += dodgeDir * dodgeSpeed * 0.35f;   // arranque instantáneo
        Sfx.Play(Sonido.Embestida, transform.position, 0.45f, CallPitch * 1.35f, 6f, 0.15f, 0.2f);
    }

    // Después de recibir un disparo, a veces rompe la línea de fuego para que el siguiente no le dé
    void AfterHitEvasion()
    {
        if (!CanEvade || dodgeTimer > 0f || dodgeDelay >= 0f || player == null) return;
        jinkTimer = Mathf.Max(jinkTimer, 1.2f);
        if (dodgeCooldown > 0f || Random.value > DodgeChance * 0.6f) return;
        Vector3 from = player.transform.position + Vector3.up * 0.8f;
        Vector3 line = (transform.position - from).normalized;
        Vector3 side = Vector3.Cross(line, Vector3.up).normalized * (Random.value < 0.5f ? -1f : 1f);
        pendingDodgeDir = (side + Vector3.up * Random.Range(0f, 0.4f)).normalized;
        dodgeDelay = Random.Range(0.05f, 0.12f);
    }

    // ======================= ZIGZAG Y VIRAJE =======================

    // Velocidad lateral que hace zigzaguear alrededor de 'dir' (amp en m/s)
    Vector3 Weave(Vector3 dir, float freq, float amp)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        if (side.sqrMagnitude < 0.01f) side = transform.right;
        side.Normalize();
        float t = weaveClock * freq + weaveSeed;
        // Dos ondas mezcladas: el zigzag nunca es igual (más difícil de anticipar)
        float s = Mathf.Cos(t) * 0.75f + Mathf.Cos(t * 1.7f + 1.3f) * 0.25f;
        float vert = Mathf.Sin(t * 0.8f + 0.6f) * 0.3f;
        return (side * s + Vector3.up * vert) * amp;
    }

    // Zigzag de una embestida: amplio de lejos y cerrado al llegar (para que el golpe conecte)
    Vector3 AttackWeave(Vector3 dir, float dist, float amp)
    {
        return Weave(dir, 9f, amp * Mathf.Clamp01((dist - 2f) / 9f));
    }

    // Se aleja hacia un costado después de atacar (el lado cambia cada vez)
    Vector3 BreakAway(Vector3 fromDir, float speed)
    {
        Vector3 flat = new Vector3(fromDir.x, 0f, fromDir.z);
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        flat.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, flat) * breakSide;
        return (flat * 0.55f + side * 0.85f + Vector3.up * 0.3f).normalized * speed;
    }

    void NewBreakSide()
    {
        breakSide = Random.value < 0.5f ? -1f : 1f;
    }

    // ======================= HUIDA =======================

    void MaybeFlee()
    {
        if (IsBoss || fledOnce || dead || carried.Count > 0 || Health01 > 0.3f) return;
        fledOnce = true;
        if (Random.value > FleeChance) return;
        fleeTimer = Random.Range(3.5f, 5f);
        diving = false;
        if (TargetFish != null) ReleaseTarget();
        CancelSpecialHunt();
        combatPhase = 0;
        combatTimer = 0f;
        jinkTimer = fleeTimer;
        Call(1f);
    }

    BTStatus Flee()
    {
        tree.MarkActive("Huir herida");
        float dt = Time.fixedDeltaTime;
        fleeTimer -= dt;

        // Al borde del lago, del lado contrario al jugador, y algo alta
        Vector3 away = player != null ? transform.position - player.transform.position : transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();
        Vector3 goal = lake.Center + away * lake.Radius * 0.95f;
        goal.y = lake.SurfaceY + patrolAltitude.y;
        desiredVelocity = (goal - transform.position).normalized * chaseSpeed * 1.25f;

        // Va soltando plumas: se nota que está herida
        fleeFeatherTimer -= dt;
        if (fleeFeatherTimer <= 0f)
        {
            fleeFeatherTimer = 0.22f;
            Vector3 v = rb.linearVelocity;
            HitFX.FeatherTrail(transform.position, v.sqrMagnitude > 1f ? v.normalized : transform.forward, bodyRadius * 0.6f, FeatherColor);
        }

        if (fleeTimer <= 0f)
        {
            // Vuelve furiosa: ataca enseguida
            fleeTimer = 0f;
            jinkTimer = 0f;
            aggroUntil = Time.time + aggroMemory;
            combatCooldown = 0f;
            attackTimer = 0f;
            combatPhase = 0;
            combatTimer = 0f;
            Call(1.1f);
            return BTStatus.Success;
        }
        return BTStatus.Running;
    }
}
