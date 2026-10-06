using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mecánica principal:
//  - Peces nadando en cardúmenes por el lago.
//  - Oleadas de pájaros que llegan desde el cielo (con cinemática) a cazarlos.
//  - Cápsulas de munición repartidas por el lago.
//  - GANAS si eliminas a todos los pájaros de todas las oleadas.
//  - PIERDES si mueres o si cazan el 70% de los peces (configurable).
public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    public static bool InCinematic { get; private set; }

    public enum GameState { Calm, WaveIncoming, WaveActive, Intermission, Victory, Defeat }

    [System.Serializable]
    public class Wave
    {
        public int birds = 2;
        [Tooltip("Segundos de calma antes de esta oleada")]
        public float delayBefore = 12f;
        public int birdHealth = 40;
    }

    [Header("Referencias")]
    public PlayerController_Base player;
    public LakeVolume lake;

    [Header("Peces")]
    public int fishCount = 16;
    public GameObject fishModel;
    public float fishModelScale = 28f;
    public Vector3 fishModelEuler = new Vector3(90f, 0f, 0f);
    public float fishColliderRadius = 0.7f;
    public int schools = 3;
    [Tooltip("Pierdes si cazan este porcentaje de peces")]
    [Range(0.1f, 1f)] public float maxFishLossFraction = 0.7f;

    [Header("Pájaros")]
    public GameObject birdModel;
    public float birdModelScale = 1f;
    public Vector3 birdModelEuler = Vector3.zero;
    public float birdColliderRadius = 1.3f;
    public List<Wave> waves = new List<Wave>
    {
        new Wave { birds = 2, delayBefore = 15f, birdHealth = 40 },
        new Wave { birds = 3, delayBefore = 12f, birdHealth = 40 },
        new Wave { birds = 4, delayBefore = 12f, birdHealth = 50 },
    };
    public float arrivalDistance = 120f;
    public float arrivalHeight = 45f;
    [Tooltip("Dirección (grados) desde la que llegan. -1 = aleatoria")]
    public float arrivalYaw = -1f;

    [Header("Cinemática de llegada")]
    public bool playArrivalCinematic = true;
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
    public int TotalWaves { get { return waves.Count; } }
    public int TotalFish { get; private set; }
    public int FishLost { get; private set; }
    public int FishAlive { get { return TotalFish - FishLost; } }
    public int MaxFishLoss { get { return Mathf.Max(1, Mathf.CeilToInt(TotalFish * maxFishLossFraction)); } }
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

    void Awake()
    {
        Instance = this;
        InCinematic = false;
    }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();

        if (lake == null) lake = FindFirstObjectByType<LakeVolume>();
        if (lake == null) lake = gameObject.AddComponent<LakeVolume>();
        if (player != null) lake.surfaceOffset = player.waterSurfaceOffset;
        if (lake.waterTrigger == null) lake.waterTrigger = LakeVolume.FindWaterTrigger();
        lake.Recalculate();

        if (lake.waterTrigger == null)
        {
            Debug.LogError("GameDirector: no hay ningún trigger con tag 'Water'. El lago no puede funcionar.");
            enabled = false;
            return;
        }

        if (disablePreplacedActors) DisablePreplaced();

        if (GetComponent<GameHUD>() == null) gameObject.AddComponent<GameHUD>();

        SpawnFish();
        for (int i = 0; i < ammoPickups; i++) SpawnPickup();

        StartCoroutine(GameFlow());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        InCinematic = false;
    }

    // ======================= FLUJO =======================

    IEnumerator GameFlow()
    {
        State = GameState.Calm;
        yield return null;
        Banner?.Invoke("EL LAGO ESTÁ EN CALMA", "Protege a los peces  //  Recoge cápsulas de munición en el agua", Cyan);

        for (int i = 0; i < waves.Count; i++)
        {
            Wave wave = waves[i];
            State = i == 0 ? GameState.Calm : GameState.Intermission;
            Countdown = wave.delayBefore;
            while (Countdown > 0f)
            {
                if (ended) yield break;
                Countdown -= Time.deltaTime;
                yield return null;
            }

            State = GameState.WaveIncoming;
            CurrentWave = i + 1;
            List<BirdAI> spawned = SpawnWave(wave);

            if (playArrivalCinematic && player != null && player.PlayerCamera != null && !player.isDead)
                yield return ArrivalCinematic(spawned);
            else
                Banner?.Invoke("OLEADA " + CurrentWave + " / " + TotalWaves, "¡DEPREDADORES EN CAMINO!", Danger);

            State = GameState.WaveActive;
            while (AliveBirds() > 0)
            {
                if (ended) yield break;
                yield return null;
            }

            if (i < waves.Count - 1)
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
        State = victory ? GameState.Victory : GameState.Defeat;

        string title = victory ? "¡LAGO A SALVO!" : "LOS PECES FUERON CAZADOS";
        string sub = victory
            ? "Salvaste " + FishAlive + " de " + TotalFish + " peces  //  Depredadores abatidos: " + BirdsKilled
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

    // ======================= PECES =======================

    void SpawnFish()
    {
        FishAI.SchoolAnchors = new Vector3[Mathf.Max(1, schools)];
        for (int s = 0; s < FishAI.SchoolAnchors.Length; s++)
        {
            Vector3 p;
            lake.TryGetRandomSwimPoint(0.5f, 1.5f, 0.6f, out p);
            FishAI.SchoolAnchors[s] = p;
        }

        int fishLayer = LayerMask.NameToLayer("Fish");
        TotalFish = 0;
        for (int i = 0; i < fishCount; i++)
        {
            int school = i % FishAI.SchoolAnchors.Length;
            Vector3 pos;
            if (!lake.TryGetSwimPoint(FishAI.SchoolAnchors[school], 10f, 0.5f, 1.6f, 0.6f, out pos))
                lake.TryGetRandomSwimPoint(0.5f, 1.6f, 0.6f, out pos);

            GameObject go = new GameObject("Pez_" + (i + 1));
            go.tag = "Fish";
            if (fishLayer >= 0) go.layer = fishLayer;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            FishAI fish = go.AddComponent<FishAI>();
            go.GetComponent<SphereCollider>().radius = fishColliderRadius;
            fish.schoolId = school;
            fish.visual = CreateVisual(fishModel, go.transform, fishModelScale, fishModelEuler, PrimitiveType.Capsule, fishLayer);
            fish.Died += OnFishDied;
            fish.Rescued += OnFishRescued;
            TotalFish++;
        }
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

    List<BirdAI> SpawnWave(Wave wave)
    {
        List<BirdAI> list = new List<BirdAI>();
        float yaw = arrivalYaw >= 0f ? arrivalYaw : Random.Range(0f, 360f);
        Vector3 fromDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fromDir);
        Vector3 center = lake.Center;

        for (int i = 0; i < wave.birds; i++)
        {
            // Formación en V
            int row = (i + 1) / 2;
            float side = i == 0 ? 0f : (i % 2 == 1 ? -1f : 1f);
            Vector3 offset = right * side * row * 7f + fromDir * row * 6f + Vector3.up * row * 2f;
            Vector3 spawn = center + fromDir * arrivalDistance + Vector3.up * arrivalHeight + offset;
            Vector3 arrive = center + Vector3.up * Random.Range(14f, 20f) + right * side * row * 6f;

            BirdAI bird = CreateBird("Pájaro_" + CurrentWave + "_" + (i + 1), spawn, wave.birdHealth);
            bird.BeginArrival(arrive, lake, player);
            list.Add(bird);
            birds.Add(bird);
        }
        return list;
    }

    BirdAI CreateBird(string name, Vector3 position, int healthValue)
    {
        GameObject go = new GameObject(name);
        go.tag = "Enemy";
        go.transform.position = position;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f;
        SphereCollider col = go.AddComponent<SphereCollider>();
        col.radius = birdColliderRadius;
        col.center = new Vector3(0f, 0.3f, 0f);

        Damageable dmg = go.AddComponent<Damageable>();
        dmg.destroyOnDeath = false;
        dmg.SetMaxHealth(healthValue);

        BirdAI bird = go.AddComponent<BirdAI>();
        bird.visual = CreateVisual(birdModel, go.transform, birdModelScale, birdModelEuler, PrimitiveType.Capsule, go.layer);
        bird.SetVisualScale(birdModelScale);
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

    Transform CreateVisual(GameObject model, Transform parent, float scale, Vector3 euler, PrimitiveType fallback, int layer)
    {
        GameObject v;
        if (model != null)
        {
            v = Instantiate(model, parent);
        }
        else
        {
            v = GameObject.CreatePrimitive(fallback);
            v.transform.SetParent(parent, false);
        }
        v.name = "Visual";
        v.transform.localPosition = Vector3.zero;
        v.transform.localRotation = Quaternion.Euler(euler);
        v.transform.localScale = Vector3.one * scale;

        foreach (Collider c in v.GetComponentsInChildren<Collider>()) Destroy(c);
        if (layer >= 0)
            foreach (Transform t in v.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        return v.transform;
    }

    // ======================= CINEMÁTICA =======================

    IEnumerator ArrivalCinematic(List<BirdAI> wave)
    {
        InCinematic = true;
        player.SetCinematicLock(true);
        Letterbox?.Invoke(true);
        Banner?.Invoke("OLEADA " + CurrentWave + " / " + TotalWaves, "¡DEPREDADORES EN CAMINO!", Danger);

        Camera cam = player.PlayerCamera;
        Transform camT = cam.transform;
        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;
        float startFov = cam.fieldOfView;

        Vector3 flock = FlockCenter(wave);
        Vector3 toFlock = flock - player.transform.position;
        toFlock.y = 0f;
        toFlock = toFlock.sqrMagnitude > 0.01f ? toFlock.normalized : player.transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toFlock);
        // Plano por encima del hombro, mirando al cielo por donde llegan
        Vector3 shotPos = player.transform.position - toFlock * 5f + side * 2.5f + Vector3.up * 3.5f;
        shotPos.y = Mathf.Max(shotPos.y, lake.SurfaceY + 1.5f, lake.GroundHeight(shotPos.x, shotPos.z) + 1.5f);

        float t = 0f;
        while (t < cinematicDuration)
        {
            t += Time.deltaTime;
            flock = FlockCenter(wave);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.9f));
            Vector3 dolly = shotPos + toFlock * t * 0.4f;
            Vector3 pos = Vector3.Lerp(startPos, dolly, blend);
            Quaternion look = Quaternion.LookRotation((flock - pos).normalized, Vector3.up);
            camT.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, look, blend));
            // Zoom que encuadra a la bandada (más cerrado cuando están lejos)
            float dist = Vector3.Distance(pos, flock);
            float frameFov = Mathf.Clamp(2f * Mathf.Atan(14f / Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 22f, 60f);
            cam.fieldOfView = Mathf.Lerp(startFov, frameFov, blend);

            // Se puede saltar
            if (t > 0.6f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
                break;
            yield return null;
        }

        // Regreso suave a la vista del jugador
        Vector3 fromPos = camT.position;
        Quaternion fromRot = camT.rotation;
        float fromFov = cam.fieldOfView;
        float back = 0f;
        while (back < 0.7f)
        {
            back += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, back / 0.7f);
            Vector3 p;
            Quaternion r;
            player.GetDefaultCameraPose(out p, out r);
            camT.SetPositionAndRotation(Vector3.Lerp(fromPos, p, k), Quaternion.Slerp(fromRot, r, k));
            cam.fieldOfView = Mathf.Lerp(fromFov, startFov, k);
            yield return null;
        }

        Letterbox?.Invoke(false);
        player.SetCinematicLock(false);
        InCinematic = false;
    }

    Vector3 FlockCenter(List<BirdAI> wave)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (BirdAI b in wave)
        {
            if (b == null) continue;
            sum += b.transform.position;
            n++;
        }
        return n > 0 ? sum / n : lake.Center + Vector3.up * 20f;
    }

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
