using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Progreso del jugador en un archivo JSON (Application.persistentDataPath/progreso.json):
// niveles desbloqueados, estrellas por nivel, cómics ya vistos, mejoras del traje y visitas al Taller.
[System.Serializable]
public class SaveData
{
    public int version = 1;
    [Tooltip("Cuántos niveles están desbloqueados (1 = solo el primero)")]
    public int unlockedLevels = 1;
    public List<int> stars = new List<int>();
    public List<string> comicsSeen = new List<string>();
    [Tooltip("Mejoras instaladas, como \"Nombre:nivel\" (por ejemplo BubbleGun:1)")]
    public List<string> upgrades = new List<string>();
    [Tooltip("Niveles (índice del catálogo) cuya visita al Taller ya se usó")]
    public List<int> workshopsUsed = new List<int>();
}

public static class SaveSystem
{
    const string FileName = "progreso.json";
    static SaveData data;

    public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static void Load()
    {
        data = null;
        try
        {
            if (File.Exists(FilePath)) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveSystem: no se pudo leer el progreso (" + e.Message + "). Se empieza de cero.");
        }
        if (data == null) data = new SaveData();
        if (data.stars == null) data.stars = new List<int>();
        if (data.comicsSeen == null) data.comicsSeen = new List<string>();
        if (data.upgrades == null) data.upgrades = new List<string>();
        if (data.workshopsUsed == null) data.workshopsUsed = new List<int>();
        if (data.unlockedLevels < 1) data.unlockedLevels = 1;
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveSystem: no se pudo guardar el progreso (" + e.Message + ").");
        }
    }

    // ---- Niveles ----

    public static bool IsUnlocked(int index) { return index >= 0 && index < Data.unlockedLevels; }

    public static int GetStars(int index)
    {
        return index >= 0 && index < Data.stars.Count ? Data.stars[index] : 0;
    }

    public static bool IsCompleted(int index) { return GetStars(index) > 0; }

    public static int TotalStars
    {
        get
        {
            int total = 0;
            foreach (int s in Data.stars) total += s;
            return total;
        }
    }

    // Guarda el mejor resultado del nivel y desbloquea el siguiente
    public static void RecordVictory(int index, int stars)
    {
        if (index < 0) return;
        while (Data.stars.Count <= index) Data.stars.Add(0);
        Data.stars[index] = Mathf.Max(Data.stars[index], Mathf.Clamp(stars, 1, 3));
        Data.unlockedLevels = Mathf.Max(Data.unlockedLevels, index + 2);
        Save();
    }

    // ---- Cómics ----

    public static bool HasSeen(ComicDefinition comic)
    {
        return comic != null && Data.comicsSeen.Contains(comic.Id);
    }

    public static void MarkSeen(ComicDefinition comic)
    {
        if (comic == null || Data.comicsSeen.Contains(comic.Id)) return;
        Data.comicsSeen.Add(comic.Id);
        Save();
    }

    // ---- Mejoras del traje ----

    public static int GetUpgradeLevel(UpgradeId id)
    {
        string prefix = id + ":";
        foreach (string entry in Data.upgrades)
        {
            if (!entry.StartsWith(prefix)) continue;
            int level;
            if (int.TryParse(entry.Substring(prefix.Length), out level)) return Mathf.Clamp(level, 0, 2);
        }
        return 0;
    }

    public static void SetUpgradeLevel(UpgradeId id, int level)
    {
        string prefix = id + ":";
        Data.upgrades.RemoveAll(e => e.StartsWith(prefix));
        if (level > 0) Data.upgrades.Add(prefix + Mathf.Clamp(level, 1, 2));
        Save();
        Upgrades.NotifyChanged();
    }

    // ---- Taller ----

    public static bool IsWorkshopUsed(int levelIndex) { return Data.workshopsUsed.Contains(levelIndex); }

    public static void MarkWorkshopUsed(int levelIndex)
    {
        if (levelIndex < 0 || Data.workshopsUsed.Contains(levelIndex)) return;
        Data.workshopsUsed.Add(levelIndex);
        Save();
    }

    // ---- Pruebas ----

    public static void ResetProgress()
    {
        data = new SaveData();
        Save();
        Upgrades.NotifyChanged();
    }

    public static void UnlockAll(int levelCount)
    {
        Data.unlockedLevels = Mathf.Max(Data.unlockedLevels, levelCount);
        Save();
    }
}
