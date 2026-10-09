using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mecánica principal:
//  - Peces nadando en cardúmenes por el lago.
//  - Oleadas de pájaros que llegan desde el cielo (con cinemática) a cazarlos.
//  - Cápsulas de munición repartidas por el lago.
//  - GANAS si eliminas a todos los pájaros de todas las oleadas.
//  - PIERDES si mueres o si cazan el 70% de los peces (configurable).
//
// Los modelos de peces y aves se configuran en "Tipos de peces" y
// "Tipos de aves enemigas" (ver ActorTypes.cs).
public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    public static bool InCinematic { get { return WaveCinematic.IsPlaying; } }

    public enum GameState { Calm, WaveIncoming, WaveActive, Intermission, Victory, Defeat }

    [System.Serializable]
    public class Wave
    {
        public int birds = 2;
        [Tooltip("Segundos de calma antes de esta oleada")]
        public float delayBefore = 12f;
        [Tooltip("Índices de 'Tipos de aves enemigas' que pueden salir en esta oleada. Vacío = cualquiera")]
        public int[] birdTypes = new int[0];
        [Tooltip("Multiplica la vida de las aves de esta oleada")]
        public float healthMultiplier = 1f;
    }

    [Header("Referencias")]
    public PlayerController_Base player;
    public LakeVolume lake;

    [Header("Nivel (archivo de datos)")]
    [Tooltip("Si asignas un Nivel (Create > Fish Out Of Water > Nivel), sus peces y oleadas " +
             "reemplazan a las listas 'Tipos de peces', 'Tipos de aves' y 'Oleadas' de abajo")]
    public LevelDefinition level;

    [Header("Tipos de peces (solo si no hay Nivel)")]
    public List<FishType> fishTypes = new List<FishType> { new FishType { name = "Pez común" } };
    public int schools = 3;
    [Tooltip("Pierdes si cazan este porcentaje de peces")]
    [Range(0.1f, 1f)] public float maxFishLossFraction = 0.7f;

    [Header("Tipos de aves enemigas (solo si no hay Nivel)")]
    public List<BirdType> birdTypes = new List<BirdType> { new BirdType { name = "Ave depredadora" } };

    [Header("Oleadas (solo si no hay Nivel)")]
    public List<Wave> waves = new List<Wave>
    {
        new Wave { birds = 2, delayBefore = 15f },
        new Wave { birds = 3, delayBefore = 12f },
        new Wave { birds = 4, delayBefore = 12f, healthMultiplier = 1.25f },
    };
    [Header("Llegada de las aves")]
    public float arrivalDistance = 120f;
    public float arrivalHeight = 45f;
    [Tooltip("Dirección (grados) desde la que llegan. -1 = aleatoria")]
    public float arrivalYaw = -1f;

    [Header("Cinemática de llegada")]
    public bool playArrivalCinematic = true;
    [Tooltip("Mostrar la cinemática solo en la primera oleada")]
    public bool cinematicOnlyFirstWave = false;
    public float cinematicDuration = 5f;

    [Header("Munición")]
    public int ammoPickups = 6;
    public int ammoPerPickup = 10;
    public float pickupRespawnTime = 12f;
    [Range(0f, 1f)] public float underwaterPickupChance = 0.4f;

    [Header("Efectos")]
    public Material fxMaterialTemplate;
    public GameObject splashPrefab;
    public GameObject birdHitPrefab;
    public GameObject birdDeathPrefab;
    public GameObject pickupCollectPrefab;

    [Header("Opciones")]
    [Tooltip("Desactiva los pájaros/peces colocados a mano en la escena (los reemplaza el director)")]
    public bool disablePreplacedActors = true;

    // ---- Estado ----
    public GameState State { get; private set; }
    public int CurrentWave { get; private set; }
    public int TotalWaves { get { return plan != null ? plan.Count : waves.Count; } }
    public int TotalFish { get; private set; }
    public int FishLost { get; private set; }
    public int FishAlive { get { return TotalFish - FishLost; } }
    public int MaxFishLoss { get { return Mathf.Max(1, Mathf.CeilToInt(TotalFish * FishLossFraction)); } }
    // Nivel que se está jugando (null si se usan las listas del Inspector)
    public LevelDefinition ActiveLevel { get { return usingLevel ? level : null; } }
    // Estrellas al ganar (1–3)
    public int Stars { get; private set; }
    // Porcentaje de peces cazados con el que se pierde (del Nivel o del Inspector)
    public float FishLossFraction { get { return usingLevel ? level.maxFishLossFraction : maxFishLossFraction; } }
    public int BirdsKilled { get; private set; }
    public float Countdown { get; private set; }
    public IReadOnlyList<AmmoPickup> Pickups { get { return pickups; } }

    // Mensajes para el HUD
    public event System.Action<string, string, Color> Banner;
    public event System.Action<string, Color> Toast;
    public event System.Action<bool> Letterbox;

    public static readonly Color Cyan = new Color(0.41f, 0.87f, 0.9f, 1f);
    public static readonly Color Danger = new Color(1f, 0.3f, 0.25f, 1f);

    readonly List<BirdAI> birds = new List<BirdAI>();
    readonly List<AmmoPickup> pickups = new List<AmmoPickup>();
    float anchorTimer;
    bool ended;

    // Cinemática en curso
    WaveCinematic cinematic;

    // ---- Plan de la partida (sale del Nivel o de las listas del Inspector) ----
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
    bool usingLevel;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();

        if (lake == null) lake = FindFirstObjectByType<LakeVolume>();
        if (lake == null) lake = gameObject.AddComponent<LakeVolume>();
        lake.Recalculate();

        if (!lake.IsValid)
        {
            Debug.LogError("GameDirector: no hay agua. Agrega un objeto con el componente LakeWater dentro del hueco del lago.");
            enabled = false;
            return;
        }

        if (disablePreplacedActors) DisablePreplaced();

        BuildPlan();

        if (GetComponent<GameHUD>() == null) gameObject.AddComponent<GameHUD>();

        SpawnFish();
        for (int i = 0; i < ammoPickups; i++) SpawnPickup();

        StartCoroutine(GameFlow());
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
        string calmTitle = usingLevel && !string.IsNullOrEmpty(level.displayName) ? level.displayName.ToUpper() : "EL LAGO ESTÁ EN CALMA";
        Banner?.Invoke(calmTitle, "Protege a los peces  //  Recoge cápsulas de munición en el agua", Cyan);

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
            if (showCinematic)
            {
                cinematic = WaveCinematic.Play(player, spawned, lake, cinematicDuration, on => Letterbox?.Invoke(on));
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
                Banner?.Invoke("OLEADA SUPERADA", "Recarga munición antes de que vuelvan", Cyan);
        }

        if (!ended) EndGame(true);
    }

    void Update()
    {
        if (ended) return;

        if (player != null && player.isDead)
        {
            // La pantalla de muerte la muestra PauseMenu
            ended = true;
            State = GameState.Defeat;
            StopCinematic();
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
        Stars = !victory ? 0 : usingLevel ? level.StarsFor(saved) : (saved >= 0.9f ? 3 : saved >= 0.6f ? 2 : 1);
        // (texto en vez de símbolos de estrella: la fuente del HUD podría no tenerlos)
        string stars = "ESTRELLAS " + Stars + "/3";

        string title = victory ? "¡LAGO A SALVO!" : "LOS PECES FUERON CAZADOS";
        string sub = victory
            ? stars + "   Salvaste " + FishAlive + " de " + TotalFish + " peces  //  Depredadores abatidos: " + BirdsKilled
            : "Cazaron " + FishLost + " de " + TotalFish + " peces";
        Banner?.Invoke(title, sub, victory ? Cyan : Danger);

        PauseMenu menu = FindFirstObjectByType<PauseMenu>();
        if (menu != null) menu.ShowEndScreen(victory, title, sub);
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

    // Convierte el Nivel (o las listas del Inspector) en la lista de peces y oleadas de esta partida
    void BuildPlan()
    {
        usingLevel = false;
        if (level != null)
        {
            usingLevel = true;
            BuildFromLevel();
            if (fishPlan.Count == 0 || plan.Count == 0)
            {
                Debug.LogWarning("GameDirector: el nivel '" + level.name + "' no tiene peces u oleadas válidos " +
                                 "(¿faltan tipos o cantidades?). Se usan las listas del Inspector.");
                usingLevel = false;
            }
        }
        if (!usingLevel) BuildFromInspector();

        if (plan.Count == 0) Debug.LogError("GameDirector: no hay oleadas configuradas.");
    }

    void BuildFromLevel()
    {
        fishPlan.Clear();
        foreach (FishSpawn f in level.fish)
        {
            if (f == null || f.type == null || f.type.fish == null) continue;
            int count = f.count > 0 ? f.count : f.type.fish.count;
            if (count > 0) fishPlan.Add(new FishPlan { type = f.type.fish, count = count });
        }
        schoolCount = Mathf.Max(1, level.schools);

        plan = new List<RuntimeWave>();
        foreach (WaveDefinition w in level.waves)
        {
            if (w == null) continue;
            RuntimeWave rw = new RuntimeWave { delay = w.delayBefore, healthMultiplier = w.healthMultiplier };
            foreach (BirdSpawn b in w.birds)
            {
                if (b == null || b.type == null || b.type.bird == null) continue;
                for (int i = 0; i < b.count; i++) rw.birds.Add(b.type.bird);
            }
            if (rw.birds.Count > 0) plan.Add(rw);
        }
    }

    void BuildFromInspector()
    {
        fishPlan.Clear();
        foreach (FishType t in UsableTypes(fishTypes, x => x.model != null))
            if (t.count > 0) fishPlan.Add(new FishPlan { type = t, count = t.count });
        if (fishPlan.Count == 0) fishPlan.Add(new FishPlan { type = new FishType(), count = 16 });
        schoolCount = Mathf.Max(1, schools);

        plan = new List<RuntimeWave>();
        foreach (Wave w in waves)
        {
            if (w == null) continue;
            RuntimeWave rw = new RuntimeWave { delay = w.delayBefore, healthMultiplier = w.healthMultiplier };
            for (int i = 0; i < w.birds; i++) rw.birds.Add(PickBirdType(w));
            if (rw.birds.Count > 0) plan.Add(rw);
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
            FishType type = entry.type;
            for (int n = 0; n < entry.count; n++)
            {
                int school = index % FishAI.SchoolAnchors.Length;
                index++;
                Vector3 pos;
                if (!lake.TryGetSwimPoint(FishAI.SchoolAnchors[school], 10f, 0.5f, 1.6f, 0.6f, out pos))
                    lake.TryGetRandomSwimPoint(0.5f, 1.6f, 0.6f, out pos);

                GameObject go = new GameObject("Pez_" + type.name + "_" + (n + 1));
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
                fish.Died += OnFishDied;
                fish.Rescued += OnFishRescued;
                TotalFish++;
            }
        }
    }

    // Devuelve los tipos que tienen modelo; si ninguno tiene, devuelve todos (se usan cápsulas de prueba)
    static List<T> UsableTypes<T>(List<T> all, System.Predicate<T> hasModel)
    {
        List<T> result = new List<T>();
        if (all == null) return result;
        foreach (T t in all) if (t != null && hasModel(t)) result.Add(t);
        if (result.Count == 0)
            foreach (T t in all) if (t != null) result.Add(t);
        return result;
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
        FishLost++;
        int left = MaxFishLoss - FishLost;
        if (!ended)
            Toast?.Invoke(left > 0 ? "UN PEZ FUE CAZADO  //  " + FishAlive + " a salvo" : "¡DEMASIADOS PECES PERDIDOS!", Danger);
    }

    void OnFishRescued(FishAI fish)
    {
        Toast?.Invoke("¡PEZ RESCATADO!", Cyan);
    }

    // ======================= PÁJAROS =======================

    List<BirdAI> SpawnWave(RuntimeWave wave)
    {
        List<BirdAI> list = new List<BirdAI>();
        float yaw = arrivalYaw >= 0f ? arrivalYaw : Random.Range(0f, 360f);
        Vector3 fromDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fromDir);
        Vector3 center = lake.Center;

        for (int i = 0; i < wave.birds.Count; i++)
        {
            // Formación en V
            int row = (i + 1) / 2;
            float side = i == 0 ? 0f : (i % 2 == 1 ? -1f : 1f);
            Vector3 offset = right * side * row * 7f + fromDir * row * 6f + Vector3.up * row * 2f;
            Vector3 spawn = center + fromDir * arrivalDistance + Vector3.up * arrivalHeight + offset;
            Vector3 arrive = center + Vector3.up * Random.Range(14f, 20f) + right * side * row * 6f;

            BirdType type = wave.birds[i];
            int health = Mathf.Max(1, Mathf.RoundToInt(type.health * Mathf.Max(0.1f, wave.healthMultiplier)));
            BirdAI bird = CreateBird("Ave_" + type.name + "_" + CurrentWave + "_" + (i + 1), spawn, type, health);
            bird.BeginArrival(arrive, lake, player);
            list.Add(bird);
            birds.Add(bird);
        }
        return list;
    }

    BirdType PickBirdType(Wave wave)
    {
        List<BirdType> usable = UsableTypes(birdTypes, t => t.model != null);
        if (usable.Count == 0) return new BirdType();

        // Filtra por los índices permitidos en esta oleada
        if (wave.birdTypes != null && wave.birdTypes.Length > 0)
        {
            List<BirdType> allowed = new List<BirdType>();
            foreach (int idx in wave.birdTypes)
            {
                if (idx < 0 || idx >= birdTypes.Count) continue;
                BirdType t = birdTypes[idx];
                if (t != null && usable.Contains(t)) allowed.Add(t);
            }
            if (allowed.Count > 0) usable = allowed;
        }
        return usable[Random.Range(0, usable.Count)];
    }

    BirdAI CreateBird(string name, Vector3 position, BirdType type, int healthValue)
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
        WaterInteractor bw = go.AddComponent<WaterInteractor>();
        bw.size = 1.5f;
        bw.wakeMinSpeed = 4f;
        bird.splashPrefab = splashPrefab;
        bird.hitPrefab = birdHitPrefab;
        bird.deathPrefab = birdDeathPrefab;
        bird.Died += OnBirdDied;
        bird.FishCaught += (b, f) => Toast?.Invoke("¡UN PÁJARO ATRAPÓ UN PEZ!  Dispárale para que lo suelte", Danger);
        return bird;
    }

    void OnBirdDied(BirdAI bird)
    {
        BirdsKilled++;
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

        GameObject go = new GameObject("CapsulaMunicion");
        go.transform.position = pos;
        AmmoPickup pickup = go.AddComponent<AmmoPickup>();
        pickup.ammo = ammoPerPickup;
        pickup.collectPrefab = pickupCollectPrefab;
        pickup.IsUnderwater = underwater;
        pickup.Build(fxMaterialTemplate);
        pickup.Collected += OnPickupCollected;
        pickups.Add(pickup);
    }

    void OnPickupCollected(AmmoPickup pickup)
    {
        pickups.Remove(pickup);
        Toast?.Invoke("+" + pickup.ammo + " MUNICIÓN", Cyan);
        StartCoroutine(RespawnPickup());
    }

    IEnumerator RespawnPickup()
    {
        yield return new WaitForSeconds(pickupRespawnTime);
        if (!ended) SpawnPickup();
    }

    // ======================= ESCENA =======================

    void DisablePreplaced()
    {
        foreach (EnemyBird old in FindObjectsByType<EnemyBird>(FindObjectsSortMode.None))
            old.gameObject.SetActive(false);

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Fish"))
        {
            if (go.GetComponentInParent<FishAI>() != null) continue;
            if (go.GetComponentInParent<PlayerController_Base>() != null) continue;
            go.SetActive(false);
        }
    }
}
