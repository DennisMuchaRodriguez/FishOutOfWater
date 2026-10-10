using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Munición dentro del visor del casco (GDD "HUD dentro del traje"):
//  - Arco de celdas cian en el borde derecho; cada celda es una bala y se apaga al disparar
//    (con el Cargador ampliado una celda vale 2 balas para que el arco no se llene de rayitas)
//  - Número grande, "/ máximo" y el nombre del arma; parpadea en rojo con 5 balas o menos
//  - Carril interior que se llena al cargar el disparo cargado (las celdas que va a gastar se ven blancas)
//  - Debajo: misiles (si tienes la mejora) y el escudo de burbuja con su anillo de recarga
// Lo crea HelmetVisorHUD dentro de "VisorSway": se mueve con la inercia del casco, arranca con su
// encendido y parpadea con el daño. En tercera persona se pega más al borde de la pantalla.
public class VisorAmmoHUD : MonoBehaviour
{
    // ---- Medidas en unidades del canvas del visor (800 de ancho; las mismas de la maqueta) ----
    const float R = 300f;            // radio del arco (el centro queda a la izquierda, fuera del widget)
    const float Half = 19f;          // medio ángulo del arco (grados)
    const float CellLen = 13f;       // largo radial de cada celda
    const float CellGap = 1.6f;
    const int MaxCells = 45;
    const float LaneOff = 5.5f, LaneT = 2.2f;
    const int LaneDashes = 30;
    const float GuideOff = 4.5f, GuideT = 1f;
    const float CapExtra = 1.4f, CapIn = 9f, CapOut = 9f, CapT = 1.4f, CapFoot = 2.6f;
    const float TickLen = 4.5f, TickT = 1.2f;
    const float BandIn = 13f, BandOut = 13f, BandSoft = 6f;
    const float NumX = -32f, NumY = 11f, NumSize = 38f;
    const float MaxY = -15f, MaxSize = 12f;
    const float LabelY = 36f, LabelSize = 9f, LabelW = 140f;
    const float PlateX = -78f, PlateY = -12f, PlateW = 250f, PlateH = 200f, PlateA = 0.6f;
    const float MslY = -45f, MslW = 7f, MslH = 18f, MslPitch = 10.5f, MslX = -37f;
    const float ShdX = -44f, ShdY = -72f, ShdSize = 16f;
    const float FpMargin = 14f, TpInset = 16f, YOff = 18f;

    static readonly Color Navy = new Color(0.01f, 0.035f, 0.06f, 1f);

    [HideInInspector] public HelmetVisorHUD visor;
    [HideInInspector] public PlayerController_Base player;
    [HideInInspector] public PlayerShooting shooting;

    Color accent = new Color(0.41f, 0.87f, 0.9f, 1f);
    Color danger = new Color(1f, 0.15f, 0.1f, 1f);

    RectTransform root, cellRoot, tickRoot;
    RawImage plate, band, glow, lines, lane, numberGlow, labelLine, labelLineBright;
    Image laneFill;
    TextMeshProUGUI numberText, maxText, labelText;
    readonly List<RawImage> cells = new List<RawImage>();
    readonly List<RawImage> ticks = new List<RawImage>();
    int builtMax = -1, bulletsPerCell = 1;
    string shownWeapon;
    int shownAmmo = -1, shownMax = -1;
    bool shownEmpty;

    // Misiles y escudo
    RectTransform missileRoot, shieldRoot;
    readonly List<RawImage> missileOutlines = new List<RawImage>();
    readonly List<Image> missileFills = new List<Image>();
    TextMeshProUGUI missileLabel, missileKey, shieldLabel;
    Image shieldRing;
    RawImage shieldTrack, shieldBubble;
    int builtCapacity = -1;
    float missileFlash, shieldFlash;
    MissileLauncher hookedLauncher;
    BubbleShield hookedShield;

    // Animación
    float shotFlash;       // la celda de arriba destella al disparar
    bool lastShotCharged;
    float pickupFlash;     // todo el arco brilla al recoger munición
    int lastAmmo;

    static Texture2D bandTex, glowTex, linesTex, laneTex, missileTex, missileOutlineTex;
    static Rect bandBox, glowBox, linesBox, laneBox;
    static Sprite laneSprite, missileSprite, ringSprite;

    public void Build(RectTransform parent, Color accentColor, Color dangerColor)
    {
        accent = accentColor;
        danger = dangerColor;
        MakeTextures();

        root = NewRect("VisorMunicion", parent);
        root.anchorMin = root.anchorMax = new Vector2(1f, 0.5f);
        root.sizeDelta = Vector2.zero;

        plate = Raw("Placa", root, FXFactory.SoftDot, new Color(Navy.r, Navy.g, Navy.b, PlateA));
        Place(plate.rectTransform, PlateX, PlateY, PlateW, PlateH);

        band = Raw("Banda", root, bandTex, new Color(Navy.r, Navy.g, Navy.b, 0.42f));
        PlaceBox(band.rectTransform, bandBox);
        glow = Raw("Brillo", root, glowTex, new Color(accent.r, accent.g, accent.b, 0.1f));
        PlaceBox(glow.rectTransform, glowBox);
        lines = Raw("Lineas", root, linesTex, new Color(accent.r, accent.g, accent.b, 0.75f));
        PlaceBox(lines.rectTransform, linesBox);

        lane = Raw("CarrilCarga", root, laneTex, new Color(accent.r, accent.g, accent.b, 0.12f));
        PlaceBox(lane.rectTransform, laneBox);
        laneFill = NewRect("CargaRelleno", root).gameObject.AddComponent<Image>();
        laneFill.sprite = laneSprite;
        laneFill.type = Image.Type.Filled;
        laneFill.fillMethod = Image.FillMethod.Vertical;
        laneFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        laneFill.fillAmount = 0f;
        laneFill.raycastTarget = false;
        PlaceBox(laneFill.rectTransform, laneBox);

        tickRoot = NewRect("Marcas", root);
        cellRoot = NewRect("Celdas", root);

        numberGlow = Raw("BrilloNumero", root, FXFactory.SoftDot, new Color(accent.r, accent.g, accent.b, 0.16f));
        Place(numberGlow.rectTransform, NumX - 34f, NumY, 92f, 50f);

        numberText = Text("Numero", root, NumSize, MenuUI.TitleFont, 2f);
        PlaceText(numberText, NumX, NumY, 160f, 50f);
        maxText = Text("Maximo", root, MaxSize, MenuUI.BodyFont, 4f);
        PlaceText(maxText, NumX, MaxY, 160f, 18f);
        labelText = Text("Arma", root, LabelSize, MenuUI.BodyFont, 10f);
        PlaceText(labelText, NumX, LabelY, LabelW, 14f);
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 6.5f;
        labelText.fontSizeMax = LabelSize;

        labelLine = Raw("LineaArma", root, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.35f));
        Place(labelLine.rectTransform, NumX - LabelW * 0.5f, LabelY - 7.5f, LabelW, 1f);
        labelLineBright = Raw("LineaArmaBrillo", root, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.9f));
        Place(labelLineBright.rectTransform, NumX - 7f, LabelY - 7.5f, 14f, 1.6f);

        missileRoot = NewRect("Misiles", root);
        missileLabel = Text("MisilTexto", missileRoot, 7.5f, MenuUI.BodyFont, 8f);
        missileLabel.text = "MISIL";
        missileKey = Text("MisilTecla", missileRoot, 7.5f, MenuUI.BodyFont, 4f);
        missileKey.text = "[E]";
        missileRoot.gameObject.SetActive(false);

        shieldRoot = NewRect("Escudo", root);
        shieldTrack = Raw("Pista", shieldRoot, ringSprite.texture, new Color(accent.r, accent.g, accent.b, 0.18f));
        Place(shieldTrack.rectTransform, ShdX, ShdY, ShdSize * 1.45f * 2f, ShdSize * 1.45f * 2f);
        shieldRing = NewRect("Recarga", shieldRoot).gameObject.AddComponent<Image>();
        shieldRing.sprite = ringSprite;
        shieldRing.type = Image.Type.Filled;
        shieldRing.fillMethod = Image.FillMethod.Radial360;
        shieldRing.fillOrigin = (int)Image.Origin360.Top;
        shieldRing.fillClockwise = true;
        shieldRing.raycastTarget = false;
        Place(shieldRing.rectTransform, ShdX, ShdY, ShdSize * 1.45f * 2f, ShdSize * 1.45f * 2f);
        shieldBubble = Raw("Burbuja", shieldRoot, FXFactory.Bubble, accent);
        Place(shieldBubble.rectTransform, ShdX, ShdY, ShdSize, ShdSize);
        shieldLabel = Text("EscudoTexto", shieldRoot, 7.5f, MenuUI.BodyFont, 8f);
        PlaceText(shieldLabel, ShdX - ShdSize * 0.95f, ShdY, 90f, 12f);
        shieldRoot.gameObject.SetActive(false);

        if (shooting != null)
        {
            shooting.OnShot += HandleShot;
            shooting.OnAmmoPickup += HandlePickup;
            lastAmmo = shooting.currentAmmo;
        }
        Refresh(0f);
    }

    void OnDestroy()
    {
        if (shooting != null)
        {
            shooting.OnShot -= HandleShot;
            shooting.OnAmmoPickup -= HandlePickup;
        }
        if (hookedLauncher != null) hookedLauncher.Fired -= HandleMissileFired;
        if (hookedShield != null) hookedShield.Popped -= HandleShieldPopped;
    }

    void HandleShot(bool charged)
    {
        shotFlash = charged ? 0.35f : 0.18f;
        lastShotCharged = charged;
    }
    void HandlePickup(int amount) { pickupFlash = 0.6f; }
    void HandleMissileFired() { missileFlash = 0.4f; }
    void HandleShieldPopped() { shieldFlash = 0.8f; }

    void Update()
    {
        if (root == null) return;
        Refresh(Time.deltaTime);
    }

    // ======================= ACTUALIZACIÓN =======================

    void Refresh(float dt)
    {
        bool show = shooting != null && shooting.enabled && (player == null || !player.isDead);
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (!show) return;

        // Posición: pegado a la curva derecha del visor en primera persona, al borde en tercera
        RectTransform canvasRect = (RectTransform)root.parent;
        float cw = Mathf.Max(1f, canvasRect.rect.width);
        float ch = Mathf.Max(1f, canvasRect.rect.height);
        float s = Mathf.Clamp(ch / 450f, 0.72f, 1f);
        bool firstPerson = player == null || !player.isThirdPerson;
        float visorSize = visor != null ? visor.visorSize : 0.97f;
        float inset = firstPerson ? (1f - visorSize) * cw * 0.5f + FpMargin : TpInset;
        root.anchoredPosition = new Vector2(-inset, YOff * s);
        root.localScale = Vector3.one * s;

        int maxAmmo = Mathf.Max(1, shooting.maxAmmo);
        int ammo = Mathf.Clamp(shooting.currentAmmo, 0, maxAmmo);
        if (maxAmmo != builtMax) BuildCells(maxAmmo);
        if (ammo > lastAmmo) pickupFlash = Mathf.Max(pickupFlash, 0.15f);   // recarga en el agua: brillo suave
        lastAmmo = ammo;

        shotFlash = Mathf.Max(0f, shotFlash - dt);
        pickupFlash = Mathf.Max(0f, pickupFlash - dt);

        bool low = ammo <= 5;
        bool empty = ammo <= 0;
        bool blinkOn = !low || Mathf.Repeat(Time.time * 4f, 1f) < 0.6f;
        Color baseC = low ? danger : accent;
        float frac = ammo / (float)maxAmmo;
        float pick = pickupFlash / 0.6f;

        glow.color = new Color(baseC.r, baseC.g, baseC.b, 0.10f + 0.10f * frac + 0.35f * pick);
        lines.color = new Color(accent.r, accent.g, accent.b, 0.75f);

        // Carril de carga
        bool charging = shooting.IsCharging;
        float charge = charging ? shooting.ChargeProgress01 : 0f;
        laneFill.fillAmount = charge;
        bool full = charge >= 1f;
        laneFill.color = full ? Color.Lerp(accent, Color.white, Mathf.Sin(Time.time * 25f) * 0.5f + 0.5f) : new Color(accent.r, accent.g, accent.b, 0.95f);

        // Celdas: se apagan de arriba hacia abajo
        int n = cells.Count;
        int top = Mathf.CeilToInt(ammo / (float)bulletsPerCell) - 1;
        int costCells = Mathf.CeilToInt(Mathf.Max(1, shooting.chargedAmmoCost) / (float)bulletsPerCell);
        int spentCells = lastShotCharged ? costCells : 1;
        for (int i = 0; i < n; i++)
        {
            float v = Mathf.Clamp(ammo - i * bulletsPerCell, 0, bulletsPerCell) / (float)bulletsPerCell;
            Color c;
            if (v > 0f)
            {
                float a = blinkOn ? 0.35f + 0.6f * v : 0.3f;
                c = new Color(baseC.r, baseC.g, baseC.b, a);
                if (charging && i > top - costCells) c = new Color(0.85f, 1f, 1f, a);
                if (pick > 0f) c = Color.Lerp(c, Color.white, pick * 0.5f);
            }
            else
            {
                c = new Color(accent.r, accent.g, accent.b, 0.12f);
                // La celda que se acaba de gastar destella y se apaga
                if (shotFlash > 0f && i >= top + 1 && i <= top + spentCells) c = Color.Lerp(c, Color.white, shotFlash / 0.35f);
            }
            cells[i].color = c;
        }

        // Textos
        if (ammo != shownAmmo)
        {
            numberText.text = ammo.ToString();
            shownAmmo = ammo;
        }
        Color numC = low ? danger : accent;
        numberText.color = new Color(numC.r, numC.g, numC.b, blinkOn ? 1f : 0.35f);
        numberText.rectTransform.localScale = Vector3.one * (1f + pick * 0.12f);
        numberGlow.color = new Color(numC.r, numC.g, numC.b, 0.16f + 0.2f * pick);

        if (empty != shownEmpty || maxAmmo != shownMax)
        {
            maxText.text = empty ? "SIN MUNICIÓN" : "/ " + maxAmmo;
            maxText.fontSize = empty ? 10f : MaxSize;
            maxText.characterSpacing = empty ? 8f : 4f;
            shownEmpty = empty;
            shownMax = maxAmmo;
        }
        maxText.color = empty ? new Color(danger.r, danger.g, danger.b, blinkOn ? 1f : 0.3f) : new Color(accent.r, accent.g, accent.b, 0.8f);

        string weapon = shooting.CurrentWeaponName;
        if (weapon != shownWeapon)
        {
            labelText.text = weapon;
            shownWeapon = weapon;
        }
        labelText.color = new Color(accent.r, accent.g, accent.b, 0.95f);

        RefreshMissiles(dt);
        RefreshShield(dt);
    }

    void RefreshMissiles(float dt)
    {
        MissileLauncher ml = MissileLauncher.Instance;
        if (ml != hookedLauncher)
        {
            if (hookedLauncher != null) hookedLauncher.Fired -= HandleMissileFired;
            hookedLauncher = ml;
            if (ml != null) ml.Fired += HandleMissileFired;
        }
        bool show = ml != null && ml.isActiveAndEnabled && ml.Unlocked && ml.Capacity > 0;
        if (missileRoot.gameObject.activeSelf != show) missileRoot.gameObject.SetActive(show);
        if (!show) return;

        int cap = Mathf.Min(ml.Capacity, 6);
        if (cap != builtCapacity) BuildMissiles(cap);
        missileFlash = Mathf.Max(0f, missileFlash - dt);
        float flash = missileFlash / 0.4f;
        for (int j = 0; j < cap; j++)
        {
            missileOutlines[j].color = new Color(accent.r, accent.g, accent.b, 0.55f);
            Image fill = missileFills[j];
            if (j < ml.Loaded)
            {
                fill.fillAmount = 1f;
                fill.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            }
            else if (j == ml.Loaded)
            {
                fill.fillAmount = Mathf.Clamp01(ml.Reload01);
                fill.color = new Color(accent.r, accent.g, accent.b, 0.45f);
            }
            else
            {
                fill.fillAmount = 0f;
            }
            // El misil que acaba de salir destella en blanco
            if (flash > 0f && j == ml.Loaded) missileOutlines[j].color = Color.Lerp(missileOutlines[j].color, Color.white, flash);
        }
        float keyA = ml.Loaded > 0 ? 1f : 0.4f;
        missileKey.color = new Color(accent.r, accent.g, accent.b, keyA);
        missileLabel.color = new Color(accent.r, accent.g, accent.b, 0.85f);
    }

    void RefreshShield(float dt)
    {
        BubbleShield bs = BubbleShield.Instance;
        if (bs != hookedShield)
        {
            if (hookedShield != null) hookedShield.Popped -= HandleShieldPopped;
            hookedShield = bs;
            if (bs != null) bs.Popped += HandleShieldPopped;
        }
        bool show = bs != null && bs.isActiveAndEnabled && bs.Unlocked;
        if (shieldRoot.gameObject.activeSelf != show) shieldRoot.gameObject.SetActive(show);
        if (!show) return;

        shieldFlash = Mathf.Max(0f, shieldFlash - dt);
        bool ready = bs.Ready;
        float r01 = ready ? 1f : Mathf.Clamp01(bs.Recharge01);
        shieldRing.fillAmount = r01;
        Color c = shieldFlash > 0f && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? danger : accent;
        shieldRing.color = new Color(c.r, c.g, c.b, 0.9f);
        shieldBubble.color = new Color(c.r, c.g, c.b, ready ? 0.95f : 0.35f);
        shieldBubble.rectTransform.localScale = Vector3.one * (ready ? 1f + Mathf.Sin(Time.time * 3f) * 0.04f : 0.85f);
        shieldLabel.text = ready ? "ESCUDO" : "ESCUDO " + Mathf.FloorToInt(r01 * 100f) + "%";
        shieldLabel.color = new Color(accent.r, accent.g, accent.b, ready ? 0.8f : 0.5f);
    }

    // ======================= CONSTRUCCIÓN =======================

    void BuildCells(int maxAmmo)
    {
        builtMax = maxAmmo;
        bulletsPerCell = Mathf.CeilToInt(maxAmmo / (float)MaxCells);
        int n = Mathf.CeilToInt(maxAmmo / (float)bulletsPerCell);

        float step = 2f * Half / n;
        float width = 2f * R * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad) - CellGap;
        float rc = R - CellLen * 0.5f;
        for (int i = 0; i < n; i++)
        {
            RawImage cell;
            if (i < cells.Count) cell = cells[i];
            else
            {
                cell = Raw("Celda" + i, cellRoot, FXFactory.White, accent);
                cells.Add(cell);
            }
            cell.gameObject.SetActive(true);
            float phi = -Half + (i + 0.5f) * step;
            float rad = phi * Mathf.Deg2Rad;
            RectTransform rt = cell.rectTransform;
            rt.anchoredPosition = new Vector2(-R + rc * Mathf.Cos(rad), rc * Mathf.Sin(rad));
            rt.sizeDelta = new Vector2(CellLen, Mathf.Max(0.6f, width));
            rt.localEulerAngles = new Vector3(0f, 0f, phi);
        }
        for (int i = n; i < cells.Count; i++)
        {
            Destroy(cells[i].gameObject);
        }
        if (cells.Count > n) cells.RemoveRange(n, cells.Count - n);

        // Marcas cada 10 balas por fuera del arco
        foreach (RawImage t in ticks) Destroy(t.gameObject);
        ticks.Clear();
        for (int b = 10; b < maxAmmo; b += 10)
        {
            float phi = -Half + b / (float)maxAmmo * 2f * Half;
            float rad = phi * Mathf.Deg2Rad;
            float rm = R + GuideOff + TickLen * 0.5f;
            RawImage t = Raw("Marca" + b, tickRoot, FXFactory.White, new Color(accent.r, accent.g, accent.b, 0.75f));
            t.rectTransform.anchoredPosition = new Vector2(-R + rm * Mathf.Cos(rad), rm * Mathf.Sin(rad));
            t.rectTransform.sizeDelta = new Vector2(TickLen, TickT);
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, phi);
            ticks.Add(t);
        }
    }

    void BuildMissiles(int capacity)
    {
        builtCapacity = capacity;
        foreach (RawImage r in missileOutlines) Destroy(r.gameObject);
        foreach (Image i in missileFills) Destroy(i.gameObject);
        missileOutlines.Clear();
        missileFills.Clear();
        for (int j = 0; j < capacity; j++)
        {
            float cx = MslX - (capacity - 1 - j) * MslPitch;
            Image fill = NewRect("Misil" + j, missileRoot).gameObject.AddComponent<Image>();
            fill.sprite = missileSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.raycastTarget = false;
            Place(fill.rectTransform, cx, MslY, MslW, MslH);
            missileFills.Add(fill);
            RawImage outline = Raw("MisilBorde" + j, missileRoot, missileOutlineTex, accent);
            Place(outline.rectTransform, cx, MslY, MslW, MslH);
            missileOutlines.Add(outline);
        }
        float left = MslX - (capacity - 1) * MslPitch - MslW * 0.5f - 5f;
        PlaceText(missileLabel, left, MslY + 3.5f, 60f, 10f);
        PlaceText(missileKey, left, MslY - 5f, 60f, 10f);
    }

    // ======================= AYUDAS =======================

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    static RawImage Raw(string name, Transform parent, Texture tex, Color color)
    {
        RawImage img = NewRect(name, parent).gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Place(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void PlaceBox(RectTransform rt, Rect box)
    {
        Place(rt, box.center.x, box.center.y, box.width, box.height);
    }

    TextMeshProUGUI Text(string name, Transform parent, float size, TMP_FontAsset font, float spacing)
    {
        TextMeshProUGUI t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.characterSpacing = spacing;
        t.alignment = TextAlignmentOptions.Right;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.color = accent;
        t.text = "";
        return t;
    }

    // Texto alineado a la derecha: el borde derecho de la caja queda en (x, y)
    static void PlaceText(TextMeshProUGUI t, float x, float y, float w, float h)
    {
        RectTransform rt = t.rectTransform;
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ======================= TEXTURAS PROCEDURALES =======================
    // Se dibujan en coordenadas del widget: el arco tiene su centro en (-R, 0).

    delegate float ArcAlpha(float r, float ang);

    static void MakeTextures()
    {
        if (bandTex != null) return;
        float l = CellLen;
        bandTex = ArcTexture(R - l - BandIn, R + BandOut, Half + 3.5f, 2f, (r, a) => BandAlpha(r, a, R - l - BandIn, R + BandOut, BandSoft), "FX_VisorBanda", out bandBox);
        glowTex = ArcTexture(R - l - BandIn - 8f, R + BandOut + 8f, Half + 5f, 1.5f, (r, a) => BandAlpha(r, a, R - l - BandIn - 6f, R + BandOut + 6f, 12f), "FX_VisorBrillo", out glowBox);
        linesTex = ArcTexture(R - l - CapIn - 2f, R + CapOut + 2f, Half + CapExtra + 1f, 4f, LinesAlpha, "FX_VisorLineas", out linesBox);
        float lr = R - l - LaneOff;
        laneTex = ArcTexture(lr - 2.5f, lr + 2.5f, Half, 4f, LaneAlpha, "FX_VisorCarril", out laneBox);
        laneSprite = FXFactory.SpriteFrom(laneTex);
        MakeMissileTextures();
        missileSprite = FXFactory.SpriteFrom(missileTex);
        Texture2D ring = FXFactory.MakeRadial(128, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.07f), "FX_VisorAnillo");
        ringSprite = FXFactory.SpriteFrom(ring);
    }

    static Texture2D ArcTexture(float rIn, float rOut, float halfDeg, float ppu, ArcAlpha fn, string name, out Rect box)
    {
        float a = halfDeg * Mathf.Deg2Rad;
        float x0 = -R + rIn * Mathf.Cos(a), x1 = -R + rOut;
        float y1 = rOut * Mathf.Sin(a), y0 = -y1;
        int w = Mathf.CeilToInt((x1 - x0) * ppu), h = Mathf.CeilToInt((y1 - y0) * ppu);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.name = name;
        Color32[] px = new Color32[w * h];
        for (int j = 0; j < h; j++)
        {
            float y = y0 + (j + 0.5f) / h * (y1 - y0);
            for (int i = 0; i < w; i++)
            {
                float x = x0 + (i + 0.5f) / w * (x1 - x0);
                float r = Mathf.Sqrt((x + R) * (x + R) + y * y);
                float ang = Mathf.Atan2(y, x + R) * Mathf.Rad2Deg;
                px[j * w + i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(fn(r, ang)) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        box = new Rect(x0, y0, x1 - x0, y1 - y0);
        return tex;
    }

    static float BandAlpha(float r, float ang, float rin, float rout, float soft)
    {
        float radial = Mathf.Clamp01((r - rin) / soft) * Mathf.Clamp01((rout - r) / soft);
        radial = radial * radial * (3f - 2f * radial);
        float end = Mathf.Clamp01((Half + 3f - Mathf.Abs(ang)) / 4f);
        return radial * end;
    }

    static float Line(float dist, float thick)
    {
        const float aa = 0.6f;
        return Mathf.Clamp01((thick * 0.5f + aa * 0.5f - dist) / aa);
    }

    static float LinesAlpha(float r, float ang)
    {
        float capA = Half + CapExtra;
        float absA = Mathf.Abs(ang);
        // Línea guía exterior
        float guide = absA <= capA ? Line(Mathf.Abs(r - (R + GuideOff)), GuideT) : 0f;
        // Topes: línea radial en cada punta + "pies" hacia adentro y hacia afuera
        float rin = R - CellLen - CapIn, rout = R + CapOut;
        float distCap = r * Mathf.Abs(Mathf.Sin((absA - capA) * Mathf.Deg2Rad));
        float cap = (r >= rin && r <= rout && absA <= capA + 0.5f) ? Line(distCap, CapT) : 0f;
        bool footZone = absA <= capA && absA >= capA - CapFoot;
        float footIn = footZone ? Line(Mathf.Abs(r - rin), CapT) : 0f;
        float footOut = footZone ? Line(Mathf.Abs(r - rout), CapT) : 0f;
        return Mathf.Max(Mathf.Max(guide * 0.55f, cap), Mathf.Max(footIn, footOut));
    }

    static float LaneAlpha(float r, float ang)
    {
        if (ang < -Half || ang > Half) return 0f;
        float lr = R - CellLen - LaneOff;
        float radial = Line(Mathf.Abs(r - lr), LaneT);
        float t = (ang + Half) / (2f * Half) * LaneDashes;
        float frac = t - Mathf.Floor(t);
        return frac > 0.12f && frac < 0.88f ? radial : 0f;
    }

    // Silueta de misil (cono, cuerpo y aletas) y su contorno
    static void MakeMissileTextures()
    {
        const int W = 32, H = 80;
        bool[] solid = new bool[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;              // 0 abajo, 1 arriba
            for (int x = 0; x < W; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / W * 2f - 1f);
                bool body = u < 0.42f && v > 0.12f && v < 0.72f;
                bool nose = v >= 0.72f && v < 0.98f && u < 0.42f * (0.98f - v) / 0.26f;
                bool fins = v > 0f && v < 0.34f && u > 0.2f && u < 0.42f + 0.58f * (0.34f - v) / 0.34f;
                solid[y * W + x] = body || nose || fins;
            }
        }
        missileTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        missileOutlineTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        missileTex.wrapMode = missileOutlineTex.wrapMode = TextureWrapMode.Clamp;
        missileTex.name = "FX_Misil";
        missileOutlineTex.name = "FX_MisilBorde";
        Color32[] fill = new Color32[W * H];
        Color32[] edge = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                bool s = solid[y * W + x];
                bool border = false;
                if (s)
                {
                    for (int dy = -2; dy <= 2 && !border; dy++)
                        for (int dx = -2; dx <= 2 && !border; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H || !solid[yy * W + xx]) border = true;
                        }
                }
                fill[y * W + x] = new Color32(255, 255, 255, (byte)(s ? 255 : 0));
                edge[y * W + x] = new Color32(255, 255, 255, (byte)(border ? 255 : 0));
            }
        }
        missileTex.SetPixels32(fill);
        missileTex.Apply();
        missileOutlineTex.SetPixels32(edge);
        missileOutlineTex.Apply();
    }
}
