using System.Collections.Generic;
using UnityEngine;

// Lista de todas las mejoras del traje (los planos del Taller).
// Vive en Assets/Resources/UpgradeCatalog.asset para que el Taller y los menús la encuentren solos.
// Si falta una mejora en la lista, el juego sigue funcionando con su nombre interno.
[CreateAssetMenu(menuName = "Fish Out Of Water/Catálogo de mejoras", fileName = "UpgradeCatalog", order = 21)]
public class UpgradeCatalog : ScriptableObject
{
    public const string ResourcePath = "UpgradeCatalog";

    [Tooltip("Las mejoras del traje (una por cada UpgradeId); este orden se usa para buscar reemplazos")]
    public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

    static UpgradeCatalog cached;

    public static UpgradeCatalog Load()
    {
        if (cached == null) cached = Resources.Load<UpgradeCatalog>(ResourcePath);
        return cached;
    }

    public UpgradeDefinition Get(UpgradeId id)
    {
        foreach (UpgradeDefinition u in upgrades)
            if (u != null && u.id == id) return u;
        return null;
    }

    // ---- Atajos (funcionan aunque falte el archivo) ----

    public static UpgradeDefinition Find(UpgradeId id)
    {
        UpgradeCatalog c = Load();
        return c != null ? c.Get(id) : null;
    }

    public static string NameOf(UpgradeId id)
    {
        UpgradeDefinition u = Find(id);
        return u != null && !string.IsNullOrEmpty(u.displayName) ? u.displayName : id.ToString();
    }

    public static int MaxLevelOf(UpgradeId id)
    {
        UpgradeDefinition u = Find(id);
        if (u != null) return Mathf.Clamp(u.maxLevel, 1, 2);
        return id == UpgradeId.DoubleDash || id == UpgradeId.PickupMagnet ? 1 : 2;
    }

    public static UpgradeCategory CategoryOf(UpgradeId id)
    {
        UpgradeDefinition u = Find(id);
        if (u != null) return u.category;
        switch (id)
        {
            case UpgradeId.BubbleGun:
            case UpgradeId.PiercingShot: return UpgradeCategory.PrimaryWeapon;
            case UpgradeId.HomingMissiles: return UpgradeCategory.SecondaryWeapon;
            case UpgradeId.ExtraTank:
            case UpgradeId.EfficientTurbo:
            case UpgradeId.DoubleDash: return UpgradeCategory.Mobility;
            case UpgradeId.ReinforcedPlates:
            case UpgradeId.BubbleShield:
            case UpgradeId.WaterRepair: return UpgradeCategory.Defense;
            default: return UpgradeCategory.Utility;
        }
    }

    public static Color AccentOf(UpgradeId id)
    {
        UpgradeDefinition u = Find(id);
        return u != null ? u.accent : UpgradeDefinition.CategoryColor(CategoryOf(id));
    }

    public static UpgradeIcon IconOf(UpgradeId id)
    {
        UpgradeDefinition u = Find(id);
        return u != null ? u.icon : (UpgradeIcon)Mathf.Clamp((int)id, 0, (int)UpgradeIcon.Sonar);
    }

    // Todas las mejoras en el orden del catálogo (y al final las que falten en la lista)
    public static List<UpgradeId> OrderedIds()
    {
        List<UpgradeId> ids = new List<UpgradeId>();
        UpgradeCatalog c = Load();
        if (c != null)
            foreach (UpgradeDefinition u in c.upgrades)
                if (u != null && !ids.Contains(u.id)) ids.Add(u.id);
        foreach (UpgradeId id in (UpgradeId[])System.Enum.GetValues(typeof(UpgradeId)))
            if (!ids.Contains(id)) ids.Add(id);
        return ids;
    }

    // Mejoras instaladas ahora (nivel 1 o 2), en el orden del catálogo
    public static List<UpgradeId> Installed()
    {
        List<UpgradeId> list = new List<UpgradeId>();
        foreach (UpgradeId id in OrderedIds())
            if (Upgrades.Level(id) > 0) list.Add(id);
        return list;
    }
}
