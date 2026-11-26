using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Visor de cómic a PANTALLA COMPLETA. La viñeta ocupa toda la pantalla (con franjas negras si la proporción no
// coincide), entra con un fundido y se acerca muy despacio (como una cámara), y el recuadro de narración va arriba a la
// izquierda del dibujo (esa esquina se deja libre al dibujar). El título, los puntos y los botones van en las franjas.
// Mientras se ve suena la música del cómic (ComicDefinition.music: "Historia" si está vacía, "Amenaza" antes de un jefe,
// "Creditos" en el final); al terminar vuelve la que sonaba antes, salvo que el cómic diga keepMusic.
// Uso: ComicViewer.Show(comic, () => { ...lo que pasa al terminar... });
//  - Clic, Espacio, Enter o flecha derecha: siguiente viñeta.  Flecha izquierda: la anterior.  Esc o "SALTAR": termina.
//  - Una viñeta sin dibujo se muestra en blanco ("Viñeta 2 de 6") para saber dónde va el arte.
//  - Funciona con el juego en pausa (usa tiempo real).
public class ComicViewer : MonoBehaviour
{
    public static bool IsShowing { get; private set; }
    // Cuadro en el que se cerró: quien escuche Esc lo ignora ese cuadro (si no, Esc cerraría también su pantalla)
    public static int ClosedFrame { get; private set; }

    const float DefaultAspect = 1380f / 660f;   // el tamaño de los dibujos de la historia
    const float FadeIn = 0.35f;
    const float CrossFade = 0.35f;
    const float ZoomTime = 12f;
    const float Zoom = 0.045f;

    ComicDefinition comic;
    System.Action onDone;
    int index;
    int lastAdvanceFrame = -1;
    bool finished;
    string previousTrack;
    string comicTrack;

    CanvasGroup group;
    AspectRatioFitter fitter;
    Image artBack, artFront;
    RectTransform placeholder;
    TextMeshProUGUI placeholderText;
    RectTransform captionBox;
    CanvasGroup captionGroup;
    TextMeshProUGUI captionText;
    TextMeshProUGUI nextLabel;
    Image[] dots;
    float shown;        // tiempo desde que entró la viñeta
    float life;         // tiempo desde que se abrió el visor
    Vector2 zoomPivot = new Vector2(0.5f, 0.5f);
    static Vector2 DotSize(bool current) { return Vector2.one * (current ? 14f : 9f); }

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
        v.StartMusic();
        v.Build();
        v.ShowPanel(0);
        return v;
    }

    // ======================= MÚSICA =======================

    void StartMusic()
    {
        previousTrack = Music.Current;
        comicTrack = string.IsNullOrEmpty(comic.music) ? Music.Story : comic.music;
        Music.Play(comicTrack, 1.2f);
    }

    void RestoreMusic()
    {
        if (comic != null && comic.keepMusic) return;
        if (Music.IsLoopTrack(previousTrack))
        {
            if (previousTrack != comicTrack) Music.Play(previousTrack, 1.2f);
        }
        else Music.Stop(1.2f);   // antes sonaba un remate (victoria) o nada: vuelve el silencio
    }

    // ======================= CONSTRUCCIÓN =======================

    void Build()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Canvas canvas = MenuUI.CreateCanvas("ComicCanvas", 500);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // Fondo negro: un clic en cualquier parte pasa a la siguiente viñeta
        Image backdrop = MenuUI.Panel("Fondo", root, new Color(0.01f, 0.015f, 0.03f, 1f));
        backdrop.raycastTarget = true;
        Button backdropButton = backdrop.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(Next);

        // El dibujo: lo más grande que quepa en la pantalla, con su proporción
        RectTransform frame = MenuUI.Stretch("Vineta", root);
        fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = DefaultAspect;
        frame.gameObject.AddComponent<RectMask2D>();

        artBack = ArtLayer("DibujoAnterior", frame);
        artFront = ArtLayer("Dibujo", frame);

        placeholder = MenuUI.Stretch("EnBlanco", frame);
        Image ph = placeholder.gameObject.AddComponent<Image>();
        ph.color = new Color(0.9f, 0.89f, 0.86f, 1f);
        ph.raycastTarget = false;
        placeholderText = MenuUI.Text("Texto", placeholder, "", 56f, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                                      new Vector2(880f, 90f), new Color(0.62f, 0.64f, 0.68f, 1f));
        UseComicFont(placeholderText);
        UseComicFont(MenuUI.Text("Nota", placeholder, "(en blanco: aquí va el dibujo)", 22f, new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -44f), new Vector2(880f, 40f), new Color(0.6f, 0.62f, 0.66f, 1f)));

        // Recuadro de narración: amarillo con contorno, arriba a la izquierda del dibujo (esa esquina está libre)
        // (los dibujos dejan libre arriba a la izquierda: el 61 % del ancho y el 10 % del alto)
        captionBox = InkBox("Narracion", frame, new Vector2(0.012f, 0.884f), new Vector2(0.6f, 0.986f), MenuUI.Yellow);
        captionGroup = captionBox.gameObject.AddComponent<CanvasGroup>();
        captionGroup.blocksRaycasts = false;
        captionText = MenuUI.Text("Texto", captionBox, "", 24f, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, MenuUI.Ink);
        RectTransform ct = captionText.rectTransform;
        ct.anchorMin = Vector2.zero;
        ct.anchorMax = Vector2.one;
        ct.offsetMin = new Vector2(18f, 8f);
        ct.offsetMax = new Vector2(-18f, -8f);
        UseComicFont(captionText);
        captionText.alignment = TextAlignmentOptions.MidlineLeft;
        captionText.enableAutoSizing = true;   // un texto largo se achica en vez de salirse del recuadro
        captionText.fontSizeMin = 13f;
        captionText.fontSizeMax = 24f;

        // Sombras arriba y abajo: el título y los botones se leen aunque la pantalla no deje franjas
        Shade("SombraArriba", root, true);
        Shade("SombraAbajo", root, false);

        // Título del cómic (arriba al centro)
        TextMeshProUGUI title = MenuUI.Text("Titulo", root, string.IsNullOrEmpty(comic.title) ? "HISTORIA" : comic.title.ToUpper(), 24f,
                                            new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 40f), MenuUI.Cream, true);
        title.characterSpacing = 6f;

        // Puntos de avance (abajo al centro)
        int n = comic.panels.Count;
        dots = new Image[n];
        for (int i = 0; i < n; i++)
            dots[i] = MenuUI.Img("Punto" + i, root, MenuUI.Diamond, MenuUI.Accent, new Vector2(0.5f, 0f),
                                 new Vector2((i - (n - 1) * 0.5f) * 22f, 26f), DotSize(false));

        // Ayuda y botones
        TextMeshProUGUI hint = MenuUI.Text("Ayuda", root, "Clic o Espacio: seguir  ·  ←: volver  ·  Esc: saltar", 14f,
                                           new Vector2(0f, 0f), new Vector2(200f, 26f), new Vector2(360f, 28f), MenuUI.TextDim);
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        Button next = MenuUI.ComicButton("SIGUIENTE", root, new Vector2(1f, 0f), new Vector2(-112f, 26f), new Vector2(196f, 40f), MenuUI.Orange, Next, 21f);
        nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        MenuUI.ComicButton("SALTAR", root, new Vector2(1f, 1f), new Vector2(-76f, -26f), new Vector2(124f, 36f), MenuUI.BlueDark, Finish, 17f);
    }

    static Image ArtLayer(string name, RectTransform frame)
    {
        RectTransform rt = MenuUI.Stretch(name, frame);
        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = false;   // el marco ya tiene la proporción del dibujo
        return img;
    }

    static void Shade(string name, RectTransform root, bool top)
    {
        RectTransform rt = MenuUI.Stretch(name, root);
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 64f);
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = VerticalFade();
        img.uvRect = top ? new Rect(0f, 0f, 1f, 1f) : new Rect(0f, 1f, 1f, -1f);
        img.color = new Color(0f, 0f, 0f, 0.55f);
        img.raycastTarget = false;
    }

    static Texture2D fadeTex;
    static Texture2D VerticalFade()
    {
        if (fadeTex != null) return fadeTex;
        fadeTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FX_ComicShade" };
        for (int y = 0; y < 32; y++) fadeTex.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, y / 31f)));
        fadeTex.Apply();
        return fadeTex;
    }

    static void UseComicFont(TextMeshProUGUI t)
    {
        TMP_FontAsset f = MenuUI.ComicFont;
        if (f != null) t.font = f;
    }

    // Recuadro de cómic que se estira con el dibujo: sombra, contorno oscuro y relleno
    static RectTransform InkBox(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color fill)
    {
        RectTransform box = MenuUI.Stretch(name, parent);
        box.anchorMin = anchorMin;
        box.anchorMax = anchorMax;
        box.offsetMin = box.offsetMax = Vector2.zero;
        Layer(box, "Sombra", MenuUI.WithAlpha(MenuUI.Ink, 0.55f), new Vector2(5f, -5f), new Vector2(5f, -5f));
        Layer(box, "Contorno", MenuUI.Ink, Vector2.zero, Vector2.zero);
        Layer(box, "Relleno", fill, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        return box;
    }

    static void Layer(RectTransform box, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = MenuUI.Stretch(name, box);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = MenuUI.RoundedSmall;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
    }

    // ======================= VIÑETAS =======================

    void ShowPanel(int i)
    {
        bool first = shown <= 0f && index == 0 && i == 0;
        index = i;
        ComicPanel p = comic.panels[i];
        bool hasArt = p != null && p.image != null;

        // La viñeta anterior se queda detrás y la nueva entra con un fundido
        artBack.sprite = artFront.sprite;
        artBack.enabled = artFront.enabled && artFront.sprite != null && !first;
        artBack.rectTransform.localScale = artFront.rectTransform.localScale;
        artBack.rectTransform.pivot = artFront.rectTransform.pivot;
        artBack.color = Color.white;
        artFront.enabled = hasArt;
        if (hasArt)
        {
            artFront.sprite = p.image;
            Rect r = p.image.rect;
            if (r.height > 0f) fitter.aspectRatio = r.width / r.height;
        }
        artFront.color = new Color(1f, 1f, 1f, first ? 1f : 0f);
        // El acercamiento lento va hacia un lado distinto en cada viñeta
        zoomPivot = new Vector2(i % 2 == 0 ? 0.62f : 0.38f, i % 3 == 0 ? 0.42f : 0.56f);
        artFront.rectTransform.pivot = zoomPivot;
        artFront.rectTransform.localScale = Vector3.one;

        placeholder.gameObject.SetActive(!hasArt);
        placeholderText.text = "VIÑETA " + (i + 1) + " DE " + comic.panels.Count;

        bool hasCaption = p != null && !string.IsNullOrEmpty(p.caption);
        captionBox.gameObject.SetActive(hasCaption);
        if (hasCaption) captionText.text = p.caption;
        captionGroup.alpha = 0f;

        for (int k = 0; k < dots.Length; k++)
        {
            dots[k].color = k == i ? MenuUI.Orange : MenuUI.WithAlpha(MenuUI.Accent, 0.35f);
            dots[k].rectTransform.sizeDelta = DotSize(k == i);
        }
        nextLabel.text = i >= comic.panels.Count - 1 ? "CONTINUAR" : "SIGUIENTE";
        shown = 0.0001f;

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

    void Previous()
    {
        if (finished || index <= 0 || Time.frameCount == lastAdvanceFrame) return;
        lastAdvanceFrame = Time.frameCount;
        ShowPanel(index - 1);
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        IsShowing = false;
        ClosedFrame = Time.frameCount;
        RestoreMusic();
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
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            Previous();
        else if (Input.GetKeyDown(KeyCode.Escape))
            Finish();

        float dt = Time.unscaledDeltaTime;
        life += dt;
        shown += dt;
        group.alpha = Mathf.Clamp01(life / FadeIn);

        // Fundido entre viñetas y acercamiento lento (como una cámara)
        float a = Mathf.Clamp01(shown / CrossFade);
        artFront.color = new Color(1f, 1f, 1f, Mathf.Max(artFront.color.a, a * a * (3f - 2f * a)));
        if (a >= 1f) artBack.enabled = false;
        float z = 1f + Zoom * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shown / ZoomTime));
        artFront.rectTransform.localScale = new Vector3(z, z, 1f);

        // La narración aparece un instante después del dibujo
        float c = Mathf.Clamp01((shown - 0.18f) / 0.25f);
        captionGroup.alpha = c;
        float s = 0.94f + 0.06f * (1f - (1f - c) * (1f - c));
        captionBox.localScale = new Vector3(s, s, 1f);
    }

    void OnDestroy()
    {
        if (!finished) IsShowing = false;
    }
}
