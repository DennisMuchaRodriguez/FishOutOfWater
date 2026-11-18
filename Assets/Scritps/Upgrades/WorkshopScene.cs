using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// El Taller (escena propia, GDD "El Taller"): una cueva bajo el lago donde el mecánico ofrece 3 planos de
// mejora y se instala solo 1. Todo se construye por código (como el menú principal); basta con este
// componente en la escena Taller. Si falta un modelo, se usa una figura simple.
//  - Cueva bajo el lago: estalactitas y estalagmitas, cristales que brillan, algas, la superficie del lago que
//    se ve arriba con rayos de luz que bajan, cáusticas que se mueven en el suelo, burbujas y un cardumen.
//  - Guarida de inventor (referencia: el taller de Tony Stark): sala de armaduras en una repisa con tres
//    vitrinas iluminadas (trajes viejos Mk I, II y III), mesa holográfica con el traje en holograma, brazo
//    robótico que suelda junto al traje, monitores y lámparas colgantes.
//  - Mesa de chatarra con herramientas, el traje en un soporte que gira, el mecánico (bagre con gafas) y alevines.
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

    [Header("Guarida (cueva y taller de inventor)")]
    [Tooltip("Vitrina para los trajes viejos (Vitrina_Traje)")]
    public GameObject displayCaseModel;
    [Tooltip("Mesa holográfica (Mesa_Holo)")]
    public GameObject holoTableModel;
    [Tooltip("Monitores (Pantallas_Taller)")]
    public GameObject monitorsModel;
    [Tooltip("Brazo robótico: base, tramo y pinza (BrazoRobot_*)")]
    public GameObject armBaseModel;
    public GameObject armSegmentModel;
    public GameObject armClawModel;
    [Tooltip("Racimo de estalactitas (también sirve de estalagmitas)")]
    public GameObject stalactiteModel;
    [Tooltip("Cristales que brillan (Cristales_Cueva)")]
    public GameObject crystalModel;

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
    // Guarida
    Transform armYaw, armShoulder, armElbow, armWrist, armTip, hologram;
    ParticleSystem weldSparks;
    Light weldLight;
    Material hologramMat, skylightMat, causticMatA, causticMatB;
    readonly List<Transform> holoRings = new List<Transform>();

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
        BuildCaveDetails(env);
        BuildLair(env);
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
        Material rayMat = WorkshopKit.Glow(fxMaterialTemplate, WorkshopKit.BeamTexture(), new Color(0.55f, 0.85f, 1f, 0.16f));
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
        bench.rotation = Quaternion.Euler(0f, benchModel != null ? 198f : 18f, 0f);
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
        Material holo = WorkshopKit.Glow(fxMaterialTemplate, FXFactory.White, new Color(0.25f, 0.72f, 1f, 0.5f));

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
        // Con el modelo (bagre erguido) flota sobre la arena junto a la mesa; el provisional va a la altura de antes
        mech.position = mechanicModel != null ? new Vector3(-1.55f, 0.3f, 0.1f) : new Vector3(-1.9f, 1.55f, 0.2f);
        mech.rotation = Quaternion.LookRotation(new Vector3(0.35f, 0f, -1f));
        Transform model = new GameObject("Modelo").transform;
        model.SetParent(mech, false);
        if (WorkshopKit.PlaceModel(mechanicModel, model, 1.7f, true) == null)
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

    // ======================= CUEVA BAJO EL LAGO =======================

    // Altura del techo de la cueva (la malla es un elipsoide de 11 x 7,5 x 11 m centrado en (0, 0, 2))
    static float CeilingAt(float x, float z)
    {
        float k = 1f - (x * x + (z - 2f) * (z - 2f)) / 121f;
        return 7.5f * Mathf.Sqrt(Mathf.Max(0.05f, k)) - 0.55f;
    }

    void BuildCaveDetails(Transform env)
    {
        Transform cave = new GameObject("DetallesCueva").transform;
        cave.SetParent(env, false);

        // Estalactitas colgando del techo y estalagmitas junto a las paredes
        Vector2[] up = { new Vector2(-4f, 3f), new Vector2(5.8f, 4.6f), new Vector2(0.4f, 8.6f), new Vector2(-6.2f, 6.8f),
                         new Vector2(6.3f, 2.4f), new Vector2(-2.2f, 9.2f), new Vector2(5.2f, 8.4f), new Vector2(-1.6f, 3.2f) };
        for (int i = 0; i < up.Length; i++)
        {
            Transform h = new GameObject("Estalactita" + i).transform;
            h.SetParent(cave, false);
            h.position = new Vector3(up[i].x, CeilingAt(up[i].x, up[i].y), up[i].y);
            h.rotation = Quaternion.Euler(0f, i * 53f, 0f);
            float sc = 1f + (i % 3) * 0.35f;
            if (!PlaceRaw(stalactiteModel, h, sc))
                WorkshopKit.Shape(PrimitiveType.Capsule, "Punta", h, Vector3.down * 0.6f * sc, new Vector3(0.25f, 0.6f, 0.25f) * sc,
                                  WorkshopKit.Lit(new Color(0.2f, 0.28f, 0.3f), null, 0.1f, "Taller_Estalactita"));
        }
        Vector3[] down = { new Vector3(-7.4f, 0f, 4.2f), new Vector3(7.7f, 0f, 5.1f), new Vector3(-5.2f, 0f, 8.6f),
                           new Vector3(5.6f, 0f, 8.9f), new Vector3(-8.2f, 0f, -0.8f) };
        for (int i = 0; i < down.Length && stalactiteModel != null; i++)
        {
            Transform h = new GameObject("Estalagmita" + i).transform;
            h.SetParent(cave, false);
            h.position = down[i] + Vector3.down * 0.15f;
            h.rotation = Quaternion.Euler(180f, i * 71f, 0f);   // dadas vuelta: crecen desde el suelo
            PlaceRaw(stalactiteModel, h, 0.9f + (i % 2) * 0.4f);
        }

        // Cristales que brillan (con una luz suave cada uno)
        Vector3[] crystals = { new Vector3(-6.3f, 0f, 1.6f), new Vector3(6.5f, 0f, 0.4f), new Vector3(0.9f, 0f, 8.9f),
                               new Vector3(-3.9f, 3.02f, 6.3f), new Vector3(4.3f, 3.02f, 6.4f), new Vector3(-7.2f, 0f, 6.3f) };
        Color crystalGlow = new Color(0.4f, 0.9f, 1f);
        for (int i = 0; i < crystals.Length && crystalModel != null; i++)
        {
            Transform h = new GameObject("Cristales" + i).transform;
            h.SetParent(cave, false);
            h.position = crystals[i];
            h.rotation = Quaternion.Euler(0f, i * 97f, 0f);
            PlaceRaw(crystalModel, h, 1.2f + (i % 3) * 0.35f);
            Light l = new GameObject("LuzCristal").AddComponent<Light>();
            l.transform.SetParent(h, false);
            l.transform.localPosition = Vector3.up * 0.7f;
            l.type = LightType.Point;
            l.color = crystalGlow;
            l.intensity = 1.1f;
            l.range = 3.5f;
            l.shadows = LightShadows.None;
            algaeLights.Add(l);
        }

        // La superficie del lago vista desde abajo: un claro brillante en el techo que ondula
        skylightMat = WorkshopKit.Glow(fxMaterialTemplate, RippleTexture(), new Color(0.65f, 0.92f, 1f, 0.55f));
        GameObject sky = WorkshopKit.Shape(PrimitiveType.Cylinder, "SuperficieDelLago", cave, new Vector3(0.6f, CeilingAt(0.6f, 4f) + 0.35f, 4f),
                                           new Vector3(4.2f, 0.01f, 3.4f), skylightMat);
        sky.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Cáusticas: la luz que atraviesa el agua dibuja una red que se mueve en el suelo (dos capas cruzadas)
        Texture2D caustic = CausticTexture();
        causticMatA = WorkshopKit.Glow(fxMaterialTemplate, caustic, new Color(0.55f, 0.85f, 1f, 0.22f));
        causticMatB = WorkshopKit.Glow(fxMaterialTemplate, caustic, new Color(0.55f, 0.85f, 1f, 0.16f));
        for (int k = 0; k < 2; k++)
        {
            GameObject q = WorkshopKit.Shape(PrimitiveType.Quad, "Causticas" + k, cave, new Vector3(0f, 0.04f + k * 0.01f, 3f),
                                             new Vector3(16f, 14f, 1f), k == 0 ? causticMatA : causticMatB, new Vector3(90f, 0f, 0f));
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        SetTiling(causticMatA, new Vector2(3f, 2.6f));
        SetTiling(causticMatB, new Vector2(2.2f, 1.9f));

        // Un cardumen chico nadando arriba, cerca del techo
        for (int i = 0; i < 6; i++)
        {
            Transform f = new GameObject("Cardumen" + i).transform;
            f.SetParent(cave, false);
            GameObject inst = WorkshopKit.PlaceModel(fryModel, f, 0.38f, false);
            if (inst == null) continue;
            if (fryMaterial != null)
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = fryMaterial;
            Animator anim = inst.GetComponentInChildren<Animator>();
            if (anim == null && fryAnimator != null) anim = inst.AddComponent<Animator>();
            if (anim != null && fryAnimator != null) anim.runtimeAnimatorController = fryAnimator;
            WorkshopCritter c = f.gameObject.AddComponent<WorkshopCritter>();
            c.kind = WorkshopCritter.Kind.Fry;
            c.center = new Vector3(0.5f, 0f, 4.5f);
            c.radius = 5f + (i % 3) * 0.5f;
            c.height = 4.4f + (i % 2) * 0.5f + i * 0.08f;
            c.speed = 0.22f + (i % 3) * 0.02f;
            c.direction = 1f;
        }
    }

    // ======================= GUARIDA DEL INVENTOR =======================

    void BuildLair(Transform env)
    {
        Transform lair = new GameObject("Guarida").transform;
        lair.SetParent(env, false);
        Material metal = WorkshopKit.Lit(new Color(0.26f, 0.3f, 0.34f), null, 0.6f, "Taller_Pasarela");
        Texture2D ledgeTex = WorkshopKit.NoiseTexture(128, 7f, 1.5f, new Color(0.08f, 0.13f, 0.15f), new Color(0.22f, 0.31f, 0.32f), "Taller_RocaRepisa", 13);
        Material rock = WorkshopKit.Lit(Color.white, ledgeTex, 0.1f, "Taller_Repisa");
        Material strip = WorkshopKit.Glow(fxMaterialTemplate, FXFactory.White, new Color(0.35f, 0.9f, 1f, 0.8f));

        // --- Sala de armaduras: repisa de roca al fondo con pasarela de metal y tres vitrinas ---
        // (alta, para que se vea por encima de los planos de la interfaz, como la sala de armaduras de Stark)
        WorkshopKit.Shape(PrimitiveType.Cube, "Repisa", lair, new Vector3(0.2f, 1.4f, 6.9f), new Vector3(11f, 3f, 2.4f), rock);
        WorkshopKit.Shape(PrimitiveType.Cube, "Pasarela", lair, new Vector3(0.2f, 2.94f, 6.4f), new Vector3(10.4f, 0.12f, 1.9f), metal);
        WorkshopKit.Shape(PrimitiveType.Cube, "LuzBorde", lair, new Vector3(0.2f, 2.9f, 5.44f), new Vector3(10.4f, 0.035f, 0.035f), strip);
        for (int i = 0; i < 9; i++)
            WorkshopKit.Shape(PrimitiveType.Cylinder, "Baranda" + i, lair, new Vector3(-4.8f + i * 1.25f, 3.35f, 5.5f),
                              new Vector3(0.05f, 0.4f, 0.05f), metal);
        WorkshopKit.Shape(PrimitiveType.Cube, "Pasamanos", lair, new Vector3(0.2f, 3.75f, 5.5f), new Vector3(10.2f, 0.06f, 0.06f), metal);

        // Trajes viejos: Mk I (hierro gris), Mk II (plata) y Mk III (rojo y oro)
        Color[] tints = { new Color(0.55f, 0.55f, 0.58f), new Color(0.88f, 0.9f, 0.96f), new Color(1f, 0.5f, 0.32f) };
        string[] names = { "MkI", "MkIII", "MkII" };
        float[] xs = { -3.3f, 0.2f, 3.7f };
        int[] tintIndex = { 0, 2, 1 };
        for (int i = 0; i < 3; i++)
        {
            Transform h = new GameObject("Vitrina" + names[i]).transform;
            h.SetParent(lair, false);
            h.position = new Vector3(xs[i], 3.0f, 6.55f);
            h.rotation = Quaternion.Euler(0f, 180f - xs[i] * 3f, 0f);
            if (WorkshopKit.PlaceModel(displayCaseModel, h, 2.7f, true) == null)
                WorkshopKit.Shape(PrimitiveType.Cube, "Nicho", h, new Vector3(0f, 1.3f, -0.5f), new Vector3(1.5f, 2.6f, 0.1f), metal);
            Transform inner = new GameObject("Traje").transform;
            inner.SetParent(h, false);
            inner.localPosition = new Vector3(0f, 0.17f, 0.05f);
            GameObject old = WorkshopKit.PlaceModel(suitModel, inner, 1.45f, true);
            if (old != null)
            {
                foreach (Animator a in old.GetComponentsInChildren<Animator>()) a.enabled = false;
                TintModel(old, tints[tintIndex[i]]);
            }
            Light l = new GameObject("LuzVitrina").AddComponent<Light>();
            l.transform.SetParent(h, false);
            l.transform.localPosition = new Vector3(0f, 2.3f, 0.3f);
            l.type = LightType.Point;
            l.color = new Color(0.75f, 0.92f, 1f);
            l.intensity = 2.2f;
            l.range = 3.4f;
            l.shadows = LightShadows.None;
        }

        // --- Mesa holográfica con el traje en holograma ---
        Transform table = new GameObject("MesaHolografica").transform;
        table.SetParent(lair, false);
        table.position = new Vector3(-1.1f, 0f, 3.3f);
        if (WorkshopKit.PlaceModel(holoTableModel, table, 1.0f, true) == null)
            WorkshopKit.Shape(PrimitiveType.Cylinder, "Mesa", table, new Vector3(0f, 0.48f, 0f), new Vector3(1.2f, 0.48f, 1.2f), metal);
        hologramMat = WorkshopKit.Glow(fxMaterialTemplate, FXFactory.White, new Color(0.3f, 0.85f, 1f, 0.22f));
        hologram = new GameObject("Holograma").transform;
        hologram.SetParent(table, false);
        hologram.localPosition = Vector3.up * 1.45f;
        GameObject holo = WorkshopKit.PlaceModel(suitModel, hologram, 1.5f, true);
        if (holo != null)
        {
            foreach (Animator a in holo.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (Renderer r in holo.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = hologramMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        Material ringMat = WorkshopKit.Glow(fxMaterialTemplate, FXFactory.White, new Color(0.35f, 0.9f, 1f, 0.6f));
        for (int k = 0; k < 3; k++)
        {
            GameObject ring = WorkshopKit.MeshObject("AnilloHolo" + k, table, WorkshopKit.RingMesh(0.55f + k * 0.12f, 0.58f + k * 0.12f, 48), ringMat);
            ring.transform.localPosition = Vector3.up * (1.2f + k * 0.65f);
            holoRings.Add(ring.transform);
        }
        Material beamMat = WorkshopKit.Glow(fxMaterialTemplate, WorkshopKit.BeamTexture(), new Color(0.4f, 0.9f, 1f, 0.18f));
        GameObject beam = WorkshopKit.MeshObject("HazHolo", table, WorkshopKit.BeamMesh(), beamMat);
        beam.transform.localPosition = Vector3.up * 0.98f;
        beam.transform.localScale = new Vector3(1.1f, 2.4f, 1f);
        beam.AddComponent<WorkshopSway>().amount = 1f;

        // --- Monitores a la izquierda, mirando al taller ---
        Transform mon = new GameObject("Monitores").transform;
        mon.SetParent(lair, false);
        mon.position = new Vector3(-4.6f, 0f, 3.9f);
        mon.rotation = Quaternion.Euler(0f, 180f + 28f, 0f);
        WorkshopKit.PlaceModel(monitorsModel, mon, 2.6f, true);

        // --- Brazo robótico junto al traje (suelda durante la instalación) ---
        BuildArm(lair, new Vector3(3.9f, 0f, 1.7f));

        // --- Lámparas colgantes sobre la mesa y el traje ---
        HangingLamp(lair, new Vector3(-2.5f, 4.3f, 1.4f));
        HangingLamp(lair, new Vector3(1.3f, 4.7f, 1.6f));
    }

    void BuildArm(Transform parent, Vector3 pos)
    {
        Transform root = new GameObject("BrazoRobot").transform;
        root.SetParent(parent, false);
        root.position = pos;
        root.localScale = Vector3.one * 1.25f;
        Material yellow = WorkshopKit.Lit(new Color(0.85f, 0.64f, 0.23f), null, 0.4f, "Taller_Brazo");

        armYaw = new GameObject("Giro").transform;
        armYaw.SetParent(root, false);
        if (!PlaceRaw(armBaseModel, armYaw, 1f))
            WorkshopKit.Shape(PrimitiveType.Cylinder, "Base", armYaw, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.25f, 0.5f), yellow);

        armShoulder = new GameObject("Hombro").transform;
        armShoulder.SetParent(armYaw, false);
        armShoulder.localPosition = Vector3.up * 0.5f;
        if (!PlaceRaw(armSegmentModel, armShoulder, 1f))
            WorkshopKit.Shape(PrimitiveType.Cube, "Tramo", armShoulder, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 1f, 0.2f), yellow);

        armElbow = new GameObject("Codo").transform;
        armElbow.SetParent(armShoulder, false);
        armElbow.localPosition = Vector3.up * 1f;
        if (!PlaceRaw(armSegmentModel, armElbow, 1f))
            WorkshopKit.Shape(PrimitiveType.Cube, "Tramo", armElbow, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 1f, 0.2f), yellow);

        armWrist = new GameObject("Muneca").transform;
        armWrist.SetParent(armElbow, false);
        armWrist.localPosition = Vector3.up * 1f;
        if (!PlaceRaw(armClawModel, armWrist, 1f))
            WorkshopKit.Shape(PrimitiveType.Cube, "Pinza", armWrist, new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.4f, 0.12f), yellow);

        armTip = new GameObject("Punta").transform;
        armTip.SetParent(armWrist, false);
        armTip.localPosition = Vector3.up * 0.45f;

        Material sparkMat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, Color.white);
        weldSparks = FXFactory.CreateParticleSystem("ChispasSoldadura", null, sparkMat);
        var main = weldSparks.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.gravityModifier = 0.7f;
        main.maxParticles = 400;
        weldSparks.Play();
        weldLight = new GameObject("LuzSoldadura").AddComponent<Light>();
        weldLight.transform.SetParent(armTip, false);
        weldLight.type = LightType.Point;
        weldLight.color = new Color(0.6f, 0.9f, 1f);
        weldLight.range = 3f;
        weldLight.intensity = 0f;
        weldLight.shadows = LightShadows.None;
    }

    void HangingLamp(Transform parent, Vector3 pos)
    {
        Material dark = WorkshopKit.Lit(new Color(0.12f, 0.13f, 0.15f), null, 0.4f, "Taller_Cable");
        Material shade = WorkshopKit.Lit(new Color(0.85f, 0.45f, 0.18f), null, 0.5f, "Taller_Lampara");
        float top = CeilingAt(pos.x, pos.z);
        float len = Mathf.Max(0.3f, top - pos.y);
        Transform lamp = new GameObject("Lampara").transform;
        lamp.SetParent(parent, false);
        lamp.position = new Vector3(pos.x, top, pos.z);
        WorkshopKit.Shape(PrimitiveType.Cylinder, "Cable", lamp, Vector3.down * len * 0.5f, new Vector3(0.025f, len * 0.5f, 0.025f), dark);
        WorkshopKit.Shape(PrimitiveType.Cylinder, "Pantalla", lamp, Vector3.down * (len + 0.08f), new Vector3(0.45f, 0.1f, 0.45f), shade);
        Material bulbMat = WorkshopKit.Lit(new Color(1f, 0.9f, 0.7f), null, 0.8f, "Taller_Foco");
        WorkshopKit.SetEmission(bulbMat, new Color(1f, 0.85f, 0.55f) * 2.5f);
        WorkshopKit.Shape(PrimitiveType.Sphere, "Foco", lamp, Vector3.down * (len + 0.2f), Vector3.one * 0.16f, bulbMat);
        Light l = new GameObject("Luz").AddComponent<Light>();
        l.transform.SetParent(lamp, false);
        l.transform.localPosition = Vector3.down * (len + 0.35f);
        l.type = LightType.Point;
        l.color = new Color(1f, 0.82f, 0.6f);
        l.intensity = 1.8f;
        l.range = 5.5f;
        l.shadows = LightShadows.None;
        lamp.gameObject.AddComponent<WorkshopSway>().amount = 1.5f;
    }

    void AnimateLair(float time, float dt)
    {
        // Brazo: en reposo se mece apuntando al traje; durante la instalación suelda (chispas y destellos)
        if (armYaw != null)
        {
            Vector3 to = SuitPos - armYaw.position;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + Mathf.Sin(time * 0.6f) * (installing ? 4f : 9f);
            armYaw.rotation = Quaternion.Euler(0f, yaw, 0f);
            float work = installing ? 1f : 0f;
            armShoulder.localRotation = Quaternion.Euler(28f + Mathf.Sin(time * 0.7f) * 6f + work * 6f, 0f, 0f);
            armElbow.localRotation = Quaternion.Euler(62f + Mathf.Sin(time * 0.9f + 1f) * 8f - work * 4f, 0f, 0f);
            armWrist.localRotation = Quaternion.Euler(28f + Mathf.Sin(time * 2.1f) * (installing ? 14f : 6f), Mathf.Sin(time * 1.3f) * 20f, 0f);

            bool weld = installing ? Mathf.Repeat(time * 1.7f, 1f) < 0.55f : Mathf.Repeat(time * 0.23f, 1f) < 0.08f;
            if (weld && weldSparks != null)
            {
                ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
                int n = installing ? 4 : 2;
                for (int i = 0; i < n; i++)
                {
                    ep.position = armTip.position;
                    ep.velocity = (Random.insideUnitSphere + Vector3.up * 0.6f) * Random.Range(1.5f, 4f);
                    ep.startColor = Color.Lerp(new Color(1f, 0.85f, 0.45f), new Color(0.6f, 0.95f, 1f), Random.value);
                    weldSparks.Emit(ep, 1);
                }
            }
            if (weldLight != null)
                weldLight.intensity = weld ? Random.Range(2f, 5f) * (installing ? 1.2f : 0.6f) : Mathf.MoveTowards(weldLight.intensity, 0f, dt * 30f);
        }

        // Holograma: gira, parpadea como una proyección y los anillos suben y bajan
        if (hologram != null)
        {
            hologram.Rotate(0f, (installing ? 60f : 22f) * dt, 0f, Space.World);
            float flicker = 0.18f + 0.04f * Mathf.Sin(time * 23f) + (Random.value < 0.03f ? -0.1f : 0f);
            SetGlowAlpha(hologramMat, new Color(0.3f, 0.85f, 1f), installing ? flicker + 0.1f : flicker);
            for (int i = 0; i < holoRings.Count; i++)
            {
                Transform r = holoRings[i];
                Vector3 lp = r.localPosition;
                lp.y = 1.2f + Mathf.Repeat(time * 0.35f + i / 3f, 1f) * 1.95f;
                r.localPosition = lp;
                r.Rotate(0f, (20f + i * 15f) * dt, 0f, Space.Self);
            }
        }

        // Agua: la superficie del lago ondula arriba y las cáusticas se mueven en el suelo
        if (skylightMat != null) SetOffset(skylightMat, new Vector2(time * 0.012f, time * 0.008f));
        if (causticMatA != null) SetOffset(causticMatA, new Vector2(time * 0.018f, time * 0.011f));
        if (causticMatB != null) SetOffset(causticMatB, new Vector2(-time * 0.013f, time * 0.016f));
    }

    // ======================= AYUDAS DE LA GUARIDA =======================

    // Pone un modelo con su pivote original (sin recentrar) y escalado; false si no hay modelo
    static bool PlaceRaw(GameObject model, Transform parent, float scale)
    {
        if (model == null) return false;
        GameObject inst = Instantiate(model, parent, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = inst.transform.localScale * scale;
        WorkshopKit.StripColliders(inst);
        return true;
    }

    // Tiñe un modelo con copias de sus materiales (los archivos no se tocan)
    static void TintModel(GameObject go, Color tint)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            Material[] mats = r.materials;
            foreach (Material m in mats)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                if (m.HasProperty("_Color")) m.SetColor("_Color", m.GetColor("_Color") * tint);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.6f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
            }
            r.materials = mats;
        }
    }

    static void SetGlowAlpha(Material m, Color c, float a)
    {
        if (m == null) return;
        Color col = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
    }

    static void SetOffset(Material m, Vector2 offset)
    {
        if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", offset);
        if (m.HasProperty("_MainTex")) m.SetTextureOffset("_MainTex", offset);
    }

    static void SetTiling(Material m, Vector2 tiling)
    {
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
        if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling);
    }

    // Red de cáusticas (que se repite sin cortes): líneas brillantes donde dos celdas de Voronoi se tocan
    static Texture2D CausticTexture()
    {
        const int S = 256, N = 28;
        System.Random rng = new System.Random(77);
        Vector2[] pts = new Vector2[N];
        for (int i = 0; i < N; i++) pts[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                float f1 = 9f, f2 = 9f;
                for (int i = 0; i < N; i++)
                {
                    Vector2 d = p - pts[i];
                    d.x -= Mathf.Round(d.x);
                    d.y -= Mathf.Round(d.y);
                    float dist = d.magnitude;
                    if (dist < f1) { f2 = f1; f1 = dist; }
                    else if (dist < f2) f2 = dist;
                }
                float edge = f2 - f1;
                float v = Mathf.Pow(Mathf.Clamp01(1f - edge / 0.06f), 2.2f);
                byte b = (byte)(v * 255f);
                px[y * S + x] = new Color32(255, 255, 255, b);
            }
        }
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
        tex.name = "Taller_Causticas";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    // Superficie del agua vista desde abajo: manchas claras que ondulan (se repite sin cortes)
    static Texture2D RippleTexture()
    {
        const int S = 128;
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S * Mathf.PI * 2f, v = y / (float)S * Mathf.PI * 2f;
                float w = Mathf.Sin(u * 2f + Mathf.Sin(v * 3f) * 0.8f) * 0.5f + Mathf.Sin(v * 2f + Mathf.Sin(u * 2f) * 1.1f) * 0.5f;
                float a = Mathf.Clamp01(0.5f + 0.45f * w);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
        tex.name = "Taller_SuperficieLago";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
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
        FitText(MenuUI.Text("Efecto", card, "<color=" + MenuUI.HexAccent + ">+</color> " + what, 15f, top, new Vector2(0f, -256f), new Vector2(262f, 74f), MenuUI.Cream));

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
        Sfx.Play2D(Sonido.Instalar, 0.9f);
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
        AnimateLair(time, Time.deltaTime);

        if (ComicViewer.IsShowing || Time.frameCount == ComicViewer.ClosedFrame || leaving) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (installed) Continue();
            else if (!installing) Leave();
        }
    }
}
