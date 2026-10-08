using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  AGUA DEL LAGO
//  Uso:
//   1. Pon este objeto dentro del hueco del lago (X/Z en cualquier punto del agua).
//   2. La altura (Y) del objeto ES el nivel del agua: muévelo arriba/abajo.
//      (o usa el botón "Ajustar altura al hueco" del Inspector)
//   3. La malla se genera sola llenando el hueco del terreno (como un balde de pintura)
//      y se vuelve a generar al mover el objeto o cambiar el terreno ("Reconstruir").
//  Todo lo que entra o sale del agua (jugador, peces, aves, balas) hace anillos
//  y salpicaduras gracias al componente WaterInteractor.
// ============================================================================
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LakeWater : MonoBehaviour
{
    public static readonly List<LakeWater> All = new List<LakeWater>();

    [Header("Forma del lago")]
    [Tooltip("Tamaño de cada celda de la malla (m). Más pequeño = más detalle y más polígonos")]
    [Range(0.5f, 6f)] public float cellSize = 2f;
    [Tooltip("Hasta qué distancia busca agua desde este punto (m)")]
    public float maxExtent = 400f;
    [Tooltip("Celdas que la malla se mete bajo la orilla (evita huecos en el borde)")]
    [Range(0, 4)] public int shoreOverlap = 2;
    [Tooltip("Capas que cuentan como suelo donde no hay Terrain")]
    public LayerMask groundMask = ~0;
    [Tooltip("Margen por debajo del nivel de desborde al usar 'Ajustar altura al hueco'")]
    public float autoHeightMargin = 1.5f;

    [Header("Salpicaduras")]
    [Tooltip("Material de partículas de base (ej. WaterJek)")]
    public Material splashMaterialTemplate;
    public Color splashColor = new Color(0.78f, 0.95f, 1f, 0.95f);
    [Tooltip("Efecto extra para golpes fuertes (opcional)")]
    public GameObject bigSplashPrefab;
    [Range(0f, 1f)] public float bigSplashThreshold = 0.55f;

    // ---- Datos generados (solo lectura) ----
    public bool HasData { get; private set; }
    public bool Spills { get; private set; }
    public string LastError { get; private set; }
    public float SurfaceY { get { return transform.position.y; } }
    public Bounds WaterBounds { get; private set; }
    public Vector3 Centroid { get; private set; }
    public float Area { get; private set; }
    public float MaxDepth { get; private set; }
    public int VertexCount { get; private set; }

    int nx, nz;
    float ox, oz, cs;
    bool[] mask;
    float[] ground;
    Mesh mesh;
    bool dirty = true;
    Vector3 builtPosition;
    float builtCell;

    // Anillos de impacto compartidos con el shader
    const int MaxRipples = 16;
    static readonly Vector4[] ripples = new Vector4[MaxRipples];
    static int rippleIndex;
    static readonly int RipplesId = Shader.PropertyToID("_WaterRipples");

    ParticleSystem drops, rings, mist;

    // ======================= CICLO DE VIDA =======================

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        if (Application.isPlaying)
        {
            for (int i = 0; i < MaxRipples; i++) ripples[i] = Vector4.zero;
        }
        Shader.SetGlobalVectorArray(RipplesId, ripples);
        dirty = true;
        // En el editor se construye ya; en juego se espera a Start (cuando los terrenos ya están activos)
        if (!Application.isPlaying) Rebuild();
    }

    void Start()
    {
        if (Application.isPlaying) EnsureBuilt();
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void OnDestroy()
    {
        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }

    // Construye el agua si todavía no se hizo en esta partida (lo usan las IA y el jugador)
    public void EnsureBuilt()
    {
        if (Application.isPlaying && playBuilt) return;
        Rebuild();
        if (Application.isPlaying) playBuilt = true;
    }

    bool playBuilt;

    void OnValidate()
    {
        dirty = true;
    }

    void Update()
    {
        if (Application.isPlaying) return;
        // En el editor: se reconstruye al mover el objeto o cambiar ajustes
        if (dirty || (transform.position - builtPosition).sqrMagnitude > 0.0001f || !Mathf.Approximately(builtCell, cellSize))
            Rebuild();
    }

    // ======================= CONSTRUCCIÓN =======================

    [ContextMenu("Reconstruir agua")]
    public void Rebuild()
    {
        dirty = false;
        builtPosition = transform.position;
        builtCell = cellSize;
        HasData = false;
        Spills = false;
        LastError = null;

        // La malla asume que el objeto no está rotado ni escalado
        if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
        if (transform.lossyScale != Vector3.one && transform.parent == null) transform.localScale = Vector3.one;

        cs = Mathf.Max(0.25f, cellSize);
        float surface = SurfaceY;
        int half = Mathf.Max(4, Mathf.CeilToInt(maxExtent / cs));
        int n = half * 2 + 1;
        float gx0 = transform.position.x - (half + 0.5f) * cs;
        float gz0 = transform.position.z - (half + 0.5f) * cs;

        Terrain[] terrains = Terrain.activeTerrains;
        float[] g = new float[n * n];
        bool[] sampled = new bool[n * n];
        System.Func<int, int, float> groundAt = (i, k) =>
        {
            int idx = k * n + i;
            if (!sampled[idx])
            {
                g[idx] = SampleGround(gx0 + (i + 0.5f) * cs, gz0 + (k + 0.5f) * cs, surface, terrains);
                sampled[idx] = true;
            }
            return g[idx];
        };

        if (groundAt(half, half) >= surface)
        {
            LastError = "El punto del objeto está sobre el terreno: muévelo dentro del hueco o súbelo.";
            ClearMesh();
            return;
        }

        // Relleno por inundación desde el punto del objeto
        bool[] flooded = new bool[n * n];
        Queue<int> queue = new Queue<int>();
        int seed = half * n + half;
        flooded[seed] = true;
        queue.Enqueue(seed);
        int minI = half, maxI = half, minK = half, maxK = half;
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int ci = c % n, ck = c / n;
            if (ci < minI) minI = ci;
            if (ci > maxI) maxI = ci;
            if (ck < minK) minK = ck;
            if (ck > maxK) maxK = ck;
            if (ci == 0 || ck == 0 || ci == n - 1 || ck == n - 1) { Spills = true; continue; }

            TryFlood(ci + 1, ck, n, flooded, queue, groundAt, surface);
            TryFlood(ci - 1, ck, n, flooded, queue, groundAt, surface);
            TryFlood(ci, ck + 1, n, flooded, queue, groundAt, surface);
            TryFlood(ci, ck - 1, n, flooded, queue, groundAt, surface);
        }

        // Recorta la rejilla al área del lago (+ margen bajo la orilla)
        int pad = shoreOverlap + 1;
        int i0 = Mathf.Max(0, minI - pad), i1 = Mathf.Min(n - 1, maxI + pad);
        int k0 = Mathf.Max(0, minK - pad), k1 = Mathf.Min(n - 1, maxK + pad);
        nx = i1 - i0 + 1;
        nz = k1 - k0 + 1;
        ox = gx0 + i0 * cs;
        oz = gz0 + k0 * cs;
        mask = new bool[nx * nz];
        ground = new float[nx * nz];

        float area = 0f, maxDepth = 0f, minGround = surface;
        Vector3 centroid = Vector3.zero;
        int count = 0;
        for (int k = 0; k < nz; k++)
        {
            for (int i = 0; i < nx; i++)
            {
                int gi = i + i0, gk = k + k0;
                int src = gk * n + gi;
                int dst = k * nx + i;
                mask[dst] = flooded[src];
                ground[dst] = groundAt(gi, gk);
                if (mask[dst])
                {
                    count++;
                    float depth = surface - ground[dst];
                    if (depth > maxDepth) maxDepth = depth;
                    if (ground[dst] < minGround) minGround = ground[dst];
                    centroid += new Vector3(ox + (i + 0.5f) * cs, surface, oz + (k + 0.5f) * cs);
                }
            }
        }
        area = count * cs * cs;
        Area = area;
        MaxDepth = maxDepth;
        Centroid = count > 0 ? centroid / count : transform.position;
        Vector3 min = new Vector3(ox, minGround, oz);
        Vector3 max = new Vector3(ox + nx * cs, surface, oz + nz * cs);
        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        WaterBounds = b;

        BuildMesh(surface);
        HasData = count > 0;
    }

    static void TryFlood(int i, int k, int n, bool[] flooded, Queue<int> queue, System.Func<int, int, float> groundAt, float surface)
    {
        if (i < 0 || k < 0 || i >= n || k >= n) return;
        int idx = k * n + i;
        if (flooded[idx]) return;
        if (groundAt(i, k) >= surface) return;
        flooded[idx] = true;
        queue.Enqueue(idx);
    }

    float SampleGround(float x, float z, float surface, Terrain[] terrains)
    {
        float best = float.NegativeInfinity;
        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null) continue;
            Vector3 tp = t.transform.position;
            Vector3 size = t.terrainData.size;
            if (x < tp.x || x > tp.x + size.x || z < tp.z || z > tp.z + size.z) continue;
            float h = t.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
            if (h > best) best = h;
        }
        if (!float.IsNegativeInfinity(best)) return best;

        // Sin terreno: raycast contra la geometría estática
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, surface + 300f, z), Vector3.down, 1000f, groundMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? surface - 30f : best;
    }

    void BuildMesh(float surface)
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "AguaLago (generada)";
            mesh.hideFlags = HideFlags.DontSave;
            mesh.MarkDynamic();
        }
        mesh.Clear();

        // Celdas a dibujar: las inundadas + un borde que se mete bajo la orilla
        bool[] draw = new bool[nx * nz];
        for (int k = 0; k < nz; k++)
        {
            for (int i = 0; i < nx; i++)
            {
                if (!mask[k * nx + i]) continue;
                for (int dk = -shoreOverlap; dk <= shoreOverlap; dk++)
                    for (int di = -shoreOverlap; di <= shoreOverlap; di++)
                    {
                        int a = i + di, c = k + dk;
                        if (a >= 0 && c >= 0 && a < nx && c < nz) draw[c * nx + a] = true;
                    }
            }
        }

        int vx = nx + 1, vz = nz + 1;
        int[] vIndex = new int[vx * vz];
        for (int v = 0; v < vIndex.Length; v++) vIndex[v] = -1;
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        Vector3 origin = transform.position;

        for (int k = 0; k < nz; k++)
        {
            for (int i = 0; i < nx; i++)
            {
                if (!draw[k * nx + i]) continue;
                int v00 = Vertex(i, k, vx, vIndex, verts, uvs, origin, surface);
                int v10 = Vertex(i + 1, k, vx, vIndex, verts, uvs, origin, surface);
                int v01 = Vertex(i, k + 1, vx, vIndex, verts, uvs, origin, surface);
                int v11 = Vertex(i + 1, k + 1, vx, vIndex, verts, uvs, origin, surface);
                tris.Add(v00); tris.Add(v01); tris.Add(v11);
                tris.Add(v00); tris.Add(v11); tris.Add(v10);
            }
        }

        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        Vector3[] normals = new Vector3[verts.Count];
        for (int v = 0; v < normals.Length; v++) normals[v] = Vector3.up;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        Bounds mb = mesh.bounds;
        mb.Expand(new Vector3(0f, 1f, 0f));
        mesh.bounds = mb;
        VertexCount = verts.Count;

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    int Vertex(int i, int k, int vx, int[] vIndex, List<Vector3> verts, List<Vector2> uvs, Vector3 origin, float surface)
    {
        int key = k * vx + i;
        if (vIndex[key] >= 0) return vIndex[key];

        // Profundidad en la esquina = promedio de las celdas que la rodean
        float sum = 0f;
        int c = 0;
        for (int dk = -1; dk <= 0; dk++)
            for (int di = -1; di <= 0; di++)
            {
                int a = i + di, b = k + dk;
                if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
                sum += ground[b * nx + a];
                c++;
            }
        float g = c > 0 ? sum / c : surface;

        vIndex[key] = verts.Count;
        verts.Add(new Vector3(ox + i * cs - origin.x, 0f, oz + k * cs - origin.z));
        uvs.Add(new Vector2(surface - g, 0f));
        return vIndex[key];
    }

    void ClearMesh()
    {
        if (mesh != null) mesh.Clear();
        VertexCount = 0;
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // Busca el nivel más alto al que el agua llena el hueco sin desbordarse
    public float FindSpillHeight()
    {
        float originalY = transform.position.y;
        Terrain[] terrains = Terrain.activeTerrains;
        float floor = SampleGround(transform.position.x, transform.position.z, originalY, terrains);
        float lo = floor + 0.05f, hi = floor + 200f;
        for (int it = 0; it < 14; it++)
        {
            float mid = (lo + hi) * 0.5f;
            SetY(mid);
            Rebuild();
            if (Spills || !HasData) hi = mid; else lo = mid;
        }
        SetY(originalY);
        Rebuild();
        return lo;
    }

    public void AutoFitHeight()
    {
        float spill = FindSpillHeight();
        SetY(spill - autoHeightMargin);
        Rebuild();
    }

    void SetY(float y)
    {
        Vector3 p = transform.position;
        p.y = y;
        transform.position = p;
    }

    // ======================= CONSULTAS =======================

    public bool IsWaterXZ(float x, float z)
    {
        if (!HasData) return false;
        int i = Mathf.FloorToInt((x - ox) / cs);
        int k = Mathf.FloorToInt((z - oz) / cs);
        if (i < 0 || k < 0 || i >= nx || k >= nz) return false;
        return mask[k * nx + i];
    }

    public float GroundAt(float x, float z)
    {
        if (!HasData) return SurfaceY - 30f;
        int i = Mathf.Clamp(Mathf.FloorToInt((x - ox) / cs), 0, nx - 1);
        int k = Mathf.Clamp(Mathf.FloorToInt((z - oz) / cs), 0, nz - 1);
        return ground[k * nx + i];
    }

    public static LakeWater FindAt(Vector3 p)
    {
        foreach (LakeWater w in All)
        {
            if (w == null) continue;
            if (Application.isPlaying && !w.playBuilt) w.EnsureBuilt();
            if (w.IsWaterXZ(p.x, p.z)) return w;
        }
        return null;
    }

    // ¿Hay agua en esta columna X/Z? Devuelve la altura de la superficie
    public static bool TryGetSurface(Vector3 p, out float surfaceY)
    {
        LakeWater w = FindAt(p);
        surfaceY = w != null ? w.SurfaceY : 0f;
        return w != null;
    }

    public static bool IsUnderwater(Vector3 p)
    {
        float s;
        return TryGetSurface(p, out s) && p.y < s;
    }

    // ======================= SALPICADURAS Y ANILLOS =======================

    // Salpicadura en la superficie. strength 0..1, entering = cae al agua (si no, sale)
    public static void Splash(Vector3 point, float strength, bool entering = true)
    {
        LakeWater w = FindAt(point);
        if (w == null && All.Count > 0) w = All[0];
        if (w != null) w.DoSplash(point, strength, entering);
    }

    // Ondas suaves al rozar la superficie
    public static void Wake(Vector3 point, float strength)
    {
        LakeWater w = FindAt(point);
        if (w == null) return;
        point.y = w.SurfaceY;
        AddRipple(point, Mathf.Clamp01(strength) * 0.6f, 0f);
        if (Application.isPlaying)
        {
            w.EnsureSplashFX();
            w.EmitDrops(point, 2, 0.35f, 0.5f);
        }
    }

    static void AddRipple(Vector3 p, float strength, float delay)
    {
        ripples[rippleIndex] = new Vector4(p.x, p.z, Time.timeSinceLevelLoad + delay, strength);
        rippleIndex = (rippleIndex + 1) % MaxRipples;
        Shader.SetGlobalVectorArray(RipplesId, ripples);
    }

    void DoSplash(Vector3 point, float strength, bool entering)
    {
        strength = Mathf.Clamp01(strength);
        point.y = SurfaceY;

        AddRipple(point, Mathf.Max(0.25f, strength), 0f);
        if (strength > 0.3f) AddRipple(point, strength * 0.6f, 0.28f);
        if (!Application.isPlaying) return;

        EnsureSplashFX();
        if (entering)
        {
            EmitDrops(point, Mathf.RoundToInt(8 + 45 * strength), 0.5f + strength * 1.3f, 0.6f + strength);
            EmitRing(point, 1.5f + 5f * strength);
            EmitMist(point, Mathf.RoundToInt(2 + 8 * strength), 0.6f + strength);
            if (bigSplashPrefab != null && strength >= bigSplashThreshold)
                FXFactory.SpawnOneShot(bigSplashPrefab, point, Quaternion.identity, 0.5f + strength * 0.8f, 3f);
        }
        else
        {
            // Salir del agua: chorrito hacia arriba y un anillo más pequeño
            EmitDrops(point, Mathf.RoundToInt(5 + 20 * strength), 0.4f + strength, 0.5f + strength * 0.6f);
            EmitRing(point, 1f + 3f * strength);
        }
    }

    void EmitDrops(Vector3 p, int count, float speedMul, float sizeMul)
    {
        if (drops == null) return;
        var main = drops.main;
        main.startSpeedMultiplier = 6f * speedMul;
        main.startSizeMultiplier = 0.22f * sizeMul;
        drops.transform.position = p;
        drops.Emit(count);
    }

    void EmitRing(Vector3 p, float size)
    {
        if (rings == null) return;
        var main = rings.main;
        main.startSizeMultiplier = size;
        rings.transform.position = p + Vector3.up * 0.03f;
        rings.Emit(1);
    }

    void EmitMist(Vector3 p, int count, float sizeMul)
    {
        if (mist == null) return;
        var main = mist.main;
        main.startSizeMultiplier = 1.2f * sizeMul;
        mist.transform.position = p;
        mist.Emit(count);
    }

    void EnsureSplashFX()
    {
        if (drops != null) return;

        // Gotas: bolitas que saltan y caen
        Material dropMat = FXFactory.ParticleMaterialAlpha(splashMaterialTemplate, FXFactory.SoftDot, Color.white);
        drops = FXFactory.CreateParticleSystem("Salpicadura_Gotas", transform, dropMat);
        var dm = drops.main;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        dm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        dm.startColor = new ParticleSystem.MinMaxGradient(splashColor, Color.white);
        dm.gravityModifier = 1.6f;
        dm.maxParticles = 600;
        var ds = drops.shape;
        ds.enabled = true;
        ds.shapeType = ParticleSystemShapeType.Cone;
        ds.angle = 28f;
        ds.radius = 0.35f;
        ds.rotation = new Vector3(-90f, 0f, 0f); // hacia arriba
        var dsz = drops.sizeOverLifetime;
        dsz.enabled = true;
        dsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
        var dcol = drops.colorOverLifetime;
        dcol.enabled = true;
        dcol.color = FadeGradient(0.9f);
        drops.Play();

        // Anillo de espuma plano sobre la superficie
        Texture2D ringTex = FXFactory.Ring;
        Material ringMat = FXFactory.ParticleMaterialAlpha(splashMaterialTemplate, ringTex, Color.white);
        rings = FXFactory.CreateParticleSystem("Salpicadura_Anillo", transform, ringMat);
        var rm = rings.main;
        rm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
        rm.startSpeed = 0f;
        rm.startSize = 1f;
        rm.startColor = splashColor;
        rm.maxParticles = 60;
        var rsz = rings.sizeOverLifetime;
        rsz.enabled = true;
        rsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
        var rcol = rings.colorOverLifetime;
        rcol.enabled = true;
        rcol.color = FadeGradient(0.85f);
        rings.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        rings.Play();

        // Bruma: nubecitas suaves en el impacto
        Material mistMat = FXFactory.ParticleMaterialAlpha(splashMaterialTemplate, FXFactory.SoftDot, Color.white);
        mist = FXFactory.CreateParticleSystem("Salpicadura_Bruma", transform, mistMat);
        var mm = mist.main;
        mm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        mm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        mm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        mm.startColor = new Color(splashColor.r, splashColor.g, splashColor.b, 0.35f);
        mm.gravityModifier = -0.05f;
        mm.maxParticles = 120;
        var ms = mist.shape;
        ms.enabled = true;
        ms.shapeType = ParticleSystemShapeType.Hemisphere;
        ms.radius = 0.5f;
        ms.rotation = new Vector3(-90f, 0f, 0f);
        var mcol = mist.colorOverLifetime;
        mcol.enabled = true;
        mcol.color = FadeGradient(0.35f);
        var msz = mist.sizeOverLifetime;
        msz.enabled = true;
        msz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
        mist.Play();
    }

    static ParticleSystem.MinMaxGradient FadeGradient(float alpha)
    {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    // ======================= AYUDAS VISUALES =======================

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        if (HasData)
        {
            Bounds b = WaterBounds;
            Gizmos.DrawWireCube(new Vector3(b.center.x, SurfaceY, b.center.z), new Vector3(b.size.x, 0.05f, b.size.z));
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
