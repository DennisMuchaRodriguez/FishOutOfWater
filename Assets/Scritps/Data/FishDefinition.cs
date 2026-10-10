using UnityEngine;

// Un tipo de pez del lago guardado como archivo de datos.
// Crear: clic derecho en la carpeta > Create > Fish Out Of Water > Pez.
// Cada nivel (LevelDefinition) dice cuántos de cada tipo aparecen.
[CreateAssetMenu(menuName = "Fish Out Of Water/Pez", fileName = "Pez_Nuevo", order = 2)]
public class FishDefinition : ScriptableObject
{
    [Tooltip("Modelo, material, animación y comportamiento de este pez (la cantidad la pone cada nivel)")]
    public FishType fish = new FishType();
}
