using System.Collections.Generic;
using UnityEngine;

// Un cómic (varias viñetas) para contar la historia entre niveles.
// Crear: clic derecho en la carpeta > Create > Fish Out Of Water > Cómic.
// Se asigna a un Nivel en "Comic Before" (se ve antes de jugarlo) o "Comic After" (al ganarlo).
[CreateAssetMenu(menuName = "Fish Out Of Water/Cómic", fileName = "Comic_Nuevo", order = 3)]
public class ComicDefinition : ScriptableObject
{
    [Tooltip("Título que se muestra arriba mientras se ve el cómic")]
    public string title = "Cómic";
    [Tooltip("Las viñetas, en orden. Una viñeta sin dibujo se muestra en blanco")]
    public List<ComicPanel> panels = new List<ComicPanel>();

    // Para recordar si ya se vio (se guarda en el progreso)
    public string Id { get { return name; } }
}

[System.Serializable]
public class ComicPanel
{
    [Tooltip("Dibujo de la viñeta (vacío = viñeta en blanco)")]
    public Sprite image;
    [Tooltip("Texto del recuadro de narración (opcional)")]
    [TextArea(2, 4)] public string caption;
}
