using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD de la partida (mismo estilo cian del visor):
//  - Objetivos abajo a la derecha: oleada, depredadores, peces a salvo (+ barra con el límite de pérdida)
//  - Anuncios grandes (oleadas, victoria) y avisos cortos (pez cazado, munición...)
//  - Barras de cine durante la cinemática
//  - Marcadores sobre los pájaros (rojo; parpadea si lleva un pez) y sobre las cápsulas de munición
public class GameHUD : MonoBehaviour
{
    public float pickupMarkerRange = 70f;
    public int lowAmmoForArrow = 8;

    GameDirector director;
    PlayerController_Base player;
    PlayerShooting shooting;
    RectTransform root;

    RectTransform objectives;
    TextMeshProUGUI waveText, birdsText, fishText, countdownText;
    RawImage fishBarFill, fishBarLimit;
    RectTransform fishBarBg;

    TextMeshProUGUI bannerTitle, bannerSub;
    float bannerTimer;
    TextMeshProUGUI toastText;
    float toastTimer;
    readonly Queue<KeyValuePair<string, Color>> toastQueue = new Queue<KeyValuePair<string, Color>>();

    RectTransform barTop, barBottom;
    float letterbox;
    bool letterboxOn;

    RectTransform markerRoot;
    readonly List<RawImage> markerPool = new List<RawImage>();
    int markersUsed;
    Texture2D triangleTex, diamondTex;

    const float BarWidth = 240f;

    void Start()
    {
        director = GetComponent<GameDirector>();
        if (director == null) director = GameDirector.Instance;
        player = director != null ? director.player : FindFirstObjectByType<PlayerController_Base>();
        if (player != null) shooting = player.GetComponent<PlayerShooting>();

        Canvas canvas = MenuUI.CreateCanvas("GameHUDCanvas", 20);
        canvas.transform.SetParent(transform, false);
        root = (RectTransform)canvas.transform;
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        triangleTex = MakeTriangle(64);
        diamondTex = MakeDiamond(64);

        markerRoot = MenuUI.Stretch("Marcadores", root);
        BuildObjectives();
        BuildBanner();
        BuildLetterbox();

        if (director != null)
        {
            director.Banner += ShowBanner;
            director.Toast += ShowToast;
            director.Letterbox += on => letterboxOn = on;
        }
    }

    void OnDestroy()
    {
        if (director != null)
        {
            director.Banner -= ShowBanner;
            director.Toast -= ShowToast;
        }
    }

    // ======================= CONSTRUCCIÓN =======================

    void BuildObjectives()
    {
        objectives = MenuUI.Rect("Objetivos", root, new Vector2(1f, 0f), new Vector2(-170f, 110f), new Vector2(280f, 150f));

        waveText = Line("Oleada", 16f, 52f);
        birdsText = Line("Depredadores", 16f, 28f);
        fishText = Line("Peces", 18f, 2f);
        fishText.fontStyle = FontStyles.Bold;

        fishBarBg = MenuUI.Rect("BarraPeces", objectives, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(BarWidth, 7f));
        RawImage bg = fishBarBg.gameObject.AddComponent<RawImage>();
        bg.texture = FXFactory.White;
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false;

        fishBarFill = CreateRaw("Relleno", fishBarBg, FXFactory.White, GameDirector.Cyan);
        RectTransform fr = fishBarFill.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;

        fishBarLimit = CreateRaw("Limite", fishBarBg, FXFactory.White, GameDirector.Danger);
        fishBarLimit.rectTransform.sizeDelta = new Vector2(2f, 15f);

        countdownText = Line("Cuenta", 15f, -46f);
    }

    TextMeshProUGUI Line(string name, float size, float y)
    {
        TextMeshProUGUI t = MenuUI.CreateText(name, objectives, "", size, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(280f, 26f));
        t.alignment = TextAlignmentOptions.Right;
        t.characterSpacing = 5f;
        return t;
    }

    RawImage CreateRaw(string name, Transform parent, Texture tex, Color c)
    {
        RectTransform rt = MenuUI.Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 10f);
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void BuildBanner()
    {
        bannerTitle = MenuUI.CreateText("Anuncio", root, "", 46f, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1200f, 70f));
        bannerTitle.fontStyle = FontStyles.Bold;
        bannerTitle.characterSpacing = 10f;
        bannerSub = MenuUI.CreateText("AnuncioSub", root, "", 20f, new Vector2(0.5f, 0.5f), new Vector2(0f, 122f), new Vector2(1200f, 34f));
        bannerSub.characterSpacing = 6f;

        toastText = MenuUI.CreateText("Aviso", root, "", 20f, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1100f, 34f));
        toastText.fontStyle = FontStyles.Bold;
        toastText.characterSpacing = 4f;
    }

    void BuildLetterbox()
    {
        barTop = MenuUI.Rect("CineArriba", root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, 0f));
        barTop.pivot = new Vector2(0.5f, 1f);
        barTop.anchoredPosition = Vector2.zero;
        RawImage t = barTop.gameObject.AddComponent<RawImage>();
        t.texture = FXFactory.White;
        t.color = Color.black;
        t.raycastTarget = false;

        barBottom = MenuUI.Rect("CineAbajo", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 0f));
        barBottom.pivot = new Vector2(0.5f, 0f);
        barBottom.anchoredPosition = Vector2.zero;
        RawImage b = barBottom.gameObject.AddComponent<RawImage>();
        b.texture = FXFactory.White;
        b.color = Color.black;
        b.raycastTarget = false;
    }

    // ======================= MENSAJES =======================

    void ShowBanner(string title, string sub, Color color)
    {
        bannerTitle.text = title;
        bannerSub.text = sub;
        bannerTitle.color = color;
        bannerSub.color = new Color(color.r, color.g, color.b, 0.8f);
        bannerTimer = 4f;
    }

    void ShowToast(string text, Color color)
    {
        if (toastQueue.Count > 3) toastQueue.Dequeue();
        toastQueue.Enqueue(new KeyValuePair<string, Color>(text, color));
    }

    // ======================= ACTUALIZACIÓN =======================

    void Update()
    {
        if (root == null) return;
        float dt = Time.unscaledDeltaTime;
        bool cinematic = GameDirector.InCinematic;

        UpdateObjectives(cinematic);
        UpdateMessages(dt);

        // Barras de cine
        letterbox = Mathf.MoveTowards(letterbox, letterboxOn ? 1f : 0f, dt * 2.5f);
        float h = Mathf.SmoothStep(0f, 1f, letterbox) * 95f;
        barTop.sizeDelta = new Vector2(4000f, h);
        barBottom.sizeDelta = new Vector2(4000f, h);

        UpdateMarkers(cinematic);
    }

    void UpdateObjectives(bool cinematic)
    {
        objectives.gameObject.SetActive(!cinematic && director != null);
        if (director == null || cinematic) return;

        waveText.text = director.CurrentWave > 0
            ? "OLEADA " + director.CurrentWave + " / " + director.TotalWaves
            : "OLEADA - / " + director.TotalWaves;
        int alive = director.AliveBirds();
        birdsText.text = "DEPREDADORES  " + alive;
        birdsText.color = alive > 0 ? GameDirector.Danger : GameDirector.Cyan;

        int total = Mathf.Max(1, director.TotalFish);
        float alive01 = (float)director.FishAlive / total;
        int remainingBeforeLoss = director.MaxFishLoss - director.FishLost;
        bool danger = remainingBeforeLoss <= Mathf.Max(2, total / 8);
        fishText.text = "PECES A SALVO  " + director.FishAlive + "/" + director.TotalFish;
        fishText.color = danger ? Color.Lerp(GameDirector.Danger, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 0.5f)) : GameDirector.Cyan;

        fishBarFill.rectTransform.anchorMax = new Vector2(alive01, 1f);
        fishBarFill.color = Color.Lerp(GameDirector.Danger, GameDirector.Cyan, Mathf.Clamp01((alive01 - (1f - director.maxFishLossFraction)) / 0.3f));
        // Línea roja: si los peces bajan de aquí, pierdes
        float limit01 = (float)(total - director.MaxFishLoss) / total;
        fishBarLimit.rectTransform.anchoredPosition = new Vector2((limit01 - 0.5f) * BarWidth, 0f);

        bool waiting = director.State == GameDirector.GameState.Calm || director.State == GameDirector.GameState.Intermission;
        countdownText.gameObject.SetActive(waiting && director.Countdown > 0f);
        if (waiting) countdownText.text = "PRÓXIMA OLEADA EN " + Mathf.CeilToInt(director.Countdown);
    }

    void UpdateMessages(float dt)
    {
        if (bannerTimer > 0f) bannerTimer -= dt;
        float ba = Mathf.Clamp01(bannerTimer / 0.6f) * Mathf.Clamp01((4f - bannerTimer) / 0.25f);
        SetAlpha(bannerTitle, ba);
        SetAlpha(bannerSub, ba * 0.85f);
        bannerTitle.rectTransform.localScale = Vector3.one * (1f + (1f - Mathf.Clamp01((4f - bannerTimer) / 0.25f)) * 0.15f);

        if (toastTimer > 0f) toastTimer -= dt;
        if (toastTimer <= 0f && toastQueue.Count > 0)
        {
            KeyValuePair<string, Color> next = toastQueue.Dequeue();
            toastText.text = next.Key;
            toastText.color = next.Value;
            toastTimer = 2.4f;
        }
        SetAlpha(toastText, Mathf.Clamp01(toastTimer / 0.4f));
    }

    static void SetAlpha(TextMeshProUGUI t, float a)
    {
        Color c = t.color;
        c.a = a;
        t.color = c;
    }

    // ======================= MARCADORES =======================

    void UpdateMarkers(bool cinematic)
    {
        markersUsed = 0;
        Camera cam = player != null ? player.PlayerCamera : Camera.main;
        if (cam != null && director != null && !cinematic && player != null && !player.isDead)
        {
            foreach (BirdAI bird in director.Birds)
            {
                if (bird.IsArriving && !IsOnScreen(cam, bird.transform.position)) continue;
                bool carrying = bird.CarriedFish != null;
                Color c = carrying
                    ? Color.Lerp(GameDirector.Danger, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f))
                    : GameDirector.Danger;
                PlaceMarker(cam, bird.transform.position + Vector3.up * 2.5f, triangleTex, c, carrying ? 30f : 20f, true);
            }

            bool lowAmmo = shooting != null && shooting.currentAmmo <= lowAmmoForArrow;
            AmmoPickup nearest = null;
            float nearestDist = float.MaxValue;
            foreach (AmmoPickup p in director.Pickups)
            {
                if (p == null) continue;
                float d = Vector3.Distance(player.transform.position, p.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = p; }
                if (d < pickupMarkerRange && IsOnScreen(cam, p.transform.position))
                    PlaceMarker(cam, p.transform.position + Vector3.up * 1.4f, diamondTex, new Color(0.4f, 0.95f, 1f, 0.85f), 16f, false);
            }
            // Con poca munición, una flecha señala la cápsula más cercana aunque esté fuera de pantalla
            if (lowAmmo && nearest != null && !IsOnScreen(cam, nearest.transform.position))
                PlaceMarker(cam, nearest.transform.position, diamondTex, new Color(0.4f, 0.95f, 1f, 0.9f), 18f, true);
        }

        for (int i = markersUsed; i < markerPool.Count; i++)
            if (markerPool[i].gameObject.activeSelf) markerPool[i].gameObject.SetActive(false);
    }

    static bool IsOnScreen(Camera cam, Vector3 world)
    {
        Vector3 sp = cam.WorldToViewportPoint(world);
        return sp.z > 0f && sp.x > 0f && sp.x < 1f && sp.y > 0f && sp.y < 1f;
    }

    void PlaceMarker(Camera cam, Vector3 world, Texture tex, Color color, float size, bool clampToEdge)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool behind = vp.z < 0f;
        if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
        bool onScreen = !behind && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
        if (!onScreen && !clampToEdge) return;

        Rect r = root.rect;
        Vector2 pos = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
        float rotation = 180f; // triángulo apuntando hacia abajo, sobre el objetivo

        if (!onScreen)
        {
            // Flecha en el borde de la pantalla apuntando al objetivo
            Vector2 dir = pos.sqrMagnitude > 0.01f ? pos.normalized : Vector2.down;
            Vector2 half = new Vector2(r.width * 0.5f - 40f, r.height * 0.5f - 40f);
            float k = Mathf.Min(Mathf.Abs(half.x / Mathf.Max(0.001f, Mathf.Abs(dir.x))), Mathf.Abs(half.y / Mathf.Max(0.001f, Mathf.Abs(dir.y))));
            pos = dir * k;
            rotation = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }

        RawImage img;
        if (markersUsed < markerPool.Count)
        {
            img = markerPool[markersUsed];
        }
        else
        {
            img = CreateRaw("Marcador", markerRoot, tex, color);
            markerPool.Add(img);
        }
        markersUsed++;

        img.gameObject.SetActive(true);
        img.texture = tex;
        img.color = color;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = Vector2.one * size;
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, tex == triangleTex ? rotation : 0f);
    }

    // ======================= TEXTURAS =======================

    static Texture2D MakeTriangle(int s)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s;
                float v = (y + 0.5f) / s;
                // Triángulo apuntando hacia arriba (se rota según haga falta)
                float halfWidth = (1f - v) * 0.5f;
                float d = Mathf.Abs(u - 0.5f) - halfWidth;
                float edge = Mathf.Max(d, 0.08f - v);
                float a = Mathf.Clamp01(-edge * s * 0.5f);
                // Solo el contorno + un poco de relleno
                float inner = Mathf.Clamp01((-edge - 0.12f) * s * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * (1f - inner * 0.75f)));
            }
        }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static Texture2D MakeDiamond(int s)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / s * 2f - 1f);
                float v = Mathf.Abs((y + 0.5f) / s * 2f - 1f);
                float d = u + v;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.12f);
                float core = d < 0.35f ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, core)));
            }
        }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
