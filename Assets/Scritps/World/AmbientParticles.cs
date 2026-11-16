using System.Collections.Generic;
using UnityEngine;

// Efectos ambientales del lago (los crea LakeAmbience; siguen a la cámara y cuestan muy poco):
//  - HOJAS: caen de los árboles de hoja ancha cercanos (abedules, sauces, árboles de hoja), giran en el aire,
//    el viento las lleva y las que llegan al agua se quedan flotando un rato.
//  - POLEN: motas de luz cálida que flotan despacio alrededor de la cámara.
//  - NIEBLA BAJA: velos suaves sobre el agua, siempre a unos metros de la cámara (no tapan la acción).
// Con la cámara bajo el agua no se emiten (allí manda el filtro submarino).
[DisallowMultipleComponent]
public class AmbientParticles : MonoBehaviour
{
    [Header("Hojas")]
    public bool leaves = true;
    [Tooltip("Nombres de las entradas de la decoración que sueltan hojas")]
    public string[] leafTreeNames = { "Arboles de hoja", "Abedules", "Sauces de orilla" };
    [Tooltip("Hojas por segundo con viento normal (las ráfagas sueltan más)")]
    public float leavesPerSecond = 7f;
    [Tooltip("Solo caen hojas de los árboles a esta distancia de la cámara (m)")]
    public float leafRange = 55f;
    public Color[] leafColors =
    {
        new Color(0.45f, 0.66f, 0.24f), new Color(0.62f, 0.75f, 0.3f), new Color(0.78f, 0.78f, 0.28f),
        new Color(0.92f, 0.7f, 0.24f), new Color(0.86f, 0.47f, 0.18f)
    };

    [Header("Polen y motas de luz")]
    public bool pollen = true;
    public float pollenPerSecond = 16f;
    [Tooltip("Caja alrededor de la cámara donde aparecen (m)")]
    public Vector3 pollenBox = new Vector3(30f, 10f, 30f);

    [Header("Niebla baja sobre el agua")]
    public bool mist = true;
    public float mistPerSecond = 1.2f;
    [Tooltip("Distancia máxima a la cámara (m)")]
    public float mistRange = 75f;
    [Tooltip("Nunca más cerca de la cámara que esto (m), para no tapar la pelea")]
    public float mistMinDistance = 14f;
    [Range(0f, 0.3f)] public float mistOpacity = 0.09f;

    ParticleSystem leafPS, pollenPS, mistPS;
    Camera cam;
    float camTimer;
    LakeVolume lake;
    Transform waterPlane;
    readonly List<Vector4> trees = new List<Vector4>();   // xyz = centro de la copa, w = radio
    readonly List<int> nearTrees = new List<int>();
    float treeScanTimer, nearTimer;
    float leafAcc, pollenAcc, mistAcc;
    static Texture2D leafTex;

    void Start()
    {
        Material template = null;
        PlayerShooting shooting = FindFirstObjectByType<PlayerShooting>();
        if (shooting != null) template = shooting.fxMaterialTemplate;
        lake = LakeVolume.Instance;

        BuildLeaves(template);
        BuildPollen(template);
        BuildMist(template);
    }

    // ======================= CONSTRUCCIÓN =======================

    void BuildLeaves(Material template)
    {
        Material mat = FXFactory.ParticleMaterialAlpha(template, LeafTexture(), Color.white);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);   // se ven de los dos lados al girar
        leafPS = FXFactory.CreateParticleSystem("Hojas", transform, mat);
        var main = leafPS.main;
        main.maxParticles = 450;
        main.gravityModifier = 0.05f;
        main.startRotation3D = true;
        var em = leafPS.emission;
        em.enabled = false;

        // Giran sobre los tres ejes: se ven aletear al caer
        var rot = leafPS.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);
        rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rot.z = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);
        var noise = leafPS.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.3f;
        var fade = leafPS.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(Fade(0.06f, 0.82f, 1f));

        // Las hojas que llegan al agua se quedan flotando (y se van apagando)
        if (lake != null)
        {
            GameObject plane = new GameObject("PlanoAgua");
            plane.transform.SetParent(transform, false);
            plane.transform.position = new Vector3(0f, lake.SurfaceY + 0.02f, 0f);
            waterPlane = plane.transform;
            var col = leafPS.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            col.SetPlane(0, waterPlane);
            col.dampen = 1f;
            col.bounce = 0f;
            col.lifetimeLoss = 0f;
            col.radiusScale = 0.2f;
        }

        ParticleSystemRenderer r = leafPS.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        r.alignment = ParticleSystemRenderSpace.World;
        leafPS.Play();
    }

    void BuildPollen(Material template)
    {
        pollenPS = FXFactory.CreateParticleSystem("Polen", transform, FXFactory.ParticleMaterial(template, FXFactory.SoftDot, Color.white));
        var main = pollenPS.main;
        main.maxParticles = 320;
        var em = pollenPS.emission;
        em.enabled = false;
        var noise = pollenPS.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;
        var fade = pollenPS.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(Fade(0.25f, 0.7f, 1f));
        pollenPS.Play();
    }

    void BuildMist(Material template)
    {
        mistPS = FXFactory.CreateParticleSystem("NieblaBaja", transform, FXFactory.ParticleMaterialAlpha(template, FXFactory.SoftDot, Color.white));
        var main = mistPS.main;
        main.maxParticles = 90;
        main.startSize3D = true;
        var em = mistPS.emission;
        em.enabled = false;
        var fade = mistPS.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(Fade(0.3f, 0.7f, 1f));
        var size = mistPS.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));
        mistPS.Play();
    }

    // Aparece, se mantiene y se desvanece
    static Gradient Fade(float fadeIn, float fadeOut, float alpha)
    {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, fadeIn), new GradientAlphaKey(alpha, fadeOut),
                          new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // Hoja: óvalo con punta, nervio central y nervios laterales (blanca; el color va en cada partícula)
    static Texture2D LeafTexture()
    {
        if (leafTex != null) return leafTex;
        const int S = 64;
        leafTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        leafTex.name = "FX_Hoja";
        leafTex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2f - 1f;      // ancho
                float v = (y + 0.5f) / S;                // largo (0 = tallo, 1 = punta)
                // Contorno: más ancho cerca de la base y con punta
                float half = 0.62f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * 0.92f + 0.04f)), 0.8f) * (1f - 0.35f * v);
                float edge = half - Mathf.Abs(u);
                float a = Mathf.Clamp01(edge * S * 0.25f);
                // Tallito
                if (v < 0.08f && Mathf.Abs(u) < 0.05f) a = 1f;
                // Nervios: el central y algunos laterales más oscuros
                float vein = Mathf.Clamp01(1f - Mathf.Abs(u) * 30f) * 0.35f;
                float side = Mathf.Repeat(v * 6f - Mathf.Abs(u) * 2.2f, 1f);
                vein = Mathf.Max(vein, Mathf.Clamp01(1f - Mathf.Abs(side - 0.5f) * 14f) * 0.18f * Mathf.Clamp01(edge * 6f));
                float shade = 1f - vein - 0.12f * Mathf.Abs(u);
                byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f);
                px[y * S + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        leafTex.SetPixels32(px);
        leafTex.Apply();
        return leafTex;
    }

    // ======================= ACTUALIZACIÓN =======================

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        camTimer -= dt;
        if (cam == null || camTimer <= 0f)
        {
            cam = Camera.main;
            camTimer = 1f;
            if (lake == null) lake = LakeVolume.Instance;
        }
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float surface = lake != null ? lake.SurfaceY : float.NegativeInfinity;
        if (cp.y < surface) return;   // bajo el agua: nada de esto

        float wind = LakeAmbience.Wind01;
        Vector3 windDir = LakeAmbience.WindDirection;

        if (leaves && leafPS != null) UpdateLeaves(dt, cp, wind, windDir);
        if (pollen && pollenPS != null) EmitPollen(dt, cp, surface, windDir);
        if (mist && mistPS != null && lake != null) EmitMist(dt, cp, surface, windDir);
    }

    void UpdateLeaves(float dt, Vector3 cp, float wind, Vector3 windDir)
    {
        // Busca los árboles (la decoración se genera al empezar; se vuelve a mirar de vez en cuando)
        treeScanTimer -= dt;
        if (treeScanTimer <= 0f)
        {
            ScanTrees();
            treeScanTimer = trees.Count == 0 ? 3f : 20f;
        }
        if (trees.Count == 0) return;

        nearTimer -= dt;
        if (nearTimer <= 0f)
        {
            nearTimer = 0.5f;
            nearTrees.Clear();
            float maxSq = leafRange * leafRange;
            for (int i = 0; i < trees.Count; i++)
            {
                Vector4 t = trees[i];
                float dx = t.x - cp.x, dz = t.z - cp.z;
                if (dx * dx + dz * dz < maxSq) nearTrees.Add(i);
            }
        }
        if (nearTrees.Count == 0) return;

        // Más hojas con las ráfagas (y con más árboles cerca, hasta un tope)
        float trees01 = Mathf.Clamp01(nearTrees.Count / 6f);
        leafAcc += dt * leavesPerSecond * Mathf.Lerp(0.4f, 1.9f, wind) * Mathf.Lerp(0.4f, 1f, trees01);
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        int guard = 0;
        while (leafAcc >= 1f && guard++ < 20)
        {
            leafAcc -= 1f;
            Vector4 t = trees[nearTrees[Random.Range(0, nearTrees.Count)]];
            Vector3 r = Random.insideUnitSphere;
            ep.position = new Vector3(t.x + r.x * t.w, t.y + r.y * t.w * 0.4f, t.z + r.z * t.w);
            ep.velocity = windDir * Random.Range(0.6f, 2.2f) * (0.4f + wind) + Vector3.down * Random.Range(0.3f, 0.8f);
            ep.startSize = Random.Range(0.13f, 0.24f);
            ep.startLifetime = Random.Range(8f, 13f);
            ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            Color c = leafColors.Length > 0 ? leafColors[Random.Range(0, leafColors.Length)] : Color.green;
            float k = Random.Range(0.85f, 1.1f);
            ep.startColor = new Color(c.r * k, c.g * k, c.b * k, 1f);
            leafPS.Emit(ep, 1);
        }
    }

    void ScanTrees()
    {
        trees.Clear();
        LakeDecorator deco = LakeDecorator.Instance;
        if (deco == null) return;
        // Raíz "Decoracion" > zona > objetos con el nombre de su entrada
        foreach (Transform zone in deco.transform)
        {
            foreach (Transform item in zone)
            {
                if (!IsLeafTree(item.name)) continue;
                Renderer[] rends = item.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) continue;
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float radius = Mathf.Max(b.extents.x, b.extents.z) * 0.75f;
                trees.Add(new Vector4(b.center.x, b.min.y + b.size.y * 0.72f, b.center.z, radius));
            }
        }
    }

    bool IsLeafTree(string name)
    {
        foreach (string n in leafTreeNames)
            if (n == name) return true;
        return false;
    }

    void EmitPollen(float dt, Vector3 cp, float surface, Vector3 windDir)
    {
        pollenAcc += dt * pollenPerSecond;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        int guard = 0;
        while (pollenAcc >= 1f && guard++ < 20)
        {
            pollenAcc -= 1f;
            Vector3 p = cp + new Vector3(Random.Range(-0.5f, 0.5f) * pollenBox.x, Random.Range(-0.2f, 0.8f) * pollenBox.y,
                                         Random.Range(-0.5f, 0.5f) * pollenBox.z);
            if (p.y < surface + 0.3f) p.y = surface + Random.Range(0.3f, 2f);
            ep.position = p;
            ep.velocity = windDir * Random.Range(0.15f, 0.45f) + Random.insideUnitSphere * 0.12f;
            ep.startSize = Random.Range(0.035f, 0.085f);
            ep.startLifetime = Random.Range(6f, 10f);
            ep.startColor = Random.value < 0.75f ? new Color(1f, 0.93f, 0.7f, 0.55f) : new Color(0.8f, 0.95f, 1f, 0.5f);
            pollenPS.Emit(ep, 1);
        }
    }

    void EmitMist(float dt, Vector3 cp, float surface, Vector3 windDir)
    {
        mistAcc += dt * mistPerSecond;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        int guard = 0;
        while (mistAcc >= 1f && guard++ < 6)
        {
            mistAcc -= 1f;
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(mistMinDistance, mistRange);
            Vector3 p = new Vector3(cp.x + d.x, surface + Random.Range(0.4f, 1.4f), cp.z + d.y);
            if (!lake.IsInsideXZ(p, 2f)) continue;   // solo sobre el agua
            ep.position = p;
            ep.velocity = windDir * Random.Range(0.3f, 0.8f);
            ep.startSize3D = new Vector3(Random.Range(9f, 17f), Random.Range(2.2f, 3.8f), 1f);
            ep.startLifetime = Random.Range(16f, 24f);
            ep.startColor = new Color(0.86f, 0.93f, 1f, mistOpacity);
            mistPS.Emit(ep, 1);
        }
    }
}
