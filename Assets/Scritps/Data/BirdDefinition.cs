using UnityEngine;

// Un tipo de ave enemiga guardado como archivo de datos.
// Crear: clic derecho en la carpeta > Create > Fish Out Of Water > Ave.
// Se usa en las oleadas de cada nivel (LevelDefinition).
[CreateAssetMenu(menuName = "Fish Out Of Water/Ave", fileName = "Ave_Nueva", order = 1)]
public class BirdDefinition : ScriptableObject
{
    [Tooltip("Modelo, estadísticas y comportamiento de esta ave")]
    public BirdType bird = new BirdType();
}
