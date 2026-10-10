using UnityEngine;

// Consulta de las mejoras instaladas: 0 = no la tienes, 1 = nivel I, 2 = nivel II.
// En Historia salen del progreso guardado (SaveSystem).
// CONTRATO: otros scripts (HUD, menú) solo usan Level, Has y Changed.
public static class Upgrades
{
    public static event System.Action Changed;

    public static int Level(UpgradeId id)
    {
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
}
