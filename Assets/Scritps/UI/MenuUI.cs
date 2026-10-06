using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Ayudas para construir menús por código con el mismo estilo cian del HUD.
public static class MenuUI
{
    public static readonly Color Accent = new Color(0.41f, 0.87f, 0.9f, 1f);
    public static readonly Color Dark = new Color(0.01f, 0.04f, 0.07f, 0.92f);

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

    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        RectTransform rt = Rect(name, parent, anchor, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Accent;
        t.raycastTarget = false;
        return t;
    }

    public static Button CreateButton(string label, Transform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = Rect("Btn_" + label, parent, new Vector2(0.5f, 0.5f), pos, new Vector2(320f, 54f));
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = Color.white;

        // Borde cian
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button b = rt.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(Accent.r, Accent.g, Accent.b, 0.08f);
        cb.highlightedColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        cb.selectedColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        cb.pressedColor = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.targetGraphic = bg;
        b.onClick.AddListener(onClick);

        TextMeshProUGUI t = CreateText("Label", rt, label, 24f, new Vector2(0.5f, 0.5f), Vector2.zero, rt.sizeDelta);
        t.characterSpacing = 8f;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;

        rt.gameObject.AddComponent<MenuButtonFX>();
        return b;
    }
}
