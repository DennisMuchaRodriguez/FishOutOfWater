using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Pantalla de carga entre escenas (jugar un nivel, repetir, el siguiente nivel, volver al mapa o al menú, ir al
// Taller). Así nunca se ve la escena "a medio armar" ni el tirón de los primeros cuadros:
//  1. Un fundido tapa la pantalla.
//  2. La escena nueva se carga en segundo plano (la barra avanza).
//  3. Al entrar, se queda tapada hasta que la escena terminó de armarse (vegetación del lago, peces, aves, casco...):
//     espera a que los cuadros vuelvan a ser rápidos. Mientras tanto el juego está en pausa (no corre la cuenta atrás).
//  4. Se destapa con un fundido.
// La crea sola GameSession / WorkshopFlow; no hace falta ponerla en ninguna escena.
public class LoadingScreen : MonoBehaviour
{
    static LoadingScreen instance;

    // True mientras la pantalla de carga tapa el juego
    public static bool IsLoading { get { return instance != null && instance.busy; } }

    [Tooltip("Segundos mínimos que se ve la pantalla (para que no parpadee)")]
    public float minimumTime = 0.8f;
    [Tooltip("Tiempo máximo esperando a que la escena se estabilice")]
    public float maxSettleTime = 8f;

    static readonly string[] Tips =
    {
        "El agua recarga el propulsor del traje. Sin agua, arrástrate con ESPACIO.",
        "Pulsa E para el giro de esquive: con A o D eliges el lado.",
        "La metralleta de burbujas se recarga sola dentro del agua.",
        "Si un ave atrapa un pez, dispárale: lo soltará.",
        "Los peces defensores disparan a las aves, pero su traje no aguanta para siempre.",
        "Con la R cambias entre primera y tercera persona. Con la F lanzas los misiles.",
        "Las cápsulas de munición flotan en el lago (y algunas están bajo el agua).",
        "Cuidado con los choques a toda velocidad: el traje pierde armadura."
    };

    bool busy;
    Canvas canvas;
    CanvasGroup group;
    RectTransform barFill;
    RawImage ring;
    TextMeshProUGUI percent, tip;
    float progress, shown;

    public static void Load(string sceneName)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PantallaDeCarga");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadingScreen>();
            instance.Build();
        }
        if (instance.busy) return;
        instance.StartCoroutine(instance.Run(sceneName));
    }

    // Por si el proyecto entra a Play sin recargar el dominio
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    void Build()
    {
        canvas = MenuUI.CreateCanvas("CanvasCarga", 5000);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        MenuUI.Panel("Fondo", root, MenuUI.Ink);

        MenuUI.Title("Titulo", root, "CARGANDO", 54f, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(700f, 70f), MenuUI.Cream);

        // Aro que gira (como el del casco)
        RectTransform ringRt = MenuUI.Rect("Aro", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(64f, 64f));
        ring = ringRt.gameObject.AddComponent<RawImage>();
        ring.texture = FXFactory.Ring;
        ring.color = MenuUI.Accent;
        ring.raycastTarget = false;

        // Barra de progreso
        RectTransform bar = MenuUI.Rect("Barra", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(460f, 8f));
        RawImage bg = bar.gameObject.AddComponent<RawImage>();
        bg.texture = FXFactory.White;
        bg.color = new Color(MenuUI.Accent.r, MenuUI.Accent.g, MenuUI.Accent.b, 0.18f);
        bg.raycastTarget = false;
        barFill = MenuUI.Rect("Relleno", bar, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
        barFill.pivot = new Vector2(0f, 0.5f);
        RawImage fill = barFill.gameObject.AddComponent<RawImage>();
        fill.texture = FXFactory.White;
        fill.color = MenuUI.Accent;
        fill.raycastTarget = false;

        percent = MenuUI.Text("Porcentaje", root, "0%", 16f, new Vector2(0.5f, 0.5f), new Vector2(0f, -94f), new Vector2(200f, 24f), MenuUI.TextDim);
        tip = MenuUI.Text("Consejo", root, "", 18f, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1000f, 60f), MenuUI.Cream);
        canvas.enabled = false;
    }

    IEnumerator Run(string sceneName)
    {
        busy = true;
        canvas.enabled = true;
        group.blocksRaycasts = true;
        tip.text = Tips[Random.Range(0, Tips.Length)];
        progress = 0f;
        shown = 0f;
        float start = Time.unscaledTime;

        yield return Fade(1f, 0.25f);

        // Carga en segundo plano (hasta el 70% de la barra)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("Pantalla de carga: no se encontró la escena " + sceneName);
            yield return Fade(0f, 0.25f);
            Finish();
            yield break;
        }
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            progress = op.progress / 0.9f * 0.7f;
            yield return null;
        }
        progress = 0.7f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // La escena ya está: el juego en pausa mientras se arma (los primeros cuadros son lentos)
        Time.timeScale = 0f;
        float settleStart = Time.unscaledTime;
        int fastFrames = 0;
        int frames = 0;
        while (Time.unscaledTime - settleStart < maxSettleTime)
        {
            yield return null;
            frames++;
            // Algunas escenas vuelven a poner la escala de tiempo al empezar: se mantiene en pausa hasta el final
            Time.timeScale = 0f;
            fastFrames = Time.unscaledDeltaTime < 1f / 25f ? fastFrames + 1 : 0;
            progress = Mathf.Max(progress, 0.7f + 0.3f * Mathf.Clamp01(fastFrames / 8f));
            if (frames >= 3 && fastFrames >= 8 && Time.unscaledTime - start >= minimumTime) break;
        }
        progress = 1f;
        yield return new WaitForSecondsRealtime(0.15f);

        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
        yield return Fade(0f, 0.4f);
        Finish();
    }

    void Finish()
    {
        busy = false;
        group.blocksRaycasts = false;
        canvas.enabled = false;
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    IEnumerator Fade(float target, float duration)
    {
        float from = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }

    void Update()
    {
        if (!busy) return;
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, progress, dt * 1.5f);
        if (barFill != null) barFill.sizeDelta = new Vector2(460f * shown, 8f);
        if (percent != null) percent.text = Mathf.RoundToInt(shown * 100f) + "%";
        if (ring != null)
        {
            ring.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 220f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            ring.color = new Color(MenuUI.Accent.r, MenuUI.Accent.g, MenuUI.Accent.b, pulse);
        }
    }
}
