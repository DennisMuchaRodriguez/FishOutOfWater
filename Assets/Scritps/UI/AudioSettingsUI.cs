using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ajustes de sonido con el estilo del casco: tres deslizadores holográficos
//   VOLUMEN GENERAL (todo lo que se oye) · MÚSICA · EFECTOS (disparos, aves, el traje, la interfaz)
// Lo usan el menú principal (AJUSTES) y la pausa. Los cambios se oyen al momento y se guardan solos (PlayerPrefs).
// Ratón: arrastrar o hacer clic en la barra. Teclado: flechas arriba/abajo para elegir, izquierda/derecha para cambiar.
public static class AudioSettingsUI
{
    public const float DefaultMaster = 0.8f;
    public const float DefaultMusic = 0.7f;
    public const float DefaultEffects = 1f;

    public class Panel
    {
        public RectTransform root;
        public Slider first;
        readonly List<System.Action> refresh = new List<System.Action>();

        // Vuelve a leer los valores guardados (por si cambiaron en otra pantalla)
        public void Refresh()
        {
            foreach (System.Action a in refresh) a();
        }

        public void Add(System.Action a) { refresh.Add(a); }
    }

    static float lastPreview = -10f;

    // width: ancho de las filas. rowStep: separación entre filas
    public static Panel Build(Transform parent, Vector2 anchor, Vector2 pos, float width, float rowStep = 78f)
    {
        Panel p = new Panel();
        p.root = MenuUI.Rect("AjustesSonido", parent, anchor, pos, new Vector2(width, rowStep * 3f));
        p.first = Row(p, new Vector2(0f, rowStep), width, "VOLUMEN GENERAL", MenuUI.Speaker, MenuUI.Accent,
                      () => Sfx.Volume, v => Sfx.Volume = v, false);
        Row(p, Vector2.zero, width, "MÚSICA", MenuUI.Note, MenuUI.Purple, () => Music.Volume, v => Music.Volume = v, false);
        Row(p, new Vector2(0f, -rowStep), width, "EFECTOS", MenuUI.RingTicks, MenuUI.Orange,
            () => Sfx.EffectsVolume, v => Sfx.EffectsVolume = v, true);
        return p;
    }

    // Vuelve a los valores de fábrica
    public static void ResetDefaults(Panel p)
    {
        Sfx.Volume = DefaultMaster;
        Music.Volume = DefaultMusic;
        Sfx.EffectsVolume = DefaultEffects;
        PlayerPrefs.Save();
        if (p != null) p.Refresh();
    }

    static Slider Row(Panel p, Vector2 center, float width, string label, Sprite icon, Color color,
                      System.Func<float> get, System.Action<float> set, bool preview)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        RectTransform row = MenuUI.Rect("Fila_" + label, p.root, mid, center, new Vector2(width, 64f));
        float left = -width * 0.5f;

        MenuUI.Img("HaloIcono", row, MenuUI.Glow, MenuUI.WithAlpha(color, 0.32f), mid, new Vector2(left + 18f, 13f), new Vector2(46f, 46f));
        MenuUI.Img("Icono", row, icon, Color.Lerp(color, Color.white, 0.3f), mid, new Vector2(left + 18f, 13f), new Vector2(26f, 26f));
        TextMeshProUGUI t = MenuUI.Text("Etiqueta", row, label, 18f, mid, new Vector2(left + 46f + 130f, 13f), new Vector2(260f, 26f), MenuUI.Cream, true);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.characterSpacing = 3f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        TextMeshProUGUI val = MenuUI.Text("Valor", row, "", 18f, mid, new Vector2(width * 0.5f - 42f, 13f), new Vector2(84f, 26f),
                                          Color.Lerp(color, Color.white, 0.25f), true);
        val.alignment = TextAlignmentOptions.MidlineRight;

        Slider s = MakeSlider(row, new Vector2(23f, -15f), width - 46f, color);
        s.SetValueWithoutNotify(get());
        val.text = Percent(get());
        s.onValueChanged.AddListener(v =>
        {
            set(v);
            val.text = Percent(v);
            // Efectos: un sonido corto para oír cómo queda
            if (preview && Time.unscaledTime - lastPreview > 0.12f)
            {
                lastPreview = Time.unscaledTime;
                Sfx.Play2D(Sonido.UIPasar, 0.9f, 1f, 0.03f, 0.1f);
            }
        });
        p.Add(() =>
        {
            s.SetValueWithoutNotify(get());
            val.text = Percent(get());
        });
        return s;
    }

    static string Percent(float v)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + " %";
    }

    // Deslizador holográfico: pista con marcas cada 10 %, relleno del color de la fila y una manija brillante
    static Slider MakeSlider(RectTransform parent, Vector2 pos, float width, Color color)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        RectTransform root = MenuUI.Rect("Deslizador", parent, mid, pos, new Vector2(width, 26f));
        // Zona de clic (invisible): toda la barra responde al ratón
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        MenuUI.Img("Pista", root, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Ink, 0.92f), mid, Vector2.zero, new Vector2(width, 8f));
        MenuUI.Img("BordePista", root, MenuUI.Pixel, MenuUI.WithAlpha(color, 0.35f), mid, new Vector2(0f, -5f), new Vector2(width, 1f));
        for (int i = 0; i <= 10; i++)
        {
            float x = -width * 0.5f + width * i / 10f;
            MenuUI.Img("Marca", root, MenuUI.Pixel, MenuUI.WithAlpha(color, i % 5 == 0 ? 0.55f : 0.25f), mid, new Vector2(x, -9f),
                       new Vector2(1.5f, i % 5 == 0 ? 6f : 4f));
        }

        // Relleno: lo estira el Slider (de 0 al valor)
        RectTransform fillArea = MenuUI.Rect("ZonaRelleno", root, mid, Vector2.zero, new Vector2(width, 8f));
        RectTransform fill = MenuUI.Stretch("Relleno", fillArea);
        Image fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.sprite = MenuUI.Pixel;
        fillImg.color = color;
        fillImg.raycastTarget = false;
        Image fillGlow = MenuUI.Stretch("BrilloRelleno", fill).gameObject.AddComponent<Image>();
        fillGlow.sprite = MenuUI.Glow;
        fillGlow.color = MenuUI.WithAlpha(color, 0.35f);
        fillGlow.raycastTarget = false;
        fillGlow.rectTransform.offsetMin = new Vector2(-6f, -8f);
        fillGlow.rectTransform.offsetMax = new Vector2(6f, 8f);

        // Manija: el Slider le pone el ancla en el valor y la estira a lo alto de su zona (-4 = 22 px de alto)
        RectTransform handleArea = MenuUI.Rect("ZonaManija", root, mid, Vector2.zero, new Vector2(width, 26f));
        RectTransform handle = MenuUI.Rect("Manija", handleArea, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(22f, -4f);
        handle.anchoredPosition = Vector2.zero;
        Image glow = MenuUI.Img("Brillo", handle, MenuUI.Glow, MenuUI.WithAlpha(color, 0.55f), mid, Vector2.zero, new Vector2(48f, 48f));
        glow.raycastTarget = false;
        Image knob = MenuUI.Img("Perilla", handle, MenuUI.Circle, MenuUI.Cream, mid, Vector2.zero, new Vector2(20f, 20f));
        MenuUI.Img("Centro", handle, MenuUI.Circle, color, mid, Vector2.zero, new Vector2(9f, 9f));

        Slider s = root.gameObject.AddComponent<Slider>();
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
        s.fillRect = fill;
        s.handleRect = handle;
        s.targetGraphic = knob;
        ColorBlock cb = s.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 1f, 1f, 1f);
        cb.selectedColor = new Color(0.85f, 1f, 1f, 1f);
        cb.pressedColor = new Color(1f, 0.9f, 0.7f, 1f);
        cb.fadeDuration = 0.08f;
        s.colors = cb;
        return s;
    }
}
