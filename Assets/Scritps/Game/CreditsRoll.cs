using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Créditos del final: al vencer al jefe final (último nivel de la Historia), después del cómic final.
// Una cinemática sobre el lago con los nombres subiendo y la música "El lago a salvo" (Music.Credits):
//   1. La cámara deja la vista del jugador y queda a ras del agua junto al pez, girando despacio a su alrededor.
//   2. Se aleja y sube mientras sigue girando, hasta ver todo el lago en calma (el cardumen nada, el agua se mueve).
//   3. Al final mira al horizonte; fundido a negro con "GRACIAS POR JUGAR".
// Como WaveCinematic, NO toca cámaras: calcula una pose y la cámara del jugador la mezcla (PlayerController_Base).
// Mientras dura, GameDirector.InCinematic es true (el HUD se esconde) y el pez queda quieto (SetCinematicLock).
// El tiempo corre normal durante los créditos (el lago está vivo) y al terminar vuelve como estaba (pausa del final).
// Los nombres se editan en Assets/Resources/Creditos.txt.  Esc o SALTAR los termina.
public class CreditsRoll : MonoBehaviour
{
    public const string CreditsPath = "Creditos";
    const float Speed = 46f;          // unidades de la interfaz por segundo (lienzo de 1280 x 720)
    const float BlendIn = 1.6f;
    const float EndCard = 4f;         // "Gracias por jugar"
    const float SkipDelay = 1f;

    static CreditsRoll active;

    // True mientras corren los créditos (el HUD se oculta y el jugador no se mueve)
    public static bool IsPlaying { get { return active != null && !active.finished; } }

    // Pose que la cámara del jugador debe mezclar con la suya (false = no hay créditos)
    public static bool TryGetPose(out Vector3 position, out Quaternion rotation, out float fov, out float blend)
    {
        if (!IsPlaying || active.weight <= 0f)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            fov = 60f;
            blend = 0f;
            return false;
        }
        position = active.posePos;
        rotation = active.poseRot;
        fov = active.poseFov;
        blend = active.weight;
        return true;
    }

    // ¿Toca créditos? Solo en la Historia, de un jugador, al ganar el último nivel del catálogo
    public static bool ShouldPlay(GameDirector director, LevelDefinition level)
    {
        if (director == null || level == null || director.IsCoop || GameSession.IsSurvival) return false;
        if (GameSession.CurrentMode != GameSession.Mode.Historia) return false;
        LevelCatalog catalog = GameSession.Catalog;
        return catalog != null && GameSession.LevelIndex >= 0 && GameSession.LevelIndex == catalog.Count - 1;
    }

    public static CreditsRoll Play(PlayerController_Base player, System.Action onDone)
    {
        if (active != null) Destroy(active.gameObject);
        GameObject go = new GameObject("Creditos");
        CreditsRoll c = go.AddComponent<CreditsRoll>();
        active = c;
        c.player = player;
        c.onDone = onDone;
        c.Begin();
        return c;
    }

    PlayerController_Base player;
    System.Action onDone;
    bool finished;
    float savedTimeScale = 1f;

    // Cámara
    float t, weight;
    float duration = 60f;
    Vector3 hero, center;
    float radius = 60f, surface;
    float startAngle;
    LakeVolume lake;
    Vector3 posePos;
    Quaternion poseRot = Quaternion.identity;
    float poseFov = 50f;

    // Interfaz
    RectTransform roll;
    float rollHeight;
    RectTransform barTop, barBottom;
    Image black;
    CanvasGroup endCard;
    GameObject skipButton;
    float endTimer = -1f;   // >= 0: ya terminó de subir (fundido y "Gracias por jugar")
    bool skipping;

    // ======================= ARRANQUE =======================

    void Begin()
    {
        savedTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        if (player != null) player.SetCinematicLock(true);
        Music.Play(Music.Credits, 1.5f);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        lake = LakeVolume.Instance;
        hero = player != null ? player.transform.position : Vector3.zero;
        if (lake != null && lake.IsValid)
        {
            center = lake.Center;
            radius = Mathf.Max(25f, lake.Radius);
            surface = lake.SurfaceY;
        }
        else
        {
            center = hero;
            surface = hero.y;
        }
        // Empieza detrás de donde miraba la cámara del jugador (sin salto)
        Camera cam = Camera.main;
        Vector3 from = cam != null ? cam.transform.position - hero : -Vector3.forward;
        startAngle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;

        BuildUI();
        duration = Mathf.Max(30f, (rollHeight + 760f) / Speed);
        UpdatePose(0f);
    }

    // ======================= LA CÁMARA =======================

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void UpdatePose(float time)
    {
        float u = Mathf.Clamp01(time / duration);
        // Centro de la órbita: del pez al centro del lago
        Vector3 pivot = Vector3.Lerp(hero, center, Smooth(u / 0.55f));
        float angle = (startAngle + u * 200f) * Mathf.Deg2Rad;
        float r = Mathf.Lerp(7f, radius * 0.85f, Smooth(u / 0.6f));
        float h = Mathf.Lerp(1.8f, Mathf.Clamp(radius * 0.3f, 12f, 45f), Smooth(u));
        Vector3 pos = new Vector3(pivot.x + Mathf.Sin(angle) * r, surface + h, pivot.z + Mathf.Cos(angle) * r);
        if (lake != null) pos.y = Mathf.Max(pos.y, lake.GroundHeight(pos.x, pos.z) + 3f);

        // Mira al pez, después al lago y al final al horizonte
        Vector3 look = Vector3.Lerp(hero + Vector3.up * 0.6f, new Vector3(center.x, surface, center.z), Smooth((u - 0.1f) / 0.5f));
        Vector3 dir = look - pos;
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
        dir = Vector3.Slerp(dir.normalized, (flat.normalized + Vector3.up * 0.08f).normalized, Smooth((u - 0.78f) / 0.22f));

        // Un poco de cámara en mano
        float n1 = Mathf.PerlinNoise(time * 0.23f, 3.1f) - 0.5f;
        float n2 = Mathf.PerlinNoise(7.7f, time * 0.19f) - 0.5f;
        posePos = pos;
        poseRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(n1 * 0.8f, n2 * 0.8f, n1 * 1.2f);
        poseFov = Mathf.Lerp(48f, 56f, Smooth(u));
    }

    // ======================= LA INTERFAZ =======================

    void BuildUI()
    {
        Canvas canvas = MenuUI.CreateCanvas("CreditosCanvas", 450);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;

        // Franja oscura al centro para que los nombres se lean sobre el lago
        RectTransform band = MenuUI.Rect("Banda", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 2000f));
        RawImage bandImg = band.gameObject.AddComponent<RawImage>();
        bandImg.texture = BandTexture();
        bandImg.color = new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, 0.55f);
        bandImg.raycastTarget = false;

        roll = MenuUI.Rect("Rodillo", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(760f, 10f));
        roll.pivot = new Vector2(0.5f, 1f);
        rollHeight = BuildLines(roll);
        roll.anchoredPosition = new Vector2(0f, -20f);

        // Franjas de cine arriba y abajo
        barTop = Bar(root, true);
        barBottom = Bar(root, false);

        Button skip = MenuUI.ComicButton("SALTAR", root, new Vector2(1f, 1f), new Vector2(-82f, -30f), new Vector2(128f, 36f),
                                         MenuUI.BlueDark, Skip, 17f);
        skipButton = skip.gameObject;
        skipButton.SetActive(false);

        // Final: negro y "Gracias por jugar"
        black = MenuUI.Panel("Negro", root, new Color(0f, 0f, 0f, 0f));
        black.raycastTarget = false;
        RectTransform card = MenuUI.Stretch("Final", root);
        endCard = card.gameObject.AddComponent<CanvasGroup>();
        endCard.alpha = 0f;
        endCard.blocksRaycasts = false;
        MenuUI.Title("Gracias", card, "GRACIAS POR JUGAR", 60f, new Vector2(0.5f, 0.5f), new Vector2(0f, 26f),
                     new Vector2(1000f, 90f), MenuUI.Orange);
        MenuUI.Text("Juego", card, "FISH OUT OF WATER", 22f, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                    new Vector2(700f, 36f), MenuUI.Accent, true).characterSpacing = 10f;
    }

    RectTransform Bar(RectTransform root, bool top)
    {
        RectTransform rt = MenuUI.Stretch(top ? "FranjaArriba" : "FranjaAbajo", root);
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 0f);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }

    static Texture2D bandTex;
    static Texture2D BandTexture()
    {
        if (bandTex != null) return bandTex;
        bandTex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FX_CreditsBand" };
        for (int x = 0; x < 64; x++)
        {
            float d = Mathf.Abs(x / 63f - 0.5f) * 2f;
            bandTex.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - Smooth((d - 0.35f) / 0.65f)));
        }
        bandTex.Apply();
        return bandTex;
    }

    // Arma la columna de nombres desde Resources/Creditos.txt. Devuelve su alto.
    float BuildLines(RectTransform parent)
    {
        TextAsset file = Resources.Load<TextAsset>(CreditsPath);
        string text = file != null ? file.text : "[LOGO]\n\nUn juego de\n# KAI CODEX";
        float y = 0f;
        foreach (string raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("//")) continue;
            if (line.Length == 0) { y += 26f; continue; }
            if (line == "[LOGO]")
            {
                Sprite logo = Resources.Load<Sprite>(MainMenu.LogoPath);
                if (logo != null)
                {
                    Image img = MenuUI.Img("Logo", parent, logo, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -y - 110f), new Vector2(600f, 220f));
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    y += 236f;
                }
                else y += Line(parent, "FISH OUT OF WATER", 54f, MenuUI.Orange, true, y, 80f);
                continue;
            }
            if (line.StartsWith("### ")) y += Line(parent, line.Substring(4), 16f, MenuUI.TextDim, false, y, 26f);
            else if (line.StartsWith("## "))
            {
                y += 10f;
                y += Line(parent, line.Substring(3).ToUpper(), 19f, MenuUI.Accent, true, y, 32f, 6f);
            }
            else if (line.StartsWith("# ")) y += Line(parent, line.Substring(2), 46f, MenuUI.Orange, true, y, 64f, 4f);
            else y += Line(parent, line, 28f, MenuUI.Cream, false, y, 40f);
        }
        return y;
    }

    static float Line(RectTransform parent, string text, float size, Color color, bool title, float y, float height, float spacing = 0f)
    {
        TextMeshProUGUI t = MenuUI.Text("Linea", parent, text, size, new Vector2(0.5f, 1f), new Vector2(0f, -y - height * 0.5f),
                                        new Vector2(740f, height), color, title);
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        return height;
    }

    // ======================= CADA FRAME =======================

    void Skip()
    {
        if (finished || skipping) return;
        skipping = true;
        if (endTimer < 0f) endTimer = 0f;
    }

    void Update()
    {
        if (finished) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        t += dt;
        weight = Smooth(t / BlendIn);
        UpdatePose(t);

        float bars = Smooth(t / 1.2f) * 58f;
        barTop.sizeDelta = new Vector2(0f, bars);
        barBottom.sizeDelta = new Vector2(0f, bars);
        if (!skipButton.activeSelf && t > SkipDelay) skipButton.SetActive(true);
        if (Input.GetKeyDown(KeyCode.Escape) && t > SkipDelay) Skip();

        // Los nombres suben desde abajo de la pantalla
        float y = -20f + Mathf.Max(0f, t - 1.2f) * Speed;
        roll.anchoredPosition = new Vector2(0f, y);
        if (endTimer < 0f && y > rollHeight + 760f) endTimer = 0f;

        if (endTimer >= 0f)
        {
            endTimer += dt;
            float fade = skipping ? 0.6f : 1.4f;
            black.color = new Color(0f, 0f, 0f, Smooth(endTimer / fade));
            float card = skipping ? 1.6f : EndCard;
            endCard.alpha = Smooth((endTimer - fade) / 0.6f) * (1f - Smooth((endTimer - fade - card + 0.6f) / 0.6f));
            if (endTimer > fade + 0.4f && Music.Current == Music.Credits) Music.Stop(card);
            if (endTimer > fade + card) End();
        }
    }

    void End()
    {
        if (finished) return;
        finished = true;
        if (active == this) active = null;
        Time.timeScale = savedTimeScale;
        if (player != null) player.SetCinematicLock(false);
        System.Action done = onDone;
        onDone = null;
        Destroy(gameObject);
        if (done != null) done();
    }

    void OnDestroy()
    {
        if (finished) return;
        // Si la escena cambia a mitad de los créditos, el tiempo vuelve a la normalidad
        finished = true;
        if (active == this) active = null;
        Time.timeScale = savedTimeScale;
    }
}
