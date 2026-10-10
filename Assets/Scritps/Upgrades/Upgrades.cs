using UnityEngine;

// Consulta de las mejoras instaladas: 0 = no la tienes, 1 = nivel I, 2 = nivel II.
// En Historia salen del progreso guardado (SaveSystem).
// CONTRATO: otros scripts (HUD, menú) solo usan Level, Has y Changed.
public static class Upgrades
{
    public static event System.Action Changed;

    public static int Level(UpgradeId id)
    {
#if UNITY_EDITOR
        if (debugLevel >= 0) return debugLevel;
#endif
        return SaveSystem.GetUpgradeLevel(id);
    }

    public static bool Has(UpgradeId id)
    {
        return Level(id) > 0;
    }

    public static void NotifyChanged()
    {
        if (Changed != null) Changed();
    }

#if UNITY_EDITOR
    // Solo en el editor (atajo F10 del lago): fuerza el mismo nivel en todas las mejoras
    // sin tocar el progreso guardado. -1 = usar el progreso.
    static int debugLevel = -1;

    public static int DebugLevel { get { return debugLevel; } }

    public static void SetDebugLevel(int level)
    {
        debugLevel = Mathf.Clamp(level, -1, 2);
        NotifyChanged();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDebug() { debugLevel = -1; }
#endif
}
