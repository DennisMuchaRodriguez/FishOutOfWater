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
    public Color accent = new Color(0.41f, 0.87f, 0.9f, 1f);
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
    CanvasGroup hudGroup;
    RectTransform reticleRoot;
    RawImage reticleRing, reticleDot;
    RawImage[] reticleTicks = new RawImage[4];
    RawImage[] hitTicks = new RawImage[4];
    Image chargeRing;
    TextMeshProUGUI statusText, readoutText;
    VisorAmmoHUD ammoHud;
    VisorSonarHUD sonarHud;
    readonly List<Droplet> droplets = new List<Droplet>();
    RectTransform dropletRoot;

    Vector2 swayOffset;
    float lastYaw;
    float bootTimer;
    float glitchTimer;
    float hitTimer;
    bool lastHitWasKill;
    float lastArmor;
    float submergedWeight;
    float reticleSpreadPx;
    string bootMessage = "SISTEMAS DEL TRAJE EN LÍNEA";

    class Droplet
    {
        public RawImage img;
        public Vector2 pos;
        public float speed, life, maxLife, size;
    }

    void Start()
    {
        canvasRect = (RectTransform)transform;
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();
        if (shooting == null && player != null) shooting = player.GetComponent<PlayerShooting>();

        BuildFrame();
        BuildSwayRoot();
        BuildOverlay();

        // Munición (arco del borde derecho) y sonar, dentro del visor
        ammoHud = gameObject.AddComponent<VisorAmmoHUD>();
        ammoHud.visor = this;
        ammoHud.player = player;
        ammoHud.shooting = shooting;
        ammoHud.Build(swayRoot, accent, dangerColor);
        sonarHud = gameObject.AddComponent<VisorSonarHUD>();
        sonarHud.player = player;
        sonarHud.Build(overlayRoot, accent);

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

        frameImage = CreateRaw("HelmetFrame", frameRoot, MakeFrameTexture(), Color.white, true);
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
        if (player == null) return;
        float dt = Time.deltaTime;
        bool firstPerson = !player.isThirdPerson;
        bool cinematic = GameDirector.InCinematic;

        // Durante la cinemática se apaga TODO el canvas del casco (marco, HUD, filtros):
        // la cinemática es una vista de cámara externa, no desde dentro del casco
        if (visorCanvas == null) visorCanvas = GetComponent<Canvas>();
        if (visorCanvas != null) visorCanvas.enabled = !cinematic;
        frameRoot.gameObject.SetActive(firstPerson && !cinematic);
        overlayRoot.gameObject.SetActive(!cinematic);
        swayRoot.gameObject.SetActive(!cinematic);

        UpdateBoot(dt);
        UpdateSway(dt, firstPerson);
        UpdateGlass(dt);
        UpdateDamage(dt);
        UpdateReticle(dt);
        UpdateTexts();
        UpdateDroplets(dt);
        UpdateCursor();
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
            target.y += Mathf.Sin(Time.time * 1.4f) * breathingAmount;
            if (player.isInWater) target.x += Mathf.Sin(Time.time * 2.1f) * breathingAmount * (0.5f + player.SwimSpeed01);
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

    void UpdateGlass(float dt)
    {
        // Al hundirse el tinte entra casi de golpe (igual que el filtro); al salir se va más despacio
        submergedWeight = Mathf.MoveTowards(submergedWeight, player.IsEyeSubmerged ? 1f : 0f, dt * (player.IsEyeSubmerged ? 10f : 2.5f));
        float caustic = 0.85f + Mathf.Sin(Time.time * 1.7f) * 0.1f + Mathf.Sin(Time.time * 3.1f) * 0.05f;
        glassTint.color = new Color(waterTint.r, waterTint.g, waterTint.b, submergedWeight * underwaterTintOpacity * caustic);

        // Líneas de escaneo que se desplazan lentamente
        float lines = Mathf.Max(1f, canvasRect.rect.height / 3f);
        scanlines.uvRect = new Rect(0f, Time.time * 0.6f, 1f, lines);

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
        if (armor01 <= 0.25f || player.isDead)
        {
            float pulse = player.isDead ? 1f : (Mathf.Sin(Time.time * 4f) * 0.5f + 0.5f);
            rim = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.45f, 1f), pulse);
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
        if (reticleFollowsMouse)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out local);
        }
        reticleRoot.anchoredPosition = local;

        // Dispersión -> separación de las marcas
        float spreadPx = 8f + shooting.CurrentSpread * 7f;
        float sinceShot = Time.time - shooting.LastShotTime;
        if (sinceShot < 0.08f) spreadPx += 6f;
        reticleSpreadPx = Mathf.Lerp(reticleSpreadPx, spreadPx, 1f - Mathf.Exp(-18f * dt));
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f * Mathf.Deg2Rad;
            reticleTicks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * reticleSpreadPx;
        }

        bool noAmmo = shooting.currentAmmo <= 0;
        Color baseC = noAmmo ? dangerColor : accent;
        reticleRing.color = new Color(baseC.r, baseC.g, baseC.b, 0.55f + (sinceShot < 0.1f ? 0.4f : 0f));
        reticleRing.rectTransform.localEulerAngles = new Vector3(0f, 0f, Time.time * 20f);
        reticleDot.color = baseC;
        foreach (RawImage t in reticleTicks) t.color = new Color(baseC.r, baseC.g, baseC.b, 0.9f);

        // Anillo de carga
        float charge = shooting.ChargeProgress01;
        chargeRing.fillAmount = charge;
        bool full = charge >= 1f;
        chargeRing.color = full ? Color.Lerp(accent, Color.white, Mathf.Sin(Time.time * 25f) * 0.5f + 0.5f) : new Color(accent.r, accent.g, accent.b, 0.85f);
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
            else if (shooting != null && shooting.currentAmmo <= 0)
            {
                msg = "SIN MUNICIÓN  //  BUSCA CÁPSULAS EN EL LAGO"; warn = true;
            }
            else if (player.IsTurbo)
            {
                msg = energy01 < 0.25f ? "TURBO  //  COMBUSTIBLE BAJO" : "TURBO";
                warn = energy01 < 0.25f;
            }
            else if (shooting != null && shooting.currentAmmo <= 5)
            {
                msg = "MUNICIÓN BAJA  //  BUSCA CÁPSULAS";
            }
            else if (player.isInWater && energy01 < 0.999f)
            {
                msg = "HIDRO-RECARGA EN CURSO";
            }

            statusText.text = msg;
            Color c = warn ? dangerColor : accent;
            float a = warn ? (Mathf.Sin(Time.time * 8f) > -0.2f ? 0.95f : 0.25f) : 0.85f;
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

    void HandleTargetHit(bool killed)
    {
        hitTimer = killed ? 0.3f : 0.18f;
        lastHitWasKill = killed;
    }
}
