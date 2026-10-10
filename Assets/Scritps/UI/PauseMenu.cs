using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// Pausa (ESC), pantalla de muerte y pantalla de fin de nivel. Se construyen por código
// con el mismo estilo de cómic del menú principal (MenuUI).
//  - Victoria: estrellas, resumen, cómic de después del nivel (la primera vez) y
//    SIGUIENTE NIVEL / REPETIR / MAPA DE NIVELES.
//  - Derrota o muerte: REINTENTAR / MAPA DE NIVELES.
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
    float deathTimer;

    void Awake()
    {
        IsPaused = false;
        GameEnded = false;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();

        Canvas canvas = MenuUI.CreateCanvas("PauseCanvas", 100);
        canvas.transform.SetParent(transform, false);
        root = (RectTransform)canvas.transform;

        // ---- Pausa ----
        pauseGroup = MenuUI.Stretch("Pausa", root);
        MenuUI.Panel("Oscuro", pauseGroup, new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, 0.78f));
        RectTransform card = MenuUI.ComicPanel("Ficha", pauseGroup, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(460f, 470f), MenuUI.Cream, 6f, 10f);
        MenuUI.Title("Titulo", card, "PAUSA", 70f, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(420f, 90f), MenuUI.Orange);
        MenuUI.ComicButton("CONTINUAR", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(340f, 62f), MenuUI.Orange, Resume, 28f);
        MenuUI.ComicButton("REINICIAR NIVEL", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.ReplayLevel, 24f);
        MenuUI.ComicButton("MAPA DE NIVELES", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.GoToLevelMap, 24f);
        MenuUI.ComicButton("MENÚ PRINCIPAL", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -162f), new Vector2(340f, 56f), MenuUI.BlueDark, GameSession.GoToMainMenu, 24f);
        pauseGroup.gameObject.SetActive(false);

        // ---- Muerte ----
        deathGroup = MenuUI.Stretch("Muerte", root);
        MenuUI.Panel("Rojo", deathGroup, new Color(0.18f, 0f, 0f, 0.6f));
        RectTransform dcard = MenuUI.ComicPanel("Ficha", deathGroup, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(560f, 380f), MenuUI.Cream, 6f, 10f);
        MenuUI.Title("Titulo", dcard, "TRAJE DESTRUIDO", 64f, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(520f, 90f), MenuUI.Red);
        MenuUI.Text("Sub", dcard, "La armadura llegó a cero", 24f, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(500f, 40f), MenuUI.Ink);
        MenuUI.ComicButton("REINTENTAR", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(340f, 62f), MenuUI.Orange, GameSession.ReplayLevel, 28f);
        MenuUI.ComicButton("MAPA DE NIVELES", dcard, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(340f, 56f), MenuUI.Blue, GameSession.GoToLevelMap, 24f);
        deathGroup.gameObject.SetActive(false);
    }

    void Update()
    {
        if (GameEnded || ComicViewer.IsShowing) return;
        bool dead = player != null && player.isDead;

        if (dead)
        {
            deathTimer += Time.unscaledDeltaTime;
            if (deathTimer >= deathScreenDelay && !deathGroup.gameObject.activeSelf)
            {
                deathGroup.gameObject.SetActive(true);
                ShowCursor();
                deathGroup.GetComponentInChildren<Button>().Select();
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
        RectTransform group = MenuUI.Stretch("Resultado", root);
        MenuUI.Panel("Oscuro", group, victory ? new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, 0.8f) : new Color(0.18f, 0f, 0f, 0.65f));
        RectTransform card = MenuUI.ComicPanel("Ficha", group, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(640f, 600f), MenuUI.Cream, 6f, 12f);

        int number = director != null ? director.LevelNumber : 1;
        string levelName = level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : "";
        MenuUI.Text("Nivel", card, "NIVEL " + number + (levelName != "" ? "  ·  " + levelName.ToUpper() : ""), 22f,
                    new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 34f), new Color(0.3f, 0.35f, 0.42f, 1f));
        MenuUI.Title("Titulo", card, victory ? "¡LAGO A SALVO!" : "LOS PECES FUERON CAZADOS", victory ? 66f : 50f,
                     new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 80f), victory ? MenuUI.Orange : MenuUI.Red);

        int stars = director != null ? director.Stars : 0;
        Image[] starImgs = MenuUI.Stars(card, new Vector2(0.5f, 1f), new Vector2(0f, -178f), 70f, stars);
        StartCoroutine(PopStars(starImgs, stars));

        if (director != null)
        {
            string stats = "Peces a salvo: <color=#E8622A>" + director.FishAlive + " de " + director.TotalFish + "</color>\n" +
                           "Aves derribadas: <color=#E8622A>" + director.BirdsKilled + "</color>      " +
                           "Peces rescatados: <color=#E8622A>" + director.FishRescued + "</color>";
            MenuUI.Text("Resumen", card, stats, 23f, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(600f, 70f), MenuUI.Ink).lineSpacing = 8f;
        }

        // Novedades: siguiente nivel desbloqueado, Taller, historia completada
        string news = "";
        if (victory)
        {
            if (level != null && level.workshopAfter) news = "TALLER: pronto podrás elegir una mejora del traje aquí";
            else if (GameSession.HasNextLevel) news = "¡Nivel " + (number + 1) + " desbloqueado!";
            else if (GameSession.LevelIndex >= 0) news = "¡COMPLETASTE LA HISTORIA!";
        }
        else news = "Protege mejor a los peces: si cazan demasiados, pierdes";
        if (news != "")
        {
            RectTransform tag = MenuUI.ComicPanel("Novedad", card, new Vector2(0.5f, 1f), new Vector2(0f, -334f), new Vector2(560f, 44f),
                                                  victory ? MenuUI.Yellow : new Color(1f, 0.84f, 0.78f, 1f), 3f, 4f);
            MenuUI.Text("Texto", tag, news, 19f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(540f, 40f), MenuUI.Ink);
        }

        // Botones
        Button first;
        if (victory)
        {
            if (GameSession.HasNextLevel)
            {
                first = MenuUI.ComicButton("SIGUIENTE NIVEL", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 64f), MenuUI.Orange, GameSession.PlayNextLevel, 30f);
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
        first.Select();

        StartCoroutine(PopIn(card));
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

    IEnumerator PopStars(Image[] stars, int earned)
    {
        foreach (Image s in stars) s.rectTransform.localScale = Vector3.zero;
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
                stars[i].rectTransform.localScale = Vector3.one * (k < 1f ? Mathf.Max(k, s * k) : 1f);
                yield return null;
            }
            stars[i].rectTransform.localScale = Vector3.one;
        }
    }

    // ======================= PAUSA =======================

    void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
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
