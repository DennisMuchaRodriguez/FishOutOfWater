using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    public static bool InCinematic { get; private set; }

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

    [Header("Tipos de peces (pon aquí tus modelos)")]
    public List<FishType> fishTypes = new List<FishType> { new FishType { name = "Pez común" } };
    public int schools = 3;
    [Tooltip("Pierdes si cazan este porcentaje de peces")]
    [Range(0.1f, 1f)] public float maxFishLossFraction = 0.7f;

    [Header("Tipos de aves enemigas (pon aquí tus modelos)")]
    public List<BirdType> birdTypes = new List<BirdType> { new BirdType { name = "Ave depredadora" } };

    [Header("Oleadas")]
    public List<Wave> waves = new List<Wave>
    {
        new Wave { birds = 2, delayBefore = 15f },
        new Wave { birds = 3, delayBefore = 12f },
        new Wave { birds = 4, delayBefore = 12f, healthMultiplier = 1.25f },
    };
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

    // Cinemática
    Camera cinematicCamera;
    bool cinematicActive;
    float cinematicDeadline;

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
        lake.Recalculate();

        if (!lake.IsValid)
        {
            Debug.LogError("GameDirector: no hay agua. Agrega un objeto con el componente LakeWater dentro del hueco del lago.");
            enabled = false;
            return;
        }

        if (disablePreplacedActors) DisablePreplaced();

        if (GetComponent<GameHUD>() == null) gameObject.AddComponent<GameHUD>();

        SpawnFish();
        for (int i = 0; i < ammoPickups; i++) SpawnPickup();

        StartCoroutine(GameFlow());
    }

    void OnDisable()
    {
        // Pase lo que pase, nunca dejar la cámara en modo cinemática
        EndCinematic();
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

            bool showCinematic = playArrivalCinematic && (!cinematicOnlyFirstWave || i == 0)
                                 && player != null && player.PlayerCamera != null && !player.isDead;
            if (showCinematic)
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
        // Seguro: si la cinemática se colgó por cualquier motivo, se termina sola
        if (cinematicActive && Time.time > cinematicDeadline) EndCinematic();

        if (ended) return;

        if (player != null && player.isDead)
        {
            // La pantalla de muerte la muestra PauseMenu
            ended = true;
            State = GameState.Defeat;
            EndCinematic();
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
        EndCinematic();
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

        List<FishType> types = UsableTypes(fishTypes, t => t.model != null);
        if (types.Count == 0) types.Add(new FishType());

        int fishLayer = LayerMask.NameToLayer("Fish");
        TotalFish = 0;
        int index = 0;
        foreach (FishType type in types)
        {
            for (int n = 0; n < type.count; n++)
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
                                           type.animatorController, fishLayer);
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

            BirdType type = PickBirdType(wave);
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
                                   type.animatorController, go.layer);
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
                           RuntimeAnimatorController controller, int layer)
    {
        GameObject v;
        if (model != null)
        {
            v = Instantiate(model, parent);
        }
        else
        {
            // Sin modelo asignado: cápsula de prueba para que el juego siga funcionando
            v = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            v.transform.SetParent(parent, false);
            scale = 1f;
            euler = new Vector3(90f, 0f, 0f);
        }
        v.name = "Visual";
        v.transform.localPosition = offset;
        v.transform.localRotation = Quaternion.Euler(euler);
        v.transform.localScale = Vector3.one * scale;

        foreach (Collider c in v.GetComponentsInChildren<Collider>()) Destroy(c);
        if (layer >= 0)
            foreach (Transform t in v.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        if (controller != null)
        {
            Animator anim = v.GetComponentInChildren<Animator>();
            if (anim == null) anim = v.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
        }
        return v.transform;
    }

    // ======================= CINEMÁTICA =======================
    // Usa una cámara propia y temporal: la cámara del jugador nunca se toca,
    // así que al terminar (o si algo falla) siempre vuelve a la vista normal.

    IEnumerator ArrivalCinematic(List<BirdAI> wave)
    {
        BeginCinematic();
        try
        {
            if (cinematicCamera == null) yield break;
            Camera playerCam = player.PlayerCamera;
            Transform camT = cinematicCamera.transform;
            Vector3 startPos = camT.position;
            Quaternion startRot = camT.rotation;
            float startFov = cinematicCamera.fieldOfView;

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
                if (cinematicCamera == null) yield break;
                t += Time.deltaTime;
                flock = FlockCenter(wave);
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.9f));
                Vector3 dolly = shotPos + toFlock * t * 0.4f;
                Vector3 pos = Vector3.Lerp(startPos, dolly, blend);
                Vector3 lookDir = flock - pos;
                Quaternion look = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir.normalized, Vector3.up) : camT.rotation;
                camT.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, look, blend));

                // Zoom que encuadra a la bandada (más cerrado cuando están lejos)
                float dist = lookDir.magnitude;
                float frameFov = Mathf.Clamp(2f * Mathf.Atan(14f / Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 22f, 60f);
                cinematicCamera.fieldOfView = Mathf.Lerp(startFov, frameFov, blend);

                // Se puede saltar
                if (t > 0.6f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
                    break;
                yield return null;
            }

            // Regreso suave a la vista real del jugador (su cámara se sigue actualizando por debajo)
            Vector3 fromPos = camT.position;
            Quaternion fromRot = camT.rotation;
            float fromFov = cinematicCamera.fieldOfView;
            float back = 0f;
            while (back < 0.7f)
            {
                if (cinematicCamera == null || playerCam == null) yield break;
                back += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, back / 0.7f);
                camT.SetPositionAndRotation(Vector3.Lerp(fromPos, playerCam.transform.position, k),
                                            Quaternion.Slerp(fromRot, playerCam.transform.rotation, k));
                cinematicCamera.fieldOfView = Mathf.Lerp(fromFov, playerCam.fieldOfView, k);
                yield return null;
            }
        }
        finally
        {
            EndCinematic();
        }
    }

    void BeginCinematic()
    {
        if (cinematicActive) return;
        Camera playerCam = player.PlayerCamera;

        cinematicActive = true;
        InCinematic = true;
        cinematicDeadline = Time.time + cinematicDuration + 3f;
        player.SetCinematicLock(true);

        GameObject go = new GameObject("CamaraCinematica");
        go.tag = "MainCamera";
        cinematicCamera = go.AddComponent<Camera>();
        cinematicCamera.CopyFrom(playerCam); // copia ajustes y posición
        cinematicCamera.depth = playerCam.depth + 1f;
        var src = playerCam.GetUniversalAdditionalCameraData();
        var dst = cinematicCamera.GetUniversalAdditionalCameraData();
        dst.renderPostProcessing = src.renderPostProcessing;
        dst.antialiasing = src.antialiasing;
        dst.renderShadows = src.renderShadows;
        dst.volumeLayerMask = src.volumeLayerMask;
        playerCam.enabled = false;

        Letterbox?.Invoke(true);
        Banner?.Invoke("OLEADA " + CurrentWave + " / " + TotalWaves, "¡DEPREDADORES EN CAMINO!", Danger);
    }

    // Se puede llamar varias veces sin problema
    void EndCinematic()
    {
        if (!cinematicActive) return;
        cinematicActive = false;

        if (player != null)
        {
            if (player.PlayerCamera != null) player.PlayerCamera.enabled = true;
            player.SetCinematicLock(false);
        }
        if (cinematicCamera != null) Destroy(cinematicCamera.gameObject);
        cinematicCamera = null;

        Letterbox?.Invoke(false);
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
