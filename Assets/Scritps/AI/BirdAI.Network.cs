using UnityEngine;

// Modo cooperativo (parte de BirdAI).
//  - En el anfitrión las aves son las de siempre; cada una persigue a uno de los jugadores (SetTarget) y CoopGame
//    va mandando dónde está, cómo se mueve y cuánta vida le queda.
//  - En los demás jugadores el ave es una "marioneta": tiene el mismo modelo, colisionador y vida (para que los
//    disparos, la mira, los misiles y el sonar funcionen igual), pero no piensa: sigue lo que manda el anfitrión.
//    Los golpes que recibe se mandan al anfitrión (Damageable.NetForward); el destello y los números se ven al
//    momento; la muerte llega del anfitrión (PuppetDie).
public partial class BirdAI
{
    public bool IsNetPuppet { get; private set; }
    // Número del ave en la partida (lo da el anfitrión)
    [System.NonSerialized] public int NetId = -1;
    // A qué jugador persigue
    public PlayerController_Base Target { get { return player; } }

    Vector3 netPos, netVel;
    Quaternion netRot = Quaternion.identity;
    float netTime = -1f;

    // Anfitrión: cambiar de presa (otro jugador)
    public void SetTarget(PlayerController_Base target)
    {
        if (target == null || target == player) return;
        player = target;
    }

    // Banderas para la red: 1 picada, 2 bajo el agua, 4 al acecho, 8 cernida, 16 lleva un pez
    public byte NetFlags
    {
        get
        {
            byte f = 0;
            if (diving) f |= 1;
            if (submerged) f |= 2;
            if (lurking) f |= 4;
            if (hovering) f |= 8;
            if (carried.Count > 0) f |= 16;
            return f;
        }
    }

    // Cliente: el ave queda como marioneta (llamar justo después de crearla)
    public void MakePuppet()
    {
        IsNetPuppet = true;
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        netPos = transform.position;
        netRot = transform.rotation;
        netTime = Time.time;
        arriving = false;
    }

    // Cliente: lo último que mandó el anfitrión
    public void PuppetState(Vector3 position, Quaternion rotation, byte flags, float health01)
    {
        if (!IsNetPuppet || dead) return;
        if (netTime > 0f)
        {
            float dt = Time.time - netTime;
            if (dt > 0.02f) netVel = Vector3.Lerp(netVel, (position - netPos) / dt, 0.6f);
        }
        netPos = position;
        netRot = rotation;
        netTime = Time.time;
        diving = (flags & 1) != 0;
        submerged = (flags & 2) != 0;
        lurking = (flags & 4) != 0;
        hovering = (flags & 8) != 0;
        if (health != null) health.SetHealthSilently(Mathf.RoundToInt(Mathf.Clamp01(health01) * health.maxHealth));
    }

    void PuppetStep(float dt)
    {
        if (dead || rb == null) return;
        // Un poco hacia adelante con la velocidad (los mensajes llegan cada 0,1 s)
        Vector3 target = netPos + netVel * Mathf.Clamp(Time.time - netTime, 0f, 0.3f);
        float k = 1f - Mathf.Exp(-14f * dt);
        Vector3 p = (target - rb.position).sqrMagnitude > 900f ? target : Vector3.Lerp(rb.position, target, k);
        rb.MovePosition(p);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, netRot, k));
    }

    // Golpe (de este jugador o de otro): solo el efecto, sin reacciones (las decide el anfitrión)
    public void PuppetHitFx(int amount, Vector3 point)
    {
        if (dead) return;
        hitPunch = IsArmored ? 0.45f : 1f;
        ShowHitEffect(amount, point);
        FXFactory.SpawnOneShot(hitPrefab, point, Quaternion.identity, 0.8f, 2f);
        PlayHitSound(point);
    }

    // El anfitrión dice que murió: cae con todos sus efectos
    public void PuppetDie()
    {
        if (dead) return;
        if (rb != null) rb.isKinematic = false;
        OnDied();
    }
}
