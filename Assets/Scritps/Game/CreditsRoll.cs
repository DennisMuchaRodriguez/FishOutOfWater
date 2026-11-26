using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Créditos del final: al vencer al jefe final (último nivel de la Historia), después del cómic final.
// Una cinemática sobre el lago con los nombres subiendo y la música "El lago a salvo" (Music.Credits):
//   1. El pez está bajo el agua; enciende el propulsor y sale volando del lago (salpicadura), con la cámara a ras del
//      agua mirándolo subir.
//   2. Vuela en círculos alrededor del lago, inclinándose en la curva; la cámara lo sigue (por detrás, de costado y,
//      al final, desde lo alto con todo el lago en calma).
//   3. Al final mira al horizonte; fundido a negro con "GRACIAS POR JUGAR".
// Como WaveCinematic, NO toca cámaras: calcula una pose y la cámara del jugador la mezcla (PlayerController_Base).
// Mientras dura, GameDirector.InCinematic es true (el HUD se esconde) y el pez queda quieto (SetCinematicLock).
// El tiempo corre normal durante los créditos (el lago está vivo) y al terminar vuelve como estaba (pausa del final).
// Los nombres se editan en Assets/Resources/Creditos.txt.  Esc o SALTAR los termina.
public class CreditsRoll : MonoBehaviour
{
    public const string CreditsPath = "Creditos";
    const float Speed = 46f;          // unidades de la interfaz por segundo (lienzo de 1280 x 720)
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

    // Cámara y vuelo del pez
    float t, weight;          // t: tiempo real (interfaz y cámara)
    float ft;                 // tiempo del vuelo del pez (FixedUpdate)
    float duration = 60f;
    Vector3 center;
    float radius = 30f, surface, cruise;
    float angle0;
    Vector3 startPos;
    Rigidbody rb;
    bool jetOn, splashed;
    LakeVolume lake;
    Vector3 posePos;
    Quaternion poseRot = Quaternion.identity;
    float poseFov = 50f;

    const float Under = 1.0f;     // bajo el agua
    const float Rise = 0.8f;      // hasta romper la superficie
    const float Climb = 1.6f;     // subiendo hasta la altura de vuelo
    const float FlySpeed = 13f;   // m/s dando vueltas
    const float Depth = 3f;
    float T2 { get { return Under + Rise; } }
    float T3 { get { return Under + Rise + Climb; } }
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
        Vector3 hero = player != null ? player.transform.position : Vector3.zero;
        bool valid = lake != null && lake.IsValid;
        center = valid ? lake.Center : hero;
        surface = valid ? lake.SurfaceY : hero.y;
        radius = valid ? Mathf.Clamp(lake.Radius * 0.5f, 14f, 70f) : 25f;
        cruise = surface + 10f;

        // El pez sale del agua en el círculo, del lado donde estaba (con agua honda para salir disparado)
        Vector3 from = hero - center;
        from.y = 0f;
        if (from.sqrMagnitude < 1f) from = Vector3.forward;
        float a = Mathf.Atan2(from.x, from.z);
        Vector3 p = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
        if (valid && lake.WaterDepthAt(p.x, p.z) < Depth + 1f)
        {
            Vector3 q;
            if (lake.TryGetSwimPoint(p, 25f, Depth + 1f, 80f, 1.2f, out q, 20)) p = q;
        }
        Vector3 d = p - center;
        d.y = 0f;
        radius = Mathf.Max(10f, d.magnitude);
        angle0 = Mathf.Atan2(d.x, d.z);
        float ground = valid ? lake.GroundHeight(p.x, p.z) : surface - 10f;
        startPos = new Vector3(p.x, Mathf.Max(ground + 1.2f, surface - Depth), p.z);

        rb = player != null ? player.GetComponent<Rigidbody>() : null;
        PlaceFish(startPos, FacingAt(0f));

        BuildUI();
        duration = Mathf.Max(30f, (rollHeight + 760f) / Speed);
        UpdatePose();
    }

    // ======================= EL VUELO DEL PEZ =======================

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // Ángulo en el círculo: quieto hasta salir del agua, acelera y después da vueltas a velocidad constante
    float AngleAt(float time)
    {
        float w = FlySpeed / radius;
        float x = time - T2;
        if (x <= 0f) return angle0;
        if (x < Climb) return angle0 + w * x * x / (2f * Climb);
        return angle0 + w * (Climb * 0.5f + (x - Climb));
    }

    float HeightAt(float time)
    {
        if (time < Under) return startPos.y + 0.35f * Smooth(time / Under);
        if (time < T2)
        {
            float k = (time - Under) / Rise;
            return Mathf.Lerp(startPos.y + 0.35f, surface + 0.6f, k * k);
        }
        if (time < T3)
        {
            float k = (time - T2) / Climb;
            return Mathf.Lerp(surface + 0.6f, cruise, 1f - (1f - k) * (1f - k));
        }
        return cruise + 1.5f * Mathf.Sin((time - T3) * 0.6f);
    }

    Vector3 FishAt(float time)
    {
        float a = AngleAt(time);
        float r = radius * (1f + 0.12f * Mathf.Sin(0.21f * Mathf.Max(0f, time - T3)));
        Vector3 pos = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
        pos.y = HeightAt(time);
        if (time > T3 && lake != null) pos.y = Mathf.Max(pos.y, lake.GroundHeight(pos.x, pos.z) + 5f);
        return pos;
    }

    // Hacia dónde vuela (tangente al círculo, sentido de las agujas del reloj visto desde arriba)
    Vector3 TangentAt(float time)
    {
        float a = AngleAt(time);
        return new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
    }

    Quaternion FacingAt(float time)
    {
        // Se inclina hacia el centro del círculo en la curva
        float bank = Smooth((time - T2) / Climb) * 20f;
        return Quaternion.LookRotation(TangentAt(time), Vector3.up) * Quaternion.Euler(0f, 0f, -bank);
    }

    void PlaceFish(Vector3 pos, Quaternion rot)
    {
        if (player == null) return;
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = rot;
        }
        player.transform.SetPositionAndRotation(pos, rot);
    }

    void FixedUpdate()
    {
        if (finished || player == null) return;
        ft += Time.fixedDeltaTime;
        Vector3 pos = FishAt(ft);
        Quaternion rot = FacingAt(ft);
        if (rb != null)
        {
            rb.MovePosition(pos);
            rb.MoveRotation(rot);
        }
        else player.transform.SetPositionAndRotation(pos, rot);

        if (!jetOn && ft >= Under - 0.1f)
        {
            jetOn = true;
            player.SetCinematicJet(true, 1f);
        }
        if (!splashed && pos.y >= surface)
        {
            splashed = true;
            LakeWater.Splash(new Vector3(pos.x, surface, pos.z), 1f, false);
            Sfx.Play(Sonido.SalidaAgua, pos, 1f);
        }
    }

    // ======================= LA CÁMARA =======================

    void UpdatePose()
    {
        Vector3 fish = player != null ? player.transform.position : FishAt(ft);
        Vector3 tangent = TangentAt(ft);
        Vector3 outward = fish - center;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;
        float u = Mathf.Clamp01(t / duration);

        // 1. La salida: al costado (afuera del círculo), bajo el agua y después a ras del agua mirándolo subir
        Vector3 side = (outward * 0.8f + tangent * 0.5f).normalized;
        float dist = Mathf.Lerp(6f, 11f, Smooth((ft - Under) / (T3 - Under)));
        float camY = ft < T2 ? Mathf.Min(fish.y - 0.3f, surface - 0.9f)
                             : Mathf.Lerp(surface - 0.9f, surface + 0.9f, Smooth((ft - T2) / 0.35f));
        Vector3 launchPos = new Vector3(fish.x, camY, fish.z) + side * dist;
        Vector3 launchLook = fish + Vector3.up * 0.8f;

        // 2. Dando vueltas: por detrás y de costado (va y viene); 3. al final, desde lo alto con todo el lago
        Vector3 behind = fish - tangent * 11f + Vector3.up * 3.5f + outward * 2f;
        Vector3 beside = fish + outward * 13f + Vector3.up * 1.5f + tangent * 2f;
        float m = 0.5f + 0.5f * Mathf.Sin((ft - T3) * 0.16f);
        Vector3 followPos = Vector3.Lerp(behind, beside, m);
        Vector3 followLook = fish + tangent * 3f + Vector3.up * 0.5f;
        Vector3 highPos = center + outward * radius * 1.6f + Vector3.up * (cruise - surface + 24f);
        float high = Smooth((u - 0.7f) / 0.2f);
        followPos = Vector3.Lerp(followPos, highPos, high);
        followLook = Vector3.Lerp(followLook, fish, high);

        float k = Smooth((ft - T3) / 1.8f);
        Vector3 pos = Vector3.Lerp(launchPos, followPos, k);
        Vector3 look = Vector3.Lerp(launchLook, followLook, k);
        if (lake != null) pos.y = Mathf.Max(pos.y, lake.GroundHeight(pos.x, pos.z) + (ft < T2 ? 1f : 1.5f));

        // Al final mira al horizonte
        Vector3 dir = look - pos;
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
        dir = Vector3.Slerp(dir.normalized, (flat.normalized + Vector3.up * 0.08f).normalized, Smooth((u - 0.9f) / 0.1f));

        // Un poco de cámara en mano
        float n1 = Mathf.PerlinNoise(t * 0.23f, 3.1f) - 0.5f;
        float n2 = Mathf.PerlinNoise(7.7f, t * 0.19f) - 0.5f;
        posePos = pos;
        poseRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(n1 * 0.8f, n2 * 0.8f, n1 * 1.2f);
        poseFov = Mathf.Lerp(52f, 58f, k);
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
        weight = 1f;   // corte directo: antes de los créditos estaba el cómic a pantalla completa
        UpdatePose();

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
        if (player != null)
        {
            player.SetCinematicJet(false);
            player.SetCinematicLock(false);
        }
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
        if (player != null)
        {
            player.SetCinematicJet(false);
            player.SetCinematicLock(false);
        }
    }
}
