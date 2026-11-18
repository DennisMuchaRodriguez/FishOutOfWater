using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Panel de la misión dentro del visor del casco, en el borde izquierdo (el espejo de la munición, que va a la derecha):
//  - Arco de celdas: una celda por pez (o por grupo de peces si son muchos). Las encendidas son los peces a salvo
//    y se apagan de arriba hacia abajo cuando cazan uno. La marca roja es el límite: si las celdas encendidas
//    llegan a ella, se pierde el nivel.
//  - Número grande con los peces a salvo, "/ total" y PECES A SALVO (en rojo y parpadeando cerca del límite).
//  - OLEADA 2 / 3 con un rombo por oleada (hechas, la actual latiendo y las que faltan).
//  - Depredadores vivos (o ZONA DESPEJADA) y la cuenta atrás de la próxima oleada.
// Lo crea HelmetVisorHUD dentro de "VisorSway" (se mueve con el casco). SOLO se ve en primera persona, como el
// resto del casco: en tercera persona no hay panel de misión.
// Mientras exista, GameHUD no muestra sus objetivos viejos de abajo a la derecha (ni en tercera persona).
public class VisorMissionHUD : MonoBehaviour
{
    // ---- Medidas en unidades del canvas del visor (800 de ancho), igual que VisorAmmoHUD ----
    const float R = 300f;             // radio del arco (el centro queda a la derecha, fuera del panel)
    const float Half = 19f;           // medio ángulo del arco (grados)
    const float CellLen = 13f;        // largo radial de cada celda
    const float CellGap = 1.6f;
    const int MaxCells = 40;
    const int GuideDashes = 26;
    const float TextX = 30f;          // borde izquierdo de los textos (por dentro de las celdas)
    const float LabelY = 40f, NumY = 15f, MaxY = -10f, WaveY = -36f, PipY = -50f, BirdsY = -70f, DefY = -86f, CountY = -102f;
    const float NumSize = 34f, MaxSize = 12f, LabelSize = 9f, SmallSize = 9f;
    const float FpMargin = 14f, YOff = 18f;

    static readonly Color Navy = new Color(0.006f, 0.02f, 0.06f, 1f);
    static readonly Color Shine = new Color(0.82f, 0.95f, 1f, 1f);

    public static VisorMissionHUD Instance { get; private set; }
    // GameHUD lo consulta: con el casco puesto, los objetivos viejos no se muestran nunca
    public static bool IsActive { get { return Instance != null && Instance.isActiveAndEnabled; } }
    public static bool IsShowing { get { return IsActive && Instance.showing; } }

    [HideInInspector] public HelmetVisorHUD visor;
    [HideInInspector] public PlayerController_Base player;

    // Lo que muestra el panel: en el juego sale del GameDirector; en la vista previa del editor, de previewSample
    public struct Sample
    {
        public int totalFish, fishAlive, maxFishLoss, wave, totalWaves, birds;
        public int defenders, defendersTotal;   // peces defensores con traje / los que hubo en el nivel (0 = no hay)
        public float countdown;
        public bool waveActive;   // oleada en curso (las pastillas laten)
        public bool waiting;      // calma o descanso entre oleadas (se ve la cuenta atrás)

        public static Sample Example
        {
            get
            {
                return new Sample { totalFish = 24, fishAlive = 17, maxFishLoss = 17, wave = 2, totalWaves = 4, birds = 5, waveActive = true,
                                    defenders = 3, defendersTotal = 4 };
            }
        }

        public static Sample From(GameDirector d)
        {
            return new Sample
            {
                totalFish = d.TotalFish,
                fishAlive = d.FishAlive,
                maxFishLoss = d.MaxFishLoss,
                wave = d.CurrentWave,
                totalWaves = d.TotalWaves,
                birds = d.AliveBirds(),
                defenders = d.DefendersAlive,
                defendersTotal = d.DefendersSpawned,
                countdown = d.Countdown,
                waveActive = d.State == GameDirector.GameState.WaveActive || d.State == GameDirector.GameState.WaveIncoming,
                waiting = d.State == GameDirector.GameState.Calm || d.State == GameDirector.GameState.Intermission
            };
        }
    }
    [System.NonSerialized] public bool previewMode;
    [System.NonSerialized] public Sample previewSample;

    Color accent = new Color(0.25f, 0.72f, 1f, 1f);
    Color danger = new Color(1f, 0.15f, 0.1f, 1f);

    RectTransform root, swayParent, cellRoot, pipRoot;
    RawImage plate, limitTick, limitGlow, birdIcon, labelLine, labelLineBright;
    readonly List<RawImage> cells = new List<RawImage>();
    readonly List<RawImage> guide = new List<RawImage>();
    readonly List<Image> pips = new List<Image>();
    TextMeshProUGUI label, number, maxText, waveText, birdsText, countText, defText;
    RawImage defIcon;
    int shownDefenders = -1, lastDefenders = -1;
    float defFlash;
    int builtCells = -1, builtTotal = -1, fishPerCell = 1, builtWaves = -1, builtLimit = -1;
    int lastAlive = -1, shownAlive = -1, shownTotal = -1, shownWave = -1, shownBirds = -1, shownCount = -1;
    float lossFlash;
    bool showing;
    static Texture2D triangleTex;

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // flat: ya no se usa (antes era el lugar del panel en tercera persona); se deja para no cambiar la llamada
    public void Build(RectTransform sway, RectTransform flat, Color accentColor, Color dangerColor)
    {
        accent = accentColor;
        danger = dangerColor;
        swayParent = sway;
        if (triangleTex == null) triangleTex = MakeTriangle(32);

        root = NewRect("VisorMision", swayParent);
        root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
        root.sizeDelta = Vector2.zero;

        // Placa oscura para que se lea sobre el cielo
        plate = Raw("Placa", root, FXFactory.SoftDot, new Color(Navy.r, Navy.g, Navy.b, 0.6f));
        Place(plate.rectTransform, 80f, -18f, 250f, 230f);

        // Guía punteada por fuera del arco
        for (int i = 0; i < GuideDashes; i++)
        {
            float t = (i + 0.5f) / GuideDashes;
            float ang = (180f + Half - t * 2f * Half) * Mathf.Deg2Rad;
            Vector2 p = ArcPoint(ang, R + 4.5f);
            RawImage d = Raw("Guia" + i, root, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.35f));
            PlaceRot(d.rectTransform, p, 1f, (2f * Half * Mathf.Deg2Rad * R / GuideDashes) * 0.55f, ang * Mathf.Rad2Deg);
            guide.Add(d);
        }

        cellRoot = NewRect("Celdas", root);

        limitGlow = Raw("LimiteBrillo", root, FXFactory.SoftDot, new Color(danger.r, danger.g, danger.b, 0.35f));
        limitTick = Raw("Limite", root, FXFactory.White, danger);

        label = Text("Nombre", root, LabelSize, MenuUI.BodyFont, 8f);
        PlaceText(label, TextX, LabelY, 170f, 14f);
        labelLine = Raw("LineaNombre", root, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.35f));
        Place(labelLine.rectTransform, TextX + 60f, LabelY - 8f, 120f, 1f);
        labelLineBright = Raw("LineaNombreBrillo", root, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.9f));
        Place(labelLineBright.rectTransform, TextX + 7f, LabelY - 8f, 14f, 1.6f);

        number = Text("Numero", root, NumSize, MenuUI.TitleFont, 2f);
        PlaceText(number, TextX, NumY, 140f, 44f);
        maxText = Text("Total", root, MaxSize, MenuUI.BodyFont, 4f);
        PlaceText(maxText, TextX + 2f, MaxY, 160f, 18f);

        waveText = Text("Oleada", root, SmallSize, MenuUI.BodyFont, 7f);
        PlaceText(waveText, TextX, WaveY, 170f, 14f);
        pipRoot = NewRect("Oleadas", root);

        birdIcon = Raw("IconoAves", root, triangleTex, danger);
        Place(birdIcon.rectTransform, TextX + 5f, BirdsY, 10f, 9f);
        birdIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
        birdsText = Text("Depredadores", root, SmallSize, MenuUI.BodyFont, 6f);
        PlaceText(birdsText, TextX + 15f, BirdsY, 170f, 14f);

        // Peces defensores que siguen con traje (solo si en el nivel hay)
        defIcon = Raw("IconoDefensores", root, FXFactory.SoftDot, accent);
        Place(defIcon.rectTransform, TextX + 5f, DefY, 9f, 9f);
        defText = Text("Defensores", root, SmallSize, MenuUI.BodyFont, 6f);
        PlaceText(defText, TextX + 15f, DefY, 170f, 14f);

        countText = Text("Cuenta", root, SmallSize, MenuUI.BodyFont, 5f);
        PlaceText(countText, TextX, CountY, 190f, 14f);

        Refresh(0f);
    }

    void Update()
    {
        if (root == null) return;
        Refresh(Time.deltaTime);
    }

    // ======================= ACTUALIZACIÓN =======================

    // También la llama la vista previa del casco en el editor
    public void Refresh(float dt)
    {
        GameDirector director = GameDirector.Instance;
        bool hasData = previewMode || director != null;
        Sample d = previewMode ? previewSample : (director != null ? Sample.From(director) : new Sample());
        // Solo en primera persona (dentro del casco), como el resto del visor
        showing = hasData && d.totalFish > 0 && player != null && !player.isDead && !GameDirector.InCinematic
                  && !player.isThirdPerson;
        if (root.gameObject.activeSelf != showing) root.gameObject.SetActive(showing);
        if (!showing) return;

        if (root.parent != swayParent) root.SetParent(swayParent, false);
        RectTransform canvasRect = (RectTransform)root.parent;
        float cw = Mathf.Max(1f, canvasRect.rect.width);
        float ch = Mathf.Max(1f, canvasRect.rect.height);
        float s = Mathf.Clamp(ch / 450f, 0.72f, 1f);
        float visorSize = visor != null ? visor.visorSize : 0.97f;
        float inset = (1f - visorSize) * cw * 0.5f + FpMargin;
        root.anchoredPosition = new Vector2(inset, YOff * s);
        root.localScale = Vector3.one * s;

        int total = d.totalFish;
        int alive = Mathf.Clamp(d.fishAlive, 0, total);
        int limitAlive = Mathf.Clamp(total - d.maxFishLoss, 0, total);   // con esta cantidad o menos se pierde
        if (total != builtTotal) BuildCells(total);
        if (limitAlive != builtLimit) PlaceLimit(limitAlive);

        if (lastAlive >= 0 && alive < lastAlive) lossFlash = 0.7f;
        lastAlive = alive;
        lossFlash = Mathf.Max(0f, lossFlash - dt);
        float flash = lossFlash / 0.7f;

        // Cerca del límite: rojo y parpadeo
        int margin = alive - limitAlive;
        bool critical = margin <= Mathf.Max(2, total / 8);
        bool blinkOn = !critical || Mathf.Repeat(HelmetVisorHUD.Now * 3f, 1f) < 0.65f;
        Color baseC = critical ? danger : accent;

        // Celdas: encendidas = peces a salvo (de abajo arriba). Un destello las recorre cada pocos segundos
        int n = cells.Count;
        float lit = alive / (float)fishPerCell;
        float sweepK = Mathf.Repeat(HelmetVisorHUD.Now + 1.7f, 3.4f) / 0.75f;
        float sweepPos = sweepK < 1f ? Mathf.Lerp(-3f, n + 3f, sweepK) : -100f;
        int top = Mathf.CeilToInt(lit) - 1;
        for (int i = 0; i < n; i++)
        {
            float v = Mathf.Clamp01(lit - i);
            Color c;
            if (v > 0f)
            {
                float a = blinkOn ? 0.35f + 0.6f * v : 0.3f;
                c = new Color(baseC.r, baseC.g, baseC.b, a);
                float sh = Mathf.Exp(-(i - sweepPos) * (i - sweepPos) * 0.18f);
                if (!critical && sh > 0.01f) c = Color.Lerp(c, Shine, sh * 0.7f);
            }
            else
            {
                // Peces perdidos: celda apagada; la que se acaba de perder destella
                c = new Color(danger.r, danger.g, danger.b, 0.16f);
                if (flash > 0f && i == top + 1) c = Color.Lerp(c, Color.white, flash);
            }
            cells[i].color = c;
        }
        for (int i = 0; i < guide.Count; i++) guide[i].color = new Color(baseC.r, baseC.g, baseC.b, 0.35f);

        float pulse = Mathf.Sin(HelmetVisorHUD.Now * 5f) * 0.5f + 0.5f;
        limitTick.color = new Color(danger.r, danger.g, danger.b, critical ? 0.6f + 0.4f * pulse : 0.85f);
        limitGlow.color = new Color(danger.r, danger.g, danger.b, critical ? 0.25f + 0.35f * pulse : 0.2f);

        // Textos
        if (alive != shownAlive)
        {
            number.text = alive.ToString();
            shownAlive = alive;
        }
        Color numC = Color.Lerp(baseC, Color.white, flash * 0.6f);
        number.color = new Color(numC.r, numC.g, numC.b, blinkOn ? 1f : 0.35f);
        number.rectTransform.localScale = Vector3.one * (1f + flash * 0.15f);
        if (total != shownTotal)
        {
            maxText.text = "/ " + total;
            shownTotal = total;
        }
        maxText.color = new Color(accent.r, accent.g, accent.b, 0.8f);
        string lbl = critical ? "¡CARDUMEN EN PELIGRO!" : "PECES A SALVO";
        if (label.text != lbl) label.text = lbl;
        label.color = new Color(baseC.r, baseC.g, baseC.b, blinkOn ? 0.95f : 0.4f);
        labelLine.color = new Color(baseC.r, baseC.g, baseC.b, 0.35f);
        labelLineBright.color = new Color(baseC.r, baseC.g, baseC.b, 0.9f);

        RefreshWaves(d);
        RefreshBirds(d);
        RefreshDefenders(d, dt);
        RefreshCountdown(d);
    }

    void RefreshWaves(Sample d)
    {
        int waves = Mathf.Max(0, d.totalWaves);
        if (waves != builtWaves) BuildPips(waves);
        int wave = Mathf.Clamp(d.wave, 0, waves);
        if (wave != shownWave)
        {
            waveText.text = "OLEADA  " + (wave > 0 ? wave.ToString() : "-") + " / " + waves;
            shownWave = wave;
        }
        waveText.color = new Color(accent.r, accent.g, accent.b, 0.95f);

        bool active = d.waveActive;
        float pulse = Mathf.Sin(HelmetVisorHUD.Now * 6f) * 0.5f + 0.5f;
        for (int i = 0; i < pips.Count; i++)
        {
            int w = i + 1;
            Color c;
            if (w < wave || (w == wave && !active)) c = accent;                                // ya superada
            else if (w == wave) c = Color.Lerp(accent, Shine, pulse * 0.8f);                    // en curso
            else c = new Color(accent.r, accent.g, accent.b, 0.18f);                            // por venir
            pips[i].color = c;
            pips[i].rectTransform.localScale = Vector3.one * (w == wave && active ? 1f + 0.25f * pulse : 1f);
        }
    }

    void RefreshBirds(Sample d)
    {
        int birds = d.birds;
        if (birds != shownBirds)
        {
            birdsText.text = birds > 0 ? "DEPREDADORES  " + birds : "ZONA DESPEJADA";
            shownBirds = birds;
        }
        Color c = birds > 0 ? danger : accent;
        birdsText.color = new Color(c.r, c.g, c.b, 0.95f);
        birdIcon.enabled = birds > 0;
        birdIcon.color = new Color(danger.r, danger.g, danger.b, 0.6f + 0.4f * (Mathf.Sin(HelmetVisorHUD.Now * 7f) * 0.5f + 0.5f));
        // Sin depredadores el texto empieza donde iría el triángulo
        birdsText.rectTransform.anchoredPosition = new Vector2(birds > 0 ? TextX + 15f : TextX, BirdsY);
    }

    void RefreshDefenders(Sample d, float dt)
    {
        bool show = d.defendersTotal > 0;
        if (defText.gameObject.activeSelf != show)
        {
            defText.gameObject.SetActive(show);
            defIcon.gameObject.SetActive(show);
        }
        if (!show) return;
        if (lastDefenders >= 0 && d.defenders < lastDefenders) defFlash = 1.2f;
        lastDefenders = d.defenders;
        defFlash = Mathf.Max(0f, defFlash - dt);
        if (d.defenders != shownDefenders)
        {
            defText.text = d.defenders > 0 ? "DEFENSORES  " + d.defenders + " / " + d.defendersTotal : "SIN DEFENSORES";
            shownDefenders = d.defenders;
        }
        // Al caer uno destella en rojo; sin defensores queda apagado
        bool blink = defFlash > 0f && Mathf.Repeat(HelmetVisorHUD.Now * 6f, 1f) < 0.6f;
        Color c = blink ? danger : (d.defenders > 0 ? accent : new Color(danger.r, danger.g, danger.b, 0.6f));
        defText.color = new Color(c.r, c.g, c.b, 0.95f);
        defIcon.color = new Color(c.r, c.g, c.b, 0.6f + 0.4f * (Mathf.Sin(HelmetVisorHUD.Now * 3f) * 0.5f + 0.5f));
    }

    void RefreshCountdown(Sample d)
    {
        bool show = d.waiting && d.countdown > 0f;
        if (countText.gameObject.activeSelf != show) countText.gameObject.SetActive(show);
        if (!show) return;
        int secs = Mathf.CeilToInt(d.countdown);
        if (secs != shownCount)
        {
            countText.text = "PRÓXIMA OLEADA  " + secs;
            shownCount = secs;
        }
        bool urgent = secs <= 3;
        float blink = Mathf.Repeat(HelmetVisorHUD.Now * (urgent ? 4f : 1.5f), 1f) < 0.6f ? 1f : 0.45f;
        Color c = urgent ? Color.Lerp(accent, Shine, 0.6f) : accent;
        countText.color = new Color(c.r, c.g, c.b, blink);
    }

    // ======================= CONSTRUCCIÓN =======================

    void BuildCells(int total)
    {
        foreach (RawImage c in cells) if (c != null) HelmetVisorHUD.Kill(c.gameObject);
        cells.Clear();
        fishPerCell = Mathf.Max(1, Mathf.CeilToInt(total / (float)MaxCells));
        int n = Mathf.Max(1, Mathf.CeilToInt(total / (float)fishPerCell));
        float step = 2f * Half / n;
        float thick = Mathf.Max(1.2f, step * Mathf.Deg2Rad * R - CellGap);
        for (int i = 0; i < n; i++)
        {
            float ang = (180f + Half - (i + 0.5f) * step) * Mathf.Deg2Rad;   // la celda 0 queda abajo
            Vector2 p = ArcPoint(ang, R - CellLen * 0.5f);
            RawImage c = Raw("Celda" + i, cellRoot, FXFactory.White, accent);
            PlaceRot(c.rectTransform, p, CellLen, thick, ang * Mathf.Rad2Deg);
            cells.Add(c);
        }
        builtCells = n;
        builtTotal = total;
        builtLimit = -1;
        lastAlive = -1;
    }

    // Marca roja en el borde entre las celdas que ya hacen perder y las que todavía salvan el nivel
    void PlaceLimit(int limitAlive)
    {
        builtLimit = limitAlive;
        int n = Mathf.Max(1, builtCells);
        float b = Mathf.Clamp(limitAlive / (float)fishPerCell, 0f, n);
        float ang = (180f + Half - b * (2f * Half / n)) * Mathf.Deg2Rad;
        Vector2 p = ArcPoint(ang, R - CellLen * 0.5f + 1f);
        PlaceRot(limitTick.rectTransform, p, CellLen + 12f, 2f, ang * Mathf.Rad2Deg);
        Place(limitGlow.rectTransform, p.x, p.y, 30f, 14f);
        limitGlow.rectTransform.localEulerAngles = new Vector3(0f, 0f, ang * Mathf.Rad2Deg);
    }

    void BuildPips(int waves)
    {
        foreach (Image p in pips) if (p != null) HelmetVisorHUD.Kill(p.gameObject);
        pips.Clear();
        builtWaves = waves;
        for (int i = 0; i < waves; i++)
        {
            Image img = NewRect("Oleada" + (i + 1), pipRoot).gameObject.AddComponent<Image>();
            img.sprite = MenuUI.Diamond;
            img.raycastTarget = false;
            Place(img.rectTransform, TextX + 5f + i * 14f, PipY, 10f, 10f);
            pips.Add(img);
        }
    }

    // Punto del arco: el centro está a la derecha (R, 0), así que el arco se curva hacia el borde del casco
    static Vector2 ArcPoint(float angRad, float radius)
    {
        return new Vector2(R + Mathf.Cos(angRad) * radius, Mathf.Sin(angRad) * radius);
    }

    // ======================= AYUDAS =======================

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    static RawImage Raw(string name, Transform parent, Texture tex, Color color)
    {
        RawImage img = NewRect(name, parent).gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void PlaceRot(RectTransform rt, Vector2 c, float w, float h, float deg)
    {
        rt.anchoredPosition = c;
        rt.sizeDelta = new Vector2(w, h);
        rt.localEulerAngles = new Vector3(0f, 0f, deg);
    }

    // Textos alineados a la izquierda desde x
    static void PlaceText(TextMeshProUGUI t, float x, float y, float w, float h)
    {
        RectTransform rt = t.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        t.alignment = TextAlignmentOptions.MidlineLeft;
    }

    TextMeshProUGUI Text(string name, Transform parent, float size, TMP_FontAsset font, float spacing)
    {
        TextMeshProUGUI t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.characterSpacing = spacing;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        t.color = accent;
        t.text = "";
        return t;
    }

    // Triángulo apuntando hacia arriba (se gira 180° para que apunte abajo, como los marcadores de las aves)
    static Texture2D MakeTriangle(int s)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.name = "UI_TrianguloMision";
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                float halfWidth = (1f - v) * 0.5f;
                float edge = Mathf.Max(Mathf.Abs(u - 0.5f) - halfWidth, 0.06f - v);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(-edge * s * 0.6f)));
            }
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return tex;
    }
}
