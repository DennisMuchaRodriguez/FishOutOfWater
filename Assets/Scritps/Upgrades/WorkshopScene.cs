using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// El Taller (escena propia, GDD "El Taller"): una cueva bajo el lago donde el mecánico ofrece 3 planos de
// mejora y se instala solo 1. Todo se construye por código (como el menú principal); basta con este
// componente en la escena Taller. Si falta un modelo, se usa una figura simple.
//  - Cueva con algas que brillan, burbujas y rayos de luz que bajan desde la entrada.
//  - Mesa de chatarra, el traje en un soporte que gira, el mecánico (bagre con lentes) y alevines ayudantes.
//  - Los 3 planos son hologramas: clic = elegir, INSTALAR (o clic otra vez) = instalar.
//  - Esc o VOLVER AL MAPA sin elegir: la visita queda pendiente (botón IR AL TALLER en el mapa).
// Abrir la escena directo en el Editor muestra una visita de prueba (testLevelIndex).
public class WorkshopScene : MonoBehaviour
{
    [Header("Modelos (si falta alguno se usa una figura simple)")]
    public GameObject suitModel;
    public RuntimeAnimatorController suitAnimator;
    public GameObject mechanicModel;
    public GameObject fryModel;
    public Material fryMaterial;
    public RuntimeAnimatorController fryAnimator;
    public GameObject benchModel;
    [Tooltip("Chatarra sobre la mesa: lata, rueda de bicicleta, dron, batería...")]
    public GameObject[] scrapModels;
    public GameObject[] rockModels;
    public GameObject[] algaeModels;
    public GameObject glowAlgaModel;

    [Header("Efectos")]
    [Tooltip("Material base de las partículas (el mismo del lago)")]
    public Material fxMaterialTemplate;
    public Color glowColor = new Color(0.35f, 1f, 0.8f, 1f);

    [Header("Prueba (abrir la escena directo en el Editor)")]
    [Tooltip("Índice del nivel de la visita de prueba (nivel 3 = índice 2)")]
    public int testLevelIndex = 2;

    // ---- Visita ----
    int levelIndex;
    int visitNumber;
    bool testVisit;
    bool alreadyUsed;
    List<WorkshopOffer> offers = new List<WorkshopOffer>();
    int selected = -1;
    bool installing, installed, leaving;

    // ---- Escena ----
    Transform suitRoot, suitSpin;
    Transform installRing;
    Material installRingMat;
    ParticleSystem sparks, bubbles;
    WorkshopCritter mechanic;
    readonly List<WorkshopCritter> fry = new List<WorkshopCritter>();
    readonly List<Light> algaeLights = new List<Light>();

    // ---- UI ----
    RectTransform ui;
    readonly List<WorkshopCard> cards = new List<WorkshopCard>();
    Button installButton, backButton;
    TextMeshProUGUI installLabel, statusText, mechanicText;
    RectTransform installedList;
    Image fade;

    static readonly Vector3 SuitPos = new Vector3(2.3f, 0f, 0.6f);

    // Franjas de la interfaz (unidades de 1280x720, desde abajo): así los planos nunca tapan los botones
    const float ButtonsY = 36f;       // centro de la fila de botones (VOLVER AL MAPA, INSTALAR)
    const float StatusY = 80f;        // mensaje (instalando / listo)
    const float CardsBottom = 100f;   // borde de abajo de los planos
    static readonly Vector2 CardSize = new Vector2(290f, 310f);
    static readonly Vector3 BenchPos = new Vector3(-2.6f, 0f, 1.1f);

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        testVisit = !WorkshopFlow.HasVisit;
        levelIndex = testVisit ? testLevelIndex : WorkshopFlow.VisitLevelIndex;
        visitNumber = WorkshopFlow.VisitNumber(levelIndex);
        alreadyUsed = !testVisit && SaveSystem.IsWorkshopUsed(levelIndex);

        BuildEnvironment();
        BuildUI();

        WorkshopPlan plan = WorkshopPlan.Load();
        WorkshopVisit visit = plan != null ? plan.FindVisit(levelIndex, visitNumber) : null;
        mechanicText.text = MechanicLine(visit);

        StartCoroutine(FadeTo(0f, 0.8f));

        // Primera visita: el cómic del Taller (una sola vez) antes de elegir
        ComicDefinition comic = plan != null ? plan.firstVisitComic : null;
        if (comic != null && !SaveSystem.HasSeen(comic) && !alreadyUsed)
        {
            ComicViewer.Show(comic, () => { SaveSystem.MarkSeen(comic); ShowOffers(visit); });
        }
        else ShowOffers(visit);
    }

    string MechanicLine(WorkshopVisit visit)
    {
        if (alreadyUsed) return "¡Ese traje ya tiene su pieza nueva! Vuelve después del próximo nivel con más chatarra.";
        if (visit != null && !string.IsNullOrEmpty(visit.mechanicLine)) return visit.mechanicLine;
        if (visitNumber <= 1) return "¡Así que tú eres el pez del traje! Elige un plano: lo armo con lo que encontré en el fondo del lago.";
        return "Encontré más chatarra en el fondo. ¿Qué le ponemos al traje esta vez?";
    }

    // ======================= ESCENARIO =======================

    void BuildEnvironment()
    {
        // Agua verde azulada, niebla y luz que baja desde la entrada
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.03f, 0.13f, 0.17f);
        RenderSettings.fogDensity = 0.045f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.34f, 0.4f);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.fieldOfView = 50f;
            cam.transform.position = new Vector3(0f, 0.95f, -7f);
            cam.transform.LookAt(new Vector3(0f, 1.85f, 0f));
        }

        Light sun = new GameObject("LuzEntrada").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.6f, 0.9f, 1f);
        sun.intensity = 0.75f;
        sun.transform.rotation = Quaternion.Euler(62f, -25f, 0f);
        Light key = new GameObject("LuzTraje").AddComponent<Light>();
        key.type = LightType.Point;
        key.color = new Color(1f, 0.85f, 0.65f);
        key.intensity = 2.2f;
        key.range = 7f;
        key.transform.position = SuitPos + new Vector3(-1.2f, 2.8f, -2f);

        Transform env = new GameObject("Cueva").transform;

        // Paredes, techo y suelo
        Texture2D rockTex = WorkshopKit.NoiseTexture(128, 6f, 1.6f, new Color(0.07f, 0.13f, 0.15f), new Color(0.2f, 0.3f, 0.3f), "Taller_Roca", 3);
        Texture2D sandTex = WorkshopKit.NoiseTexture(128, 9f, 1.2f, new Color(0.42f, 0.4f, 0.32f), new Color(0.62f, 0.58f, 0.45f), "Taller_Arena", 7);
        GameObject cave = WorkshopKit.MeshObject("Paredes", env, WorkshopKit.CaveMesh(new Vector3(11f, 7.5f, 11f), 48, 24, 0.22f, 11), WorkshopKit.Lit(Color.white, rockTex, 0.15f, "Taller_Roca"));
        cave.transform.position = new Vector3(0f, 0f, 2f);
        GameObject floor = WorkshopKit.MeshObject("Suelo", env, WorkshopKit.FloorMesh(11f, 14, 48, 0.35f, 5), WorkshopKit.Lit(Color.white, sandTex, 0.1f, "Taller_Arena"));
        floor.transform.position = new Vector3(0f, 0f, 2f);

        BuildRocks(env);
        BuildAlgae(env);
        BuildRays(env);
        BuildBubbles(env);
        BuildBench(env);
        BuildSuit(env);
        BuildCritters(env);
    }

    void BuildRocks(Transform env)
    {
        Material rockMat = WorkshopKit.Lit(new Color(0.16f, 0.22f, 0.24f), null, 0.1f, "Taller_RocaSuelta");
        Vector3[] spots = { new Vector3(-6.5f, 0f, 4f), new Vector3(6.8f, 0f, 3.5f), new Vector3(-4.5f, 0f, 7f), new Vector3(4.2f, 0f, 7.5f),
                            new Vector3(-7.8f, 0f, -0.5f), new Vector3(7.5f, 0f, -1f), new Vector3(0.5f, 0f, 8.2f) };
        for (int i = 0; i < spots.Length; i++)
        {
            Transform holder = new GameObject("Roca" + i).transform;
            holder.SetParent(env, false);
            holder.position = spots[i];
            holder.rotation = Quaternion.Euler(0f, i * 67f, 0f);
            float size = 1.3f + (i % 3) * 0.6f;
            GameObject model = rockModels != null && rockModels.Length > 0 ? rockModels[i % rockModels.Length] : null;
            if (WorkshopKit.PlaceModel(model, holder, size, false) == null)
                WorkshopKit.Shape(PrimitiveType.Sphere, "Piedra", holder, Vector3.up * size * 0.25f, new Vector3(size, size * 0.6f, size * 0.85f), rockMat);
        }
    }

    // Algas que brillan (cian verdoso) junto a las paredes, con luces suaves
    void BuildAlgae(Transform env)
    {
        Material leafMat = WorkshopKit.Lit(new Color(0.12f, 0.4f, 0.3f), null, 0.3f, "Taller_Alga");
        Material glowMat = WorkshopKit.Lit(glowColor * 0.6f, null, 0.5f, "Taller_AlgaBrillante");
        WorkshopKit.SetEmission(glowMat, glowColor * 1.6f);
        Vector3[] spots = { new Vector3(-5.5f, 0f, 5.5f), new Vector3(5.8f, 0f, 5f), new Vector3(-7f, 0f, 2f), new Vector3(7.2f, 0f, 1.5f),
                            new Vector3(-2.5f, 0f, 7.6f), new Vector3(2.8f, 0f, 7.8f), new Vector3(-6f, 0f, -2.5f), new Vector3(6.2f, 0f, -2.8f) };
        for (int i = 0; i < spots.Length; i++)
        {
            bool glowing = i % 2 == 0;
            Transform holder = new GameObject(glowing ? "AlgaBrillante" + i : "Alga" + i).transform;
            holder.SetParent(env, false);
            holder.position = spots[i];
            holder.rotation = Quaternion.Euler(0f, i * 41f, 0f);
            GameObject model = glowing ? glowAlgaModel : (algaeModels != null && algaeModels.Length > 0 ? algaeModels[i % algaeModels.Length] : null);
            GameObject placed = WorkshopKit.PlaceModel(model, holder, 1.6f + (i % 3) * 0.5f, true);
            if (placed != null)
            {
                if (glowing) WorkshopKit.TintEmission(placed, glowColor * 1.4f);
            }
            else
            {
                // Tallos: cápsulas delgadas que se mecen
                for (int k = 0; k < 4; k++)
                {
                    float h = 1.2f + ((i + k) % 3) * 0.5f;
                    GameObject stalk = WorkshopKit.Shape(PrimitiveType.Capsule, "Tallo", holder, new Vector3((k - 1.5f) * 0.22f, h * 0.5f, (k % 2) * 0.2f),
                                                         new Vector3(0.12f, h * 0.5f, 0.12f), glowing ? glowMat : leafMat);
                    stalk.AddComponent<WorkshopSway>().amount = 6f + k;
                }
            }
            if (glowing)
            {
                Light l = new GameObject("LuzAlga").AddComponent<Light>();
                l.transform.SetParent(holder, false);
                l.transform.localPosition = Vector3.up * 1.2f;
                l.type = LightType.Point;
                l.color = glowColor;
                l.intensity = 1.4f;
                l.range = 4.5f;
                l.shadows = LightShadows.None;
                algaeLights.Add(l);
            }
        }
    }

    // Rayos de luz que bajan desde la entrada de la cueva
    void BuildRays(Transform env)
    {
        Material rayMat = WorkshopKit.Glow(fxMaterialTemplate, WorkshopKit.BeamTexture(), new Color(0.6f, 0.95f, 1f, 0.16f));
        Mesh beam = WorkshopKit.BeamMesh();
        for (int i = 0; i < 4; i++)
        {
            GameObject ray = WorkshopKit.MeshObject("Rayo" + i, env, beam, rayMat);
            ray.transform.position = new Vector3(-1.5f + i * 1.2f, 7.2f, 3.5f + (i % 2) * 1.2f);
            ray.transform.rotation = Quaternion.Euler(0f, 0f, 180f + (-14f + i * 7f));
            ray.transform.localScale = new Vector3(0.9f + (i % 3) * 0.4f, 7.5f, 1f);
            ray.AddComponent<WorkshopSway>().amount = 2f;
        }
    }

    void BuildBubbles(Transform env)
    {
        Material mat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.Bubble, Color.white);
        bubbles = FXFactory.CreateParticleSystem("Burbujas", env, mat);
        bubbles.transform.position = new Vector3(0f, 0f, 3f);
        var main = bubbles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startColor = new Color(0.8f, 0.97f, 1f, 0.6f);
        main.gravityModifier = -0.04f;
        main.maxParticles = 400;
        var emission = bubbles.emission;
        emission.rateOverTime = 18f;
        var shape = bubbles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 0.5f, 10f);
        var noise = bubbles.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.4f;
        bubbles.Play();
    }

    // Mesa de trabajo hecha de chatarra
    void BuildBench(Transform env)
    {
        Transform bench = new GameObject("Mesa").transform;
        bench.SetParent(env, false);
        bench.position = BenchPos;
        bench.rotation = Quaternion.Euler(0f, 18f, 0f);
        if (WorkshopKit.PlaceModel(benchModel, bench, 2.2f, false) == null)
        {
            Material wood = WorkshopKit.Lit(new Color(0.36f, 0.27f, 0.2f), null, 0.15f, "Taller_Tabla");
            Material metal = WorkshopKit.Lit(new Color(0.42f, 0.46f, 0.5f), null, 0.55f, "Taller_Metal");
            WorkshopKit.Shape(PrimitiveType.Cube, "Tabla", bench, new Vector3(0f, 0.95f, 0f), new Vector3(2.1f, 0.1f, 0.95f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -0.9f : 0.9f, z = i % 2 == 0 ? -0.35f : 0.35f;
                WorkshopKit.Shape(PrimitiveType.Cylinder, "Pata", bench, new Vector3(x, 0.45f, z), new Vector3(0.12f, 0.45f, 0.12f), metal);
            }
        }

        // Chatarra encima
        Vector3[] spots = { new Vector3(-0.65f, 1f, 0.1f), new Vector3(-0.1f, 1f, -0.15f), new Vector3(0.5f, 1f, 0.15f), new Vector3(0.85f, 1f, -0.2f) };
        float[] sizes = { 0.35f, 0.7f, 0.55f, 0.3f };
        Material can = WorkshopKit.Lit(new Color(0.8f, 0.3f, 0.2f), null, 0.6f, "Taller_Lata");
        Material dark = WorkshopKit.Lit(new Color(0.15f, 0.17f, 0.2f), null, 0.4f, "Taller_Oscuro");
        for (int i = 0; i < spots.Length; i++)
        {
            Transform holder = new GameObject("Chatarra" + i).transform;
            holder.SetParent(bench, false);
            holder.localPosition = spots[i];
            holder.localRotation = Quaternion.Euler(0f, i * 53f, 0f);
            GameObject model = scrapModels != null && i < scrapModels.Length ? scrapModels[i] : null;
            if (WorkshopKit.PlaceModel(model, holder, sizes[i], false) != null) continue;
            switch (i)
            {
                case 0: WorkshopKit.Shape(PrimitiveType.Cylinder, "Lata", holder, Vector3.up * 0.15f, new Vector3(0.18f, 0.15f, 0.18f), can); break;
                case 1:
                    GameObject wheel = WorkshopKit.MeshObject("Rueda", holder, WorkshopKit.RingMesh(0.26f, 0.32f, 32), dark);
                    wheel.transform.localPosition = Vector3.up * 0.33f;
                    wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case 2: WorkshopKit.Shape(PrimitiveType.Cube, "Dron", holder, Vector3.up * 0.08f, new Vector3(0.45f, 0.12f, 0.45f), dark); break;
                default: WorkshopKit.Shape(PrimitiveType.Cube, "Bateria", holder, Vector3.up * 0.1f, new Vector3(0.25f, 0.2f, 0.15f), can); break;
            }
        }
    }

    // El traje en un soporte que gira despacio
    void BuildSuit(Transform env)
    {
        Material metal = WorkshopKit.Lit(new Color(0.25f, 0.3f, 0.34f), null, 0.6f, "Taller_Soporte");
        Material holo = WorkshopKit.Glow(fxMaterialTemplate, FXFactory.White, new Color(0.41f, 0.87f, 0.9f, 0.5f));

        suitRoot = new GameObject("Soporte").transform;
        suitRoot.SetParent(env, false);
        suitRoot.position = SuitPos;
        WorkshopKit.Shape(PrimitiveType.Cylinder, "Base", suitRoot, new Vector3(0f, 0.08f, 0f), new Vector3(1.3f, 0.08f, 1.3f), metal);
        WorkshopKit.Shape(PrimitiveType.Cylinder, "Columna", suitRoot, new Vector3(0f, 0.6f, 0f), new Vector3(0.22f, 0.55f, 0.22f), metal);
        GameObject ring = WorkshopKit.MeshObject("AnilloHolo", suitRoot, WorkshopKit.RingMesh(0.55f, 0.62f, 48), holo);
        ring.transform.localPosition = Vector3.up * 1.18f;

        suitSpin = new GameObject("Traje").transform;
        suitSpin.SetParent(suitRoot, false);
        suitSpin.localPosition = Vector3.up * 1.25f;
        suitSpin.localRotation = Quaternion.Euler(0f, 200f, 0f);
        GameObject suit = WorkshopKit.PlaceModel(suitModel, suitSpin, 1.5f, true);
        if (suit != null)
        {
            Animator anim = suit.GetComponentInChildren<Animator>();
            if (anim == null && suitAnimator != null) anim = suit.AddComponent<Animator>();
            if (anim != null && suitAnimator != null)
            {
                anim.runtimeAnimatorController = suitAnimator;
                anim.applyRootMotion = false;
            }
        }
        else
        {
            Material orange = WorkshopKit.Lit(new Color(1f, 0.48f, 0.18f), null, 0.4f, "Taller_Traje");
            WorkshopKit.Shape(PrimitiveType.Capsule, "Traje", suitSpin, Vector3.up * 0.75f, new Vector3(0.9f, 0.75f, 0.9f), orange);
        }

        // Anillo de luz de la instalación (sube por el traje) y chispas
        installRingMat = WorkshopKit.Glow(fxMaterialTemplate, FXFactory.White, new Color(0.6f, 1f, 1f, 0f));
        installRing = WorkshopKit.MeshObject("AnilloInstalacion", suitRoot, WorkshopKit.RingMesh(0.75f, 0.95f, 48), installRingMat).transform;
        installRing.localPosition = Vector3.up * 1.2f;

        Material sparkMat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, Color.white);
        sparks = FXFactory.CreateParticleSystem("Chispas", suitRoot, sparkMat);
        sparks.transform.localPosition = Vector3.up * 1.9f;
        var main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(0.6f, 1f, 1f));
        main.gravityModifier = 0.6f;
        var shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;
        sparks.Play();
    }

    // El mecánico junto a la mesa y los alevines ayudantes nadando alrededor del traje
    void BuildCritters(Transform env)
    {
        Transform mech = new GameObject("Mecanico").transform;
        mech.SetParent(env, false);
        mech.position = new Vector3(-1.9f, 1.55f, 0.2f);
        mech.rotation = Quaternion.LookRotation(new Vector3(0.35f, 0f, -1f));
        Transform model = new GameObject("Modelo").transform;
        model.SetParent(mech, false);
        if (WorkshopKit.PlaceModel(mechanicModel, model, 1.4f, true) == null)
        {
            // Bagre provisional: cuerpo, lentes y bigotes
            Material skin = WorkshopKit.Lit(new Color(0.45f, 0.38f, 0.3f), null, 0.3f, "Taller_Bagre");
            Material lens = WorkshopKit.Lit(new Color(0.85f, 0.95f, 1f), null, 0.9f, "Taller_Lentes");
            WorkshopKit.Shape(PrimitiveType.Capsule, "Cuerpo", model, new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 0.6f, 0.7f), skin, new Vector3(90f, 0f, 0f));
            WorkshopKit.Shape(PrimitiveType.Sphere, "LenteI", model, new Vector3(-0.17f, 0.78f, 0.55f), Vector3.one * 0.22f, lens);
            WorkshopKit.Shape(PrimitiveType.Sphere, "LenteD", model, new Vector3(0.17f, 0.78f, 0.55f), Vector3.one * 0.22f, lens);
            for (int s = -1; s <= 1; s += 2)
                WorkshopKit.Shape(PrimitiveType.Cylinder, "Bigote", model, new Vector3(s * 0.3f, 0.52f, 0.62f), new Vector3(0.03f, 0.3f, 0.03f), skin, new Vector3(0f, 0f, s * 60f));
        }
        mechanic = mech.gameObject.AddComponent<WorkshopCritter>();
        mechanic.kind = WorkshopCritter.Kind.Mechanic;
        mechanic.lookTarget = suitSpin;

        for (int i = 0; i < 3; i++)
        {
            Transform f = new GameObject("Alevin" + i).transform;
            f.SetParent(env, false);
            GameObject inst = WorkshopKit.PlaceModel(fryModel, f, 0.45f, false);
            if (inst != null)
            {
                if (fryMaterial != null)
                    foreach (Renderer r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = fryMaterial;
                Animator anim = inst.GetComponentInChildren<Animator>();
                if (anim == null && fryAnimator != null) anim = inst.AddComponent<Animator>();
                if (anim != null && fryAnimator != null) anim.runtimeAnimatorController = fryAnimator;
            }
            else
            {
                Material gold = WorkshopKit.Lit(new Color(1f, 0.75f, 0.25f), null, 0.4f, "Taller_Alevin");
                WorkshopKit.Shape(PrimitiveType.Sphere, "Cuerpo", f, Vector3.zero, new Vector3(0.18f, 0.2f, 0.36f), gold);
            }
            WorkshopCritter c = f.gameObject.AddComponent<WorkshopCritter>();
            c.kind = WorkshopCritter.Kind.Fry;
            c.center = SuitPos;
            c.radius = 1.25f + i * 0.25f;
            c.height = 1.6f + i * 0.35f;
            c.speed = 0.55f + i * 0.12f;
            c.direction = i % 2 == 0 ? 1f : -1f;
            fry.Add(c);
        }
    }

    // ======================= INTERFAZ =======================

    void BuildUI()
    {
        Canvas canvas = MenuUI.CreateCanvas("TallerCanvas", 10);
        ui = (RectTransform)canvas.transform;
        MenuUI.VisorOverlay(ui);

        Vector2 top = new Vector2(0.5f, 1f);
        MenuUI.Title("Titulo", ui, "EL TALLER", 64f, top, new Vector2(0f, -48f), new Vector2(600f, 80f), MenuUI.Orange);
        string sub = "VISITA " + visitNumber + " DE " + WorkshopFlow.VisitCount + "  ·  DESPUÉS DEL NIVEL " + (levelIndex + 1);
        if (testVisit) sub += "  ·  PRUEBA";
        MenuUI.Text("Sub", ui, sub, 18f, top, new Vector2(0f, -94f), new Vector2(800f, 28f), MenuUI.Accent).characterSpacing = 4f;

        // Lo que dice el mecánico (arriba a la izquierda)
        RectTransform talk = MenuUI.ComicPanel("Mecanico", ui, new Vector2(0f, 1f), new Vector2(250f, -170f), new Vector2(430f, 112f), MenuUI.Glass);
        MenuUI.Tag("EL MECÁNICO", talk, new Vector2(0f, 1f), new Vector2(92f, 0f), MenuUI.Orange, 13f, MenuUI.Wrench);
        mechanicText = MenuUI.Text("Linea", talk, "", 18f, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(392f, 80f), MenuUI.Cream);
        mechanicText.alignment = TextAlignmentOptions.MidlineLeft;

        // Lo que ya tiene el traje (arriba a la derecha)
        RectTransform list = MenuUI.ComicPanel("Instalado", ui, new Vector2(1f, 1f), new Vector2(-170f, -168f), new Vector2(290f, 200f), MenuUI.Glass);
        MenuUI.Text("Encabezado", list, "EN TU TRAJE", 16f, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(260f, 24f), MenuUI.Accent)
            .characterSpacing = 4f;
        installedList = MenuUI.Rect("Lista", list, new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(260f, 152f));
        RefreshInstalled();

        // Abajo, en franjas que no se pisan: botones (y 13..63), mensaje (y 65..95) y los planos (desde y 100)
        statusText = MenuUI.Text("Estado", ui, "", 20f, new Vector2(0.5f, 0f), new Vector2(0f, StatusY), new Vector2(900f, 30f), MenuUI.Yellow);
        statusText.characterSpacing = 3f;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 13f;
        statusText.fontSizeMax = 20f;
        statusText.textWrappingMode = TextWrappingModes.NoWrap;

        installButton = MenuUI.ComicButton("INSTALAR", ui, new Vector2(0.5f, 0f), new Vector2(0f, ButtonsY), new Vector2(320f, 54f), MenuUI.Orange, OnInstallPressed, 26f);
        installLabel = installButton.GetComponentInChildren<TextMeshProUGUI>();
        installButton.gameObject.SetActive(false);

        backButton = MenuUI.ComicButton("VOLVER AL MAPA", ui, new Vector2(0f, 0f), new Vector2(150f, ButtonsY), new Vector2(240f, 46f), MenuUI.BlueDark, Leave, 20f);

        fade = MenuUI.Panel("Fundido", ui, MenuUI.Ink);
        fade.raycastTarget = false;
    }

    void RefreshInstalled()
    {
        foreach (Transform c in installedList) Destroy(c.gameObject);
        List<UpgradeId> ids = UpgradeCatalog.Installed();
        if (ids.Count == 0)
        {
            MenuUI.Text("Nada", installedList, "Todavía nada: el traje es el original.", 15f, new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                        new Vector2(250f, 40f), MenuUI.TextDim);
            return;
        }
        int rows = Mathf.Min(ids.Count, 6);
        for (int i = 0; i < rows; i++)
        {
            UpgradeId id = ids[i];
            float y = -12f - i * 22f;
            Color accent = UpgradeCatalog.AccentOf(id);
            MenuUI.Img("Icono", installedList, WorkshopIcons.For(id), accent, new Vector2(0f, 1f), new Vector2(14f, y), new Vector2(20f, 20f));
            TextMeshProUGUI t = MenuUI.Text("Mejora", installedList, UpgradeCatalog.NameOf(id) + "  <color=" + MenuUI.HexYellow + ">" +
                                            UpgradeDefinition.Roman(Upgrades.Level(id)) + "</color>", 14f, new Vector2(0f, 1f), new Vector2(140f, y),
                                            new Vector2(230f, 22f), MenuUI.Cream);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (ids.Count > rows)
            MenuUI.Text("Mas", installedList, "y " + (ids.Count - rows) + " más", 13f, new Vector2(0.5f, 1f), new Vector2(0f, -12f - rows * 22f),
                        new Vector2(250f, 22f), MenuUI.TextDim);
    }

    void ShowOffers(WorkshopVisit visit)
    {
        if (alreadyUsed)
        {
            statusText.text = "YA INSTALASTE LA MEJORA DE ESTA VISITA";
            SetContinue();
            return;
        }
        offers = WorkshopPlan.ResolveOffers(visit, visitNumber);
        if (offers.Count == 0)
        {
            statusText.text = "¡EL TRAJE YA TIENE TODAS LAS MEJORAS!";
            SetContinue();
            return;
        }
        float spacing = 320f;
        for (int i = 0; i < offers.Count; i++)
        {
            Vector2 pos = new Vector2((i - (offers.Count - 1) * 0.5f) * spacing, CardsBottom + CardSize.y * 0.5f);
            cards.Add(BuildCard(i, offers[i], pos));
            cards[i].Appear(0.15f + i * 0.12f);
        }
    }

    WorkshopCard BuildCard(int index, WorkshopOffer offer, Vector2 pos)
    {
        UpgradeDefinition def = UpgradeCatalog.Find(offer.upgrade);
        Color accent = UpgradeCatalog.AccentOf(offer.upgrade);
        UpgradeCategory category = UpgradeCatalog.CategoryOf(offer.upgrade);
        Vector2 size = CardSize;
        Vector2 top = new Vector2(0.5f, 1f);

        // Haz del proyector (de la mesa hacia el plano)
        Image beam = MenuUI.Img("Haz", ui, WorkshopIcons.Beam, MenuUI.WithAlpha(accent, 0.16f), new Vector2(0.5f, 0f), pos + new Vector2(0f, -165f), new Vector2(200f, 150f));
        beam.transform.SetSiblingIndex(1);

        RectTransform card = MenuUI.HoloPanel("Plano" + index, ui, new Vector2(0.5f, 0f), pos, size,
                                              MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, accent, 0.18f), 0.9f), MenuUI.WithAlpha(accent, 0.95f),
                                              MenuUI.WithAlpha(accent, 0.08f), MenuUI.WithAlpha(accent, 0.35f), 0.06f, true, true);
        card.SetSiblingIndex(fade.transform.GetSiblingIndex());
        card.Find("Relleno").GetComponent<Image>().raycastTarget = true;

        // Ícono del plano dentro de un hexágono
        MenuUI.Img("HaloIcono", card, MenuUI.HexGlow, MenuUI.WithAlpha(accent, 0.3f), top, new Vector2(0f, -62f), new Vector2(110f, 110f));
        MenuUI.Img("Hexagono", card, MenuUI.HexLine, accent, top, new Vector2(0f, -62f), new Vector2(86f, 86f));
        MenuUI.Img("Icono", card, WorkshopIcons.For(offer.upgrade), Color.Lerp(accent, Color.white, 0.35f), top, new Vector2(0f, -62f), new Vector2(56f, 56f));

        string name = def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : UpgradeCatalog.NameOf(offer.upgrade);
        TextMeshProUGUI title = MenuUI.Text("Nombre", card, name.ToUpper(), 22f, top, new Vector2(0f, -124f), new Vector2(270f, 30f), MenuUI.Cream, true);
        title.enableAutoSizing = true;
        title.fontSizeMin = 15f;
        title.fontSizeMax = 22f;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        MenuUI.Tag(UpgradeDefinition.CategoryLabel(category).ToUpper(), card, top, new Vector2(-62f, -153f), UpgradeDefinition.CategoryColor(category), 12f);
        MenuUI.Tag("NIVEL " + UpgradeDefinition.Roman(offer.level), card, top, new Vector2(82f, -153f), offer.level >= 2 ? MenuUI.Yellow : MenuUI.Blue, 12f);

        string desc = def != null ? def.description : "";
        FitText(MenuUI.Text("Descripcion", card, desc, 15f, top, new Vector2(0f, -194f), new Vector2(262f, 46f), MenuUI.TextDim));
        string what = def != null ? def.LevelText(offer.level) : "";
        if (offer.level >= 2 && def != null) what = "Nivel II: " + what;
        FitText(MenuUI.Text("Efecto", card, "<color=" + MenuUI.HexAccent + ">+</color> " + what, 15f, top, new Vector2(0f, -246f), new Vector2(262f, 54f), MenuUI.Cream));
        if (def != null && !string.IsNullOrEmpty(def.suitPart))
            FitText(MenuUI.Text("Pieza", card, "En el traje: " + def.suitPart, 12f, top, new Vector2(0f, -290f), new Vector2(262f, 18f), MenuUI.WithAlpha(MenuUI.TextDim, 0.8f)), 10f);

        WorkshopCard wc = card.gameObject.AddComponent<WorkshopCard>();
        wc.index = index;
        wc.onClick = OnCardClicked;
        wc.glow = card.Find("Brillo").GetComponent<Image>();
        wc.beam = beam;
        wc.group = card.gameObject.AddComponent<CanvasGroup>();
        wc.basePosition = pos;
        return wc;
    }

    // El texto se achica solo si no cabe (las descripciones largas no se salen del plano)
    static void FitText(TextMeshProUGUI t, float min = 12f)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = t.fontSize;
        t.overflowMode = TextOverflowModes.Truncate;
    }

    void OnCardClicked(int index)
    {
        if (installing || installed || leaving) return;
        if (index == selected)
        {
            OnInstallPressed();
            return;
        }
        selected = index;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].Selected = i == selected;
            cards[i].Dimmed = i != selected;
        }
        WorkshopOffer o = offers[selected];
        installLabel.text = "INSTALAR " + UpgradeDefinition.Roman(o.level);
        installButton.gameObject.SetActive(true);
        installButton.Select();
        statusText.text = "";
    }

    void OnInstallPressed()
    {
        if (installing || leaving) return;
        if (installed)
        {
            Continue();
            return;
        }
        if (selected < 0 || selected >= offers.Count) return;
        StartCoroutine(Install(offers[selected]));
    }

    IEnumerator Install(WorkshopOffer offer)
    {
        installing = true;
        installButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);
        foreach (WorkshopCard c in cards)
        {
            c.Interactive = false;
            c.Dimmed = c.index != selected;
        }
        string name = UpgradeCatalog.NameOf(offer.upgrade);
        statusText.text = "INSTALANDO " + name.ToUpper() + "...";

        // Los alevines se alborotan y el mecánico mira el traje
        mechanic.excitement = 1f;
        foreach (WorkshopCritter f in fry) f.excitement = 1f;

        // Un anillo de luz sube por el traje soltando chispas
        float t = 0f;
        const float dur = 1.8f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            installRing.localPosition = Vector3.up * Mathf.Lerp(1.15f, 2.85f, Mathf.SmoothStep(0f, 1f, k));
            installRing.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f);
            SetRingAlpha(Mathf.Sin(k * Mathf.PI) * 0.9f);
            if (Random.value < 0.6f) sparks.Emit(3);
            yield return null;
        }
        SetRingAlpha(0f);
        sparks.Emit(60);

        // Guarda la mejora y gasta la visita
        SaveSystem.SetUpgradeLevel(offer.upgrade, offer.level);
        if (!testVisit) SaveSystem.MarkWorkshopUsed(levelIndex);
        RefreshInstalled();

        mechanic.excitement = 0.3f;
        foreach (WorkshopCritter f in fry) f.excitement = 0.4f;
        statusText.text = "¡LISTO! " + name.ToUpper() + " " + UpgradeDefinition.Roman(offer.level) + " INSTALADO EN EL TRAJE";
        mechanicText.text = "¡Quedó de lujo! Pruébalo en el lago... y cuida a los peces.";
        installing = false;
        installed = true;
        SetContinue();
    }

    void SetRingAlpha(float a)
    {
        Color c = new Color(0.6f, 1f, 1f, Mathf.Clamp01(a));
        if (installRingMat.HasProperty("_BaseColor")) installRingMat.SetColor("_BaseColor", c);
        if (installRingMat.HasProperty("_Color")) installRingMat.SetColor("_Color", c);
    }

    // El botón principal pasa a CONTINUAR (siguiente nivel o mapa)
    void SetContinue()
    {
        bool next = !testVisit && WorkshopFlow.ContinueToNextLevel && GameSession.HasNextLevel;
        installLabel.text = next ? "SIGUIENTE NIVEL" : "VOLVER AL MAPA";
        installButton.gameObject.SetActive(true);
        installButton.Select();
        backButton.gameObject.SetActive(false);
        installed = true;
    }

    void Continue()
    {
        if (leaving) return;
        bool next = !testVisit && WorkshopFlow.ContinueToNextLevel && GameSession.HasNextLevel;
        LevelCatalog catalog = GameSession.Catalog;
        LevelDefinition nextLevel = next && catalog != null ? catalog.Get(GameSession.LevelIndex + 1) : null;
        ComicDefinition comic = nextLevel != null ? nextLevel.comicBefore : null;
        // Si el siguiente nivel tiene su cómic (los jefes) y no se ha visto, primero el cómic
        if (comic != null && !SaveSystem.HasSeen(comic))
        {
            ComicViewer.Show(comic, () => { SaveSystem.MarkSeen(comic); StartCoroutine(LeaveRoutine(true)); });
            return;
        }
        StartCoroutine(LeaveRoutine(next));
    }

    // Salir sin elegir: la visita queda pendiente
    void Leave()
    {
        if (leaving || installing) return;
        StartCoroutine(LeaveRoutine(false));
    }

    IEnumerator LeaveRoutine(bool playNext)
    {
        leaving = true;
        yield return FadeTo(1f, 0.5f);
        if (testVisit)
        {
            WorkshopFlow.ClearVisit();
            GameSession.GoToLevelMap();
        }
        else GameSession.LeaveWorkshop(playNext);
    }

    IEnumerator FadeTo(float target, float duration)
    {
        float start = fade.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fade.color = MenuUI.WithAlpha(MenuUI.Ink, Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        fade.color = MenuUI.WithAlpha(MenuUI.Ink, target);
    }

    void Update()
    {
        float time = Time.time;
        if (suitSpin != null) suitSpin.Rotate(0f, (installing ? 120f : 14f) * Time.deltaTime, 0f, Space.World);
        for (int i = 0; i < algaeLights.Count; i++)
            algaeLights[i].intensity = 1.2f + Mathf.Sin(time * 1.3f + i * 1.7f) * 0.35f;

        if (ComicViewer.IsShowing || Time.frameCount == ComicViewer.ClosedFrame || leaving) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (installed) Continue();
            else if (!installing) Leave();
        }
    }
}
