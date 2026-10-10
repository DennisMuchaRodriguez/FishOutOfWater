using UnityEngine;

// Arma secundaria: misiles teledirigidos (tecla E).
// CONTRATO para el HUD: Instance, Unlocked, Capacity, Loaded, Reload01 y Fired.
public class MissileLauncher : MonoBehaviour
{
    public static MissileLauncher Instance { get; private set; }

    public bool Unlocked { get; protected set; }
    public int Capacity { get; protected set; }
    public int Loaded { get; protected set; }
    // Progreso de la recarga del siguiente misil (0..1); 1 si está lleno
    public float Reload01 { get; protected set; }

    public event System.Action Fired;

    protected virtual void Awake() { Instance = this; }
    protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

    protected void RaiseFired() { if (Fired != null) Fired(); }
}
