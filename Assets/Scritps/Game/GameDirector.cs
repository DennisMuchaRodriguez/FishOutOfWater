using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Mecánica principal de un nivel:
//  - Peces nadando en cardúmenes por el lago.
//  - Oleadas de pájaros que llegan desde el cielo (con cinemática) a cazarlos.
//  - Cápsulas de munición repartidas por el lago.
//  - GANAS si eliminas a todos los pájaros de todas las oleadas (1 a 3 estrellas).
//  - PIERDES si mueres o si cazan demasiados peces.
//
// TODO el contenido del nivel (peces, aves, oleadas, % de pérdida, cómics) sale del
// archivo de datos del Nivel (LevelDefinition). Desde el menú se juega el nivel elegido
// en el mapa; si das Play directo en esta escena se usa "Test Level".
// El modo SUPERVIVENCIA (oleadas sin fin) está en GameDirector.Survival.cs.
public partial class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    // Cinemática de oleada o créditos del final: el HUD se esconde y el jugador no se mueve ni dispara
    public static bool InCinematic { get { return WaveCinematic.IsPlaying || CreditsRoll.IsPlaying || LaunchCinematic.IsPlaying; } }

    public enum GameState { Calm, WaveIncoming, WaveActive, Intermission, Victory, Defeat }

    [Header("Referencias")]
    public PlayerController_Base player;
    public LakeVolume lake;

    [Header("Nivel de prueba")]
    [Tooltip("Nivel que se juega al dar Play directo en esta escena. " +
             "Si entras desde el menú se usa el nivel elegido en el mapa")]
    [FormerlySerializedAs("level")]
    public LevelDefinition testLevel;

    [Header("Llegada de las aves")]
    public float arrivalDistance = 120f;
    public float arrivalHeight = 45f;
    [Tooltip("Dirección (grados) desde la que llegan. -1 = aleatoria")]
    public float arrivalYaw = -1f;
    [Tooltip("Las oleadas grandes llegan en varias bandadas de este tamaño, cada una por un lado distinto")]
    public int flockSize = 8;

    [Header("Dificultad")]
    [Tooltip("Las oleadas de la HISTORIA traen esta cantidad de veces más aves que las del nivel (los jefes no se " +
             "multiplican; las aves extra son de las mismas especies de la oleada). 1 = como en el nivel")]
    public float birdCountMultiplier = 2.5f;
    [Tooltip("Aves extra en cada oleada de la Historia (además del multiplicador): así desde el nivel 1 hay muchas")]
    public int extraBirdsPerWave = 3;
    [Tooltip("Tope de aves en una oleada de la Historia (rendimiento)")]
    public int maxBirdsPerWave = 40;
    [Tooltip("Refuerzo de los PRIMEROS niveles: el nivel 1 suma estas aves en cada oleada, y el refuerzo baja de a poco " +
             "hasta 0 en el nivel 'Early Levels'")]
    public int earlyLevelExtraBirds = 10;
    [Tooltip("Nivel en el que el refuerzo de los primeros niveles llega a 0")]
    public int earlyLevels = 8;
    [Tooltip("Escala final de la cantidad de aves de la Historia (después de todo lo de arriba): 0,7 = 30 % menos")]
    [Range(0.3f, 1.5f)] public float finalBirdScale = 0.7f;

    [Header("Cooperativo")]
    [Tooltip("Al terminar cada oleada, los compañeros caídos vuelven a la pelea con esta parte de la armadura")]
    [Range(0.1f, 1f)] public float coopReviveArmor = 0.5f;
    [Tooltip("Con más aves hace falta más munición: cápsulas en el lago = Ammo Pickups x esto")]
    public float ammoPickupScale = 1.6f;
    [Tooltip("...y vuelven más rápido: Pickup Respawn Time x esto")]
    [Range(0.1f, 1f)] public float pickupRespawnScale = 0.65f;

    [Header("Cinemática de llegada")]
    public bool playArrivalCinematic = true;
    [Tooltip("Mostrar la cinemática solo en la primera oleada")]
    public bool cinematicOnlyFirstWave = false;
    [Tooltip("Segundos que dura (se reparte entre los tres planos)")]
    public float cinematicDuration = 6.5f;
    [Tooltip("Encuadre de cada plano. Con el GameDirector seleccionado se ven las tres cámaras en la escena; " +
             "el menú ⋮ del componente tiene \"Cinemática: ver plano ...\" para mirar por cada una")]
    public WaveCinematic.Settings cinematicShots = new WaveCinematic.Settings();

    [Header("Cinemática de inicio")]
    [Tooltip("Al empezar el nivel, el pez sale disparado del fondo del lago hacia el cielo con el propulsor " +
             "(Historia y Supervivencia de un jugador; Espacio o Enter la saltan)")]
    public bool playLaunchIntro = true;
    public LaunchCinematic.Settings launchIntro = new LaunchCinematic.Settings();

    [Header("Peces defensores")]
    [Tooltip("Nivel desde el que aparecen los peces defensores (con traje, disparan a las aves)")]
    public int defenderStartLevel = 10;
    [Tooltip("Cada cuántos niveles se suman más defensores")]
    public int defenderLevelStep = 3;
    [Tooltip("Defensores que se suman cada vez")]
    public int defendersPerStep = 2;
    [Tooltip("Para probar: si es mayor que 0, aparecen estos defensores en cualquier nivel")]
    public int testDefenders = 0;
    [Tooltip("Traje de los defensores (Assets/Models/Entorno/Traje_Defensor)")]
    public GameObject defenderSuitModel;
    [Tooltip("Colores de los defensores (distintos del naranja del protagonista)")]
    public Material[] defenderMaterials;

    [Header("Munición")]
    public int ammoPickups = 6;
    public int ammoPerPickup = 10;
    public float pickupRespawnTime = 12f;
    [Range(0f, 1f)] public float underwaterPickupChance = 0.4f;

    [Header("Efectos")]
    public Material fxMaterialTemplate;
    public GameObject birdHitPrefab;
    public GameObject birdDeathPrefab;
    public GameObject pickupCollectPrefab;

    // ---- Estado ----
    public GameState State { get; private set; }
    public int CurrentWave { get; private set; }
    // En Supervivencia no hay total (0 = sin fin)
    public int TotalWaves { get { return IsSurvival ? 0 : (plan != null ? plan.Count : 0); } }
    public int TotalFish { get; private set; }
    public int FishLost { get; private set; }
    public int FishAlive { get { return TotalFish - FishLost; } }
    public int MaxFishLoss { get { return Mathf.Max(1, Mathf.CeilToInt(TotalFish * FishLossFraction)); } }
    // Nivel que se está jugando y su número (1, 2, 3...)
    public LevelDefinition Level { get; private set; }
    public int LevelNumber { get { return GameSession.LevelIndex >= 0 ? GameSession.LevelIndex + 1 : (Level != null ? Level.number : 1); } }
    // Estrellas al ganar (1–3)
    public int Stars { get; private set; }
    // Porcentaje de peces cazados con el que se pierde
    public float FishLossFraction { get { return IsSurvival ? survivalLossShare : StoryLossFraction(Level); } }

    // Historia: margen extra sobre el porcentaje de cada nivel (hay que dejar cazar más peces para perder, así hay más
    // tiempo para defenderlos), sin pasar del 90 %. Lo usa también el mapa de niveles para el "Pierdes si cazan el X %".
    public const float StoryLossBonus = 0.1f;
    public const float MaxLossFraction = 0.9f;

    public static float StoryLossFraction(LevelDefinition level)
    {
        float f = level != null ? level.maxFishLossFraction : 0.7f;
        return Mathf.Min(MaxLossFraction, f + StoryLossBonus);
    }
    public int FishRescued { get; private set; }
    public int BirdsKilled { get; private set; }
    public float Countdown { get; private set; }
    public IReadOnlyList<AmmoPickup> Pickups { get { return pickups; } }
    // Peces defensores de este nivel: desde defenderStartLevel, defendersPerStep más cada defenderLevelStep niveles
    // (con los valores de fábrica: 2 en los niveles 10-12, 4 en 13-15, 6 en 16-18 y 8 en 19-20)
    public int DefenderCount
    {
        get
        {
            // Cooperativo en línea: sin peces defensores (solo los jugadores)
            if (IsCoop) return 0;
            if (testDefenders > 0) return testDefenders;
            if (IsSurvival) return SurvivalWaves.DefendersFor(Mathf.Max(1, CurrentWave > 0 ? CurrentWave : SurvivalRun.NextWave), SurvivalWaves.WorkshopEvery);
            if (LevelNumber < defenderStartLevel) return 0;
            return defendersPerStep * (1 + (LevelNumber - defenderStartLevel) / Mathf.Max(1, defenderLevelStep));
        }
    }
    public int DefendersSpawned { get; private set; }
    // Defensores que siguen con el traje puesto
    public int DefendersAlive
    {
        get
        {
            int n = 0;
            foreach (DefenderFish d in defenders)
                if (d != null && d.SuitIntact && d.Fish != null && d.Fish.IsAlive) n++;
            return n;
        }
    }
    readonly List<DefenderFish> defenders = new List<DefenderFish>();

    // Mensajes para el HUD
    public event System.Action<string, string, Color> Banner;
    public event System.Action<string, Color> Toast;
    public event System.Action<bool> Letterbox;

    public static readonly Color Cyan = new Color(0.25f, 0.72f, 1f, 1f);   // azul eléctrico del casco
    public static readonly Color Danger = new Color(1f, 0.3f, 0.25f, 1f);

    readonly List<BirdAI> birds = new List<BirdAI>();
    float currentHealthMultiplier = 1f;
    readonly List<AmmoPickup> pickups = new List<AmmoPickup>();
    float anchorTimer;
    bool ended;

    // Cinemática en curso
    WaveCinematic cinematic;

    // ---- Plan de la partida (sale del Nivel) ----
    class RuntimeWave
    {
        public float delay;
        public float healthMultiplier = 1f;
        public readonly List<BirdType> birds = new List<BirdType>();
    }

    struct FishPlan
    {
        public FishType type;
        public int count;
    }

    List<RuntimeWave> plan;
    readonly List<FishPlan> fishPlan = new List<FishPlan>();
    int schoolCount = 3;

    void Awake()
    {
        Instance = this;
        // Antes que cualquier Start (el ambiente y la pausa ya tienen que saber que es Supervivencia)
        PrepareTestSurvival();
    }

    void Start()
    {
        if (player == null) player = PlayerController_Base.FindLocal();

        if (lake == null) lake = FindFirstObjectByType<LakeVolume>();
        if (lake == null) lake = gameObject.AddComponent<LakeVolume>();
        lake.Recalculate();

        if (!lake.IsValid)
        {
            Debug.LogError("GameDirector: no hay agua. Agrega un objeto con el componente LakeWater dentro del hueco del lago.");
            enabled = false;
            return;
        }

        if (!ResolveLevel() || !(IsSurvival ? BuildSurvivalPlan() : BuildPlan()))
        {
            enabled = false;
            return;
        }

        if (GetComponent<GameHUD>() == null) gameObject.AddComponent<GameHUD>();

        // Cooperativo: el cliente no simula (arma lo que manda el anfitrión: CoopGame)
        SetupCoop();
        if (IsCoopClient)
        {
            StartCoopClient();
            return;
        }

        SpawnFish();
        if (IsSurvival) RestoreSurvivalTotals();
        int pickupCount = Mathf.Max(ammoPickups, Mathf.RoundToInt(ammoPickups * Mathf.Max(1f, ammoPickupScale)));
        for (int i = 0; i < pickupCount; i++) SpawnPickup();

        StartCoroutine(IsSurvival ? SurvivalFlow() : GameFlow());
    }

    void OnDisable()
    {
        // Pase lo que pase, nunca dejar la cámara en modo cinemática
        StopCinematic();
    }

    void StopCinematic()
    {
        if (cinematic != null) Destroy(cinematic.gameObject);
        cinematic = null;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ======================= FLUJO =======================

    IEnumerator GameFlow()
    {
        State = GameState.Calm;
        yield return null;
        // Mientras la pantalla de carga tapa el juego no empieza nada (el cartel del nivel sale al destaparse)
        while (LoadingScreen.IsLoading) yield return null;
        // El pez sale disparado del fondo del lago (cinemática de inicio)
        yield return LaunchIntro();
        string calmTitle = "NIVEL " + LevelNumber + (string.IsNullOrEmpty(Level.displayName) ? "" : "  ·  " + Level.displayName.ToUpper());
        string hora = Level.timeOfDay == LevelTime.Dia ? "" : LevelDefinition.TimeName(Level.timeOfDay) + "  //  ";
        Banner?.Invoke(calmTitle, hora + "Protege a los peces  //  Recoge cápsulas de munición en el agua", Cyan);
        if (DefendersSpawned > 0)
            Toast?.Invoke(DefendersSpawned + " PECES DEFENSORES PATRULLAN EL LAGO", Cyan);

        for (int i = 0; i < plan.Count; i++)
        {
            RuntimeWave wave = plan[i];
            State = i == 0 ? GameState.Calm : GameState.Intermission;
            Countdown = wave.delay;
            while (Countdown > 0f)
            {
                if (ended) yield break;
                Countdown -= Time.deltaTime;
                yield return null;
            }

            State = GameState.WaveIncoming;
            CurrentWave = i + 1;
            List<BirdAI> spawned = SpawnWave(wave);

            bool showCinematic = playArrivalCinematic && (!cinematicOnlyFirstWave || i == 0)
                                 && player != null && player.PlayerCamera != null && !player.isDead;
            Banner?.Invoke("OLEADA " + CurrentWave + " / " + TotalWaves, "¡DEPREDADORES EN CAMINO!", Danger);
            Play2DAll(Sonido.Oleada, 0.9f);
            if (showCinematic)
            {
                cinematic = WaveCinematic.Play(player, spawned, lake, cinematicDuration, on => Letterbox?.Invoke(on), cinematicShots);
                // Espera a que termine (con tope por si acaso)
                float guard = 0f;
                while (cinematic != null && guard < cinematicDuration + 8f)
                {
                    if (!PauseMenu.IsPaused) guard += Time.unscaledDeltaTime;
                    yield return null;
                }
                StopCinematic();
            }

            State = GameState.WaveActive;
            while (AliveBirds() > 0)
            {
                if (ended) yield break;
                yield return null;
            }

            if (i < plan.Count - 1)
            {
                Banner?.Invoke("OLEADA SUPERADA", "Recarga munición antes de que vuelvan", Cyan);
                Sfx.Play2D(Sonido.PezRescatado, 0.8f, 0.85f);
                ReviveFallenCoop();
            }
        }

        if (!ended) EndGame(true);
    }

    void Update()
    {
        if (ended || IsCoopClient) return;

        // Cooperativo: se pierde cuando caen todos (el que cae mira a los demás y vuelve al terminar la oleada)
        if (IsCoop)
        {
            if (coop != null && coop.AllPlayersDead)
            {
                EndGame(false);
                return;
            }
        }
        else if (player != null && player.isDead)
        {
            // La pantalla de muerte la muestra PauseMenu; suena el remate de la derrota
            ended = true;
            State = GameState.Defeat;
            StopCinematic();
            if (IsSurvival) OnSurvivalOver();
            Music.PlayEnd(false);
            return;
        }

        if (FishLost >= MaxFishLoss)
        {
            EndGame(false);
            return;
        }

        UpdateSchoolAnchors();
    }

    void EndGame(bool victory)
    {
        if (ended) return;
        ended = true;
        StopCinematic();
        State = victory ? GameState.Victory : GameState.Defeat;

        float saved = TotalFish > 0 ? (float)FishAlive / TotalFish : 0f;
        Stars = victory && !IsSurvival ? Level.StarsFor(saved) : 0;

        if (IsSurvival)
        {
            OnSurvivalOver();
            if (IsCoop)
            {
                bool allDown = coop != null && coop.AllPlayersDead;
                Banner?.Invoke(allDown ? "CAYÓ TODO EL EQUIPO" : "¡CAZARON DEMASIADOS PECES!",
                               "Llegaron a la oleada " + Mathf.Max(1, CurrentWave) + "  //  " + SurvivalRun.WavesCleared + " superadas", Danger);
                if (coop != null) coop.HostGameOver();
            }
            else
                Banner?.Invoke("¡CAZARON DEMASIADOS PECES!", "Llegaste a la oleada " + Mathf.Max(1, CurrentWave) +
                               "  //  Récord: " + SaveSystem.BestSurvivalWave + " oleadas", Danger);
            Music.PlayEnd(false);
            PauseMenu over = FindFirstObjectByType<PauseMenu>();
            if (over != null) over.ShowEndScreen(false);
            return;
        }

        // Progreso del modo Historia: guarda estrellas y desbloquea el siguiente nivel
        if (victory && GameSession.CurrentMode == GameSession.Mode.Historia && GameSession.LevelIndex >= 0)
            SaveSystem.RecordVictory(GameSession.LevelIndex, Stars);
        else
            SaveSystem.Save(); // aves derribadas del Archivo

        string title = victory ? "¡LAGO A SALVO!" : "LOS PECES FUERON CAZADOS";
        string sub = victory
            ? "Salvaste " + FishAlive + " de " + TotalFish + " peces  //  Depredadores abatidos: " + BirdsKilled
            : "Cazaron " + FishLost + " de " + TotalFish + " peces";
        Banner?.Invoke(title, sub, victory ? Cyan : Danger);
        Music.PlayEnd(victory);

        PauseMenu menu = FindFirstObjectByType<PauseMenu>();
        if (menu != null) menu.ShowEndScreen(victory);
    }

    public int AliveBirds()
    {
        int n = 0;
        foreach (BirdAI b in birds) if (b != null && b.IsAlive) n++;
        return n;
    }

    public IEnumerable<BirdAI> Birds
    {
        get { foreach (BirdAI b in birds) if (b != null && b.IsAlive) yield return b; }
    }

    // ======================= PLAN =======================

    // Elige el nivel: el del mapa (menú) o, si se dio Play directo en la escena, el de prueba
    bool ResolveLevel()
    {
        LevelCatalog catalog = GameSession.Catalog;
        if (ResolveSurvival(catalog)) return Level != null;
        Level = GameSession.CurrentLevel;
        if (Level == null)
        {
            Level = testLevel;
            if (Level == null && catalog != null) Level = catalog.Get(0);
            GameSession.CurrentMode = GameSession.Mode.Historia;
            GameSession.LevelIndex = catalog != null ? catalog.IndexOf(Level) : -1;
        }
        if (Level == null)
        {
            Debug.LogError("GameDirector: no hay nivel para jugar. Asigna 'Test Level' o revisa Assets/Resources/LevelCatalog.");
            return false;
        }
        return true;
    }

    // Convierte el Nivel en la lista de peces y oleadas de esta partida
    bool BuildPlan()
    {
        fishPlan.Clear();
        foreach (FishSpawn f in Level.fish)
        {
            if (f == null || f.type == null || f.type.fish == null || f.count <= 0) continue;
            fishPlan.Add(new FishPlan { type = f.type.fish, count = f.count });
        }
        schoolCount = Mathf.Max(1, Level.schools);

        plan = new List<RuntimeWave>();
        foreach (WaveDefinition w in Level.waves)
        {
            if (w == null) continue;
            RuntimeWave rw = new RuntimeWave { delay = w.delayBefore, healthMultiplier = w.healthMultiplier };
            foreach (BirdSpawn bs in w.birds)
            {
                if (bs == null || bs.type == null || bs.type.bird == null) continue;
                for (int i = 0; i < bs.count; i++) rw.birds.Add(bs.type.bird);
            }
            MultiplyWave(rw.birds);
            if (rw.birds.Count > 0) plan.Add(rw);
        }

        if (fishPlan.Count == 0 || plan.Count == 0)
        {
            Debug.LogError("GameDirector: el nivel '" + Level.name + "' no tiene peces u oleadas con aves. " +
                           "Revisa sus listas Fish y Waves (cada entrada necesita Type y Count).");
            return false;
        }
        return true;
    }

    // Dificultad: más aves en cada oleada de la Historia. Los jefes quedan igual; las aves extra repiten las especies
    // de la oleada en el mismo orden (la mezcla se mantiene)
    void MultiplyWave(List<BirdType> wave)
    {
        List<BirdType> normal = wave.FindAll(b => b != null && b.boss == BossKind.None);
        if (normal.Count == 0) return;
        int target = Mathf.RoundToInt(normal.Count * Mathf.Max(1f, birdCountMultiplier)) + Mathf.Max(0, extraBirdsPerWave)
                     + EarlyLevelBonus();
        target = Mathf.Max(normal.Count, Mathf.Min(target, Mathf.Max(1, maxBirdsPerWave)));
        target = Mathf.Max(normal.Count, Mathf.RoundToInt(target * Mathf.Clamp(finalBirdScale, 0.3f, 1.5f)));
        for (int k = 0; normal.Count + k < target; k++) wave.Add(normal[k % normal.Count]);
    }

    // Cooperativo: al terminar una oleada, los compañeros caídos vuelven a la pelea (con media armadura)
    void ReviveFallenCoop()
    {
        if (!IsCoop || IsCoopClient || coop == null) return;
        int n = coop.HostReviveFallen(coopReviveArmor);
        if (n > 0) Toast?.Invoke(n == 1 ? "¡UN COMPAÑERO VUELVE A LA PELEA!" : "¡" + n + " COMPAÑEROS VUELVEN A LA PELEA!", Cyan);
    }

    // Aves extra de los primeros niveles: el máximo en el nivel 1, bajando hasta 0 en el nivel 'earlyLevels'
    int EarlyLevelBonus()
    {
        if (earlyLevelExtraBirds <= 0 || earlyLevels <= 1) return 0;
        float k = 1f - (LevelNumber - 1) / (float)(earlyLevels - 1);
        return Mathf.Max(0, Mathf.RoundToInt(earlyLevelExtraBirds * Mathf.Clamp01(k)));
    }

    // Cinemática de inicio (Historia y Supervivencia de un jugador)
    IEnumerator LaunchIntro()
    {
        if (!playLaunchIntro || IsCoop || player == null || player.isDead || lake == null || !lake.IsValid) yield break;
        LaunchCinematic c = LaunchCinematic.Play(player, lake, launchIntro);
        float guard = 0f;
        while (c != null && !c.Done && guard < 15f)
        {
            if (!PauseMenu.IsPaused) guard += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ======================= PECES =======================

    void SpawnFish()
    {
        FishAI.SchoolAnchors = new Vector3[Mathf.Max(1, schoolCount)];
        for (int s = 0; s < FishAI.SchoolAnchors.Length; s++)
        {
            Vector3 p;
            lake.TryGetRandomSwimPoint(0.5f, 1.5f, 0.6f, out p);
            FishAI.SchoolAnchors[s] = p;
        }

        int fishLayer = LayerMask.NameToLayer("Fish");
        TotalFish = 0;
        int index = 0;
        foreach (FishPlan entry in fishPlan)
        {
            for (int n = 0; n < entry.count; n++)
            {
                SpawnOneFish(entry.type, index, n + 1, fishLayer, true);
                index++;
                TotalFish++;
            }
        }
        SpawnDefenders(fishLayer);
    }

    // Un pez normal en su cardumen (index reparte entre los cardúmenes)
    FishAI SpawnOneFish(FishType type, int index, int number, int fishLayer, bool hookEvents)
    {
        int school = index % FishAI.SchoolAnchors.Length;
        Vector3 pos;
        if (!lake.TryGetSwimPoint(FishAI.SchoolAnchors[school], 10f, 0.5f, 1.6f, 0.6f, out pos))
            lake.TryGetRandomSwimPoint(0.5f, 1.6f, 0.6f, out pos);

        GameObject go = new GameObject("Pez_" + type.name + "_" + number);
        go.tag = "Fish";
        if (fishLayer >= 0) go.layer = fishLayer;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        FishAI fish = go.AddComponent<FishAI>();
        go.GetComponent<SphereCollider>().radius = type.colliderRadius;
        fish.schoolId = school;
        fish.visual = CreateVisual(type.model, go.transform, type.modelScale, type.modelRotation, type.modelOffset,
                                   type.animatorController, type.materialOverride, fishLayer);
        fish.ApplyType(type);
        WaterInteractor wi = go.AddComponent<WaterInteractor>();
        wi.size = 0.8f;
        wi.minSpeed = 1f;
        if (hookEvents)
        {
            fish.Died += OnFishDied;
            fish.Rescued += OnFishRescued;
        }
        return fish;
    }

    // Peces defensores: de otro color, con traje gris y blanco; cada uno patrulla un sector del lago
    void SpawnDefenders(int fishLayer)
    {
        DefendersSpawned = 0;
        AddDefenders(fishLayer, DefenderCount);
    }

    // Agrega defensores hasta tener 'count' (en Supervivencia llegan más con las oleadas)
    void AddDefenders(int fishLayer, int count)
    {
        if (count <= DefendersSpawned || fishPlan.Count == 0) return;
        FishType type = fishPlan[0].type;

        for (int i = DefendersSpawned; i < count; i++)
        {
            float a = (i + 0.5f) / count * Mathf.PI * 2f;
            Vector3 guess = lake.Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * lake.Radius * 0.5f;
            Vector3 pos;
            if (!lake.TryGetSwimPoint(guess, 12f, 0.35f, 0.7f, 0.6f, out pos))
                lake.TryGetRandomSwimPoint(0.35f, 0.7f, 0.6f, out pos);
            // La mitad con pistola (más daño, más lenta) y la otra mitad con la metralleta de burbujas
            DefenderFish.Weapon gun = i % 2 == 0 ? DefenderFish.Weapon.Pistol : DefenderFish.Weapon.Bubbles;
            SpawnDefender(type, i, count, pos, fishLayer, gun, true);
        }
    }

    // Un pez defensor con su traje (hookEvents = false: marioneta del cooperativo, no cuenta aquí)
    DefenderFish SpawnDefender(FishType type, int i, int count, Vector3 pos, int fishLayer, DefenderFish.Weapon gun, bool hookEvents)
    {
        PlayerShooting shooting = player != null ? player.GetComponent<PlayerShooting>() : null;
        GameObject projectile = shooting != null ? shooting.projectilePrefab : null;
        GameObject impact = shooting != null ? shooting.impactEffectPrefab : null;
        float scale = type.modelScale / 28f;
        {
            GameObject go = new GameObject("PezDefensor_" + (i + 1));
            go.tag = "Fish";
            if (fishLayer >= 0) go.layer = fishLayer;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            FishAI fish = go.AddComponent<FishAI>();
            go.GetComponent<SphereCollider>().radius = type.colliderRadius;
            fish.schoolId = i;
            Material skin = defenderMaterials != null && defenderMaterials.Length > 0
                ? defenderMaterials[i % defenderMaterials.Length] : type.materialOverride;
            fish.visual = CreateVisual(type.model, go.transform, type.modelScale, type.modelRotation, type.modelOffset,
                                       type.animatorController, skin, fishLayer);
            fish.ApplyType(type);
            WaterInteractor wi = go.AddComponent<WaterInteractor>();
            wi.size = 0.8f;
            wi.minSpeed = 1f;

            DefenderFish defender = go.AddComponent<DefenderFish>();
            defender.Setup(defenderSuitModel, scale, projectile, impact, fxMaterialTemplate, i, count, gun, shooting);
            defenders.Add(defender);
            DefendersSpawned++;
            if (hookEvents)
            {
                defender.SuitBroken += OnDefenderSuitBroken;
                fish.Died += OnFishDied;
                fish.Rescued += OnFishRescued;
                // En Supervivencia los defensores no cuentan entre los peces del lago (llegan y se van con las oleadas)
                if (!IsSurvival) TotalFish++;
            }
            return defender;
        }
    }

    void OnDefenderSuitBroken(DefenderFish d)
    {
        if (ended) return;
        int left = DefendersAlive;
        string rest = left > 0 ? "QUEDAN " + left : "NO QUEDAN DEFENSORES";
        Toast?.Invoke("¡UN DEFENSOR PERDIÓ SU TRAJE!  //  " + rest, Danger);
        AlertAll("DEFENSOR DESTRUIDO  //  " + rest, 3.5f);
        Play2DAll(Sonido.Aviso, 0.6f, 0.9f);
    }

    void UpdateSchoolAnchors()
    {
        // Los cardúmenes se desplazan lentamente por el lago
        anchorTimer -= Time.deltaTime;
        if (anchorTimer > 0f || FishAI.SchoolAnchors == null) return;
        anchorTimer = Random.Range(8f, 14f);
        int s = Random.Range(0, FishAI.SchoolAnchors.Length);
        Vector3 p;
        if (lake.TryGetSwimPoint(FishAI.SchoolAnchors[s], 25f, 0.5f, 1.5f, 0.6f, out p))
            FishAI.SchoolAnchors[s] = p;
    }

    void OnFishDied(FishAI fish)
    {
        if (IsSurvival && fish != null && fish.Defender != null)
        {
            if (!ended) Toast?.Invoke("¡CAZARON A UN PEZ DEFENSOR!", Danger);
            return;
        }
        FishLost++;
        int left = MaxFishLoss - FishLost;
        if (!ended)
        {
            Toast?.Invoke(left > 0 ? "UN PEZ FUE CAZADO  //  " + FishAlive + " a salvo" : "¡DEMASIADOS PECES PERDIDOS!", Danger);
            Play2DAll(Sonido.Aviso, 0.7f, 0.75f);
        }
    }

    void OnFishRescued(FishAI fish)
    {
        FishRescued++;
        Toast?.Invoke("¡PEZ RESCATADO!", Cyan);
        Play2DAll(Sonido.PezRescatado, 0.8f);
    }

    // ======================= PÁJAROS =======================

    List<BirdAI> SpawnWave(RuntimeWave wave)
    {
        currentHealthMultiplier = wave.healthMultiplier;
        float yaw = arrivalYaw >= 0f ? arrivalYaw : Random.Range(0f, 360f);
        return SpawnGroup(wave.birds, yaw, wave.healthMultiplier, 1f, "");
    }

    // Una bandada en V que llega desde 'yaw' (grados). Los jefes usan su vida tal cual (ya está pensada para la
    // pelea) por bossHealthMultiplier (en Supervivencia sube en cada vuelta)
    List<BirdAI> SpawnGroup(List<BirdType> flock, float yaw, float healthMultiplier, float bossHealthMultiplier, string tag)
    {
        List<BirdAI> list = new List<BirdAI>();
        Vector3 center = lake.Center;
        int size = Mathf.Max(3, flockSize);

        for (int i = 0; i < flock.Count; i++)
        {
            // Una oleada grande llega en varias bandadas (de 'flockSize'), cada una por otro lado: 0°, +40°, -40°, +80°...
            int sub = i / size;
            int idx = i % size;
            float subYaw = yaw + (sub == 0 ? 0f : ((sub + 1) / 2) * 40f * (sub % 2 == 1 ? 1f : -1f));
            Vector3 fromDir = Quaternion.Euler(0f, subYaw, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fromDir);
            // Formación en V
            int row = (idx + 1) / 2;
            float side = idx == 0 ? 0f : (idx % 2 == 1 ? -1f : 1f);
            Vector3 offset = right * side * row * 7f + fromDir * row * 6f + Vector3.up * row * 2f;
            Vector3 spawn = center + fromDir * (arrivalDistance + sub * 12f) + Vector3.up * arrivalHeight + offset;
            Vector3 arrive = center + Vector3.up * Random.Range(14f, 20f) + right * side * row * 6f;

            BirdType type = flock[i];
            if (type == null) continue;
            float mult = type.boss != BossKind.None ? Mathf.Max(0.1f, bossHealthMultiplier) : Mathf.Max(0.1f, healthMultiplier);
            int health = Mathf.Max(1, Mathf.RoundToInt(type.health * mult));
            BirdAI bird = CreateBird("Ave_" + type.name + "_" + CurrentWave + tag + "_" + (i + 1), spawn, type, health);
            bird.BeginArrival(arrive, lake, CoopTarget(i));
            list.Add(bird);
            birds.Add(bird);
        }
        return list;
    }

    BirdAI CreateBird(string name, Vector3 position, BirdType type, int healthValue)
    {
        BirdAI bird = BuildBirdBody(name, position, type, healthValue, false);
        bird.Died += OnBirdDied;
        bird.FishCaught += (b, f) =>
        {
            // Un bocado del pelícano atrapa varios: un solo aviso
            if (b.CarriedCount <= 1)
            {
                Toast?.Invoke("¡UN PÁJARO ATRAPÓ UN PEZ!  Dispárale para que lo suelte", Danger);
                Play2DAll(Sonido.PezAtrapado, 0.8f);
            }
            else if (b.CarriedCount == 2) Toast?.Invoke("¡ATRAPÓ VARIOS PECES DE UN BOCADO!", Danger);
        };
        return bird;
    }

    // El ave con su modelo, colisionador y vida (puppet = marioneta del cooperativo: no piensa)
    BirdAI BuildBirdBody(string name, Vector3 position, BirdType type, int healthValue, bool puppet)
    {
        GameObject go = new GameObject(name);
        go.tag = "Enemy";
        go.transform.position = position;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f;
        SphereCollider col = go.AddComponent<SphereCollider>();
        col.radius = type.colliderRadius;
        col.center = type.colliderCenter;

        Damageable dmg = go.AddComponent<Damageable>();
        dmg.destroyOnDeath = false;
        dmg.SetMaxHealth(healthValue);

        BirdAI bird = go.AddComponent<BirdAI>();
        bird.visual = CreateVisual(type.model, go.transform, type.modelScale, type.modelRotation, type.modelOffset,
                                   type.animatorController, type.materialOverride, go.layer);
        bird.ApplyType(type);
        if (puppet) bird.MakePuppet();
        // Archivo de aves: la primera vez que aparece una especie queda registrada (es de cada jugador)
        if (SaveSystem.MarkBirdSeen(type.name))
            LocalToast("NUEVA AVE EN EL ARCHIVO:  " + type.name.ToUpperInvariant(), Cyan);
        WaterInteractor bw = go.AddComponent<WaterInteractor>();
        bw.size = 1.5f;
        bw.wakeMinSpeed = 4f;
        bird.hitPrefab = birdHitPrefab;
        bird.deathPrefab = birdDeathPrefab;
        return bird;
    }

    // Aviso corto en el HUD desde otros scripts (jefes, habilidades)
    public void ShowToast(string text, Color color)
    {
        Toast?.Invoke(text, color);
    }

    // Refuerzos de un jefe (Reina Águila): llegan desde lo alto cerca de 'near' y cuentan para ganar
    public void SpawnReinforcements(BirdType type, int count, Vector3 near)
    {
        if (type == null || count <= 0 || lake == null || State == GameState.Victory || State == GameState.Defeat) return;
        for (int i = 0; i < count; i++)
        {
            float a = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 spawn = near + dir * 25f + Vector3.up * 18f;
            Vector3 arrive = lake.Center + dir * lake.Radius * 0.4f + Vector3.up * Random.Range(12f, 18f);
            int health = Mathf.Max(1, Mathf.RoundToInt(type.health * Mathf.Max(0.1f, currentHealthMultiplier)));
            BirdAI bird = CreateBird("Refuerzo_" + type.name + "_" + (i + 1), spawn, type, health);
            bird.BeginArrival(arrive, lake, CoopTarget(i));
            birds.Add(bird);
        }
        Toast?.Invoke("¡LLEGAN REFUERZOS!", Danger);
    }

    void OnBirdDied(BirdAI bird)
    {
        BirdsKilled++;
        if (bird != null && bird.Type != null) SaveSystem.AddBirdKill(bird.Type.name);
        int left = AliveBirds();
        Toast?.Invoke(left > 0 ? "DEPREDADOR ABATIDO  //  quedan " + left : "DEPREDADOR ABATIDO", Cyan);
    }

    Transform CreateVisual(GameObject model, Transform parent, float scale, Vector3 euler, Vector3 offset,
                           RuntimeAnimatorController controller, Material materialOverride, int layer)
    {
        // "Visual" es un contenedor con la rotación/escala del Inspector; el modelo va dentro.
        // Así una animación que mueva la raíz del modelo nunca pisa esa rotación ni la escala.
        GameObject v = new GameObject("Visual");
        v.transform.SetParent(parent, false);
        GameObject inst;
        if (model != null)
        {
            inst = Instantiate(model, v.transform, false);
        }
        else
        {
            // Sin modelo asignado: cápsula de prueba para que el juego siga funcionando
            inst = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            inst.transform.SetParent(v.transform, false);
            scale = 1f;
            euler = new Vector3(90f, 0f, 0f);
        }
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        v.transform.localPosition = offset;
        v.transform.localRotation = Quaternion.Euler(euler);
        v.transform.localScale = Vector3.one * scale;

        foreach (Collider c in v.GetComponentsInChildren<Collider>()) Destroy(c);

        if (materialOverride != null)
        {
            foreach (Renderer r in v.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = materialOverride;
                r.sharedMaterials = mats;
            }
        }
        if (layer >= 0)
            foreach (Transform t in v.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        if (controller != null)
        {
            Animator anim = v.GetComponentInChildren<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
        }
        return v.transform;
    }

    // ======================= CINEMÁTICA =======================
    // La cinemática de llegada vive en WaveCinematic.cs: no usa cámaras propias, solo le pasa una
    // pose a la cámara del jugador (PlayerController_Base.LateUpdate), que la mezcla con su vista.

    // ======================= MUNICIÓN =======================

    void SpawnPickup()
    {
        bool underwater = Random.value < underwaterPickupChance;
        Vector3 pos;
        if (!lake.TryGetRandomSwimPoint(underwater ? 1.2f : 0.1f, underwater ? 3f : 0.1f, 0.8f, out pos)) return;
        if (!underwater) pos.y = lake.SurfaceY + 0.5f;

        AmmoPickup pickup = BuildPickup(pos, underwater);
        pickup.Collected += OnPickupCollected;
        pickups.Add(pickup);
    }

    AmmoPickup BuildPickup(Vector3 pos, bool underwater)
    {
        GameObject go = new GameObject("CapsulaMunicion");
        go.transform.position = pos;
        AmmoPickup pickup = go.AddComponent<AmmoPickup>();
        pickup.ammo = ammoPerPickup;
        pickup.collectPrefab = pickupCollectPrefab;
        pickup.IsUnderwater = underwater;
        pickup.Build(fxMaterialTemplate);
        return pickup;
    }

    void OnPickupCollected(AmmoPickup pickup)
    {
        pickups.Remove(pickup);
        LocalToast("¡MUNICIÓN LLENA!", Cyan);
        StartCoroutine(RespawnPickup());
    }

    IEnumerator RespawnPickup()
    {
        yield return new WaitForSeconds(pickupRespawnTime * Mathf.Clamp(pickupRespawnScale, 0.1f, 1f));
        if (!ended) SpawnPickup();
    }

#if UNITY_EDITOR
    // ======================= VISTA PREVIA DE LA CINEMÁTICA (SOLO EDITOR) =======================
    // Con el GameDirector seleccionado se dibujan las cámaras de los tres planos (la línea va del inicio al final
    // de cada plano y la pirámide es el encuadre a mitad del plano). La bandada se dibuja donde suele estar en ese
    // momento: viniendo desde arrivalYaw (si es aleatoria, desde el norte), a medio camino hacia el lago.

    WaveCinematic.Anchor PreviewAnchor()
    {
        LakeVolume l = lake != null ? lake : FindFirstObjectByType<LakeVolume>();
        Vector3 center = transform.position;
        if (l != null) center = l.water != null && l.water.HasData ? l.water.Centroid : l.transform.position;
        Vector3 p = player != null ? player.transform.position : center;
        float yaw = arrivalYaw >= 0f ? arrivalYaw : 0f;
        Vector3 fromDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 flock = center + fromDir * arrivalDistance * 0.65f + Vector3.up * arrivalHeight * 0.8f;
        Vector3 to = flock - p;
        to.y = 0f;
        Vector3 fly = center + Vector3.up * 17f - flock;
        return new WaveCinematic.Anchor
        {
            player = p,
            toFlock = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward,
            flock = flock,
            flockDir = fly.sqrMagnitude > 0.01f ? fly.normalized : -fromDir,
            flockRadius = 9f,
            sideSign = 1f,
            lake = l
        };
    }

    static readonly Color[] ShotGizmoColors =
    {
        new Color(0.3f, 0.9f, 1f, 1f), new Color(1f, 0.8f, 0.25f, 1f), new Color(1f, 0.4f, 0.8f, 1f)
    };

    void OnDrawGizmosSelected()
    {
        if (!playArrivalCinematic || Application.isPlaying) return;
        WaveCinematic.Anchor a = PreviewAnchor();

        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(a.flock, a.flockRadius);
        Gizmos.DrawLine(a.flock, a.flock + a.flockDir * 25f);
        UnityEditor.Handles.Label(a.flock + Vector3.up * (a.flockRadius + 1f), "Bandada (ejemplo)");

        for (int i = 0; i < WaveCinematic.ShotCount; i++)
        {
            Vector3 p0, p1, pos;
            Quaternion r0, r1, rot;
            float f0, f1, fov;
            WaveCinematic.Evaluate(cinematicShots, i, 0f, a, out p0, out r0, out f0);
            WaveCinematic.Evaluate(cinematicShots, i, 1f, a, out p1, out r1, out f1);
            WaveCinematic.Evaluate(cinematicShots, i, 0.5f, a, out pos, out rot, out fov);

            Gizmos.color = ShotGizmoColors[i];
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawSphere(p0, 0.25f);
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, fov, 8f, 0.3f, 16f / 9f);
            Gizmos.matrix = Matrix4x4.identity;
            UnityEditor.Handles.color = ShotGizmoColors[i];
            UnityEditor.Handles.Label(pos + Vector3.up * 0.7f, (i + 1) + " · " + WaveCinematic.ShotNames[i]);
        }
    }

    [ContextMenu("Cinemática: ver plano 1 (general)")]
    void ViewShot1() { ViewShot(0); }

    [ContextMenu("Cinemática: ver plano 2 (bandada)")]
    void ViewShot2() { ViewShot(1); }

    [ContextMenu("Cinemática: ver plano 3 (sobre el hombro)")]
    void ViewShot3() { ViewShot(2); }

    [ContextMenu("Cinemática: devolver la vista de escena normal")]
    void ViewShotReset()
    {
        UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
        if (view == null) return;
        UnityEditor.SceneView.CameraSettings cam = view.cameraSettings;
        cam.fieldOfView = 60f;
        view.cameraSettings = cam;
        view.Repaint();
    }

    // Pone la vista de escena justo donde estaría la cámara a mitad de ese plano (con su mismo zoom)
    void ViewShot(int shot)
    {
        UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
        if (view == null)
        {
            Debug.Log("Cinemática: abre la ventana Scene para ver el plano.");
            return;
        }
        Vector3 pos;
        Quaternion rot;
        float fov;
        WaveCinematic.Evaluate(cinematicShots, shot, 0.5f, PreviewAnchor(), out pos, out rot, out fov);
        UnityEditor.SceneView.CameraSettings cam = view.cameraSettings;
        cam.fieldOfView = fov;
        view.cameraSettings = cam;
        GameObject temp = new GameObject("PlanoCinematica");
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.SetPositionAndRotation(pos, rot);
        view.AlignViewToObject(temp.transform);
        DestroyImmediate(temp);
        view.Repaint();
    }
#endif
}
