using UnityEngine;
using UnityEngine.SceneManagement;

// Lo que se eligió en el menú (modo y nivel) y los saltos entre escenas.
// El menú llama a PlayLevel; el juego lee CurrentLevel; los botones de fin de nivel usan el resto.
public static class GameSession
{
    public enum Mode { Historia, Supervivencia, Cooperativo }

    public const string MenuScene = "MainMenu";
    public const string LakeScene = "SampleScene";
    public const string WorkshopScene = WorkshopFlow.WorkshopScene;

    public static Mode CurrentMode = Mode.Historia;
    // Índice en el LevelCatalog (-1 = no se eligió desde el menú)
    public static int LevelIndex = -1;
    // Al volver al menú, abrir directamente el mapa de niveles
    public static bool OpenLevelMap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        CurrentMode = Mode.Historia;
        LevelIndex = -1;
        OpenLevelMap = false;
    }

    public static LevelCatalog Catalog { get { return LevelCatalog.Load(); } }

    public static LevelDefinition CurrentLevel
    {
        get { return Catalog != null ? Catalog.Get(LevelIndex) : null; }
    }

    public static bool HasNextLevel
    {
        get { return Catalog != null && LevelIndex >= 0 && LevelIndex + 1 < Catalog.Count; }
    }

    public static void PlayLevel(int index)
    {
        CurrentMode = Mode.Historia;
        LevelIndex = index;
        Time.timeScale = 1f;
        SceneManager.LoadScene(LakeScene);
    }

    public static void ReplayLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void PlayNextLevel()
    {
        if (HasNextLevel) PlayLevel(LevelIndex + 1);
        else GoToLevelMap();
    }

    public static void GoToLevelMap()
    {
        OpenLevelMap = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }

    public static void GoToMainMenu()
    {
        OpenLevelMap = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }

    // ---- Taller (modo Historia) ----

    // ¿Ganar este nivel da una visita al Taller que todavía no se usó?
    public static bool HasPendingWorkshop(int index) { return WorkshopFlow.IsPending(index); }

    // Abre el Taller por la visita de ese nivel. continueToNextLevel: al terminar, siguiente nivel (si no, el mapa)
    public static void GoToWorkshop(int index, bool continueToNextLevel)
    {
        WorkshopFlow.Open(index, continueToNextLevel);
    }

    // Salida del Taller: siguiente nivel (si se pidió y existe) o el mapa
    public static void LeaveWorkshop(bool playNextLevel)
    {
        WorkshopFlow.ClearVisit();
        if (playNextLevel && HasNextLevel) PlayLevel(LevelIndex + 1);
        else GoToLevelMap();
    }
}
