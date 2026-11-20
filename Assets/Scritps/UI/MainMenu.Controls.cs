using TMPro;
using UnityEngine;

// Pantalla CONTROLES del menú principal (parte de MainMenu): el manual del traje.
//  - Tres paneles holográficos: 01 MOVIMIENTO (azul), 02 COMBATE y CÁMARA (naranja) y 03 EN EL LAGO (verde: el
//    objetivo y los consejos).
//  - Cada fila: las teclas dibujadas como teclas (el ratón, con el botón que se usa encendido) y la acción, con una
//    aclaración debajo cuando hace falta. Todo alineado en dos columnas: teclas a la izquierda, texto a la derecha.
public partial class MainMenu
{
    struct ControlRow
    {
        public string[] keys;     // "W", "ESPACIO"...; "#L" / "#R" = botón izquierdo / derecho del ratón
        public string action;
        public string hint;       // aclaración (opcional)
        public string section;    // si no es null: subtítulo dentro del panel (no lleva teclas)
    }

    static ControlRow Keys(string action, string hint, params string[] keys)
    {
        return new ControlRow { keys = keys, action = action, hint = hint };
    }

    static ControlRow Section(string title)
    {
        return new ControlRow { section = title };
    }

    const float ControlsColumnWidth = 360f;
    const float ControlsColumnHeight = 430f;
    const float ControlsRowStep = 50f;
    const float ControlsFirstRow = -100f;
    // Borde izquierdo del contenido de cada panel (unidades del panel, desde su centro)
    const float ControlsLeft = -ControlsColumnWidth * 0.5f + 24f;
    // Donde empieza el texto de las acciones (a la izquierda quedan las teclas)
    const float ControlsTextLeft = ControlsLeft + 100f;
    const float ControlsRight = ControlsColumnWidth * 0.5f - 20f;

    CanvasGroup BuildControlsScreen()
    {
        RectTransform s = MenuUI.Stretch("Controles", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();
        Vector2 top = new Vector2(0.5f, 1f);

        MenuUI.Title("Encabezado", s, "CONTROLES", 72f, top, new Vector2(0f, -76f), new Vector2(800f, 90f), MenuUI.Cream);
        MenuUI.Rule(s, top, new Vector2(0f, -128f), 460f, MenuUI.Accent);
        MenuUI.Text("Sub", s, "MANUAL DEL TRAJE  ·  TECLADO Y RATÓN", 15f, top, new Vector2(0f, -150f), new Vector2(700f, 22f), MenuUI.TextDim)
            .characterSpacing = 5f;

        const float y = -25f;
        RectTransform move = ControlsColumn(s, new Vector2(-384f, y), "MOVIMIENTO", "01", MenuUI.Accent, MenuUI.Chevron);
        ControlRows(move, MenuUI.Accent, new[]
        {
            Keys("Avanzar / retroceder", null, "W", "S"),
            Keys("Girar", null, "A", "D"),
            Keys("Propulsor", "En el agua: subir y salir disparado", "ESPACIO"),
            Keys("Turbo", "Mantener  ·  en el agua: impulso de nado", "SHIFT"),
            Keys("Bucear más hondo", null, "CTRL"),
            Keys("Esquive", "A la izquierda / a la derecha", "Q", "E"),
        });

        RectTransform fight = ControlsColumn(s, new Vector2(0f, y), "COMBATE", "02", MenuUI.Orange, MenuUI.RingTicks);
        ControlRows(fight, MenuUI.Orange, new[]
        {
            Keys("Disparar", "Mantener = ráfaga", "#L"),
            Keys("Disparo cargado", "Suelta cuando se llene", "#R"),
            Keys("Cambiar arma", "Pistola de agua / burbujas", "1", "2"),
            Keys("Misiles teledirigidos", "Mejora del Taller", "F"),
            Section("CÁMARA"),
            Keys("Cambiar cámara", "1ª persona · 3ª persona · mirar atrás", "R"),
            Keys("Pausa", null, "ESC"),
        });

        RectTransform lake = ControlsColumn(s, new Vector2(384f, y), "EN EL LAGO", "03", MenuUI.Green, MenuUI.Hex);
        BuildLakeTips(lake);

        MenuUI.ComicButton("VOLVER", s, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(220f, 54f), MenuUI.BlueDark, () => Go(titleScreen), 24f);
        return g;
    }

    // Panel de una columna: cristal del color de la sección, ícono, título, número y una línea debajo
    RectTransform ControlsColumn(Transform parent, Vector2 pos, string title, string number, Color color, Sprite icon)
    {
        RectTransform col = MenuUI.HoloPanel("Panel_" + title, parent, new Vector2(0.5f, 0.5f), pos,
                                             new Vector2(ControlsColumnWidth, ControlsColumnHeight),
                                             MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, color, 0.12f), 0.9f), MenuUI.WithAlpha(color, 0.85f),
                                             MenuUI.WithAlpha(color, 0.16f), MenuUI.WithAlpha(color, 0.22f), 0.05f);
        Vector2 top = new Vector2(0.5f, 1f);
        const float headY = -40f;
        MenuUI.Img("HaloIcono", col, MenuUI.Glow, MenuUI.WithAlpha(color, 0.35f), top, new Vector2(ControlsLeft + 12f, headY), new Vector2(46f, 46f));
        MenuUI.Img("Icono", col, icon, Color.Lerp(color, Color.white, 0.25f), top, new Vector2(ControlsLeft + 12f, headY), new Vector2(24f, 24f));

        TextMeshProUGUI t = MenuUI.Text("Titulo", col, title, 24f, top, new Vector2(ControlsLeft + 34f + 110f, headY), new Vector2(220f, 32f),
                                        MenuUI.Cream, true);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.characterSpacing = 3f;
        t.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI n = MenuUI.Text("Numero", col, number, 14f, top, new Vector2(ControlsRight - 24f, headY), new Vector2(48f, 20f),
                                        MenuUI.WithAlpha(color, 0.75f), true);
        n.alignment = TextAlignmentOptions.MidlineRight;
        n.characterSpacing = 2f;

        MenuUI.Img("Linea", col, MenuUI.Pixel, MenuUI.WithAlpha(color, 0.45f), top, new Vector2(0f, -64f), new Vector2(ControlsColumnWidth - 48f, 1.5f));
        MenuUI.Img("Rombo", col, MenuUI.Diamond, color, top, new Vector2(ControlsLeft, -64f), new Vector2(8f, 8f));
        return col;
    }

    void ControlRows(RectTransform col, Color accent, ControlRow[] rows)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        float textWidth = ControlsRight - ControlsTextLeft;
        float textCenter = ControlsTextLeft + textWidth * 0.5f;
        float y = ControlsFirstRow;
        foreach (ControlRow r in rows)
        {
            if (r.section != null)
            {
                // Subtítulo con su línea (separa la cámara del combate)
                TextMeshProUGUI st = MenuUI.Text("Seccion", col, r.section, 14f, top, new Vector2(ControlsLeft + 50f, y + 8f),
                                                 new Vector2(100f, 20f), MenuUI.WithAlpha(accent, 0.9f), true);
                st.alignment = TextAlignmentOptions.MidlineLeft;
                st.characterSpacing = 4f;
                float lineLeft = ControlsLeft + 96f;
                MenuUI.Img("LineaSeccion", col, MenuUI.Pixel, MenuUI.WithAlpha(accent, 0.3f), top,
                           new Vector2((lineLeft + ControlsRight) * 0.5f, y + 8f), new Vector2(ControlsRight - lineLeft, 1f));
                y -= 40f;
                continue;
            }

            float x = ControlsLeft;
            if (r.keys != null)
                foreach (string k in r.keys) x += KeyCap(col, k, x, y, accent) + 6f;

            bool hasHint = !string.IsNullOrEmpty(r.hint);
            TextMeshProUGUI a = MenuUI.Text("Accion", col, r.action, 18f, top, new Vector2(textCenter, y + (hasHint ? 8f : 0f)),
                                            new Vector2(textWidth, 24f), MenuUI.Cream);
            a.alignment = TextAlignmentOptions.MidlineLeft;
            a.textWrappingMode = TextWrappingModes.NoWrap;
            a.enableAutoSizing = true;
            a.fontSizeMin = 13f;
            a.fontSizeMax = 18f;
            if (hasHint)
            {
                TextMeshProUGUI h = MenuUI.Text("Detalle", col, r.hint, 13f, top, new Vector2(textCenter, y - 12f),
                                                new Vector2(textWidth, 18f), MenuUI.TextDim);
                h.alignment = TextAlignmentOptions.MidlineLeft;
                h.textWrappingMode = TextWrappingModes.NoWrap;
                h.enableAutoSizing = true;
                h.fontSizeMin = 10f;
                h.fontSizeMax = 13f;
                h.overflowMode = TextOverflowModes.Ellipsis;
            }
            y -= ControlsRowStep;
        }
    }

    // Una tecla (con su base, para que se vea en relieve) o el ratón. Devuelve el ancho que ocupa
    float KeyCap(RectTransform parent, string key, float x, float y, Color accent)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        if (key == "#L" || key == "#R")
        {
            const float size = 40f, width = 24f;
            Vector2 p = new Vector2(x + width * 0.5f, y);
            MenuUI.Img("RatonBrillo", parent, MenuUI.Glow, MenuUI.WithAlpha(accent, 0.28f), top, p, new Vector2(size * 1.4f, size * 1.6f));
            MenuUI.Img("Boton", parent, key == "#L" ? MenuUI.MouseLeft : MenuUI.MouseRight, accent, top, p, new Vector2(size, size));
            MenuUI.Img("Raton", parent, MenuUI.MouseLine, MenuUI.Cream, top, p, new Vector2(size, size));
            return width;
        }

        float w = Mathf.Max(30f, key.Length * 9.5f + 18f);
        Vector2 pos = new Vector2(x + w * 0.5f, y);
        Color clear = new Color(0f, 0f, 0f, 0f);
        MenuUI.HoloPanel("TeclaBase", parent, top, pos + new Vector2(0f, -3f), new Vector2(w, 30f),
                         MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, accent, 0.45f), 0.95f), MenuUI.WithAlpha(accent, 0.4f),
                         clear, clear, 0f, false, false);
        RectTransform cap = MenuUI.HoloPanel("Tecla_" + key, parent, top, pos, new Vector2(w, 30f),
                                             MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, accent, 0.2f), 0.97f),
                                             MenuUI.WithAlpha(Color.Lerp(accent, Color.white, 0.35f), 0.95f),
                                             MenuUI.WithAlpha(accent, 0.16f), MenuUI.WithAlpha(accent, 0.3f), 0.14f, false, false);
        TextMeshProUGUI t = MenuUI.Text("Letra", cap, key, 15f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(w, 26f), MenuUI.Cream, true);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.characterSpacing = 1f;
        return w;
    }

    // Panel 03: el objetivo destacado y los consejos del lago, cada uno con su ícono en un hexágono
    void BuildLakeTips(RectTransform col)
    {
        Vector2 top = new Vector2(0.5f, 1f), center = new Vector2(0.5f, 0.5f);
        RectTransform box = MenuUI.HoloPanel("Objetivo", col, top, new Vector2(0f, -128f), new Vector2(ControlsColumnWidth - 48f, 96f),
                                             MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, MenuUI.Orange, 0.18f), 0.9f),
                                             MenuUI.WithAlpha(MenuUI.Orange, 0.85f), MenuUI.WithAlpha(MenuUI.Orange, 0.18f),
                                             MenuUI.WithAlpha(MenuUI.Orange, 0.25f), 0.06f, false, false);
        MenuUI.Tag("OBJETIVO", box, new Vector2(0f, 1f), new Vector2(62f, 0f), MenuUI.Orange, 12f);
        TextMeshProUGUI goal = MenuUI.Text("Texto", box, "Derriba a todas las aves antes de que cacen a demasiados peces.", 17f,
                                           center, new Vector2(0f, -6f), new Vector2(ControlsColumnWidth - 80f, 64f), MenuUI.Cream);
        goal.enableAutoSizing = true;
        goal.fontSizeMin = 13f;
        goal.fontSizeMax = 17f;

        string hl = "<color=" + MenuUI.HexAccent + ">", end = "</color>";
        Sprite[] icons = { MenuUI.Circle, MenuUI.Hex, MenuUI.Star, MenuUI.Wrench };
        string[] tips =
        {
            "El agua " + hl + "recarga el propulsor" + end + ".",
            "La munición está en " + hl + "cápsulas" + end + " dentro del lago.",
            "Si un ave atrapa un pez, " + hl + "dispárale" + end + ": lo soltará.",
            "En el " + hl + "Taller" + end + ", el mecánico mejora tu traje.",
        };
        float textLeft = ControlsLeft + 44f;
        float textWidth = ControlsRight - textLeft;
        float y = -212f;
        for (int i = 0; i < tips.Length; i++)
        {
            Vector2 p = new Vector2(ControlsLeft + 16f, y);
            MenuUI.Img("HaloIcono", col, MenuUI.HexGlow, MenuUI.WithAlpha(MenuUI.Green, 0.22f), top, p, new Vector2(48f, 48f));
            MenuUI.Img("Hexagono", col, MenuUI.HexLine, MenuUI.WithAlpha(MenuUI.Green, 0.9f), top, p, new Vector2(36f, 36f));
            MenuUI.Img("Icono", col, icons[i], MenuUI.Cream, top, p, new Vector2(18f, 18f));
            TextMeshProUGUI t = MenuUI.Text("Consejo", col, tips[i], 15f, top, new Vector2(textLeft + textWidth * 0.5f, y), new Vector2(textWidth, 44f),
                                            MenuUI.Cream);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableAutoSizing = true;
            t.fontSizeMin = 12f;
            t.fontSizeMax = 15f;
            y -= 54f;
        }
    }
}
