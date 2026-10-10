using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Menú principal. Se construye solo por código (basta con este componente en la escena MainMenu).
// Pantallas:
//  1. Título:   JUGAR / CONTROLES / SALIR, con el protagonista flotando a la derecha.
//  2. Modos:    HISTORIA (disponible), SUPERVIVENCIA y COOPERATIVO (próximamente).
//  3. Mapa:     los niveles del LevelCatalog, que se desbloquean de uno en uno.
//               Clic en un nivel = ficha con estrellas, detalles, JUGAR y VER CÓMIC.
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
        public Image ring;
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

        RectTransform pill = MenuUI.ComicPanel("Subtitulo", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, 32f), new Vector2(520f, 46f), MenuUI.Cream, 4f, 5f);
        pill.localRotation = Quaternion.Euler(0f, 0f, -2f);
        MenuUI.Text("Texto", pill, subtitle, 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(500f, 40f), MenuUI.Ink);

        Button play = MenuUI.ComicButton("JUGAR", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, -70f), new Vector2(380f, 74f), MenuUI.Orange, () => Go(modesScreen), 36f);
        MenuUI.ComicButton("CONTROLES", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, -164f), new Vector2(340f, 60f), MenuUI.Blue, () => Go(controlsScreen), 28f);
        MenuUI.ComicButton("SALIR", s, new Vector2(0.5f, 0.5f), new Vector2(-300f, -244f), new Vector2(300f, 54f), MenuUI.BlueDark, Quit, 26f);
        play.Select();

        TextMeshProUGUI ver = MenuUI.Text("Version", s, "PROTOTIPO  ·  MODO HISTORIA", 15f, new Vector2(1f, 0f), new Vector2(-170f, 24f),
                                          new Vector2(320f, 30f), new Color(MenuUI.Cream.r, MenuUI.Cream.g, MenuUI.Cream.b, 0.55f));
        ver.characterSpacing = 4f;
        return g;
    }

    // ======================= PANTALLA: MODOS =======================

    CanvasGroup BuildModesScreen()
    {
        RectTransform s = MenuUI.Stretch("Modos", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Encabezado", s, "ELIGE TU MODO", 76f, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(900f, 100f), MenuUI.Yellow);

        int unlocked = catalog != null ? Mathf.Min(SaveSystem.Data.unlockedLevels, catalog.Count) : 0;
        int levels = catalog != null ? catalog.Count : 0;
        ModeCard(s, new Vector2(-380f, -30f), "HISTORIA", MenuUI.Orange,
                 levels + " niveles  ·  3 oleadas cada uno\nDefiende el lago y mejora tu traje",
                 "NIVEL " + Mathf.Max(1, unlocked) + " DE " + levels, true, () => Go(mapScreen), true);
        ModeCard(s, new Vector2(0f, -30f), "SUPERVIVENCIA", MenuUI.Blue,
                 "Oleadas sin fin\nLos peces no vuelven", "", false, null, false);
        ModeCard(s, new Vector2(380f, -30f), "COOPERATIVO", new Color32(124, 92, 206, 255),
                 "Hasta 4 jugadores online\nChat de voz tipo radio", "", false, null, false);

        MenuUI.ComicButton("VOLVER", s, new Vector2(0f, 0f), new Vector2(130f, 52f), new Vector2(200f, 52f), MenuUI.BlueDark, () => Go(titleScreen), 24f);
        return g;
    }

    void ModeCard(Transform parent, Vector2 pos, string title, Color color, string desc, string progress, bool available,
                  UnityEngine.Events.UnityAction onClick, bool showStars)
    {
        Vector2 size = new Vector2(340f, 400f);
        RectTransform card;
        if (available)
        {
            Button b = MenuUI.ComicButton(title, parent, new Vector2(0.5f, 0.5f), pos, size, color, onClick, 1f);
            card = (RectTransform)b.transform;
            Destroy(card.Find("Label").gameObject);
            Destroy(card.Find("Brillo").gameObject);
        }
        else
        {
            card = MenuUI.ComicPanel(title, parent, new Vector2(0.5f, 0.5f), pos, size, Color.Lerp(color, MenuUI.Locked, 0.55f), 4f, 7f);
        }

        MenuUI.Text("Titulo", card, title, 50f, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(320f, 70f), MenuUI.Cream, true, 0.22f);
        MenuUI.Text("Desc", card, desc, 21f, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(300f, 80f), MenuUI.Cream, false, 0.15f);

        if (available)
        {
            if (showStars)
            {
                MenuUI.Img("Estrella", card, MenuUI.Star, MenuUI.Yellow, new Vector2(0.5f, 0f), new Vector2(-46f, 150f), new Vector2(46f, 46f));
                int max = catalog != null ? catalog.Count * 3 : 0;
                MenuUI.Text("Estrellas", card, SaveSystem.TotalStars + " / " + max, 30f, new Vector2(0.5f, 0f), new Vector2(30f, 150f),
                            new Vector2(160f, 46f), MenuUI.Cream, false, 0.25f).alignment = TextAlignmentOptions.MidlineLeft;
            }
            MenuUI.Text("Progreso", card, progress, 24f, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(300f, 40f), MenuUI.Cream, false, 0.25f);
            MenuUI.Tag("JUGAR", card, new Vector2(0.5f, 0f), new Vector2(0f, 44f), MenuUI.OrangeDark, 22f);
        }
        else
        {
            MenuUI.Img("Candado", card, MenuUI.Lock, MenuUI.Ink, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(84f, 84f));
            MenuUI.Tag("PRÓXIMAMENTE", card, new Vector2(0.5f, 0f), new Vector2(0f, 50f), MenuUI.OrangeDark, 20f);
        }
    }

    // ======================= PANTALLA: MAPA DE NIVELES =======================

    static readonly string[] BlockNames = { "LA LLEGADA", "LA ORILLA", "BAJO EL AGUA", "LOS CIELOS" };

    CanvasGroup BuildMapScreen()
    {
        RectTransform s = MenuUI.Stretch("Mapa", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Title("Encabezado", s, "MODO HISTORIA", 62f, new Vector2(0f, 1f), new Vector2(230f, -50f), new Vector2(460f, 80f), MenuUI.Orange);

        // Total de estrellas (arriba a la derecha)
        MenuUI.Img("EstrellaTotal", s, MenuUI.Star, MenuUI.Yellow, new Vector2(1f, 1f), new Vector2(-190f, -50f), new Vector2(46f, 46f));
        totalStarsText = MenuUI.Text("TotalEstrellas", s, "", 32f, new Vector2(1f, 1f), new Vector2(-90f, -50f), new Vector2(150f, 50f), MenuUI.Cream, false, 0.25f);
        totalStarsText.alignment = TextAlignmentOptions.MidlineLeft;

        // El lago con los nenúfares
        RectTransform map = MenuUI.ComicPanel("Lago", s, new Vector2(0.5f, 0.5f), new Vector2(-210f, -24f), new Vector2(820f, 548f),
                                              new Color32(22, 96, 146, 255), 6f, 10f);
        Image water = MenuUI.Img("Agua", map, MenuUI.Rounded, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(808f, 536f));
        water.sprite = MenuUI.WaterGradient;
        water.type = Image.Type.Simple;
        water.color = new Color(1f, 1f, 1f, 0.55f);
        mapNodesHolder = MenuUI.Rect("Niveles", map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 548f));

        // Ficha del nivel (derecha)
        RectTransform info = MenuUI.ComicPanel("Ficha", s, new Vector2(0.5f, 0.5f), new Vector2(432f, -24f), new Vector2(360f, 548f), MenuUI.Cream, 6f, 10f);
        infoContent = MenuUI.Rect("Contenido", info, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 548f));

        MenuUI.ComicButton("VOLVER", s, new Vector2(0f, 0f), new Vector2(120f, 40f), new Vector2(180f, 48f), MenuUI.BlueDark, () => Go(modesScreen), 22f);
        Button reset = MenuUI.ComicButton("BORRAR PROGRESO", s, new Vector2(0f, 0f), new Vector2(360f, 40f), new Vector2(250f, 40f),
                                          new Color32(110, 120, 135, 255), ResetProgress, 17f);
        resetLabel = reset.GetComponentInChildren<TextMeshProUGUI>();

        mapMessage = MenuUI.Text("Mensaje", s, "", 22f, new Vector2(0.5f, 0f), new Vector2(-210f, 40f), new Vector2(640f, 40f), MenuUI.Yellow, false, 0.25f);

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
                        24f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 120f), MenuUI.Cream, false, 0.2f);
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
                                                  new Vector2(140f, 64f), new Color32(13, 52, 84, 255), 3f, 4f);
            MenuUI.Text("Num", lbl, "BLOQUE " + (row + 1), 15f, new Vector2(0.5f, 0.5f), new Vector2(0f, 13f), new Vector2(130f, 22f), MenuUI.Accent);
            MenuUI.Text("Nombre", lbl, row < BlockNames.Length ? BlockNames[row] : "", 18f, new Vector2(0.5f, 0.5f), new Vector2(0f, -9f),
                        new Vector2(132f, 26f), MenuUI.Cream, false, 0.15f);
        }

        // Sendero de puntos entre niveles
        for (int i = 0; i + 1 < count; i++)
        {
            Vector2 a = NodePosition(i), b = NodePosition(i + 1);
            int steps = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 18f));
            bool open = i + 1 < unlocked;
            for (int k = 1; k < steps; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, k / (float)steps);
                MenuUI.Img("Punto", mapNodesHolder, MenuUI.Circle,
                           open ? new Color(1f, 0.95f, 0.8f, 0.85f) : new Color(0.75f, 0.85f, 0.95f, 0.3f),
                           new Vector2(0.5f, 0.5f), p, Vector2.one * (open ? 8f : 6f));
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
        int stars = SaveSystem.GetStars(index);
        float size = boss ? 80f : 66f;
        Vector2 pos = NodePosition(index);

        RectTransform holder = MenuUI.Rect("Nivel" + (index + 1), mapNodesHolder, new Vector2(0.5f, 0.5f), pos, new Vector2(size + 16f, size + 16f));

        // Anillo (seleccionado / nivel actual)
        Image ring = MenuUI.Img("Anillo", holder, MenuUI.Circle, new Color(1f, 1f, 1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size + 18f));

        MenuUI.Img("Sombra", holder, MenuUI.Circle, new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(4f, -5f), Vector2.one * (size + 6f));
        MenuUI.Img("Contorno", holder, MenuUI.Circle, boss && unlocked ? MenuUI.Red : MenuUI.Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size + 8f));
        Image pad = MenuUI.Img("Nenufar", holder, MenuUI.Circle, unlocked ? MenuUI.Green : MenuUI.Locked, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size);
        pad.raycastTarget = true;
        // Brillo y la "muesca" del nenúfar
        MenuUI.Img("Brillo", holder, MenuUI.Circle, new Color(1f, 1f, 1f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(-size * 0.14f, size * 0.16f), Vector2.one * size * 0.5f);

        if (unlocked)
            MenuUI.Text("Numero", holder, (index + 1).ToString(), boss ? 40f : 34f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                        Vector2.one * size, MenuUI.Cream, true, 0.3f);
        else
            MenuUI.Img("Candado", holder, MenuUI.Lock, MenuUI.Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 0.55f);

        if (stars > 0)
        {
            for (int k = 0; k < 3; k++)
                MenuUI.Img("Est" + k, holder, MenuUI.Star, k < stars ? MenuUI.Yellow : new Color(0.15f, 0.25f, 0.35f, 0.9f),
                           new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 19f, -size * 0.5f - 6f + (k == 1 ? -3f : 0f)), Vector2.one * 20f);
        }
        // Etiquetas: JEFE arriba; TALLER arriba (o abajo si el nivel también es de jefe)
        if (boss) MenuUI.Tag("JEFE", holder, new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.5f + 14f), unlocked ? MenuUI.Red : MenuUI.Locked, 14f);
        if (shop) MenuUI.Tag("TALLER", holder, new Vector2(0.5f, 0.5f), new Vector2(0f, boss ? -size * 0.5f - 34f : size * 0.5f + 13f),
                             unlocked ? MenuUI.Blue : MenuUI.Locked, 12f);

        Button b = holder.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        int captured = index;
        b.onClick.AddListener(() => SelectLevel(captured));
        holder.gameObject.AddComponent<MenuButtonFX>();

        nodes.Add(new LevelNode { index = index, rt = holder, ring = ring, current = isCurrent && unlocked });
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
        Color ink = MenuUI.Ink;

        MenuUI.Title("Numero", infoContent, "NIVEL " + (index + 1), 56f, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(340f, 70f),
                     level.isBossLevel ? MenuUI.Red : MenuUI.Orange);
        MenuUI.Text("Nombre", infoContent, level.displayName, 26f, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(320f, 60f), ink);
        TextMeshProUGUI desc = MenuUI.Text("Descripcion", infoContent, level.description, 18f, new Vector2(0.5f, 1f), new Vector2(0f, -172f),
                                           new Vector2(310f, 80f), new Color(0.25f, 0.3f, 0.36f, 1f));
        desc.alignment = TextAlignmentOptions.Top;

        MenuUI.Stars(infoContent, new Vector2(0.5f, 1f), new Vector2(0f, -248f), 44f, stars);

        int fish = 0;
        foreach (FishSpawn f in level.fish) if (f != null && f.type != null) fish += f.count;
        string details = level.waves.Count + " OLEADAS  ·  " + fish + " PECES\nPierdes si cazan el " + Mathf.RoundToInt(level.maxFishLossFraction * 100f) + " %";
        MenuUI.Text("Detalles", infoContent, details, 18f, new Vector2(0.5f, 1f), new Vector2(0f, -314f), new Vector2(320f, 56f), ink);

        float tagY = -360f;
        if (level.isBossLevel && level.workshopAfter)
        {
            MenuUI.Tag("JEFE", infoContent, new Vector2(0.5f, 1f), new Vector2(-70f, tagY), MenuUI.Red, 15f);
            MenuUI.Tag("TALLER DESPUÉS", infoContent, new Vector2(0.5f, 1f), new Vector2(60f, tagY), MenuUI.Blue, 15f);
        }
        else if (level.isBossLevel) MenuUI.Tag("NIVEL DE JEFE", infoContent, new Vector2(0.5f, 1f), new Vector2(0f, tagY), MenuUI.Red, 15f);
        else if (level.workshopAfter) MenuUI.Tag("TALLER DESPUÉS", infoContent, new Vector2(0.5f, 1f), new Vector2(0f, tagY), MenuUI.Blue, 15f);

        if (unlocked)
        {
            Button play = MenuUI.ComicButton("JUGAR", infoContent, new Vector2(0.5f, 0f), new Vector2(0f, level.comicBefore != null ? 108f : 64f),
                                             new Vector2(290f, 66f), MenuUI.Orange, () => PlayLevel(index), 32f);
            play.Select();
            if (level.comicBefore != null)
            {
                ComicDefinition comic = level.comicBefore;
                MenuUI.ComicButton("VER CÓMIC", infoContent, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(230f, 44f), MenuUI.Blue,
                                   () => ComicViewer.Show(comic, () => SaveSystem.MarkSeen(comic)), 20f);
            }
        }
        else
        {
            MenuUI.Img("Candado", infoContent, MenuUI.Lock, MenuUI.Locked, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(64f, 64f));
            MenuUI.Text("Bloqueado", infoContent, "Supera el nivel " + index + " para desbloquearlo", 19f, new Vector2(0.5f, 0f),
                        new Vector2(0f, 56f), new Vector2(310f, 50f), MenuUI.Locked);
        }

        foreach (LevelNode n in nodes) n.ring.color = n.index == index ? MenuUI.Cream : new Color(1f, 1f, 1f, 0f);
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

        MenuUI.Title("Encabezado", s, "CONTROLES", 72f, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(800f, 90f), MenuUI.Yellow);
        RectTransform panel = MenuUI.ComicPanel("Lista", s, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1000f, 470f), MenuUI.Cream, 6f, 10f);

        string controls =
            "<color=#E8622A>W / S</color>  Avanzar / retroceder        <color=#E8622A>A / D</color>  Girar\n" +
            "<color=#E8622A>ESPACIO</color>  Propulsor (en el agua: subir y salir disparado)\n" +
            "<color=#E8622A>SHIFT</color>  Turbo (mantener) · impulso de nado (en el agua)\n" +
            "<color=#E8622A>CTRL</color>  Bucear más hondo\n" +
            "<color=#E8622A>CLIC IZQUIERDO</color>  Disparar (mantener = ráfaga)\n" +
            "<color=#E8622A>CLIC DERECHO</color>  Disparo cargado (soltar al llenarse)\n" +
            "<color=#E8622A>Q</color>  Cambiar cámara        <color=#E8622A>ESC</color>  Pausa\n\n" +
            "<color=#2F80ED>OBJETIVO:</color> derriba a todas las aves antes de que cacen a demasiados peces.\n" +
            "El agua recarga el propulsor. La munición está en cápsulas dentro del lago.\n" +
            "Si un ave atrapa un pez, dispárale: lo soltará.";
        TextMeshProUGUI t = MenuUI.Text("Texto", panel, controls, 23f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 420f), MenuUI.Ink);
        t.lineSpacing = 12f;

        MenuUI.ComicButton("VOLVER", s, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(220f, 54f), MenuUI.BlueDark, () => Go(titleScreen), 24f);
        return g;
    }

    // ======================= NAVEGACIÓN =======================

    void Go(CanvasGroup target)
    {
        if (busy || target == current) return;
        if (target == mapScreen) { selectedLevel = -1; RebuildMap(); }
        StartCoroutine(SwitchTo(target));
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

        // Nivel actual en el mapa: anillo amarillo que late
        if (current == mapScreen)
        {
            foreach (LevelNode n in nodes)
            {
                if (!n.current || n.index == selectedLevel) continue;
                float pulse = Mathf.Sin(time * 4f) * 0.5f + 0.5f;
                n.ring.color = new Color(MenuUI.Yellow.r, MenuUI.Yellow.g, MenuUI.Yellow.b, 0.35f + 0.5f * pulse);
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

        if (busy || ComicViewer.IsShowing) return;
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
