using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Archivo de aves: pantalla que se abre desde el mapa de niveles (botón ARCHIVO DE AVES).
// A la izquierda, la lista de especies del catálogo de niveles; a la derecha, la ficha de la especie elegida
// con su boceto holográfico (Resources/Archivo), estadísticas, cómo caza, cómo pelea, ataques de los jefes
// y un consejo. Las especies que aún no se vieron en combate salen como "???" con el nivel donde aparecen.
// Atajos: flechas arriba/abajo para moverse, ESC vuelve al mapa, F9 registra todas las aves (prueba).
public partial class MainMenu
{
    const int StatSegments = 16;
    static readonly string[] StatNames = { "VIDA", "VELOCIDAD", "DAÑO", "VISTA" };
    const string HexAttackName = "#B8EBFF";
    const string HexAttackText = "#96B2D6";
    const string ArchiveHint = "Cada especie se registra la primera vez que aparece en combate";

    CanvasGroup archiveScreen;
    RectTransform archiveBadge;
    TextMeshProUGUI archiveBadgeText;
    Image archiveBadgeGlow;

    RectTransform archiveListHolder;
    TextMeshProUGUI archiveCount;
    TextMeshProUGUI archiveMessage;
    float archiveMessageTimer;
    readonly List<ArchiveRow> archiveRows = new List<ArchiveRow>();
    int archiveSelected = -1;

    // Ficha
    RawImage archiveSketch;
    Image archiveHalo, archiveRingA, archiveRingB, archiveLock, archiveScanLine, archiveScanGlow;
    TextMeshProUGUI archiveHeader, archiveName, archiveRole, archiveFacts, archiveTip, archiveBody;
    RectTransform archiveTags;
    readonly Image[][] archiveBars = new Image[4][];
    readonly TextMeshProUGUI[] archiveBarValues = new TextMeshProUGUI[4];
    readonly float[] archiveBarTargets = new float[4];
    bool archiveShownSeen;
    int archiveBodyChars;
    float archiveReveal = 1f;

    static readonly Dictionary<string, Texture2D> sketches = new Dictionary<string, Texture2D>();
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    const float SketchSize = 300f;

    class ArchiveRow
    {
        public BirdArchive.Entry entry;
        public Image bg;
        public Image mark;
        public RawImage thumb;
        public TextMeshProUGUI name;
        public RectTransform badge;
        public Image badgeGlow;
    }

    static Texture2D Sketch(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;
        Texture2D tex;
        if (!sketches.TryGetValue(assetName, out tex))
        {
            tex = Resources.Load<Texture2D>("Archivo/" + assetName);
            sketches[assetName] = tex;
        }
        return tex;
    }

    // ======================= BOTÓN EN EL MAPA =======================

    void BuildArchiveButton(RectTransform mapRoot)
    {
        Button b = MenuUI.ComicButton("ARCHIVO DE AVES", mapRoot, new Vector2(1f, 1f), new Vector2(-420f, -50f), new Vector2(250f, 48f),
                                      MenuUI.Blue, () => Go(archiveScreen), 20f);
        // Aviso amarillo con las especies nuevas sin abrir
        archiveBadge = MenuUI.Rect("Aviso", b.transform, new Vector2(1f, 1f), new Vector2(-6f, -4f), new Vector2(28f, 28f));
        archiveBadgeGlow = MenuUI.Img("Brillo", archiveBadge, MenuUI.Glow, MenuUI.WithAlpha(MenuUI.Yellow, 0.5f), Mid, Vector2.zero, new Vector2(56f, 56f));
        MenuUI.Img("Fondo", archiveBadge, MenuUI.Circle, MenuUI.Yellow, Mid, Vector2.zero, new Vector2(28f, 28f));
        archiveBadgeText = MenuUI.Text("Num", archiveBadge, "", 16f, Mid, new Vector2(0f, 1f), new Vector2(28f, 28f), MenuUI.Ink, true);
        RefreshArchiveBadge();
    }

    void RefreshArchiveBadge()
    {
        if (archiveBadge == null) return;
        int n = SaveSystem.NewBirdCount;
        archiveBadge.gameObject.SetActive(n > 0);
        archiveBadgeText.text = n > 9 ? "9+" : n.ToString();
    }

    // Aviso del botón del mapa: late mientras haya especies nuevas
    void UpdateArchiveBadge(float time)
    {
        if (archiveBadge == null || !archiveBadge.gameObject.activeSelf) return;
        float pulse = Mathf.Sin(time * 4f) * 0.5f + 0.5f;
        archiveBadgeGlow.color = MenuUI.WithAlpha(MenuUI.Yellow, 0.25f + 0.4f * pulse);
        archiveBadge.localScale = Vector3.one * (1f + 0.08f * pulse);
    }

    // ======================= PANTALLA =======================

    CanvasGroup BuildArchiveScreen()
    {
        RectTransform s = MenuUI.Stretch("Archivo", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Encabezado", s, "ARCHIVO DE AVES", 56f, new Vector2(0f, 1f), new Vector2(280f, -50f), new Vector2(540f, 76f), MenuUI.Cream);
        archiveCount = MenuUI.Text("Registradas", s, "", 20f, new Vector2(1f, 1f), new Vector2(-210f, -50f), new Vector2(360f, 40f), MenuUI.Accent, true);
        archiveCount.alignment = TextAlignmentOptions.MidlineRight;
        archiveCount.characterSpacing = 3f;

        Color edge = MenuUI.WithAlpha(MenuUI.Accent, 0.9f);
        Color glow = MenuUI.WithAlpha(MenuUI.Accent, 0.16f);
        Color inner = MenuUI.WithAlpha(MenuUI.Accent, 0.25f);

        // Lista de especies (izquierda)
        RectTransform list = MenuUI.HoloPanel("Lista", s, Mid, new Vector2(-400f, -24f), new Vector2(380f, 548f), MenuUI.Glass, edge, glow, inner, 0.04f);
        TextMeshProUGUI head = MenuUI.Text("Cabecera", list, "REGISTRO DE ESPECIES", 14f, Mid, new Vector2(0f, 252f), new Vector2(340f, 22f), MenuUI.TextDim, true);
        head.characterSpacing = 5f;
        archiveListHolder = MenuUI.Rect("Filas", list, Mid, Vector2.zero, new Vector2(380f, 548f));
        TextMeshProUGUI foot = MenuUI.Text("Pie", list, "RED DE SONAR DEL TRAJE  //  DATOS DE CAMPO", 11f, Mid, new Vector2(0f, -258f),
                                           new Vector2(340f, 18f), MenuUI.WithAlpha(MenuUI.TextDim, 0.6f), true);
        foot.characterSpacing = 3f;

        // Ficha (derecha)
        RectTransform detail = MenuUI.HoloPanel("Ficha", s, Mid, new Vector2(190f, -24f), new Vector2(780f, 548f), MenuUI.Glass, edge, glow, inner, 0.04f);
        BuildArchiveDetail(detail);

        MenuUI.ComicButton("VOLVER", s, new Vector2(0f, 0f), new Vector2(120f, 40f), new Vector2(180f, 48f), MenuUI.BlueDark, () => Go(mapScreen), 22f);
        archiveMessage = MenuUI.Text("Mensaje", s, ArchiveHint, 18f, new Vector2(0.5f, 0f), new Vector2(60f, 40f), new Vector2(760f, 36f), MenuUI.TextDim);
        return g;
    }

    void BuildArchiveDetail(RectTransform d)
    {
        Color dim = MenuUI.TextDim;

        archiveHeader = MenuUI.Text("Cabecera", d, "", 13f, Mid, new Vector2(-205f, 250f), new Vector2(340f, 22f), dim, true);
        archiveHeader.characterSpacing = 3f;

        // Visor del boceto: halo, rejilla hexagonal, aros con marcas que giran, cruz de mira y línea de escaneo
        RectTransform view = MenuUI.Rect("Visor", d, Mid, new Vector2(-205f, 64f), new Vector2(330f, 330f));
        archiveHalo = MenuUI.Img("Halo", view, MenuUI.Glow, MenuUI.WithAlpha(MenuUI.AccentDeep, 0.35f), Mid, Vector2.zero, new Vector2(400f, 400f));
        Texture2D hexTex = MenuUI.HexGridTexture;
        RawImage grid = MenuUI.Rect("Rejilla", view, Mid, Vector2.zero, new Vector2(SketchSize, SketchSize)).gameObject.AddComponent<RawImage>();
        grid.texture = hexTex;
        grid.uvRect = new Rect(0f, 0f, SketchSize / hexTex.width, SketchSize / hexTex.height);
        grid.color = MenuUI.WithAlpha(MenuUI.Accent, 0.06f);
        grid.raycastTarget = false;
        MenuUI.Img("Aro", view, MenuUI.Ring, MenuUI.WithAlpha(MenuUI.Accent, 0.22f), Mid, Vector2.zero, new Vector2(318f, 318f));
        archiveRingA = MenuUI.Img("Marcas", view, MenuUI.RingTicks, MenuUI.WithAlpha(MenuUI.Accent, 0.55f), Mid, Vector2.zero, new Vector2(338f, 338f));
        archiveRingB = MenuUI.Img("MarcasInt", view, MenuUI.RingTicks, MenuUI.WithAlpha(MenuUI.AccentGlow, 0.16f), Mid, Vector2.zero, new Vector2(286f, 286f));
        MenuUI.Img("EjeH", view, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Accent, 0.1f), Mid, Vector2.zero, new Vector2(SketchSize, 1f));
        MenuUI.Img("EjeV", view, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Accent, 0.1f), Mid, Vector2.zero, new Vector2(1f, SketchSize));
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? 1f : -1f;
            Vector2 corner = new Vector2(sx * 158f, sy * 158f);
            MenuUI.Img("Mira", view, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.AccentGlow, 0.6f), Mid, corner - new Vector2(sx * 9f, 0f), new Vector2(18f, 1.5f));
            MenuUI.Img("Mira", view, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.AccentGlow, 0.6f), Mid, corner - new Vector2(0f, sy * 9f), new Vector2(1.5f, 18f));
        }
        RectTransform sketchRt = MenuUI.Rect("Boceto", view, Mid, Vector2.zero, new Vector2(SketchSize, SketchSize));
        archiveSketch = sketchRt.gameObject.AddComponent<RawImage>();
        archiveSketch.raycastTarget = false;
        archiveLock = MenuUI.Img("Candado", view, MenuUI.Lock, MenuUI.WithAlpha(MenuUI.Locked, 0.9f), Mid, Vector2.zero, new Vector2(64f, 64f));
        archiveScanGlow = MenuUI.Img("EscanerHalo", view, MenuUI.Glow, MenuUI.WithAlpha(MenuUI.Accent, 0f), Mid, Vector2.zero, new Vector2(340f, 30f));
        archiveScanLine = MenuUI.Img("Escaner", view, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.AccentGlow, 0f), Mid, Vector2.zero, new Vector2(SketchSize, 2f));

        archiveFacts = MenuUI.Text("Datos", d, "", 15f, Mid, new Vector2(-205f, -122f), new Vector2(340f, 26f), dim, true);
        archiveFacts.characterSpacing = 2f;

        // Consejo (recuadro amarillo)
        RectTransform tip = MenuUI.HoloPanel("Consejo", d, Mid, new Vector2(-205f, -200f), new Vector2(340f, 112f),
                                             MenuUI.WithAlpha(MenuUI.Ink, 0.6f), MenuUI.WithAlpha(MenuUI.Yellow, 0.7f),
                                             MenuUI.WithAlpha(MenuUI.Yellow, 0.08f), MenuUI.WithAlpha(MenuUI.Yellow, 0.07f), 0f, false, false);
        TextMeshProUGUI tipTitle = MenuUI.Text("Titulo", tip, "// CONSEJO", 13f, Mid, new Vector2(0f, 40f), new Vector2(310f, 20f), MenuUI.Yellow, true);
        tipTitle.alignment = TextAlignmentOptions.MidlineLeft;
        tipTitle.characterSpacing = 3f;
        archiveTip = MenuUI.Text("Texto", tip, "", 15f, Mid, new Vector2(0f, -10f), new Vector2(310f, 74f), MenuUI.Cream);
        archiveTip.alignment = TextAlignmentOptions.TopLeft;
        archiveTip.enableAutoSizing = true;
        archiveTip.fontSizeMax = 15f;
        archiveTip.fontSizeMin = 11f;

        // Separador vertical entre columnas
        MenuUI.Img("Separador", d, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Accent, 0.14f), Mid, new Vector2(-18f, 0f), new Vector2(1f, 500f));

        // Columna derecha: nombre, rol, etiquetas, estadísticas y textos
        archiveName = MenuUI.Text("Nombre", d, "", 34f, Mid, new Vector2(185f, 226f), new Vector2(370f, 48f), MenuUI.Cream, true);
        archiveName.alignment = TextAlignmentOptions.MidlineLeft;
        archiveName.textWrappingMode = TextWrappingModes.NoWrap;
        archiveName.enableAutoSizing = true;
        archiveName.fontSizeMax = 34f;
        archiveName.fontSizeMin = 20f;
        archiveRole = MenuUI.Text("Rol", d, "", 16f, Mid, new Vector2(185f, 194f), new Vector2(370f, 24f), MenuUI.Accent, true);
        archiveRole.alignment = TextAlignmentOptions.MidlineLeft;
        archiveRole.characterSpacing = 3f;
        archiveTags = MenuUI.Rect("Etiquetas", d, Mid, new Vector2(185f, 164f), new Vector2(370f, 28f));

        for (int k = 0; k < 4; k++)
        {
            float y = 128f - k * 25f;
            TextMeshProUGUI label = MenuUI.Text("Stat" + k, d, StatNames[k], 14f, Mid, new Vector2(40f, y), new Vector2(110f, 22f), dim, true);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.characterSpacing = 2f;
            archiveBars[k] = new Image[StatSegments];
            for (int j = 0; j < StatSegments; j++)
                archiveBars[k][j] = MenuUI.Img("Seg", d, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Locked, 0.35f), Mid,
                                               new Vector2(105.5f + j * 14f, y), new Vector2(11f, 10f));
            archiveBarValues[k] = MenuUI.Text("Valor" + k, d, "", 15f, Mid, new Vector2(355f, y), new Vector2(60f, 22f), MenuUI.Cream, true);
            archiveBarValues[k].alignment = TextAlignmentOptions.MidlineRight;
        }
        MenuUI.Img("Linea", d, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Accent, 0.3f), Mid, new Vector2(185f, 26f), new Vector2(370f, 1f));
        MenuUI.Img("Rombo", d, MenuUI.Diamond, MenuUI.Accent, Mid, new Vector2(0f, 26f), new Vector2(9f, 9f));

        archiveBody = MenuUI.Text("Texto", d, "", 16f, Mid, new Vector2(185f, -122f), new Vector2(370f, 276f), MenuUI.Cream);
        archiveBody.alignment = TextAlignmentOptions.TopLeft;
        archiveBody.enableAutoSizing = true;
        archiveBody.fontSizeMax = 16f;
        archiveBody.fontSizeMin = 12f;
        archiveBody.lineSpacing = -6f;
    }

    // ======================= LISTA =======================

    void RefreshArchive()
    {
        if (archiveListHolder == null) return;
        foreach (Transform c in archiveListHolder) Destroy(c.gameObject);
        archiveRows.Clear();

        List<BirdArchive.Entry> entries = BirdArchive.Entries;
        int n = entries.Count;
        archiveCount.text = "REGISTRADAS  " + BirdArchive.SeenCount + " / " + n;
        if (archiveMessageTimer <= 0f)
        {
            archiveMessage.text = ArchiveHint;
            archiveMessage.color = MenuUI.TextDim;
        }
        if (n == 0)
        {
            MenuUI.Text("Vacio", archiveListHolder, "No hay aves en el catálogo de niveles", 18f, Mid, Vector2.zero, new Vector2(320f, 80f), MenuUI.TextDim);
            archiveSelected = -1;
            ShowArchiveEntry(null, 0, 0);
            return;
        }

        float rowH = Mathf.Min(40f, 476f / n);
        for (int i = 0; i < n; i++) BuildArchiveRow(i, entries[i], new Vector2(0f, 218f - i * rowH), rowH - 4f);

        // Al abrir: la primera especie nueva, o la que estaba elegida, o la primera vista
        int pick = -1;
        for (int i = 0; i < n && pick < 0; i++) if (entries[i].IsNew) pick = i;
        if (pick < 0 && archiveSelected >= 0 && archiveSelected < n) pick = archiveSelected;
        for (int i = 0; i < n && pick < 0; i++) if (entries[i].Seen) pick = i;
        SelectArchive(pick < 0 ? 0 : pick);
    }

    void BuildArchiveRow(int index, BirdArchive.Entry e, Vector2 pos, float h)
    {
        ArchiveRow row = new ArchiveRow { entry = e };
        bool seen = e.Seen;
        RectTransform r = MenuUI.Rect("Fila" + index, archiveListHolder, Mid, pos, new Vector2(352f, h));
        row.bg = r.gameObject.AddComponent<Image>();
        row.bg.sprite = MenuUI.RoundedSmall;
        row.bg.type = Image.Type.Sliced;
        row.bg.color = MenuUI.WithAlpha(MenuUI.Accent, 0.1f);
        Button b = r.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.6f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.selectedColor = cb.normalColor;
        cb.pressedColor = new Color(1f, 1f, 1f, 1f);
        cb.colorMultiplier = 2f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.targetGraphic = row.bg;
        // Las flechas las maneja UpdateArchive (si no, el EventSystem también movería la selección)
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        int idx = index;
        b.onClick.AddListener(() => SelectArchive(idx));
        r.gameObject.AddComponent<MenuButtonFX>();

        row.mark = MenuUI.Img("Marca", r, MenuUI.Pixel, MenuUI.AccentGlow, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(3f, h - 6f));
        row.mark.enabled = false;

        TextMeshProUGUI num = MenuUI.Text("Num", r, (index + 1).ToString("00"), 13f, new Vector2(0f, 0.5f), new Vector2(22f, 0f),
                                          new Vector2(30f, h), MenuUI.WithAlpha(MenuUI.TextDim, 0.8f), true);
        num.characterSpacing = 1f;

        // Miniatura del boceto sobre un hexágono. Las aves van con las alas abiertas (más anchas que altas):
        // se recorta solo el margen de arriba y abajo del dibujo para que el ave se vea grande
        MenuUI.Img("Marco", r, MenuUI.HexLine, MenuUI.WithAlpha(seen ? MenuUI.Accent : MenuUI.Locked, 0.6f), new Vector2(0f, 0.5f),
                   new Vector2(62f, 0f), new Vector2(h, h));
        row.thumb = MenuUI.Rect("Boceto", r, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(h * 1.5f, h)).gameObject.AddComponent<RawImage>();
        row.thumb.raycastTarget = false;
        row.thumb.uvRect = new Rect(0.05f, 0.2f, 0.86f, 0.58f);
        row.thumb.texture = Sketch(e.assetName);
        row.thumb.enabled = row.thumb.texture != null;
        row.thumb.color = seen ? Color.white : new Color(0.12f, 0.22f, 0.4f, 0.55f);

        string label = seen ? e.Species : (e.isBoss ? "JEFE ???" : "???");
        row.name = MenuUI.Text("Nombre", r, label, 17f, new Vector2(0f, 0.5f), new Vector2(196f, 0f), new Vector2(200f, h),
                               seen ? MenuUI.Cream : MenuUI.Locked, true);
        row.name.alignment = TextAlignmentOptions.MidlineLeft;
        row.name.textWrappingMode = TextWrappingModes.NoWrap;
        row.name.overflowMode = TextOverflowModes.Ellipsis;
        row.name.enableAutoSizing = true;
        row.name.fontSizeMax = 17f;
        row.name.fontSizeMin = 13f;

        // Derecha: calavera de jefe o candado, nivel donde aparece y aviso de NUEVO
        if (e.isBoss)
            MenuUI.Img("Jefe", r, MenuUI.Skull, MenuUI.WithAlpha(MenuUI.Red, seen ? 0.95f : 0.45f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(18f, 18f));
        else if (!seen)
            MenuUI.Img("Candado", r, MenuUI.Lock, MenuUI.WithAlpha(MenuUI.Locked, 0.8f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(16f, 16f));
        if (!seen)
        {
            TextMeshProUGUI lvl = MenuUI.Text("Nivel", r, "NIVEL " + e.firstLevel, 11f, new Vector2(1f, 0.5f), new Vector2(-62f, 0f),
                                              new Vector2(60f, h), MenuUI.WithAlpha(MenuUI.TextDim, 0.7f), true);
            lvl.characterSpacing = 1f;
        }
        row.badge = MenuUI.Rect("Nuevo", r, new Vector2(1f, 0.5f), new Vector2(e.isBoss ? -62f : -36f, 0f), new Vector2(56f, h));
        row.badgeGlow = MenuUI.Img("Brillo", row.badge, MenuUI.Glow, MenuUI.WithAlpha(MenuUI.Yellow, 0.35f), Mid, Vector2.zero, new Vector2(70f, 30f));
        TextMeshProUGUI nt = MenuUI.Text("Texto", row.badge, "NUEVO", 12f, Mid, Vector2.zero, new Vector2(56f, h), MenuUI.Yellow, true);
        nt.characterSpacing = 2f;
        row.badge.gameObject.SetActive(e.IsNew);

        archiveRows.Add(row);
    }

    void SelectArchive(int index)
    {
        if (index < 0 || index >= archiveRows.Count) return;
        archiveSelected = index;
        BirdArchive.Entry e = archiveRows[index].entry;
        if (e.IsNew)
        {
            SaveSystem.MarkBirdViewed(e.Species);
            archiveRows[index].badge.gameObject.SetActive(false);
            RefreshArchiveBadge();
        }
        for (int i = 0; i < archiveRows.Count; i++)
        {
            bool sel = i == index;
            archiveRows[i].mark.enabled = sel;
            archiveRows[i].bg.color = MenuUI.WithAlpha(MenuUI.Accent, sel ? 0.3f : 0.1f);
        }
        ShowArchiveEntry(e, index, archiveRows.Count);
    }

    // ======================= FICHA =======================

    static string Section(string title, string body)
    {
        return "<size=78%><color=" + MenuUI.HexAccent + "><cspace=3>// " + title + "</cspace></color></size>\n" + body + "\n<size=45%>\n</size>";
    }

    void ShowArchiveEntry(BirdArchive.Entry e, int index, int count)
    {
        foreach (Transform c in archiveTags) Destroy(c.gameObject);
        archiveReveal = 0f;
        bool seen = e != null && e.Seen;
        archiveShownSeen = seen;

        Texture2D tex = e != null ? Sketch(e.assetName) : null;
        archiveSketch.texture = tex;
        archiveSketch.enabled = tex != null;
        archiveLock.enabled = !seen;

        if (e == null)
        {
            archiveHeader.text = "SIN REGISTROS";
            archiveName.text = "---";
            archiveRole.text = "";
            archiveFacts.text = "";
            archiveTip.text = "";
            archiveBody.text = "";
            for (int k = 0; k < 4; k++) { archiveBarTargets[k] = 0f; archiveBarValues[k].text = "--"; }
            archiveBodyChars = 0;
            return;
        }

        archiveHeader.text = "FICHA " + (index + 1).ToString("00") + " / " + count.ToString("00") + "  //  " +
                             (seen ? (e.isBoss ? "JEFE IDENTIFICADO" : "ESPECIE IDENTIFICADA") : "SEÑAL DESCONOCIDA");
        archiveHeader.color = seen ? MenuUI.TextDim : MenuUI.WithAlpha(MenuUI.Red, 0.8f);

        if (seen)
        {
            archiveName.text = e.Species.ToUpperInvariant();
            archiveName.color = MenuUI.Cream;
            archiveRole.text = (e.isBoss ? "JEFE  //  " : "ESTILO DE COMBATE  //  ") + e.role;
            archiveRole.color = e.isBoss ? Color.Lerp(MenuUI.Red, MenuUI.Cream, 0.25f) : MenuUI.Accent;
            archiveFacts.text = "NIVEL " + e.firstLevel + "  //  DERRIBAD" + (e.isBoss ? "O: " : "AS: ") + e.Kills;
            archiveTip.text = e.tip;

            string body;
            if (e.isBoss)
            {
                string atk = "";
                if (e.attacks != null)
                    foreach (BirdArchive.Attack a in e.attacks)
                        atk += "<color=" + HexAttackName + "><b>" + a.name + "</b></color>  <color=" + HexAttackText + ">" + a.text + "</color>\n";
                body = Section("AMENAZA PARA LOS PECES", e.hunt) + Section("EN COMBATE", e.fight) + Section("ATAQUES FUERTES", atk.TrimEnd('\n'));
            }
            else
            {
                body = Section("CÓMO CAZA", e.hunt) + Section("EN COMBATE", e.fight);
            }
            archiveBody.text = body;

            archiveBarTargets[0] = e.Health01;
            archiveBarTargets[1] = e.Speed01;
            archiveBarTargets[2] = e.Damage01;
            archiveBarTargets[3] = e.Sight01;
            archiveBarValues[0].text = e.type.health.ToString();
            archiveBarValues[1].text = "x" + e.type.speedMultiplier.ToString("0.0#");
            archiveBarValues[2].text = Mathf.RoundToInt(e.type.damage).ToString();
            archiveBarValues[3].text = Mathf.RoundToInt(e.type.detectRange) + " m";

            float x = 0f;
            if (e.tags != null)
                foreach (string tag in e.tags)
                {
                    const float fs = 12f;
                    float w = Mathf.Max(48f, tag.Length * fs * 0.6f + 18f);   // mismo ancho que calcula MenuUI.Tag
                    if (x + w > 370f) break;
                    Color fill = tag == "JEFE" ? MenuUI.Red : (tag == "BLINDADA" ? MenuUI.Purple : MenuUI.Blue);
                    MenuUI.Tag(tag, archiveTags, new Vector2(0f, 0.5f), new Vector2(x + w * 0.5f, 0f), fill, fs);
                    x += w + 6f;
                }
        }
        else
        {
            archiveName.text = e.isBoss ? "JEFE SIN IDENTIFICAR" : "???";
            archiveName.color = MenuUI.Locked;
            archiveRole.text = "SEÑAL NO IDENTIFICADA";
            archiveRole.color = MenuUI.TextDim;
            archiveFacts.text = "APARECE EN EL NIVEL " + e.firstLevel;
            archiveTip.text = "Llega al nivel " + e.firstLevel + " para encontrarte con esta especie y completar su ficha.";
            archiveBody.text = Section("SIN DATOS", "Esta especie todavía no aparece en los registros del traje. " +
                                                   "El sonar la identifica sola la primera vez que la tengas cerca en combate.");
            for (int k = 0; k < 4; k++) { archiveBarTargets[k] = 0f; archiveBarValues[k].text = "--"; }
            if (e.isBoss) MenuUI.Tag("JEFE", archiveTags, new Vector2(0f, 0.5f), new Vector2(24f, 0f), MenuUI.Red, 12f);
        }

        archiveBody.maxVisibleCharacters = 99999;
        archiveBody.ForceMeshUpdate(true);
        archiveBodyChars = archiveBody.textInfo.characterCount;
        archiveBody.maxVisibleCharacters = 0;
    }

    // ======================= ANIMACIÓN =======================

    void UpdateArchive(float dt, float time)
    {
        archiveReveal = Mathf.Min(1f, archiveReveal + dt / 0.75f);
        float k = archiveReveal;
        float ease = Mathf.SmoothStep(0f, 1f, k);
        float breathe = Mathf.Sin(time * 1.7f) * 0.5f + 0.5f;

        // Aros que giran y halo que respira
        archiveRingA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 18f);
        archiveRingB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 32f);
        archiveHalo.color = MenuUI.WithAlpha(MenuUI.AccentDeep, 0.25f + 0.15f * breathe + (1f - ease) * 0.25f);

        // Boceto: se dibuja de arriba hacia abajo detrás de la línea de escaneo, con parpadeo de holograma
        if (archiveSketch.enabled)
        {
            float shown = archiveShownSeen ? ease : 1f;
            RectTransform rt = archiveSketch.rectTransform;
            rt.sizeDelta = new Vector2(SketchSize, SketchSize * shown);
            rt.anchoredPosition = new Vector2(0f, SketchSize * 0.5f * (1f - shown));
            archiveSketch.uvRect = new Rect(0f, 1f - shown, 1f, shown);
            float flicker = k < 1f ? (Random.value > 0.25f ? 1f : 0.55f) : 0.92f + 0.08f * Mathf.Sin(time * 23f) * Mathf.Sin(time * 3.1f);
            archiveSketch.color = archiveShownSeen ? new Color(1f, 1f, 1f, flicker)
                                                   : new Color(0.12f, 0.22f, 0.4f, 0.5f + 0.1f * breathe);
        }
        archiveLock.color = MenuUI.WithAlpha(MenuUI.Locked, 0.6f + 0.3f * breathe);

        // Línea de escaneo: barre al abrir la ficha y después pasa despacio cada pocos segundos
        float scanY, scanA;
        if (k < 1f)
        {
            scanY = Mathf.Lerp(SketchSize * 0.5f, -SketchSize * 0.5f, ease);
            scanA = 1f;
        }
        else
        {
            float u = Mathf.Repeat(time / 3.2f, 1f);
            scanY = Mathf.Lerp(SketchSize * 0.5f, -SketchSize * 0.5f, u);
            scanA = 0.3f * Mathf.Sin(u * Mathf.PI);
        }
        archiveScanLine.rectTransform.anchoredPosition = new Vector2(0f, scanY);
        archiveScanGlow.rectTransform.anchoredPosition = new Vector2(0f, scanY);
        archiveScanLine.color = MenuUI.WithAlpha(MenuUI.AccentGlow, 0.85f * scanA);
        archiveScanGlow.color = MenuUI.WithAlpha(MenuUI.Accent, 0.35f * scanA);

        // Nombre con parpadeo al aparecer
        archiveName.alpha = k < 0.35f ? (Random.value > 0.35f ? 1f : 0.25f) : 1f;

        // Barras segmentadas que se llenan; el segmento de la punta destella
        float fillK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.3f));
        for (int s = 0; s < 4; s++)
        {
            int litCount = Mathf.CeilToInt(archiveBarTargets[s] * StatSegments * fillK - 0.001f);
            for (int j = 0; j < StatSegments; j++)
            {
                Color c;
                if (j < litCount)
                {
                    c = Color.Lerp(MenuUI.AccentDeep, MenuUI.Accent, j / (float)(StatSegments - 1));
                    if (j == litCount - 1) c = Color.Lerp(c, MenuUI.AccentGlow, 0.55f + 0.35f * breathe);
                }
                else c = MenuUI.WithAlpha(MenuUI.Locked, 0.3f);
                archiveBars[s][j].color = c;
            }
        }

        // Texto que se escribe solo
        archiveBody.maxVisibleCharacters = k >= 1f ? 99999 : Mathf.RoundToInt(archiveBodyChars * Mathf.Clamp01(k * 1.15f));

        // Avisos de NUEVO y fila elegida
        float pulse = Mathf.Sin(time * 4f) * 0.5f + 0.5f;
        foreach (ArchiveRow row in archiveRows)
            if (row.badge.gameObject.activeSelf) row.badgeGlow.color = MenuUI.WithAlpha(MenuUI.Yellow, 0.2f + 0.35f * pulse);
        if (archiveSelected >= 0 && archiveSelected < archiveRows.Count)
            archiveRows[archiveSelected].mark.color = MenuUI.WithAlpha(MenuUI.AccentGlow, 0.6f + 0.4f * pulse);

        if (archiveMessageTimer > 0f)
        {
            archiveMessageTimer -= dt;
            if (archiveMessageTimer <= 0f)
            {
                archiveMessage.text = ArchiveHint;
                archiveMessage.color = MenuUI.TextDim;
            }
        }

        if (busy) return;

        // Teclado: flechas para recorrer la lista
        if (archiveRows.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) SelectArchive((archiveSelected + 1) % archiveRows.Count);
            if (Input.GetKeyDown(KeyCode.UpArrow)) SelectArchive((archiveSelected - 1 + archiveRows.Count) % archiveRows.Count);
        }

        // Prueba: F9 registra todas las aves
        if (Input.GetKeyDown(KeyCode.F9))
        {
            foreach (BirdArchive.Entry e in BirdArchive.Entries) SaveSystem.MarkBirdSeen(e.Species);
            archiveMessage.text = "Todas las aves registradas (prueba)";
            archiveMessage.color = MenuUI.Yellow;
            archiveMessageTimer = 2.5f;
            RefreshArchive();
            RefreshArchiveBadge();
        }
    }
}
