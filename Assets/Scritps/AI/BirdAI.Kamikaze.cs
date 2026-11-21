using UnityEngine;
using FishGame.AI;

// Piquero KAMIKAZE (BirdAbility.Kamikaze). En la vida real el piquero pesca lanzándose al agua en picada como una
// flecha; aquí se lanza así contra el pez del traje. NO caza peces:
//  1. Acecho: te rodea a media distancia, un poco más alto (y esquiva disparos como las demás).
//  2. Aviso: se queda quieto apuntándote medio segundo: chilla y se pone rojo. ¡Es el momento del dash!
//  3. Embestida: se lanza en línea recta, muy rápido, corrigiendo poco (se puede esquivar).
//  4. Si te alcanza: te golpea fuerte y revienta (explosión de agua). Si lo esquivas: sigue de largo, remonta y
//     vuelve a intentarlo.
// Es frágil: 1 o 2 disparos. En cooperativo va por el jugador que le toque (el anfitrión reparte los blancos).
public partial class BirdAI
{
    public bool IsKamikaze { get { return Ability == BirdAbility.Kamikaze; } }

    enum KamiPhase { Approach, Aim, Strike, Overshoot }
    KamiPhase kamiPhase;
    float kamiTimer = -1f;
    float kamiOrbit;
    float kamiOrbitDir = 1f;
    Vector3 kamiDir = Vector3.forward;
    bool kamiLabelShown;

    const float KamiOrbitRadius = 16f;
    const float KamiOrbitHeight = 6f;
    const float KamiAimTime = 0.55f;
    const float KamiTurnRate = 55f;        // grados por segundo que corrige en la embestida: un dash lo esquiva
    const float KamiStrikeTime = 2.4f;
    const float KamiStrikeSpeed = 1.3f;    // veces su velocidad de picada
    const float KamiMaxPlayerDepth = 2f;   // si te escondes más hondo que esto, espera arriba

    bool WantsKamikaze()
    {
        return IsKamikaze && !IsBoss && player != null && !player.isDead;
    }

    void StartKamikazeApproach(float minTime, float maxTime)
    {
        kamiPhase = KamiPhase.Approach;
        kamiTimer = Random.Range(minTime, maxTime);
        kamiOrbitDir = Random.value < 0.5f ? -1f : 1f;
        Vector3 from = transform.position - player.transform.position;
        kamiOrbit = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
    }

    BTStatus Kamikaze()
    {
        float dt = Time.fixedDeltaTime;
        if (kamiTimer < 0f) StartKamikazeApproach(1.2f, 2.6f);
        // Lo sigue aunque salga del lago (los límites de vuelo se abren para el que pelea)
        aggroUntil = Mathf.Max(aggroUntil, Time.time + 1.5f);

        Vector3 target = player.transform.position + Vector3.up * 0.4f;
        Vector3 to = target - transform.position;
        float dist = to.magnitude;
        bool reachable = lake.SurfaceY - player.transform.position.y < KamiMaxPlayerDepth;
        kamiTimer -= dt;

        switch (kamiPhase)
        {
            case KamiPhase.Approach:
            {
                kamiOrbit += kamiOrbitDir * dt * 40f;
                Vector3 ring = Quaternion.Euler(0f, kamiOrbit, 0f) * Vector3.forward * KamiOrbitRadius;
                Vector3 point = player.transform.position + ring + Vector3.up * KamiOrbitHeight;
                point.y = Mathf.Max(point.y, lake.SurfaceY + 3f);
                Vector3 go = point - transform.position;
                float speed = Mathf.Lerp(patrolSpeed, chaseSpeed, Mathf.Clamp01(go.magnitude / 8f));
                desiredVelocity = go.sqrMagnitude > 0.01f ? go.normalized * speed : Vector3.zero;
                if (kamiTimer <= 0f && reachable && dist < 34f)
                {
                    kamiPhase = KamiPhase.Aim;
                    kamiTimer = KamiAimTime;
                    // Aviso: chillido agudo, se pone rojo y (la primera vez) el cartel
                    Sfx.Play(Sonido.Picada, transform.position, 1f, 1.35f, 14f, 0.08f, 0.1f);
                    Flash(new Color(3.6f, 0.5f, 0.35f, 1f), KamiAimTime + 0.1f);
                    if (!kamiLabelShown)
                    {
                        kamiLabelShown = true;
                        HitFX.BirdLabel(transform.position + Vector3.up * 0.8f, "¡KAMIKAZE!", new Color(1f, 0.38f, 0.28f, 1f));
                    }
                }
                break;
            }
            case KamiPhase.Aim:
                // Quieto (retrocede apenas), aleteando rápido y apuntándote
                windup = true;
                lookOverride = to;
                desiredVelocity = -to.normalized * 2f;
                if (kamiTimer <= 0f)
                {
                    kamiPhase = KamiPhase.Strike;
                    kamiTimer = KamiStrikeTime;
                    // Apunta un poco adelante de donde vas
                    Vector3 lead = target + player.Velocity * Mathf.Clamp(dist / Mathf.Max(1f, diveSpeed * KamiStrikeSpeed), 0f, 0.6f) * 0.5f;
                    kamiDir = (lead - transform.position).normalized;
                    Sfx.Play(Sonido.Embestida, transform.position, 1f, 1.25f, 12f, 0.06f, 0.1f);
                }
                break;

            case KamiPhase.Strike:
            {
                diving = true;
                diveTicked = true;
                burst = true;
                Vector3 want = to.sqrMagnitude > 0.01f ? to.normalized : kamiDir;
                kamiDir = Vector3.RotateTowards(kamiDir, want, KamiTurnRate * Mathf.Deg2Rad * dt, 0f);
                desiredVelocity = kamiDir * diveSpeed * KamiStrikeSpeed;
                lookOverride = kamiDir;
                if (dist < attackReach + 0.6f)
                {
                    // En plena vuelta de esquive te pasa de largo (no revienta)
                    if (player.IsDodgeInvulnerable) { kamiPhase = KamiPhase.Overshoot; kamiTimer = 1.1f; break; }
                    KamikazeImpact();
                    break;
                }
                // Ya te pasó de largo (o se acabó el impulso): sigue derecho y remonta
                if ((Vector3.Dot(to, kamiDir) < 0f && dist > 3f) || kamiTimer <= 0f || !reachable)
                {
                    kamiPhase = KamiPhase.Overshoot;
                    kamiTimer = 1.1f;
                }
                break;
            }
            case KamiPhase.Overshoot:
                desiredVelocity = (new Vector3(kamiDir.x, 0f, kamiDir.z).normalized + Vector3.up * 0.6f).normalized * chaseSpeed;
                if (kamiTimer <= 0f) StartKamikazeApproach(0.8f, 1.8f);
                break;
        }
        return BTStatus.Running;
    }

    // Te alcanzó: golpe fuerte y revienta con él (explosión de agua, cuenta como derribado)
    void KamikazeImpact()
    {
        if (dead) return;
        if (player != null && !player.isDead)
        {
            Vector3 dir = player.transform.position - transform.position;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            player.TakeHit(damage, dir * knockbackForce * 1.4f + Vector3.up * knockbackUp);
        }
        Sfx.Play(Sonido.Embestida, transform.position, 1f, 0.9f, 12f, 0.06f, 0.05f);
        if (health != null) health.TakeDamage(health.CurrentHealth + 9999, transform.position);
        else OnDied();
    }
}
