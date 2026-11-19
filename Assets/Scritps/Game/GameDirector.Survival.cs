using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Modo SUPERVIVENCIA del GameDirector (oleadas sin fin, mismo lago que la historia).
// Lo que dura toda la partida vive en SurvivalRun; las oleadas las arma SurvivalWaves. Flujo:
//  1. Al cargar el lago: los peces que quedan (los cazados no vuelven), los defensores (uno más cada 5 oleadas) y la
//     hora del día donde quedó.
//  2. Cuenta atrás y llega la oleada: a veces toda junta, a veces por grupos que llegan por distintos lados (avisa
//     por dónde viene cada grupo). Cinemática en la primera oleada y en las de jefe.
//  3. Oleada superada: se guarda el récord y la siguiente llega aquí mismo, SIN reparar el traje (la armadura que
//     perdiste no vuelve). CADA 5 OLEADAS (después de cada jefe) pasan las tres cosas juntas: se va al Taller a elegir
//     una mejora (estilo roguelike: solo para esta partida), el traje vuelve reparado y recargado (es la ÚNICA vez que
//     te curas) y llega 1 pez defensor más. Con el traje ya al máximo igual se baja al Taller a repararlo.
//     Al vencer a un jefe llegan unos peces nuevos al lago.
//  4. Se pierde si el traje se destruye o si cazan demasiados peces (el 75 % de los del lago).
// Para probar sin pasar por el menú: marca "Test Survival" y dale Play en la escena del lago.
public partial class GameDirector
{
    [Header("Supervivencia")]
    [Tooltip("Para probar: al dar Play directo en esta escena se juega Supervivencia en vez de 'Test Level'")]
    public bool testSurvival = false;
    [Tooltip("Peces del lago al empezar la partida (los cazados no vuelven)")]
    [Min(4)] public int survivalFish = 28;
    [Tooltip("Pierdes si cazan este porcentaje de los peces del lago")]
    [Range(0.3f, 1f)] public float survivalLossFraction = 0.75f;
    [Tooltip("Peces nuevos que llegan al lago al vencer una oleada con jefe (sin pasar de los del principio)")]
    [Min(0)] public int survivalFishPerBoss = 3;
    [Tooltip("Segundos de calma antes de cada oleada")]
    public float survivalCalm = 10f;

    // ¿Se está jugando Supervivencia?
    public bool IsSurvival { get; private set; }
    // La oleada de Supervivencia en curso o la que viene (null en la historia)
    public SurvivalWaves.Wave SurvivalWave { get; private set; }
    // Grupos de la oleada que todavía no llegaron
    public int PendingGroups { get; private set; }

    // Play directo en la escena con "Test Survival" (sin venir del menú): empieza una partida de prueba
    void PrepareTestSurvival()
    {
        if (testSurvival && GameSession.CurrentLevel == null && !SurvivalRun.Active)
        {
            GameSession.CurrentMode = GameSession.Mode.Supervivencia;
            GameSession.LevelIndex = -1;
            SurvivalRun.Begin();
        }
    }

    // ¿Supervivencia? (desde el menú, o con "Test Survival" al dar Play en la escena)
    bool ResolveSurvival(LevelCatalog catalog)
    {
        IsSurvival = SurvivalRun.InSurvival;
        if (!IsSurvival) return false;

        // Los tipos de pez y los cardúmenes salen del último nivel de la historia
        Level = catalog != null && catalog.Count > 0 ? catalog.Get(catalog.Count - 1) : null;
        if (Level == null) Level = testLevel;
        if (Level == null)
            Debug.LogError("GameDirector: Supervivencia necesita niveles en Assets/Resources/LevelCatalog (o un 'Test Level').");
        return true;
    }

    // Peces de la partida: los que quedan vivos, repartidos entre los tipos del nivel base
    bool BuildSurvivalPlan()
    {
        fishPlan.Clear();
        plan = new List<RuntimeWave>();
        List<FishSpawn> valid = new List<FishSpawn>();
        int baseTotal = 0;
        foreach (FishSpawn f in Level.fish)
        {
            if (f == null || f.type == null || f.type.fish == null || f.count <= 0) continue;
            valid.Add(f);
            baseTotal += f.count;
        }
        if (baseTotal <= 0)
        {
            Debug.LogError("GameDirector: el nivel '" + Level.name + "' no tiene peces para Supervivencia.");
            return false;
        }

        int alive = Mathf.Clamp(survivalFish - SurvivalRun.FishLost, 0, survivalFish);
        int given = 0;
        for (int i = 0; i < valid.Count; i++)
        {
            int c = i == valid.Count - 1 ? alive - given : Mathf.RoundToInt(alive * (float)valid[i].count / baseTotal);
            c = Mathf.Clamp(c, 0, alive - given);
            given += c;
            fishPlan.Add(new FishPlan { type = valid[i].type.fish, count = c });
        }
        schoolCount = Mathf.Max(1, Level.schools);
        return true;
    }

    // Después de soltar los peces: los totales de la partida (los defensores no cuentan)
    void RestoreSurvivalTotals()
    {
        TotalFish = Mathf.Max(1, survivalFish);
        FishLost = Mathf.Clamp(SurvivalRun.FishLost, 0, TotalFish);
        BirdsKilled = SurvivalRun.BirdsKilled;
        FishRescued = SurvivalRun.FishRescued;
    }

    // Fin de la partida (muerte o demasiados peces cazados)
    void OnSurvivalOver()
    {
        SurvivalRun.SaveTotals(FishLost, BirdsKilled, FishRescued);
        SaveSystem.Save(); // aves derribadas del Archivo
    }

    IEnumerator SurvivalFlow()
    {
        State = GameState.Calm;
        yield return null;
        // Mientras la pantalla de carga tapa el juego no empieza nada
        while (LoadingScreen.IsLoading) yield return null;

        int n = SurvivalRun.NextWave;
        int fishLayer = LayerMask.NameToLayer("Fish");
        if (n == 1)
            Banner?.Invoke("SUPERVIVENCIA", "Oleadas sin fin  //  Cada " + SurvivalWaves.WorkshopEvery +
                           " oleadas: Taller, traje reparado y un defensor más", Cyan);
        else
            Banner?.Invoke("SE ACERCA LA OLEADA " + n, "Traje reparado  //  Quedan " + FishAlive + " peces en el lago", Cyan);
        if (DefendersSpawned > 0)
            Toast?.Invoke(DefendersSpawned == 1 ? "LLEGÓ UN PEZ DEFENSOR A PATRULLAR EL LAGO"
                                                : DefendersSpawned + " PECES DEFENSORES PATRULLAN EL LAGO (UNO NUEVO)", Cyan);

        float calm = survivalCalm + (n == 1 ? 3f : 0f);
        bool firstHere = true;
        while (!ended)
        {
            SurvivalWaves.Wave wave = SurvivalWaves.Build(n, SurvivalRun.Seed);
            SurvivalWave = wave;
            CurrentWave = n;
            State = firstHere ? GameState.Calm : GameState.Intermission;
            Countdown = calm;
            while (Countdown > 0f)
            {
                if (ended) yield break;
                Countdown -= Time.deltaTime;
                yield return null;
            }

            // ---- Llega la oleada: los grupos sin espera ya; el resto, a su tiempo ----
            State = GameState.WaveIncoming;
            currentHealthMultiplier = wave.healthMultiplier;
            float baseYaw = arrivalYaw >= 0f ? arrivalYaw : Random.Range(0f, 360f);
            List<SurvivalWaves.Group> later = new List<SurvivalWaves.Group>();
            List<BirdAI> spawned = new List<BirdAI>();
            for (int g = 0; g < wave.groups.Count; g++)
            {
                SurvivalWaves.Group group = wave.groups[g];
                if (group.delay <= 0.01f)
                    spawned.AddRange(SpawnGroup(group.birds, baseYaw + group.yawOffset, wave.healthMultiplier, wave.bossHealthMultiplier, "_" + (g + 1)));
                else later.Add(group);
            }
            later.Sort((a, b) => a.delay.CompareTo(b.delay));
            PendingGroups = later.Count;

            Banner?.Invoke("OLEADA " + n, SurvivalWaves.Describe(wave), Danger);
            Sfx.Play2D(Sonido.Oleada, 0.9f);
            bool showCinematic = playArrivalCinematic && (n == 1 || wave.IsBossWave) && spawned.Count > 0
                                 && player != null && player.PlayerCamera != null && !player.isDead;
            if (showCinematic)
            {
                cinematic = WaveCinematic.Play(player, spawned, lake, cinematicDuration, on => Letterbox?.Invoke(on), cinematicShots);
                float guard = 0f;
                while (cinematic != null && guard < cinematicDuration + 8f)
                {
                    if (!PauseMenu.IsPaused) guard += Time.unscaledDeltaTime;
                    yield return null;
                }
                StopCinematic();
            }

            State = GameState.WaveActive;
            float clock = 0f;
            while (AliveBirds() > 0 || later.Count > 0)
            {
                if (ended) yield break;
                clock += Time.deltaTime;
                if (later.Count > 0)
                {
                    // Si ya no queda ninguna en el aire, el siguiente grupo no se hace esperar
                    if (AliveBirds() == 0 && later[0].delay - clock > 3f) clock = later[0].delay - 3f;
                    if (clock >= later[0].delay)
                    {
                        SurvivalWaves.Group group = later[0];
                        later.RemoveAt(0);
                        PendingGroups = later.Count;
                        float yaw = baseYaw + group.yawOffset;
                        SpawnGroup(group.birds, yaw, wave.healthMultiplier, wave.bossHealthMultiplier, "_" + (wave.groups.IndexOf(group) + 1));
                        AnnounceGroup(group, yaw);
                    }
                }
                yield return null;
            }
            PendingGroups = 0;
            if (ended) yield break;

            // ---- Oleada superada ----
            if (wave.IsBossWave && survivalFishPerBoss > 0 && FishLost > 0)
            {
                int back = Mathf.Min(survivalFishPerBoss, FishLost);
                FishLost -= back;
                AddSurvivalFish(back, fishLayer);
                Toast?.Invoke("¡LLEGAN " + back + " PECES NUEVOS AL LAGO!", Cyan);
            }
            bool record = SurvivalRun.RecordWave(n, wave.IsBossWave, FishLost, BirdsKilled, FishRescued);
            string news = record ? "¡NUEVO RÉCORD!  //  " : "";
            n++;

            // Cada 5 oleadas (SurvivalWaves.WorkshopEvery, después de cada jefe): al Taller (mejora y traje reparado).
            // Con todo mejorado igual se baja a repararlo. Entre medio, nada de Taller ni de curarse
            int every = SurvivalWaves.WorkshopEvery;
            int cleared = n - 1;
            bool workshop = cleared % every == 0 && Application.CanStreamedLevelBeLoaded(WorkshopFlow.WorkshopScene);
            if (workshop)
            {
                State = GameState.Intermission;
                Countdown = 0f;
                Banner?.Invoke("OLEADA " + cleared + " SUPERADA", news + "Al Taller: una mejora, traje reparado y un defensor más", Cyan);
                Sfx.Play2D(Sonido.PezRescatado, 0.8f, 0.85f);
                float wait = 3.5f;
                while (wait > 0f)
                {
                    if (ended) yield break;
                    wait -= Time.deltaTime;
                    yield return null;
                }
                if (ended || player == null || player.isDead) yield break;
                // Desde aquí ya no se pierde: se va al Taller (y vuelve con la oleada siguiente)
                ended = true;
                StopCinematic();
                WorkshopFlow.OpenSurvival();
                yield break;
            }

            // Sin Taller: la siguiente llega aquí mismo (el traje NO se repara), con los defensores que toquen
            int workshopAfter = cleared + (every - cleared % every);
            string next = workshopAfter == n
                ? "Después de la próxima: el Taller"
                : "Taller después de la oleada " + workshopAfter;
            bool hurt = player != null && player.currentArmor < player.maxArmor - 0.5f;
            Banner?.Invoke("OLEADA " + cleared + " SUPERADA",
                           news + next + (hurt ? "  //  El traje no se repara hasta entonces" : "  //  Recarga munición"), Cyan);
            Sfx.Play2D(Sonido.PezRescatado, 0.8f, 0.85f);
            int before = DefendersSpawned;
            AddDefenders(fishLayer, SurvivalWaves.DefendersFor(n, every));
            if (DefendersSpawned > before)
                Toast?.Invoke("¡LLEGAN " + (DefendersSpawned - before) + " PECES DEFENSORES!", Cyan);
            calm = survivalCalm;
            firstHere = false;
        }
    }

    // Peces nuevos que llegan al lago (premio por vencer a un jefe)
    void AddSurvivalFish(int count, int fishLayer)
    {
        if (fishPlan.Count == 0 || FishAI.SchoolAnchors == null || FishAI.SchoolAnchors.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            FishPlan entry = fishPlan[Random.Range(0, fishPlan.Count)];
            SpawnOneFish(entry.type, Random.Range(0, FishAI.SchoolAnchors.Length), 100 + i, fishLayer);
        }
    }

    // Aviso de un grupo que llega más tarde: por qué lado (respecto a hacia dónde mira el jugador)
    void AnnounceGroup(SurvivalWaves.Group group, float yaw)
    {
        string side = SideFromPlayer(yaw);
        bool boss = group.HasBoss;
        Toast?.Invoke(boss ? "¡OTRO JEFE LLEGA " + side + "!" : "¡OTRO GRUPO LLEGA " + side + "!  (" + group.birds.Count + " aves)", Danger);
        HelmetVisorHUD.ShowAlert((boss ? "OTRO JEFE" : "NUEVO GRUPO") + "  //  " + side, 3f);
        Sfx.Play2D(Sonido.Oleada, 0.55f, 1.15f);
    }

    string SideFromPlayer(float yaw)
    {
        Vector3 spawn = lake.Center + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * arrivalDistance;
        Transform view = null;
        if (player != null) view = player.PlayerCamera != null ? player.PlayerCamera.transform : player.transform;
        if (view == null) return "POR OTRO LADO";
        Vector3 to = spawn - view.position;
        to.y = 0f;
        Vector3 fwd = view.forward;
        fwd.y = 0f;
        if (to.sqrMagnitude < 1f || fwd.sqrMagnitude < 0.001f) return "POR OTRO LADO";
        float a = Vector3.SignedAngle(fwd, to, Vector3.up);
        if (Mathf.Abs(a) <= 45f) return "DE FRENTE";
        if (Mathf.Abs(a) >= 135f) return "POR DETRÁS";
        return a > 0f ? "POR LA DERECHA" : "POR LA IZQUIERDA";
    }
}
