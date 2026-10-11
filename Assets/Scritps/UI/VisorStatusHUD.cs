using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Propulsor (jetpack) y estado del traje dentro del visor del casco, con el mismo estilo que la munición:
//  - Dos medidores de celdas que siguen la curva de arriba del visor: PROPULSOR a la izquierda y
//    TRAJE a la derecha (el mismo lugar que tenían las barras viejas)
//  - Se llenan desde la esquina del casco hacia el centro; número grande con % y su nombre
//  - Propulsor: un brillo recorre las celdas mientras se recarga en el agua y la última celda
//    titila con el turbo. Con poca carga parpadea en rojo
//  - Traje: al recibir daño las celdas perdidas destellan y se apagan despacio (se ve cuánto quitó el golpe);
//    con poca armadura parpadea en rojo y dice CRÍTICO
// Lo crea HelmetVisorHUD dentro de "VisorSway": se mueve con la inercia del casco y arranca con su encendido.
public class VisorStatusHUD : MonoBehaviour
{
    // ---- Medidas en unidades del canvas del visor (800 de ancho) ----
    const int Cells = 20;
    const float U0 = 0.55f, U1 = 0.86f;   // tramo del borde de arriba del visor (fracción del medio ancho)
    const float Inset = 17f;             // separación entre el borde del visor y las celdas
    const float CellT = 9f;              // grosor de las celdas (hacia dentro del visor)
    const float CellGap = 1.8f;
    const float TickLen = 4f, TickT = 1.2f;
    const float LabelOff = 13f;          // nombre: por debajo de las celdas
    const float NumberOff = 31f;         // número: debajo del nombre
    const float NumSize = 22f, LabelSize = 7.5f;
    const float Low = 0.25f;

    static readonly Color Navy = new Color(0.01f, 0.035f, 0.06f, 1f);

    [HideInInspector] public HelmetVisorHUD visor;
    [HideInInspector] public PlayerController_Base player;

    Color accent = new Color(0.41f, 0.87f, 0.9f, 1f);
    Color danger = new Color(1f, 0.15f, 0.1f, 1f);
    Color suitColor = new Color(0.33f, 0.89f, 0.61f, 1f);

    class Gauge
    {
        public string name;
        public float side;               // -1 izquierda, +1 derecha
        public Color color;
        public RectTransform root;
        public RawImage plate, guide;
        public readonly List<RawImage> cells = new List<RawImage>();
        public readonly List<RawImage> ticks = new List<RawImage>();
        public TextMeshProUGUI label, number;
        public float ghost = 1f;         // valor que cae despacio después de un golpe
        public float ghostWait;
        public float last = -1f;
        public float heal;               // brillo al recuperar
        public int shownPercent = -1;
        public string shownLabel;
    }

    RectTransform root;
    Gauge fuel, suit;
    float builtW, builtH, builtVisor;

    public void Build(RectTransform parent, Color accentColor, Color dangerColor)
    {
        accent = accentColor;
        danger = dangerColor;
        suitColor = MenuUI.Green;

        root = NewRect("VisorEstado", parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        fuel = MakeGauge("Propulsor", -1f, accent);
        suit = MakeGauge("Traje", 1f, suitColor);
        Refresh(0f);
    }

    Gauge MakeGauge(string name, float side, Color color)
    {
        Gauge g = new Gauge { name = name, side = side, color = color };
        g.root = NewRect(name, root);
        g.plate = Raw("Placa", g.root, FXFactory.SoftDot, new Color(Navy.r, Navy.g, Navy.b, 0.55f));
        g.guide = Raw("Guia", g.root, FXFactory.White, new Color(color.r, color.g, color.b, 0.3f));
        for (int i = 0; i < Cells; i++) g.cells.Add(Raw("Celda" + i, g.root, FXFactory.White, color));
        for (int i = 0; i < 3; i++) g.ticks.Add(Raw("Marca" + i, g.root, FXFactory.White, new Color(color.r, color.g, color.b, 0.7f)));

        g.label = Text("Nombre", g.root, LabelSize, MenuUI.BodyFont, 9f);
        g.number = Text("Numero", g.root, NumSize, MenuUI.TitleFont, 2f);
        return g;
    }

    void Update()
    {
        if (root == null) return;
        Refresh(Time.deltaTime);
    }

    // ======================= ACTUALIZACIÓN =======================

    void Refresh(float dt)
    {
        bool show = player != null && !player.isDead;
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (!show) return;

        RectTransform canvasRect = (RectTransform)root.parent;
        float cw = Mathf.Max(1f, canvasRect.rect.width);
        float ch = Mathf.Max(1f, canvasRect.rect.height);
        float visorSize = visor != null ? visor.visorSize : 0.97f;
        if (!Mathf.Approximately(cw, builtW) || !Mathf.Approximately(ch, builtH) || !Mathf.Approximately(visorSize, builtVisor))
        {
            builtW = cw;
            builtH = ch;
            builtVisor = visorSize;
            Layout(fuel, cw, ch, visorSize);
            Layout(suit, cw, ch, visorSize);
        }

        float fuel01 = player.maxJetpackEnergy > 0f ? Mathf.Clamp01(player.currentJetpackEnergy / player.maxJetpackEnergy) : 0f;
        float suit01 = player.maxArmor > 0f ? Mathf.Clamp01(player.currentArmor / player.maxArmor) : 0f;
        UpdateFuel(fuel01);
        UpdateSuit(suit01, dt);
    }

    void UpdateFuel(float v)
    {
        Gauge g = fuel;
        bool low = v <= Low;
        bool blinkOn = !low || Mathf.Repeat(Time.time * 4f, 1f) < 0.6f;
        bool recharging = player.isInWater && v < 0.999f;
        bool burning = player.IsTurbo || player.IsJetting;
        Color baseC = low ? danger : g.color;
        float lit = v * Cells;
        float sweep = Mathf.Repeat(Time.time * 14f, Cells + 6f) - 3f;   // brillo de la recarga
        int front = Mathf.CeilToInt(lit) - 1;

        for (int i = 0; i < Cells; i++)
        {
            float fill = Mathf.Clamp01(lit - i);
            Color c;
            if (fill > 0f)
            {
                float a = blinkOn ? 0.35f + 0.6f * fill : 0.3f;
                c = new Color(baseC.r, baseC.g, baseC.b, a);
                if (recharging) c = Color.Lerp(c, Color.white, Mathf.Exp(-(i - sweep) * (i - sweep) * 0.35f) * 0.55f);
                if (burning && i == front) c.a *= 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
            }
            else
            {
                c = new Color(g.color.r, g.color.g, g.color.b, 0.12f);
            }
            g.cells[i].color = c;
        }

        SetNumber(g, v, low, blinkOn);
        string label = low ? (v <= 0.001f ? "PROPULSOR VACÍO" : "PROPULSOR BAJO") : (recharging ? "PROPULSOR · RECARGA" : "PROPULSOR");
        SetLabel(g, label, low ? danger : g.color, blinkOn);
        g.guide.color = new Color(baseC.r, baseC.g, baseC.b, 0.3f);
    }

    void UpdateSuit(float v, float dt)
    {
        Gauge g = suit;
        if (g.last < 0f) { g.last = v; g.ghost = v; }
        // Golpe: lo perdido queda marcado un momento y luego se apaga despacio
        if (v < g.last - 0.0001f) g.ghostWait = 0.45f;
        if (v > g.last + 0.0001f) g.heal = Mathf.Min(1f, g.heal + (v - g.last) * 6f + 0.25f);
        g.last = v;
        if (g.ghost < v) g.ghost = v;
        g.ghostWait = Mathf.Max(0f, g.ghostWait - dt);
        if (g.ghostWait <= 0f) g.ghost = Mathf.MoveTowards(g.ghost, v, dt * 0.45f);
        g.heal = Mathf.Max(0f, g.heal - dt * 1.5f);

        bool low = v <= Low;
        bool blinkOn = !low || Mathf.Repeat(Time.time * 4f, 1f) < 0.6f;
        Color baseC = low ? danger : g.color;
        float lit = v * Cells, ghostLit = g.ghost * Cells;
        float hit = g.ghostWait > 0f ? 1f : 0.6f;

        for (int i = 0; i < Cells; i++)
        {
            float fill = Mathf.Clamp01(lit - i);
            Color c;
            if (fill > 0f)
            {
                float a = blinkOn ? 0.35f + 0.6f * fill : 0.3f;
                c = new Color(baseC.r, baseC.g, baseC.b, a);
                if (g.heal > 0f) c = Color.Lerp(c, Color.white, g.heal * 0.35f);
            }
            else if (i < ghostLit)
            {
                // Lo que se llevó el golpe: blanco-rojo que se apaga
                c = Color.Lerp(danger, Color.white, 0.45f);
                c.a = 0.85f * hit;
            }
            else
            {
                c = new Color(g.color.r, g.color.g, g.color.b, 0.12f);
            }
            g.cells[i].color = c;
        }

        SetNumber(g, v, low, blinkOn);
        SetLabel(g, low ? "TRAJE · CRÍTICO" : "TRAJE", low ? danger : g.color, blinkOn);
        g.guide.color = new Color(baseC.r, baseC.g, baseC.b, 0.3f);
    }

    void SetNumber(Gauge g, float v, bool low, bool blinkOn)
    {
        int pct = Mathf.RoundToInt(v * 100f);
        if (pct != g.shownPercent)
        {
            g.number.text = pct + "<size=55%><voffset=0.55em>%</voffset></size>";
            g.shownPercent = pct;
        }
        Color c = low ? danger : g.color;
        g.number.color = new Color(c.r, c.g, c.b, blinkOn ? 1f : 0.35f);
    }

    void SetLabel(Gauge g, string text, Color c, bool blinkOn)
    {
        if (text != g.shownLabel)
        {
            g.label.text = text;
            g.shownLabel = text;
        }
        g.label.color = new Color(c.r, c.g, c.b, blinkOn ? 0.9f : 0.4f);
    }

    // ======================= FORMA =======================

    // Borde de arriba del visor (la misma superelipse que dibuja HelmetVisorHUD), en unidades del canvas
    static Vector2 Edge(float u, float hw, float hh, float visorSize)
    {
        float au = Mathf.Clamp01(u / visorSize);
        float v = (visorSize - 0.02f) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(au, 5f)), 0.25f);
        return new Vector2(u * hw, v * hh);
    }

    void Layout(Gauge g, float cw, float ch, float visorSize)
    {
        float hw = cw * 0.5f, hh = ch * 0.5f;
        float s = Mathf.Clamp(ch / 450f, 0.75f, 1.25f);

        // Curva muestreada y medida a lo largo (de la esquina del casco hacia el centro)
        const int N = 120;
        Vector2[] p = new Vector2[N];
        float[] len = new float[N];
        for (int k = 0; k < N; k++)
        {
            p[k] = Edge(Mathf.Lerp(U1, U0, k / (N - 1f)), hw, hh, visorSize);
            len[k] = k == 0 ? 0f : len[k - 1] + Vector2.Distance(p[k], p[k - 1]);
        }
        float total = len[N - 1];
        Vector2 pos, tan, nrm;

        float step = total / Cells;
        for (int i = 0; i < Cells; i++)
        {
            Sample(p, len, (i + 0.5f) * step, out pos, out tan, out nrm);
            Vector2 c = pos + nrm * (Inset + CellT * 0.5f) * s;
            PlaceRotated(g.cells[i].rectTransform, g.side, c, tan, Mathf.Max(0.8f, step - CellGap * s), CellT * s);
        }

        // Guía fina por fuera de las celdas (hacia el borde del casco)
        Vector2 a = p[0] + Normal(p, 0) * (Inset - 3f) * s;
        Vector2 b = p[N - 1] + Normal(p, N - 1) * (Inset - 3f) * s;
        PlaceRotated(g.guide.rectTransform, g.side, (a + b) * 0.5f, (b - a).normalized, (b - a).magnitude, 1f);

        // Marcas al 25, 50 y 75 % por dentro
        for (int t = 0; t < 3; t++)
        {
            Sample(p, len, total * (t + 1) * 0.25f, out pos, out tan, out nrm);
            Vector2 c = pos + nrm * (Inset + CellT + 2.5f + TickLen * 0.5f) * s;
            PlaceRotated(g.ticks[t].rectTransform, g.side, c, tan, TickT, TickLen * s);
        }

        // Nombre y número debajo de la mitad interior del medidor
        Sample(p, len, total * 0.62f, out pos, out tan, out nrm);
        Vector2 basePos = pos + nrm * (Inset + CellT) * s;
        PlaceText(g.label, g.side, basePos + Vector2.down * LabelOff * s, 150f * s, 12f * s, LabelSize * s);
        PlaceText(g.number, g.side, basePos + Vector2.down * NumberOff * s, 120f * s, 30f * s, NumSize * s);

        // Placa oscura detrás de todo (para que se lea sobre el cielo)
        Sample(p, len, total * 0.5f, out pos, out tan, out nrm);
        Vector2 plateC = pos + nrm * (Inset + 16f) * s;
        RectTransform rt = g.plate.rectTransform;
        rt.anchoredPosition = new Vector2(plateC.x * g.side, plateC.y);
        rt.sizeDelta = new Vector2(total * 1.4f, 100f * s);
        rt.localEulerAngles = Vector3.zero;
    }

    // Punto, tangente y normal hacia dentro del visor a la distancia d a lo largo de la curva
    static void Sample(Vector2[] p, float[] len, float d, out Vector2 pos, out Vector2 tan, out Vector2 nrm)
    {
        int k = 1;
        while (k < p.Length - 1 && len[k] < d) k++;
        float seg = Mathf.Max(1e-5f, len[k] - len[k - 1]);
        float t = Mathf.Clamp01((d - len[k - 1]) / seg);
        pos = Vector2.Lerp(p[k - 1], p[k], t);
        tan = (p[k] - p[k - 1]).normalized;
        nrm = InwardNormal(tan);
    }

    static Vector2 Normal(Vector2[] p, int k)
    {
        Vector2 tan = k == 0 ? p[1] - p[0] : p[k] - p[k - 1];
        return InwardNormal(tan.normalized);
    }

    // De las dos normales de la curva, la que mira hacia el interior del visor es la que apunta hacia abajo
    static Vector2 InwardNormal(Vector2 tan)
    {
        Vector2 n = new Vector2(-tan.y, tan.x);
        if (n.y > 0f) n = -n;
        return n;
    }

    // Rectángulo centrado en c (medido en el lado derecho) y alineado con dir; en el lado izquierdo se refleja
    static void PlaceRotated(RectTransform rt, float side, Vector2 c, Vector2 dir, float w, float h)
    {
        rt.anchoredPosition = new Vector2(c.x * side, c.y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x * side) * Mathf.Rad2Deg);
    }

    static void PlaceText(TextMeshProUGUI t, float side, Vector2 c, float w, float h, float size)
    {
        RectTransform rt = t.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(c.x * side, c.y);
        rt.sizeDelta = new Vector2(w, h);
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
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

    TextMeshProUGUI Text(string name, Transform parent, float size, TMP_FontAsset font, float spacing)
    {
        TextMeshProUGUI t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.characterSpacing = spacing;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        t.color = accent;
        t.text = "";
        return t;
    }
}
