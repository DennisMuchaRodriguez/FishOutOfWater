using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

// Pausa (ESC), pantalla de muerte y pantalla de fin de nivel. Se construyen por código
// con el mismo estilo holográfico del casco que el menú principal (MenuUI).
//  - Victoria: estrellas, resumen, cómic de después del nivel (la primera vez) y
//    SIGUIENTE NIVEL / REPETIR / MAPA DE NIVELES. Si el nivel da visita al Taller: IR AL TALLER.
//  - Derrota o muerte: REINTENTAR / MAPA DE NIVELES.
//  - Supervivencia: fin de la partida con las oleadas superadas y el récord; NUEVA PARTIDA / MENÚ PRINCIPAL.
//  - Cooperativo: la pausa NO detiene el juego (los demás siguen jugando), solo tus controles. Si tu traje se
//    destruye miras a tus compañeros (SEGUIR MIRANDO) hasta el Taller. Al final, el anfitrión elige NUEVA PARTIDA
//    o VOLVER A LA SALA; SALIR DE LA SALA vuelve al menú.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    public static bool GameEnded { get; private set; }
    // Evita que el clic que cierra la pausa dispare un proyectil
    public static bool InputBlockedThisFrame { get { return Time.frameCount <= resumeFrame + 1; } }
    static int resumeFrame = -10;

    public PlayerController_Base player;
    public KeyCode pauseKey = KeyCode.Escape;
    public float deathScreenDelay = 1.5f;

    RectTransform root;
    RectTransform pauseGroup;
    RectTransform deathGroup;
    TextMeshProUGUI deathSub;
    bool deathDismissed;
    static bool Coop { get { return CoopSession.InGame; } }
    float deathTimer;

    void Awake()
    {
        IsPaused = false;
        GameEnded = false;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (player == null) player = PlayerController_Base.FindLocal();

        Canvas canvas = MenuUI.CreateCanvas("PauseCanvas", 100);
        canvas.transform.SetParent(transform, false);
        root = (RectTransform)canvas.transform;

        // ---- Pausa ----
        pauseGroup = MenuUI.Stretch("Pausa", root);
        Backdrop(pauseGroup, MenuUI.WithAlpha(MenuUI.Ink, 0.78f));
        RectTransform card = Card("Ficha", pauseGroup, new Vector2(0f, -10f), new Vector2(460f, 470f), MenuUI.Accent, MenuUI.Glass);
        MenuUI.Title("Titulo", card, "PAUSA", 70f, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(420f, 90f), MenuUI.Accent);
        MenuUI.Rule(card, new Vector2(0.5f, 1f), new Vector2(0f, -108f), 300f, MenuUI.Accent);
        MenuUI.ComicButton("CONTINUAR", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(340f, 62f), MenuUI.Orange, Resume, 28f);
        // En Supervivencia no hay mapa: NUEVA PARTIDA y MENÚ PRINCIPAL (que termina la partida)
        bool survival = GameSession.IsSurvival;
        bool coop = Coop;
        if (coop)
        {
            // Cooperativo: solo el anfitrión puede empezar otra partida (para todos)
            float y = -18f;
            if (CoopSession.IsHost)
            {
                MenuUI.ComicButton("NUEVA PARTIDA", card, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 56f), MenuUI.Blue, GameSession.ReplayLevel, 24f);
                y -= 72f;
            }
            MenuUI.ComicButton("SALIR DE LA SALA", card, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 56f), MenuUI.BlueDark, CoopSession.Leave, 24f);
            MenuUI.Text("Aviso", card, "El juego sigue mientras estás en pausa", 16f, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 24f), MenuUI.TextDim);
        }
        else
        {
            MenuUI.ComicButton(survival ? "NUEVA PARTIDA" : "REINICIAR NIVEL", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.ReplayLevel, 24f);
            if (!survival)
                MenuUI.ComicButton("MAPA DE NIVELES", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.GoToLevelMap, 24f);
            MenuUI.ComicButton("MENÚ PRINCIPAL", card, new Vector2(0.5f, 0.5f), new Vector2(0f, survival ? -90f : -162f), new Vector2(340f, 56f), MenuUI.BlueDark, GameSession.GoToMainMenu, 24f);
        }
        pauseGroup.gameObject.SetActive(false);

        // ---- Muerte ----
        deathGroup = MenuUI.Stretch("Muerte", root);
        Backdrop(deathGroup, new Color(0.18f, 0f, 0f, 0.6f));
        RectTransform dcard = Card("Ficha", deathGroup, new Vector2(0f, -10f), new Vector2(560f, 380f), MenuUI.Red,
                                   MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, MenuUI.Red, 0.08f), 0.9f));
        MenuUI.Title("Titulo", dcard, "TRAJE DESTRUIDO", 64f, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(520f, 90f), MenuUI.Red);
        deathSub = MenuUI.Text("Sub", dcard, "La armadura llegó a cero", 24f, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(500f, 40f), MenuUI.TextDim);
        deathSub.characterSpacing = 2f;
        if (coop)
        {
            // Cooperativo: sigues mirando a tus compañeros; vuelves reparado en el próximo Taller
            deathSub.text = "Mira a tus compañeros: vuelves reparado en el próximo Taller";
            MenuUI.ComicButton("SEGUIR MIRANDO", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(340f, 62f), MenuUI.Orange, DismissDeath, 28f);
            MenuUI.ComicButton("SALIR DE LA SALA", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(340f, 56f), MenuUI.Blue, CoopSession.Leave, 24f);
        }
        else
        {
            MenuUI.ComicButton(survival ? "NUEVA PARTIDA" : "REINTENTAR", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(340f, 62f), MenuUI.Orange, GameSession.ReplayLevel, 28f);
            if (survival)
                MenuUI.ComicButton("MENÚ PRINCIPAL", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.GoToMainMenu, 24f);
            else
                MenuUI.ComicButton("MAPA DE NIVELES", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.GoToLevelMap, 24f);
        }
        deathGroup.gameObject.SetActive(false);
    }

    void Update()
    {
        if (GameEnded || ComicViewer.IsShowing) return;
        bool dead = player != null && player.isDead;

        // Cooperativo: después de SEGUIR MIRANDO, la pausa funciona normal (para poder salir de la sala)
        if (dead && !(Coop && deathDismissed))
        {
            deathTimer += Time.unscaledDeltaTime;
            if (deathTimer >= deathScreenDelay && !deathGroup.gameObject.activeSelf)
            {
                // Supervivencia: hasta qué oleada se llegó y el récord
                if (GameSession.IsSurvival && !Coop && deathSub != null)
                {
                    GameDirector d = GameDirector.Instance;
                    int wave = d != null ? Mathf.Max(1, d.CurrentWave) : SurvivalRun.NextWave;
                    deathSub.text = "Llegaste a la oleada " + wave + "  ·  " +
                                    (SurvivalRun.NewRecord ? "¡NUEVO RÉCORD!" : "Récord: " + SaveSystem.BestSurvivalWave + " oleadas");
                }
                deathGroup.gameObject.SetActive(true);
                ShowCursor();
                StartCoroutine(ArmButtons(deathGroup, deathGroup.GetComponentInChildren<Button>()));
            }
            return;
        }

        if (Input.GetKeyDown(pauseKey))
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    // ======================= FIN DEL NIVEL (lo llama el GameDirector) =======================

    public void ShowEndScreen(bool victory)
    {
        if (GameEnded) return;
        GameEnded = true;
        if (pauseGroup != null) pauseGroup.gameObject.SetActive(false);
        StartCoroutine(EndScreenRoutine(victory));
    }

    IEnumerator EndScreenRoutine(bool victory)
    {
        // Deja ver un momento el final (cámara lenta) antes de mostrar el resultado
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(2f);

        IsPaused = true;
        Time.timeScale = 0f;
        ShowCursor();

        // Cómic de después del nivel (solo la primera vez que se gana)
        GameDirector director = GameDirector.Instance;
        LevelDefinition level = director != null ? director.Level : null;
        ComicDefinition comic = victory && level != null ? level.comicAfter : null;
        if (comic != null && !SaveSystem.HasSeen(comic))
        {
            bool done = false;
            ComicViewer.Show(comic, () => { SaveSystem.MarkSeen(comic); done = true; });
            while (!done) yield return null;
            ShowCursor();
        }

        BuildResults(victory, director, level);
    }

    void BuildResults(bool victory, GameDirector director, LevelDefinition level)
    {
        if (director != null && director.IsCoop)
        {
            BuildCoopResults(director);
            return;
        }
        if (director != null && director.IsSurvival)
        {
            BuildSurvivalResults(director);
            return;
        }
        RectTransform group = MenuUI.Stretch("Resultado", root);
        Backdrop(group, victory ? MenuUI.WithAlpha(MenuUI.Ink, 0.8f) : new Color(0.16f, 0f, 0f, 0.68f));
        RectTransform card = Card("Ficha", group, new Vector2(0f, -6f), new Vector2(640f, 600f), victory ? MenuUI.Accent : MenuUI.Red, MenuUI.Glass);

        int number = director != null ? director.LevelNumber : 1;
        string levelName = level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : "";
        MenuUI.Text("Nivel", card, "NIVEL " + number + (levelName != "" ? "  ·  " + levelName.ToUpper() : ""), 22f,
                    new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 34f), MenuUI.TextDim).characterSpacing = 3f;
        MenuUI.Title("Titulo", card, victory ? "¡LAGO A SALVO!" : "LOS PECES FUERON CAZADOS", victory ? 66f : 50f,
                     new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 80f), victory ? MenuUI.Orange : MenuUI.Red);

        int stars = director != null ? director.Stars : 0;
        RectTransform[] starImgs = MenuUI.Stars(card, new Vector2(0.5f, 1f), new Vector2(0f, -178f), 70f, stars);
        StartCoroutine(PopStars(starImgs, stars));

        if (director != null)
        {
            string o = "<color=" + MenuUI.HexOrange + ">";
            string stats = "Peces a salvo: " + o + director.FishAlive + " de " + director.TotalFish + "</color>\n" +
                           "Aves derribadas: " + o + director.BirdsKilled + "</color>      " +
                           "Peces rescatados: " + o + director.FishRescued + "</color>";
            MenuUI.Text("Resumen", card, stats, 23f, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(600f, 70f), MenuUI.Cream).lineSpacing = 8f;
        }

        // Novedades: Taller abierto, siguiente nivel desbloqueado, historia completada
        bool workshop = victory && WorkshopFlow.IsPending(GameSession.LevelIndex);
        string news = "";
        Color newsColor = MenuUI.Yellow;
        if (workshop) { news = "TALLER ABIERTO: instala una mejora en tu traje"; newsColor = MenuUI.Orange; }
        else if (victory)
        {
            if (GameSession.HasNextLevel) news = "¡Nivel " + (number + 1) + " desbloqueado!";
            else if (GameSession.LevelIndex >= 0) news = "¡COMPLETASTE LA HISTORIA!";
        }
        else { news = "Protege mejor a los peces: si cazan demasiados, pierdes"; newsColor = MenuUI.Red; }
        if (news != "")
        {
            RectTransform tag = MenuUI.HoloPanel("Novedad", card, new Vector2(0.5f, 1f), new Vector2(0f, -334f), new Vector2(560f, 44f),
                                                 MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, newsColor, 0.16f), 0.92f), MenuUI.WithAlpha(newsColor, 0.92f),
                                                 MenuUI.WithAlpha(newsColor, 0.28f), MenuUI.WithAlpha(newsColor, 0.38f), 0f, false, false);
            Color textColor = Color.Lerp(newsColor, Color.white, 0.55f);
            float textX = 0f;
            if (workshop)
            {
                MenuUI.Img("Llave", tag, MenuUI.Wrench, newsColor, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(24f, 24f));
                textX = 12f;
            }
            MenuUI.Text("Texto", tag, news, 19f, new Vector2(0.5f, 0.5f), new Vector2(textX, 1f), new Vector2(520f, 40f), textColor);
        }

        // Botones
        Button first;
        if (workshop)
        {
            // Después de un nivel con Taller: primero la mejora (al salir del Taller sigue el siguiente nivel)
            int levelIndex = GameSession.LevelIndex;
            first = MenuUI.ComicButton("IR AL TALLER", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 64f), MenuUI.Orange,
                                       () => WorkshopFlow.Open(levelIndex, true), 30f);
            MenuUI.ComicButton("REPETIR", card, new Vector2(0.5f, 0f), new Vector2(-130f, 66f), new Vector2(230f, 54f), MenuUI.Blue, GameSession.ReplayLevel, 22f);
            MenuUI.ComicButton("MAPA DE NIVELES", card, new Vector2(0.5f, 0f), new Vector2(130f, 66f), new Vector2(230f, 54f), MenuUI.Blue, GameSession.GoToLevelMap, 20f);
        }
        else if (victory)
        {
            if (GameSession.HasNextLevel)
            {
                first = MenuUI.ComicButton("SIGUIENTE NIVEL", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 64f), MenuUI.Orange, PlayNext, 30f);
                MenuUI.ComicButton("REPETIR", card, new Vector2(0.5f, 0f), new Vector2(-130f, 66f), new Vector2(230f, 54f), MenuUI.Blue, GameSession.ReplayLevel, 22f);
                MenuUI.ComicButton("MAPA DE NIVELES", card, new Vector2(0.5f, 0f), new Vector2(130f, 66f), new Vector2(230f, 54f), MenuUI.Blue, GameSession.GoToLevelMap, 20f);
            }
            else
            {
                first = MenuUI.ComicButton("MAPA DE NIVELES", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 64f), MenuUI.Orange, GameSession.GoToLevelMap, 28f);
                MenuUI.ComicButton("REPETIR", card, new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(260f, 54f), MenuUI.Blue, GameSession.ReplayLevel, 22f);
            }
        }
        else
        {
            first = MenuUI.ComicButton("REINTENTAR", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 64f), MenuUI.Orange, GameSession.ReplayLevel, 30f);
            MenuUI.ComicButton("MAPA DE NIVELES", card, new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(300f, 54f), MenuUI.Blue, GameSession.GoToLevelMap, 22f);
        }
        StartCoroutine(ArmButtons(group, first));
        StartCoroutine(PopIn(card));
    }

    // Fin de una partida de Supervivencia: hasta qué oleada se llegó, el récord y NUEVA PARTIDA / MENÚ PRINCIPAL
    void BuildSurvivalResults(GameDirector director)
    {
        RectTransform group = MenuUI.Stretch("Resultado", root);
        Backdrop(group, new Color(0.16f, 0f, 0f, 0.68f));
        RectTransform card = Card("Ficha", group, new Vector2(0f, -6f), new Vector2(640f, 560f), MenuUI.Red, MenuUI.Glass);

        MenuUI.Text("Modo", card, "SUPERVIVENCIA", 22f, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 34f), MenuUI.TextDim)
            .characterSpacing = 3f;
        MenuUI.Title("Titulo", card, "FIN DE LA PARTIDA", 56f, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(600f, 80f), MenuUI.Red);

        int cleared = SurvivalRun.WavesCleared;
        int best = SaveSystem.BestSurvivalWave;
        string o = "<color=" + MenuUI.HexOrange + ">";
        MenuUI.Title("Oleadas", card, cleared + (cleared == 1 ? " OLEADA SUPERADA" : " OLEADAS SUPERADAS"), 40f,
                     new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(600f, 60f), MenuUI.Orange);
        string stats = "Llegaste a la oleada " + o + Mathf.Max(1, director.CurrentWave) + "</color>      Récord: " + o + best + "</color>\n" +
                       "Aves derribadas: " + o + director.BirdsKilled + "</color>      " +
                       "Peces rescatados: " + o + director.FishRescued + "</color>\n" +
                       "Peces que quedaban: " + o + director.FishAlive + " de " + director.TotalFish + "</color>";
        MenuUI.Text("Resumen", card, stats, 22f, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(600f, 100f), MenuUI.Cream).lineSpacing = 8f;

        bool record = SurvivalRun.NewRecord;
        Color newsColor = record ? MenuUI.Yellow : MenuUI.Red;
        string news = record ? "¡NUEVO RÉCORD!" : "Mejora el traje entre oleadas y cuida a los peces";
        RectTransform tag = MenuUI.HoloPanel("Novedad", card, new Vector2(0.5f, 1f), new Vector2(0f, -344f), new Vector2(560f, 44f),
                                             MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, newsColor, 0.16f), 0.92f), MenuUI.WithAlpha(newsColor, 0.92f),
                                             MenuUI.WithAlpha(newsColor, 0.28f), MenuUI.WithAlpha(newsColor, 0.38f), 0f, false, false);
        MenuUI.Text("Texto", tag, news, 19f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(520f, 40f), Color.Lerp(newsColor, Color.white, 0.55f));

        Button first = MenuUI.ComicButton("NUEVA PARTIDA", card, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(380f, 64f), MenuUI.Orange, GameSession.ReplayLevel, 30f);
        MenuUI.ComicButton("MENÚ PRINCIPAL", card, new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(300f, 54f), MenuUI.Blue, GameSession.GoToMainMenu, 22f);
        StartCoroutine(ArmButtons(group, first));
        StartCoroutine(PopIn(card));
    }

    // Fin de una partida cooperativa: lo que lograron entre todos; el anfitrión elige qué sigue
    void BuildCoopResults(GameDirector director)
    {
        RectTransform group = MenuUI.Stretch("Resultado", root);
        Backdrop(group, new Color(0.16f, 0f, 0f, 0.68f));
        RectTransform card = Card("Ficha", group, new Vector2(0f, -6f), new Vector2(640f, 560f), MenuUI.Purple, MenuUI.Glass);

        MenuUI.Text("Modo", card, "COOPERATIVO  ·  " + CoopSession.PlayerCount + " JUGADORES", 22f, new Vector2(0.5f, 1f), new Vector2(0f, -36f),
                    new Vector2(600f, 34f), MenuUI.TextDim).characterSpacing = 3f;
        MenuUI.Title("Titulo", card, "FIN DE LA PARTIDA", 56f, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(600f, 80f), MenuUI.Red);
        int cleared = SurvivalRun.WavesCleared;
        MenuUI.Title("Oleadas", card, cleared + (cleared == 1 ? " OLEADA SUPERADA" : " OLEADAS SUPERADAS"), 40f,
                     new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(600f, 60f), MenuUI.Orange);
        string o = "<color=" + MenuUI.HexOrange + ">";
        string stats = "Llegaron a la oleada " + o + Mathf.Max(1, director.CurrentWave) + "</color>\n" +
                       "Aves derribadas: " + o + director.BirdsKilled + "</color>      " +
                       "Peces rescatados: " + o + director.FishRescued + "</color>\n" +
                       "Peces que quedaban: " + o + director.FishAlive + " de " + director.TotalFish + "</color>";
        MenuUI.Text("Resumen", card, stats, 22f, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(600f, 100f), MenuUI.Cream).lineSpacing = 8f;

        Button first;
        if (CoopSession.IsHost)
        {
            first = MenuUI.ComicButton("NUEVA PARTIDA", card, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(380f, 60f), MenuUI.Orange, GameSession.ReplayLevel, 28f);
            MenuUI.ComicButton("VOLVER A LA SALA", card, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(340f, 52f), MenuUI.Purple, CoopSession.HostBackToLobby, 22f);
            MenuUI.ComicButton("SALIR DE LA SALA", card, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(300f, 48f), MenuUI.BlueDark, CoopSession.Leave, 20f);
        }
        else
        {
            MenuUI.Text("Espera", card, "El anfitrión elige si juegan otra partida", 19f, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(560f, 30f), MenuUI.Yellow);
            first = MenuUI.ComicButton("SALIR DE LA SALA", card, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(340f, 54f), MenuUI.BlueDark, CoopSession.Leave, 22f);
        }
        StartCoroutine(ArmButtons(group, first));
        StartCoroutine(PopIn(card));
    }

    // Siguiente nivel: si tiene cómic de antes y no se ha visto, primero el cómic
    // (así se ven las presentaciones de los jefes sin pasar por el mapa)
    void PlayNext()
    {
        LevelCatalog catalog = GameSession.Catalog;
        LevelDefinition next = catalog != null ? catalog.Get(GameSession.LevelIndex + 1) : null;
        ComicDefinition comic = next != null ? next.comicBefore : null;
        if (comic != null && !SaveSystem.HasSeen(comic))
            ComicViewer.Show(comic, () => { SaveSystem.MarkSeen(comic); GameSession.PlayNextLevel(); });
        else
            GameSession.PlayNextLevel();
    }

    // Fondo oscuro con líneas de escaneo (como mirar a través del visor)
    static void Backdrop(RectTransform group, Color color)
    {
        MenuUI.Panel("Oscuro", group, color);
        Image scan = MenuUI.Stretch("Escaneo", group).gameObject.AddComponent<Image>();
        scan.sprite = MenuUI.ScanSprite;
        scan.type = Image.Type.Tiled;
        scan.color = MenuUI.WithAlpha(MenuUI.Accent, 0.04f);
        scan.raycastTarget = false;
    }

    // Ficha holográfica con borde del color indicado (cian normal, rojo en la derrota)
    static RectTransform Card(string name, RectTransform parent, Vector2 pos, Vector2 size, Color edge, Color glass)
    {
        return MenuUI.HoloPanel(name, parent, new Vector2(0.5f, 0.5f), pos, size, glass, MenuUI.WithAlpha(edge, 0.9f),
                                MenuUI.WithAlpha(edge, 0.18f), MenuUI.WithAlpha(edge, 0.3f), 0.05f);
    }

    // Los botones no responden durante medio segundo: así un clic o tecla que venía
    // del juego (disparo, propulsor) no pulsa REINTENTAR o SIGUIENTE NIVEL sin querer
    IEnumerator ArmButtons(RectTransform group, Button first)
    {
        CanvasGroup cg = group.GetComponent<CanvasGroup>();
        if (cg == null) cg = group.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        yield return new WaitForSecondsRealtime(0.5f);
        cg.blocksRaycasts = true;
        if (first != null) first.Select();
    }

    IEnumerator PopIn(RectTransform card)
    {
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.35f);
            float s = 0.7f + 0.3f * (1f - Mathf.Pow(1f - k, 3f)) + Mathf.Sin(k * Mathf.PI) * 0.06f;
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    IEnumerator PopStars(RectTransform[] stars, int earned)
    {
        foreach (RectTransform s in stars) s.localScale = Vector3.zero;
        yield return new WaitForSecondsRealtime(0.25f);
        for (int i = 0; i < stars.Length; i++)
        {
            float t = 0f;
            float dur = i < earned ? 0.28f : 0.12f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = i < earned ? 1f + Mathf.Sin(k * Mathf.PI) * 0.35f : k;
                stars[i].localScale = Vector3.one * (k < 1f ? Mathf.Max(k, s * k) : 1f);
                yield return null;
            }
            stars[i].localScale = Vector3.one;
        }
    }

    // ======================= PAUSA =======================

    // Cooperativo: el aviso de la muerte se cierra y se sigue mirando a los compañeros
    void DismissDeath()
    {
        deathDismissed = true;
        deathGroup.gameObject.SetActive(false);
    }

    void Pause()
    {
        IsPaused = true;
        // En cooperativo el juego sigue (los demás no se pueden quedar congelados)
        Time.timeScale = Coop ? 1f : 0f;
        pauseGroup.gameObject.SetActive(true);
        ShowCursor();
        pauseGroup.GetComponentInChildren<Button>().Select();
    }

    void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        resumeFrame = Time.frameCount;
        pauseGroup.gameObject.SetActive(false);
    }

    static void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void OnDestroy()
    {
        IsPaused = false;
        GameEnded = false;
        Time.timeScale = 1f;
    }
}
