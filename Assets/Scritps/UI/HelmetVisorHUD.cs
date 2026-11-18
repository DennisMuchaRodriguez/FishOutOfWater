using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Visor de casco en primera persona (estilo Metroid Prime / Iron Man).
// Envuelve el HUD existente sin cambiar sus sprites:
//  - Marco del casco con borde cian brillante, ceja y mentón
//  - Cristal: reflejo, líneas de escaneo, tinte bajo el agua y gotas al salir del agua
//  - El HUD "flota" dentro del casco con inercia al girar, subir y caer
//  - Retícula que sigue al mouse, con dispersión, carga y marcador de impacto
//  - Secuencia de encendido y fallas visuales al recibir daño
[RequireComponent(typeof(Canvas))]
public class HelmetVisorHUD : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerController_Base player;
    public PlayerShooting shooting;
    [Tooltip("Panel del HUD actual (barras, munición...)")]
    public RectTransform hudPanel;

    [Header("Colores (mismo estilo que el HUD)")]
    public Color accent = new Color(0.25f, 0.72f, 1f, 1f);
    public Color frameColor = new Color(0.01f, 0.035f, 0.06f, 1f);
    public Color dangerColor = new Color(1f, 0.15f, 0.1f, 1f);
    public Color waterTint = new Color(0.05f, 0.45f, 0.55f, 1f);

    [Header("Marco del casco")]
    [Range(0f, 1f)] public float frameOpacity = 0.92f;
    [Range(0f, 1f)] public float rimGlow = 0.65f;
    [Tooltip("Qué tanto invade el marco la pantalla (más alto = casco más cerrado)")]
    [Range(0.8f, 1.1f)] public float visorSize = 0.97f;

    [Header("Cristal")]
    [Range(0f, 0.3f)] public float scanlineOpacity = 0.05f;
    [Range(0f, 0.3f)] public float reflectionOpacity = 0.07f;
    [Range(0f, 1f)] public float underwaterTintOpacity = 0.45f;
    public int dropletsOnExit = 14;

    [Header("Daño del cristal")]
    [Tooltip("Opacidad de las grietas del cristal (salen con el traje bajo 60%, 40%, 25% y 12%)")]
    [Range(0f, 1f)] public float crackOpacity = 0.9f;
    public Color crackColor = new Color(0.86f, 0.96f, 1f, 1f);
    [Tooltip("Segundos que parpadea el borde en rojo al quedar con poca vida; después queda un rojo fijo y suave")]
    public float lowHealthBlinkTime = 2.5f;
    [Tooltip("Qué tan rojo queda el borde después de parpadear (0 = nada, 1 = rojo pleno)")]
    [Range(0f, 1f)] public float lowHealthSteadyRed = 0.35f;

    [Header("Inercia del HUD")]
    public float swayFromTurn = 0.12f;
    public float swayFromVertical = 1.8f;
    public float maxSway = 28f;
    public float swaySmoothing = 6f;
    public float breathingAmount = 2f;

    [Header("Retícula")]
    public bool showReticle = true;
    public bool reticleFollowsMouse = true;
    public bool hideSystemCursor = true;
    public float reticleSize = 34f;

    [Header("Textos")]
    public bool showStatusText = true;
    public bool showReadout = true;

    [Header("Encendido")]
    public float bootDuration = 1.4f;

    // ---- Interno ----
    Canvas visorCanvas;
    RectTransform canvasRect;
    RectTransform frameRoot, swayRoot, overlayRoot;
    RawImage frameImage, scanlines, reflection, glassTint, bootLine;
    // Destellos del borde: línea brillante que respira y dos chispas que recorren el borde del visor
    RawImage rimSheen;
    readonly RawImage[] glints = new RawImage[2];
    static readonly Color GlintColor = new Color(0.72f, 0.92f, 1f, 1f);
    CanvasGroup hudGroup;
    RectTransform reticleRoot;
    RawImage reticleRing, reticleDot;
    RawImage[] reticleTicks = new RawImage[4];
    RawImage[] hitTicks = new RawImage[4];
    Image chargeRing;
    TextMeshProUGUI statusText, readoutText;
    VisorAmmoHUD ammoHud;
    VisorStatusHUD statusHud;
    VisorSonarHUD sonarHud;
    VisorMissionHUD missionHud;
    readonly List<Droplet> droplets = new List<Droplet>();
    RectTransform dropletRoot;

    Vector2 swayOffset;
    float lastYaw;
    float bootTimer;
    float glitchTimer;
    float lowHealthTime;
    bool wasCritical;

    // Grietas del cristal: una por cada umbral de traje que se cruza (se van si el traje se repara)
    static readonly float[] CrackThresholds = { 0.6f, 0.4f, 0.25f, 0.12f };
    static readonly Vector2[] CrackSpots = { new Vector2(-0.33f, 0.24f), new Vector2(0.31f, -0.26f), new Vector2(0.29f, 0.3f), new Vector2(-0.18f, -0.14f) };
    static readonly float[] CrackSizes = { 0.36f, 0.42f, 0.38f, 0.55f };
    static Texture2D[] crackTextures;
    class Crack
    {
        public RawImage img, flash;
        public float shown;    // 0..1 (aparece creciendo; se borra al repararse)
        public bool on;
        public float flashTime;
    }
    readonly Crack[] cracks = new Crack[4];
    RectTransform crackRoot;
    float hitTimer;
    bool lastHitWasKill;
    float lastArmor;
    float submergedWeight;
    float reticleSpreadPx;
    float reticleLock;   // 0..1: la mira tiene un ave fijada (la ayuda de puntería la corrige)
    string bootMessage = "SISTEMAS DEL TRAJE EN LÍNEA";

    class Droplet
    {
        public RawImage img;
        public Vector2 pos;
        public float speed, life, maxLife, size;
    }

    // Manchas en el cristal (el "regalo" de una gaviota): se escurren despacio y se borran
    class Splat
    {
        public RawImage img;
        public Vector2 pos;
        public float life, maxLife, size, angle;
    }
    readonly List<Splat> splats = new List<Splat>();
    static Texture2D splatTex;

    public static HelmetVisorHUD Instance { get; private set; }

    // Reloj de las animaciones del visor: el del juego en Play y el real en el editor (la vista previa del casco
    // anima sin darle Play; en el editor Time.time no avanza)
    public static float Now { get { return Application.isPlaying ? Time.time : Time.realtimeSinceStartup; } }

    // Destroy en el juego, DestroyImmediate en el editor (Destroy no se puede usar fuera de Play)
    public static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // ---- Vista previa en el editor (Herramientas > Casco > Vista previa del casco) ----
    // La vista previa es una COPIA oculta de este visor que no se guarda en la escena; estos campos solo los usa esa copia.
    [System.NonSerialized] public bool previewMode;
    [System.NonSerialized] public bool previewUnderwater;
    public VisorMissionHUD MissionHud { get { return missionHud; } }

    // Ensucia el visor (solo se ve en primera persona)
    public static void SplatGlass()
    {
        if (Instance != null) Instance.AddSplat();
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        canvasRect = (RectTransform)transform;
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();
        if (shooting == null && player != null) shooting = player.GetComponent<PlayerShooting>();
        BuildAll();
    }

    // Arma todo el casco: lo usa el juego (Start) y la vista previa del editor (BuildPreview)
    void BuildAll()
    {
        BuildFrame();
        BuildSwayRoot();
        BuildOverlay();

        // Munición (arco del borde derecho), propulsor y traje (arriba), misión (arco del borde izquierdo)
        // y sonar, dentro del visor
        ammoHud = gameObject.AddComponent<VisorAmmoHUD>();
        ammoHud.visor = this;
        ammoHud.player = player;
        ammoHud.shooting = shooting;
        ammoHud.Build(swayRoot, accent, dangerColor);
        // Propulsor y traje (arriba, siguiendo la curva del visor)
        statusHud = gameObject.AddComponent<VisorStatusHUD>();
        statusHud.visor = this;
        statusHud.player = player;
        statusHud.Build(swayRoot, accent, dangerColor);
        sonarHud = gameObject.AddComponent<VisorSonarHUD>();
        sonarHud.player = player;
        sonarHud.Build(overlayRoot, accent);
        // Misión (borde izquierdo, espejo de la munición): peces a salvo, oleada, depredadores y cuenta atrás
        missionHud = gameObject.AddComponent<VisorMissionHUD>();
        missionHud.visor = this;
        missionHud.player = player;
        missionHud.previewMode = previewMode;
        if (previewMode) missionHud.previewSample = VisorMissionHUD.Sample.Example;
        missionHud.Build(swayRoot, overlayRoot, accent, dangerColor);

        if (player != null)
        {
            lastArmor = player.maxArmor;
            lastYaw = player.transform.eulerAngles.y;
            player.OnSubmergedChanged += HandleSubmerged;
        }
        if (shooting != null) shooting.OnTargetHit += HandleTargetHit;

        bootTimer = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (player != null) player.OnSubmergedChanged -= HandleSubmerged;
        if (shooting != null) shooting.OnTargetHit -= HandleTargetHit;
    }

    void OnDisable()
    {
        Cursor.visible = true;
        if (visorCanvas != null) visorCanvas.enabled = true;
    }

    // ======================= CONSTRUCCIÓN =======================

    static RectTransform CreateRect(string name, Transform parent, bool stretch)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        }
        return rt;
    }

    static RawImage CreateRaw(string name, Transform parent, Texture tex, Color color, bool stretch)
    {
        RectTransform rt = CreateRect(name, parent, stretch);
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    void BuildFrame()
    {
        frameRoot = CreateRect("VisorFrame", canvasRect, true);
        frameRoot.SetSiblingIndex(0);

        glassTint = CreateRaw("GlassWaterTint", frameRoot, MakeVignetteTexture(), new Color(waterTint.r, waterTint.g, waterTint.b, 0f), true);

        reflection = CreateRaw("GlassReflection", frameRoot, MakeReflectionTexture(), new Color(1f, 1f, 1f, reflectionOpacity), true);

        Texture2D scan = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        scan.SetPixels(new[] { new Color(1, 1, 1, 0), new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0) });
        scan.Apply();
        scan.wrapMode = TextureWrapMode.Repeat;
        scan.filterMode = FilterMode.Point;
        scanlines = CreateRaw("Scanlines", frameRoot, scan, new Color(accent.r, accent.g, accent.b, scanlineOpacity), true);

        dropletRoot = CreateRect("Droplets", frameRoot, true);
        BuildCracks();

        frameImage = CreateRaw("HelmetFrame", frameRoot, MakeFrameTexture(), Color.white, true);
        rimSheen = CreateRaw("RimSheen", frameRoot, MakeRimTexture(), new Color(GlintColor.r, GlintColor.g, GlintColor.b, 0.35f), true);
        for (int i = 0; i < glints.Length; i++)
        {
            glints[i] = CreateRaw("Destello" + i, frameRoot, FXFactory.Streak, GlintColor, false);
            glints[i].rectTransform.sizeDelta = new Vector2(7f, 110f);
        }
    }

    void BuildSwayRoot()
    {
        swayRoot = CreateRect("VisorSway", canvasRect, true);
        hudGroup = swayRoot.gameObject.AddComponent<CanvasGroup>();
        hudGroup.blocksRaycasts = false;
        hudGroup.interactable = false;
        hudGroup.alpha = 0f;

        if (hudPanel != null)
        {
            swayRoot.SetSiblingIndex(hudPanel.GetSiblingIndex());
            hudPanel.SetParent(swayRoot, false);
        }
        else
        {
            swayRoot.SetSiblingIndex(frameRoot.GetSiblingIndex() + 1);
        }
    }

    void BuildOverlay()
    {
        overlayRoot = CreateRect("VisorOverlay", canvasRect, true);
        overlayRoot.SetSiblingIndex(swayRoot.GetSiblingIndex() + 1);

        // Línea de escaneo del encendido
        bootLine = CreateRaw("BootLine", overlayRoot, FXFactory.Streak, accent, false);
        bootLine.rectTransform.anchorMin = new Vector2(0f, 1f);
        bootLine.rectTransform.anchorMax = new Vector2(1f, 1f);
        bootLine.rectTransform.sizeDelta = new Vector2(0f, 6f);
        bootLine.uvRect = new Rect(0f, 0f, 1f, 1f);
        bootLine.rectTransform.localEulerAngles = Vector3.zero;

        // Retícula
        reticleRoot = CreateRect("Reticle", overlayRoot, false);
        reticleRoot.sizeDelta = Vector2.zero;

        Texture2D ring = FXFactory.MakeRadial(128, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.06f), "FX_Ring");
        reticleRing = CreateRaw("Ring", reticleRoot, ring, new Color(accent.r, accent.g, accent.b, 0.75f), false);
        reticleRing.rectTransform.sizeDelta = Vector2.one * reticleSize;

        reticleDot = CreateRaw("Dot", reticleRoot, FXFactory.SoftDot, accent, false);
        reticleDot.rectTransform.sizeDelta = Vector2.one * 6f;

        for (int i = 0; i < 4; i++)
        {
            RawImage tick = CreateRaw("Tick" + i, reticleRoot, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.9f), false);
            tick.rectTransform.sizeDelta = new Vector2(2f, 9f);
            tick.rectTransform.localEulerAngles = new Vector3(0f, 0f, i * 90f);
            reticleTicks[i] = tick;

            RawImage hit = CreateRaw("Hit" + i, reticleRoot, FXFactory.White, new Color(1f, 1f, 1f, 0f), false);
            hit.rectTransform.sizeDelta = new Vector2(2.5f, 10f);
            hit.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f + i * 90f);
            hitTicks[i] = hit;
        }

        GameObject chargeGo = CreateRect("ChargeRing", reticleRoot, false).gameObject;
        chargeRing = chargeGo.AddComponent<Image>();
        chargeRing.sprite = FXFactory.SpriteFrom(ring);
        chargeRing.type = Image.Type.Filled;
        chargeRing.fillMethod = Image.FillMethod.Radial360;
        chargeRing.fillOrigin = (int)Image.Origin360.Top;
        chargeRing.fillClockwise = true;
        chargeRing.fillAmount = 0f;
        chargeRing.color = Color.white;
        chargeRing.raycastTarget = false;
        chargeRing.rectTransform.sizeDelta = Vector2.one * (reticleSize + 18f);

        // Textos del visor
        if (showStatusText)
        {
            statusText = CreateText("VisorStatus", overlayRoot, 15f, TextAlignmentOptions.Center);
            RectTransform rt = statusText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 34f);
            rt.sizeDelta = new Vector2(520f, 26f);
        }
        if (showReadout)
        {
            readoutText = CreateText("VisorReadout", overlayRoot, 12f, TextAlignmentOptions.Center);
            RectTransform rt = readoutText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -30f);
            rt.sizeDelta = new Vector2(420f, 22f);
        }
    }

    TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        RectTransform rt = CreateRect(name, parent, false);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.characterSpacing = 6f;
        t.color = accent;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.text = "";
        return t;
    }

    // ======================= TEXTURAS PROCEDURALES =======================

    Texture2D MakeFrameTexture()
    {
        int w = 640, h = 360;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;

                // Forma del visor: superelipse con "ceja" arriba al centro y "mentón" abajo
                float au = Mathf.Abs(u) / visorSize;
                float av = Mathf.Abs(v) / (visorSize - 0.02f);
                float d = Mathf.Pow(Mathf.Pow(au, 5f) + Mathf.Pow(av, 4f), 1f / 5f);
                float threshold = 1f;
                if (v > 0f) threshold -= 0.07f * Mathf.Exp(-(u * u) / (0.22f * 0.22f)) * Mathf.Clamp01(v * 1.4f);
                else threshold -= 0.10f * Mathf.Exp(-(u * u) / (0.16f * 0.16f)) * Mathf.Clamp01(-v * 1.4f);
                // esquinas inferiores un poco más cerradas (mejillas del casco)
                if (v < 0f) threshold -= 0.05f * Mathf.Clamp01(Mathf.Abs(u) - 0.55f) * Mathf.Clamp01(-v);

                float edge = d - threshold;

                // Capa oscura del casco
                float darkA = edge > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.06f)) * frameOpacity : 0f;
                // Sombra interior suave para dar profundidad
                if (edge <= 0f) darkA = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge + 0.22f) / 0.22f)) * 0.22f;

                Color c = frameColor;
                // Reflejo metálico tenue cerca del borde exterior
                if (edge > 0f)
                {
                    float metal = Mathf.Exp(-((edge - 0.035f) * (edge - 0.035f)) / (0.02f * 0.02f)) * 0.35f;
                    c = Color.Lerp(c, accent * 0.45f, metal);
                }

                // Línea brillante del borde + resplandor
                float line = Mathf.Exp(-(edge * edge) / (0.0045f * 0.0045f));
                float glow = Mathf.Exp(-(edge * edge) / (0.03f * 0.03f)) * 0.35f;

                // Segmentos técnicos interiores (línea discontinua a ciertos ángulos)
                float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                float a2 = Mathf.Repeat(ang, 360f);
                bool seg = (a2 > 18f && a2 < 34f) || (a2 > 146f && a2 < 162f) || (a2 > 200f && a2 < 222f) || (a2 > 318f && a2 < 340f);
                float inset = edge + 0.04f;
                float segLine = seg ? Mathf.Exp(-(inset * inset) / (0.004f * 0.004f)) * 0.8f : 0f;

                // Componer el brillo encima de la capa oscura
                float glowA = Mathf.Clamp01((line + glow + segLine) * rimGlow);
                float outA = glowA + darkA * (1f - glowA);
                Color outC = outA > 0.0001f
                    ? (c * darkA * (1f - glowA) + accent * glowA) / outA
                    : accent;
                px[y * w + x] = new Color(outC.r, outC.g, outC.b, outA);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        tex.name = "FX_HelmetFrame";
        return tex;
    }

    Texture2D MakeVignetteTexture()
    {
        return FXFactory.MakeRadial(128, d => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.35f) / 1.0f)), "FX_Vignette");
    }

    Texture2D MakeReflectionTexture()
    {
        int s = 256;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (float)x / s;
                float v = (float)y / s;
                float k = u + (1f - v) * 0.55f;
                float band = Mathf.Exp(-Mathf.Pow((k - 0.42f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((k - 0.58f) / 0.018f, 2f)) * 0.6f;
                // Solo en la parte superior izquierda del cristal
                float mask = Mathf.Clamp01(1.3f - Vector2.Distance(new Vector2(u, v), new Vector2(0.2f, 0.85f)) * 1.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(band * mask)));
            }
        }
        tex.Apply();
        tex.name = "FX_Reflection";
        return tex;
    }

    // ======================= ACTUALIZACIÓN =======================

    void Update()
    {
        Tick(Time.deltaTime);
    }

    void Tick(float dt)
    {
        if (player == null) return;
        bool firstPerson = !player.isThirdPerson;
        bool cinematic = !previewMode && GameDirector.InCinematic;

        // Durante la cinemática se apaga TODO el canvas del casco (marco, HUD, filtros):
        // la cinemática es una vista de cámara externa, no desde dentro del casco
        if (visorCanvas == null) visorCanvas = GetComponent<Canvas>();
        if (visorCanvas != null) visorCanvas.enabled = !cinematic;
        // El casco (marco, munición, propulsor, traje, textos y sonar) solo se ve en primera persona.
        // En tercera persona queda solo la retícula para apuntar
        frameRoot.gameObject.SetActive(firstPerson && !cinematic);
        overlayRoot.gameObject.SetActive(!cinematic);
        swayRoot.gameObject.SetActive(firstPerson && !cinematic);
        bootLine.gameObject.SetActive(firstPerson);
        if (statusText != null) statusText.gameObject.SetActive(firstPerson);
        if (sonarHud != null) sonarHud.SetVisible(firstPerson);

        UpdateBoot(dt);
        UpdateSway(dt, firstPerson);
        UpdateGlass(dt);
        UpdateShine();
        UpdateDamage(dt);
        UpdateReticle(dt);
        UpdateTexts();
        UpdateDroplets(dt);
        UpdateCracks(dt);
        UpdateSplats(dt);
        if (!previewMode) UpdateCursor();   // en el editor no se toca el cursor
    }

    void UpdateBoot(float dt)
    {
        if (bootTimer >= bootDuration + 2f) return;
        bootTimer += dt;
        float t = Mathf.Clamp01(bootTimer / bootDuration);

        // Línea de escaneo bajando por el cristal
        float sweep = Mathf.Clamp01(t / 0.6f);
        bootLine.rectTransform.anchoredPosition = new Vector2(0f, -sweep * canvasRect.rect.height);
        bootLine.color = new Color(accent.r, accent.g, accent.b, sweep < 1f ? 0.9f : 0f);

        // El HUD parpadea mientras arranca
        float a;
        if (t < 0.45f) a = 0f;
        else if (t < 0.8f) a = Random.value > 0.45f ? Mathf.Lerp(0.3f, 1f, t) : 0.1f;
        else a = 1f;
        hudGroup.alpha = a;
    }

    void UpdateSway(float dt, bool firstPerson)
    {
        float yaw = player.transform.eulerAngles.y;
        float yawRate = dt > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
        lastYaw = yaw;

        Vector2 target = Vector2.zero;
        if (firstPerson)
        {
            target.x = -yawRate * swayFromTurn;
            target.y = -player.Velocity.y * swayFromVertical;
            target = Vector2.ClampMagnitude(target, maxSway);
            // Respiración
            target.y += Mathf.Sin(Now * 1.4f) * breathingAmount;
            if (player.isInWater) target.x += Mathf.Sin(Now * 2.1f) * breathingAmount * (0.5f + player.SwimSpeed01);
        }
        if (glitchTimer > 0f) target += Random.insideUnitCircle * 9f * (glitchTimer / 0.35f);

        swayOffset = Vector2.Lerp(swayOffset, target, 1f - Mathf.Exp(-swaySmoothing * dt));
        swayRoot.anchoredPosition = swayOffset;
        swayRoot.localEulerAngles = new Vector3(0f, 0f, firstPerson ? -player.TurnInput * 1.2f : 0f);

        float scale = 1f;
        if (player.IsJetting) scale = 1.012f;
        if (player.IsDashing || player.IsTurbo) scale = 1.025f;
        swayRoot.localScale = Vector3.Lerp(swayRoot.localScale, Vector3.one * scale, 1f - Mathf.Exp(-8f * dt));
    }

    // El borde respira y, cada pocos segundos, una chispa recorre el borde de arriba y otra el de abajo
    void UpdateShine()
    {
        if (rimSheen == null || canvasRect == null) return;
        float time = Time.unscaledTime;
        float breathe = 0.28f + 0.14f * Mathf.Sin(time * 2.1f) + 0.06f * Mathf.Sin(time * 5.3f);
        rimSheen.color = new Color(GlintColor.r, GlintColor.g, GlintColor.b, Mathf.Clamp01(breathe));

        Rect r = canvasRect.rect;
        float hw = r.width * 0.5f, hh = r.height * 0.5f;
        const float cycle = 4.6f, travel = 1.15f;
        for (int i = 0; i < glints.Length; i++)
        {
            float local = Mathf.Repeat(time + i * cycle * 0.5f, cycle);
            float k = local / travel;
            RawImage g = glints[i];
            if (k >= 1f)
            {
                if (g.gameObject.activeSelf) g.gameObject.SetActive(false);
                continue;
            }
            if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
            // La de arriba va de izquierda a derecha; la de abajo al revés
            float u = Mathf.Lerp(-0.9f, 0.9f, Mathf.SmoothStep(0f, 1f, k)) * (i == 0 ? 1f : -1f);
            float side = i == 0 ? 1f : -1f;
            Vector2 p = RimPoint(u, side, hw, hh);
            Vector2 q = RimPoint(u + 0.01f, side, hw, hh);
            Vector2 tan = (q - p).normalized;
            RectTransform rt = g.rectTransform;
            rt.anchoredPosition = p;
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg - 90f);
            float a = Mathf.Sin(k * Mathf.PI);
            g.color = new Color(GlintColor.r, GlintColor.g, GlintColor.b, 0.95f * a);
            rt.localScale = new Vector3(1f + a * 0.6f, 0.7f + a * 0.6f, 1f);
        }
    }

    // Punto del borde del visor (la misma superelipse que el marco), en unidades del canvas. side: +1 arriba, -1 abajo
    Vector2 RimPoint(float u, float side, float hw, float hh)
    {
        float au = Mathf.Clamp01(Mathf.Abs(u) / visorSize);
        float v = (visorSize - 0.02f) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(au, 5f)), 0.25f);
        float dip = side > 0f ? 0.07f * Mathf.Exp(-(u * u) / (0.22f * 0.22f)) : 0.10f * Mathf.Exp(-(u * u) / (0.16f * 0.16f));
        v *= 1f - dip;
        return new Vector2(u * hw, side * v * hh);
    }

    // Solo la línea del borde del visor (brillante, para el destello)
    Texture2D MakeRimTexture()
    {
        int w = 640, h = 360;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float au = Mathf.Abs(u) / visorSize;
                float av = Mathf.Abs(v) / (visorSize - 0.02f);
                float d = Mathf.Pow(Mathf.Pow(au, 5f) + Mathf.Pow(av, 4f), 1f / 5f);
                float threshold = 1f;
                if (v > 0f) threshold -= 0.07f * Mathf.Exp(-(u * u) / (0.22f * 0.22f)) * Mathf.Clamp01(v * 1.4f);
                else threshold -= 0.10f * Mathf.Exp(-(u * u) / (0.16f * 0.16f)) * Mathf.Clamp01(-v * 1.4f);
                if (v < 0f) threshold -= 0.05f * Mathf.Clamp01(Mathf.Abs(u) - 0.55f) * Mathf.Clamp01(-v);
                float edge = d - threshold;
                float line = Mathf.Exp(-(edge * edge) / (0.0035f * 0.0035f));
                float halo = Mathf.Exp(-((edge + 0.01f) * (edge + 0.01f)) / (0.018f * 0.018f)) * 0.35f;
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(line + halo));
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        tex.name = "FX_BordeVisor";
        return tex;
    }

    void UpdateGlass(float dt)
    {
        // Al hundirse el tinte entra casi de golpe (igual que el filtro); al salir se va más despacio
        bool eyeSubmerged = previewMode ? previewUnderwater : player.IsEyeSubmerged;
        submergedWeight = Mathf.MoveTowards(submergedWeight, eyeSubmerged ? 1f : 0f, dt * (eyeSubmerged ? 10f : 2.5f));
        float caustic = 0.85f + Mathf.Sin(Now * 1.7f) * 0.1f + Mathf.Sin(Now * 3.1f) * 0.05f;
        glassTint.color = new Color(waterTint.r, waterTint.g, waterTint.b, submergedWeight * underwaterTintOpacity * caustic);

        // Líneas de escaneo que se desplazan lentamente
        float lines = Mathf.Max(1f, canvasRect.rect.height / 3f);
        scanlines.uvRect = new Rect(0f, Now * 0.6f, 1f, lines);

        // El reflejo se mueve un poco con el giro (como si la luz se quedara fija)
        float shift = Mathf.Repeat(player.transform.eulerAngles.y / 360f, 1f);
        float refl = reflectionOpacity * (1f - submergedWeight * 0.6f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(shift * Mathf.PI * 2f)));
        reflection.color = new Color(1f, 1f, 1f, refl);
        reflection.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(shift * Mathf.PI * 2f) * 30f, 0f);
    }

    void UpdateDamage(float dt)
    {
        if (player.currentArmor < lastArmor - 0.01f)
        {
            glitchTimer = 0.35f;
        }
        lastArmor = player.currentArmor;

        float armor01 = player.maxArmor > 0f ? player.currentArmor / player.maxArmor : 1f;
        Color rim = Color.white;
        // Poca vida: el borde parpadea en rojo unos segundos (y otra vez al quedar crítico) y después se queda
        // en un rojo suave y fijo, para avisar sin molestar
        bool low = armor01 <= 0.25f && !player.isDead;
        bool critical = armor01 <= 0.12f && !player.isDead;
        if (critical && !wasCritical) lowHealthTime = 0f;
        wasCritical = critical;
        lowHealthTime = low ? lowHealthTime + dt : 0f;
        if (low || player.isDead)
        {
            float blink = Mathf.Sin(Now * 7f) * 0.5f + 0.5f;
            float settle = Mathf.Clamp01((lowHealthTime - lowHealthBlinkTime) / 0.8f);
            float red = player.isDead ? 1f : Mathf.Lerp(blink, lowHealthSteadyRed, settle);
            rim = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.45f, 1f), red);
        }

        if (glitchTimer > 0f)
        {
            glitchTimer -= dt;
            if (bootTimer > bootDuration) hudGroup.alpha = Random.value > 0.3f ? 1f : 0.35f;
            rim = Color.Lerp(rim, new Color(1f, 0.4f, 0.4f, 1f), 0.6f);
        }
        else if (bootTimer > bootDuration)
        {
            hudGroup.alpha = 1f;
        }

        frameImage.color = rim;
    }

    void UpdateReticle(float dt)
    {
        bool show = showReticle && shooting != null && shooting.enabled && !player.isDead && !PauseMenu.IsPaused;
        reticleRoot.gameObject.SetActive(show);
        if (!show) return;

        Vector2 local = Vector2.zero;
        if (reticleFollowsMouse && !previewMode)   // en la vista previa queda al centro
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out local);
        }
        reticleRoot.anchoredPosition = local;

        // Ave fijada: las marcas se cierran en diagonal y la retícula se pone blanca
        reticleLock = Mathf.MoveTowards(reticleLock, shooting.AimTarget != null ? 1f : 0f, dt * 9f);

        // Dispersión -> separación de las marcas
        float spreadPx = 8f + shooting.CurrentSpread * 7f;
        float sinceShot = Now - shooting.LastShotTime;
        if (sinceShot < 0.08f) spreadPx += 6f;
        spreadPx = Mathf.Lerp(spreadPx, 6.5f, reticleLock * 0.6f);
        reticleSpreadPx = Mathf.Lerp(reticleSpreadPx, spreadPx, 1f - Mathf.Exp(-18f * dt));
        for (int i = 0; i < 4; i++)
        {
            float ang = (i * 90f + reticleLock * 45f) * Mathf.Deg2Rad;
            reticleTicks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * reticleSpreadPx;
            reticleTicks[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -(i * 90f + reticleLock * 45f));
        }

        bool noAmmo = shooting.currentAmmo <= 0;
        Color baseC = noAmmo ? dangerColor : Color.Lerp(accent, Color.white, reticleLock * 0.75f);
        reticleRing.color = new Color(baseC.r, baseC.g, baseC.b, 0.55f + (sinceShot < 0.1f ? 0.4f : 0f) + reticleLock * 0.2f);
        reticleRing.rectTransform.localEulerAngles = new Vector3(0f, 0f, Now * (20f + reticleLock * 70f));
        reticleRing.rectTransform.localScale = Vector3.one * (1f - reticleLock * 0.18f);
        reticleDot.color = baseC;
        foreach (RawImage t in reticleTicks) t.color = new Color(baseC.r, baseC.g, baseC.b, 0.9f);

        // Anillo de carga
        float charge = shooting.ChargeProgress01;
        chargeRing.fillAmount = charge;
        bool full = charge >= 1f;
        chargeRing.color = full ? Color.Lerp(accent, Color.white, Mathf.Sin(Now * 25f) * 0.5f + 0.5f) : new Color(accent.r, accent.g, accent.b, 0.85f);
        chargeRing.rectTransform.localScale = Vector3.one * (full ? 1.05f : 1f);

        // Marcador de impacto
        if (hitTimer > 0f) hitTimer -= dt;
        float hitA = Mathf.Clamp01(hitTimer / 0.18f);
        Color hc = lastHitWasKill ? dangerColor : Color.white;
        for (int i = 0; i < 4; i++)
        {
            float ang = (45f + i * 90f) * Mathf.Deg2Rad;
            hitTicks[i].rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * (14f + (1f - hitA) * 6f);
            hitTicks[i].color = new Color(hc.r, hc.g, hc.b, hitA);
        }
    }

    void UpdateTexts()
    {
        if (statusText != null)
        {
            string msg = "";
            bool warn = false;
            float energy01 = player.maxJetpackEnergy > 0f ? player.currentJetpackEnergy / player.maxJetpackEnergy : 0f;

            if (bootTimer < bootDuration + 1.5f)
            {
                msg = bootTimer > bootDuration * 0.5f ? bootMessage : "";
            }
            else if (player.isDead)
            {
                msg = "";
            }
            else if (player.IsTurningBack)
            {
                msg = "FUERA DE LA ZONA  //  VOLVIENDO AL LAGO"; warn = true;
            }
            else if (player.IsSuffocating)
            {
                msg = "¡TE ASFIXIAS!  //  ARRÁSTRATE AL AGUA CON [ESPACIO]"; warn = true;
            }
            else if (player.IsStranded)
            {
                int secs = Mathf.CeilToInt(Mathf.Max(0f, player.strandedGrace - player.StrandedTime));
                msg = "SIN AGUA  //  ASFIXIA EN " + secs + " s  //  [ESPACIO] ARRASTRARSE"; warn = true;
            }
            else if (player.BoundaryWarning)
            {
                msg = "LÍMITE DE LA ZONA  //  REGRESA AL LAGO"; warn = true;
            }
            else if (!player.isInWater && energy01 <= 0.001f)
            {
                msg = "PROPULSOR SIN CARGA  //  BUSCA AGUA"; warn = true;
            }
            else if (player.IsSputtering)
            {
                msg = "PROPULSOR: CARGA BAJA"; warn = true;
            }
            else if (shooting != null && shooting.IsCharging)
            {
                msg = shooting.ChargeProgress01 >= 1f ? "DISPARO CARGADO  //  SUELTA" : "CARGANDO DISPARO";
            }
            else if (shooting != null && shooting.HudAmmo <= 0)
            {
                msg = "SIN MUNICIÓN  //  BUSCA CÁPSULAS EN EL LAGO"; warn = true;
            }
            else if (player.IsTurbo)
            {
                msg = energy01 < 0.25f ? "TURBO  //  COMBUSTIBLE BAJO" : "TURBO";
                warn = energy01 < 0.25f;
            }
            else if (shooting != null && shooting.HudAmmo <= Mathf.Max(5, shooting.HudMaxAmmo / 6))
            {
                msg = "MUNICIÓN BAJA  //  BUSCA CÁPSULAS";
            }
            else if (player.isInWater && energy01 < 0.999f)
            {
                msg = "HIDRO-RECARGA EN CURSO";
            }

            statusText.text = msg;
            Color c = warn ? dangerColor : accent;
            float a = warn ? (Mathf.Sin(Now * 8f) > -0.2f ? 0.95f : 0.25f) : 0.85f;
            statusText.color = new Color(c.r, c.g, c.b, a);
        }

        if (readoutText != null)
        {
            bool fp = !player.isThirdPerson;
            readoutText.gameObject.SetActive(fp && bootTimer > bootDuration);
            if (fp)
            {
                int heading = Mathf.RoundToInt(Mathf.Repeat(player.transform.eulerAngles.y, 360f));
                string second;
                if (player.isInWater)
                {
                    float depth = Mathf.Max(0f, player.WaterSurfaceY - (player.transform.position.y + player.firstPersonOffset.y));
                    second = "PROF " + depth.ToString("0.0") + " m";
                }
                else
                {
                    second = "ALT " + GetAltitude().ToString("0.0") + " m";
                }
                readoutText.text = "RUMBO " + heading.ToString("000") + "°    " + second;
                readoutText.color = new Color(accent.r, accent.g, accent.b, 0.7f);
            }
        }
    }

    float altitudeCache;
    float altitudeTimer;
    float GetAltitude()
    {
        altitudeTimer -= Time.deltaTime;
        if (altitudeTimer > 0f) return altitudeCache;
        altitudeTimer = 0.1f;

        Vector3 origin = player.transform.position;
        RaycastHit[] hits = Physics.RaycastAll(origin + Vector3.up * 0.5f, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
        float best = 500f;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(player.transform)) continue;
            if (h.collider.attachedRigidbody != null) continue;
            float dist = h.distance - 0.5f;
            if (dist >= 0f && dist < best) best = dist;
        }
        // La superficie del agua cuenta como "suelo" para la altitud
        float surface;
        if (LakeWater.TryGetSurface(origin, out surface) && origin.y >= surface)
            best = Mathf.Min(best, origin.y - surface);
        altitudeCache = best;
        return best;
    }

    void UpdateCursor()
    {
        if (!hideSystemCursor) return;
        bool playing = !PauseMenu.IsPaused && !player.isDead;
        Cursor.visible = !playing;
        Cursor.lockState = playing ? CursorLockMode.Confined : CursorLockMode.None;
    }

    // ======================= GOTAS EN EL CRISTAL =======================

    void HandleSubmerged(bool submerged)
    {
        if (!submerged && !player.isThirdPerson)
        {
            SpawnDroplets(dropletsOnExit);
        }
    }

    void SpawnDroplets(int count)
    {
        Rect r = canvasRect.rect;
        for (int i = 0; i < count; i++)
        {
            Droplet d = null;
            foreach (Droplet existing in droplets)
            {
                if (existing.life <= 0f) { d = existing; break; }
            }
            if (d == null)
            {
                d = new Droplet();
                d.img = CreateRaw("Drop", dropletRoot, FXFactory.Bubble, Color.white, false);
                droplets.Add(d);
            }
            d.img.gameObject.SetActive(true);
            d.size = Random.Range(8f, 22f);
            d.pos = new Vector2(Random.Range(-r.width * 0.48f, r.width * 0.48f), Random.Range(-r.height * 0.2f, r.height * 0.48f));
            d.speed = Random.Range(10f, 60f);
            d.maxLife = Random.Range(1.2f, 2.8f);
            d.life = d.maxLife;
        }
    }

    void UpdateDroplets(float dt)
    {
        foreach (Droplet d in droplets)
        {
            if (d.life <= 0f) continue;
            d.life -= dt;
            d.speed += dt * 25f;
            d.pos.y -= d.speed * dt;
            float t = Mathf.Clamp01(d.life / d.maxLife);
            d.img.rectTransform.anchoredPosition = d.pos;
            d.img.rectTransform.sizeDelta = new Vector2(d.size, d.size * Mathf.Lerp(1.6f, 1.1f, t));
            d.img.color = new Color(0.85f, 0.97f, 1f, 0.55f * t);
            if (d.life <= 0f) d.img.gameObject.SetActive(false);
        }
    }

    void AddSplat()
    {
        if (dropletRoot == null || canvasRect == null) return;
        if (splatTex == null) splatTex = MakeSplatTexture();
        Rect r = canvasRect.rect;
        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            Splat sp = null;
            foreach (Splat existing in splats)
                if (existing.life <= 0f) { sp = existing; break; }
            if (sp == null)
            {
                sp = new Splat();
                sp.img = CreateRaw("Mancha", dropletRoot, splatTex, Color.white, false);
                splats.Add(sp);
            }
            sp.img.gameObject.SetActive(true);
            sp.size = Random.Range(90f, 170f) * (i == 0 ? 1f : 0.55f);
            sp.pos = new Vector2(Random.Range(-r.width * 0.3f, r.width * 0.3f), Random.Range(-r.height * 0.05f, r.height * 0.3f));
            sp.angle = Random.Range(-25f, 25f);
            sp.maxLife = Random.Range(3.5f, 4.5f);
            sp.life = sp.maxLife;
        }
        glitchTimer = Mathf.Max(glitchTimer, 0.12f);
    }

    void UpdateSplats(float dt)
    {
        foreach (Splat sp in splats)
        {
            if (sp.life <= 0f) continue;
            sp.life -= dt;
            float t = Mathf.Clamp01(sp.life / sp.maxLife);
            float age = sp.maxLife - sp.life;
            sp.pos.y -= dt * (4f + age * 3f);   // se escurre
            RectTransform rt = sp.img.rectTransform;
            rt.anchoredPosition = sp.pos;
            float pop = Mathf.Min(1f, age * 9f);
            rt.sizeDelta = new Vector2(sp.size, sp.size * (1f + age * 0.08f)) * Mathf.Lerp(0.6f, 1f, pop);
            rt.localEulerAngles = new Vector3(0f, 0f, sp.angle);
            sp.img.color = new Color(0.96f, 0.95f, 0.88f, 0.92f * Mathf.Clamp01(t * 1.6f));
            if (sp.life <= 0f) sp.img.gameObject.SetActive(false);
        }
    }

    // Mancha blanca con gotas que se escurren hacia abajo
    static Texture2D MakeSplatTexture()
    {
        const int S = 128;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.name = "FX_Mancha";
        System.Random rng = new System.Random(7);
        Vector3[] blobs = new Vector3[9];
        blobs[0] = new Vector3(0f, 0.12f, 0.3f);
        for (int i = 1; i < blobs.Length; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = 0.18f + (float)rng.NextDouble() * 0.2f;
            blobs[i] = new Vector3(Mathf.Cos(a) * d, 0.12f + Mathf.Sin(a) * d * 0.8f, 0.05f + (float)rng.NextDouble() * 0.09f);
        }
        Vector3[] drips = { new Vector3(-0.12f, -0.42f, 0.045f), new Vector3(0.05f, -0.3f, 0.05f), new Vector3(0.17f, -0.36f, 0.035f) };
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f;
                float field = 0f;
                foreach (Vector3 b in blobs)
                {
                    float dx = u - b.x, dy = v - b.y;
                    field = Mathf.Max(field, 1f - Mathf.Sqrt(dx * dx + dy * dy) / b.z);
                }
                foreach (Vector3 d in drips)
                {
                    // Gota: rayita vertical desde la mancha hasta la punta, con la punta redonda
                    float dx = Mathf.Abs(u - d.x);
                    if (v > d.y && v < 0.12f) field = Mathf.Max(field, 1f - dx / (d.z * 0.55f));
                    float ty = v - d.y;
                    field = Mathf.Max(field, 1f - Mathf.Sqrt(dx * dx + ty * ty) / d.z);
                }
                float a01 = Mathf.Clamp01(field * 6f);
                float shade = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(field * 2f));
                byte c = (byte)Mathf.RoundToInt(255f * shade);
                px[y * S + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(255f * a01));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    void HandleTargetHit(bool killed)
    {
        hitTimer = killed ? 0.3f : 0.18f;
        lastHitWasKill = killed;
    }

    // ======================= GRIETAS DEL CRISTAL =======================

    void BuildCracks()
    {
        if (crackTextures == null)
        {
            crackTextures = new Texture2D[CrackThresholds.Length];
            for (int i = 0; i < crackTextures.Length; i++) crackTextures[i] = MakeCrackTexture(1234 + i * 977);
        }
        crackRoot = CreateRect("Grietas", frameRoot, true);
        for (int i = 0; i < cracks.Length; i++)
        {
            Crack c = new Crack();
            c.flash = CreateRaw("Impacto" + i, crackRoot, FXFactory.SoftDot, new Color(1f, 1f, 1f, 0f), false);
            c.img = CreateRaw("Grieta" + i, crackRoot, crackTextures[i % crackTextures.Length], new Color(crackColor.r, crackColor.g, crackColor.b, 0f), false);
            c.img.rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            c.img.gameObject.SetActive(false);
            c.flash.gameObject.SetActive(false);
            cracks[i] = c;
        }
    }

    void UpdateCracks(float dt)
    {
        if (crackRoot == null) return;
        float armor01 = player.maxArmor > 0f ? Mathf.Clamp01(player.currentArmor / player.maxArmor) : 1f;
        Rect r = canvasRect.rect;
        for (int i = 0; i < cracks.Length; i++)
        {
            Crack c = cracks[i];
            float th = CrackThresholds[i];
            bool want = armor01 <= th || player.isDead;
            // Se rompe al cruzar el umbral; solo se va si el traje se repara un poco más allá (sin parpadeos)
            if (want && !c.on)
            {
                c.on = true;
                c.flashTime = 0.45f;
                c.img.rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
                if (!previewMode && !player.isThirdPerson) Sfx.Play2D(Sonido.GolpeTraje, 0.7f, 1.75f, 0.08f, 0.15f);
                glitchTimer = Mathf.Max(glitchTimer, 0.25f);
            }
            else if (c.on && armor01 > th + 0.06f && !player.isDead)
            {
                c.on = false;
            }

            c.shown = Mathf.MoveTowards(c.shown, c.on ? 1f : 0f, dt * (c.on ? 7f : 0.8f));
            c.flashTime = Mathf.Max(0f, c.flashTime - dt);
            bool visible = c.shown > 0.001f;
            if (c.img.gameObject.activeSelf != visible) c.img.gameObject.SetActive(visible);
            bool flashing = c.flashTime > 0f;
            if (c.flash.gameObject.activeSelf != flashing) c.flash.gameObject.SetActive(flashing);
            if (!visible) continue;

            Vector2 pos = new Vector2(CrackSpots[i].x * r.width, CrackSpots[i].y * r.height);
            float size = CrackSizes[i] * r.height;
            // Al romperse la grieta "corre": crece rápido desde el punto de impacto
            float grow = Mathf.SmoothStep(0.35f, 1f, Mathf.Clamp01(c.shown * 1.4f));
            RectTransform rt = c.img.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = Vector2.one * size;
            rt.localScale = Vector3.one * (c.on ? grow : 1f);
            // Bajo el agua se notan menos (el agua llena las grietas)
            float a = crackOpacity * c.shown * (1f - submergedWeight * 0.35f);
            float glint = 0.85f + 0.15f * Mathf.Sin(Now * 1.3f + i * 1.7f);
            c.img.color = new Color(crackColor.r, crackColor.g, crackColor.b, a * glint);

            if (flashing)
            {
                float f = c.flashTime / 0.45f;
                RectTransform fr = c.flash.rectTransform;
                fr.anchoredPosition = pos;
                fr.sizeDelta = Vector2.one * size * Mathf.Lerp(0.9f, 0.25f, f);
                c.flash.color = new Color(1f, 1f, 1f, f * 0.85f);
            }
        }
    }

    // Grieta de vidrio en forma de telaraña: brazos que salen del impacto con quiebres y ramas, anillos
    // irregulares entre los brazos y el punto de impacto astillado. Blanco con un halo oscuro suave.
    static Texture2D MakeCrackTexture(int seed)
    {
        const int S = 256;
        float[] core = new float[S * S];
        float[] halo = new float[S * S];
        System.Random rng = new System.Random(seed);
        System.Func<float, float, float> rnd = (a, b) => a + (float)rng.NextDouble() * (b - a);
        Vector2 center = new Vector2(S * 0.5f, S * 0.5f);

        int arms = 7 + rng.Next(3);
        float[] armAngle = new float[arms];
        List<Vector2>[] armPoints = new List<Vector2>[arms];
        for (int i = 0; i < arms; i++)
        {
            float ang = (i + rnd(-0.3f, 0.3f)) / arms * Mathf.PI * 2f;
            armAngle[i] = ang;
            float length = S * rnd(0.38f, 0.5f);
            List<Vector2> pts = new List<Vector2> { center };
            Vector2 p = center;
            float traveled = 0f;
            while (traveled < length)
            {
                // Tramos casi rectos con algún quiebre en ángulo (así se raja el vidrio)
                float seg = rnd(10f, 22f);
                ang += rng.NextDouble() < 0.22 ? rnd(-0.45f, 0.45f) : rnd(-0.1f, 0.1f);
                Vector2 q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * seg;
                float w = Mathf.Lerp(2.6f, 0.7f, traveled / length);
                CrackLine(core, halo, S, p, q, w);
                // Ramas cortas que se abren hacia los lados
                if (traveled > S * 0.08f && rng.NextDouble() < 0.18)
                {
                    float bang = ang + (rng.NextDouble() < 0.5 ? -1f : 1f) * rnd(0.45f, 0.9f);
                    Vector2 b0 = q;
                    float blen = rnd(12f, 34f);
                    for (float t = 0f; t < blen; t += 8f)
                    {
                        bang += rnd(-0.08f, 0.08f);
                        Vector2 b1 = b0 + new Vector2(Mathf.Cos(bang), Mathf.Sin(bang)) * 8f;
                        CrackLine(core, halo, S, b0, b1, Mathf.Lerp(w * 0.7f, 0.4f, t / blen));
                        b0 = b1;
                    }
                }
                p = q;
                traveled += seg;
                pts.Add(p);
            }
            armPoints[i] = pts;
        }

        // Anillos de la telaraña: tramos rectos entre brazos vecinos a tres distancias del centro
        float[] ringRadius = { S * rnd(0.07f, 0.1f), S * rnd(0.16f, 0.21f), S * rnd(0.27f, 0.33f) };
        foreach (float rr in ringRadius)
        {
            for (int i = 0; i < arms; i++)
            {
                if (rng.NextDouble() < (rr < S * 0.12f ? 0.1 : 0.3)) continue;   // anillos incompletos
                Vector2 a = PointAtRadius(armPoints[i], center, rr * rnd(0.9f, 1.1f));
                Vector2 b = PointAtRadius(armPoints[(i + 1) % arms], center, rr * rnd(0.9f, 1.1f));
                CrackLine(core, halo, S, a, b, 1f);
            }
        }

        // Impacto: un círculo astillado
        for (int k = 0; k < 14; k++)
        {
            float ang = rnd(0f, Mathf.PI * 2f);
            Vector2 a = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rnd(1f, 4f);
            Vector2 b = center + new Vector2(Mathf.Cos(ang + rnd(-0.6f, 0.6f)), Mathf.Sin(ang + rnd(-0.6f, 0.6f))) * rnd(5f, 10f);
            CrackLine(core, halo, S, a, b, 1.1f);
        }

        Color32[] px = new Color32[S * S];
        for (int i = 0; i < px.Length; i++)
        {
            float c = Mathf.Clamp01(core[i]);
            float h = Mathf.Clamp01(halo[i]) * 0.4f;
            float alpha = Mathf.Max(c, h);
            // Núcleo blanco; el halo es más oscuro (da relieve a la grieta)
            float bright = alpha > 0.001f ? Mathf.Lerp(0.25f, 1f, c / alpha) : 0f;
            byte v = (byte)(bright * 255f);
            px[i] = new Color32(v, v, v, (byte)(alpha * 255f));
        }
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.name = "FX_GrietaCristal";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static Vector2 PointAtRadius(List<Vector2> pts, Vector2 center, float radius)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            if ((pts[i] - center).magnitude >= radius)
            {
                float d0 = (pts[i - 1] - center).magnitude, d1 = (pts[i] - center).magnitude;
                float t = d1 - d0 > 0.001f ? (radius - d0) / (d1 - d0) : 0f;
                return Vector2.Lerp(pts[i - 1], pts[i], Mathf.Clamp01(t));
            }
        }
        return pts[pts.Count - 1];
    }

    // Línea con borde suave (antialias) en el núcleo y un halo más ancho
    static void CrackLine(float[] core, float[] halo, int S, Vector2 a, Vector2 b, float width)
    {
        float hw = width * 0.5f;
        float haloW = width * 0.5f + 2.2f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - haloW - 1f));
        int x1 = Mathf.Min(S - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + haloW + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - haloW - 1f));
        int y1 = Mathf.Min(S - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + haloW + 1f));
        Vector2 ab = b - a;
        float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = (p - (a + ab * t)).magnitude;
                int i = y * S + x;
                float c = Mathf.Clamp01(hw + 0.6f - d);
                if (c > core[i]) core[i] = c;
                float h = Mathf.Clamp01(1f - d / haloW);
                if (h > halo[i]) halo[i] = h;
            }
        }
    }

    // ======================= VISTA PREVIA EN EL EDITOR =======================
    // La llama HelmetPreviewWindow sobre la copia oculta del visor (nunca sobre el de la escena).

    public void BuildPreview()
    {
        previewMode = true;
        canvasRect = (RectTransform)transform;
        BuildAll();
        if (player != null) lastArmor = player.currentArmor;   // sin "golpe" al armarse con el traje a medias
        // Sin la secuencia de encendido: se ve directamente como en pleno juego
        bootTimer = bootDuration + 2f;
        hudGroup.alpha = 1f;
    }

    public void PreviewTick(float dt)
    {
        Tick(dt);
        if (ammoHud != null) ammoHud.Refresh(dt);
        if (statusHud != null) statusHud.Refresh(dt);
        if (missionHud != null) missionHud.Refresh(dt);
        if (sonarHud != null) sonarHud.Tick();
    }

    public void PreviewBoot() { bootTimer = 0f; }
    public void PreviewHit() { glitchTimer = 0.35f; }
    public void PreviewDroplets() { SpawnDroplets(dropletsOnExit); }
    public void PreviewSplat() { AddSplat(); }

    // Borra las texturas que arma este visor (las de los demás paneles se comparten y se quedan)
    public void DestroyPreviewTextures()
    {
        if (!previewMode) return;
        Texture[] owned =
        {
            frameImage != null ? frameImage.texture : null,
            glassTint != null ? glassTint.texture : null,
            reflection != null ? reflection.texture : null,
            scanlines != null ? scanlines.texture : null,
            rimSheen != null ? rimSheen.texture : null,
            reticleRing != null ? reticleRing.texture : null
        };
        if (chargeRing != null && chargeRing.sprite != null)
        {
            Sprite sp = chargeRing.sprite;
            chargeRing.sprite = null;
            DestroyImmediate(sp);
        }
        foreach (Texture t in owned)
        {
            if (t == null || t == FXFactory.White || t == FXFactory.SoftDot || t == FXFactory.Bubble || t == FXFactory.Streak) continue;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(t)) continue;
#endif
            DestroyImmediate(t);
        }
    }

#if UNITY_EDITOR
    // Avisa a la vista previa del casco que cambiaste algo en el Inspector (para regenerarla)
    public static event System.Action<HelmetVisorHUD> EditorValidated;

    void OnValidate()
    {
        if (previewMode || (gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0) return;
        if (EditorValidated != null) EditorValidated(this);
    }
#endif
}
