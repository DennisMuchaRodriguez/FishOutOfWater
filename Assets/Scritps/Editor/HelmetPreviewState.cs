using UnityEditor;
using UnityEngine;

// Estado de la vista previa del casco (HelmetPreviewWindow): si está encendida y los valores de ejemplo que muestra
// (traje, propulsor, munición, peces, oleada...). Vive solo en memoria y sobrevive a las recompilaciones
// (ScriptableSingleton). Va en su propio archivo porque Unity necesita que el nombre del archivo coincida con la
// clase para volver a encontrarla después de recompilar.
public class HelmetPreviewState : ScriptableSingleton<HelmetPreviewState>
{
    public bool on;

    // Jugador
    public float suit01 = 0.8f;
    public float fuel01 = 0.65f;
    public int ammo = 22;
    public bool inWater;
    public bool underwater;
    public bool thirdPerson;

    // Misión
    public int totalFish = 24;
    public int fishAlive = 17;
    public float lossFraction = 0.7f;
    public int totalWaves = 4;
    public int wave = 2;
    public int birds = 5;
    public bool waiting;
    public float countdown = 8f;
    public int defendersTotal = 4;
    public int defenders = 3;
}
