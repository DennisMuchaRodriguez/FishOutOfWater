using System.Collections.Generic;
using UnityEngine;

// Partida del modo SUPERVIVENCIA (oleadas sin fin). Vive entre escenas: después de cada oleada se va al Taller y al
// volver el lago se carga de nuevo, así que lo que dura toda la partida se guarda aquí:
//  - La última oleada superada, los peces cazados (no vuelven, salvo los que llegan al vencer a un jefe), las aves
//    derribadas y los peces rescatados.
//  - Las mejoras de la partida: empiezan de cero (estilo roguelike) y NO tocan las del modo Historia.
//  - La hora del día (el ciclo de día, tarde y noche sigue donde quedó).
// La empieza GameSession.PlaySurvival (o NUEVA PARTIDA); se termina al volver al menú.
public static class SurvivalRun
{
    // Hora con la que empieza cada partida (0 = amanecer, 0,25 = mediodía; ver LakeAmbience)
    public const float StartTimeOfDay = 0.1f;

    public static bool Active { get; private set; }
    // Oleadas superadas en esta partida (la siguiente es NextWave)
    public static int WavesCleared { get; private set; }
    public static int NextWave { get { return WavesCleared + 1; } }
    public static int FishLost { get; private set; }
    public static int BirdsKilled { get; private set; }
    public static int FishRescued { get; private set; }
    public static int WorkshopVisits { get; private set; }
    // La última oleada superada tenía jefe (el mecánico lo comenta)
    public static bool LastWasBoss { get; private set; }
    // Esta partida ya batió el récord
    public static bool NewRecord { get; private set; }
    // Semilla de la partida: las oleadas y los planos del Taller salen de ella
    public static int Seed { get; private set; }
    // Hora del día (0-1). La avanza y la guarda LakeAmbience
    public static float TimeOfDay = StartTimeOfDay;

    static readonly Dictionary<UpgradeId, int> upgrades = new Dictionary<UpgradeId, int>();

    // ¿Se está jugando Supervivencia? (partida en curso y ese modo elegido)
    public static bool InSurvival { get { return Active && GameSession.CurrentMode == GameSession.Mode.Supervivencia; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        Clear();
    }

    // Partida nueva: todo de cero
    public static void Begin()
    {
        Clear();
        Active = true;
        Seed = Random.Range(1, 1000000);
        Upgrades.NotifyChanged();
    }

    // Fin de la partida (al volver al menú)
    public static void End()
    {
        if (!Active) return;
        Active = false;
        Clear();
        Upgrades.NotifyChanged();
    }

    static void Clear()
    {
        WavesCleared = 0;
        FishLost = 0;
        BirdsKilled = 0;
        FishRescued = 0;
        WorkshopVisits = 0;
        LastWasBoss = false;
        NewRecord = false;
        TimeOfDay = StartTimeOfDay;
        upgrades.Clear();
    }

    // ---- Mejoras de la partida ----

    public static int UpgradeLevel(UpgradeId id)
    {
        int level;
        return upgrades.TryGetValue(id, out level) ? level : 0;
    }

    public static void SetUpgrade(UpgradeId id, int level)
    {
        upgrades[id] = Mathf.Clamp(level, 0, 2);
        Upgrades.NotifyChanged();
    }

    // ¿Le queda algo por mejorar al traje en esta partida?
    public static bool HasUpgradesLeft()
    {
        foreach (UpgradeId id in UpgradeCatalog.OrderedIds())
            if (UpgradeLevel(id) < UpgradeCatalog.MaxLevelOf(id)) return true;
        return false;
    }

    // ---- Progreso ----

    // Al superar una oleada (lo llama el GameDirector): guarda cómo quedó el lago. Devuelve true si es récord
    public static bool RecordWave(int wave, bool boss, int fishLost, int birdsKilled, int fishRescued)
    {
        WavesCleared = Mathf.Max(WavesCleared, wave);
        LastWasBoss = boss;
        SaveTotals(fishLost, birdsKilled, fishRescued);
        bool record = SaveSystem.RecordSurvival(WavesCleared);
        if (record) NewRecord = true;
        return record;
    }

    public static void SaveTotals(int fishLost, int birdsKilled, int fishRescued)
    {
        FishLost = Mathf.Max(0, fishLost);
        BirdsKilled = Mathf.Max(0, birdsKilled);
        FishRescued = Mathf.Max(0, fishRescued);
    }

    // Al salir del Taller
    public static void MarkWorkshopVisit()
    {
        WorkshopVisits++;
    }
}
