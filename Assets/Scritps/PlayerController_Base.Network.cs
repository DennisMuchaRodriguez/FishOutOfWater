using System;
using UnityEngine;

// Modo cooperativo (parte de PlayerController_Base): el pez de ESTE jugador y los "representantes" de los demás.
//  - Local: el pez que se maneja en esta computadora (el de la escena). Los HUD, el audio y las mejoras lo buscan con
//    FindLocal(), nunca con FindFirstObjectByType (que podría devolver un representante).
//  - Representante de red (IsNetworkProxy): un PlayerController_Base vacío que el anfitrión pone donde está cada
//    compañero, para que las aves lo persigan y lo ataquen igual que al pez local. No lee teclas, no tiene cámara ni
//    física: lo mueve CoopRemotePlayer con lo que llega por la red. Los golpes, empujones y corrientes que recibe NO
//    se aplican aquí: salen por la red al jugador dueño (ProxyHit, ProxyKnockback, ProxyCurrent), que los recibe en
//    su pez de verdad (con su esquive, su escudo y su armadura).
public partial class PlayerController_Base
{
    // El pez que se maneja en esta computadora
    public static PlayerController_Base Local { get; private set; }

    public bool IsNetworkProxy { get; private set; }
    // Casilla en la sala (0-3). -1 = sin sala
    [NonSerialized] public int NetSlot = -1;
    // Velocidad del compañero (la usan las aves para adelantarse)
    [NonSerialized] public Vector3 ProxyVelocity;

    // Lo que le pasa a un representante en el anfitrión (CoopGame lo manda al dueño)
    public static event Action<PlayerController_Base, float, Vector3> ProxyHit;
    public static event Action<PlayerController_Base, Vector3, float> ProxyKnockback;
    public static event Action<PlayerController_Base, Vector3> ProxyCurrent;

    static bool spawningProxy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetNetworkStatics()
    {
        Local = null;
        spawningProxy = false;
        ProxyHit = null;
        ProxyKnockback = null;
        ProxyCurrent = null;
    }

    void Awake()
    {
        if (spawningProxy)
        {
            IsNetworkProxy = true;
            return;
        }
        if (Local == null) Local = this;
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    // Crea el representante de un compañero en 'go' (sin cámara, sin física, sin teclas)
    public static PlayerController_Base AddNetworkProxy(GameObject go, int slot)
    {
        spawningProxy = true;
        PlayerController_Base p;
        try { p = go.AddComponent<PlayerController_Base>(); }
        finally { spawningProxy = false; }
        p.IsNetworkProxy = true;
        p.NetSlot = slot;
        return p;
    }

    // El pez de esta computadora (nunca un representante)
    public static PlayerController_Base FindLocal()
    {
        if (Local != null) return Local;
        foreach (PlayerController_Base p in FindObjectsByType<PlayerController_Base>(FindObjectsSortMode.None))
            if (p != null && !p.IsNetworkProxy) return p;
        return null;
    }

    // Lo que hace un representante cuando lo golpean, lo empujan o lo arrastra una corriente: avisar al dueño
    bool ForwardHit(float damage, Vector3 knockbackVelocity)
    {
        if (!IsNetworkProxy) return false;
        if (!isDead) ProxyHit?.Invoke(this, damage, knockbackVelocity);
        return true;
    }

    bool ForwardKnockback(Vector3 velocityChange, float controlLossTime)
    {
        if (!IsNetworkProxy) return false;
        if (!isDead) ProxyKnockback?.Invoke(this, velocityChange, controlLossTime);
        return true;
    }

    bool ForwardCurrent(Vector3 velocityChange)
    {
        if (!IsNetworkProxy) return false;
        if (!isDead) ProxyCurrent?.Invoke(this, velocityChange);
        return true;
    }

    // El representante copia el estado que mandó el dueño (CoopRemotePlayer)
    public void ApplyProxyState(bool dead, bool inWater, Vector3 velocity, float armor01)
    {
        if (!IsNetworkProxy) return;
        isDead = dead;
        isInWater = inWater;
        ProxyVelocity = velocity;
        currentArmor = Mathf.Clamp01(armor01) * Mathf.Max(1f, maxArmor);
    }
}
