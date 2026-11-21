using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Efecto de los disparos que dan en un ave (lo llama BirdAI). Las armas del traje son de agua, así que cada golpe
// revienta como un globo de agua:
//  - Gotas que salen disparadas y caen (estiradas por la velocidad) y una nube de rocío que se abre
//  - Plumas que saltan del color de la especie (más y más grandes en los golpes fuertes)
//  - Anillo de impacto azul eléctrico, un destello y chispas de agua en los golpes fuertes
//  - Número de daño que salta y se desvanece (naranja y más grande en los golpes fuertes; "x2" si el jefe está
//    vulnerable; "BLINDADA" la primera vez que le das a un ave que no se mueve con los disparos)
//  - Al morir: EXPLOSIÓN DE AGUA (muchas gotas, rocío, dos ondas, plumas), un sacudón de cámara si estás cerca y una
//    pausa cortísima (hit-stop) que hace sentir el golpe. Los jefes, más grande.
// Optimizado: todo sale de 5 sistemas de partículas que se crean una sola vez (nada se instancia por golpe); cuanto más
// lejos de la cámara, menos partículas (y nada a más de 140 m); y hay un tope de partículas por cuadro, así una ráfaga
// larga de la metralleta no dispara miles de golpe.
// El destello del cuerpo y el tambaleo los hace BirdAI. Se crea solo la primera vez que hace falta (uno por escena).
public class HitFX : MonoBehaviour
{
    static HitFX instance;

    ParticleSystem feathers, rings, flashes, drops, mist;
    RectTransform canvasRect;
    readonly List<Number> numbers = new List<Number>();
    Camera cam;

    static readonly Color Electric = new Color(0.25f, 0.72f, 1f, 1f);
    static readonly Color Glow = new Color(0.75f, 0.93f, 1f, 1f);
    static readonly Color Strong = new Color(1f, 0.62f, 0.2f, 1f);
    static readonly Color Bonus = new Color(1f, 0.32f, 0.22f, 1f);
    static readonly Color Water = new Color(0.72f, 0.9f, 1f, 0.9f);
    static readonly Color Foam = new Color(0.96f, 0.99f, 1f, 0.95f);
    static readonly Color Spray = new Color(0.86f, 0.95f, 1f, 0.4f);

    // Tope de partículas nuevas por cuadro (todas las clases juntas)
    const int FrameBudget = 240;
    int budgetFrame = -1;
    int budgetUsed;
    // Hit-stop: cuánto se frena el tiempo y cada cuánto puede volver a pasar
    const float HitStopScale = 0.06f;
    float lastHitStop = -10f;

    class Number
    {
        public TextMeshProUGUI text;
        public Vector3 world;
        public Vector2 drift;
        public float age, life;
        public bool active;
    }

    static HitFX Get()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("EfectosDeImpacto");
            instance = go.AddComponent<HitFX>();
            instance.Build();
        }
        return instance;
    }

    // ======================= LLAMADAS =======================

    // Un disparo dio en un ave. damage = daño de este disparo; shownNumber = número a mostrar (0 = no mostrar:
    // los disparos seguidos se suman en uno solo); strong = golpe fuerte (cargado, misil); bonus = jefe vulnerable
    public static void BirdHit(Vector3 point, Vector3 from, int damage, int shownNumber, float size, Color featherColor,
                               bool strong, bool bonus, bool armored)
    {
        HitFX fx = Get();
        Vector3 dir = point - from;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
        bool weak = damage < 6 && !strong;
        float k = fx.Detail(point);
        if (k > 0f)
        {
            float s = Mathf.Max(0.7f, size);
            fx.EmitDrops(point, dir, fx.Take(k, strong ? 28 : (weak ? 7 : 14)), s * (strong ? 1.25f : 1f), strong ? 10f : 7f);
            fx.EmitMist(point, fx.Take(k, strong ? 4 : (weak ? 1 : 2)), s * (strong ? 1.3f : 0.85f));
            fx.EmitFeathers(point, dir, fx.Take(k, strong ? 14 : (weak ? 2 : 7)), size * (strong ? 1.3f : 1f), featherColor, strong ? 7f : 4.5f);
            fx.EmitRing(point, (strong ? 2.6f : (weak ? 1f : 1.6f)) * s, armored ? Glow : Electric, 0.28f);
            fx.EmitFlash(point, (strong ? 1.8f : (weak ? 0.7f : 1.1f)) * s, bonus ? Strong : Glow);
            if (strong)
            {
                fx.EmitGlints(point, fx.Take(k, 8), s);
                fx.Kick(point, 0.6f);
            }
        }
        if (shownNumber <= 0) return;
        string label = bonus ? shownNumber + " <size=70%>x2</size>" : shownNumber.ToString();
        Color col = bonus ? Bonus : (strong ? Strong : (armored ? Glow : Color.white));
        fx.ShowNumber(point, label, col, strong || bonus ? 1.45f : 1f);
    }

    // Texto corto sobre un ave (por ejemplo "BLINDADA")
    public static void BirdLabel(Vector3 point, string text, Color color)
    {
        Get().ShowNumber(point + Vector3.up * 0.6f, text, color, 0.75f);
    }

    // Plumas sueltas que va perdiendo un ave herida mientras huye (sin aro, destello ni número)
    public static void FeatherTrail(Vector3 point, Vector3 flyDir, float size, Color featherColor)
    {
        Get().EmitFeathers(point, -flyDir, 1, size, featherColor, 1.5f);
    }

    // Ave derribada: explosión de agua (los jefes, el doble)
    public static void BirdDeath(Vector3 point, float size, Color featherColor, bool boss = false)
    {
        HitFX fx = Get();
        float k = fx.Detail(point);
        float s = Mathf.Max(0.8f, size) * (boss ? 1.5f : 1f);
        if (k > 0f)
        {
            fx.EmitDrops(point, Vector3.up, fx.Take(k, boss ? 130 : 64), s * 1.3f, boss ? 14f : 11f);
            fx.EmitMist(point, fx.Take(k, boss ? 16 : 8), s * 1.7f);
            fx.EmitFeathers(point, Vector3.up, fx.Take(k, boss ? 40 : 26), size * 1.2f, featherColor, 8f);
            fx.EmitGlints(point, fx.Take(k, boss ? 24 : 14), s);
        }
        // La onda y el destello se ven de lejos (son una partícula cada uno)
        fx.EmitRing(point, 3.2f * s, Electric, 0.28f);
        fx.EmitRing(point, 6.0f * s, Glow, 0.55f);
        fx.EmitFlash(point, 2.4f * s, Glow);
        fx.Kick(point, boss ? 3f : 1.4f);
        if (!boss) fx.HitStop(point, 0.05f);
    }

    // ======================= PARTÍCULAS =======================

    void Build()
    {
        Material template = null;
        PlayerShooting ps = FindFirstObjectByType<PlayerShooting>();
        if (ps != null) template = ps.fxMaterialTemplate;

        feathers = FXFactory.CreateParticleSystem("Plumas", transform, FXFactory.ParticleMaterialAlpha(template, FeatherTexture(), Color.white));
        var main = feathers.main;
        main.maxParticles = 600;
        main.gravityModifier = 0.35f;
        var limit = feathers.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1.2f;
        limit.dampen = 0.12f;
        var rot = feathers.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        var fade = feathers.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(FadeGradient(0.75f));
        var noise = feathers.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.6f;

        rings = FXFactory.CreateParticleSystem("Anillos", transform, FXFactory.ParticleMaterial(template, FXFactory.Ring, Color.white));
        var rm = rings.main;
        rm.maxParticles = 60;
        var rs = rings.sizeOverLifetime;
        rs.enabled = true;
        rs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1f, 0.3f, 0f)));
        var rc = rings.colorOverLifetime;
        rc.enabled = true;
        rc.color = new ParticleSystem.MinMaxGradient(FadeGradient(0f));

        flashes = FXFactory.CreateParticleSystem("Destellos", transform, FXFactory.ParticleMaterial(template, FXFactory.SoftDot, Color.white));
        var fm = flashes.main;
        fm.maxParticles = 60;
        var fs = flashes.sizeOverLifetime;
        fs.enabled = true;
        fs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
        var fc = flashes.colorOverLifetime;
        fc.enabled = true;
        fc.color = new ParticleSystem.MinMaxGradient(FadeGradient(0f));

        // Gotas: se estiran con la velocidad (parecen chorritos) y caen con gravedad
        drops = FXFactory.CreateParticleSystem("Gotas", transform, FXFactory.ParticleMaterialAlpha(template, FXFactory.SoftDot, Color.white));
        var dm = drops.main;
        dm.maxParticles = 700;
        dm.gravityModifier = 1.3f;
        var dEm = drops.emission;
        dEm.enabled = false;
        var ds = drops.sizeOverLifetime;
        ds.enabled = true;
        ds.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.45f)));
        var dc = drops.colorOverLifetime;
        dc.enabled = true;
        dc.color = new ParticleSystem.MinMaxGradient(FadeGradient(0.7f));
        ParticleSystemRenderer dr = drops.GetComponent<ParticleSystemRenderer>();
        dr.renderMode = ParticleSystemRenderMode.Stretch;
        dr.velocityScale = 0.05f;
        dr.lengthScale = 1.6f;

        // Rocío: nubecitas que se abren y se desvanecen
        mist = FXFactory.CreateParticleSystem("Rocio", transform, FXFactory.ParticleMaterialAlpha(template, FXFactory.SoftDot, Color.white));
        var mm = mist.main;
        mm.maxParticles = 160;
        var mEm = mist.emission;
        mEm.enabled = false;
        var ms = mist.sizeOverLifetime;
        ms.enabled = true;
        ms.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.7f, 0.6f, 0f)));
        var mc = mist.colorOverLifetime;
        mc.enabled = true;
        mc.color = new ParticleSystem.MinMaxGradient(FadeGradient(0.25f));
        var ml = mist.limitVelocityOverLifetime;
        ml.enabled = true;
        ml.limit = 0.4f;
        ml.dampen = 0.15f;

        BuildCanvas();
    }

    // ======================= DETALLE SEGÚN DISTANCIA Y TOPE POR CUADRO =======================

    // 1 = efecto completo; menos con la distancia a la cámara; 0 = muy lejos (no vale la pena)
    float Detail(Vector3 point)
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return 1f;
        Vector3 v = point - cam.transform.position;
        float d = v.magnitude;
        if (d > 140f) return 0f;
        float k = d < 25f ? 1f : Mathf.Lerp(1f, 0.2f, (d - 25f) / 115f);
        // Detrás de la cámara casi no se ve: lo mínimo
        if (Vector3.Dot(v, cam.transform.forward) < 0f) k *= 0.25f;
        return k;
    }

    // Cuántas partículas se pueden emitir de las 'wanted' (con el detalle k y el tope del cuadro)
    int Take(float k, int wanted)
    {
        if (budgetFrame != Time.frameCount)
        {
            budgetFrame = Time.frameCount;
            budgetUsed = 0;
        }
        int n = Mathf.Min(Mathf.CeilToInt(wanted * k), FrameBudget - budgetUsed);
        if (n <= 0) return 0;
        budgetUsed += n;
        return n;
    }

    // ======================= CÁMARA: SACUDÓN Y HIT-STOP =======================

    PlayerController_Base localPlayer;

    void Kick(Vector3 point, float strength)
    {
        if (localPlayer == null) localPlayer = PlayerController_Base.Local != null ? PlayerController_Base.Local : PlayerController_Base.FindLocal();
        if (localPlayer == null) return;
        float d = Vector3.Distance(point, localPlayer.transform.position);
        if (d > 35f) return;
        localPlayer.AddRecoil(0f, strength * Mathf.Lerp(1f, 0.25f, d / 35f));
    }

    // Una pausa cortísima al derribar un ave cerca: el golpe "pesa". Nunca en cooperativo (cada uno tiene su tiempo),
    // en pausa, cargando ni si otra cosa ya cambió la escala de tiempo (cámara lenta del final)
    void HitStop(Vector3 point, float seconds)
    {
        if (seconds <= 0f || Time.unscaledTime - lastHitStop < 0.35f) return;
        if (!Mathf.Approximately(Time.timeScale, 1f) || PauseMenu.IsPaused || LoadingScreen.IsLoading || CoopSession.InGame) return;
        if (cam == null || (point - cam.transform.position).sqrMagnitude > 30f * 30f) return;
        lastHitStop = Time.unscaledTime;
        StartCoroutine(HitStopRoutine(seconds));
    }

    IEnumerator HitStopRoutine(float seconds)
    {
        Time.timeScale = HitStopScale;
        yield return new WaitForSecondsRealtime(seconds);
        // Solo vuelve si nadie la cambió mientras tanto (pausa, cámara lenta del final de la partida...)
        if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
    }

    // Opaco hasta 'keepUntil' (fracción de la vida) y luego se desvanece
    static Gradient FadeGradient(float keepUntil)
    {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Max(0.01f, keepUntil)), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    void EmitFeathers(Vector3 point, Vector3 dir, int count, float size, Color color, float speed)
    {
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            Vector3 v = (dir * 0.8f + Random.insideUnitSphere + Vector3.up * 0.5f).normalized * speed * Random.Range(0.4f, 1f);
            ep.position = point + Random.insideUnitSphere * 0.2f * size;
            ep.velocity = v;
            ep.startSize = Random.Range(0.22f, 0.42f) * Mathf.Max(0.6f, size);
            ep.startLifetime = Random.Range(1.1f, 2.1f);
            ep.rotation = Random.Range(0f, 360f);
            // Cada pluma un poco distinta (más clara u oscura)
            float k = Random.Range(0.8f, 1.15f);
            ep.startColor = new Color(Mathf.Clamp01(color.r * k), Mathf.Clamp01(color.g * k), Mathf.Clamp01(color.b * k), 1f);
            feathers.Emit(ep, 1);
        }
    }

    void EmitRing(Vector3 point, float size, Color color, float life)
    {
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = point;
        ep.velocity = Vector3.zero;
        ep.startSize = size;
        ep.startLifetime = life;
        ep.startColor = color;
        rings.Emit(ep, 1);
    }

    // Gotas que revientan: salen hacia donde iba el disparo, hacia los lados y un poco hacia arriba, y caen
    void EmitDrops(Vector3 point, Vector3 dir, int count, float size, float speed)
    {
        if (count <= 0) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            Vector3 v = (dir * 0.55f + Random.onUnitSphere + Vector3.up * 0.45f).normalized * speed * Random.Range(0.4f, 1f);
            ep.position = point + Random.insideUnitSphere * 0.15f * size;
            ep.velocity = v;
            ep.startSize = Random.Range(0.05f, 0.13f) * size;
            ep.startLifetime = Random.Range(0.45f, 0.9f);
            ep.startColor = Random.value < 0.25f ? Foam : Water;
            drops.Emit(ep, 1);
        }
    }

    // Nube de rocío que se abre
    void EmitMist(Vector3 point, int count, float size)
    {
        if (count <= 0) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = point + Random.insideUnitSphere * 0.3f * size;
            ep.velocity = Random.insideUnitSphere * 1.4f + Vector3.up * 0.6f;
            ep.startSize = Random.Range(0.9f, 1.6f) * size;
            ep.startLifetime = Random.Range(0.45f, 0.85f);
            ep.rotation = Random.Range(0f, 360f);
            ep.startColor = Spray;
            mist.Emit(ep, 1);
        }
    }

    // Chispas de agua: puntos brillantes rápidos y cortos (con el sistema de destellos)
    void EmitGlints(Vector3 point, int count, float size)
    {
        if (count <= 0) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = point;
            ep.velocity = Random.onUnitSphere * Random.Range(6f, 11f) + Vector3.up * 1.5f;
            ep.startSize = Random.Range(0.12f, 0.24f) * size;
            ep.startLifetime = Random.Range(0.18f, 0.32f);
            ep.startColor = Glow;
            flashes.Emit(ep, 1);
        }
    }

    void EmitFlash(Vector3 point, float size, Color color)
    {
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = point;
        ep.velocity = Vector3.zero;
        ep.startSize = size;
        ep.startLifetime = 0.12f;
        ep.startColor = color;
        flashes.Emit(ep, 1);
    }

    // Pluma: hoja alargada con el cañón en el centro
    static Texture2D featherTex;
    static Texture2D FeatherTexture()
    {
        if (featherTex != null) return featherTex;
        const int W = 32, H = 64;
        featherTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        featherTex.wrapMode = TextureWrapMode.Clamp;
        featherTex.name = "FX_Pluma";
        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;                 // 0 abajo (cañón) .. 1 punta
            float half = Mathf.Sin(Mathf.Clamp01(v * 1.05f) * Mathf.PI) * 0.42f * (0.6f + 0.4f * v);
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W - 0.5f;
                float edge = Mathf.Clamp01((half - Mathf.Abs(u)) * W * 0.6f);
                float shaft = Mathf.Clamp01(1f - Mathf.Abs(u) * W * 0.7f) * (v < 0.95f ? 1f : 0f);
                float barb = 0.85f + 0.15f * Mathf.Sin(v * 40f + Mathf.Abs(u) * 30f);   // barbas: rayitas
                float a = Mathf.Max(edge * barb, shaft * 0.9f * (v < 0.25f ? 1f : edge));
                byte c = (byte)Mathf.RoundToInt(255f * (shaft > 0.5f ? 0.85f : 1f));
                px[y * W + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a)));
            }
        }
        featherTex.SetPixels32(px);
        featherTex.Apply();
        return featherTex;
    }

    // ======================= NÚMEROS DE DAÑO =======================

    void BuildCanvas()
    {
        GameObject go = new GameObject("NumerosDeDanio", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)go.transform;
    }

    void ShowNumber(Vector3 world, string label, Color color, float scale)
    {
        Number n = null;
        foreach (Number existing in numbers)
            if (!existing.active) { n = existing; break; }
        if (n == null)
        {
            if (numbers.Count >= 40) return;
            n = new Number();
            GameObject go = new GameObject("Numero", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            n.text = go.AddComponent<TextMeshProUGUI>();
            if (MenuUI.TitleFont != null) n.text.font = MenuUI.TitleFont;
            n.text.alignment = TextAlignmentOptions.Center;
            n.text.textWrappingMode = TextWrappingModes.NoWrap;
            n.text.raycastTarget = false;
            n.text.richText = true;
            n.text.outlineWidth = 0.22f;
            n.text.outlineColor = new Color32(4, 12, 34, 230);
            ((RectTransform)go.transform).sizeDelta = new Vector2(220f, 60f);
            numbers.Add(n);
        }
        n.active = true;
        n.world = world;
        n.drift = new Vector2(Random.Range(-28f, 28f), Random.Range(50f, 75f));
        n.age = 0f;
        n.life = 0.85f;
        n.text.text = label;
        n.text.color = color;
        n.text.fontSize = 30f * scale;
        n.text.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        if (numbers.Count == 0) return;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        float dt = Time.deltaTime;
        foreach (Number n in numbers)
        {
            if (!n.active) continue;
            n.age += dt;
            float t = n.age / n.life;
            if (t >= 1f || cam == null)
            {
                n.active = false;
                n.text.gameObject.SetActive(false);
                continue;
            }
            Vector3 sp = cam.WorldToScreenPoint(n.world);
            if (sp.z <= 0.1f)
            {
                n.text.gameObject.SetActive(false);
                continue;
            }
            n.text.gameObject.SetActive(true);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
            float rise = 1f - (1f - t) * (1f - t);
            RectTransform rt = n.text.rectTransform;
            rt.anchoredPosition = local + n.drift * rise;
            // Salta grande y se asienta; se desvanece al final. Más chico cuanto más lejos
            float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
            float dist = Mathf.Clamp(sp.z, 4f, 60f);
            rt.localScale = Vector3.one * pop * Mathf.Lerp(1.15f, 0.65f, (dist - 4f) / 56f);
            Color c = n.text.color;
            c.a = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
            n.text.color = c;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        // Si la escena se va en medio de un hit-stop, el tiempo no se queda frenado
        if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
    }
}
