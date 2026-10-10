using System.Collections.Generic;
using UnityEngine;

// Dibujos de los planos del Taller, generados por código (blancos: se tiñen con el color de la mejora).
// Cada ícono es una suma de formas simples (círculos, cápsulas, cajas, triángulos) con borde suave.
public static class WorkshopIcons
{
    const int Size = 128;
    static readonly Dictionary<UpgradeIcon, Sprite> cache = new Dictionary<UpgradeIcon, Sprite>();
    static Sprite scanlines, beam, glowDot;

    public static Sprite Get(UpgradeIcon icon)
    {
        Sprite s;
        if (cache.TryGetValue(icon, out s) && s != null) return s;
        s = Make("Icono_" + icon, Distance(icon));
        cache[icon] = s;
        return s;
    }

    public static Sprite For(UpgradeId id) { return Get(UpgradeCatalog.IconOf(id)); }

    // Líneas de escaneo (se repite: usar con Image.Type.Tiled)
    public static Sprite Scanlines
    {
        get
        {
            if (scanlines == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                tex.name = "Taller_Scanlines";
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Point;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, y < 2 ? 1f : 0f));
                tex.Apply();
                scanlines = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                scanlines.name = tex.name;
            }
            return scanlines;
        }
    }

    // Haz del proyector: abajo angosto y fuerte, arriba ancho y suave
    public static Sprite Beam
    {
        get
        {
            if (beam == null)
            {
                int w = 64, h = 64;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.name = "Taller_Haz";
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < h; y++)
                {
                    float v = (y + 0.5f) / h;                    // 0 = abajo (proyector), 1 = arriba (plano)
                    float half = Mathf.Lerp(0.12f, 0.5f, v);
                    for (int x = 0; x < w; x++)
                    {
                        float u = Mathf.Abs((x + 0.5f) / w - 0.5f);
                        float edge = Mathf.Clamp01((half - u) / 0.06f);
                        float a = edge * Mathf.Lerp(0.9f, 0.25f, v);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                beam = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
                beam.name = tex.name;
            }
            return beam;
        }
    }

    // Punto de luz suave (brillo detrás de los íconos y chispas de la interfaz)
    public static Sprite GlowDot
    {
        get
        {
            if (glowDot == null) glowDot = FXFactory.SpriteFrom(FXFactory.MakeRadial(64, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2f), "Taller_Brillo"));
            return glowDot;
        }
    }

    // ---------- Formas (distancia con signo: positiva dentro; coordenadas de -0.5 a 0.5, y hacia arriba) ----------

    static float Circle(Vector2 p, Vector2 c, float r) { return r - (p - c).magnitude; }

    static float Ring(Vector2 p, Vector2 c, float r, float w) { return w - Mathf.Abs((p - c).magnitude - r); }

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return r - (pa - ba * h).magnitude;
    }

    static float Box(Vector2 p, Vector2 c, Vector2 half, float round)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        return -(outside + inside - round);
    }

    // Triángulo (vértices en cualquier orden)
    static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
        return Mathf.Min(Edge(p, a, b, s), Mathf.Min(Edge(p, b, c, s), Edge(p, c, a, s)));
    }

    static float Edge(Vector2 p, Vector2 a, Vector2 b, float s)
    {
        Vector2 e = b - a;
        Vector2 n = new Vector2(-e.y, e.x).normalized * s;
        return Vector2.Dot(p - a, n);
    }

    static float Union(float a, float b) { return Mathf.Max(a, b); }
    static float Cut(float a, float b) { return Mathf.Min(a, -b); }

    static System.Func<Vector2, float> Distance(UpgradeIcon icon)
    {
        switch (icon)
        {
            case UpgradeIcon.Bubbles:
                return p => Union(Union(Ring(p, new Vector2(-0.13f, -0.12f), 0.2f, 0.045f), Ring(p, new Vector2(0.2f, 0.16f), 0.13f, 0.04f)),
                                  Union(Ring(p, new Vector2(0.27f, -0.24f), 0.08f, 0.035f), Circle(p, new Vector2(-0.2f, -0.04f), 0.045f)));
            case UpgradeIcon.Missile:
                return p =>
                {
                    float body = Capsule(p, new Vector2(-0.2f, -0.2f), new Vector2(0.22f, 0.22f), 0.095f);
                    float fins = Union(Capsule(p, new Vector2(-0.2f, -0.2f), new Vector2(-0.36f, -0.06f), 0.04f),
                                       Capsule(p, new Vector2(-0.2f, -0.2f), new Vector2(-0.06f, -0.36f), 0.04f));
                    float puff = Union(Circle(p, new Vector2(-0.34f, -0.34f), 0.05f), Circle(p, new Vector2(-0.42f, -0.22f), 0.03f));
                    float window = Circle(p, new Vector2(0.08f, 0.08f), 0.035f);
                    return Cut(Union(Union(body, fins), puff), window);
                };
            case UpgradeIcon.Pierce:
                return p =>
                {
                    float shaft = Capsule(p, new Vector2(-0.4f, 0f), new Vector2(0.2f, 0f), 0.04f);
                    float head = Tri(p, new Vector2(0.42f, 0f), new Vector2(0.16f, 0.17f), new Vector2(0.16f, -0.17f));
                    float t1 = Cut(Ring(p, new Vector2(-0.14f, 0f), 0.15f, 0.03f), Box(p, new Vector2(-0.14f, 0f), new Vector2(0.3f, 0.07f), 0f));
                    float t2 = Cut(Ring(p, new Vector2(0.08f, 0f), 0.11f, 0.03f), Box(p, new Vector2(0.08f, 0f), new Vector2(0.3f, 0.07f), 0f));
                    return Union(Union(shaft, head), Union(t1, t2));
                };
            case UpgradeIcon.Tank:
                return p =>
                {
                    float tank = Box(p, new Vector2(0f, -0.05f), new Vector2(0.19f, 0.31f), 0.13f);
                    float gauge = Box(p, new Vector2(0f, 0.04f), new Vector2(0.25f, 0.025f), 0f);
                    float cap = Box(p, new Vector2(0f, 0.32f), new Vector2(0.08f, 0.06f), 0.02f);
                    float valve = Capsule(p, new Vector2(-0.12f, 0.4f), new Vector2(0.12f, 0.4f), 0.03f);
                    return Union(Union(Cut(tank, gauge), cap), valve);
                };
            case UpgradeIcon.Turbo:
                return p =>
                {
                    float outer = Union(Circle(p, new Vector2(0f, -0.14f), 0.24f), Tri(p, new Vector2(-0.22f, -0.06f), new Vector2(0.22f, -0.06f), new Vector2(0.02f, 0.44f)));
                    float inner = Union(Circle(p, new Vector2(0f, -0.17f), 0.11f), Tri(p, new Vector2(-0.1f, -0.13f), new Vector2(0.1f, -0.13f), new Vector2(0f, 0.12f)));
                    return Cut(outer, inner);
                };
            case UpgradeIcon.Dash:
                return p =>
                {
                    float a = Union(Capsule(p, new Vector2(-0.32f, 0.24f), new Vector2(-0.06f, 0f), 0.065f), Capsule(p, new Vector2(-0.06f, 0f), new Vector2(-0.32f, -0.24f), 0.065f));
                    float b = Union(Capsule(p, new Vector2(0.02f, 0.24f), new Vector2(0.28f, 0f), 0.065f), Capsule(p, new Vector2(0.28f, 0f), new Vector2(0.02f, -0.24f), 0.065f));
                    return Union(a, b);
                };
            case UpgradeIcon.Plates:
                return p =>
                {
                    float shield = Union(Box(p, new Vector2(0f, 0.1f), new Vector2(0.3f, 0.22f), 0.06f), Tri(p, new Vector2(-0.3f, -0.08f), new Vector2(0.3f, -0.08f), new Vector2(0f, -0.43f)));
                    float inner = Union(Box(p, new Vector2(0f, 0.1f), new Vector2(0.21f, 0.14f), 0.03f), Tri(p, new Vector2(-0.21f, -0.02f), new Vector2(0.21f, -0.02f), new Vector2(0f, -0.3f)));
                    float rivets = Union(Circle(p, new Vector2(-0.1f, 0.12f), 0.045f), Union(Circle(p, new Vector2(0.1f, 0.12f), 0.045f), Circle(p, new Vector2(0f, -0.08f), 0.045f)));
                    return Union(Cut(shield, inner), rivets);
                };
            case UpgradeIcon.Shield:
                return p =>
                {
                    float bubble = Ring(p, Vector2.zero, 0.36f, 0.045f);
                    float shine = Cut(Ring(p, Vector2.zero, 0.26f, 0.03f), Box(p, new Vector2(0.2f, -0.2f), new Vector2(0.32f, 0.32f), 0f));
                    float fish = Union(Circle(p, new Vector2(0.03f, -0.02f), 0.1f), Tri(p, new Vector2(-0.05f, -0.02f), new Vector2(-0.2f, 0.08f), new Vector2(-0.2f, -0.12f)));
                    return Union(Union(bubble, shine), fish);
                };
            case UpgradeIcon.Repair:
                return p =>
                {
                    float drop = Union(Circle(p, new Vector2(0f, -0.1f), 0.26f), Tri(p, new Vector2(-0.23f, -0.02f), new Vector2(0.23f, -0.02f), new Vector2(0f, 0.43f)));
                    float cross = Union(Box(p, new Vector2(0f, -0.08f), new Vector2(0.15f, 0.045f), 0.01f), Box(p, new Vector2(0f, -0.08f), new Vector2(0.045f, 0.15f), 0.01f));
                    return Cut(drop, cross);
                };
            case UpgradeIcon.Magazine:
                return p =>
                {
                    float d = -1f;
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * 0.2f;
                        d = Union(d, Capsule(p, new Vector2(x, -0.26f), new Vector2(x, 0.12f), 0.075f));
                        d = Cut(d, Box(p, new Vector2(x, 0.02f), new Vector2(0.1f, 0.018f), 0f));
                    }
                    return d;
                };
            case UpgradeIcon.Magnet:
                return p =>
                {
                    float arc = Cut(Ring(p, new Vector2(0f, 0.06f), 0.22f, 0.08f), Box(p, new Vector2(0f, -0.3f), new Vector2(0.5f, 0.36f), 0f));
                    float legs = Union(Box(p, new Vector2(-0.22f, -0.12f), new Vector2(0.08f, 0.2f), 0f), Box(p, new Vector2(0.22f, -0.12f), new Vector2(0.08f, 0.2f), 0f));
                    float tips = Box(p, new Vector2(0f, -0.2f), new Vector2(0.4f, 0.02f), 0f);
                    float sparks = Union(Circle(p, new Vector2(-0.4f, 0.3f), 0.035f), Circle(p, new Vector2(0.4f, 0.3f), 0.035f));
                    return Union(Cut(Union(arc, legs), tips), sparks);
                };
            default: // Sonar
                return p =>
                {
                    Vector2 c = new Vector2(-0.3f, -0.3f);
                    Vector2 q = p - c;
                    float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                    float wedge = Mathf.Min(ang - 8f, 82f - ang) * 0.01f;
                    float arcs = Union(Ring(p, c, 0.24f, 0.035f), Union(Ring(p, c, 0.44f, 0.035f), Ring(p, c, 0.64f, 0.035f)));
                    float dot = Circle(p, c, 0.09f);
                    return Union(Mathf.Min(arcs, wedge), dot);
                };
        }
    }

    static Sprite Make(string name, System.Func<Vector2, float> dist)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[Size * Size];
        float aa = 1.2f / Size;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
                float a = Mathf.Clamp01(dist(p) / aa + 0.5f);
                px[y * Size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        return s;
    }
}
