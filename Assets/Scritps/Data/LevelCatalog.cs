using System.Collections.Generic;
using UnityEngine;

// Lista ordenada de los niveles del modo Historia.
// Vive en Assets/Resources/LevelCatalog.asset para que el menú y el juego la encuentren solos.
// Para agregar un nivel: créalo (Create > Fish Out Of Water > Nivel) y súmalo al final de "Levels".
[CreateAssetMenu(menuName = "Fish Out Of Water/Catálogo de niveles", fileName = "LevelCatalog", order = 10)]
public class LevelCatalog : ScriptableObject
{
    public const string ResourcePath = "LevelCatalog";

    [Tooltip("Los niveles del modo Historia, en orden (el primero empieza desbloqueado)")]
    public List<LevelDefinition> levels = new List<LevelDefinition>();

    static LevelCatalog cached;

    public static LevelCatalog Load()
    {
        if (cached == null) cached = Resources.Load<LevelCatalog>(ResourcePath);
        return cached;
    }

    public int Count { get { return levels.Count; } }

    public LevelDefinition Get(int index)
    {
        return index >= 0 && index < levels.Count ? levels[index] : null;
    }

    public int IndexOf(LevelDefinition level)
    {
        return level != null ? levels.IndexOf(level) : -1;
    }
}
