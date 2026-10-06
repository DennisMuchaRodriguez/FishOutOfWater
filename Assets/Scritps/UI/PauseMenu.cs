using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Pausa (ESC) y pantalla de muerte provisionales. Se construyen por código.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    // Evita que el clic que cierra la pausa dispare un proyectil
    public static bool InputBlockedThisFrame { get { return Time.frameCount <= resumeFrame + 1; } }
    static int resumeFrame = -10;

    public string mainMenuScene = "MainMenu";
    public PlayerController_Base player;
    public KeyCode pauseKey = KeyCode.Escape;
    public float deathScreenDelay = 1.5f;

    RectTransform pauseGroup;
    RectTransform deathGroup;
    float deathTimer;

    void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController_Base>();

        Canvas canvas = MenuUI.CreateCanvas("PauseCanvas", 100);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;

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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}
