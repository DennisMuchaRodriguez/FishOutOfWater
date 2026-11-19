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
//  - Ciclo de día, tarde y noche (en Supervivencia; en la historia si se marca "Day Cycle In Story"): el sol cruza
//    el cielo y se pone, el cielo pasa por la tarde dorada, el atardecer, el anochecer azul y una noche con luna y
//    estrellas que NO es muy oscura (Night Brightness), y vuelve a amanecer. También cambian la luz, la luz
//    ambiente, la niebla y el tono del agua. En Supervivencia la hora sigue donde quedó al volver del Taller.
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

    [Header("Ciclo de día, tarde y noche")]
    [Tooltip("En Supervivencia el cielo pasa por día, tarde, atardecer, noche y amanecer")]
    public bool dayCycleInSurvival = true;
    [Tooltip("También en el modo Historia (si no, la historia queda siempre de día, como antes)")]
    public bool dayCycleInStory = false;
    [Tooltip("Minutos de juego que dura un día completo")]
    [Min(1f)] public float dayLengthMinutes = 10f;
    [Tooltip("Qué tan clara es la noche: 0,55 = noche azul con luna en la que se ve bien; 1 = casi como el día")]
    [Range(0.2f, 1f)] public float nightBrightness = 0.55f;
    [Tooltip("Hora con la que empieza la historia (si tiene ciclo)")]
    [Range(0f, 1f)] public float storyStartTime = 0.2f;
    [Tooltip("Para probar: fija la hora (0 amanecer · 0,25 mediodía · 0,47 tarde · 0,55 atardecer · 0,8 noche). " +
             "-1 = la hora avanza sola")]
    [Range(-1f, 1f)] public float previewTimeOfDay = -1f;

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
    static readonly int SkySunId = Shader.PropertyToID("_FOW_SkySunDir");
    static readonly int StarsId = Shader.PropertyToID("_FOW_Stars");
    static readonly int SkyTintId = Shader.PropertyToID("_FOW_SkyTint");

    // ---- Ciclo de día y noche ----
    // Hora del día: 0 amanecer, 0,25 mediodía, 0,47 tarde, 0,55 atardecer, 0,62 anochecer, 0,8 noche
    public static float TimeOfDay { get; private set; } = 0.25f;
    public static bool CycleActive { get { return Instance != null && Instance.cycleOn; } }
    const float DayEnd = 0.58f;      // el sol se pone; de aquí a 1 manda la luna
    const float SunPeak = 62f;       // altura máxima del sol (grados)
    const float MoonPeak = 48f;
    bool cycleOn;
    float timeOfDay;
    Material skyInstance;
    Light sunLight;
    float baseSunIntensity = 1f;
    float sunAzimuth;
    Color waterTint = Color.white;
    int lastPhase = -1;
    struct Key { public float t; public Look look; }
    Key[] keys;

    // Cómo se ve el lago a una hora
    struct Look
    {
        public Color top, mid, horizon, ground, disk, glow, cloudLit, cloudShadow, light, ambSky, ambEq, ambGround, water;
        public float glowStrength, glowPower, horizonGlow, diskIntensity, diskSize, lightIntensity, stars;

        public static Look Lerp(Look a, Look b, float k)
        {
            Look r;
            r.top = Color.Lerp(a.top, b.top, k);
            r.mid = Color.Lerp(a.mid, b.mid, k);
            r.horizon = Color.Lerp(a.horizon, b.horizon, k);
            r.ground = Color.Lerp(a.ground, b.ground, k);
            r.disk = Color.Lerp(a.disk, b.disk, k);
            r.glow = Color.Lerp(a.glow, b.glow, k);
            r.cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, k);
            r.cloudShadow = Color.Lerp(a.cloudShadow, b.cloudShadow, k);
            r.light = Color.Lerp(a.light, b.light, k);
            r.ambSky = Color.Lerp(a.ambSky, b.ambSky, k);
            r.ambEq = Color.Lerp(a.ambEq, b.ambEq, k);
            r.ambGround = Color.Lerp(a.ambGround, b.ambGround, k);
            r.water = Color.Lerp(a.water, b.water, k);
            r.glowStrength = Mathf.Lerp(a.glowStrength, b.glowStrength, k);
            r.glowPower = Mathf.Lerp(a.glowPower, b.glowPower, k);
            r.horizonGlow = Mathf.Lerp(a.horizonGlow, b.horizonGlow, k);
            r.diskIntensity = Mathf.Lerp(a.diskIntensity, b.diskIntensity, k);
            r.diskSize = Mathf.Lerp(a.diskSize, b.diskSize, k);
            r.lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, k);
            r.stars = Mathf.Lerp(a.stars, b.stars, k);
            return r;
        }
    }

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
        // Que el ciclo no quede aplicado en otras escenas
        if (cycleOn) ResetCycleGlobals();
        if (skyInstance != null) Destroy(skyInstance);
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

        SetupCycle(sky);
    }

    // ======================= CICLO DE DÍA, TARDE Y NOCHE =======================

    void SetupCycle(Material sky)
    {
        bool want = SurvivalRun.InSurvival ? dayCycleInSurvival : dayCycleInStory;
        if (!Application.isPlaying || !want || sky == null || !sky.HasProperty("_TopColor"))
        {
            if (cycleOn || !want) ResetCycleGlobals();
            cycleOn = false;
            return;
        }

        // Una copia del cielo: así el material del proyecto no se toca
        if (skyInstance != null) Destroy(skyInstance);
        skyInstance = new Material(sky) { name = sky.name + " (ciclo)" };
        RenderSettings.skybox = skyInstance;

        sunLight = FindSun();
        if (sunLight != null)
        {
            baseSunIntensity = Mathf.Max(0.1f, sunLight.intensity);
            Vector3 toSun = -sunLight.transform.forward;
            sunAzimuth = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg;
        }
        BuildKeys(sky);

        cycleOn = true;
        timeOfDay = SurvivalRun.InSurvival ? SurvivalRun.TimeOfDay : storyStartTime;
        lastPhase = PhaseIndex(timeOfDay);
        ApplyCycle(timeOfDay);
    }

    void ResetCycleGlobals()
    {
        Shader.SetGlobalVector(SkySunId, Vector4.zero);
        Shader.SetGlobalFloat(StarsId, 0f);
        Shader.SetGlobalVector(SkyTintId, Vector4.zero);
    }

    // Las horas clave. El día sale del material del cielo y del Inspector (lo de siempre); el resto, a mano
    void BuildKeys(Material sky)
    {
        Look day;
        day.top = sky.GetColor("_TopColor");
        day.mid = sky.GetColor("_MidColor");
        day.horizon = sky.GetColor("_HorizonColor");
        day.ground = sky.GetColor("_GroundColor");
        day.disk = sky.GetColor("_SunColor");
        day.glow = sky.GetColor("_GlowColor");
        day.cloudLit = sky.GetColor("_CloudLit");
        day.cloudShadow = sky.GetColor("_CloudShadow");
        day.glowStrength = sky.GetFloat("_GlowStrength");
        day.glowPower = sky.GetFloat("_GlowPower");
        day.horizonGlow = sky.GetFloat("_HorizonGlow");
        day.diskIntensity = sky.GetFloat("_SunIntensity");
        day.diskSize = sky.GetFloat("_SunSize");
        day.light = tintSun ? sunColor : (sunLight != null ? sunLight.color : Color.white);
        day.lightIntensity = 1f;
        day.ambSky = ambientSky;
        day.ambEq = ambientEquator;
        day.ambGround = ambientGround;
        day.water = Color.white;
        day.stars = 0f;

        // Tarde: luz dorada
        Look afternoon = day;
        afternoon.top = new Color(0.24f, 0.42f, 0.8f);
        afternoon.mid = new Color(0.55f, 0.62f, 0.86f);
        afternoon.horizon = new Color(0.98f, 0.8f, 0.58f);
        afternoon.ground = new Color(0.55f, 0.52f, 0.5f);
        afternoon.disk = new Color(1f, 0.85f, 0.6f);
        afternoon.glow = new Color(1f, 0.68f, 0.38f);
        afternoon.glowStrength = 0.6f;
        afternoon.horizonGlow = 0.55f;
        afternoon.cloudLit = new Color(1f, 0.9f, 0.75f);
        afternoon.cloudShadow = new Color(0.56f, 0.53f, 0.68f);
        afternoon.light = new Color(1f, 0.82f, 0.6f);
        afternoon.lightIntensity = 0.9f;
        afternoon.ambSky = new Color(0.6f, 0.62f, 0.75f);
        afternoon.ambEq = new Color(0.68f, 0.6f, 0.5f);
        afternoon.ambGround = new Color(0.32f, 0.27f, 0.2f);
        afternoon.water = new Color(1f, 0.95f, 0.88f);

        // Atardecer: naranja y rosa
        Look sunset = afternoon;
        sunset.top = new Color(0.2f, 0.25f, 0.52f);
        sunset.mid = new Color(0.56f, 0.43f, 0.62f);
        sunset.horizon = new Color(1f, 0.56f, 0.33f);
        sunset.ground = new Color(0.42f, 0.33f, 0.36f);
        sunset.disk = new Color(1f, 0.62f, 0.32f);
        sunset.glow = new Color(1f, 0.46f, 0.22f);
        sunset.glowStrength = 1f;
        sunset.horizonGlow = 1.2f;
        sunset.cloudLit = new Color(1f, 0.67f, 0.5f);
        sunset.cloudShadow = new Color(0.43f, 0.33f, 0.52f);
        sunset.light = new Color(1f, 0.58f, 0.36f);
        sunset.lightIntensity = 0.65f;
        sunset.ambSky = new Color(0.5f, 0.44f, 0.62f);
        sunset.ambEq = new Color(0.64f, 0.47f, 0.42f);
        sunset.ambGround = new Color(0.26f, 0.21f, 0.21f);
        sunset.water = new Color(0.96f, 0.8f, 0.74f);

        // Noche: azul con luna y estrellas, sin quedar a oscuras (Night Brightness)
        float n = Mathf.Clamp(nightBrightness / 0.55f, 0.35f, 1.8f);
        Look night;
        night.top = new Color(0.05f, 0.09f, 0.22f);
        night.mid = new Color(0.09f, 0.15f, 0.32f);
        night.horizon = new Color(0.2f, 0.28f, 0.45f);
        night.ground = new Color(0.1f, 0.13f, 0.2f);
        night.disk = new Color(0.92f, 0.95f, 1f);          // la luna
        night.glow = new Color(0.45f, 0.56f, 0.92f);
        night.glowStrength = 0.32f;
        night.glowPower = 20f;
        night.horizonGlow = 0.1f;
        night.diskIntensity = 1.5f;
        night.diskSize = 0.027f;
        night.cloudLit = new Color(0.4f, 0.46f, 0.64f);
        night.cloudShadow = new Color(0.15f, 0.19f, 0.31f);
        night.light = new Color(0.62f, 0.72f, 1f);
        night.lightIntensity = 0.42f * n;
        night.ambSky = new Color(0.3f, 0.37f, 0.58f) * n;
        night.ambEq = new Color(0.24f, 0.29f, 0.42f) * n;
        night.ambGround = new Color(0.12f, 0.13f, 0.17f) * n;
        night.water = Color.Lerp(new Color(0.42f, 0.5f, 0.72f), Color.white, Mathf.Clamp01(n - 1f));
        night.stars = 1f;

        // Anochecer (y antes de amanecer): la hora azul
        Look dusk = Look.Lerp(sunset, night, 0.6f);
        dusk.disk = night.disk;
        dusk.diskSize = night.diskSize;
        dusk.diskIntensity = night.diskIntensity;
        dusk.stars = 0.35f;

        // Amanecer
        Look dawn = sunset;
        dawn.top = new Color(0.22f, 0.32f, 0.62f);
        dawn.mid = new Color(0.58f, 0.55f, 0.72f);
        dawn.horizon = new Color(1f, 0.72f, 0.55f);
        dawn.ground = new Color(0.45f, 0.42f, 0.46f);
        dawn.disk = new Color(1f, 0.74f, 0.5f);
        dawn.glow = new Color(1f, 0.6f, 0.42f);
        dawn.glowStrength = 0.65f;
        dawn.horizonGlow = 0.8f;
        dawn.cloudLit = new Color(1f, 0.79f, 0.68f);
        dawn.cloudShadow = new Color(0.5f, 0.45f, 0.62f);
        dawn.light = new Color(1f, 0.72f, 0.52f);
        dawn.lightIntensity = 0.6f;
        dawn.ambSky = new Color(0.5f, 0.5f, 0.66f);
        dawn.ambEq = new Color(0.6f, 0.5f, 0.48f);
        dawn.ambGround = new Color(0.26f, 0.23f, 0.22f);
        dawn.water = new Color(0.9f, 0.84f, 0.82f);

        keys = new[]
        {
            new Key { t = 0f, look = dawn },
            new Key { t = 0.07f, look = day },
            new Key { t = 0.38f, look = day },
            new Key { t = 0.47f, look = afternoon },
            new Key { t = 0.55f, look = sunset },
            new Key { t = 0.62f, look = dusk },
            new Key { t = 0.7f, look = night },
            new Key { t = 0.9f, look = night },
            new Key { t = 0.95f, look = dusk },
            new Key { t = 1f, look = dawn }
        };
    }

    Look SampleLook(float t)
    {
        t = Mathf.Repeat(t, 1f);
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t > keys[i + 1].t) continue;
            float k = Mathf.InverseLerp(keys[i].t, keys[i + 1].t, t);
            return Look.Lerp(keys[i].look, keys[i + 1].look, Mathf.SmoothStep(0f, 1f, k));
        }
        return keys[keys.Length - 1].look;
    }

    static Vector3 SkyDirection(float elevation, float azimuth)
    {
        float e = elevation * Mathf.Deg2Rad, a = azimuth * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
    }

    // Cooperativo: los clientes siguen la hora del anfitrión (se corrige suave para que no salte)
    static float syncedTime = -1f;

    public static void SyncTime(float t)
    {
        syncedTime = Mathf.Repeat(t, 1f);
    }

    void UpdateCycle(float dt)
    {
        if (!cycleOn || keys == null) return;
        if (previewTimeOfDay >= 0f) timeOfDay = previewTimeOfDay;
        else timeOfDay = Mathf.Repeat(timeOfDay + dt / (Mathf.Max(1f, dayLengthMinutes) * 60f), 1f);
        if (syncedTime >= 0f && previewTimeOfDay < 0f)
        {
            float diff = Mathf.Repeat(syncedTime - timeOfDay + 0.5f, 1f) - 0.5f;
            timeOfDay = Mathf.Abs(diff) > 0.05f ? syncedTime : Mathf.Repeat(timeOfDay + diff * Mathf.Clamp01(dt * 2f), 1f);
            syncedTime = -1f;
        }
        TimeOfDay = timeOfDay;
        if (SurvivalRun.InSurvival) SurvivalRun.TimeOfDay = timeOfDay;
        ApplyCycle(timeOfDay);

        // Aviso al cambiar de momento del día
        int phase = PhaseIndex(timeOfDay);
        if (phase != lastPhase)
        {
            lastPhase = phase;
            string msg = phase == 1 ? "ATARDECE SOBRE EL LAGO" : phase == 2 ? "CAE LA NOCHE" : phase == 3 ? "AMANECE" : "";
            if (msg != "" && previewTimeOfDay < 0f && GameDirector.Instance != null)
                GameDirector.Instance.ShowToast(msg, new Color(1f, 0.78f, 0.45f, 1f));
        }
    }

    // 0 día, 1 atardecer, 2 noche, 3 amanecer
    static int PhaseIndex(float t)
    {
        if (t >= 0.97f || t < 0.05f) return 3;
        if (t < 0.52f) return 0;
        if (t < 0.65f) return 1;
        return 2;
    }

    void ApplyCycle(float t)
    {
        Look look = SampleLook(t);

        // Sol de día (sale, cruza el cielo y se pone); luna de noche, del otro lado
        bool day = t < DayEnd;
        float k = day ? t / DayEnd : (t - DayEnd) / (1f - DayEnd);
        float elevation = -3f + ((day ? SunPeak : MoonPeak) + 3f) * Mathf.Sin(k * Mathf.PI);
        float azimuth = sunAzimuth + (day ? Mathf.Lerp(-110f, 110f, k) : 180f + Mathf.Lerp(-100f, 100f, k));
        Vector3 sky = SkyDirection(elevation, azimuth);
        Shader.SetGlobalVector(SkySunId, new Vector4(sky.x, sky.y, sky.z, 1f));
        Shader.SetGlobalFloat(StarsId, look.stars);
        waterTint = look.water;
        Shader.SetGlobalVector(SkyTintId, new Vector4(waterTint.r, waterTint.g, waterTint.b, 1f));

        if (sunLight != null)
        {
            // La luz nunca viene desde abajo del horizonte; cerca del horizonte se apaga (sin saltos al cambiar a la luna)
            Vector3 toLight = SkyDirection(Mathf.Max(elevation, 7f), azimuth);
            sunLight.transform.rotation = Quaternion.LookRotation(-toLight, Vector3.up);
            float rise = Mathf.Clamp01(Mathf.InverseLerp(-3f, 12f, elevation));
            sunLight.intensity = baseSunIntensity * look.lightIntensity * Mathf.Lerp(0.12f, 1f, rise);
            sunLight.color = look.light;
        }

        if (skyInstance != null)
        {
            skyInstance.SetColor("_TopColor", look.top);
            skyInstance.SetColor("_MidColor", look.mid);
            skyInstance.SetColor("_HorizonColor", look.horizon);
            skyInstance.SetColor("_GroundColor", look.ground);
            skyInstance.SetColor("_SunColor", look.disk);
            skyInstance.SetColor("_GlowColor", look.glow);
            skyInstance.SetColor("_CloudLit", look.cloudLit);
            skyInstance.SetColor("_CloudShadow", look.cloudShadow);
            skyInstance.SetFloat("_GlowStrength", look.glowStrength);
            skyInstance.SetFloat("_GlowPower", look.glowPower);
            skyInstance.SetFloat("_HorizonGlow", look.horizonGlow);
            skyInstance.SetFloat("_SunIntensity", look.diskIntensity);
            skyInstance.SetFloat("_SunSize", look.diskSize);
        }

        if (setAmbient)
        {
            RenderSettings.ambientSkyColor = look.ambSky;
            RenderSettings.ambientEquatorColor = look.ambEq;
            RenderSettings.ambientGroundColor = look.ambGround;
        }

        // Niebla del color del horizonte de esta hora (bajo el agua manda la del agua)
        if (fog && fogFromSky)
        {
            airFogColor = Color.Lerp(look.horizon, look.ground, 0.2f);
            if (under01 <= 0f) RenderSettings.fogColor = airFogColor;
        }
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

        UpdateCycle(dt);
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
        if (cycleOn) water *= waterTint;   // de noche el agua también se oscurece
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
