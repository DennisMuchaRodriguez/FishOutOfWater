using UnityEngine;

// Escudo de burbuja: absorbe un golpe y vuelve a cargarse.
// CONTRATO para el HUD: Instance, Unlocked, Ready, Recharge01 y Popped.
public class BubbleShield : MonoBehaviour
{
    public static BubbleShield Instance { get; private set; }

    public bool Unlocked { get; protected set; }
    public bool Ready { get; protected set; }
    // Progreso de la recarga (0..1); 1 si está listo
    public float Recharge01 { get; protected set; }

    public event System.Action Popped;

    protected virtual void Awake() { Instance = this; }
    protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

    protected void RaisePopped() { if (Popped != null) Popped(); }
}
