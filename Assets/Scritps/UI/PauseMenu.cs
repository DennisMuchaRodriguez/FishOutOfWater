using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Pausa (ESC), pantalla de muerte y pantalla de fin de partida. Se construyen por código.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    public static bool GameEnded { get; private set; }
    // Evita que el clic que cierra la pausa dispare un proyectil
    public static bool InputBlockedThisFrame { get { return Time.frameCount <= resumeFrame + 1; } }
    static int resumeFrame = -10;

    public string mainMenuScene = "MainMenu";
    public PlayerController_Base player;
    public KeyCode pauseKey = KeyCode.Escape;
    public float deathScreenDelay = 1.5f;

    RectTransform root;
    RectTransform pauseGroup;
    RectTransform deathGroup;
    RectTransform endGroup;
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

        // Pausa
        pauseGroup = MenuUI.Stretch("Pause", root);
        MenuUI.Panel("Dim", pauseGroup, new Color(0.01f, 0.04f, 0.07f, 0.8f));
        TextMeshProUGUI t = MenuUI.CreateText("Title", pauseGroup, "PAUSA", 60f, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(800f, 90f));
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 14f;
        MenuUI.CreateButton("CONTINUAR", pauseGroup, new Vector2(0f, 40f), Resume);
        MenuUI.CreateButton("REINICIAR", pauseGroup, new Vector2(0f, -30f), Restart);
        MenuUI.CreateButton("MENÚ PRINCIPAL", pauseGroup, new Vector2(0f, -100f), GoToMenu);
        pauseGroup.gameObject.SetActive(false);

        // Muerte
        deathGroup = MenuUI.Stretch("Death", root);
        MenuUI.Panel("Dim", deathGroup, new Color(0.15f, 0f, 0f, 0.55f));
        TextMeshProUGUI d = MenuUI.CreateText("Title", deathGroup, "TRAJE DESTRUIDO", 56f, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1000f, 90f));
        d.fontStyle = FontStyles.Bold;
        d.characterSpacing = 12f;
        d.color = new Color(1f, 0.35f, 0.3f, 1f);
        MenuUI.CreateButton("REINTENTAR", deathGroup, new Vector2(0f, 10f), Restart);
        MenuUI.CreateButton("MENÚ PRINCIPAL", deathGroup, new Vector2(0f, -60f), GoToMenu);
        deathGroup.gameObject.SetActive(false);
    }

    void Update()
    {
        if (GameEnded) return;
        bool dead = player != null && player.isDead;

        if (dead)
        {
            deathTimer += Time.unscaledDeltaTime;
            if (deathTimer >= deathScreenDelay && !deathGroup.gameObject.activeSelf)
            {
                deathGroup.gameObject.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
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

    // Victoria o derrota (la llama el GameDirector)
    public void ShowEndScreen(bool victory, string title, string subtitle)
    {
        if (GameEnded) return;
        GameEnded = true;
        if (pauseGroup != null) pauseGroup.gameObject.SetActive(false);
        StartCoroutine(EndScreenRoutine(victory, title, subtitle));
    }

    System.Collections.IEnumerator EndScreenRoutine(bool victory, string title, string subtitle)
    {
        // Deja ver un momento el final (cámara lenta) antes de mostrar el menú
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(2f);

        Color c = victory ? MenuUI.Accent : new Color(1f, 0.35f, 0.3f, 1f);
        endGroup = MenuUI.Stretch("Fin", root);
        MenuUI.Panel("Dim", endGroup, victory ? new Color(0.01f, 0.06f, 0.09f, 0.82f) : new Color(0.15f, 0f, 0f, 0.6f));
        TextMeshProUGUI t = MenuUI.CreateText("Title", endGroup, title, 56f, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 90f));
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 12f;
        t.color = c;
        TextMeshProUGUI st = MenuUI.CreateText("Sub", endGroup, subtitle, 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1200f, 40f));
        st.color = new Color(c.r, c.g, c.b, 0.8f);
        st.characterSpacing = 6f;
        Button first = MenuUI.CreateButton(victory ? "JUGAR DE NUEVO" : "REINTENTAR", endGroup, new Vector2(0f, -10f), Restart);
        MenuUI.CreateButton("MENÚ PRINCIPAL", endGroup, new Vector2(0f, -80f), GoToMenu);

        IsPaused = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        first.Select();
    }

    void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        pauseGroup.gameObject.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        pauseGroup.GetComponentInChildren<Button>().Select();
    }

    void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        resumeFrame = Time.frameCount;
        pauseGroup.gameObject.SetActive(false);
    }

    void Restart()
    {
        IsPaused = false;
        GameEnded = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu()
    {
        IsPaused = false;
        GameEnded = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    void OnDestroy()
    {
        IsPaused = false;
        GameEnded = false;
        Time.timeScale = 1f;
    }
}
