using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Menú principal provisional. Se construye solo por código:
// basta con un GameObject vacío con este script en la escena MainMenu.
public class MainMenu : MonoBehaviour
{
    [Tooltip("Nombre de la escena del juego (debe estar en Build Settings)")]
    public string gameSceneName = "SampleScene";
    public string title = "FISH OUT OF WATER";
    public string subtitle = "UN PEZ. UN TRAJE. NADA DE AGUA.";

    RectTransform root;
    RectTransform mainGroup;
    RectTransform controlsGroup;
    TextMeshProUGUI titleText;
    Image fade;
    bool loading;

    readonly List<Bubble> bubbles = new List<Bubble>();
    RectTransform bubbleLayer;

    class Bubble
    {
        public RectTransform rt;
        public RawImage img;
        public float speed, wobble, phase, size;
    }

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Canvas canvas = MenuUI.CreateCanvas("MainMenuCanvas", 0);
        root = (RectTransform)canvas.transform;

        // Fondo
        MenuUI.Panel("Background", root, new Color(0.01f, 0.05f, 0.09f, 1f));
        RawImage glow = MenuUI.Stretch("Glow", root).gameObject.AddComponent<RawImage>();
        glow.texture = FXFactory.MakeRadial(128, d => Mathf.Clamp01(1f - d) * 0.9f, "FX_MenuGlow");
        glow.color = new Color(0.1f, 0.45f, 0.6f, 0.45f);
        glow.raycastTarget = false;

        bubbleLayer = MenuUI.Stretch("Bubbles", root);
        for (int i = 0; i < 40; i++) bubbles.Add(CreateBubble(true));

        // Grupo principal
        mainGroup = MenuUI.Stretch("Main", root);
        titleText = MenuUI.CreateText("Title", mainGroup, title, 78f, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1200f, 110f));
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 10f;
        TextMeshProUGUI sub = MenuUI.CreateText("Subtitle", mainGroup, subtitle, 20f, new Vector2(0.5f, 0.5f), new Vector2(0f, 118f), new Vector2(1000f, 40f));
        sub.characterSpacing = 12f;
        sub.color = new Color(MenuUI.Accent.r, MenuUI.Accent.g, MenuUI.Accent.b, 0.7f);

        Button play = MenuUI.CreateButton("JUGAR", mainGroup, new Vector2(0f, 10f), Play);
        MenuUI.CreateButton("CONTROLES", mainGroup, new Vector2(0f, -60f), () => ShowControls(true));
        MenuUI.CreateButton("SALIR", mainGroup, new Vector2(0f, -130f), Quit);
        play.Select();

        TextMeshProUGUI ver = MenuUI.CreateText("Version", mainGroup, "MENÚ PROVISIONAL  //  PROTOTIPO", 14f, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 30f));
        ver.color = new Color(MenuUI.Accent.r, MenuUI.Accent.g, MenuUI.Accent.b, 0.4f);
        ver.characterSpacing = 8f;

        // Controles
        controlsGroup = MenuUI.Stretch("Controls", root);
        MenuUI.CreateText("ControlsTitle", controlsGroup, "CONTROLES", 44f, new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(800f, 60f)).fontStyle = FontStyles.Bold;
        string controls =
            "<color=#FFFFFF>W / S</color>   Avanzar / retroceder\n" +
            "<color=#FFFFFF>A / D</color>   Girar\n" +
            "<color=#FFFFFF>ESPACIO</color>   Propulsor (en agua: subir y salir disparado)\n" +
            "<color=#FFFFFF>SHIFT</color>   Impulso al nadar\n" +
            "<color=#FFFFFF>CTRL</color>   Bucear más profundo\n" +
            "<color=#FFFFFF>CLIC IZQ.</color>   Disparar (mantener = ráfaga)\n" +
            "<color=#FFFFFF>CLIC DER.</color>   Cargar disparo (soltar al llenarse)\n" +
            "<color=#FFFFFF>Q</color>   Cambiar cámara\n" +
            "<color=#FFFFFF>ESC</color>   Pausa\n\n" +
            "El traje recarga propulsor y munición dentro del agua.";
        TextMeshProUGUI ct = MenuUI.CreateText("ControlsList", controlsGroup, controls, 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 380f));
        ct.lineSpacing = 18f;
        MenuUI.CreateButton("VOLVER", controlsGroup, new Vector2(0f, -250f), () => ShowControls(false));
        controlsGroup.gameObject.SetActive(false);

        // Fundido
        fade = MenuUI.Panel("Fade", root, Color.black);
        fade.raycastTarget = false;
        StartCoroutine(FadeTo(0f, 0.8f));
    }

    Bubble CreateBubble(bool randomY)
    {
        Bubble b = new Bubble();
        b.img = MenuUI.Rect("Bubble", bubbleLayer, new Vector2(0.5f, 0f), Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
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
        b.size = Random.Range(6f, 34f);
        b.speed = Random.Range(25f, 90f) * (0.6f + b.size / 40f);
        b.wobble = Random.Range(5f, 25f);
        b.phase = Random.Range(0f, 10f);
        b.rt.sizeDelta = Vector2.one * b.size;
        b.rt.anchoredPosition = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), randomY ? Random.Range(0f, h) : -40f);
        b.img.color = new Color(0.7f, 0.95f, 1f, Random.Range(0.15f, 0.5f));
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float top = bubbleLayer.rect.height + 40f;
        foreach (Bubble b in bubbles)
        {
            Vector2 p = b.rt.anchoredPosition;
            p.y += b.speed * dt;
            p.x += Mathf.Sin(Time.unscaledTime * 1.5f + b.phase) * b.wobble * dt;
            b.rt.anchoredPosition = p;
            if (p.y > top) ResetBubble(b, false);
        }

        if (titleText != null)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 2f) * 0.5f + 0.5f;
            titleText.color = Color.Lerp(MenuUI.Accent, Color.white, pulse * 0.35f);
        }

        if (!loading && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && mainGroup.gameObject.activeSelf)
        {
            Play();
        }
        if (Input.GetKeyDown(KeyCode.Escape) && controlsGroup.gameObject.activeSelf)
        {
            ShowControls(false);
        }
    }

    void ShowControls(bool show)
    {
        controlsGroup.gameObject.SetActive(show);
        mainGroup.gameObject.SetActive(!show);
        Button first = (show ? controlsGroup : mainGroup).GetComponentInChildren<Button>();
        if (first != null) first.Select();
    }

    void Play()
    {
        if (loading) return;
        loading = true;
        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        yield return FadeTo(1f, 0.6f);
        SceneManager.LoadScene(gameSceneName);
    }

    IEnumerator FadeTo(float target, float duration)
    {
        float start = fade.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, target);
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
