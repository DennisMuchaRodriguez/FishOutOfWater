using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Visor de cómic a pantalla completa (estilo viñetas).
// Uso: ComicViewer.Show(comic, () => { ...lo que pasa al terminar... });
//  - Clic, Espacio, Enter o flecha derecha: siguiente viñeta.  Esc o "SALTAR": termina.
//  - Una viñeta sin dibujo se muestra en blanco ("Viñeta 2 de 6") para saber dónde va el arte.
//  - Funciona con el juego en pausa (usa tiempo real).
public class ComicViewer : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    ComicDefinition comic;
    System.Action onDone;
    int index;
    bool finished;
    float pop;
    float tilt;

    RectTransform panelRoot;
    Image art;
    RectTransform placeholder;
    TextMeshProUGUI placeholderText;
    RectTransform captionBox;
    TextMeshProUGUI captionText;
    TextMeshProUGUI nextLabel;
    Image[] dots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsShowing = false; }

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
        glow.color = new Color(0.2f, 0.5f, 0.7f, 0.35f);
        glow.raycastTarget = false;

        // Título del cómic
        MenuUI.Title("Titulo", root, string.IsNullOrEmpty(comic.title) ? "HISTORIA" : comic.title.ToUpper(), 48f,
                     new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(1000f, 70f), MenuUI.Yellow);

        // Viñeta
        panelRoot = MenuUI.ComicPanel("Vineta", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(960f, 480f), MenuUI.Cream, 7f, 12f);

        placeholder = MenuUI.Rect("EnBlanco", panelRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 440f));
        Image ph = placeholder.gameObject.AddComponent<Image>();
        ph.sprite = MenuUI.Rounded;
        ph.type = Image.Type.Sliced;
        ph.color = new Color(0.9f, 0.89f, 0.86f, 1f);
        ph.raycastTarget = false;
        placeholderText = MenuUI.Text("Texto", placeholder, "", 56f, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                                      new Vector2(880f, 90f), new Color(0.62f, 0.64f, 0.68f, 1f), true);
        MenuUI.Text("Nota", placeholder, "(en blanco: aquí va el dibujo)", 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, -44f),
                    new Vector2(880f, 40f), new Color(0.6f, 0.62f, 0.66f, 1f));

        art = MenuUI.Img("Dibujo", panelRoot, null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 440f));
        art.preserveAspect = true;

        // Recuadro de narración (amarillo, arriba a la izquierda)
        captionBox = MenuUI.ComicPanel("Narracion", panelRoot, new Vector2(0f, 1f), new Vector2(300f, -22f), new Vector2(560f, 86f), MenuUI.Yellow, 4f, 5f);
        captionText = MenuUI.Text("Texto", captionBox, "", 22f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 76f), MenuUI.Ink);
        captionText.alignment = TextAlignmentOptions.MidlineLeft;

        // Puntos de avance
        int n = comic.panels.Count;
        dots = new Image[n];
        for (int i = 0; i < n; i++)
            dots[i] = MenuUI.Img("Punto" + i, root, MenuUI.Circle, MenuUI.Cream, new Vector2(0.5f, 0f),
                                 new Vector2((i - (n - 1) * 0.5f) * 26f, 52f), new Vector2(14f, 14f));

        // Botones
        Button next = MenuUI.ComicButton("SIGUIENTE", root, new Vector2(1f, 0f), new Vector2(-150f, 56f), new Vector2(230f, 58f), MenuUI.Orange, Next, 26f);
        nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        MenuUI.ComicButton("SALTAR", root, new Vector2(1f, 1f), new Vector2(-104f, -46f), new Vector2(150f, 46f), MenuUI.Blue, Finish, 20f);
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
            dots[k].color = k == i ? MenuUI.Yellow : new Color(MenuUI.Cream.r, MenuUI.Cream.g, MenuUI.Cream.b, 0.35f);
            dots[k].rectTransform.sizeDelta = Vector2.one * (k == i ? 18f : 12f);
        }
        nextLabel.text = i >= comic.panels.Count - 1 ? "CONTINUAR" : "SIGUIENTE";

        pop = 1f;
        tilt = Random.Range(-1.6f, 1.6f);

        // Sin botón seleccionado: Espacio/Enter los maneja Update (si no, avanzaría dos veces)
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void Next()
    {
        if (finished) return;
        if (index + 1 < comic.panels.Count) ShowPanel(index + 1);
        else Finish();
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        IsShowing = false;
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

        // Entrada de cada viñeta: crece con rebote y se ladea un poco
        float dt = Time.unscaledDeltaTime;
        pop = Mathf.MoveTowards(pop, 0f, dt * 3.2f);
        float e = pop * pop;
        float scale = 1f - 0.12f * e + Mathf.Sin((1f - pop) * Mathf.PI) * 0.03f * pop;
        panelRoot.localScale = new Vector3(scale, scale, 1f);
        panelRoot.localRotation = Quaternion.Euler(0f, 0f, tilt * (0.4f + e));
    }

    void OnDestroy()
    {
        if (!finished) IsShowing = false;
    }
}
