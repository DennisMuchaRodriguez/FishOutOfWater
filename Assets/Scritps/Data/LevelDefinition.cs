using System.Collections.Generic;
using UnityEngine;

// Un nivel del modo Historia guardado como archivo de datos.
// Crear: clic derecho en la carpeta > Create > Fish Out Of Water > Nivel,
// y agregarlo a Assets/Resources/LevelCatalog (la lista ordenada del mapa de niveles).
[CreateAssetMenu(menuName = "Fish Out Of Water/Nivel", fileName = "Nivel_00", order = 0)]
public class LevelDefinition : ScriptableObject
{
    [Header("Identificación")]
    public int number = 1;
    public string displayName = "Nivel 1";
    [TextArea(2, 4)] public string description;

    [Header("Peces del lago")]
    public List<FishSpawn> fish = new List<FishSpawn>();
    [Tooltip("Número de cardúmenes; los peces se reparten entre ellos")]
    [Min(1)] public int schools = 3;
    [Tooltip("Pierdes si cazan este porcentaje de peces (0.7 = 70 %)")]
    [Range(0.1f, 1f)] public float maxFishLossFraction = 0.7f;

    [Header("Oleadas (en orden)")]
    public List<WaveDefinition> waves = new List<WaveDefinition>();

    [Header("Mapa de niveles")]
    [Tooltip("Marca el nivel como nivel de jefe en el mapa (calavera)")]
    public bool isBossLevel;
    [Tooltip("Después de ganarlo se visita el Taller para mejorar el traje (llave en el mapa)")]
    public bool workshopAfter;

    [Header("Historia (viñetas de cómic)")]
    [Tooltip("Se ve antes de jugar el nivel la primera vez (se puede repetir desde el mapa)")]
    public ComicDefinition comicBefore;
    [Tooltip("Se ve al ganar el nivel la primera vez")]
    public ComicDefinition comicAfter;

    [Header("Estrellas (porcentaje de peces salvados)")]
    [Range(0f, 1f)] public float twoStars = 0.6f;
    [Range(0f, 1f)] public float threeStars = 0.9f;

    // 1 estrella al ganar; 2 y 3 según los peces que quedaron a salvo
    public int StarsFor(float savedFraction)
    {
        if (savedFraction >= threeStars) return 3;
        if (savedFraction >= twoStars) return 2;
        return 1;
    }
}

[System.Serializable]
public class FishSpawn
{
    public FishDefinition type;
    [Tooltip("Cuántos peces de este tipo")]
    [Min(1)] public int count = 5;
}

[System.Serializable]
public class BirdSpawn
{
    public BirdDefinition type;
    [Min(1)] public int count = 1;
}

[System.Serializable]
public class WaveDefinition
{
    [Tooltip("Segundos de calma antes de esta oleada")]
    public float delayBefore = 12f;
    [Tooltip("Qué aves llegan y cuántas de cada una")]
    public List<BirdSpawn> birds = new List<BirdSpawn>();
    [Tooltip("Multiplica la vida de todas las aves de esta oleada")]
    public float healthMultiplier = 1f;
}
