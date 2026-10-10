using UnityEngine;

// Una mejora del traje guardada como archivo de datos (los "planos" del Taller).
// Crear: clic derecho en la carpeta > Create > Fish Out Of Water > Mejora,
// y agregarla a Assets/Resources/UpgradeCatalog.
// Aquí van los textos, el ícono y el color del plano; los valores de juego los aplica el traje.
[CreateAssetMenu(menuName = "Fish Out Of Water/Mejora", fileName = "Mejora_Nueva", order = 20)]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Identificación")]
    [Tooltip("Qué mejora del traje es (cada una va una sola vez en el catálogo)")]
    public UpgradeId id;
    [Tooltip("Nombre que se ve en el Taller")]
    public string displayName = "Mejora";
    public UpgradeCategory category;
    [Tooltip("1 = solo tiene nivel I (Dash doble, Imán de cápsulas); 2 = tiene nivel II")]
    [Range(1, 2)] public int maxLevel = 2;

    [Header("Textos del plano")]
    [Tooltip("Qué es, en una o dos líneas")]
    [TextArea(2, 3)] public string description;
    [Tooltip("Qué hace el nivel I")]
    [TextArea(1, 3)] public string levelOne;
    [Tooltip("Qué cambia el nivel II (vacío si no tiene)")]
    [TextArea(1, 3)] public string levelTwo;
    [Tooltip("Qué pieza nueva se ve en el traje")]
    public string suitPart;

    [Header("Aspecto")]
    [Tooltip("Dibujo del plano (se genera por código)")]
    public UpgradeIcon icon;
    [Tooltip("Color del holograma")]
    public Color accent = new Color(0.41f, 0.87f, 0.9f, 1f);
    [Tooltip("Solo para el modo cooperativo (todavía no se usa)")]
    public bool coopOnly;

    // Lo que hace ese nivel (1 o 2)
    public string LevelText(int level)
    {
        return level >= 2 && !string.IsNullOrEmpty(levelTwo) ? levelTwo : levelOne;
    }

    public string CategoryName { get { return CategoryLabel(category); } }

    public static string CategoryLabel(UpgradeCategory category)
    {
        switch (category)
        {
            case UpgradeCategory.PrimaryWeapon: return "Arma principal";
            case UpgradeCategory.SecondaryWeapon: return "Arma secundaria";
            case UpgradeCategory.Mobility: return "Movilidad";
            case UpgradeCategory.Defense: return "Defensa";
            default: return "Utilidad";
        }
    }

    // Color de la etiqueta de cada categoría
    public static Color CategoryColor(UpgradeCategory category)
    {
        switch (category)
        {
            case UpgradeCategory.PrimaryWeapon: return new Color(1f, 0.55f, 0.2f, 1f);
            case UpgradeCategory.SecondaryWeapon: return new Color(1f, 0.36f, 0.26f, 1f);
            case UpgradeCategory.Mobility: return new Color(0.41f, 0.87f, 0.9f, 1f);
            case UpgradeCategory.Defense: return new Color(0.42f, 0.92f, 0.55f, 1f);
            default: return new Color(1f, 0.82f, 0.3f, 1f);
        }
    }

    public static string Roman(int level) { return level >= 2 ? "II" : "I"; }
}

public enum UpgradeCategory
{
    [InspectorName("Arma principal")] PrimaryWeapon,
    [InspectorName("Arma secundaria")] SecondaryWeapon,
    [InspectorName("Movilidad")] Mobility,
    [InspectorName("Defensa")] Defense,
    [InspectorName("Utilidad")] Utility
}

// Dibujos de los planos (WorkshopIcons los genera)
public enum UpgradeIcon
{
    [InspectorName("Burbujas")] Bubbles,
    [InspectorName("Misil")] Missile,
    [InspectorName("Flecha perforante")] Pierce,
    [InspectorName("Tanque")] Tank,
    [InspectorName("Llama de turbo")] Turbo,
    [InspectorName("Doble flecha")] Dash,
    [InspectorName("Placa")] Plates,
    [InspectorName("Escudo de burbuja")] Shield,
    [InspectorName("Gota con cruz")] Repair,
    [InspectorName("Balas")] Magazine,
    [InspectorName("Imán")] Magnet,
    [InspectorName("Sonar")] Sonar
}
