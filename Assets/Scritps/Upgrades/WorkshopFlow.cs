using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Visitas al Taller del modo Historia.
// CONTRATO para el menú y la pantalla de resultado: WorkshopScene, IsPending y Open.
// Una visita queda pendiente al ganar un nivel con "Workshop After" hasta que se instala una mejora;
// si se sale del Taller sin elegir, sigue pendiente (botón IR AL TALLER en el mapa).
public static class WorkshopFlow
{
    public const string WorkshopScene = "Taller";

    // Visita en curso (la lee la escena del Taller). -1 = se abrió la escena sin venir del juego
    public static int VisitLevelIndex { get; private set; } = -1;
    // Al terminar: true = jugar el siguiente nivel, false = volver al mapa
    public static bool ContinueToNextLevel { get; private set; }
    public static bool HasVisit { get { return VisitLevelIndex >= 0; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        VisitLevelIndex = -1;
        ContinueToNextLevel = false;
    }

    // ¿El nivel (índice del catálogo) da una visita al Taller que todavía no se usó?
    public static bool IsPending(int levelIndex)
    {
        if (GameSession.CurrentMode != GameSession.Mode.Historia) return false;
        return IsWorkshopLevel(levelIndex) && SaveSystem.IsCompleted(levelIndex) && !SaveSystem.IsWorkshopUsed(levelIndex);
    }

    // Abre el Taller por la visita de ese nivel. Al terminar: siguiente nivel o mapa.
    public static void Open(int levelIndex, bool continueToNextLevel)
    {
        VisitLevelIndex = levelIndex;
        ContinueToNextLevel = continueToNextLevel;
        GameSession.CurrentMode = GameSession.Mode.Historia;
        GameSession.LevelIndex = levelIndex;
        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(WorkshopScene))
        {
            Debug.LogWarning("WorkshopFlow: la escena '" + WorkshopScene + "' no está en File > Build Profiles. Se vuelve al mapa.");
            GameSession.GoToLevelMap();
            return;
        }
        SceneManager.LoadScene(WorkshopScene);
    }

    // Se olvida la visita en curso (al salir del Taller)
    public static void ClearVisit()
    {
        VisitLevelIndex = -1;
        ContinueToNextLevel = false;
    }

    // ---- Niveles con Taller (salen de "Workshop After" de cada nivel) ----

    public static bool IsWorkshopLevel(int levelIndex)
    {
        LevelCatalog catalog = GameSession.Catalog;
        LevelDefinition level = catalog != null ? catalog.Get(levelIndex) : null;
        return level != null && level.workshopAfter;
    }

    // Índices de los niveles con Taller, en orden
    public static List<int> WorkshopLevels()
    {
        List<int> list = new List<int>();
        LevelCatalog catalog = GameSession.Catalog;
        if (catalog == null) return list;
        for (int i = 0; i < catalog.Count; i++)
        {
            LevelDefinition level = catalog.Get(i);
            if (level != null && level.workshopAfter) list.Add(i);
        }
        return list;
    }

    // Número de visita (1 = la primera). Si el nivel no tiene Taller, cuenta las visitas anteriores + 1
    public static int VisitNumber(int levelIndex)
    {
        int n = 1;
        foreach (int i in WorkshopLevels())
        {
            if (i == levelIndex) return n;
            if (i < levelIndex) n++;
        }
        return n;
    }

    public static int VisitCount { get { return Mathf.Max(1, WorkshopLevels().Count); } }
}
