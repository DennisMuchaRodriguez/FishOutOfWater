using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pantalla COOPERATIVO del menú principal (parte de MainMenu):
//  - TU NOMBRE (lo ven los demás en la sala).
//  - CREAR SALA: EN LÍNEA (por internet, con los servicios de Unity) o RED LOCAL (misma Wi-Fi).
//  - UNIRSE: escribir el código que te pasó el anfitrión (sirve para los dos tipos de sala).
// Al crear o unirse, se abre la sala de espera en el Taller (CoopLobby). Si algo falla, el mensaje aparece abajo.
public partial class MainMenu
{
    CanvasGroup coopScreen;
    TMP_InputField coopName, coopCode;
    TextMeshProUGUI coopStatus;
    Button[] coopButtons;

    CanvasGroup BuildCoopScreen()
    {
        RectTransform s = MenuUI.Stretch("Cooperativo", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();
        Vector2 top = new Vector2(0.5f, 1f), center = new Vector2(0.5f, 0.5f);

        MenuUI.Title("Encabezado", s, "COOPERATIVO", 72f, top, new Vector2(0f, -80f), new Vector2(900f, 96f), MenuUI.Cream);
        MenuUI.Rule(s, top, new Vector2(0f, -134f), 520f, MenuUI.Purple);
        MenuUI.Text("Sub", s, "Hasta 4 peces contra las oleadas sin fin  ·  Taller cada 5 oleadas", 20f, top, new Vector2(0f, -164f),
                    new Vector2(1000f, 30f), MenuUI.TextDim);

        // Nombre
        MenuUI.Text("EtiquetaNombre", s, "TU NOMBRE", 16f, center, new Vector2(0f, 168f), new Vector2(400f, 22f), MenuUI.TextDim).characterSpacing = 4f;
        coopName = MakeInput(s, center, new Vector2(0f, 128f), new Vector2(400f, 52f), "Escribe tu nombre", 14, false);
        coopName.text = CoopSession.LocalName;

        // Crear
        RectTransform create = MenuUI.HoloPanel("Crear", s, center, new Vector2(-260f, -26f), new Vector2(460f, 220f),
                                                MenuUI.WithAlpha(MenuUI.Ink, 0.82f), MenuUI.WithAlpha(MenuUI.Orange, 0.8f),
                                                MenuUI.WithAlpha(MenuUI.Orange, 0.2f), MenuUI.WithAlpha(MenuUI.Orange, 0.25f), 0.05f);
        MenuUI.Title("TituloCrear", create, "CREAR SALA", 32f, top, new Vector2(0f, -30f), new Vector2(420f, 44f), MenuUI.Orange);
        Button online = MenuUI.ComicButton("EN LÍNEA", create, center, new Vector2(0f, 0f), new Vector2(360f, 56f), MenuUI.Orange, CreateOnline, 26f);
        Button lan = MenuUI.ComicButton("RED LOCAL", create, center, new Vector2(0f, -66f), new Vector2(360f, 48f), MenuUI.Blue, CreateLan, 22f);
        MenuUI.Text("AyudaCrear", create, "Te da un código para tus amigos", 15f, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(420f, 22f), MenuUI.TextDim);

        // Unirse
        RectTransform join = MenuUI.HoloPanel("Unirse", s, center, new Vector2(260f, -26f), new Vector2(460f, 220f),
                                              MenuUI.WithAlpha(MenuUI.Ink, 0.82f), MenuUI.WithAlpha(MenuUI.Purple, 0.8f),
                                              MenuUI.WithAlpha(MenuUI.Purple, 0.2f), MenuUI.WithAlpha(MenuUI.Purple, 0.25f), 0.05f);
        MenuUI.Title("TituloUnirse", join, "UNIRSE", 32f, top, new Vector2(0f, -30f), new Vector2(420f, 44f), MenuUI.Purple);
        coopCode = MakeInput(join, center, new Vector2(0f, 4f), new Vector2(360f, 56f), "CÓDIGO DE SALA", 10, true);
        Button go = MenuUI.ComicButton("UNIRSE", join, center, new Vector2(0f, -62f), new Vector2(360f, 50f), MenuUI.Purple, JoinRoom, 24f);
        MenuUI.Text("AyudaUnirse", join, "El código que te pasó el anfitrión", 15f, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(420f, 22f), MenuUI.TextDim);

        coopStatus = MenuUI.Text("Estado", s, "", 19f, center, new Vector2(0f, -172f), new Vector2(1100f, 56f), MenuUI.Yellow);
        coopStatus.textWrappingMode = TextWrappingModes.Normal;

        Button back = MenuUI.ComicButton("VOLVER", s, new Vector2(0f, 0f), new Vector2(130f, 52f), new Vector2(200f, 52f), MenuUI.BlueDark, () => Go(modesScreen), 24f);
        coopButtons = new[] { online, lan, go, back };

        CoopSession.StatusChanged -= OnCoopStatus;
        CoopSession.StatusChanged += OnCoopStatus;
        return g;
    }

    void OnDestroy()
    {
        CoopSession.StatusChanged -= OnCoopStatus;
    }

    // Campo de texto con el estilo del casco
    TMP_InputField MakeInput(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string placeholder, int limit, bool upper)
    {
        RectTransform box = MenuUI.Rect("Campo", parent, anchor, pos, size);
        Image bg = box.gameObject.AddComponent<Image>();
        bg.sprite = MenuUI.RoundedSmall;
        bg.type = Image.Type.Sliced;
        bg.color = MenuUI.WithAlpha(MenuUI.AccentDeep, 0.35f);
        Image edge = MenuUI.Stretch("Borde", box).gameObject.AddComponent<Image>();
        edge.sprite = MenuUI.RoundedSmall;
        edge.type = Image.Type.Sliced;
        edge.fillCenter = false;
        edge.color = MenuUI.WithAlpha(MenuUI.Accent, 0.8f);
        edge.raycastTarget = false;

        RectTransform area = MenuUI.Stretch("Area", box);
        area.offsetMin = new Vector2(16f, 6f);
        area.offsetMax = new Vector2(-16f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI ph = MenuUI.Stretch("Placeholder", area).gameObject.AddComponent<TextMeshProUGUI>();
        ph.font = MenuUI.BodyFont;
        ph.text = placeholder;
        ph.fontSize = 24f;
        ph.fontStyle = FontStyles.Italic;
        ph.color = MenuUI.WithAlpha(MenuUI.TextDim, 0.7f);
        ph.alignment = TextAlignmentOptions.Center;
        ph.raycastTarget = false;

        TextMeshProUGUI txt = MenuUI.Stretch("Texto", area).gameObject.AddComponent<TextMeshProUGUI>();
        txt.font = MenuUI.BodyFont;
        txt.fontSize = upper ? 30f : 26f;
        txt.color = MenuUI.Cream;
        txt.alignment = TextAlignmentOptions.Center;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        if (upper) txt.characterSpacing = 8f;

        TMP_InputField field = box.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = txt;
        field.placeholder = ph;
        field.targetGraphic = bg;
        field.characterLimit = limit;
        field.lineType = TMP_InputField.LineType.SingleLine;
        if (upper) field.onValidateInput += (text, index, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
        return field;
    }

    string CoopPlayerName()
    {
        string n = coopName != null ? coopName.text.Trim() : "";
        if (n == "") n = CoopSession.LocalName;
        CoopSession.LocalName = n;
        return n;
    }

    void CreateOnline()
    {
        if (CoopSession.IsConnecting) return;
        CoopSession.CreateOnline(CoopPlayerName());
    }

    void CreateLan()
    {
        if (CoopSession.IsConnecting) return;
        CoopSession.CreateLan(CoopPlayerName());
    }

    void JoinRoom()
    {
        if (CoopSession.IsConnecting) return;
        string code = coopCode != null ? coopCode.text : "";
        if (string.IsNullOrWhiteSpace(code))
        {
            OnCoopStatus("Escribe el código de la sala.");
            return;
        }
        CoopSession.Join(code, CoopPlayerName());
    }

    void OnCoopStatus(string text)
    {
        if (coopStatus != null) coopStatus.text = text ?? "";
    }

    // Botones apagados mientras se conecta
    void UpdateCoopScreen()
    {
        if (coopButtons == null || current != coopScreen) return;
        bool busyNet = CoopSession.IsConnecting;
        foreach (Button b in coopButtons) if (b != null) b.interactable = !busyNet;
    }
}
