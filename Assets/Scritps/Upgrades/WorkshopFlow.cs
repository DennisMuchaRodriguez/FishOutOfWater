using UnityEngine;

// Visitas al Taller del modo Historia.
// CONTRATO para el menú y la pantalla de resultado: WorkshopScene, IsPending y Open.
public static class WorkshopFlow
{
    public const string WorkshopScene = "Taller";

    // ¿El nivel (índice del catálogo) da una visita al Taller que todavía no se usó?
    public static bool IsPending(int levelIndex)
    {
        return false;
    }

    // Abre el Taller por la visita de ese nivel. Al terminar: siguiente nivel o mapa.
    public static void Open(int levelIndex, bool continueToNextLevel)
    {
    }
}
