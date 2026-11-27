using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
//  SALA DE ESPERA DEL COOPERATIVO (en el Taller, como los lobbys de los juegos en línea)
//  Los 4 jugadores aparecen en fila delante de la cámara, cada uno sobre su pedestal con su color, su nombre y si
//  está LISTO. Las casillas vacías muestran un holograma "ESPERANDO JUGADOR". Arriba, el CÓDIGO DE SALA para pasarlo
//  a los amigos (botón COPIAR). El anfitrión empieza cuando todos están listos; los demás marcan LISTO.
//  La crea WorkshopScene cuando se abre el Taller como sala (WorkshopFlow.IsCoopLobby).
// ============================================================================
public class CoopLobby : MonoBehaviour
{
    static readonly Vector3[] PadPositions =
    {
        new Vector3(-2.7f, 0f, -1.4f),
        new Vector3(-0.9f, 0f, -1.8f),
        new Vector3(0.9f, 0f, -1.8f),
        new Vector3(2.7f, 0f, -1.4f)
    };
    static readonly Vector3 CameraPos = new Vector3(0f, 1.45f, -7.2f);
    static readonly Vector3 CameraLook = new Vector3(0f, 1.05f, -1.4f);

    WorkshopScene workshop;
    readonly Transform[] pads = new Transform[4];
    readonly Transform[] spins = new Transform[4];
    readonly GameObject[] suits = new GameObject[4];
    readonly TextMeshPro[] names = new TextMeshPro[4];
    readonly TextMeshPro[] tags = new TextMeshPro[4];
    readonly Light[] lights = new Light[4];
    readonly Transform[] rings = new Transform[4];
    readonly GameObject[] waiting = new GameObject[4];
    readonly float[] appear = new float[4];
    readonly bool[] shown = new bool[4];

    TextMeshProUGUI codeText, modeText, countText, statusText;
    Button startButton, readyButton;
    TextMeshProUGUI readyLabel, startLabel;
    float codeFlash;

    public void Build(WorkshopScene scene)
    {
        workshop = scene;
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = 46f;
            cam.transform.position = CameraPos;
            cam.transform.LookAt(CameraLook);
        }

        Material metal = WorkshopKit.Lit(new Color(0.2f, 0.25f, 0.3f), null, 0.6f, "Sala_Pedestal");
        for (int i = 0; i < 4; i++)
        {
            Color c = CoopSession.SlotColors[i];
            Transform pad = new GameObject("Pedestal" + (i + 1)).transform;
            pad.position = PadPositions[i];
            Vector3 toCam = CameraPos - PadPositions[i];
            toCam.y = 0f;
            pad.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            pads[i] = pad;

            WorkshopKit.Shape(PrimitiveType.Cylinder, "Base", pad, new Vector3(0f, 0.1f, 0f), new Vector3(1.25f, 0.1f, 1.25f), metal);
            Material ringMat = WorkshopKit.Glow(workshop.fxMaterialTemplate, FXFactory.White, new Color(c.r, c.g, c.b, 0.85f));
            GameObject ring = WorkshopKit.MeshObject("Anillo", pad, WorkshopKit.RingMesh(0.55f, 0.66f, 48), ringMat);
            ring.transform.localPosition = Vector3.up * 0.21f;
            rings[i] = ring.transform;

            Light l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(pad, false);
            l.transform.localPosition = new Vector3(0f, 2.6f, -0.6f);
            l.type = LightType.Point;
            l.color = c;
            l.range = 4.5f;
            l.intensity = 0.6f;
            lights[i] = l;

            Transform spin = new GameObject("Traje").transform;
            spin.SetParent(pad, false);
            spin.localPosition = Vector3.up * 0.2f;
            spins[i] = spin;

            names[i] = CoopVisuals.NameTag(pad, Vector3.up * 2.25f, "", c, 4.2f);
            tags[i] = CoopVisuals.NameTag(pad, Vector3.up * 1.98f, "", MenuUI.TextDim, 2.6f);

            // Casilla vacía: holograma que late
            GameObject holo = new GameObject("Esperando");
            holo.transform.SetParent(pad, false);
            TextMeshPro t = CoopVisuals.NameTag(holo.transform, Vector3.up * 1.0f, "ESPERANDO\nJUGADOR", new Color(c.r, c.g, c.b, 0.55f), 2.8f);
            t.fontStyle = FontStyles.Italic;
            waiting[i] = holo;
        }

        BuildUI();
        CoopSession.RosterChanged += Refresh;
        CoopSession.StatusChanged += OnStatus;
        Refresh();
        OnStatus(CoopSession.LastStatus);
    }

    void OnDestroy()
    {
        CoopSession.RosterChanged -= Refresh;
        CoopSession.StatusChanged -= OnStatus;
    }

    // ======================= INTERFAZ =======================

    void BuildUI()
    {
        Canvas canvas = MenuUI.CreateCanvas("SalaCanvas", 10);
        RectTransform ui = (RectTransform)canvas.transform;
        MenuUI.VisorOverlay(ui);
        Vector2 top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);

        MenuUI.Title("Titulo", ui, "SALA COOPERATIVA", 54f, top, new Vector2(0f, -46f), new Vector2(900f, 70f), MenuUI.Orange);

        // Código de la sala
        RectTransform box = MenuUI.HoloPanel("Codigo", ui, top, new Vector2(0f, -128f), new Vector2(520f, 78f),
                                             MenuUI.WithAlpha(MenuUI.Ink, 0.85f), MenuUI.WithAlpha(MenuUI.Yellow, 0.9f),
                                             MenuUI.WithAlpha(MenuUI.Yellow, 0.25f), MenuUI.WithAlpha(MenuUI.Yellow, 0.3f), 0.05f, false, true);
        MenuUI.Text("Etiqueta", box, "CÓDIGO DE SALA", 15f, new Vector2(0.5f, 1f), new Vector2(-70f, -16f), new Vector2(260f, 20f), MenuUI.TextDim)
            .characterSpacing = 4f;
        codeText = MenuUI.Text("Valor", box, "------", 40f, new Vector2(0.5f, 0.5f), new Vector2(-70f, -8f), new Vector2(330f, 50f), MenuUI.Yellow, true);
        codeText.characterSpacing = 12f;
        MenuUI.ComicButton("COPIAR", box, new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(130f, 48f), MenuUI.Blue, CopyCode, 20f);
        modeText = MenuUI.Text("Modo", ui, "", 16f, top, new Vector2(0f, -180f), new Vector2(900f, 24f), MenuUI.TextDim);
        modeText.characterSpacing = 2f;

        countText = MenuUI.Text("Jugadores", ui, "", 20f, bottom, new Vector2(0f, 146f), new Vector2(900f, 28f), MenuUI.Cream);
        statusText = MenuUI.Text("Estado", ui, "", 17f, bottom, new Vector2(0f, 116f), new Vector2(1100f, 26f), MenuUI.Accent);

        MenuUI.ComicButton("SALIR", ui, new Vector2(0f, 0f), new Vector2(130f, 46f), new Vector2(200f, 52f), MenuUI.BlueDark, Leave, 22f);
        startButton = MenuUI.ComicButton("EMPEZAR", ui, bottom, new Vector2(0f, 52f), new Vector2(380f, 64f), MenuUI.Orange, StartGame, 30f);
        startLabel = startButton.GetComponentInChildren<TextMeshProUGUI>();
        readyButton = MenuUI.ComicButton("LISTO", ui, bottom, new Vector2(0f, 52f), new Vector2(380f, 64f), MenuUI.Green, ToggleReady, 30f);
        readyLabel = readyButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    void CopyCode()
    {
        string code = CoopSession.RoomCode;
        if (string.IsNullOrEmpty(code)) return;
        GUIUtility.systemCopyBuffer = code;
        codeFlash = 1f;
        OnStatus("Código copiado: pégalo en el chat con tus amigos.");
        Sfx.Play2D(Sonido.UIClick, 0.7f);
    }

    void StartGame()
    {
        if (!CoopSession.CanStart) return;
        Sfx.Play2D(Sonido.Instalar, 0.8f);
        CoopSession.HostStartGame();
    }

    void ToggleReady()
    {
        if (!CoopSession.Active || CoopSession.IsHost) return;
        CoopSession.SetReady(!CoopSession.LocalReady);
        Sfx.Play2D(Sonido.UIClick, 0.7f);
    }

    void Leave()
    {
        CoopSession.Leave();
    }

    void OnStatus(string text)
    {
        if (statusText != null) statusText.text = text ?? "";
    }

    // ======================= CASILLAS =======================

    void Refresh()
    {
        CoopSession.Slot[] slots = CoopSession.Slots;
        bool host = CoopSession.IsHost;
        int players = CoopSession.PlayerCount;

        if (codeText != null) codeText.text = string.IsNullOrEmpty(CoopSession.RoomCode) ? "------" : CoopSession.RoomCode;
        if (modeText != null)
            modeText.text = CoopSession.IsLan
                ? "RED LOCAL  ·  los demás tienen que estar en la misma Wi-Fi"
                : "EN LÍNEA  ·  pásale el código a tus amigos (hasta 4 jugadores)";
        if (countText != null)
        {
            int ready = 0;
            if (slots != null) foreach (CoopSession.Slot s in slots) if (s.used && s.ready) ready++;
            countText.text = "JUGADORES " + players + " / " + CoopSession.MaxPlayers + "   ·   LISTOS " + ready + " / " + players;
        }

        for (int i = 0; i < 4; i++)
        {
            bool used = slots != null && slots[i].used;
            if (used && !shown[i]) ShowSuit(i);
            else if (!used && shown[i]) HideSuit(i);
            waiting[i].SetActive(!used);
            string n = used ? slots[i].name.ToUpperInvariant() : "";
            if (used && i == CoopSession.LocalSlot) n += "  (TÚ)";
            names[i].text = n;
            if (used)
            {
                bool isHostSlot = i == 0;
                tags[i].text = isHostSlot ? "<color=#FFC840>ANFITRIÓN</color>" : (slots[i].ready ? "<color=#46EBBE>LISTO</color>" : "ESPERANDO…");
            }
            else tags[i].text = "";
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(host);
            startButton.interactable = CoopSession.CanStart;
            if (startLabel != null) startLabel.text = CoopSession.CanStart ? "EMPEZAR" : "ESPERANDO A TODOS";
        }
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(!host && CoopSession.LocalSlot >= 0);
            if (readyLabel != null) readyLabel.text = CoopSession.LocalReady ? "NO ESTOY LISTO" : "¡LISTO!";
        }
    }

    void ShowSuit(int i)
    {
        shown[i] = true;
        appear[i] = 0f;
        if (suits[i] != null) Destroy(suits[i]);
        GameObject suit = workshop != null ? WorkshopKit.PlaceModel(workshop.suitModel, spins[i], 1.5f, true) : null;
        if (suit != null)
        {
            workshop.ApplyVisorGlassTo(suit);
            Animator anim = suit.GetComponentInChildren<Animator>();
            if (anim == null && workshop.suitAnimator != null) anim = suit.AddComponent<Animator>();
            if (anim != null && workshop.suitAnimator != null)
            {
                anim.runtimeAnimatorController = workshop.suitAnimator;
                anim.applyRootMotion = false;
            }
            // El estilo de cada uno (con el original, el color de su casilla)
            int skin = CoopSession.SkinOf(i);
            SkinCatalog.Apply(suit, skin);
            if (i > 0 && skin == 0) CoopVisuals.TintSuit(suit, CoopSession.SlotColors[i]);
        }
        else
        {
            Material m = WorkshopKit.Lit(CoopSession.SlotColors[i], null, 0.4f, "Sala_Traje");
            suit = WorkshopKit.Shape(PrimitiveType.Capsule, "Traje", spins[i], Vector3.up * 0.75f, new Vector3(0.8f, 0.75f, 0.8f), m);
        }
        suits[i] = suit;
        spins[i].localScale = Vector3.one * 0.01f;
        if (lights[i] != null) lights[i].intensity = 5f;
        Sfx.Play2D(i == CoopSession.LocalSlot ? Sonido.Instalar : Sonido.PezRescatado, 0.7f);
    }

    void HideSuit(int i)
    {
        shown[i] = false;
        if (suits[i] != null) Destroy(suits[i]);
        suits[i] = null;
        spins[i].localScale = Vector3.one;
        Sfx.Play2D(Sonido.Aviso, 0.5f, 0.8f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;
        for (int i = 0; i < 4; i++)
        {
            if (rings[i] != null) rings[i].Rotate(0f, (shown[i] ? 40f : 15f) * dt, 0f, Space.Self);
            if (lights[i] != null)
            {
                float target = shown[i] ? 1.4f + Mathf.Sin(t * 2f + i) * 0.2f : 0.35f + Mathf.Sin(t * 3f + i) * 0.15f;
                lights[i].intensity = Mathf.MoveTowards(lights[i].intensity, target, dt * 6f);
            }
            if (shown[i])
            {
                // Entrada con rebote y un leve vaivén
                appear[i] = Mathf.Min(1f, appear[i] + dt * 2.2f);
                float k = appear[i];
                float s = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(k * Mathf.PI)) : 1f;
                spins[i].localScale = Vector3.one * Mathf.Max(0.01f, s);
                spins[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.6f + i * 1.3f) * 12f, 0f);
                spins[i].localPosition = Vector3.up * (0.2f + Mathf.Sin(t * 1.4f + i) * 0.04f);
            }
            else if (waiting[i] != null)
            {
                float a = 0.35f + 0.25f * Mathf.Sin(t * 2.4f + i);
                foreach (TextMeshPro tmp in waiting[i].GetComponentsInChildren<TextMeshPro>())
                {
                    Color c = tmp.color;
                    c.a = a;
                    tmp.color = c;
                }
            }
            CoopVisuals.FaceCamera(names[i]);
            CoopVisuals.FaceCamera(tags[i]);
            if (waiting[i] != null && waiting[i].activeSelf)
                foreach (TextMeshPro tmp in waiting[i].GetComponentsInChildren<TextMeshPro>()) CoopVisuals.FaceCamera(tmp);
        }

        if (codeText != null)
        {
            codeFlash = Mathf.MoveTowards(codeFlash, 0f, dt * 2f);
            codeText.color = Color.Lerp(MenuUI.Yellow, Color.white, codeFlash);
        }
        if (startButton != null && startButton.gameObject.activeSelf)
            startButton.interactable = CoopSession.CanStart;

        if (Input.GetKeyDown(KeyCode.Escape)) Leave();
    }
}
