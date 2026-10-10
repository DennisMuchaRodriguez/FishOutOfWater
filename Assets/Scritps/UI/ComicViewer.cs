using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Visor de cómic a pantalla completa. La viñeta es papel de cómic (el arte de la historia);
// el marco, los botones y los puntos usan el estilo holográfico del casco.
// Uso: ComicViewer.Show(comic, () => { ...lo que pasa al terminar... });
//  - Clic, Espacio, Enter o flecha derecha: siguiente viñeta.  Esc o "SALTAR": termina.
//  - Una viñeta sin dibujo se muestra en blanco ("Viñeta 2 de 6") para saber dónde va el arte.
//  - Funciona con el juego en pausa (usa tiempo real).
public class ComicViewer : MonoBehaviour
{
    public static bool IsShowing { get; private set; }
    // Cuadro en el que se cerró: quien escuche Esc lo ignora ese cuadro (si no, Esc cerraría también su pantalla)
    public static int ClosedFrame { get; private set; }

    ComicDefinition comic;
    System.Action onDone;
    int index;
    int lastAdvanceFrame = -1;
    bool finished;
    float pop;

    RectTransform panelRoot;
    Image art;
    RectTransform placeholder;
    TextMeshProUGUI placeholderText;
    RectTransform captionBox;
    TextMeshProUGUI captionText;
    TextMeshProUGUI nextLabel;
    Image[] dots;
    static Vector2 DotSize(bool current) { return Vector2.one * (current ? 18f : 12f); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsShowing = false; ClosedFrame = -1; }

    public static ComicViewer Show(ComicDefinition comic, System.Action onDone)
    {
        if (comic == null || comic.panels == null || comic.panels.Count == 0)
        {
            if (onDone != null) onDone();
            return null;
        }
        GameObject go = new GameObject("VisorComic");
        ComicViewer v = go.AddComponent<ComicViewer>();
        v.comic = comic;
        v.onDone = onDone;
        IsShowing = true;
        v.Build();
        v.ShowPanel(0);
        return v;
    }

    void Build()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Canvas canvas = MenuUI.CreateCanvas("ComicCanvas", 500);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;

        // Fondo oscuro: un clic en cualquier parte pasa a la siguiente viñeta
        Image backdrop = MenuUI.Panel("Fondo", root, new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, 0.96f));
        backdrop.raycastTarget = true;
        Button backdropButton = backdrop.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(Next);
        RawImage glow = MenuUI.Stretch("Brillo", root).gameObject.AddComponent<RawImage>();
        glow.texture = FXFactory.MakeRadial(128, d => Mathf.Clamp01(1f - d) * 0.8f, "FX_ComicGlow");
        glow.color = new Color(0.2f, 0.5f, 0.7f, 0.3f);
        glow.raycastTarget = false;
        Image scan = MenuUI.Stretch("Escaneo", root).gameObject.AddComponent<Image>();
        scan.sprite = MenuUI.ScanSprite;
        scan.type = Image.Type.Tiled;
        scan.color = MenuUI.WithAlpha(MenuUI.Accent, 0.04f);
        scan.raycastTarget = false;

        // Título del cómic
        MenuUI.Title("Titulo", root, string.IsNullOrEmpty(comic.title) ? "HISTORIA" : comic.title.ToUpper(), 48f,
                     new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(1000f, 70f), MenuUI.Cream);

        // Viñeta
        panelRoot = MenuUI.ComicPanel("Vineta", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(960f, 480f), MenuUI.Paper);

        placeholder = MenuUI.Rect("EnBlanco", panelRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 440f));
        Image ph = placeholder.gameObject.AddComponent<Image>();
        ph.sprite = MenuUI.Rounded;
        ph.type = Image.Type.Sliced;
        ph.color = new Color(0.9f, 0.89f, 0.86f, 1f);
        ph.raycastTarget = false;
        placeholderText = MenuUI.Text("Texto", placeholder, "", 56f, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                                      new Vector2(880f, 90f), new Color(0.62f, 0.64f, 0.68f, 1f));
        UseComicFont(placeholderText);
        UseComicFont(MenuUI.Text("Nota", placeholder, "(en blanco: aquí va el dibujo)", 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, -44f),
                                 new Vector2(880f, 40f), new Color(0.6f, 0.62f, 0.66f, 1f)));

        art = MenuUI.Img("Dibujo", panelRoot, null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 440f));
        art.preserveAspect = true;

        // Recuadro de narración (amarillo con contorno, como en un cómic; arriba a la izquierda)
        captionBox = InkBox("Narracion", panelRoot, new Vector2(0f, 1f), new Vector2(300f, -22f), new Vector2(560f, 86f), MenuUI.Yellow);
        captionText = MenuUI.Text("Texto", captionBox, "", 22f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 76f), MenuUI.Ink);
        UseComicFont(captionText);
        captionText.alignment = TextAlignmentOptions.MidlineLeft;

        // Puntos de avance
        int n = comic.panels.Count;
        dots = new Image[n];
        for (int i = 0; i < n; i++)
            dots[i] = MenuUI.Img("Punto" + i, root, MenuUI.Diamond, MenuUI.Accent, new Vector2(0.5f, 0f),
                                 new Vector2((i - (n - 1) * 0.5f) * 26f, 52f), DotSize(false));

        // Botones
        Button next = MenuUI.ComicButton("SIGUIENTE", root, new Vector2(1f, 0f), new Vector2(-150f, 56f), new Vector2(230f, 58f), MenuUI.Orange, Next, 26f);
        nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        MenuUI.ComicButton("SALTAR", root, new Vector2(1f, 1f), new Vector2(-104f, -46f), new Vector2(150f, 46f), MenuUI.BlueDark, Finish, 20f);
    }

    static void UseComicFont(TextMeshProUGUI t)
    {
        TMP_FontAsset f = MenuUI.ComicFont;
        if (f != null) t.font = f;
    }

    // Recuadro de cómic: sombra, contorno oscuro y relleno (solo para la narración, que es parte del dibujo)
    static RectTransform InkBox(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
    {
        RectTransform box = MenuUI.Rect(name, parent, anchor, pos, size);
        Image shadow = MenuUI.Img("Sombra", box, MenuUI.RoundedSmall, MenuUI.WithAlpha(MenuUI.Ink, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(5f, -5f), size);
        shadow.raycastTarget = false;
        MenuUI.Img("Contorno", box, MenuUI.RoundedSmall, MenuUI.Ink, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        MenuUI.Img("Relleno", box, MenuUI.RoundedSmall, fill, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(8f, 8f));
        return box;
    }

    void ShowPanel(int i)
    {
        index = i;
        ComicPanel p = comic.panels[i];
        bool hasArt = p != null && p.image != null;
        art.gameObject.SetActive(hasArt);
        placeholder.gameObject.SetActive(!hasArt);
        if (hasArt) art.sprite = p.image;
        placeholderText.text = "VIÑETA " + (i + 1) + " DE " + comic.panels.Count;

        bool hasCaption = p != null && !string.IsNullOrEmpty(p.caption);
        captionBox.gameObject.SetActive(hasCaption);
        if (hasCaption) captionText.text = p.caption;

        for (int k = 0; k < dots.Length; k++)
        {
            dots[k].color = k == i ? MenuUI.Orange : MenuUI.WithAlpha(MenuUI.Accent, 0.35f);
            dots[k].rectTransform.sizeDelta = DotSize(k == i);
        }
        nextLabel.text = i >= comic.panels.Count - 1 ? "CONTINUAR" : "SIGUIENTE";

        pop = 1f;

        // Sin botón seleccionado: Espacio/Enter los maneja Update (si no, avanzaría dos veces)
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void Next()
    {
        // Una sola viñeta por cuadro (Enter puede llegar a la vez por el teclado y por un botón seleccionado)
        if (finished || Time.frameCount == lastAdvanceFrame) return;
        lastAdvanceFrame = Time.frameCount;
        if (index + 1 < comic.panels.Count) ShowPanel(index + 1);
        else Finish();
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        IsShowing = false;
        ClosedFrame = Time.frameCount;
        System.Action done = onDone;
        onDone = null;
        Destroy(gameObject);
        if (done != null) done();
    }

    void Update()
    {
        if (finished) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.RightArrow))
            Next();
        else if (Input.GetKeyDown(KeyCode.Escape))
            Finish();

        // Entrada de cada viñeta: crece con un pequeño rebote
        float dt = Time.unscaledDeltaTime;
        pop = Mathf.MoveTowards(pop, 0f, dt * 3.2f);
        float e = pop * pop;
        float scale = 1f - 0.12f * e + Mathf.Sin((1f - pop) * Mathf.PI) * 0.03f * pop;
        panelRoot.localScale = new Vector3(scale, scale, 1f);
    }

    void OnDestroy()
    {
        if (!finished) IsShowing = false;
    }
}
