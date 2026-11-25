using UnityEngine;
using FishGame.AI;

// Jefes con movimiento fluido (parte de BirdAI). Antes se notaban "mecánicos": rodeaban al jugador a distancia fija,
// se frenaban en seco para cargar cada ataque y repetían siempre los ataques en el mismo orden. Ahora:
//  - PASADAS (entre ataques fuertes): vuelan como un caza. Apuntan a un lado del jugador, cierran la curva y lo
//    golpean al pasar, siguen de largo con la inercia, viran en una curva amplia y vuelven por otro lado. El giro
//    tiene un límite (grados por segundo), así que nunca cambian de dirección de golpe. En el viraje lanzan sus
//    ataques a distancia (dardos, escupitajos, plumas).
//  - BARRIDA sobre el cardumen (todos los jefes): avisan, toman carrera en curva y pasan rasantes por el grupo de peces
//    más apretado. Se llevan VARIOS de una pasada (2-4 según la fase; el pelícano hasta 6) y paralizan al resto. Con
//    los peces en las garras quedan agotados un momento: es cuando hay que dispararles para que los suelten.
//  - Eligen el ataque según la situación (no en orden fijo): si el jugador está lejos o escondido en lo hondo, o si
//    hay un cardumen apretado, van por los peces. No repiten el mismo ataque seguido.
//  - COMBOS: en la fase 2 a veces, y en la fase 3 siempre, encadenan dos (o tres) ataques fuertes sin la pausa
//    vulnerable entre ellos. La pausa llega al final del combo.
public partial class BirdAI
{
    const int SweepKind = 3;           // índice del ataque "barrida" (los otros: 0-2 según el jefe)

    // Dirección de vuelo con giro limitado (curvas suaves)
    Vector3 bossHeading;
    float lastSteerTime = -10f;

    // Pasadas
    enum PassStep { Approach, Through, Turn }
    PassStep passStep = PassStep.Turn;
    float passTimer;
    float passTurnTime = 1.2f;
    float passThroughTime = 0.45f;
    float passSide = 1f;
    bool passStruck;
    float passRangedAt;
    bool passRangedDone;

    // Combos y elección del ataque
    int bossChainLeft;
    bool bossChaining;
    int lastBossAttack = -1;
    readonly float[] attackWeights = new float[4];

    // Barrida
    Vector3 sweepDir;
    float sweepSplashAt;

    // ======================= GIRO SUAVE =======================

    // Gira la dirección de vuelo hacia 'toward' a lo sumo turnDeg grados por segundo y devuelve la velocidad
    Vector3 SteerBoss(Vector3 toward, float speed, float turnDeg)
    {
        if (Time.time - lastSteerTime > 0.15f || bossHeading.sqrMagnitude < 0.5f)
        {
            Vector3 v = rb.linearVelocity;
            bossHeading = v.sqrMagnitude > 1f ? v.normalized : transform.forward;
        }
        lastSteerTime = Time.time;
        if (toward.sqrMagnitude > 0.0001f)
            bossHeading = Vector3.RotateTowards(bossHeading, toward.normalized, turnDeg * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f).normalized;
        return bossHeading * speed;
    }

    // Más rápido y más ágil en cada fase
    float BossRage { get { return 1f + 0.13f * (bossPhase - 1); } }

    float PassSpeed
    {
        get
        {
            switch (BossType)
            {
                case BossKind.GiantHeron: return chaseSpeed * 1.75f;
                case BossKind.BottomlessPelican: return chaseSpeed * 1.35f;
                case BossKind.CormorantKing: return chaseSpeed * 1.8f;
                default: return chaseSpeed * 1.6f;
            }
        }
    }

    float PassTurn
    {
        get
        {
            switch (BossType)
            {
                case BossKind.GiantHeron: return 150f;
                case BossKind.BottomlessPelican: return 85f;
                case BossKind.CormorantKing: return 130f;
                default: return 110f;
            }
        }
    }

    float PassRadius
    {
        get
        {
            switch (BossType)
            {
                case BossKind.GiantHeron: return 12f;
                case BossKind.BottomlessPelican: return 16f;
                case BossKind.CormorantKing: return 10f;
                default: return 18f;
            }
        }
    }

    // ======================= PASADAS =======================

    BTStatus BossPasses(Vector3 p, Vector3 to, bool chaseUnder)
    {
        float dt = Time.fixedDeltaTime;
        float rage = BossRage;
        float speed = PassSpeed * rage;
        float turn = PassTurn * rage;
        bool hidden = PlayerHiddenDeep() && !IsDiver;
        float dist = to.magnitude;
        Vector3 flatTo = new Vector3(to.x, 0f, to.z);
        if (flatTo.sqrMagnitude < 0.01f) flatTo = transform.forward;
        passTimer += dt;

        switch (passStep)
        {
            case PassStep.Approach:
            {
                // Apunta a un costado del jugador y cierra la curva al llegar (no viene en línea recta)
                Vector3 lead = p + player.Velocity * Mathf.Clamp(dist / Mathf.Max(1f, speed), 0f, 0.6f);
                Vector3 side = Vector3.Cross(Vector3.up, flatTo.normalized) * passSide;
                Vector3 aim = lead + side * (Mathf.Clamp01((dist - 6f) / 20f) * 7f);
                if (hidden) aim.y = lake.SurfaceY + 1.2f;            // escondido: pasa rozando el agua encima de él
                else if (!chaseUnder) aim.y = Mathf.Max(aim.y, lake.SurfaceY + 0.8f);
                desiredVelocity = SteerBoss(aim - transform.position, speed, turn * 1.15f)
                                  + Weave(bossHeading, 4f, 1.6f * Mathf.Clamp01((dist - 5f) / 15f));
                burst = dist < 14f;                                   // acelera al final, como un caza
                if (BossType == BossKind.GiantHeron) lookOverride = flatTo;
                if (chaseUnder || transform.position.y < lake.SurfaceY) KeepDiving();
                if (!passStruck && !hidden && dist < BossReach && attackTimer <= 0f)
                {
                    BumpPlayer(1f);
                    passStruck = true;
                }
                if ((dist < 16f && Vector3.Dot(to, bossHeading) < 0f) || passTimer > 4.5f)
                {
                    passStep = PassStep.Through;
                    passTimer = 0f;
                    passThroughTime = Random.Range(0.35f, 0.55f);
                }
                break;
            }
            case PassStep.Through:
                // Sigue de largo con la inercia y remonta un poco
                desiredVelocity = SteerBoss(bossHeading + Vector3.up * (chaseUnder ? 0f : 0.3f), speed * 0.95f, turn * 0.3f);
                if (chaseUnder || transform.position.y < lake.SurfaceY) KeepDiving();
                if (passTimer > passThroughTime)
                {
                    passStep = PassStep.Turn;
                    passTimer = 0f;
                    passTurnTime = Random.Range(1.0f, 1.6f) * (BossType == BossKind.BottomlessPelican ? 1.35f : 1f) / rage;
                    passRangedAt = Random.Range(0.25f, 0.7f) * passTurnTime;
                    passRangedDone = false;
                    orbitDirection = Vector3.Dot(Vector3.Cross(Vector3.up, flatTo.normalized), bossHeading) > 0f ? 1f : -1f;
                }
                break;

            default:
            {
                // Vira en una curva amplia alrededor del jugador (a su altura sobre el agua) para la próxima pasada
                Vector3 from = transform.position - p;
                from.y = 0f;
                float a = Mathf.Atan2(from.z, from.x) + orbitDirection * 0.9f;
                float r = PassRadius * (0.9f + 0.2f * Mathf.Sin(Time.time * 1.1f + weaveSeed));
                float y = Mathf.Max(p.y, lake.SurfaceY) + HarassHeight;
                if (chaseUnder) y = p.y;
                else if (hidden) y = lake.SurfaceY + HarassHeight + 2f;
                Vector3 ring = new Vector3(p.x + Mathf.Cos(a) * r, y, p.z + Mathf.Sin(a) * r);
                desiredVelocity = SteerBoss(ring - transform.position, speed * 0.85f, turn);
                if (chaseUnder) KeepDiving();
                if (!passRangedDone && passTimer > passRangedAt)
                {
                    passRangedDone = true;
                    HarassStrike(p, to);
                }
                if (passTimer > passTurnTime)
                {
                    passStep = PassStep.Approach;
                    passTimer = 0f;
                    passStruck = false;
                    passSide = Random.value < 0.5f ? -1f : 1f;
                    NewBreakSide();
                    if (!hidden) Sfx.Play(Sonido.Embestida, transform.position, 1f, 0.7f, 12f);
                }
                break;
            }
        }
        return BTStatus.Running;
    }

    // ======================= ELECCIÓN DEL ATAQUE Y COMBOS =======================

    // Según la situación: lejos o escondido -> va por los peces; un cardumen apretado -> barrida. Nunca repite
    int ChooseBossAttack()
    {
        for (int i = 0; i < attackWeights.Length; i++) attackWeights[i] = 0f;
        bool hidden = PlayerHiddenDeep() && !IsDiver;
        float far = player != null ? FlatDistance(player.transform.position, transform.position) : 0f;
        bool wantsFish = hidden || far > 40f;
        int schoolSize = CountFishNear(DensestFishPoint(), 8f);
        int fishAttack = -1;                       // el ataque fuerte que más daño hace a los peces
        switch (BossType)
        {
            case BossKind.GiantHeron:
                attackWeights[0] = 1.2f;                                // estocada
                attackWeights[1] = 1f;                                  // pisotón
                attackWeights[2] = bossPhase >= 2 ? 1f : 0f;            // lluvia de dardos
                fishAttack = 1;
                break;
            case BossKind.BottomlessPelican:
                attackWeights[0] = 1f;                                  // remolino
                attackWeights[1] = 1f;                                  // cañonazo
                attackWeights[2] = bossPhase >= 2 ? 1.2f : 0f;          // bocado al cardumen
                fishAttack = 0;
                break;
            case BossKind.CormorantKing:
                attackWeights[0] = 1.3f;                                // torpedos
                attackWeights[1] = 1f;                                  // onda expansiva
                fishAttack = 1;
                break;
            case BossKind.EagleQueen:
                attackWeights[0] = 1.2f;                                // picadas
                attackWeights[1] = 1f;                                  // lluvia de plumas
                attackWeights[2] = bossPhase >= 2 ? 1f : 0f;            // vendaval
                fishAttack = bossPhase >= 2 ? 2 : 0;
                break;
        }
        attackWeights[SweepKind] = schoolSize >= 3 ? 0.8f + 0.3f * bossPhase : (schoolSize >= 2 ? 0.4f : 0f);
        if (wantsFish)
        {
            attackWeights[SweepKind] *= 2.5f;
            if (fishAttack >= 0) attackWeights[fishAttack] *= 2.5f;
        }
        if (lastBossAttack >= 0 && lastBossAttack < attackWeights.Length) attackWeights[lastBossAttack] *= 0.2f;

        float total = 0f;
        for (int i = 0; i < attackWeights.Length; i++) total += attackWeights[i];
        int kind = 0;
        if (total > 0f)
        {
            float roll = Random.value * total;
            for (int i = 0; i < attackWeights.Length; i++)
            {
                if (attackWeights[i] <= 0f) continue;
                kind = i;
                roll -= attackWeights[i];
                if (roll <= 0f) break;
            }
        }
        lastBossAttack = kind;
        bossAttackCount++;
        return kind;
    }

    // Al empezar un ataque nuevo (no encadenado): cuántos más encadena después sin descansar
    void PlanBossChain()
    {
        if (bossChaining)
        {
            bossChaining = false;
            return;
        }
        if (bossPhase >= 3) bossChainLeft = Random.value < 0.35f ? 2 : 1;
        else if (bossPhase == 2) bossChainLeft = Random.value < 0.5f ? 1 : 0;
        else bossChainLeft = 0;
    }

    // ¿Sigue con otro ataque en vez de quedar agotado? (no si se acaba de llevar peces)
    bool ContinueBossChain(bool tired)
    {
        if (!tired || bossChainLeft <= 0 || dead || carried.Count > 0)
        {
            bossChainLeft = 0;
            bossChaining = false;
            return false;
        }
        bossChainLeft--;
        bossChaining = true;
        bossAttackCooldown = 0.4f;
        return true;
    }

    // ======================= BARRIDA SOBRE EL CARDUMEN =======================

    int SweepMax { get { return BossType == BossKind.BottomlessPelican ? 3 + bossPhase : 1 + bossPhase; } }

    BTStatus BossSweep()
    {
        float rage = BossRage;
        float speed = PassSpeed * rage;
        Vector3 grab = talons != null ? talons.position : transform.position;
        if (bossAttackStep == 0)
        {
            // Elige el cardumen más apretado y avisa
            Vector3 c = DensestFishPoint();
            if (CountFishNear(c, 8f) < 1)
            {
                EndBossAttack(false);
                return BTStatus.Failure;
            }
            bossAttackPoint = c;
            Vector3 d = c - transform.position;
            d.y = 0f;
            sweepDir = d.sqrMagnitude > 1f ? d.normalized : transform.forward;
            sweepSplashAt = 0f;
            Toast("¡" + BossName.ToUpper() + " VA POR EL CARDUMEN!");
            Sfx.Play(Sonido.Picada, transform.position, 1f, 0.75f, 30f, 0.05f, 0.2f);
            Call(1f);
            NextStep();
            return BTStatus.Running;
        }

        Vector3 center = bossAttackPoint;
        // Altura de la pasada: las garras (o el pico) justo dentro del agua; el cormorán, bajo el agua
        float grabTarget = IsDiver ? lake.SurfaceY - 1.8f : lake.SurfaceY - 0.25f;
        float skimY = transform.position.y + (grabTarget - grab.y);

        if (bossAttackStep == 1)
        {
            // Toma carrera en curva hasta quedar alineado con el cardumen
            Vector3 entry = center - sweepDir * 16f;
            entry.y = Mathf.Max(lake.SurfaceY + 4f, skimY + 3f);
            desiredVelocity = SteerBoss(entry - transform.position, speed, PassTurn * rage);
            if (FlatDistance(entry, transform.position) < 7f || bossAttackTimer > 3.5f)
            {
                NextStep();
                Sfx.Play(Sonido.Embestida, transform.position, 1f, 0.6f, 20f);
                // El cardumen se paraliza del susto un instante
                StunFishAround(new Vector3(center.x, lake.SurfaceY, center.z), 7f, 1.5f, 3f);
            }
        }
        else if (bossAttackStep == 2)
        {
            // La pasada rasante: atraviesa el cardumen llevándose varios peces y dejando una estela de espuma
            KeepDiving();
            burst = true;
            Vector3 exit = center + sweepDir * 18f;
            exit.y = skimY;
            desiredVelocity = SteerBoss(exit - transform.position, speed * 1.1f, 70f);
            if (bossAttackTimer > sweepSplashAt)
            {
                sweepSplashAt = bossAttackTimer + 0.08f;
                if (!IsDiver && lake.IsInsideXZ(transform.position, 0f))
                    LakeWater.Splash(new Vector3(grab.x, lake.SurfaceY, grab.z), 0.4f, false);
            }
            int max = SweepMax;
            float reach = catchRadius * 1.5f * Mathf.Max(1f, visualScale * 0.6f);
            float r2 = reach * reach;
            for (int i = FishAI.All.Count - 1; i >= 0 && carried.Count < max; i--)
            {
                FishAI f = FishAI.All[i];
                if (f == null || !f.IsCatchable) continue;
                if ((f.transform.position - grab).sqrMagnitude > r2) continue;
                if (!IsDiver && f.Depth > catchDepth + 1.2f) continue;
                if (f.Hunter != null && f.Hunter != this) f.Hunter = null;
                if (carried.Count == 0)
                {
                    Catch(f);
                    KeepDiving();
                }
                else GrabFish(f);
            }
            bool passed = Vector3.Dot(transform.position - center, sweepDir) > 12f;
            if (passed || bossAttackTimer > 2.4f) NextStep();
        }
        else
        {
            // Remonta con lo que se llevó
            desiredVelocity = SteerBoss(sweepDir + Vector3.up * 0.9f, speed * 0.9f, 120f);
            if (bossAttackTimer > 0.45f)
            {
                if (carried.Count > 0)
                    Toast("¡" + BossName.ToUpper() + " SE LLEVA " + carried.Count + (carried.Count == 1 ? " PEZ" : " PECES") +
                          "!  DISPÁRALE PARA QUE LOS SUELTE");
                EndBossAttack(true);
                return BTStatus.Success;
            }
        }
        return BTStatus.Running;
    }

    // Circula alrededor del jugador mientras dispara (en vez de quedarse quieto en el aire)
    Vector3 StrafeAround(Vector3 p, float radius, float height)
    {
        Vector3 from = transform.position - p;
        from.y = 0f;
        if (from.sqrMagnitude < 0.01f) from = -transform.forward;
        float a = Mathf.Atan2(from.z, from.x) + orbitDirection * 0.55f;
        Vector3 goal = new Vector3(p.x + Mathf.Cos(a) * radius, Mathf.Max(p.y, lake.SurfaceY) + height, p.z + Mathf.Sin(a) * radius);
        Vector3 to = goal - transform.position;
        return SteerBoss(to, Mathf.Min(chaseSpeed * BossRage, to.magnitude * 3f + 4f), 160f);
    }
}
