using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

// Estilo de los menús, construido por código: el mismo lenguaje que el visor del casco (HelmetVisorHUD).
//  - Paneles de "cristal" azul marino con esquinas cortadas, borde fino que brilla, líneas de escaneo
//    y miras en las esquinas. Los botones son hologramas; la acción principal va en naranja (el pez).
//  - Fuentes: "Oxanium" para títulos y números, "Rajdhani" para textos (Resources/Fonts).
//    "Lilita One" queda solo para el texto de los cómics (ComicFont).
//  - Todos los sprites (paneles, hexágonos, estrella, candado, calavera, llave...) se generan aquí.
// Los nombres viejos (ComicPanel, ComicButton) se conservan para no romper los menús: ahora dibujan
// el panel holográfico. Ningún texto usa contorno de TextMeshPro (fallaba en objetos inactivos).
public static class MenuUI
{
    // ---------- Paleta (la del traje) ----------
    public static readonly Color Ink = new Color(0.01f, 0.035f, 0.06f, 1f);        // azul marino del casco
    public static readonly Color Glass = new Color(0.02f, 0.075f, 0.115f, 0.86f);  // cristal de los paneles
    public static readonly Color Orange = new Color32(255, 122, 47, 255);          // acción principal (cuerpo del pez)
    public static readonly Color OrangeDark = new Color32(206, 74, 22, 255);
    public static readonly Color Blue = new Color32(72, 190, 228, 255);            // holograma
    public static readonly Color BlueDark = new Color32(22, 74, 104, 255);         // holograma apagado (VOLVER, SALIR)
    public static readonly Color Accent = new Color(0.41f, 0.87f, 0.9f, 1f);       // cian del HUD
    public static readonly Color Cream = new Color32(228, 244, 246, 255);          // texto claro
    public static readonly Color TextDim = new Color32(150, 186, 196, 255);        // texto secundario
    public static readonly Color Paper = new Color32(255, 246, 225, 255);          // papel de las viñetas
    public static readonly Color Yellow = new Color32(255, 200, 64, 255);          // estrellas y novedades
    public static readonly Color Green = new Color32(84, 226, 156, 255);
    public static readonly Color GreenDark = new Color32(28, 112, 84, 255);
    public static readonly Color Red = new Color(1f, 0.2f, 0.14f, 1f);             // peligro y jefes
    public static readonly Color Purple = new Color32(150, 120, 240, 255);
    public static readonly Color Locked = new Color32(84, 104, 122, 255);
    public static readonly Color Dark = new Color(0.01f, 0.035f, 0.06f, 0.92f);

    // Colores para <color=...> en textos
    public const string HexOrange = "#FF8A4C";
    public const string HexAccent = "#69DEE6";
    public const string HexYellow = "#FFC840";

    // ---------- Fuentes ----------
    static TMP_FontAsset titleFont, bodyFont, comicFont;
    static bool fontsMissing, comicMissing;

    public static TMP_FontAsset TitleFont { get { LoadFonts(); return titleFont; } }
    public static TMP_FontAsset BodyFont { get { LoadFonts(); return bodyFont; } }

    // Fuente de los cómics (narración y viñetas en blanco)
    public static TMP_FontAsset ComicFont
    {
        get
        {
            if (comicFont == null && !comicMissing)
            {
                comicFont = MakeFont("Fonts/LilitaOne-Regular");
                if (comicFont == null) comicMissing = true;
            }
            return comicFont != null ? comicFont : BodyFont;
        }
    }

    static void LoadFonts()
    {
        // (se vuelven a crear si Unity las descargó al cambiar de escena)
        if (fontsMissing || (titleFont != null && bodyFont != null)) return;
        titleFont = MakeFont("Fonts/Oxanium-ExtraBold");
        bodyFont = MakeFont("Fonts/Rajdhani-Bold");
        if (titleFont == null) titleFont = bodyFont;
        if (bodyFont == null) bodyFont = titleFont;
        if (titleFont == null) fontsMissing = true;   // sin fuentes: se usa la de TextMeshPro
    }

    static TMP_FontAsset MakeFont(string path)
    {
        Font font = Resources.Load<Font>(path);
        if (font == null) return null;
        try
        {
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset != null) asset.name = font.name + " (dinámica)";
            return asset;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("MenuUI: no se pudo crear la fuente " + path + ": " + e.Message);
            return null;
        }
    }

    // ======================= SPRITES GENERADOS =======================

    static Sprite rounded, roundedSmall, circle, star, lockIcon, gradient;
    static readonly Dictionary<string, Sprite> shapes = new Dictionary<string, Sprite>();

    // Rectángulo redondeado blanco (se estira con 9-slice)
    public static Sprite Rounded { get { if (rounded == null) rounded = MakeRounded(96, 30, "UI_Rounded"); return rounded; } }
    public static Sprite RoundedSmall { get { if (roundedSmall == null) roundedSmall = MakeRounded(48, 12, "UI_RoundedSmall"); return roundedSmall; } }

    public static Sprite Circle
    {
        get
        {
            if (circle == null) circle = MakeShape(128, "UI_Circle", (x, y) => 0.47f - Mathf.Sqrt(x * x + y * y));
            return circle;
        }
    }

    public static Sprite Star
    {
        get
        {
            if (star == null) star = MakeShape(128, "UI_Star", StarDistance);
            return star;
        }
    }

    public static Sprite Lock
    {
        get
        {
            if (lockIcon == null) lockIcon = MakeShape(128, "UI_Lock", LockDistance);
            return lockIcon;
        }
    }

    // Formas del estilo holográfico (se crean la primera vez que se piden)
    public static Sprite Hex { get { return Shape("UI_Hex", () => MakeShape(128, "UI_Hex", (u, v) => HexDistance(u, v, 0.46f))); } }
    public static Sprite HexLine { get { return Shape("UI_HexLine", () => MakeShape(128, "UI_HexLine", (u, v) => 0.016f - Mathf.Abs(HexDistance(u, v, 0.44f)))); } }
    public static Sprite HexGlow
    {
        get
        {
            return Shape("UI_HexGlow", () => MakeAlpha(128, "UI_HexGlow", (x, y) =>
            {
                float d = HexDistance(x / 128f, y / 128f, 0.34f);
                float a = d > 0f ? 1f : Mathf.Exp(d * 128f / 9f);
                // se apaga en redondo antes del borde de la textura (si no, al avivarlo se ve un cuadrado)
                float r = Mathf.Sqrt(x * x + y * y) / 64f;
                return a * Mathf.Clamp01((1f - r) / 0.22f);
            }));
        }
    }
    public static Sprite Skull { get { return Shape("UI_Skull", () => MakeShape(128, "UI_Skull", SkullDistance)); } }
    public static Sprite Wrench { get { return Shape("UI_Wrench", () => MakeShape(128, "UI_Wrench", WrenchDistance)); } }
    public static Sprite StarLine { get { return Shape("UI_StarLine", () => MakeShape(128, "UI_StarLine", (u, v) => 0.022f - Mathf.Abs(StarDistance(u * 1.08f, v * 1.08f) + 0.022f))); } }
    public static Sprite StarGlow
    {
        get
        {
            return Shape("UI_StarGlow", () => MakeAlpha(128, "UI_StarGlow", (x, y) =>
            {
                float d = StarDistance(x / 128f * 1.5f, y / 128f * 1.5f);
                float a = d > 0f ? 1f : Mathf.Exp(d * 128f / 10f);
                float r = Mathf.Sqrt(x * x + y * y) / 64f;
                return a * Mathf.Clamp01((1f - r) / 0.22f);
            }));
        }
    }
    public static Sprite Ring { get { return Shape("UI_Ring", () => MakeShape(128, "UI_Ring", (u, v) => 0.014f - Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.46f))); } }
    public static Sprite RingTicks { get { return Shape("UI_RingTicks", () => MakeShape(128, "UI_RingTicks", RingTicksDistance)); } }
    public static Sprite Sonar { get { return Shape("UI_Sonar", () => MakeShape(256, "UI_Sonar", SonarDistance)); } }
    public static Sprite Diamond { get { return Shape("UI_Diamond", () => MakeShape(64, "UI_Diamond", (u, v) => (0.42f - (Mathf.Abs(u) + Mathf.Abs(v))) * 0.7071f)); } }
    public static Sprite Glow
    {
        get
        {
            return Shape("UI_Glow", () => MakeAlpha(128, "UI_Glow", (x, y) =>
                Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 64f), 2.2f)));
        }
    }
    public static Sprite Pixel { get { return Shape("UI_Pixel", () => MakeAlpha(4, "UI_Pixel", (x, y) => 1f)); } }

    // Degradado vertical (abajo profundo, arriba turquesa) para fondos bajo el agua
    public static Sprite WaterGradient
    {
        get
        {
            if (gradient == null)
            {
                Texture2D tex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
                tex.name = "UI_WaterGradient";
                tex.wrapMode = TextureWrapMode.Clamp;
                Color bottom = new Color32(6, 22, 48, 255), mid = new Color32(14, 74, 112, 255), top = new Color32(46, 168, 190, 255);
                for (int y = 0; y < 256; y++)
                {
                    float t = y / 255f;
                    Color c = t < 0.6f ? Color.Lerp(bottom, mid, t / 0.6f) : Color.Lerp(mid, top, (t - 0.6f) / 0.4f);
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
                }
                tex.Apply();
                gradient = Sprite.Create(tex, new Rect(0, 0, 4, 256), new Vector2(0.5f, 0.5f), 100f);
                gradient.name = tex.name;
            }
            return gradient;
        }
    }

    static Sprite Shape(string key, System.Func<Sprite> make)
    {
        Sprite s;
        if (!shapes.TryGetValue(key, out s) || s == null)
        {
            s = make();
            shapes[key] = s;
        }
        return s;
    }

    static Sprite MakeRounded(int size, int radius, string name)
    {
        float half = size * 0.5f;
        Sprite s = MakeShape(size, name, (x, y) =>
        {
            // x, y en píxeles desde el centro
            float qx = Mathf.Abs(x) - (half - radius), qy = Mathf.Abs(y) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return -(outside + inside - radius);
        }, true, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        return s;
    }

    // Crea un sprite a partir de una función de distancia (positiva dentro), con antialias
    static Sprite MakeShape(int size, string name, System.Func<float, float, float> dist,
                            bool pixelUnits = false, Vector4 border = default(Vector4))
    {
        float aa = pixelUnits ? 1f : 1f / size;
        return MakeAlpha(size, size, name, (x, y) =>
        {
            float u = pixelUnits ? x : x / size;
            float v = pixelUnits ? y : y / size;
            return Mathf.Clamp01(dist(u, v) / aa + 0.5f);
        }, border);
    }

    // Sprite blanco con el alfa que da la función (x, y en píxeles desde el centro, y hacia arriba)
    static Sprite MakeAlpha(int size, string name, System.Func<float, float, float> alpha)
    {
        return MakeAlpha(size, size, name, alpha, Vector4.zero);
    }

    static Sprite MakeAlpha(int w, int h, string name, System.Func<float, float, float> alpha, Vector4 border)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float py = y + 0.5f - h * 0.5f;
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(alpha(x + 0.5f - w * 0.5f, py));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s.name = name;
        return s;
    }

    // ---------- Funciones de distancia (positivas dentro; u, v de -0.5 a 0.5) ----------

    // Estrella de 5 puntas
    static float StarDistance(float x, float y)
    {
        Vector2 p = new Vector2(x, y + 0.03f);
        float a = Mathf.Atan2(p.x, p.y);
        float r = p.magnitude;
        float seg = Mathf.PI * 2f / 5f;
        float k = Mathf.Repeat(a + seg * 0.5f, seg) - seg * 0.5f;     // ángulo relativo a la punta más cercana
        float outerR = 0.46f, innerR = 0.2f;
        float t = Mathf.Abs(k) / (seg * 0.5f);
        Vector2 tip = new Vector2(0f, outerR);
        Vector2 valley = new Vector2(Mathf.Sin(seg * 0.5f), Mathf.Cos(seg * 0.5f)) * innerR;
        Vector2 dir = new Vector2(Mathf.Sin(Mathf.Abs(k)), Mathf.Cos(Mathf.Abs(k)));
        Vector2 e = valley - tip;
        float den = dir.x * e.y - dir.y * e.x;
        float edgeR = Mathf.Abs(den) > 1e-5f ? (tip.x * e.y - tip.y * e.x) / den : Mathf.Lerp(outerR, innerR, t);
        return (edgeR - r) * 0.9f;
    }

    // Candado: cuerpo redondeado + arco + ojo de la cerradura
    static float LockDistance(float x, float y)
    {
        float bx = Mathf.Abs(x) - 0.28f, by = Mathf.Abs(y + 0.12f) - 0.2f;
        float body = -(new Vector2(Mathf.Max(bx + 0.06f, 0f), Mathf.Max(by + 0.06f, 0f)).magnitude
                       + Mathf.Min(Mathf.Max(bx + 0.06f, by + 0.06f), 0f) - 0.06f);
        float ringR = Mathf.Sqrt(x * x + (y - 0.1f) * (y - 0.1f));
        float ring = 0.06f - Mathf.Abs(ringR - 0.18f);
        if (y < 0.1f) ring = Mathf.Min(ring, 0.06f - Mathf.Abs(Mathf.Abs(x) - 0.18f));
        if (y < -0.05f) ring = -1f;
        float d = Mathf.Max(body, ring);
        return Mathf.Min(d, -CircleDistance(x, y, 0f, -0.1f, 0.05f));
    }

    // Hexágono con lados planos arriba y abajo
    static float HexDistance(float u, float v, float r)
    {
        const float k = 0.8660254f;
        float au = Mathf.Abs(u), av = Mathf.Abs(v);
        return r * k - Mathf.Max(av, au * k + av * 0.5f);
    }

    static float CircleDistance(float u, float v, float cx, float cy, float r)
    {
        return r - Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
    }

    static float BoxDistance(float u, float v, float cx, float cy, float hx, float hy, float rad = 0f)
    {
        float qx = Mathf.Abs(u - cx) - (hx - rad), qy = Mathf.Abs(v - cy) - (hy - rad);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return -(outside + inside - rad);
    }

    static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
    {
        float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
        float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        float dx = pax - bax * h, dy = pay - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Calavera (niveles de jefe)
    static float SkullDistance(float u, float v)
    {
        float head = CircleDistance(u, v, 0f, 0.07f, 0.3f);
        float jaw = BoxDistance(u, v, 0f, -0.2f, 0.17f, 0.13f, 0.05f);
        float d = Mathf.Max(head, jaw);
        d = Mathf.Min(d, -CircleDistance(u, v, -0.115f, 0.03f, 0.085f));
        d = Mathf.Min(d, -CircleDistance(u, v, 0.115f, 0.03f, 0.085f));
        d = Mathf.Min(d, -BoxDistance(u, v, 0f, -0.1f, 0.03f, 0.045f, 0.02f));
        if (v < -0.21f)
        {
            for (int i = -1; i <= 1; i++)
                d = Mathf.Min(d, -BoxDistance(u, v, i * 0.07f, -0.3f, 0.012f, 0.09f));
        }
        return d;
    }

    // Llave inglesa (Taller)
    static float WrenchDistance(float u, float v)
    {
        const float c = 0.7071068f;
        float x = u * c + v * c, y = -u * c + v * c;
        float handle = 0.06f - SegmentDistance(x, y, -0.27f, 0f, 0.12f, 0f);
        float head = CircleDistance(x, y, 0.24f, 0f, 0.15f);
        float d = Mathf.Max(handle, head);
        d = Mathf.Min(d, -BoxDistance(x, y, 0.37f, 0f, 0.13f, 0.06f));
        d = Mathf.Min(d, -CircleDistance(x, y, 0.26f, 0f, 0.06f));
        d = Mathf.Max(d, CircleDistance(x, y, -0.3f, 0f, 0.095f));
        d = Mathf.Min(d, -CircleDistance(x, y, -0.3f, 0f, 0.042f));
        return d;
    }

    // Anillo con huecos y marcas (selección en el mapa)
    static float RingTicksDistance(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float ang = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 90f);
        float ring = 0.012f - Mathf.Abs(r - 0.43f);
        if (ang > 35f && ang < 55f) ring = -1f;
        float ticks = Mathf.Min(0.016f - Mathf.Abs(r - 0.47f), 0.012f - Mathf.Abs(ang - 45f) * 0.004f);
        return Mathf.Max(ring, ticks);
    }

    // Pantalla de sonar: 3 anillos y una cruz
    static float SonarDistance(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float d = -1f;
        d = Mathf.Max(d, 0.008f - Mathf.Abs(r - 0.46f));
        d = Mathf.Max(d, 0.005f - Mathf.Abs(r - 0.33f));
        d = Mathf.Max(d, 0.005f - Mathf.Abs(r - 0.2f));
        float cross = Mathf.Min(0.004f - Mathf.Min(Mathf.Abs(u), Mathf.Abs(v)), r - 0.06f);
        return Mathf.Max(d, Mathf.Min(cross, 0.47f - r));
    }

    // ======================= PANELES HOLOGRÁFICOS =======================

    const int GlowPx = 12;   // halo exterior (px)
    const int ExtPx = 6;     // cuánto sobresalen las miras de las esquinas

    class ChamferSet
    {
        public Sprite fill, line, inner, sheen, scan, glow, brackets;
    }
    static readonly Dictionary<int, ChamferSet> chamfers = new Dictionary<int, ChamferSet>();

    // Tamaño del corte de las esquinas según el tamaño del panel
    static int TierCut(Vector2 size)
    {
        float m = Mathf.Min(size.x, size.y);
        return m < 44f ? 6 : m < 90f ? 10 : m < 260f ? 16 : 22;
    }

    // Distancia al borde de un rectángulo con esquinas cortadas (grandes arriba-izq y abajo-der)
    static float ChamferDistance(float x, float y, float hw, float hh, float c, float c2)
    {
        const float r2 = 1.4142136f;
        float d = Mathf.Min(hw - Mathf.Abs(x), hh - Mathf.Abs(y));
        d = Mathf.Min(d, ((x + hw) + (hh - y) - c) / r2);
        d = Mathf.Min(d, ((hw - x) + (hh - y) - c2) / r2);
        d = Mathf.Min(d, ((hw - x) + (y + hh) - c) / r2);
        d = Mathf.Min(d, ((x + hw) + (y + hh) - c2) / r2);
        return d;
    }

    static ChamferSet GetChamfer(int cut)
    {
        ChamferSet set;
        if (chamfers.TryGetValue(cut, out set) && set.fill != null) return set;
        set = new ChamferSet();
        float c2 = Mathf.Max(2f, Mathf.Round(cut * 0.3f));
        int b = Mathf.CeilToInt(Mathf.Max(cut + 6, 16) / 4f) * 4;
        int s = 2 * b + 16;
        float hw = s * 0.5f;
        Vector4 border = new Vector4(b, b, b, b);
        string n = "UI_Holo" + cut;
        set.fill = MakeAlpha(s, s, n + "_Relleno", (x, y) => Mathf.Clamp01(ChamferDistance(x, y, hw, hw, cut, c2) + 0.5f), border);
        set.line = MakeAlpha(s, s, n + "_Borde", (x, y) =>
        {
            float d = ChamferDistance(x, y, hw, hw, cut, c2);
            return d > -1f ? Mathf.Clamp01(1f - Mathf.Abs(d - 1f) + 0.5f) : 0f;
        }, border);
        set.inner = MakeAlpha(s, s, n + "_Interior", (x, y) =>
        {
            float d = ChamferDistance(x, y, hw, hw, cut, c2);
            return Mathf.Clamp01(d + 0.5f) * Mathf.Exp(-Mathf.Max(d, 0f) / 4f);
        }, border);
        set.sheen = MakeAlpha(s, s, n + "_Reflejo", (x, y) =>
        {
            float d = ChamferDistance(x, y, hw, hw, cut, c2);
            return Mathf.Clamp01(d + 0.5f) * Mathf.Exp(-Mathf.Max(hw - y, 0f) / 5f);
        }, border);
        set.scan = MakeAlpha(s, s, n + "_Escaneo", (x, y) =>
        {
            float d = ChamferDistance(x, y, hw, hw, cut, c2);
            int row = Mathf.FloorToInt(y + hw) % 4;
            float pattern = row == 1 ? 1f : row == 2 ? 0.35f : 0f;
            return Mathf.Clamp01(d + 0.5f) * pattern;
        }, border);
        int gb = b + GlowPx;
        set.glow = MakeAlpha(s + 2 * GlowPx, s + 2 * GlowPx, n + "_Halo", (x, y) =>
        {
            float d = ChamferDistance(x, y, hw, hw, cut, c2);
            return d < 0f ? Mathf.Exp(d * 3f / GlowPx) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 4f));
        }, new Vector4(gb, gb, gb, gb));
        int eb = b + ExtPx;
        set.brackets = MakeAlpha(s + 2 * ExtPx, s + 2 * ExtPx, n + "_Miras", (x, y) => BracketAlpha(x, y, hw, cut, c2),
                                 new Vector4(eb, eb, eb, eb));
        chamfers[cut] = set;
        return set;
    }

    // Miras de las esquinas: raya paralela al corte grande y escuadras en las esquinas chicas
    static float BracketAlpha(float x, float y, float hw, float cut, float c2)
    {
        const float o = 4f, lw = 1f, r2 = 1.4142136f;
        float len = 10f + cut * 0.4f;
        float a = 0f;
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f, sy = -sx;
            float ax = sx * (hw - cut), ay = sy * hw, bx = sx * hw, by = sy * (hw - cut);
            float nx = sx / r2, ny = sy / r2;
            float p0x = ax + (bx - ax) * 0.12f + nx * o, p0y = ay + (by - ay) * 0.12f + ny * o;
            float p1x = ax + (bx - ax) * 0.88f + nx * o, p1y = ay + (by - ay) * 0.88f + ny * o;
            a = Mathf.Max(a, Mathf.Clamp01(lw - SegmentDistance(x, y, p0x, p0y, p1x, p1y) + 0.5f));
        }
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? 1f : -1f, sy = sx;
            float cx = sx * (hw + o), cy = sy * (hw + o);
            a = Mathf.Max(a, Mathf.Clamp01(lw - SegmentDistance(x, y, cx, cy, cx - sx * len, cy) + 0.5f));
            a = Mathf.Max(a, Mathf.Clamp01(lw - SegmentDistance(x, y, cx, cy, cx, cy - sy * len) + 0.5f));
        }
        return a;
    }

    // Estilo de un panel a partir de su color de relleno (así los menús viejos se ven holográficos)
    public struct HoloStyle
    {
        public Color glass, edge, glow, inner, label;
        public float sheen;
        public bool paper, solid;
    }

    public static HoloStyle Derive(Color fill)
    {
        HoloStyle s = new HoloStyle();
        float h, sat, v;
        Color.RGBToHSV(fill, out h, out sat, out v);
        bool warm = h < 0.17f || h > 0.93f || (h > 0.22f && h < 0.45f);
        float lum = 0.2126f * fill.r + 0.7152f * fill.g + 0.0722f * fill.b;
        if (lum > 0.72f && sat < 0.35f)
        {
            // Papel (viñetas del cómic)
            s.paper = true;
            s.glass = WithAlpha(fill, 1f);
            s.edge = WithAlpha(Accent, 0.9f);
            s.glow = WithAlpha(Accent, 0.16f);
            s.inner = new Color(1f, 1f, 1f, 0f);
            s.label = Ink;
        }
        else if (v < 0.3f)
        {
            // Cristal oscuro
            s.glass = WithAlpha(fill, Mathf.Max(fill.a, 0.86f));
            s.edge = WithAlpha(Accent, 0.85f);
            s.glow = WithAlpha(Accent, 0.16f);
            s.inner = WithAlpha(Accent, 0.28f);
            s.label = Cream;
            s.sheen = 0.07f;
        }
        else if (warm && sat > 0.45f && v > 0.5f)
        {
            // Sólido: la acción principal (naranja), jefes (rojo), estrellas (amarillo)
            s.solid = true;
            s.glass = WithAlpha(fill, 0.94f);
            s.edge = WithAlpha(Color.Lerp(fill, Color.white, 0.5f), 1f);
            s.glow = WithAlpha(fill, 0.38f);
            s.inner = new Color(1f, 1f, 1f, 0.4f);
            s.label = Ink;
            s.sheen = 0.22f;
        }
        else
        {
            // Holograma
            s.glass = WithAlpha(Color.Lerp(Ink, fill, 0.22f), 0.88f);
            s.edge = WithAlpha(Color.Lerp(fill, Color.white, 0.2f), 0.95f);
            s.glow = WithAlpha(fill, 0.22f);
            s.inner = WithAlpha(fill, 0.42f);
            s.label = WithAlpha(Color.Lerp(fill, Color.white, 0.78f), 1f);
            s.sheen = 0.08f;
        }
        return s;
    }

    public static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    // Panel holográfico: halo, cristal, líneas de escaneo, brillo interior, reflejo, borde y miras.
    // Devuelve la raíz (lo que se agregue va encima). El cristal se llama "Relleno" y el halo "Brillo".
    // scan / brackets: null = solo en paneles grandes
    public static RectTransform HoloPanel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                                          Color glass, Color edge, Color glow, Color inner, float sheen = 0f,
                                          bool? scan = null, bool? brackets = null)
    {
        RectTransform root = Rect(name, parent, anchor, pos, size);
        ChamferSet set = GetChamfer(TierCut(size));
        bool big = Mathf.Min(size.x, size.y) >= 120f;
        if (glow.a > 0f) Layer("Brillo", root, set.glow, glow, GlowPx);
        Layer("Relleno", root, set.fill, glass, 0f);
        if (scan ?? big)
        {
            Image s = Layer("Escaneo", root, set.scan, WithAlpha(edge, 0.05f), 0f);
            s.type = Image.Type.Tiled;
        }
        if (inner.a > 0f) Layer("Interior", root, set.inner, inner, 0f);
        if (sheen > 0f) Layer("Reflejo", root, set.sheen, new Color(1f, 1f, 1f, sheen), 0f);
        Layer("Borde", root, set.line, edge, 0f);
        if (brackets ?? big) Layer("Miras", root, set.brackets, WithAlpha(edge, 0.75f), ExtPx);
        return root;
    }

    static Image Layer(string name, RectTransform root, Sprite sprite, Color color, float expand)
    {
        RectTransform rt = Stretch(name, root);
        rt.offsetMin = new Vector2(-expand, -expand);
        rt.offsetMax = new Vector2(expand, expand);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ======================= CANVAS =======================

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ======================= PIEZAS BÁSICAS =======================

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Image Panel(string name, Transform parent, Color color)
    {
        RectTransform rt = Stretch(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Rect(name, parent, anchor, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced;
        return img;
    }

    // Texto con las fuentes del traje. 'outline' ya no se usa (el estilo holográfico no lleva contorno);
    // se deja para no romper llamadas viejas.
    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 pos,
                                       Vector2 box, Color color, bool title = false, float outline = 0f)
    {
        RectTransform rt = Rect(name, parent, anchor, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = title ? TitleFont : BodyFont;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    // Compatibilidad con los menús anteriores
    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        return Text(name, parent, text, size, anchor, pos, box, Accent);
    }

    // Título holográfico: halo, sombra, separación de color (cian / rojo) y un parpadeo de holograma
    public static TextMeshProUGUI Title(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 pos,
                                        Vector2 box, Color color)
    {
        RectTransform holder = Rect(name, parent, anchor, pos, box);
        Image glow = Img("Halo", holder, Glow, WithAlpha(color, 0.1f), new Vector2(0.5f, 0.5f), Vector2.zero,
                         new Vector2(box.x * 1.1f, box.y * 1.4f));
        TitleLayer("Sombra", holder, text, size, box, new Vector2(0f, -size * 0.045f), WithAlpha(Ink, 0.75f));
        TextMeshProUGUI cyan = TitleLayer("Cian", holder, text, size, box, new Vector2(-size * 0.022f, 0f), WithAlpha(Accent, 0.3f));
        TextMeshProUGUI red = TitleLayer("Rojo", holder, text, size, box, new Vector2(size * 0.022f, 0f), new Color(1f, 0.25f, 0.35f, 0.22f));
        TextMeshProUGUI main = TitleLayer("Texto", holder, text, size, box, Vector2.zero, color);
        HoloFlicker flicker = holder.gameObject.AddComponent<HoloFlicker>();
        flicker.Setup(main, cyan, red, glow, size);
        return main;
    }

    static TextMeshProUGUI TitleLayer(string name, RectTransform holder, string text, float size, Vector2 box, Vector2 offset, Color color)
    {
        TextMeshProUGUI t = Text(name, holder, text, size, new Vector2(0.5f, 0.5f), offset, box, color, true);
        t.characterSpacing = 2f;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.6f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // Panel (antes "de cómic"): ahora es un panel holográfico con el estilo que sale del color de relleno.
    // border/shadow ya no se usan; se dejan por compatibilidad.
    public static RectTransform ComicPanel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill,
                                           float border = 5f, float shadow = 9f)
    {
        HoloStyle s = Derive(fill);
        return HoloPanel(name, parent, anchor, pos, size, s.glass, s.edge, s.glow, s.inner, s.sheen);
    }

    // Botón holográfico. 'fill' = color del botón (naranja = acción principal)
    public static Button ComicButton(string label, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill,
                                     UnityEngine.Events.UnityAction onClick, float fontSize = 28f)
    {
        HoloStyle s = Derive(fill);
        RectTransform root = HoloPanel("Btn_" + label, parent, anchor, pos, size, s.glass, s.edge, s.glow, s.inner, s.sheen, false, false);
        Image fillImg = root.Find("Relleno").GetComponent<Image>();
        fillImg.raycastTarget = true;

        TextMeshProUGUI t = Text("Label", root, label, fontSize, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size - new Vector2(28f, 4f), s.label);
        t.characterSpacing = 3f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMax = fontSize;
        t.fontSizeMin = fontSize * 0.65f;

        Button b = root.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.92f, 1f, 1f, 1f);
        cb.selectedColor = new Color(0.92f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.75f, 0.85f, 0.9f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.55f, 1f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.targetGraphic = fillImg;
        if (onClick != null) b.onClick.AddListener(onClick);

        root.gameObject.AddComponent<MenuButtonFX>();
        return b;
    }

    // Compatibilidad: botón naranja estándar centrado
    public static Button CreateButton(string label, Transform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        return ComicButton(label, parent, new Vector2(0.5f, 0.5f), pos, new Vector2(340f, 60f), Orange, onClick, 28f);
    }

    // Fila de 3 estrellas: las ganadas doradas con brillo, las otras apagadas con su contorno.
    // Devuelve el contenedor de cada estrella para poder animarlas
    public static RectTransform[] Stars(Transform parent, Vector2 anchor, Vector2 pos, float size, int earned, float spacing = 1.1f)
    {
        RectTransform[] holders = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            Vector2 p = pos + new Vector2((i - 1) * size * spacing, i == 1 ? size * 0.12f : 0f);
            RectTransform h = Rect("Estrella" + i, parent, anchor, p, Vector2.one * size);
            StarIcon(h, Vector2.zero, size, i < earned);
            holders[i] = h;
        }
        return holders;
    }

    // Una estrella (ganada o apagada) centrada en 'pos' dentro de 'parent'
    public static void StarIcon(Transform parent, Vector2 pos, float size, bool earned)
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        if (earned)
        {
            Img("Brillo", parent, StarGlow, WithAlpha(Yellow, 0.45f), c, pos, Vector2.one * size * 1.6f);
            Img("Relleno", parent, Star, Yellow, c, pos, Vector2.one * size);
        }
        else
        {
            Img("Relleno", parent, Star, WithAlpha(Ink, 0.55f), c, pos, Vector2.one * size);
            Img("Contorno", parent, StarLine, WithAlpha(Locked, 0.9f), c, pos, Vector2.one * size);
        }
    }

    // Etiqueta pequeña (JEFE, TALLER, PRÓXIMAMENTE...), con ícono opcional a la izquierda
    public static RectTransform Tag(string text, Transform parent, Vector2 anchor, Vector2 pos, Color fill, float fontSize = 15f,
                                    Sprite icon = null)
    {
        float w = Mathf.Max(48f, text.Length * fontSize * 0.6f + 18f) + (icon != null ? fontSize + 4f : 0f);
        float h = fontSize + 12f;
        HoloStyle s = Derive(fill);
        RectTransform root = HoloPanel("Tag_" + text, parent, anchor, pos, new Vector2(w, h), s.glass, s.edge,
                                       WithAlpha(s.glow, s.glow.a * 0.7f), s.inner, 0f, false, false);
        float tx = 0f;
        if (icon != null)
        {
            Img("Icono", root, icon, s.label, new Vector2(0f, 0.5f), new Vector2(8f + fontSize * 0.5f, 0f), Vector2.one * (fontSize + 2f));
            tx = (fontSize + 4f) * 0.5f;
        }
        TextMeshProUGUI t = Text("Texto", root, text, fontSize, new Vector2(0.5f, 0.5f), new Vector2(tx, 1f), new Vector2(w, fontSize + 8f), s.label);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.characterSpacing = 2f;
        return root;
    }

    // Línea separadora con un rombo en el centro (debajo de los títulos)
    public static void Rule(Transform parent, Vector2 anchor, Vector2 pos, float width, Color color)
    {
        float side = width * 0.5f - 16f;
        Img("Linea", parent, Pixel, WithAlpha(color, 0.5f), anchor, pos + new Vector2(-width * 0.25f - 8f, 0f), new Vector2(side, 1.5f));
        Img("Linea", parent, Pixel, WithAlpha(color, 0.5f), anchor, pos + new Vector2(width * 0.25f + 8f, 0f), new Vector2(side, 1.5f));
        Img("Rombo", parent, Diamond, color, anchor, pos, new Vector2(10f, 10f));
    }

    // ======================= VISOR DEL CASCO (fondo de los menús) =======================

    static Texture2D visorTex, hexGridTex, contourTex;
    static Sprite scanSprite;

    // Marco curvo del casco (más sutil que el del juego), líneas de escaneo y miras en las esquinas.
    // Va detrás de las pantallas del menú: así se siente que se mira desde dentro del traje.
    public static RectTransform VisorOverlay(Transform parent)
    {
        RectTransform root = Stretch("Visor", parent);
        if (visorTex == null) visorTex = MakeVisorTexture(640, 360, 1.02f, 0.8f, 0.42f);
        RawImage frame = Stretch("Marco", root).gameObject.AddComponent<RawImage>();
        frame.texture = visorTex;
        frame.raycastTarget = false;

        Image scan = Stretch("Escaneo", root).gameObject.AddComponent<Image>();
        scan.sprite = ScanSprite;
        scan.type = Image.Type.Tiled;
        scan.color = WithAlpha(Accent, 0.035f);
        scan.raycastTarget = false;

        // Miras grandes en las esquinas de la pantalla
        Vector2[] anchors = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        foreach (Vector2 a in anchors)
        {
            float sx = a.x < 0.5f ? 1f : -1f, sy = a.y < 0.5f ? 1f : -1f;
            Img("Mira", root, Pixel, WithAlpha(Accent, 0.45f), a, new Vector2(sx * 40f, sy * 26f), new Vector2(28f, 1.5f));
            Img("Mira", root, Pixel, WithAlpha(Accent, 0.45f), a, new Vector2(sx * 26f, sy * 40f), new Vector2(1.5f, 28f));
        }
        return root;
    }

    // Líneas de escaneo: una línea brillante cada 3 px (Image en mosaico)
    public static Sprite ScanSprite
    {
        get
        {
            if (scanSprite == null)
            {
                scanSprite = MakeAlpha(4, 3, "UI_Escaneo", (x, y) => Mathf.Abs(y) < 0.5f ? 1f : 0f, Vector4.zero);
                // Sin borde y en "Repeat": Unity lo dibuja en mosaico con un solo cuadro (no miles)
                scanSprite.texture.wrapMode = TextureWrapMode.Repeat;
            }
            return scanSprite;
        }
    }

    static Texture2D MakeVisorTexture(int w, int h, float visor, float opacity, float rim)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "UI_VisorMenu";
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float au = Mathf.Abs(u) / visor, av = Mathf.Abs(v) / (visor - 0.02f);
                float d = Mathf.Pow(Mathf.Pow(au, 6f) + Mathf.Pow(av, 5f), 1f / 6f);
                float thr = 1f;
                if (v > 0f) thr -= 0.05f * Mathf.Exp(-(u * u) / (0.22f * 0.22f)) * Mathf.Clamp01(v * 1.4f);
                else thr -= 0.07f * Mathf.Exp(-(u * u) / (0.16f * 0.16f)) * Mathf.Clamp01(-v * 1.4f);
                float edge = d - thr;
                float dark = edge > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.06f)) * opacity
                                       : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge + 0.25f) / 0.25f)) * 0.28f;
                float line = Mathf.Exp(-(edge * edge) / (0.004f * 0.004f));
                float glow = Mathf.Exp(-(edge * edge) / (0.03f * 0.03f)) * 0.35f;
                float ang = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360f);
                bool seg = (ang > 18f && ang < 34f) || (ang > 146f && ang < 162f) || (ang > 200f && ang < 222f) || (ang > 318f && ang < 340f);
                float ins = edge + 0.035f;
                float segLine = seg ? Mathf.Exp(-(ins * ins) / (0.0035f * 0.0035f)) * 0.8f : 0f;
                float ga = Mathf.Clamp01((line + glow + segLine) * rim);
                float outA = ga + dark * (1f - ga);
                Color c = outA > 0.0001f ? (Ink * dark * (1f - ga) + Accent * ga) / outA : Accent;
                px[y * w + x] = new Color(c.r, c.g, c.b, outA);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Rejilla de hexágonos (se repite en mosaico con uvRect) para el mapa de niveles
    public static Texture2D HexGridTexture
    {
        get
        {
            if (hexGridTex != null) return hexGridTex;
            const float r = 15f, k = 0.8660254f;
            int tw = Mathf.RoundToInt(3f * r), th = Mathf.RoundToInt(1.7320508f * r);
            hexGridTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            hexGridTex.name = "UI_RejillaHex";
            hexGridTex.wrapMode = TextureWrapMode.Repeat;
            Vector2[] centers = { new Vector2(0, 0), new Vector2(tw, 0), new Vector2(0, th), new Vector2(tw, th), new Vector2(tw * 0.5f, th * 0.5f) };
            Color32[] px = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    float best = 1e9f;
                    foreach (Vector2 c in centers)
                    {
                        float qx = Mathf.Abs(x + 0.5f - c.x) / r, qy = Mathf.Abs(y + 0.5f - c.y) / r;
                        best = Mathf.Min(best, Mathf.Max(qy, qx * k + qy * 0.5f));
                    }
                    float a = Mathf.Clamp01(1f - Mathf.Abs(best - k) * r / 0.9f);
                    px[y * tw + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            hexGridTex.SetPixels32(px);
            hexGridTex.Apply();
            return hexGridTex;
        }
    }

    // Curvas de nivel del lago (como un sonar del fondo) para el mapa de niveles
    public static Texture2D ContourTexture
    {
        get
        {
            if (contourTex != null) return contourTex;
            const int w = 256, h = 172;
            contourTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            contourTex.name = "UI_CurvasLago";
            contourTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float f = (u / 0.9f) * (u / 0.9f) + (v / 0.8f) * (v / 0.8f);
                    f += 0.12f * Mathf.Sin(u * 3.1f + 1.3f) * Mathf.Cos(v * 2.3f + 0.4f) + 0.08f * Mathf.Sin(u * 5.7f - v * 4.1f + 2f);
                    float bands = f * 6f;
                    float frac = bands - Mathf.Floor(bands);
                    float line = Mathf.Exp(-((frac - 0.5f) * (frac - 0.5f)) / (0.035f * 0.035f));
                    float shore = Mathf.Exp(-((f - 1f) * (f - 1f)) / (0.02f * 0.02f));
                    float a = Mathf.Clamp01((f < 1f ? line * 0.55f : 0f) + shore);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            contourTex.SetPixels32(px);
            contourTex.Apply();
            return contourTex;
        }
    }
}
