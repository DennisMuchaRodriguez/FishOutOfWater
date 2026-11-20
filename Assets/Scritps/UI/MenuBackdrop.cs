using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
//  FONDO DEL MENÚ PRINCIPAL: el Taller con una cinemática
//  MainMenu carga la escena del Taller de fondo (aditiva) y WorkshopScene agrega este componente
//  (WorkshopFlow.RequestMenuBackdrop). Todo se arma por código con lo que ya tiene el Taller.
//  - PANTALLA DE TÍTULO: una cinemática que se repite (unos 37 s):
//      1. La guarida: la cámara baja entre los rayos de luz, con la sala de armaduras al fondo.
//      2. El mecánico (el bagre) prepara el soporte mientras el brazo robótico suelda.
//      3. Llega el pez, sin traje, nadando hasta el soporte.
//      4. Las piezas del traje giran a su alrededor y se le encajan una a una, de los pies al casco
//         (chispas, destellos, golpes de metal); la cámara da la vuelta alrededor.
//      5. Primer plano: el visor de la escafandra brilla.
//      6. Enciende el propulsor y sale disparado por la superficie del lago (la salida de la cueva).
//      7. Afuera: rompe el agua y vuela hacia la bandada de aves que da vueltas en el cielo.
//  - RESTO DE PANTALLAS (modos, mapa, controles, cooperativo...): vista tranquila del Taller, con el pez ya con su
//    traje en el soporte y la cámara moviéndose despacio.
//  Una capa propia detrás de la interfaz del menú hace los destellos y los fundidos, y oscurece un poco el fondo en
//  las pantallas con paneles.
// ============================================================================
public class MenuBackdrop : MonoBehaviour
{
    // ---------- Acceso desde el menú ----------
    public static MenuBackdrop Instance { get; private set; }
    public static bool Ready { get { return Instance != null && Instance.built; } }
    static bool wantCinematic = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        wantCinematic = true;
    }

    // La pantalla de título pide la cinemática; las demás, la vista tranquila
    public static void SetCinematic(bool on)
    {
        wantCinematic = on;
        if (Ready) Instance.ApplyMode(false);
    }

    // ---------- Tiempos de la cinemática (segundos) ----------
    const float TMechanic = 6f;       // 2. el mecánico
    const float TArrive = 10.5f;      // 3. llega el pez
    const float TSuit = 15f;          // 4. el traje
    const float TSnapStart = 15.8f;   //    primera pieza que encaja
    const float TSnapEnd = 21.5f;     //    última pieza que encaja
    const float SnapTime = 0.42f;     //    lo que tarda cada pieza en llegar
    const float TSuitDone = 22.2f;
    const float THelmet = 23f;        // 5. primer plano del casco
    const float TLaunch = 26f;        // 6. despegue
    const float TLiftOff = 27f;
    const float TSky = 30f;           // 7. afuera
    const float TBurst = 30.3f;
    const float TLoop = 37.5f;

    const float HeroHeight = 1.5f;
    const float HelmetHeight = 1.28f;                                  // centro de la escafandra (desde los pies)
    const float HeroYaw = 200f;                                        // mirando a la cámara (como el traje del soporte)
    static readonly Vector3 JetLocal = new Vector3(0f, 0.85f, -0.22f); // propulsor en la espalda
    static readonly Vector3 OutsideOrigin = new Vector3(0f, 0f, 1500f); // el exterior, lejos de la cueva
    static readonly Vector3 FlockCenter = new Vector3(8f, 36f, 96f);   // bandada (respecto al exterior)

    WorkshopScene workshop;
    Camera cam;
    bool built, cinematic;
    float t, clock, calmT;

    // ---------- El pez ----------
    Transform hero;
    Animator heroAnim;
    readonly List<Renderer> suitRenderers = new List<Renderer>();
    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Mesh> bakedMeshes = new List<Mesh>();
    bool bakeRequested;
    float jet01;

    class Piece
    {
        public Transform t;
        public Vector3 finalPos;
        public Quaternion finalRot;
        public Vector3 orbitDir;
        public float orbitRadius, orbitHeight, phase;
        public Vector3 spinAxis;
        public float appearAt, snapAt;
        public bool glass, done;
    }

    // ---------- Efectos ----------
    ParticleSystem jetFlame, jetBubbles, sparks, splash;
    TrailRenderer trail;
    Light jetLight, snapLight, glintLight;
    float snapFlash, shake;
    Transform installRing, ripple, glintCore, glintStreak;
    Material installRingMat, rippleMat, glintCoreMat, glintStreakMat;

    // ---------- Exterior ----------
    Transform outside;
    Light sun;
    bool outsideOn;
    readonly List<Bird> birds = new List<Bird>();
    Color caveFog, caveAmbient, caveBg;
    float caveFogDensity, caveFar;

    class Bird
    {
        public Transform t;
        public readonly List<Transform> wings = new List<Transform>();
        public readonly List<Quaternion> rest = new List<Quaternion>();
        public readonly List<float> side = new List<float>();
        public float angle, radius, height, speed, phase, flap;
    }

    // ---------- Cámara y capa ----------
    Vector3 camPos, camLook;
    float camFov = 50f, frameYaw;
    int lastShot = -1;
    Image dim, flash, cut;
    float dimA, cutA;

    static Vector3 StandTop { get { return WorkshopScene.StandPosition + Vector3.up * 1.25f; } }

    // ======================= ARMADO =======================

    public void Build(WorkshopScene ws)
    {
        workshop = ws;
        Instance = this;
        cam = Camera.main;
        caveFog = RenderSettings.fogColor;
        caveFogDensity = RenderSettings.fogDensity;
        caveAmbient = RenderSettings.ambientLight;
        caveBg = cam != null ? cam.backgroundColor : caveFog;
        caveFar = cam != null ? cam.farClipPlane : 1000f;

        BuildOverlay();
        BuildHero();
        BuildEffects();
        BuildOutside();
        built = true;
        cinematic = !wantCinematic;   // para que ApplyMode aplique el modo pedido
        ApplyMode(true);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Mesh m in bakedMeshes) if (m != null) Destroy(m);
    }

    void BuildOverlay()
    {
        Canvas c = MenuUI.CreateCanvas("FondoMenuCapa", -20);
        RectTransform r = (RectTransform)c.transform;
        dim = MenuUI.Panel("Atenuar", r, MenuUI.WithAlpha(MenuUI.Ink, 0f));
        flash = MenuUI.Panel("Destello", r, new Color(0.88f, 0.97f, 1f, 0f));
        cut = MenuUI.Panel("Corte", r, MenuUI.WithAlpha(MenuUI.Ink, 1f));
        dim.raycastTarget = flash.raycastTarget = cut.raycastTarget = false;
        cutA = 1f;
    }

    void BuildHero()
    {
        hero = new GameObject("PezDelMenu").transform;
        hero.position = StandTop;
        GameObject model = WorkshopKit.PlaceModel(workshop.suitModel, hero, HeroHeight, true);
        if (model != null)
        {
            workshop.ApplyVisorGlassTo(model);
            heroAnim = model.GetComponentInChildren<Animator>();
            if (heroAnim == null && workshop.suitAnimator != null) heroAnim = model.AddComponent<Animator>();
            if (heroAnim != null)
            {
                if (workshop.suitAnimator != null) heroAnim.runtimeAnimatorController = workshop.suitAnimator;
                heroAnim.applyRootMotion = false;
                heroAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            // Piezas del traje: todo lo que no es el pez (cabeza, ojos, cuerpo y aletas). Solo las que el modelo
            // muestra: una pieza apagada a propósito sigue apagada
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || IsFishPart(r.gameObject.name)) continue;
                if (r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>() != null) suitRenderers.Add(r);
            }
        }
        else
        {
            // Sin modelo: una figura simple (el traje es la cápsula naranja)
            Material orange = WorkshopKit.Lit(new Color(1f, 0.48f, 0.18f), null, 0.4f, "Menu_Traje");
            GameObject body = WorkshopKit.Shape(PrimitiveType.Capsule, "Traje", hero, Vector3.up * 0.75f, new Vector3(0.7f, 0.75f, 0.7f), orange);
            suitRenderers.Add(body.GetComponent<Renderer>());
        }
        hero.rotation = Quaternion.Euler(0f, HeroYaw, 0f);
    }

    static bool IsFishPart(string n)
    {
        string[] fish = { "Head", "Ojos", "Body", "Aleta" };
        foreach (string f in fish)
            if (n.StartsWith(f, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    bool IsGlass(Renderer r)
    {
        string n = r.gameObject.name;
        if (n.IndexOf("Vidrio", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        foreach (Material m in r.sharedMaterials)
        {
            if (m == null) continue;
            if (m == workshop.visorGlassMaterial) return true;
            if (m.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.name.IndexOf("Vidrio", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.name.IndexOf("Eskafandra", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    void BuildEffects()
    {
        Material fx = workshop.fxMaterialTemplate;

        // Llama del propulsor (aditiva): sale de la espalda hacia los pies
        jetFlame = MakeParticles("LlamaPropulsor", hero, FXFactory.ParticleMaterial(fx, FXFactory.SoftDot, Color.white),
                                 new Vector2(0.12f, 0.3f), new Vector2(3f, 6f), new Vector2(0.12f, 0.3f),
                                 new Color(1f, 0.85f, 0.4f, 0.95f), new Color(1f, 0.45f, 0.15f, 0.85f), 0f, 400);
        jetFlame.transform.localPosition = JetLocal;
        jetFlame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ConeShape(jetFlame, 9f, 0.05f);
        FadeOverLife(jetFlame, 0.25f);

        // Burbujas del propulsor (en la cueva todo está bajo el agua)
        jetBubbles = MakeParticles("BurbujasPropulsor", hero, FXFactory.ParticleMaterial(fx, FXFactory.Bubble, Color.white),
                                   new Vector2(1.2f, 2.4f), new Vector2(1.5f, 3.2f), new Vector2(0.04f, 0.13f),
                                   new Color(0.85f, 0.97f, 1f, 0.75f), new Color(0.7f, 0.9f, 1f, 0.5f), -0.35f, 500);
        jetBubbles.transform.localPosition = JetLocal;
        jetBubbles.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ConeShape(jetBubbles, 22f, 0.08f);
        var noise = jetBubbles.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.8f;

        // Chispas al encajar cada pieza (se emiten a mano donde haga falta)
        sparks = MakeParticles("ChispasTraje", null, FXFactory.ParticleMaterial(fx, FXFactory.SoftDot, Color.white),
                               new Vector2(0.25f, 0.65f), Vector2.zero, new Vector2(0.03f, 0.08f),
                               new Color(1f, 0.85f, 0.45f), new Color(0.6f, 0.95f, 1f), 0.7f, 600);

        // Salpicadura al romper el agua (transparente normal: se ve blanca sobre el cielo)
        splash = MakeParticles("Salpicadura", null, FXFactory.ParticleMaterialAlpha(fx, FXFactory.SoftDot, Color.white),
                               new Vector2(0.7f, 1.5f), new Vector2(5f, 11f), new Vector2(0.25f, 0.7f),
                               new Color(1f, 1f, 1f, 0.9f), new Color(0.8f, 0.92f, 1f, 0.75f), 1.6f, 300);
        splash.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ConeShape(splash, 24f, 0.7f);
        FadeOverLife(splash, 0.6f);

        // Estela del propulsor en el cielo
        GameObject trailGo = new GameObject("Estela");
        trailGo.transform.SetParent(hero, false);
        trailGo.transform.localPosition = JetLocal;
        trail = trailGo.AddComponent<TrailRenderer>();
        trail.sharedMaterial = WorkshopKit.Glow(fx, FXFactory.White, new Color(1f, 0.92f, 0.8f, 0.55f));
        trail.time = 0.9f;
        trail.widthMultiplier = 0.32f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        Gradient tg = new Gradient();
        tg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.55f), 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = tg;
        trail.minVertexDistance = 0.15f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;

        // Luces: propulsor, destello al encajar y brillo del visor
        jetLight = MakeLight("LuzPropulsor", new Color(1f, 0.62f, 0.3f), 4f);
        snapLight = MakeLight("LuzPieza", new Color(0.65f, 0.95f, 1f), 3.2f);
        glintLight = MakeLight("LuzVisor", new Color(0.8f, 0.95f, 1f), 2.2f);

        // Anillo de luz que sube por el traje al terminar (como el de la instalación)
        installRingMat = WorkshopKit.Glow(fx, FXFactory.White, new Color(0.6f, 1f, 1f, 0f));
        installRing = WorkshopKit.MeshObject("AnilloTraje", null, WorkshopKit.RingMesh(0.6f, 0.78f, 48), installRingMat).transform;
        installRing.position = StandTop;
        installRing.gameObject.SetActive(false);

        // Destello del visor: un punto brillante y una raya que cruza la escafandra
        glintCoreMat = WorkshopKit.Glow(fx, FXFactory.SoftDot, new Color(1f, 1f, 1f, 0f));
        glintStreakMat = WorkshopKit.Glow(fx, FXFactory.Streak, new Color(0.8f, 0.95f, 1f, 0f));
        glintCore = WorkshopKit.Shape(PrimitiveType.Quad, "BrilloVisor", null, Vector3.zero, Vector3.one * 0.2f, glintCoreMat).transform;
        glintStreak = WorkshopKit.Shape(PrimitiveType.Quad, "RayaVisor", null, Vector3.zero, new Vector3(0.9f, 0.07f, 1f), glintStreakMat).transform;
        foreach (Transform g in new[] { glintCore, glintStreak })
        {
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.gameObject.SetActive(false);
        }
    }

    Light MakeLight(string name, Color color, float range)
    {
        Light l = new GameObject(name).AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        return l;
    }

    static ParticleSystem MakeParticles(string name, Transform parent, Material mat, Vector2 life, Vector2 speed, Vector2 size,
                                        Color a, Color b, float gravity, int max)
    {
        ParticleSystem ps = FXFactory.CreateParticleSystem(name, parent, mat);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = gravity;
        main.maxParticles = max;
        var em = ps.emission;
        em.rateOverTime = 0f;
        ps.Play();
        return ps;
    }

    static void ConeShape(ParticleSystem ps, float angle, float radius)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = radius;
    }

    static void FadeOverLife(ParticleSystem ps, float endSize)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.45f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
    }

    // ======================= EXTERIOR (el lago y el cielo) =======================

    void BuildOutside()
    {
        outside = new GameObject("ExteriorDelMenu").transform;
        outside.position = OutsideOrigin;
        Vector3 sunDir = new Vector3(-0.35f, 0.42f, 1f).normalized;   // el sol, adelante y a la izquierda (contraluz)
        Color horizon = new Color(0.8f, 0.88f, 0.94f), zenith = new Color(0.28f, 0.52f, 0.86f), sunTint = new Color(1f, 0.92f, 0.75f);

        // Agua: brillante, para que el sol deje su reflejo
        Material water = WorkshopKit.Lit(new Color(0.05f, 0.27f, 0.36f), null, 0.93f, "Menu_Lago");
        GameObject lake = WorkshopKit.Shape(PrimitiveType.Plane, "Lago", outside, Vector3.zero, new Vector3(160f, 1f, 160f), water);
        lake.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Cielo, sol y nubes: sin niebla (sombreador de interfaz), dibujados antes que todo lo demás
        Shader unlit = Shader.Find("UI/Default");
        if (unlit != null)
        {
            Material skyMat = new Material(unlit) { name = "Menu_Cielo", renderQueue = 1000 };
            KeepBehind(skyMat);
            GameObject sky = WorkshopKit.MeshObject("Cielo", outside, SkyDome(700f, horizon, zenith, sunDir, sunTint), skyMat);
            sky.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Texture2D sunTex = FXFactory.MakeRadial(128, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f), "Menu_Sol");
            Billboard("Sol", sunTex, sunDir * 600f, 150f, 150f, new Color(1f, 0.97f, 0.88f, 1f), 1001);
            Billboard("HaloSol", sunTex, sunDir * 610f, 420f, 420f, new Color(1f, 0.9f, 0.7f, 0.35f), 1001);

            Texture2D cloudTex = FXFactory.MakeRadial(128, d => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - d) / 0.6f)) * 0.9f, "Menu_Nube");
            System.Random rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                float az = Mathf.Lerp(-75f, 75f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                float el = Mathf.Lerp(4f, 20f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                float dist = Mathf.Lerp(380f, 560f, (float)rng.NextDouble());
                Vector3 d = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
                float w = Mathf.Lerp(110f, 240f, (float)rng.NextDouble());
                for (int k = 0; k < 3; k++)
                {
                    Vector3 off = new Vector3((k - 1) * w * 0.33f, (k == 1 ? 0.12f : 0f) * w, 0f);
                    Billboard("Nube", cloudTex, d * dist + Quaternion.LookRotation(d) * off, w * (k == 1 ? 0.75f : 0.55f), w * 0.3f,
                              new Color(1f, 1f, 1f, 0.55f), 1002);
                }
            }
        }

        // Orillas a lo lejos: colinas que la niebla vuelve azuladas
        Material hill = WorkshopKit.Lit(new Color(0.1f, 0.24f, 0.16f), null, 0.05f, "Menu_Colina");
        for (int i = 0; i < 16; i++)
        {
            float az = (i / 16f) * Mathf.PI * 2f + 0.2f * Mathf.Sin(i * 3.1f);
            float dist = 330f + 60f * Mathf.Abs(Mathf.Sin(i * 1.7f));
            Vector3 p = new Vector3(Mathf.Sin(az) * dist, -6f, Mathf.Cos(az) * dist);
            Vector3 s = new Vector3(110f + 50f * Mathf.Abs(Mathf.Sin(i * 2.3f)), 30f + 26f * Mathf.Abs(Mathf.Sin(i * 1.3f)), 80f);
            GameObject h = WorkshopKit.Shape(PrimitiveType.Sphere, "Colina", outside, p, s, hill);
            h.transform.rotation = Quaternion.LookRotation(-p.normalized);
            h.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Ola que se abre donde el pez rompe el agua
        rippleMat = WorkshopKit.Glow(workshop.fxMaterialTemplate, FXFactory.White, new Color(1f, 1f, 1f, 0f));
        ripple = WorkshopKit.MeshObject("Ola", outside, WorkshopKit.RingMesh(0.85f, 1f, 64), rippleMat).transform;
        ripple.localPosition = Vector3.up * 0.06f;

        // El sol (contraluz: el pez y las aves se recortan contra el cielo)
        sun = new GameObject("SolDelMenu").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.LookRotation(-sunDir + Vector3.down * 0.15f);
        sun.enabled = false;

        BuildBirds();
        outside.gameObject.SetActive(false);
    }

    // Cartel que mira al centro del exterior (sin niebla)
    void Billboard(string name, Texture tex, Vector3 local, float w, float h, Color color, int queue)
    {
        Shader unlit = Shader.Find("UI/Default");
        if (unlit == null) return;
        Material m = new Material(unlit) { name = "Menu_" + name, renderQueue = queue, mainTexture = tex, color = color };
        KeepBehind(m);
        GameObject q = WorkshopKit.Shape(PrimitiveType.Quad, name, outside, local, new Vector3(w, h, 1f), m);
        q.transform.rotation = Quaternion.LookRotation(local.normalized);
        q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // El sombreador de interfaz toma la prueba de profundidad de un valor global (pensado para los Canvas): se fija en
    // el material para que el cielo, el sol y las nubes queden siempre detrás del agua, las colinas, el pez y las aves
    static void KeepBehind(Material m)
    {
        m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
    }

    // Cúpula del cielo con el degradado en los colores de los vértices (horizonte claro, arriba azul, más cálido hacia el sol)
    static Mesh SkyDome(float radius, Color horizon, Color zenith, Vector3 sunDir, Color sunTint)
    {
        const int lat = 18, lon = 36;
        List<Vector3> verts = new List<Vector3>();
        List<Color> cols = new List<Color>();
        List<int> tris = new List<int>();
        for (int i = 0; i <= lat; i++)
        {
            float el = Mathf.Lerp(-0.25f, 0.5f, i / (float)lat) * Mathf.PI;
            float y = Mathf.Sin(el), r = Mathf.Cos(el);
            for (int j = 0; j <= lon; j++)
            {
                float az = j / (float)lon * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(az) * r, y, Mathf.Sin(az) * r);
                verts.Add(d * radius);
                Color c = y < 0f ? horizon : Color.Lerp(horizon, zenith, Mathf.Pow(y, 0.55f));
                float s = Mathf.Max(0f, Vector3.Dot(d, sunDir));
                c = Color.Lerp(c, sunTint, Mathf.Pow(s, 8f) * 0.6f);
                c.a = 1f;
                cols.Add(c);
            }
        }
        for (int i = 0; i < lat; i++)
        {
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j, b = a + lon + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }
        Mesh m = new Mesh { name = "Menu_Cielo" };
        m.SetVertices(verts);
        m.SetColors(cols);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    // Bandada: las aves del juego (las que no son jefes), dando vueltas en el cielo y aleteando
    void BuildBirds()
    {
        List<BirdType> types = new List<BirdType>();
        for (int i = 0; i < 64; i++)
        {
            BirdType b = CoopCatalog.Bird(i);
            if (b == null) break;
            if (b.model != null && b.boss == BossKind.None) types.Add(b);
        }

        const int count = 7;
        for (int i = 0; i < count; i++)
        {
            Bird bd = new Bird();
            bd.t = new GameObject("Ave" + i).transform;
            bd.t.SetParent(outside, false);
            bd.radius = 14f + (i % 4) * 4.5f;
            bd.height = -5f + (i % 3) * 5f;
            bd.speed = (i % 2 == 0 ? 1f : 0.8f) * (0.32f + (i % 3) * 0.05f);
            bd.angle = i * 0.9f;
            bd.phase = i * 1.3f;
            bd.flap = i * 0.7f;
            PlaceBird(bd, 0f);

            if (types.Count > 0)
            {
                BirdType ty = types[i % types.Count];
                Transform v = BirdVisual(ty, bd.t);
                bool flapByCode = ty.proceduralWingFlap && ty.animatorController == null;
                Animator a = v.GetComponentInChildren<Animator>();
                if (a != null && a.runtimeAnimatorController != null) flapByCode = false;
                if (flapByCode)
                {
                    foreach (Transform bone in v.GetComponentsInChildren<Transform>(true))
                    {
                        if (!InList(bone.name, ty.wingBones)) continue;
                        bd.wings.Add(bone);
                        bd.rest.Add(bone.localRotation);
                        bd.side.Add(Vector3.Dot(bone.position - bd.t.position, bd.t.right) < 0f ? -1f : 1f);
                    }
                }
            }
            else SimpleBird(bd);
            birds.Add(bd);
        }
    }

    static Transform BirdVisual(BirdType type, Transform parent)
    {
        GameObject v = new GameObject("Visual");
        v.transform.SetParent(parent, false);
        GameObject inst = Instantiate(type.model, v.transform, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        v.transform.localPosition = type.modelOffset;
        v.transform.localRotation = Quaternion.Euler(type.modelRotation);
        v.transform.localScale = Vector3.one * type.modelScale;
        WorkshopKit.StripColliders(v);
        if (type.materialOverride != null)
        {
            foreach (Renderer r in v.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = type.materialOverride;
                r.sharedMaterials = mats;
            }
        }
        if (type.animatorController != null)
        {
            Animator anim = v.GetComponentInChildren<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = type.animatorController;
            anim.applyRootMotion = false;
        }
        return v.transform;
    }

    // Sin modelos de aves: silueta simple (cuerpo y dos alas que aletean)
    void SimpleBird(Bird bd)
    {
        Material dark = WorkshopKit.Lit(new Color(0.15f, 0.15f, 0.18f), null, 0.2f, "Menu_Ave");
        WorkshopKit.Shape(PrimitiveType.Capsule, "Cuerpo", bd.t, Vector3.zero, new Vector3(0.5f, 0.9f, 0.5f), dark, new Vector3(90f, 0f, 0f));
        for (int s = -1; s <= 1; s += 2)
        {
            Transform pivot = new GameObject(s < 0 ? "AlaI" : "AlaD").transform;
            pivot.SetParent(bd.t, false);
            WorkshopKit.Shape(PrimitiveType.Cube, "Ala", pivot, new Vector3(s * 0.9f, 0f, 0f), new Vector3(1.8f, 0.06f, 0.6f), dark);
            bd.wings.Add(pivot);
            bd.rest.Add(pivot.localRotation);
            bd.side.Add(s);
        }
    }

    static bool InList(string name, string list)
    {
        if (string.IsNullOrEmpty(list)) return false;
        foreach (string part in list.Split(','))
            if (part.Trim() == name) return true;
        return false;
    }

    void PlaceBird(Bird b, float dt)
    {
        b.angle += dt * b.speed;
        float dir = Mathf.Sign(b.speed);
        Vector3 c = OutsideOrigin + FlockCenter;
        b.t.position = c + new Vector3(Mathf.Cos(b.angle) * b.radius, b.height + Mathf.Sin(b.angle * 2f + b.phase) * 1.2f, Mathf.Sin(b.angle) * b.radius);
        Vector3 tangent = new Vector3(-Mathf.Sin(b.angle), 0f, Mathf.Cos(b.angle)) * dir;
        b.t.rotation = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, 0f, 22f * dir);
    }

    void UpdateBirds(float dt)
    {
        foreach (Bird b in birds)
        {
            PlaceBird(b, dt);
            b.flap += dt * 7f;
            float a = Mathf.Sin(b.flap) * 28f;
            for (int i = 0; i < b.wings.Count; i++)
            {
                if (b.wings[i] == null) continue;
                b.wings[i].localRotation = b.rest[i];
                b.wings[i].rotation = Quaternion.AngleAxis(a * b.side[i], b.t.forward) * b.wings[i].rotation;
            }
        }
    }

    void SetOutside(bool on)
    {
        if (outsideOn == on) return;
        outsideOn = on;
        outside.gameObject.SetActive(on);
        if (sun != null) sun.enabled = on;
        if (workshop.EntranceLight != null) workshop.EntranceLight.enabled = !on;
        if (on)
        {
            RenderSettings.fogColor = new Color(0.78f, 0.87f, 0.93f);
            RenderSettings.fogDensity = 0.0026f;
            RenderSettings.ambientLight = new Color(0.56f, 0.63f, 0.72f);
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.8f, 0.88f, 0.94f);
                cam.farClipPlane = 1500f;
            }
        }
        else
        {
            RenderSettings.fogColor = caveFog;
            RenderSettings.fogDensity = caveFogDensity;
            RenderSettings.ambientLight = caveAmbient;
            if (cam != null)
            {
                cam.backgroundColor = caveBg;
                cam.farClipPlane = caveFar;
            }
        }
    }

    // ======================= MODOS =======================

    void ApplyMode(bool first)
    {
        if (!first && cinematic == wantCinematic) return;
        cinematic = wantCinematic;
        if (!first) cutA = Mathf.Max(cutA, 0.85f);   // fundido corto al cambiar de cámara
        if (cinematic) RestartCinematic();
        else EnterCalm();
    }

    void RestartCinematic()
    {
        t = 0f;
        lastShot = -1;
        cutA = 1f;
        SetOutside(false);
        ClearPieces();
        SetSuitVisible(false);
        if (heroAnim != null) heroAnim.speed = 1f;
        hero.gameObject.SetActive(false);
        hero.SetPositionAndRotation(StandTop, Quaternion.Euler(0f, HeroYaw, 0f));
        ResetEffects();
        SetCritters(0.2f);
    }

    void EnterCalm()
    {
        calmT = 0f;
        lastShot = -1;
        SetOutside(false);
        ClearPieces();
        SetSuitVisible(true);
        if (heroAnim != null) heroAnim.speed = 1f;
        hero.gameObject.SetActive(true);
        hero.SetPositionAndRotation(StandTop, Quaternion.Euler(0f, HeroYaw, 0f));
        ResetEffects();
        workshop.Welding = false;
        SetCritters(0f);
    }

    void ResetEffects()
    {
        jet01 = 0f;
        shake = 0f;
        snapFlash = 0f;
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        foreach (ParticleSystem ps in new[] { jetFlame, jetBubbles, sparks, splash })
            if (ps != null) ps.Clear();
        installRing.gameObject.SetActive(false);
        glintCore.gameObject.SetActive(false);
        glintStreak.gameObject.SetActive(false);
        glintLight.intensity = 0f;
        snapLight.intensity = 0f;
        jetLight.intensity = 0f;
    }

    void SetCritters(float excitement)
    {
        if (workshop.MechanicCritter != null) workshop.MechanicCritter.excitement = excitement;
        foreach (WorkshopCritter f in workshop.FryCritters) if (f != null) f.excitement = excitement;
    }

    void SetSuitVisible(bool on)
    {
        foreach (Renderer r in suitRenderers) if (r != null) r.enabled = on;
    }

    // ======================= CICLO =======================

    void Update()
    {
        if (!built) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        clock += dt;
        if (cinematic) StepCinematic(dt);
        else StepCalm(dt);
        UpdateJet(dt);
        UpdateOverlay(dt);
    }

    void LateUpdate()
    {
        if (!built) return;
        // Las piezas se copian después de que la animación dejó al pez en su pose de este cuadro
        if (bakeRequested)
        {
            bakeRequested = false;
            BakePieces();
        }
        ApplyCamera();
    }

    static bool Crossed(float prev, float now, float at) { return prev < at && now >= at; }

    void StepCinematic(float dt)
    {
        float prev = t;
        t += dt;
        if (t >= TLoop)
        {
            RestartCinematic();
            return;
        }

        // ---- Momentos ----
        if (Crossed(prev, t, TMechanic)) { workshop.Welding = true; SetCritters(0.6f); }
        if (Crossed(prev, t, TArrive))
        {
            hero.gameObject.SetActive(true);
            SetSuitVisible(false);
            foreach (WorkshopCritter f in workshop.FryCritters) if (f != null) f.excitement = 0.8f;
        }
        if (Crossed(prev, t, TSuit)) { bakeRequested = true; SetCritters(1f); workshop.Welding = true; }
        if (Crossed(prev, t, TSuitDone)) FinishSuit();
        if (Crossed(prev, t, THelmet)) { workshop.Welding = false; SetCritters(0.35f); }
        if (Crossed(prev, t, THelmet + 1f)) Sfx.Play2D(Sonido.EscudoListo, 0.22f, 1.1f);
        if (Crossed(prev, t, TLaunch + 0.55f)) Sfx.Play2D(Sonido.Turbo, 0.28f);
        if (Crossed(prev, t, TLiftOff)) { Sfx.Play2D(Sonido.Dash, 0.3f, 0.9f); SetCritters(1f); }
        if (Crossed(prev, t, TSky))
        {
            SetOutside(true);
            hero.position = OutsideOrigin + Vector3.down * 2f;
            if (trail != null) trail.Clear();
            jetFlame.Clear();
            jetBubbles.Clear();
        }
        if (Crossed(prev, t, TBurst)) Burst();
        if (Crossed(prev, t, 32.6f)) Sfx.Play2D(Sonido.Graznido, 0.24f);
        if (Crossed(prev, t, 34.6f)) Sfx.Play2D(Sonido.Gaviota, 0.2f);

        UpdateHero(dt);
        UpdatePieces();
        UpdateInstallRing();
        UpdateGlint(dt);
        if (outsideOn)
        {
            UpdateBirds(dt);
            UpdateRipple();
        }

        CinematicCamera(dt);
    }

    void StepCalm(float dt)
    {
        calmT += dt;
        jet01 = 0f;
        hero.SetPositionAndRotation(StandTop + Vector3.up * Mathf.Sin(calmT * 1.1f) * 0.025f,
                                    Quaternion.Euler(0f, HeroYaw + Mathf.Sin(calmT * 0.25f) * 12f, 0f));
        // Vista amplia de la guarida, con el pez y su traje en el soporte a la derecha
        Vector3 pos = new Vector3(0.5f + Mathf.Sin(calmT * 0.06f) * 0.8f, 1.75f + Mathf.Sin(calmT * 0.09f) * 0.12f, -6.4f);
        Vector3 look = new Vector3(1.0f + Mathf.Sin(calmT * 0.05f + 1f) * 0.4f, 1.95f, 1.6f);
        SetCameraTarget(0, pos, look, 47f, false, dt);
        frameYaw = Mathf.MoveTowards(frameYaw, 0f, dt * 20f);
    }

    // ======================= EL PEZ =======================

    void UpdateHero(float dt)
    {
        if (!hero.gameObject.activeSelf) return;
        Vector3 top = StandTop;
        Vector3 sky = WorkshopScene.SkylightPosition;

        if (t < TSuit)
        {
            // Llega nadando desde el fondo de la guarida hasta el soporte
            float u = Mathf.Clamp01((t - TArrive) / (TSuit - 0.5f - TArrive));
            float e = Smoother(u);
            Vector3 a = new Vector3(-3.8f, 2.6f, 4.2f), b = new Vector3(-1.2f, 3.0f, 2.6f), c = new Vector3(1.2f, 2.3f, 0.9f);
            Vector3 p = Bezier(a, b, c, top, e);
            Vector3 ahead = Bezier(a, b, c, top, Mathf.Min(1f, e + 0.02f)) - p;
            float swim = 1f - Smooth01((u - 0.68f) / 0.32f);
            p += Vector3.up * Mathf.Sin(clock * 3.1f) * 0.06f * swim;
            hero.position = p;
            float travelYaw = ahead.sqrMagnitude > 1e-6f ? Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg : HeroYaw;
            float yaw = Mathf.LerpAngle(HeroYaw, travelYaw, swim);
            hero.rotation = Quaternion.Euler(14f * swim, yaw, Mathf.Sin(clock * 2.3f) * 5f * swim);
            jet01 = 0f;
        }
        else if (t < TLaunch)
        {
            // Quieto en el soporte mientras se le pone el traje (después, flota apenas)
            float bob = t > TSuitDone ? Mathf.Sin((t - TSuitDone) * 1.4f) * 0.02f : 0f;
            hero.SetPositionAndRotation(top + Vector3.up * bob, Quaternion.Euler(0f, HeroYaw, 0f));
            jet01 = 0f;
        }
        else if (t < TLiftOff)
        {
            // Se agacha y enciende el propulsor
            float k = t - TLaunch;
            hero.SetPositionAndRotation(top + Vector3.down * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.1f, Quaternion.Euler(0f, HeroYaw, 0f));
            jet01 = k > 0.55f ? Mathf.Lerp(0f, 0.6f, Smooth01((k - 0.55f) / 0.45f)) : 0f;
        }
        else if (t < TSky)
        {
            // Sale disparado hacia la superficie del lago (el claro del techo)
            float u = Mathf.Clamp01((t - TLiftOff) / 2.8f);
            float e = 0.3f * u + 0.7f * u * u;
            Vector3 a = top + new Vector3(0f, 2.4f, 0.2f), b = sky + new Vector3(0.3f, -2.2f, -0.6f), c = sky + new Vector3(0f, 2.8f, 0.4f);
            Vector3 p = Bezier(top, a, b, c, e);
            Vector3 v = Bezier(top, a, b, c, Mathf.Min(1f, e + 0.02f)) - p;
            hero.position = p;
            Quaternion target = Quaternion.FromToRotation(Vector3.up, v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up) * Quaternion.Euler(0f, HeroYaw, 0f);
            hero.rotation = Quaternion.Slerp(hero.rotation, target, 1f - Mathf.Exp(-dt * 6f));
            jet01 = 1f;
        }
        else
        {
            // Afuera: rompe el agua y vuela hacia la bandada
            Vector3 o = OutsideOrigin;
            if (t < TBurst)
            {
                hero.SetPositionAndRotation(o + Vector3.down * 2f, Quaternion.identity);
            }
            else
            {
                float u = Mathf.Clamp01((t - TBurst) / (TLoop - TBurst));
                float e = 1f - Mathf.Pow(1f - u, 1.35f);
                Vector3 p0 = o + Vector3.down * 1.6f, a = o + new Vector3(0f, 9f, 3f), b = o + new Vector3(2f, 22f, 30f), c = o + new Vector3(5f, 34f, 78f);
                Vector3 p = Bezier(p0, a, b, c, e);
                Vector3 v = Bezier(p0, a, b, c, Mathf.Min(1f, e + 0.02f)) - p;
                hero.position = p;
                Quaternion target = Quaternion.FromToRotation(Vector3.up, v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up);
                hero.rotation = Quaternion.Slerp(hero.rotation, target, 1f - Mathf.Exp(-dt * 5f));
            }
            jet01 = 1f;
        }
    }

    void UpdateJet(float dt)
    {
        bool active = hero.gameObject.activeInHierarchy && jet01 > 0.01f;
        var flame = jetFlame.emission;
        flame.rateOverTime = active ? jet01 * 190f : 0f;
        var bubbles = jetBubbles.emission;
        bubbles.rateOverTime = active && !outsideOn ? jet01 * 110f : 0f;
        if (trail != null) trail.emitting = active && outsideOn && t > TBurst;
        jetLight.transform.position = hero.TransformPoint(JetLocal);
        jetLight.intensity = active ? jet01 * (2.6f + Random.Range(-0.4f, 0.4f)) : Mathf.MoveTowards(jetLight.intensity, 0f, dt * 20f);
        snapFlash = Mathf.MoveTowards(snapFlash, 0f, dt * 4.5f);
        snapLight.intensity = snapFlash * 4f;
        shake = Mathf.MoveTowards(shake, 0f, dt * 0.35f);
    }

    // ======================= EL TRAJE =======================

    // Copia fija de cada pieza del traje en la pose de este momento (el pez se queda quieto mientras se lo ponen)
    void BakePieces()
    {
        ClearPieces();
        if (heroAnim != null) heroAnim.speed = 0f;
        List<Piece> list = new List<Piece>();
        foreach (Renderer r in suitRenderers)
        {
            if (r == null) continue;
            Mesh mesh;
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null)
            {
                if (smr.sharedMesh == null) continue;
                // Sin compensar la escala: la copia ya sale del tamaño en pantalla (se pone con la posición y el giro).
                // La pieza está oculta (el pez llegó sin traje): se enciende solo mientras se copia (no se dibuja)
                mesh = new Mesh { name = "Pieza_" + r.name };
                bool wasOn = smr.enabled;
                smr.enabled = true;
                smr.BakeMesh(mesh, false);
                smr.enabled = wasOn;
                mesh.RecalculateBounds();
                bakedMeshes.Add(mesh);
            }
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                mesh = mf.sharedMesh;
            }
            Vector3 scale = smr != null ? Vector3.one : r.transform.lossyScale;
            Transform src = r.transform;
            // El pivote va en el centro de la pieza: así gira y llega sobre sí misma
            Vector3 center = Vector3.Scale(scale, mesh.bounds.center);
            Transform pivot = new GameObject("Pieza_" + r.name).transform;
            pivot.SetPositionAndRotation(src.position + src.rotation * center, src.rotation);
            GameObject vis = new GameObject("Malla");
            vis.transform.SetParent(pivot, false);
            vis.transform.localPosition = -center;
            vis.transform.localScale = scale;
            vis.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = vis.AddComponent<MeshRenderer>();
            mr.sharedMaterials = r.sharedMaterials;
            mr.shadowCastingMode = r.shadowCastingMode;
            pivot.gameObject.SetActive(false);
            list.Add(new Piece { t = pivot, finalPos = pivot.position, finalRot = pivot.rotation, glass = IsGlass(r) });
        }

        // Orden de llegada: de los pies a la cabeza; el vidrio de la escafandra, al final
        float baseY = hero.position.y;
        list.Sort((a, b) =>
        {
            if (a.glass != b.glass) return a.glass ? 1 : -1;
            return a.finalPos.y.CompareTo(b.finalPos.y);
        });
        int n = list.Count;
        for (int i = 0; i < n; i++)
        {
            Piece p = list[i];
            float k = n > 1 ? i / (float)(n - 1) : 0f;
            p.snapAt = Mathf.Lerp(TSnapStart, TSnapEnd, k);
            p.appearAt = TSuit + 0.6f * (i / (float)Mathf.Max(1, n));
            float ang = i * 137.5f;                                        // ángulo dorado: repartidas alrededor
            p.orbitDir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            p.orbitRadius = 1.05f + 0.4f * Mathf.Repeat(i * 0.618f, 1f);
            p.orbitHeight = (p.finalPos.y - baseY) * 1.1f + 0.15f;
            p.phase = i * 0.77f;
            p.spinAxis = new Vector3(Mathf.Sin(i * 1.9f), 1f, Mathf.Cos(i * 2.7f)).normalized;
            pieces.Add(p);
        }
    }

    void ClearPieces()
    {
        foreach (Piece p in pieces) if (p.t != null) Destroy(p.t.gameObject);
        pieces.Clear();
        foreach (Mesh m in bakedMeshes) if (m != null) Destroy(m);
        bakedMeshes.Clear();
        bakeRequested = false;
    }

    void Orbit(Piece p, float time, out Vector3 pos, out Quaternion rot)
    {
        float ang = (time - TSuit) * 38f;
        Vector3 dir = Quaternion.Euler(0f, ang, 0f) * p.orbitDir;
        pos = StandTop + dir * p.orbitRadius + Vector3.up * (p.orbitHeight + Mathf.Sin(time * 1.6f + p.phase) * 0.07f);
        rot = p.finalRot * Quaternion.AngleAxis((time - TSuit) * 55f + p.phase * 40f, p.spinAxis);
    }

    void UpdatePieces()
    {
        foreach (Piece p in pieces)
        {
            if (p.done || p.t == null) continue;
            if (t < p.appearAt)
            {
                if (p.t.gameObject.activeSelf) p.t.gameObject.SetActive(false);
                continue;
            }
            if (!p.t.gameObject.activeSelf) p.t.gameObject.SetActive(true);
            Vector3 pos;
            Quaternion rot;
            if (t < p.snapAt)
            {
                Orbit(p, t, out pos, out rot);
                p.t.SetPositionAndRotation(pos, rot);
                p.t.localScale = Vector3.one * Mathf.Max(0.02f, Smooth01((t - p.appearAt) / 0.45f));
                continue;
            }
            float k = Mathf.Clamp01((t - p.snapAt) / SnapTime);
            Orbit(p, p.snapAt, out pos, out rot);
            p.t.position = Vector3.LerpUnclamped(pos, p.finalPos, EaseOutBack(k));
            p.t.rotation = Quaternion.Slerp(rot, p.finalRot, Smooth01(k * 1.3f));
            p.t.localScale = Vector3.one;
            if (k >= 1f)
            {
                p.done = true;
                p.t.SetPositionAndRotation(p.finalPos, p.finalRot);
                SnapFx(p.finalPos);
            }
        }
    }

    void SnapFx(Vector3 at)
    {
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 16; i++)
        {
            ep.position = at;
            ep.velocity = (Random.insideUnitSphere + Vector3.up * 0.5f) * Random.Range(1.5f, 3.5f);
            ep.startColor = Color.Lerp(new Color(1f, 0.85f, 0.45f), new Color(0.6f, 0.95f, 1f), Random.value);
            sparks.Emit(ep, 1);
        }
        snapLight.transform.position = at;
        snapFlash = 1f;
        shake = Mathf.Max(shake, 0.045f);
        Sfx.Play2D(Sonido.GolpeBlindado, 0.15f, Random.Range(1.05f, 1.3f));
    }

    // El traje completo: vuelven las piezas de verdad (animadas) y un anillo de luz sube por el traje
    void FinishSuit()
    {
        ClearPieces();
        SetSuitVisible(true);
        if (heroAnim != null) heroAnim.speed = 1f;
        installRing.gameObject.SetActive(true);
        workshop.Welding = false;
        Vector3 chest = hero.TransformPoint(0f, 0.95f, 0f);
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 50; i++)
        {
            ep.position = chest + Random.insideUnitSphere * 0.5f;
            ep.velocity = (Random.insideUnitSphere + Vector3.up * 0.8f) * Random.Range(2f, 5f);
            ep.startColor = Color.Lerp(new Color(1f, 0.85f, 0.45f), new Color(0.6f, 0.95f, 1f), Random.value);
            sparks.Emit(ep, 1);
        }
        snapLight.transform.position = chest;
        snapFlash = 1f;
        shake = Mathf.Max(shake, 0.06f);
        Sfx.Play2D(Sonido.Instalar, 0.32f);
    }

    void UpdateInstallRing()
    {
        if (!installRing.gameObject.activeSelf) return;
        float k = (t - TSuitDone) / 0.9f;
        if (k < 0f || k > 1f)
        {
            installRing.gameObject.SetActive(false);
            return;
        }
        installRing.position = StandTop + Vector3.up * Mathf.Lerp(0.05f, 1.7f, Smooth01(k));
        installRing.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f);
        SetColor(installRingMat, new Color(0.6f, 1f, 1f, Mathf.Sin(k * Mathf.PI) * 0.9f));
    }

    // Destello que cruza el visor en el primer plano del casco
    void UpdateGlint(float dt)
    {
        float k = (t - (THelmet + 1f)) / 0.7f;
        bool on = k >= 0f && k <= 1f && cam != null;
        if (glintCore.gameObject.activeSelf != on)
        {
            glintCore.gameObject.SetActive(on);
            glintStreak.gameObject.SetActive(on);
        }
        if (!on)
        {
            glintLight.intensity = Mathf.MoveTowards(glintLight.intensity, 0f, dt * 8f);
            return;
        }
        Vector3 helmet = hero.TransformPoint(0f, HelmetHeight, 0f);
        Vector3 toCam = (cam.transform.position - helmet).normalized;
        Vector3 p = helmet + toCam * 0.3f + cam.transform.right * Mathf.Lerp(-0.2f, 0.22f, Smooth01(k)) + cam.transform.up * Mathf.Lerp(0.08f, -0.03f, k);
        float a = Mathf.Sin(k * Mathf.PI);
        glintCore.SetPositionAndRotation(p, cam.transform.rotation);
        glintStreak.SetPositionAndRotation(p, cam.transform.rotation * Quaternion.Euler(0f, 0f, -12f));
        glintCore.localScale = Vector3.one * (0.12f + 0.1f * a);
        SetColor(glintCoreMat, new Color(1f, 1f, 1f, a));
        SetColor(glintStreakMat, new Color(0.8f, 0.95f, 1f, a * 0.8f));
        glintLight.transform.position = helmet + toCam * 0.4f;
        glintLight.intensity = a * 2.5f;
    }

    // Rompe el agua: salpicadura, ola y sonido
    void Burst()
    {
        splash.transform.position = OutsideOrigin + Vector3.up * 0.1f;
        splash.Emit(110);
        ripple.localScale = Vector3.one;
        SetColor(rippleMat, new Color(1f, 1f, 1f, 0.7f));
        shake = Mathf.Max(shake, 0.08f);
        Sfx.Play2D(Sonido.SalidaAgua, 0.4f);
    }

    void UpdateRipple()
    {
        float k = t < TBurst ? 1f : Mathf.Clamp01((t - TBurst) / 1.8f);
        ripple.localScale = Vector3.one * Mathf.Lerp(1f, 11f, Smooth01(k));
        SetColor(rippleMat, new Color(1f, 1f, 1f, (1f - k) * 0.7f));
    }

    // ======================= CÁMARA =======================

    void CinematicCamera(float dt)
    {
        Vector3 pos, look;
        float fov;
        bool track = false;
        int shot;
        Vector3 chest = hero.TransformPoint(0f, 0.95f, 0f);
        Vector3 front = Quaternion.Euler(0f, HeroYaw, 0f) * Vector3.forward;

        if (t < TMechanic)
        {
            // 1. La guarida: grúa que baja
            shot = 1;
            float e = Smooth01(t / TMechanic);
            pos = Vector3.Lerp(new Vector3(-4.8f, 3.9f, -4.2f), new Vector3(-3.0f, 2.2f, -5.4f), e);
            look = Vector3.Lerp(new Vector3(1.2f, 3.0f, 5.2f), new Vector3(0.6f, 1.9f, 2.4f), e);
            fov = Mathf.Lerp(52f, 47f, e);
        }
        else if (t < TArrive)
        {
            // 2. El mecánico
            shot = 2;
            float e = Smooth01((t - TMechanic) / (TArrive - TMechanic));
            pos = Vector3.Lerp(new Vector3(-0.3f, 1.55f, -2.3f), new Vector3(-0.75f, 1.45f, -1.75f), e);
            look = Vector3.Lerp(new Vector3(-1.6f, 1.35f, 0.15f), new Vector3(-1.55f, 1.4f, 0.1f), e);
            fov = Mathf.Lerp(40f, 35f, e);
        }
        else if (t < TSuit)
        {
            // 3. Llega el pez (la cámara lo sigue)
            shot = 3;
            float e = Smooth01((t - TArrive) / (TSuit - TArrive));
            pos = Vector3.Lerp(new Vector3(0.6f, 2.1f, -4.8f), new Vector3(1.3f, 1.9f, -3.9f), e);
            look = chest;
            fov = 44f;
            track = true;
        }
        else if (t < THelmet)
        {
            // 4. El traje: la cámara da la vuelta alrededor
            shot = 4;
            float e = Smooth01((t - TSuit) / (THelmet - TSuit));
            Vector3 center = StandTop + Vector3.up * 0.85f;
            Vector3 dir = Quaternion.Euler(0f, Mathf.Lerp(-50f, 30f, e), 0f) * front;
            pos = center + dir * Mathf.Lerp(3.0f, 2.3f, e);
            pos.y = Mathf.Lerp(2.5f, 2.15f, e);
            look = center + Vector3.up * 0.1f;
            fov = 40f;
        }
        else if (t < TLaunch)
        {
            // 5. Primer plano del casco
            shot = 5;
            float e = Smooth01((t - THelmet) / (TLaunch - THelmet));
            Vector3 helmet = StandTop + Vector3.up * HelmetHeight;
            Vector3 dir = Quaternion.Euler(0f, -12f, 0f) * front;
            pos = helmet + dir * Mathf.Lerp(1.55f, 1.15f, e) + Vector3.up * 0.06f;
            look = helmet + Vector3.up * 0.02f;
            fov = 34f;
        }
        else if (t < TSky)
        {
            // 6. Despegue: desde abajo, mirando cómo sube hacia la luz
            shot = 6;
            float e = Smooth01((t - TLaunch) / (TSky - TLaunch));
            pos = Vector3.Lerp(new Vector3(3.9f, 1.0f, -2.2f), new Vector3(3.6f, 1.2f, -1.8f), e);
            look = chest;
            fov = Mathf.Lerp(46f, 58f, e);
            track = true;
        }
        else
        {
            // 7. Afuera: rompe el agua y la cámara lo sigue hacia la bandada
            shot = 7;
            Vector3 o = OutsideOrigin;
            float e = Smooth01((t - (TBurst + 0.4f)) / 6.4f);
            Vector3 heroPos = t < TBurst ? o : hero.position;
            pos = Vector3.Lerp(o + new Vector3(-5.5f, 1.2f, -9f), heroPos + new Vector3(-7f, -3f, -14f), e);
            Vector3 heroLook = t < TBurst ? o + Vector3.up * 1.5f : chest;
            look = Vector3.Lerp(heroLook, Vector3.Lerp(heroLook, o + FlockCenter, 0.4f), Smooth01((t - 32.5f) / 4f));
            fov = Mathf.Lerp(54f, 46f, e);
            track = true;
        }

        shake = Mathf.Max(shake, shot >= 6 ? jet01 * 0.022f : 0f);
        SetCameraTarget(shot, pos, look, fov, track, dt);
        // En el título el texto está a la izquierda: lo importante queda a la derecha de la imagen
        frameYaw = Mathf.MoveTowards(frameYaw, camFov * 0.22f, dt * 20f);
    }

    void SetCameraTarget(int shot, Vector3 pos, Vector3 look, float fov, bool track, float dt)
    {
        if (shot != lastShot)
        {
            // Corte: la cámara salta (sin arrastre)
            lastShot = shot;
            camPos = pos;
            camLook = look;
            camFov = fov;
            return;
        }
        camPos = pos;
        camLook = track ? Vector3.Lerp(camLook, look, 1f - Mathf.Exp(-dt * 5f)) : look;
        camFov = fov;
    }

    void ApplyCamera()
    {
        if (cam == null) return;
        Vector3 dir = camLook - camPos;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, -frameYaw, 0f);
        Vector3 jitter = new Vector3(Mathf.PerlinNoise(clock * 13f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, clock * 11f) - 0.5f, 0f) * shake * 2f;
        cam.transform.SetPositionAndRotation(camPos + rot * jitter, rot);
        cam.fieldOfView = camFov;
    }

    // ======================= CAPA (destellos y fundidos) =======================

    void UpdateOverlay(float dt)
    {
        float flashA = 0f;
        float end = 0f;
        if (cinematic)
        {
            if (t >= 29.1f && t < TSky) flashA = Smooth01((t - 29.1f) / 0.85f);
            else if (t >= TSky) flashA = t < TSky + 0.15f ? 1f : 1f - Smooth01((t - TSky - 0.15f) / 0.75f);
            end = Smooth01((t - (TLoop - 0.8f)) / 0.8f);
        }
        cutA = Mathf.MoveTowards(cutA, 0f, dt * 1.6f);
        dimA = Mathf.MoveTowards(dimA, cinematic ? 0f : 0.32f, dt * 1.5f);
        dim.color = MenuUI.WithAlpha(MenuUI.Ink, dimA);
        flash.color = new Color(0.88f, 0.97f, 1f, flashA);
        cut.color = MenuUI.WithAlpha(MenuUI.Ink, Mathf.Max(cutA, end));
    }

    // ======================= AYUDAS =======================

    static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    static float Smoother(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x * (x * (x * 6f - 15f) + 10f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.5f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x) - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u)
    {
        float v = 1f - u;
        return v * v * v * a + 3f * v * v * u * b + 3f * v * u * u * c + u * u * u * d;
    }
}
