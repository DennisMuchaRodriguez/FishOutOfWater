using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Menú principal. Se construye solo por código (basta con este componente en la escena MainMenu).
// Se ve como el visor del casco: paneles holográficos, marco curvo, líneas de escaneo y miras.
// Pantallas:
//  1. Título:   JUGAR / CONTROLES / SALIR, con el protagonista flotando a la derecha.
//  2. Modos:    HISTORIA (disponible), SUPERVIVENCIA y COOPERATIVO (próximamente).
//  3. Mapa:     los niveles del LevelCatalog, que se desbloquean de uno en uno.
//               Clic en un nivel = ficha con estrellas, detalles, JUGAR, VER CÓMIC e IR AL TALLER.
//               Calavera = nivel de jefe; llave inglesa = después se visita el Taller.
//  4. Controles.
// Atajos de prueba en el mapa: F9 desbloquea todos los niveles.
public class MainMenu : MonoBehaviour
{
    [Header("Protagonista flotando (opcional)")]
    [Tooltip("Modelo que flota junto al título (por ejemplo PESCAO.fbx)")]
    public GameObject heroModel;
    [Tooltip("Animación del modelo (por ejemplo Fish.controller)")]
    public RuntimeAnimatorController heroAnimator;
    [Tooltip("Alto del modelo en pantalla (metros, a 7 m de la cámara)")]
    public float heroHeight = 3.2f;

    [Header("Textos")]
    public string titleLine1 = "FISH OUT";
    public string titleLine2 = "OF WATER";
    public string subtitle = "UN PEZ. UN TRAJE. NADA DE AGUA.";

    // ---- Pantallas ----
    RectTransform root;
    CanvasGroup titleScreen, modesScreen, mapScreen, controlsScreen, current;
    Image fade;
    bool busy;

    // ---- Mapa ----
    LevelCatalog catalog;
    int selectedLevel = -1;
    RectTransform mapNodesHolder;
    RectTransform infoContent;
    TextMeshProUGUI totalStarsText;
    readonly List<LevelNode> nodes = new List<LevelNode>();
    float resetArmedUntil;
    TextMeshProUGUI resetLabel;
    TextMeshProUGUI mapMessage;
    float mapMessageTimer;

    class LevelNode
    {
        public int index;
        public RectTransform rt;
        public Image ring;          // anillo con marcas (seleccionado / nivel actual)
        public Image halo;          // aro naranja del nivel actual
        public Image workshopGlow;  // brillo de la llave si el Taller está pendiente
        public bool current;
    }

    // ---- Fondo ----
    RectTransform bgRoot;
    readonly List<Bubble> bubbles = new List<Bubble>();
    readonly List<RectTransform> weeds = new List<RectTransform>();
    readonly List<Image> rays = new List<Image>();
    RectTransform bubbleLayer;

    class Bubble
    {
        public RectTransform rt;
        public RawImage img;
        public float speed, wobble, phase, size;
    }

    // ---- Protagonista ----
    Transform hero;
    Vector3 heroBase;
    Quaternion heroBaseRot;
    float heroShown = 1f;
    float heroTarget = 1f;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        catalog = LevelCatalog.Load();

        BuildBackground();
        SetupHero();

        Canvas canvas = MenuUI.CreateCanvas("MainMenuCanvas", 0);
        root = (RectTransform)canvas.transform;

        // Marco del casco detrás de todas las pantallas (encima del lago y del protagonista)
        MenuUI.VisorOverlay(root);

        titleScreen = BuildTitleScreen();
        modesScreen = BuildModesScreen();
        mapScreen = BuildMapScreen();
        controlsScreen = BuildControlsScreen();
        foreach (CanvasGroup g in new[] { titleScreen, modesScreen, mapScreen, controlsScreen }) Hide(g);

        fade = MenuUI.Panel("Fundido", root, MenuUI.Ink);
        fade.raycastTarget = false;

        bool openMap = GameSession.OpenLevelMap && catalog != null;
        GameSession.OpenLevelMap = false;
        ShowInstant(openMap ? mapScreen : titleScreen);
        StartCoroutine(FadeTo(0f, 0.7f));
    }

    // ======================= FONDO =======================

    void BuildBackground()
    {
        Canvas bg = MenuUI.CreateCanvas("FondoCanvas", -100);
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Detrás del protagonista 3D
            bg.renderMode = RenderMode.ScreenSpaceCamera;
            bg.worldCamera = cam;
            bg.planeDistance = 30f;
        }
        bgRoot = (RectTransform)bg.transform;

        Image grad = MenuUI.Panel("Agua", bgRoot, Color.white);
        grad.sprite = MenuUI.WaterGradient;
        grad.raycastTarget = false;

        // Rayos de luz que bajan desde la superficie
        for (int i = 0; i < 5; i++)
        {
            Image ray = MenuUI.Img("Rayo" + i, bgRoot, MenuUI.RoundedSmall, new Color(0.75f, 0.95f, 1f, 0.06f + 0.02f * (i % 2)),
                                   new Vector2(0.5f, 1f), new Vector2(-520f + i * 260f, 40f), new Vector2(70f + 30f * (i % 3), 1100f));
            ray.rectTransform.pivot = new Vector2(0.5f, 1f);
            ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            rays.Add(ray);
        }

        RawImage glow = MenuUI.Stretch("Brillo", bgRoot).gameObject.AddComponent<RawImage>();
        glow.texture = FXFactory.MakeRadial(128, d => Mathf.Clamp01(1f - d) * 0.8f, "FX_MenuGlow");
        glow.color = new Color(0.3f, 0.75f, 0.9f, 0.25f);
        glow.raycastTarget = false;

        // Algas en el fondo (se mecen)
        Color[] weedColors = { new Color32(24, 92, 70, 255), new Color32(34, 120, 82, 255), new Color32(18, 70, 60, 255) };
        for (int i = 0; i < 16; i++)
        {
            float x = -680f + i * 90f + Random.Range(-25f, 25f);
            float h = Random.Range(120f, 260f);
            Image w = MenuUI.Img("Alga" + i, bgRoot, MenuUI.RoundedSmall, weedColors[i % 3], new Vector2(0.5f, 0f),
                                 new Vector2(x, -10f), new Vector2(Random.Range(18f, 30f), h));
            w.rectTransform.pivot = new Vector2(0.5f, 0f);
            weeds.Add(w.rectTransform);
        }
        // Rocas
        Color rock = new Color32(10, 30, 48, 255);
        for (int i = 0; i < 6; i++)
            MenuUI.Img("Roca" + i, bgRoot, MenuUI.Circle, rock, new Vector2(0.5f, 0f),
                       new Vector2(-640f + i * 260f + Random.Range(-40f, 40f), -60f), new Vector2(Random.Range(220f, 360f), Random.Range(140f, 200f)));

        bubbleLayer = MenuUI.Stretch("Burbujas", bgRoot);
        for (int i = 0; i < 40; i++) bubbles.Add(CreateBubble(true));
    }

    Bubble CreateBubble(bool randomY)
    {
        Bubble b = new Bubble();
        b.img = MenuUI.Rect("Burbuja", bubbleLayer, new Vector2(0.5f, 0f), Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
        b.img.texture = FXFactory.Bubble;
        b.img.raycastTarget = false;
        b.rt = b.img.rectTransform;
        ResetBubble(b, randomY);
        return b;
    }

    void ResetBubble(Bubble b, bool randomY)
    {
        Rect r = bubbleLayer.rect;
        float w = r.width > 0f ? r.width : 1280f;
        float h = r.height > 0f ? r.height : 720f;
        b.size = Random.Range(6f, 30f);
        b.speed = Random.Range(25f, 80f) * (0.6f + b.size / 40f);
        b.wobble = Random.Range(5f, 25f);
        b.phase = Random.Range(0f, 10f);
        b.rt.sizeDelta = Vector2.one * b.size;
        b.rt.anchoredPosition = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), randomY ? Random.Range(0f, h) : -40f);
        b.img.color = new Color(0.8f, 0.97f, 1f, Random.Range(0.15f, 0.45f));
    }

    // ======================= PROTAGONISTA 3D =======================

    void SetupHero()
    {
        Camera cam = Camera.main;
        if (cam == null || heroModel == null) return;

        // Luz cálida de frente y un borde cian por detrás (como el brillo del traje)
        Light key = new GameObject("LuzMenu").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.93f, 0.82f);
        key.intensity = 1.4f;
        key.transform.rotation = Quaternion.LookRotation(cam.transform.forward + Vector3.down * 0.6f + cam.transform.right * 0.4f);
        Light rim = new GameObject("LuzBorde").AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(0.4f, 0.85f, 1f);
        rim.intensity = 0.9f;
        rim.transform.rotation = Quaternion.LookRotation(-cam.transform.forward + Vector3.down * 0.3f - cam.transform.right * 0.6f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.42f, 0.55f);

        hero = new GameObject("Protagonista").transform;
        heroBase = cam.transform.position + cam.transform.forward * 7f + cam.transform.right * 2.9f - cam.transform.up * 0.4f;
        // El modelo mira hacia +Z: se gira para que mire a la cámara (un poco hacia el menú)
        heroBaseRot = Quaternion.LookRotation(-cam.transform.forward, Vector3.up) * Quaternion.Euler(0f, -22f, 0f);
        hero.SetPositionAndRotation(heroBase, heroBaseRot);

        GameObject inst = Instantiate(heroModel, hero, false);
        foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Destroy(c);

        // Escala y centrado según el tamaño real del modelo
        Bounds b;
        if (GetBounds(inst, out b) && b.size.y > 0.001f)
        {
            float s = heroHeight / b.size.y;
            inst.transform.localScale = Vector3.one * s;
            if (GetBounds(inst, out b)) inst.transform.position += hero.position - b.center;
        }

        Animator anim = inst.GetComponentInChildren<Animator>();
        if (anim == null && heroAnimator != null) anim = inst.AddComponent<Animator>();
        if (anim != null && heroAnimator != null)
        {
            anim.runtimeAnimatorController = heroAnimator;
            anim.applyRootMotion = false;
        }
    }

    static bool GetBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // ======================= PANTALLA: TÍTULO =======================

    CanvasGroup BuildTitleScreen()
    {
        RectTransform s = MenuUI.Stretch("Titulo", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Linea1", s, titleLine1, 118f, new Vector2(0.5f, 0.5f), new Vector2(-300f, 222f), new Vector2(700f, 130f), MenuUI.Orange);
        MenuUI.Title("Linea2", s, titleLine2, 118f, new Vector2(0.5f, 0.5f), new Vector2(-270f, 116f), new Vector2(700f, 130f), MenuUI.Blue);

        RectTransform pill = MenuUI.ComicPanel("Subtitulo", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, 32f), new Vector2(520f, 46f), MenuUI.Glass);
        MenuUI.Text("Texto", pill, subtitle, 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(500f, 40f), MenuUI.Accent)
            .characterSpacing = 6f;

        Button play = MenuUI.ComicButton("JUGAR", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, -70f), new Vector2(380f, 74f), MenuUI.Orange, () => Go(modesScreen), 36f);
        MenuUI.ComicButton("CONTROLES", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, -164f), new Vector2(340f, 60f), MenuUI.Blue, () => Go(controlsScreen), 28f);
        MenuUI.ComicButton("SALIR", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, -244f), new Vector2(300f, 54f), MenuUI.BlueDark, Quit, 26f);
        play.Select();

        TextMeshProUGUI ver = MenuUI.Text("Version", s, "PROTOTIPO  ·  MODO HISTORIA", 15f, new Vector2(1f, 0f), new Vector2(-170f, 24f),
                                          new Vector2(320f, 30f), MenuUI.WithAlpha(MenuUI.Accent, 0.6f));
        ver.characterSpacing = 4f;
        return g;
    }

    // ======================= PANTALLA: MODOS =======================

    CanvasGroup BuildModesScreen()
    {
        RectTransform s = MenuUI.Stretch("Modos", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Encabezado", s, "ELIGE TU MODO", 76f, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(900f, 100f), MenuUI.Cream);
        MenuUI.Rule(s, new Vector2(0.5f, 1f), new Vector2(0f, -138f), 520f, MenuUI.Accent);

        int unlocked = catalog != null ? Mathf.Min(SaveSystem.Data.unlockedLevels, catalog.Count) : 0;
        int levels = catalog != null ? catalog.Count : 0;
        ModeCard(s, new Vector2(-380f, -30f), "HISTORIA", MenuUI.Orange,
                 levels + " niveles  ·  3 oleadas cada uno\nDefiende el lago y mejora tu traje",
                 "NIVEL " + Mathf.Max(1, unlocked) + " DE " + levels, true, () => Go(mapScreen), true);
        ModeCard(s, new Vector2(0f, -30f), "SUPERVIVENCIA", MenuUI.Blue,
                 "Oleadas sin fin\nLos peces no vuelven", "", false, null, false);
        ModeCard(s, new Vector2(380f, -30f), "COOPERATIVO", MenuUI.Purple,
                 "Hasta 4 jugadores online\nChat de voz tipo radio", "", false, null, false);

        MenuUI.ComicButton("VOLVER", s, new Vector2(0f, 0f), new Vector2(130f, 52f), new Vector2(200f, 52f), MenuUI.BlueDark, () => Go(titleScreen), 24f);
        return g;
    }

    void ModeCard(Transform parent, Vector2 pos, string title, Color color, string desc, string progress, bool available,
                  UnityEngine.Events.UnityAction onClick, bool showStars)
    {
        Vector2 size = new Vector2(340f, 400f);
        Vector2 top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);
        RectTransform card;
        MenuUI.HoloStyle st;
        if (available)
        {
            st = MenuUI.Derive(color);
            card = MenuUI.HoloPanel(title, parent, new Vector2(0.5f, 0.5f), pos, size,
                                    MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, color, 0.3f), 0.9f), st.edge,
                                    MenuUI.WithAlpha(color, 0.35f), MenuUI.WithAlpha(color, 0.5f), 0.08f);
            MakeClickable(card, onClick);
        }
        else
        {
            Color c = Color.Lerp(color, MenuUI.Locked, 0.55f);
            st = MenuUI.Derive(c);
            card = MenuUI.HoloPanel(title, parent, new Vector2(0.5f, 0.5f), pos, size,
                                    MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, c, 0.12f), 0.82f), MenuUI.WithAlpha(st.edge, 0.55f),
                                    MenuUI.WithAlpha(c, 0.1f), MenuUI.WithAlpha(c, 0.22f), 0.04f);
        }

        Color titleColor = available ? MenuUI.Cream : Color.Lerp(MenuUI.Cream, MenuUI.Locked, 0.4f);
        TextMeshProUGUI t = MenuUI.Text("Titulo", card, title, 50f, top, new Vector2(0f, -54f), new Vector2(320f, 70f), titleColor, true);
        t.enableAutoSizing = true;
        t.fontSizeMax = 50f;
        t.fontSizeMin = 30f;
        t.characterSpacing = 2f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        MenuUI.Img("Linea", card, MenuUI.Pixel, MenuUI.WithAlpha(st.edge, 0.5f), top, new Vector2(0f, -92f), new Vector2(220f, 1.5f));
        MenuUI.Text("Desc", card, desc, 21f, top, new Vector2(0f, -136f), new Vector2(300f, 80f),
                    available ? MenuUI.TextDim : MenuUI.WithAlpha(MenuUI.TextDim, 0.6f));

        if (available)
        {
            if (showStars)
            {
                RectTransform starHolder = MenuUI.Rect("Estrella", card, bottom, new Vector2(-46f, 150f), new Vector2(46f, 46f));
                MenuUI.StarIcon(starHolder, Vector2.zero, 46f, true);
                int max = catalog != null ? catalog.Count * 3 : 0;
                MenuUI.Text("Estrellas", card, SaveSystem.TotalStars + " / " + max, 30f, bottom, new Vector2(30f, 150f),
                            new Vector2(160f, 46f), MenuUI.Cream, true).alignment = TextAlignmentOptions.MidlineLeft;
            }
            MenuUI.Text("Progreso", card, progress, 24f, bottom, new Vector2(0f, 96f), new Vector2(300f, 40f), MenuUI.Accent)
                .characterSpacing = 3f;
            MenuUI.Tag("JUGAR", card, bottom, new Vector2(0f, 44f), MenuUI.Orange, 22f);
        }
        else
        {
            MenuUI.Img("HaloCandado", card, MenuUI.HexGlow, MenuUI.WithAlpha(MenuUI.Locked, 0.25f), bottom, new Vector2(0f, 130f), new Vector2(120f, 120f));
            MenuUI.Img("Hexagono", card, MenuUI.HexLine, MenuUI.WithAlpha(MenuUI.Locked, 0.9f), bottom, new Vector2(0f, 130f), new Vector2(96f, 96f));
            MenuUI.Img("Candado", card, MenuUI.Lock, Color.Lerp(MenuUI.Locked, MenuUI.Cream, 0.3f), bottom, new Vector2(0f, 130f), new Vector2(52f, 52f));
            MenuUI.Tag("PRÓXIMAMENTE", card, bottom, new Vector2(0f, 50f), MenuUI.Locked, 20f);
        }
    }

    // Convierte un panel holográfico en botón (el cristal recibe el clic)
    static Button MakeClickable(RectTransform panel, UnityEngine.Events.UnityAction onClick)
    {
        Image fill = panel.Find("Relleno").GetComponent<Image>();
        fill.raycastTarget = true;
        Button b = panel.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.92f, 1f, 1f, 1f);
        cb.selectedColor = new Color(0.92f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.75f, 0.85f, 0.9f, 1f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.targetGraphic = fill;
        if (onClick != null) b.onClick.AddListener(onClick);
        panel.gameObject.AddComponent<MenuButtonFX>();
        return b;
    }

    // ======================= PANTALLA: MAPA DE NIVELES =======================

    static readonly string[] BlockNames = { "LA LLEGADA", "LA ORILLA", "BAJO EL AGUA", "LOS CIELOS" };

    CanvasGroup BuildMapScreen()
    {
        RectTransform s = MenuUI.Stretch("Mapa", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Encabezado", s, "MODO HISTORIA", 62f, new Vector2(0f, 1f), new Vector2(230f, -50f), new Vector2(460f, 80f), MenuUI.Orange);

        // Total de estrellas (arriba a la derecha)
        MenuUI.Img("BrilloEstrella", s, MenuUI.StarGlow, MenuUI.WithAlpha(MenuUI.Yellow, 0.45f), new Vector2(1f, 1f), new Vector2(-190f, -50f), new Vector2(72f, 72f));
        MenuUI.Img("EstrellaTotal", s, MenuUI.Star, MenuUI.Yellow, new Vector2(1f, 1f), new Vector2(-190f, -50f), new Vector2(46f, 46f));
        totalStarsText = MenuUI.Text("TotalEstrellas", s, "", 32f, new Vector2(1f, 1f), new Vector2(-90f, -50f), new Vector2(150f, 50f), MenuUI.Cream, true);
        totalStarsText.alignment = TextAlignmentOptions.MidlineLeft;

        // El lago visto como en el sonar del traje: cristal, rejilla hexagonal y curvas de nivel
        RectTransform map = MenuUI.HoloPanel("Lago", s, new Vector2(0.5f, 0.5f), new Vector2(-210f, -24f), new Vector2(820f, 548f),
                                             MenuUI.Glass, MenuUI.WithAlpha(MenuUI.Accent, 0.9f), MenuUI.WithAlpha(MenuUI.Accent, 0.16f),
                                             MenuUI.WithAlpha(MenuUI.Accent, 0.3f), 0.05f);
        Vector2 waterSize = new Vector2(796f, 524f);
        Image water = MenuUI.Img("Agua", map, MenuUI.WaterGradient, new Color(1f, 1f, 1f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, waterSize);
        water.type = Image.Type.Simple;
        RawImage grid = MenuUI.Rect("Rejilla", map, new Vector2(0.5f, 0.5f), Vector2.zero, waterSize).gameObject.AddComponent<RawImage>();
        Texture2D hexTex = MenuUI.HexGridTexture;
        grid.texture = hexTex;
        grid.uvRect = new Rect(0f, 0f, waterSize.x / hexTex.width, waterSize.y / hexTex.height);
        grid.color = MenuUI.WithAlpha(MenuUI.Accent, 0.07f);
        grid.raycastTarget = false;
        RawImage contour = MenuUI.Rect("Curvas", map, new Vector2(0.5f, 0.5f), Vector2.zero, waterSize).gameObject.AddComponent<RawImage>();
        contour.texture = MenuUI.ContourTexture;
        contour.color = MenuUI.WithAlpha(MenuUI.Accent, 0.13f);
        contour.raycastTarget = false;
        mapNodesHolder = MenuUI.Rect("Niveles", map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 548f));

        // Ficha del nivel (derecha)
        RectTransform info = MenuUI.ComicPanel("Ficha", s, new Vector2(0.5f, 0.5f), new Vector2(432f, -24f), new Vector2(360f, 548f), MenuUI.Glass);
        infoContent = MenuUI.Rect("Contenido", info, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 548f));

        MenuUI.ComicButton("VOLVER", s, new Vector2(0f, 0f), new Vector2(120f, 40f), new Vector2(180f, 48f), MenuUI.BlueDark, () => Go(modesScreen), 22f);
        Button reset = MenuUI.ComicButton("BORRAR PROGRESO", s, new Vector2(0f, 0f), new Vector2(360f, 40f), new Vector2(250f, 40f),
                                          Color.Lerp(MenuUI.Locked, MenuUI.Red, 0.35f), ResetProgress, 17f);
        resetLabel = reset.GetComponentInChildren<TextMeshProUGUI>();

        mapMessage = MenuUI.Text("Mensaje", s, "", 22f, new Vector2(0.5f, 0f), new Vector2(-210f, 40f), new Vector2(640f, 40f), MenuUI.Yellow);

        RebuildMap();
        return g;
    }

    Vector2 NodePosition(int index)
    {
        int row = index / 5, col = index % 5;
        if (row % 2 == 1) col = 4 - col;                   // camino en zigzag (como un sendero)
        return new Vector2(-150f + col * 128f, 196f - row * 124f);
    }

    void RebuildMap()
    {
        if (mapNodesHolder == null) return;
        foreach (Transform c in mapNodesHolder) Destroy(c.gameObject);
        nodes.Clear();

        if (catalog == null || catalog.Count == 0)
        {
            MenuUI.Text("SinNiveles", mapNodesHolder, "No se encontró Assets/Resources/LevelCatalog.\nCrea el catálogo y agrega los niveles.",
                        24f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 120f), MenuUI.Cream);
            return;
        }

        int count = catalog.Count;
        int unlocked = Mathf.Clamp(SaveSystem.Data.unlockedLevels, 1, count);
        int currentLevel = Mathf.Min(unlocked - 1, count - 1);
        totalStarsText.text = SaveSystem.TotalStars + " / " + (count * 3);

        // Nombres de los bloques (cada 5 niveles)
        for (int row = 0; row * 5 < count; row++)
        {
            RectTransform lbl = MenuUI.ComicPanel("Bloque" + row, mapNodesHolder, new Vector2(0.5f, 0.5f), new Vector2(-322f, 196f - row * 124f),
                                                  new Vector2(140f, 64f), new Color32(13, 52, 84, 255));
            MenuUI.Text("Num", lbl, "BLOQUE " + (row + 1), 15f, new Vector2(0.5f, 0.5f), new Vector2(0f, 13f), new Vector2(130f, 22f), MenuUI.Accent)
                .characterSpacing = 3f;
            MenuUI.Text("Nombre", lbl, row < BlockNames.Length ? BlockNames[row] : "", 18f, new Vector2(0.5f, 0.5f), new Vector2(0f, -9f),
                        new Vector2(132f, 26f), MenuUI.Cream);
        }

        // Sendero de rayitas entre niveles (cian si ya está abierto)
        for (int i = 0; i + 1 < count; i++)
        {
            Vector2 a = NodePosition(i), b = NodePosition(i + 1);
            int steps = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 16f));
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            bool open = i + 1 < unlocked;
            for (int k = 1; k < steps; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, k / (float)steps);
                Image dash = MenuUI.Img("Raya", mapNodesHolder, MenuUI.Pixel,
                                        open ? MenuUI.WithAlpha(MenuUI.Accent, 0.75f) : MenuUI.WithAlpha(MenuUI.Locked, 0.5f),
                                        new Vector2(0.5f, 0.5f), p, open ? new Vector2(7f, 2.5f) : new Vector2(5f, 2f));
                dash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        for (int i = 0; i < count; i++) BuildNode(i, i < unlocked, i == currentLevel);

        if (selectedLevel < 0 || selectedLevel >= count) selectedLevel = currentLevel;
        SelectLevel(selectedLevel);
    }

    void BuildNode(int index, bool unlocked, bool isCurrent)
    {
        LevelDefinition level = catalog.Get(index);
        bool boss = level != null && level.isBossLevel;
        bool shop = level != null && level.workshopAfter;
        bool pending = unlocked && WorkshopFlow.IsPending(index);
        int stars = SaveSystem.GetStars(index);
        float size = boss ? 80f : 66f;
        Vector2 pos = NodePosition(index);
        Vector2 c = new Vector2(0.5f, 0.5f);
        Color edge = !unlocked ? MenuUI.Locked : boss ? MenuUI.Red : MenuUI.Accent;

        RectTransform holder = MenuUI.Rect("Nivel" + (index + 1), mapNodesHolder, c, pos, new Vector2(size + 16f, size + 16f));

        // Aro del nivel actual y anillo con marcas (seleccionado / actual)
        Image halo = MenuUI.Img("Aro", holder, MenuUI.Ring, new Color(1f, 1f, 1f, 0f), c, Vector2.zero, Vector2.one * size * 1.45f);
        Image ring = MenuUI.Img("Anillo", holder, MenuUI.RingTicks, new Color(1f, 1f, 1f, 0f), c, Vector2.zero, Vector2.one * (size + 26f));

        // Hexágono: halo ("Brillo", lo aviva MenuButtonFX al pasar el mouse), relleno y dos bordes
        if (unlocked) MenuUI.Img("Brillo", holder, MenuUI.HexGlow, MenuUI.WithAlpha(edge, 0.28f), c, Vector2.zero, Vector2.one * size * 1.5f);
        Color fill = unlocked ? MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, edge, stars > 0 ? 0.32f : 0.18f), 0.94f) : MenuUI.WithAlpha(MenuUI.Ink, 0.85f);
        Image hex = MenuUI.Img("Hexagono", holder, MenuUI.Hex, fill, c, Vector2.zero, Vector2.one * size);
        hex.raycastTarget = true;
        MenuUI.Img("LineaInterior", holder, MenuUI.HexLine, MenuUI.WithAlpha(edge, 0.35f), c, Vector2.zero, Vector2.one * size * 0.78f);
        MenuUI.Img("Borde", holder, MenuUI.HexLine, MenuUI.WithAlpha(edge, unlocked ? 1f : 0.6f), c, Vector2.zero, Vector2.one * size);

        if (unlocked)
            MenuUI.Text("Numero", holder, (index + 1).ToString(), boss ? 34f : 28f, c, new Vector2(0f, 1f), Vector2.one * size, MenuUI.Cream, true);
        else
            MenuUI.Img("Candado", holder, MenuUI.Lock, MenuUI.WithAlpha(MenuUI.Locked, 0.9f), c, Vector2.zero, Vector2.one * size * 0.42f);

        if (stars > 0)
        {
            for (int k = 0; k < 3; k++)
                MenuUI.StarIcon(holder, new Vector2((k - 1) * 17f, -size * 0.5f - 6f + (k == 1 ? -3f : 0f)), 17f, k < stars);
        }

        // Etiquetas: JEFE arriba; TALLER arriba (o abajo si el nivel también es de jefe)
        if (boss) MenuUI.Tag("JEFE", holder, c, new Vector2(0f, size * 0.5f + 14f), unlocked ? MenuUI.Red : MenuUI.Locked, 14f, MenuUI.Skull);
        Image workshopGlow = null;
        if (shop)
        {
            Color col = pending ? MenuUI.Orange : (unlocked ? MenuUI.Blue : MenuUI.Locked);
            RectTransform tag = MenuUI.Tag("TALLER", holder, c, new Vector2(0f, boss ? -size * 0.5f - 34f : size * 0.5f + 13f), col, 12f, MenuUI.Wrench);
            if (pending)
                workshopGlow = MenuUI.Img("BrilloTaller", tag, MenuUI.Glow, MenuUI.WithAlpha(MenuUI.Orange, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), Vector2.one * 36f);
        }

        Button b = holder.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = hex;
        int captured = index;
        b.onClick.AddListener(() => SelectLevel(captured));
        holder.gameObject.AddComponent<MenuButtonFX>();

        nodes.Add(new LevelNode { index = index, rt = holder, ring = ring, halo = halo, workshopGlow = workshopGlow, current = isCurrent && unlocked });
    }

    void SelectLevel(int index)
    {
        selectedLevel = index;
        BuildInfo(index);
    }

    void BuildInfo(int index)
    {
        foreach (Transform c in infoContent) Destroy(c.gameObject);
        LevelDefinition level = catalog != null ? catalog.Get(index) : null;
        if (level == null) return;

        bool unlocked = SaveSystem.IsUnlocked(index);
        int stars = SaveSystem.GetStars(index);
        Vector2 top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);

        MenuUI.Title("Numero", infoContent, "NIVEL " + (index + 1), 56f, top, new Vector2(0f, -50f), new Vector2(340f, 70f),
                     level.isBossLevel ? MenuUI.Red : MenuUI.Orange);
        MenuUI.Text("Nombre", infoContent, level.displayName, 26f, top, new Vector2(0f, -106f), new Vector2(320f, 60f), MenuUI.Cream);
        TextMeshProUGUI desc = MenuUI.Text("Descripcion", infoContent, level.description, 18f, top, new Vector2(0f, -172f),
                                           new Vector2(310f, 80f), MenuUI.TextDim);
        desc.alignment = TextAlignmentOptions.Top;

        MenuUI.Stars(infoContent, top, new Vector2(0f, -248f), 44f, stars);

        int fish = 0;
        foreach (FishSpawn f in level.fish) if (f != null && f.type != null) fish += f.count;
        string details = level.waves.Count + " OLEADAS  ·  " + fish + " PECES\nPierdes si cazan el " + Mathf.RoundToInt(level.maxFishLossFraction * 100f) + " %";
        MenuUI.Text("Detalles", infoContent, details, 18f, top, new Vector2(0f, -314f), new Vector2(320f, 56f), MenuUI.Accent);

        float tagY = -360f;
        if (level.isBossLevel && level.workshopAfter)
        {
            MenuUI.Tag("JEFE", infoContent, top, new Vector2(-82f, tagY), MenuUI.Red, 15f, MenuUI.Skull);
            MenuUI.Tag("TALLER DESPUÉS", infoContent, top, new Vector2(62f, tagY), MenuUI.Blue, 15f, MenuUI.Wrench);
        }
        else if (level.isBossLevel) MenuUI.Tag("NIVEL DE JEFE", infoContent, top, new Vector2(0f, tagY), MenuUI.Red, 15f, MenuUI.Skull);
        else if (level.workshopAfter) MenuUI.Tag("TALLER DESPUÉS", infoContent, top, new Vector2(0f, tagY), MenuUI.Blue, 15f, MenuUI.Wrench);

        if (unlocked)
        {
            bool hasComic = level.comicBefore != null;
            if (WorkshopFlow.IsPending(index))
            {
                // Ganó el nivel pero todavía no instaló la mejora: el Taller lo espera
                Button shop = MenuUI.ComicButton("IR AL TALLER", infoContent, bottom, new Vector2(0f, 142f), new Vector2(290f, 52f),
                                                 MenuUI.Orange, () => OpenWorkshop(index), 28f);
                MenuUI.ComicButton("JUGAR", infoContent, bottom, new Vector2(0f, hasComic ? 86f : 80f), new Vector2(290f, 46f),
                                   MenuUI.Blue, () => PlayLevel(index), 26f);
                shop.Select();
            }
            else
            {
                Button play = MenuUI.ComicButton("JUGAR", infoContent, bottom, new Vector2(0f, hasComic ? 108f : 64f),
                                                 new Vector2(290f, 66f), MenuUI.Orange, () => PlayLevel(index), 32f);
                play.Select();
            }
            if (hasComic)
            {
                ComicDefinition comic = level.comicBefore;
                Button comicButton = null;
                comicButton = MenuUI.ComicButton("VER CÓMIC", infoContent, bottom, new Vector2(0f, 38f), new Vector2(230f, 40f), MenuUI.Blue,
                                   () => ComicViewer.Show(comic, () =>
                                   {
                                       SaveSystem.MarkSeen(comic);
                                       if (comicButton != null) comicButton.Select();   // recupera la navegación con teclado
                                   }), 20f);
            }
        }
        else
        {
            MenuUI.Img("HaloCandado", infoContent, MenuUI.HexGlow, MenuUI.WithAlpha(MenuUI.Locked, 0.25f), bottom, new Vector2(0f, 120f), new Vector2(96f, 96f));
            MenuUI.Img("Candado", infoContent, MenuUI.Lock, MenuUI.Locked, bottom, new Vector2(0f, 120f), new Vector2(64f, 64f));
            MenuUI.Text("Bloqueado", infoContent, "Supera el nivel " + index + " para desbloquearlo", 19f, bottom,
                        new Vector2(0f, 56f), new Vector2(310f, 50f), MenuUI.TextDim);
        }

        foreach (LevelNode n in nodes) n.ring.color = n.index == index ? MenuUI.Cream : new Color(1f, 1f, 1f, 0f);
    }

    void OpenWorkshop(int index)
    {
        if (busy) return;
        StartCoroutine(OpenWorkshopRoutine(index));
    }

    IEnumerator OpenWorkshopRoutine(int index)
    {
        busy = true;
        yield return FadeTo(1f, 0.4f);
        WorkshopFlow.Open(index, false);
    }

    void PlayLevel(int index)
    {
        if (busy || catalog == null) return;
        LevelDefinition level = catalog.Get(index);
        if (level == null || !SaveSystem.IsUnlocked(index)) return;

        // La primera vez se ve el cómic de antes del nivel
        ComicDefinition comic = level.comicBefore;
        if (comic != null && !SaveSystem.HasSeen(comic))
        {
            ComicViewer.Show(comic, () =>
            {
                SaveSystem.MarkSeen(comic);
                StartCoroutine(LoadLevel(index));
            });
            return;
        }
        StartCoroutine(LoadLevel(index));
    }

    IEnumerator LoadLevel(int index)
    {
        busy = true;
        yield return FadeTo(1f, 0.5f);
        GameSession.PlayLevel(index);
    }

    void ResetProgress()
    {
        if (Time.unscaledTime > resetArmedUntil)
        {
            resetArmedUntil = Time.unscaledTime + 3f;
            resetLabel.text = "¿SEGURO? CLIC OTRA VEZ";
            return;
        }
        resetArmedUntil = 0f;
        resetLabel.text = "BORRAR PROGRESO";
        SaveSystem.ResetProgress();
        selectedLevel = -1;
        RebuildMap();
        ShowMapMessage("Progreso borrado");
    }

    void ShowMapMessage(string text)
    {
        mapMessage.text = text;
        mapMessageTimer = 2.5f;
    }

    // ======================= PANTALLA: CONTROLES =======================

    CanvasGroup BuildControlsScreen()
    {
        RectTransform s = MenuUI.Stretch("Controles", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Encabezado", s, "CONTROLES", 72f, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(800f, 90f), MenuUI.Cream);
        MenuUI.Rule(s, new Vector2(0.5f, 1f), new Vector2(0f, -128f), 460f, MenuUI.Accent);
        RectTransform panel = MenuUI.ComicPanel("Lista", s, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1000f, 470f), MenuUI.Glass);

        string k = "<color=" + MenuUI.HexOrange + ">", e = "</color>";
        string controls =
            k + "W / S" + e + "  Avanzar / retroceder        " + k + "A / D" + e + "  Girar\n" +
            k + "ESPACIO" + e + "  Propulsor (en el agua: subir y salir disparado)\n" +
            k + "SHIFT" + e + "  Turbo (mantener) · impulso de nado (en el agua)\n" +
            k + "CTRL" + e + "  Bucear más hondo\n" +
            k + "CLIC IZQUIERDO" + e + "  Disparar (mantener = ráfaga)\n" +
            k + "CLIC DERECHO" + e + "  Disparo cargado (soltar al llenarse)\n" +
            k + "Q" + e + "  Cambiar cámara        " + k + "ESC" + e + "  Pausa\n\n" +
            "<color=" + MenuUI.HexAccent + ">OBJETIVO:</color> derriba a todas las aves antes de que cacen a demasiados peces.\n" +
            "El agua recarga el propulsor. La munición está en cápsulas dentro del lago.\n" +
            "Si un ave atrapa un pez, dispárale: lo soltará.";
        TextMeshProUGUI t = MenuUI.Text("Texto", panel, controls, 23f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 420f), MenuUI.Cream);
        t.lineSpacing = 12f;

        MenuUI.ComicButton("VOLVER", s, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(220f, 54f), MenuUI.BlueDark, () => Go(titleScreen), 24f);
        return g;
    }

    // ======================= NAVEGACIÓN =======================

    void Go(CanvasGroup target)
    {
        if (busy || target == current) return;
        if (target == mapScreen) { selectedLevel = -1; mapScreen.gameObject.SetActive(true); RebuildMap(); }
        if (target == modesScreen) target = RebuildModes();
        StartCoroutine(SwitchTo(target));
    }

    // La tarjeta de Historia muestra el progreso: se rehace al volver (por si se borró o desbloqueó con F9)
    CanvasGroup RebuildModes()
    {
        int sibling = modesScreen.transform.GetSiblingIndex();
        Destroy(modesScreen.gameObject);
        modesScreen = BuildModesScreen();
        modesScreen.transform.SetSiblingIndex(sibling);
        Hide(modesScreen);
        return modesScreen;
    }

    IEnumerator SwitchTo(CanvasGroup target)
    {
        busy = true;
        CanvasGroup from = current;
        float t = 0f;
        while (from != null && t < 0.16f)
        {
            t += Time.unscaledDeltaTime;
            from.alpha = 1f - t / 0.16f;
            yield return null;
        }
        if (from != null) Hide(from);

        ShowInstant(target);
        target.alpha = 0f;
        RectTransform rt = (RectTransform)target.transform;
        t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.22f);
            target.alpha = k;
            rt.anchoredPosition = new Vector2(0f, (1f - k) * -24f);
            yield return null;
        }
        target.alpha = 1f;
        rt.anchoredPosition = Vector2.zero;
        busy = false;
    }

    void ShowInstant(CanvasGroup g)
    {
        current = g;
        g.gameObject.SetActive(true);
        g.alpha = 1f;
        g.interactable = true;
        g.blocksRaycasts = true;
        heroTarget = g == titleScreen ? 1f : 0f;
        Button first = g.GetComponentInChildren<Button>();
        if (first != null && g != mapScreen) first.Select();
    }

    static void Hide(CanvasGroup g)
    {
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
        g.gameObject.SetActive(false);
    }

    // ======================= UPDATE =======================

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        // Burbujas, algas y rayos de luz
        float top = bubbleLayer.rect.height + 40f;
        foreach (Bubble b in bubbles)
        {
            Vector2 p = b.rt.anchoredPosition;
            p.y += b.speed * dt;
            p.x += Mathf.Sin(time * 1.5f + b.phase) * b.wobble * dt;
            b.rt.anchoredPosition = p;
            if (p.y > top) ResetBubble(b, false);
        }
        for (int i = 0; i < weeds.Count; i++)
            weeds[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.9f + i * 0.7f) * 7f);
        for (int i = 0; i < rays.Count; i++)
            rays[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f + Mathf.Sin(time * 0.25f + i) * 4f);

        // Protagonista: flota, se mece y sale de escena fuera de la pantalla de título
        if (hero != null)
        {
            heroShown = Mathf.MoveTowards(heroShown, heroTarget, dt * 2.5f);
            float k = Mathf.SmoothStep(0f, 1f, heroShown);
            Camera cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            hero.position = heroBase + Vector3.up * Mathf.Sin(time * 1.3f) * 0.15f + right * (1f - k) * 7f;
            hero.rotation = heroBaseRot * Quaternion.Euler(Mathf.Sin(time * 0.8f) * 3f, Mathf.Sin(time * 0.5f) * 14f, Mathf.Sin(time * 0.9f) * 3f);
        }

        if (current == mapScreen)
        {
            float pulse = Mathf.Sin(time * 4f) * 0.5f + 0.5f;
            foreach (LevelNode n in nodes)
            {
                // Nivel actual: aro naranja que late. El anillo con marcas gira despacio
                bool selected = n.index == selectedLevel;
                if (n.current)
                {
                    n.halo.color = MenuUI.WithAlpha(MenuUI.Orange, 0.25f + 0.3f * pulse);
                    if (!selected) n.ring.color = MenuUI.WithAlpha(MenuUI.Orange, 0.5f + 0.4f * pulse);
                }
                if (selected || n.current) n.ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 25f);
                if (n.workshopGlow != null) n.workshopGlow.color = MenuUI.WithAlpha(MenuUI.Orange, 0.25f + 0.45f * pulse);
            }
            if (mapMessageTimer > 0f)
            {
                mapMessageTimer -= dt;
                if (mapMessageTimer <= 0f) mapMessage.text = "";
            }
            if (resetArmedUntil > 0f && time > resetArmedUntil)
            {
                resetArmedUntil = 0f;
                resetLabel.text = "BORRAR PROGRESO";
            }
            // Prueba: F9 desbloquea todos los niveles
            if (Input.GetKeyDown(KeyCode.F9) && catalog != null)
            {
                SaveSystem.UnlockAll(catalog.Count);
                RebuildMap();
                ShowMapMessage("Todos los niveles desbloqueados (prueba)");
            }
        }

        if (busy || ComicViewer.IsShowing || Time.frameCount == ComicViewer.ClosedFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (current == mapScreen) Go(modesScreen);
            else if (current == modesScreen || current == controlsScreen) Go(titleScreen);
        }
    }

    IEnumerator FadeTo(float target, float duration)
    {
        float start = fade.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fade.color = new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        fade.color = new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, target);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
