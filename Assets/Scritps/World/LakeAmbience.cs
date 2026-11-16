using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// ============================================================================
//  AMBIENTE DEL LAGO
//  - Cielo estilizado (Resources/Ambiente/Cielo_Lago, shader "FishOutOfWater/Cielo Estilizado"):
//    degradado, sol con halo y nubes que se mueven con el viento.
//  - Niebla de distancia del color del horizonte (da profundidad: lo lejano se aclara y se azula).
//  - Luz ambiente en degradado (cielo / horizonte / suelo) y tono cálido del sol.
//  - Viento con ráfagas: mueve las plantas (DecorationSway), las nubes y las hojas que caen.
//  - Bajo el agua: niebla densa del color del agua (se ve un poco y lo lejano se pierde), más oscura en lo hondo.
//  - Efectos ambientales (AmbientParticles): hojas, polen y niebla baja sobre el agua; bajo el agua, partículas
//    en suspensión y rayos de luz.
//  Se agrega solo al darle Play en las escenas con LakeWater. Para volver al cielo anterior,
//  desmarca "Use Stylized Sky" (o agrega este componente a mano a la escena y ajústalo).
// ============================================================================
[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)]
public class LakeAmbience : MonoBehaviour
{
    public const string SkyPath = "Ambiente/Cielo_Lago";
    public static LakeAmbience Instance { get; private set; }

    // Viento actual: 0 = calma, 1 = ráfaga fuerte (lo leen las hojas y otros efectos)
    public static float Wind01 { get; private set; } = 0.4f;
    public static Vector3 WindDirection { get; private set; } = new Vector3(0.94f, 0f, 0.34f);

    [Header("Cielo")]
    [Tooltip("Usa el cielo estilizado del juego. Desmárcalo para dejar el cielo que tenga la escena")]
    public bool useStylizedSky = true;
    [Tooltip("Material del cielo. Vacío = Resources/Ambiente/Cielo_Lago")]
    public Material skyMaterial;

    [Header("Niebla de distancia")]
    public bool fog = true;
    [Tooltip("Toma el color del horizonte del cielo (recomendado: así el fondo se funde con el cielo)")]
    public bool fogFromSky = true;
    public Color fogColor = new Color(0.74f, 0.85f, 0.93f, 1f);
    [Tooltip("Densidad (exponencial al cuadrado): cerca casi no se nota y lo lejano se funde con el cielo")]
    [Range(0f, 0.01f)] public float fogDensity = 0.0026f;

    [Header("Luz ambiente")]
    public bool setAmbient = true;
    public Color ambientSky = new Color(0.56f, 0.69f, 0.86f, 1f);
    public Color ambientEquator = new Color(0.55f, 0.6f, 0.57f, 1f);
    public Color ambientGround = new Color(0.3f, 0.29f, 0.23f, 1f);

    [Header("Sol")]
    [Tooltip("Da al sol un tono cálido de media mañana (no cambia su intensidad ni su dirección)")]
    public bool tintSun = true;
    public Color sunColor = new Color(1f, 0.95f, 0.86f, 1f);

    [Header("Viento")]
    [Tooltip("Hacia dónde sopla (solo cuenta x y z)")]
    public Vector3 windDirection = new Vector3(0.94f, 0f, 0.34f);
    [Tooltip("Fuerza base del viento en las plantas")]
    [Range(0f, 3f)] public float windStrength = 1f;
    [Tooltip("Cuánto cambian las ráfagas")]
    [Range(0f, 1f)] public float gustStrength = 0.6f;
    [Tooltip("Segundos (aprox.) entre una ráfaga y otra")]
    public float gustPeriod = 9f;
    [Tooltip("Cuánto arrastra el viento a las nubes")]
    public float cloudDrift = 0.01f;

    [Header("Bajo el agua")]
    [Tooltip("Con la cámara bajo el agua, niebla densa del color del agua: se ve cerca y lo lejano se pierde")]
    public bool underwaterFog = true;
    [Tooltip("Color del agua cerca de la superficie (antes del filtro azul del Underwater Volume)")]
    public Color waterFogShallow = new Color(0.16f, 0.46f, 0.52f, 1f);
    [Tooltip("Color del agua en lo hondo")]
    public Color waterFogDeep = new Color(0.05f, 0.2f, 0.3f, 1f);
    [Tooltip("A cuántos metros bajo la superficie el agua ya tiene el color de lo hondo")]
    public float waterDarkDepth = 8f;
    [Tooltip("Densidad (exponencial al cuadrado). 0.06: a 10 m se ve un 25 % borroso, a 20 m un 70 %, a 25 m casi nada")]
    [Range(0.01f, 0.2f)] public float waterFogDensity = 0.06f;
    [Tooltip("Rapidez con la que vuelve la niebla de aire al sacar la cámara del agua")]
    public float waterFogFadeOut = 6f;

    [Header("Efectos")]
    [Tooltip("Hojas que caen de los árboles, polen en el aire y niebla baja sobre el agua")]
    public bool ambientEffects = true;

    DecorationSway sway;
    float swayTimer;
    // Niebla de aire (la que deja Apply) y mezcla con la del agua
    bool airFog;
    FogMode airFogMode = FogMode.ExponentialSquared;
    float airFogDensity = 0.0026f;
    Color airFogColor = new Color(0.68f, 0.8f, 0.9f, 1f);
    float under01;
    Camera cam;
    float camTimer;
    LakeVolume lake;
    Vector2 cloudOffset;
    static readonly int WindOffsetId = Shader.PropertyToID("_FOW_WindOffset");

    // ======================= SE AGREGA SOLO =======================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        Wind01 = 0.4f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetup()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAutoCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoCreate(scene);
    }

    static void TryAutoCreate(Scene scene)
    {
        if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded) return;
        bool hasLake = false;
        foreach (LakeWater lw in LakeWater.All)
        {
            if (lw != null && lw.gameObject.scene == scene) { hasLake = true; break; }
        }
        if (!hasLake) return;
        if (FindFirstObjectByType<LakeAmbience>(FindObjectsInactive.Include) != null) return;
        GameObject go = new GameObject("AmbienteLago");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<LakeAmbience>();
    }

    // ======================= CICLO DE VIDA =======================

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Apply();
        if (ambientEffects && GetComponent<AmbientParticles>() == null) gameObject.AddComponent<AmbientParticles>();
    }

    // Aplica cielo, niebla, luz ambiente y sol (también se puede llamar desde el Inspector)
    [ContextMenu("Aplicar ambiente")]
    public void Apply()
    {
        Material sky = null;
        if (useStylizedSky)
        {
            sky = skyMaterial != null ? skyMaterial : Resources.Load<Material>(SkyPath);
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                DynamicGI.UpdateEnvironment();
            }
            else Debug.LogWarning("LakeAmbience: falta Resources/" + SkyPath + "; se deja el cielo de la escena.");
        }

        if (fog)
        {
            Color c = fogColor;
            if (fogFromSky && sky != null && sky.HasProperty("_HorizonColor"))
            {
                // El horizonte, un poco hacia el color de abajo (no tan blanco)
                Color h = sky.GetColor("_HorizonColor");
                Color g = sky.HasProperty("_GroundColor") ? sky.GetColor("_GroundColor") : h;
                c = Color.Lerp(h, g, 0.2f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = c;
        }

        if (setAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
        }

        if (tintSun)
        {
            Light sun = FindSun();
            if (sun != null) sun.color = sunColor;
        }

        // La niebla de aire queda guardada para volver a ella al salir del agua
        airFog = RenderSettings.fog;
        airFogMode = RenderSettings.fogMode;
        airFogDensity = RenderSettings.fogDensity;
        airFogColor = RenderSettings.fogColor;
        under01 = 0f;
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        Light best = null;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional || !l.isActiveAndEnabled) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }

    // ======================= VIENTO =======================

    void Update()
    {
        float t = Time.time;
        float dt = Time.deltaTime;

        // Ráfagas: ruido lento + picos cortos de vez en cuando
        float slow = Mathf.PerlinNoise(t / Mathf.Max(1f, gustPeriod), 0.37f);
        float fast = Mathf.PerlinNoise(t / Mathf.Max(0.5f, gustPeriod * 0.25f), 4.1f);
        float gust = Mathf.Clamp01((slow - 0.3f) * 1.6f) * 0.7f + Mathf.Clamp01((fast - 0.55f) * 2.5f) * 0.3f;
        Wind01 = Mathf.Clamp01(0.3f + gust * gustStrength * 1.2f);

        Vector3 dir = new Vector3(windDirection.x, 0f, windDirection.z);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
        dir.Normalize();
        // La dirección gira un poco con el tiempo (no sopla siempre igual)
        float wobble = (Mathf.PerlinNoise(t * 0.02f, 9.3f) - 0.5f) * 40f;
        WindDirection = Quaternion.Euler(0f, wobble, 0f) * dir;

        // Plantas: la fuerza del viento de la decoración sigue a las ráfagas
        swayTimer -= dt;
        if (sway == null && swayTimer <= 0f)
        {
            swayTimer = 1f;
            if (LakeDecorator.Instance != null) sway = LakeDecorator.Instance.GetComponent<DecorationSway>();
        }
        if (sway != null) sway.wind = windStrength * Mathf.Lerp(0.55f, 1.7f, Wind01);

        // Nubes: se corren más rápido con las ráfagas
        cloudOffset += new Vector2(WindDirection.x, WindDirection.z) * cloudDrift * Mathf.Lerp(0.4f, 1.6f, Wind01) * dt;
        Shader.SetGlobalVector(WindOffsetId, new Vector4(cloudOffset.x, cloudOffset.y, 0f, 0f));

        UpdateWaterFog(dt);
    }

    // ======================= BAJO EL AGUA =======================

    // Igual que el filtro submarino: se decide por la posición de la cámara (sirve en tercera persona y en la
    // cinemática). Al entrar es inmediato; al salir se desvanece en un instante
    void UpdateWaterFog(float dt)
    {
        if (!underwaterFog) return;
        camTimer -= dt;
        if (cam == null || camTimer <= 0f)
        {
            cam = Camera.main;
            camTimer = 1f;
            if (lake == null) lake = LakeVolume.Instance;
        }
        if (cam == null || lake == null) return;

        Vector3 cp = cam.transform.position;
        float depth = lake.SurfaceY - cp.y;
        bool under = depth > 0f && lake.IsInsideXZ(cp, 0f);
        float before = under01;
        under01 = under ? 1f : Mathf.MoveTowards(under01, 0f, dt * Mathf.Max(0.5f, waterFogFadeOut));
        if (under01 <= 0f)
        {
            if (before > 0f) RestoreAirFog();
            return;
        }

        Color water = Color.Lerp(waterFogShallow, waterFogDeep, Mathf.Clamp01(depth / Mathf.Max(0.5f, waterDarkDepth)));
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        float airD = airFog ? airFogDensity : 0f;
        RenderSettings.fogDensity = Mathf.Lerp(airD, waterFogDensity, under01);
        RenderSettings.fogColor = Color.Lerp(airFogColor, water, under01);
    }

    void RestoreAirFog()
    {
        RenderSettings.fog = airFog;
        RenderSettings.fogMode = airFogMode;
        RenderSettings.fogDensity = airFogDensity;
        RenderSettings.fogColor = airFogColor;
    }

    // Lo usan los efectos (partículas en suspensión, rayos de luz)
    public static bool CameraUnderwater { get { return Instance != null && Instance.under01 >= 1f; } }
}
