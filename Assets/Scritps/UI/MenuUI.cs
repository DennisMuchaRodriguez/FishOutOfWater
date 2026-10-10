using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Estilo de la interfaz de menús, construido por código:
//  - Paneles y botones tipo cómic: contorno grueso oscuro + sombra desplazada.
//  - Colores del pez: naranja (cuerpo), azul (aletas) y cian (hologramas del traje).
//  - Fuentes: "Bangers" para títulos y "Lilita One" para textos (Resources/Fonts).
// Todos los sprites (esquinas redondeadas, círculo, estrella, candado) se generan aquí.
public static class MenuUI
{
    // ---------- Paleta ----------
    public static readonly Color Ink = new Color32(13, 27, 42, 255);          // contorno de cómic
    public static readonly Color Orange = new Color32(255, 122, 47, 255);     // cuerpo del pez
    public static readonly Color OrangeDark = new Color32(206, 74, 22, 255);
    public static readonly Color Blue = new Color32(47, 128, 237, 255);       // aletas
    public static readonly Color BlueDark = new Color32(26, 74, 156, 255);
    public static readonly Color Accent = new Color(0.41f, 0.87f, 0.9f, 1f);  // cian del HUD
    public static readonly Color Cream = new Color32(255, 246, 225, 255);
    public static readonly Color Yellow = new Color32(255, 210, 63, 255);
    public static readonly Color Green = new Color32(92, 186, 92, 255);       // nenúfares
    public static readonly Color GreenDark = new Color32(46, 120, 60, 255);
    public static readonly Color Red = new Color32(232, 64, 56, 255);
    public static readonly Color Locked = new Color32(122, 136, 152, 255);
    public static readonly Color Dark = new Color(0.01f, 0.04f, 0.07f, 0.92f);

    // ---------- Fuentes ----------
    static TMP_FontAsset titleFont, bodyFont;
    static bool fontsMissing;

    public static TMP_FontAsset TitleFont { get { LoadFonts(); return titleFont; } }
    public static TMP_FontAsset BodyFont { get { LoadFonts(); return bodyFont; } }

    static void LoadFonts()
    {
        // (se vuelven a crear si Unity las descargó al cambiar de escena)
        if (fontsMissing || (titleFont != null && bodyFont != null)) return;
        titleFont = MakeFont("Fonts/Bangers-Regular");
        bodyFont = MakeFont("Fonts/LilitaOne-Regular");
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

    // ---------- Sprites generados ----------
    static Sprite rounded, roundedSmall, circle, star, lockIcon, gradient;

    // Rectángulo redondeado blanco (se estira con 9-slice)
    public static Sprite Rounded { get { if (rounded == null) rounded = MakeRounded(96, 30, "UI_Rounded"); return rounded; } }
    public static Sprite RoundedSmall { get { if (roundedSmall == null) roundedSmall = MakeRounded(48, 12, "UI_RoundedSmall"); return roundedSmall; } }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
                circle = MakeShape(128, "UI_Circle", (x, y) => 0.47f - Mathf.Sqrt(x * x + y * y));
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
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        float aa = pixelUnits ? 1f : 1f / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = pixelUnits ? x + 0.5f - size * 0.5f : (x + 0.5f) / size - 0.5f;
                float v = pixelUnits ? y + 0.5f - size * 0.5f : (y + 0.5f) / size - 0.5f;
                float d = dist(u, v);
                float a = Mathf.Clamp01(d / aa + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, border);
        s.name = name;
        return s;
    }

    // Estrella de 5 puntas (distancia aproximada al borde, positiva dentro)
    static float StarDistance(float x, float y)
    {
        Vector2 p = new Vector2(x, y + 0.03f);
        float a = Mathf.Atan2(p.x, p.y);
        float r = p.magnitude;
        float seg = Mathf.PI * 2f / 5f;
        float k = Mathf.Repeat(a + seg * 0.5f, seg) - seg * 0.5f;     // ángulo relativo a la punta más cercana
        float outerR = 0.46f, innerR = 0.2f;
        // Radio del borde de la estrella en este ángulo (interpolando entre punta y valle)
        float t = Mathf.Abs(k) / (seg * 0.5f);
        Vector2 tip = new Vector2(0f, outerR);
        Vector2 valley = new Vector2(Mathf.Sin(seg * 0.5f), Mathf.Cos(seg * 0.5f)) * innerR;
        Vector2 dir = new Vector2(Mathf.Sin(Mathf.Abs(k)), Mathf.Cos(Mathf.Abs(k)));
        // Intersección del rayo con el segmento punta-valle
        Vector2 e = valley - tip;
        float den = dir.x * e.y - dir.y * e.x;
        float edgeR = Mathf.Abs(den) > 1e-5f ? (tip.x * e.y - tip.y * e.x) / den : Mathf.Lerp(outerR, innerR, t);
        return (edgeR - r) * 0.9f;
    }

    // Candado: cuerpo redondeado + arco
    static float LockDistance(float x, float y)
    {
        // Cuerpo
        float bx = Mathf.Abs(x) - 0.28f, by = Mathf.Abs(y + 0.12f) - 0.2f;
        float body = -(new Vector2(Mathf.Max(bx + 0.06f, 0f), Mathf.Max(by + 0.06f, 0f)).magnitude
                       + Mathf.Min(Mathf.Max(bx + 0.06f, by + 0.06f), 0f) - 0.06f);
        // Arco (anillo) solo en la mitad de arriba
        float ringR = Mathf.Sqrt(x * x + (y - 0.1f) * (y - 0.1f));
        float ring = 0.06f - Mathf.Abs(ringR - 0.18f);
        if (y < 0.1f) ring = Mathf.Min(ring, 0.06f - Mathf.Abs(Mathf.Abs(x) - 0.18f));
        if (y < -0.05f) ring = -1f;
        return Mathf.Max(body, ring);
    }

    // ---------- Canvas ----------

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

    // ---------- Piezas básicas ----------

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

    // Texto con la fuente del juego. outline > 0 = contorno oscuro (estilo cómic)
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
        if (outline > 0f)
        {
            t.outlineWidth = outline;
            t.outlineColor = Ink;
        }
        return t;
    }

    // Compatibilidad con los menús anteriores
    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        return Text(name, parent, text, size, anchor, pos, box, Accent);
    }

    // Título grande: letras de cómic con contorno y sombra desplazada
    public static TextMeshProUGUI Title(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 pos,
                                        Vector2 box, Color color)
    {
        RectTransform holder = Rect(name, parent, anchor, pos, box);
        TextMeshProUGUI shadow = Text("Sombra", holder, text, size, new Vector2(0.5f, 0.5f), new Vector2(size * 0.06f, -size * 0.07f), box, Ink, true, 0.25f);
        shadow.color = new Color(Ink.r, Ink.g, Ink.b, 0.85f);
        TextMeshProUGUI main = Text("Texto", holder, text, size, new Vector2(0.5f, 0.5f), Vector2.zero, box, color, true, 0.22f);
        main.characterSpacing = shadow.characterSpacing = 4f;
        return main;
    }

    // Panel de cómic: sombra + contorno + relleno. Devuelve la raíz (lo que se agregue va encima)
    public static RectTransform ComicPanel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill,
                                           float border = 5f, float shadow = 9f)
    {
        RectTransform root = Rect(name, parent, anchor, pos, size);
        Image sh = Img("Sombra", root, Rounded, new Color(Ink.r, Ink.g, Ink.b, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(shadow, -shadow), size);
        sh.rectTransform.anchorMin = Vector2.zero; sh.rectTransform.anchorMax = Vector2.one;
        sh.rectTransform.offsetMin = new Vector2(shadow, -shadow); sh.rectTransform.offsetMax = new Vector2(shadow, -shadow);
        Image br = Img("Contorno", root, Rounded, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        br.rectTransform.anchorMin = Vector2.zero; br.rectTransform.anchorMax = Vector2.one;
        br.rectTransform.offsetMin = br.rectTransform.offsetMax = Vector2.zero;
        Image fl = Img("Relleno", root, Rounded, fill, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        fl.rectTransform.anchorMin = Vector2.zero; fl.rectTransform.anchorMax = Vector2.one;
        fl.rectTransform.offsetMin = new Vector2(border, border); fl.rectTransform.offsetMax = new Vector2(-border, -border);
        return root;
    }

    // Botón de cómic. 'fill' = color del botón
    public static Button ComicButton(string label, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill,
                                     UnityEngine.Events.UnityAction onClick, float fontSize = 28f)
    {
        RectTransform root = ComicPanel("Btn_" + label, parent, anchor, pos, size, fill, 4f, 7f);
        Image fillImg = root.Find("Relleno").GetComponent<Image>();
        fillImg.raycastTarget = true;

        // Brillo superior (como plástico del traje)
        Image shine = Img("Brillo", root, RoundedSmall, new Color(1f, 1f, 1f, 0.22f), new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.22f),
                          new Vector2(size.x - 26f, size.y * 0.28f));
        shine.raycastTarget = false;

        TextMeshProUGUI t = Text("Label", root, label, fontSize, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size - new Vector2(16f, 4f), Cream, false, 0.28f);
        t.characterSpacing = 2f;
        t.textWrappingMode = TextWrappingModes.NoWrap;

        Button b = root.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
        cb.selectedColor = new Color(1f, 0.95f, 0.85f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.6f, 1f);
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

    // Fila de 3 estrellas (las ganadas en amarillo). Devuelve las imágenes para animarlas
    public static Image[] Stars(Transform parent, Vector2 anchor, Vector2 pos, float size, int earned, float spacing = 1.1f)
    {
        Image[] imgs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Vector2 p = pos + new Vector2((i - 1) * size * spacing, i == 1 ? size * 0.12f : 0f);
            Img("EstrellaSombra" + i, parent, Star, Ink, anchor, p + new Vector2(size * 0.06f, -size * 0.08f), Vector2.one * size * 1.12f);
            imgs[i] = Img("Estrella" + i, parent, Star, i < earned ? Yellow : new Color(0.35f, 0.42f, 0.5f, 1f), anchor, p, Vector2.one * size);
        }
        return imgs;
    }

    // Etiqueta pequeña tipo píldora (JEFE, TALLER, PRÓXIMAMENTE...)
    public static RectTransform Tag(string text, Transform parent, Vector2 anchor, Vector2 pos, Color fill, float fontSize = 15f)
    {
        float w = Mathf.Max(56f, text.Length * fontSize * 0.62f + 24f);
        RectTransform root = ComicPanel("Tag_" + text, parent, anchor, pos, new Vector2(w, fontSize + 14f), fill, 3f, 3f);
        Text("Texto", root, text, fontSize, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(w, fontSize + 10f), Cream, false, 0.25f)
            .textWrappingMode = TextWrappingModes.NoWrap;
        return root;
    }
}
