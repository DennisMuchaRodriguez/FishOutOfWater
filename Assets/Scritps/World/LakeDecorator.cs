using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// ============================================================================
//  DECORACIÓN DEL LAGO
//  Reparte árboles, plantas, rocas y chatarra alrededor y dentro del lago según
//  un DecorationSet (Assets/Resources/Decoracion/Decoracion_Lago).
//   - Se agrega sola al darle Play si la escena tiene LakeWater y no tiene LakeDecorator.
//   - Misma semilla = mismo reparto. Lee el terreno, la pendiente y la forma del agua.
//   - Deja libres: la salida del jugador, el centro del lago (nada asoma del agua) y
//     una rampa sobre la orilla para que las aves no choquen con las copas.
//   - En el editor: botones "Generar ahora" / "Borrar" del Inspector (o clic derecho en el
//     componente). Lo generado en el editor se guarda con la escena y ya no se regenera al jugar.
//  Este objeto es la raíz "Decoracion": cuelga un hijo por zona y no debe moverse del origen.
// ============================================================================
[DisallowMultipleComponent]
[DefaultExecutionOrder(-45)]
public class LakeDecorator : MonoBehaviour
{
    public const string ResourcePath = "Decoracion/Decoracion_Lago";
    public static LakeDecorator Instance { get; private set; }

    [Tooltip("Lista de decoración. Vacío = se carga Resources/Decoracion/Decoracion_Lago")]
    public DecorationSet set;
    [Tooltip("Agua del lago. Vacío = la primera LakeWater de la escena")]
    public LakeWater water;
    [Tooltip("Punto de salida del jugador (se deja libre). Vacío = se busca el jugador")]
    public Transform playerStart;
    [Tooltip("Generar al darle Play (si no hay decoración ya generada en la escena)")]
    public bool generateOnStart = true;
    [Tooltip("Al darle Play, borrar lo generado en el editor y volver a generar")]
    public bool regenerateOnPlay = false;

    public int PlacedCount { get; private set; }
    public string LastReport { get; private set; }

    static bool warnedMissing;

    // ======================= SE AGREGA SOLA =======================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        warnedMissing = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetup()
    {
        // AfterSceneLoad solo avisa de la primera escena: las siguientes llegan por sceneLoaded
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAutoCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoCreate(scene);
    }

    static void TryAutoCreate(Scene scene)
    {
        if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded) return;
        LakeWater lake = null;
        foreach (LakeWater lw in LakeWater.All)
        {
            if (lw != null && lw.gameObject.scene == scene) { lake = lw; break; }
        }
        if (lake == null) return;
        if (FindFirstObjectByType<LakeDecorator>(FindObjectsInactive.Include) != null) return;
        DecorationSet loaded = Resources.Load<DecorationSet>(ResourcePath);
        if (loaded == null) return;

        GameObject go = new GameObject("Decoracion");
        SceneManager.MoveGameObjectToScene(go, scene);
        LakeDecorator d = go.AddComponent<LakeDecorator>();
        d.set = loaded;
        d.water = lake;
    }

    // ======================= CICLO DE VIDA =======================

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (!generateOnStart) return;
        if (transform.childCount > 0 && !regenerateOnPlay)
        {
            // Ya viene generada desde el editor (guardada con la escena)
            ApplySwaySettings(set != null ? set : Resources.Load<DecorationSet>(ResourcePath));
            return;
        }
        Generate();
    }

    // ======================= GENERAR / BORRAR =======================

    [ContextMenu("Generar ahora")]
    public void Generate()
    {
        Clear();
        DecorationSet s = set != null ? set : Resources.Load<DecorationSet>(ResourcePath);
        if (s == null) { LastReport = "Falta el DecorationSet (Resources/" + ResourcePath + ")."; return; }
        LakeWater w = ResolveWater();
        if (w == null) { LastReport = "No hay LakeWater en la escena."; return; }
        // La decoración se coloca con el agua ya construida (así sus colisiones nunca cuentan como suelo del lago)
        w.EnsureBuilt();
        if (!w.HasData) { LastReport = "El agua del lago no tiene datos (revisa LakeWater)."; return; }
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0) { LastReport = "No hay terrenos activos."; return; }

        float startTime = Time.realtimeSinceStartup;
        if (transform.parent == null)
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        Ctx c = new Ctx();
        c.set = s;
        c.water = w;
        c.surface = w.SurfaceY;
        c.lakeCenter = w.Centroid;
        c.lakeCenterRadius = s.lakeCenterClear * Mathf.Sqrt(w.Area / Mathf.PI);
        c.terrains = new List<TerrainInfo>();
        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null) continue;
            TerrainInfo ti = new TerrainInfo();
            ti.terrain = t;
            ti.data = t.terrainData;
            ti.pos = t.GetPosition();
            ti.size = t.terrainData.size;
            c.terrains.Add(ti);
        }
        Vector3 pp;
        c.hasPlayer = ResolvePlayer(out pp);
        c.playerPos = pp;
        c.sway = GetComponent<DecorationSway>();
        if (c.sway == null) c.sway = gameObject.AddComponent<DecorationSway>();
        ApplySwaySettings(s);

        BuildMap(c);

        // Primero lo grande (árboles, rocas) y luego lo pequeño, que rellena los huecos
        List<int> order = new List<int>();
        for (int i = 0; i < s.entries.Count; i++) order.Add(i);
        order.Sort((ia, ib) =>
        {
            float sa = s.entries[ia] != null ? s.entries[ia].minSpacing : 0f;
            float sb = s.entries[ib] != null ? s.entries[ib].minSpacing : 0f;
            int cmp = sb.CompareTo(sa);
            return cmp != 0 ? cmp : ia.CompareTo(ib);
        });

        List<string> missing = new List<string>();
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        int total = 0;
        foreach (int index in order)
        {
            DecorationEntry e = s.entries[index];
            if (e == null || !e.enabled) continue;
            if (e.prefab == null)
            {
                missing.Add(e.name);
                continue;
            }
            int placed = PlaceEntry(c, e, index);
            total += placed;
            report.Append(e.name).Append(": ").Append(placed).Append('\n');
        }
        PlacedCount = total;

        if (missing.Count > 0)
        {
            report.Append("Sin modelo (se saltan): ").Append(string.Join(", ", missing.ToArray())).Append('\n');
            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("Decoración del lago: todavía no hay modelo para " + string.Join(", ", missing.ToArray()) +
                                 ". Esas entradas se saltan hasta que existan.");
            }
        }
        float seconds = Time.realtimeSinceStartup - startTime;
        LastReport = total + " objetos en " + seconds.ToString("0.00") + " s\n" + report.ToString();
        if (total > 0) Debug.Log("Decoración del lago: " + total + " objetos en " + seconds.ToString("0.00") + " s.");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(c.sway);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Borrar")]
    public void Clear()
    {
        DecorationSway sw = GetComponent<DecorationSway>();
        if (sw != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(sw, "Borrar decoración");
#endif
            sw.Clear();
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                child.transform.SetParent(null, false);
                Destroy(child);
            }
            else
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(child);
#else
                DestroyImmediate(child);
#endif
            }
        }
        PlacedCount = 0;
        LastReport = null;
    }

    void ApplySwaySettings(DecorationSet s)
    {
        DecorationSway sw = GetComponent<DecorationSway>();
        if (sw == null || s == null) return;
        sw.maxDistance = s.swayDistance;
        sw.budget = s.swayBudget;
    }

    LakeWater ResolveWater()
    {
        if (water != null) return water;
        if (LakeVolume.Instance != null && LakeVolume.Instance.water != null) water = LakeVolume.Instance.water;
        else water = FindFirstObjectByType<LakeWater>();
        return water;
    }

    bool ResolvePlayer(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (playerStart != null) { pos = playerStart.position; return true; }
        PlayerController_Base p = FindFirstObjectByType<PlayerController_Base>();
        if (p != null) { pos = p.transform.position; return true; }
        GameObject tagged = GameObject.FindWithTag("Player");
        if (tagged != null) { pos = tagged.transform.position; return true; }
        return false;
    }

    // ======================= DATOS DEL REPARTO =======================

    struct TerrainInfo
    {
        public Terrain terrain;
        public TerrainData data;
        public Vector3 pos;
        public Vector3 size;
    }

    struct ModelInfo
    {
        public Bounds local;   // medidas en el espacio del objeto raíz (para las colisiones)
        public float top;      // altura sobre el pivote, ya de pie (escala 1)
        public float radius;   // radio horizontal desde el pivote (escala 1)
        public float size;     // mayor medida (escala 1)
        public int upAxis;     // eje local que queda hacia arriba
    }

    class Ctx
    {
        public DecorationSet set;
        public LakeWater water;
        public List<TerrainInfo> terrains;
        public int lastTerrain;
        public float surface;
        public Vector3 lakeCenter;
        public float lakeCenterRadius;
        public bool hasPlayer;
        public Vector3 playerPos;
        public DecorationSway sway;

        // Rejilla de análisis del terreno
        public float x0, z0, cell;
        public int nx, nz;
        public float[] ground;
        public float[] dist;    // distancia a la orilla: + en tierra, - en el agua
        public float[] slope;
        public bool[] wet;
        public bool[] valid;

        // Separación entre objetos (x, z, radio, capa) en celdas de HashCell metros
        public Dictionary<long, List<Vector4>> hash = new Dictionary<long, List<Vector4>>();
        public float maxRadius;
        public Dictionary<int, List<Vector2>> spots = new Dictionary<int, List<Vector2>>();
        public Dictionary<DecorZone, Transform> groups = new Dictionary<DecorZone, Transform>();
    }

    const float HashCell = 4f;
    const float Far = 1e9f;

    // ======================= MAPA DEL TERRENO =======================

    static void BuildMap(Ctx c)
    {
        Bounds b = c.water.WaterBounds;
        float pad = c.set.maxDistanceFromShore + 10f;
        c.cell = Mathf.Max(1f, c.set.gridCell);
        c.x0 = b.min.x - pad;
        c.z0 = b.min.z - pad;
        c.nx = Mathf.Max(2, Mathf.CeilToInt((b.size.x + pad * 2f) / c.cell));
        c.nz = Mathf.Max(2, Mathf.CeilToInt((b.size.z + pad * 2f) / c.cell));
        int n = c.nx * c.nz;
        c.ground = new float[n];
        c.dist = new float[n];
        c.slope = new float[n];
        c.wet = new bool[n];
        c.valid = new bool[n];

        for (int k = 0; k < c.nz; k++)
        {
            float z = c.z0 + (k + 0.5f) * c.cell;
            for (int i = 0; i < c.nx; i++)
            {
                float x = c.x0 + (i + 0.5f) * c.cell;
                int idx = k * c.nx + i;
                int t = FindTerrain(c, x, z);
                if (t < 0) { c.ground[idx] = c.surface; continue; }
                c.valid[idx] = true;
                c.ground[idx] = SampleHeight(c, t, x, z);
                c.wet[idx] = c.water.IsWaterXZ(x, z);
            }
        }

        // Distancia a la orilla (chaflán 1 / √2): a la celda de agua más cercana o a la de tierra
        float[] toWater = new float[n];
        float[] toLand = new float[n];
        for (int idx = 0; idx < n; idx++)
        {
            toWater[idx] = c.wet[idx] ? 0f : Far;
            toLand[idx] = c.wet[idx] ? Far : 0f;
        }
        Chamfer(toWater, c.nx, c.nz, c.cell);
        Chamfer(toLand, c.nx, c.nz, c.cell);
        float half = c.cell * 0.5f;
        for (int idx = 0; idx < n; idx++)
            c.dist[idx] = c.wet[idx] ? -(toLand[idx] - half) : toWater[idx] - half;

        // Pendiente (grados) con diferencias centrales
        for (int k = 0; k < c.nz; k++)
        {
            for (int i = 0; i < c.nx; i++)
            {
                int idx = k * c.nx + i;
                if (!c.valid[idx]) continue;
                int il = i > 0 && c.valid[idx - 1] ? i - 1 : i;
                int ir = i < c.nx - 1 && c.valid[idx + 1] ? i + 1 : i;
                int kd = k > 0 && c.valid[idx - c.nx] ? k - 1 : k;
                int ku = k < c.nz - 1 && c.valid[idx + c.nx] ? k + 1 : k;
                float gx = ir > il ? (c.ground[k * c.nx + ir] - c.ground[k * c.nx + il]) / ((ir - il) * c.cell) : 0f;
                float gz = ku > kd ? (c.ground[ku * c.nx + i] - c.ground[kd * c.nx + i]) / ((ku - kd) * c.cell) : 0f;
                c.slope[idx] = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
            }
        }
    }

    static void Chamfer(float[] d, int nx, int nz, float cell)
    {
        float a = cell, diag = cell * 1.4142136f;
        for (int k = 0; k < nz; k++)
        {
            for (int i = 0; i < nx; i++)
            {
                int idx = k * nx + i;
                float v = d[idx];
                if (i > 0) v = Mathf.Min(v, d[idx - 1] + a);
                if (k > 0)
                {
                    v = Mathf.Min(v, d[idx - nx] + a);
                    if (i > 0) v = Mathf.Min(v, d[idx - nx - 1] + diag);
                    if (i < nx - 1) v = Mathf.Min(v, d[idx - nx + 1] + diag);
                }
                d[idx] = v;
            }
        }
        for (int k = nz - 1; k >= 0; k--)
        {
            for (int i = nx - 1; i >= 0; i--)
            {
                int idx = k * nx + i;
                float v = d[idx];
                if (i < nx - 1) v = Mathf.Min(v, d[idx + 1] + a);
                if (k < nz - 1)
                {
                    v = Mathf.Min(v, d[idx + nx] + a);
                    if (i < nx - 1) v = Mathf.Min(v, d[idx + nx + 1] + diag);
                    if (i > 0) v = Mathf.Min(v, d[idx + nx - 1] + diag);
                }
                d[idx] = v;
            }
        }
    }

    static int FindTerrain(Ctx c, float x, float z)
    {
        int count = c.terrains.Count;
        if (c.lastTerrain < count && Contains(c.terrains[c.lastTerrain], x, z)) return c.lastTerrain;
        for (int t = 0; t < count; t++)
        {
            if (!Contains(c.terrains[t], x, z)) continue;
            c.lastTerrain = t;
            return t;
        }
        return -1;
    }

    static bool Contains(TerrainInfo ti, float x, float z)
    {
        return x >= ti.pos.x && x <= ti.pos.x + ti.size.x && z >= ti.pos.z && z <= ti.pos.z + ti.size.z;
    }

    static float SampleHeight(Ctx c, int t, float x, float z)
    {
        TerrainInfo ti = c.terrains[t];
        return ti.terrain.SampleHeight(new Vector3(x, 0f, z)) + ti.pos.y;
    }

    static int CellIndex(Ctx c, float x, float z)
    {
        int i = Mathf.FloorToInt((x - c.x0) / c.cell);
        int k = Mathf.FloorToInt((z - c.z0) / c.cell);
        if (i < 0 || k < 0 || i >= c.nx || k >= c.nz) return -1;
        return k * c.nx + i;
    }

    static Vector2 CellCenter(Ctx c, int idx)
    {
        return new Vector2(c.x0 + (idx % c.nx + 0.5f) * c.cell, c.z0 + (idx / c.nx + 0.5f) * c.cell);
    }

    // Filtro grueso por celda (con margen); el fino se hace en TryPlace
    static bool CellMatches(Ctx c, DecorationEntry e, int idx)
    {
        if (!c.valid[idx]) return false;
        float tol = c.cell;
        float d = c.dist[idx];
        if (d > c.set.maxDistanceFromShore + tol) return false;
        if (d < e.distanceRange.x - tol || d > e.distanceRange.y + tol) return false;
        float s = c.slope[idx];
        if (s < e.slopeRange.x - 5f || s > e.slopeRange.y + 5f) return false;
        bool wet = c.wet[idx];
        float depth = c.surface - c.ground[idx];
        float depthTol = c.cell * 0.6f;
        if (e.IsWaterZone)
        {
            if (!wet) return false;
            if (depth < e.depthRange.x - depthTol || depth > e.depthRange.y + depthTol) return false;
        }
        else if (e.zone == DecorZone.Orilla)
        {
            if (wet && depth > e.depthRange.y + depthTol) return false;
        }
        else if (wet) return false;
        return true;
    }

    // ======================= COLOCAR =======================

    int PlaceEntry(Ctx c, DecorationEntry e, int index)
    {
        List<int> cand = new List<int>();
        for (int idx = 0; idx < c.ground.Length; idx++)
            if (CellMatches(c, e, idx)) cand.Add(idx);
        if (cand.Count == 0) return 0;

        int target = e.count > 0 ? e.count : Mathf.RoundToInt(e.densityPer100m2 * cand.Count * c.cell * c.cell / 100f);
        target = Mathf.Min(target, c.set.maxPerEntry);
        if (target <= 0) return 0;

        ModelInfo info = Measure(e);
        System.Random rng = new System.Random(unchecked(c.set.seed * 7919 + index * 104729 + 17));
        List<Vector2> spots = e.spotGroup > 0 ? GetSpots(c, e.spotGroup, cand) : null;
        Transform parent = Group(c, e.zone);
        c.maxRadius = Mathf.Max(c.maxRadius, e.minSpacing * 0.5f);

        int cluster = Mathf.Max(1, e.clusterSize);
        int tries = cluster > 1 ? 3 : 1;
        int maxAttempts = (target / cluster + 1) * 15 + 30;
        int placed = 0, attempts = 0;
        while (placed < target && attempts < maxAttempts)
        {
            attempts++;
            Vector2 center;
            if (spots != null && spots.Count > 0)
            {
                center = spots[rng.Next(spots.Count)] + RandomInDisk(rng, c.set.spotRadius);
            }
            else
            {
                center = CellCenter(c, cand[rng.Next(cand.Count)]);
                center.x += (Rand(rng, 0f, 1f) - 0.5f) * c.cell;
                center.y += (Rand(rng, 0f, 1f) - 0.5f) * c.cell;
            }

            int members = Mathf.Min(cluster, target - placed);
            for (int m = 0; m < members; m++)
            {
                for (int t = 0; t < tries; t++)
                {
                    Vector2 p = center;
                    if (cluster > 1 && (m > 0 || t > 0)) p += RandomInDisk(rng, e.clusterRadius);
                    if (TryPlace(c, e, info, p.x, p.y, rng, parent)) { placed++; break; }
                }
            }
        }
        return placed;
    }

    bool TryPlace(Ctx c, DecorationEntry e, ModelInfo info, float x, float z, System.Random rng, Transform parent)
    {
        int t = FindTerrain(c, x, z);
        if (t < 0) return false;
        int cell = CellIndex(c, x, z);
        if (cell < 0 || !c.valid[cell]) return false;

        // Zona: distancia a la orilla, agua y profundidad
        float d = c.dist[cell];
        if (d < e.distanceRange.x || d > e.distanceRange.y || d > c.set.maxDistanceFromShore) return false;
        float ground = SampleHeight(c, t, x, z);
        bool wet = c.water.IsWaterXZ(x, z);
        float depth = c.surface - ground;
        if (e.IsWaterZone)
        {
            if (!wet || depth < e.depthRange.x || depth > e.depthRange.y) return false;
        }
        else if (e.zone == DecorZone.Orilla)
        {
            if (wet && depth > e.depthRange.y) return false;
        }
        else if (wet) return false;

        TerrainInfo ti = c.terrains[t];
        float u = (x - ti.pos.x) / ti.size.x;
        float v = (z - ti.pos.z) / ti.size.z;
        float slope = ti.data.GetSteepness(u, v);
        if (slope < e.slopeRange.x || slope > e.slopeRange.y) return false;

        // Salida del jugador
        if (c.hasPlayer)
        {
            float px = x - c.playerPos.x, pz = z - c.playerPos.z;
            if (px * px + pz * pz < c.set.playerClearRadius * c.set.playerClearRadius) return false;
        }

        float scale = Mathf.Max(0.05f, Rand(rng, e.scaleRange.x, e.scaleRange.y));

        // En el fondo nada asoma: si no cabe, se achica un poco o no va
        if (e.zone == DecorZone.FondoLago)
        {
            float room = depth + e.sinkOffset * scale - c.set.underwaterTopClearance;
            if (info.top * scale > room)
            {
                float fit = room / info.top;
                if (fit < e.scaleRange.x * 0.7f) return false;
                scale = fit;
            }
        }

        // Lo que asoma del agua (juncos, nenúfares, rocas de orilla) no va en el centro del lago
        bool emerges = e.zone == DecorZone.Superficie || e.zone == DecorZone.AguaBaja || (e.zone == DecorZone.Orilla && wet);
        if (emerges && c.lakeCenterRadius > 0f)
        {
            float cx = x - c.lakeCenter.x, cz = z - c.lakeCenter.z;
            if (cx * cx + cz * cz < c.lakeCenterRadius * c.lakeCenterRadius) return false;
        }

        // Objetos altos junto a la orilla: rampa libre sobre el agua para las aves
        if (!e.IsWaterZone && info.top * scale > c.set.tallHeight)
        {
            float allowed = c.surface + c.set.maxTopAboveWater + Mathf.Max(0f, d) * c.set.clearanceSlope;
            if (ground + info.top * scale > allowed)
            {
                float fit = (allowed - ground) / info.top;
                if (fit < e.scaleRange.x * 0.8f) return false;
                scale = fit;
            }
        }
        float radius = info.radius * scale;

        // Nenúfares: toda la hoja sobre el agua
        if (e.zone == DecorZone.Superficie)
        {
            float edge = Mathf.Max(0.2f, radius * 0.8f);
            if (!c.water.IsWaterXZ(x + edge, z) || !c.water.IsWaterXZ(x - edge, z) ||
                !c.water.IsWaterXZ(x, z + edge) || !c.water.IsWaterXZ(x, z - edge)) return false;
        }

        // Separación con lo ya colocado (los nenúfares van en su propia capa)
        int layer = e.zone == DecorZone.Superficie ? 1 : 0;
        float sep = e.minSpacing * 0.5f;
        if (!HasRoom(c, x, z, sep, layer)) return false;

        // Giro: corrección del modelo, giro aleatorio, inclinación extra y pendiente
        Vector3 normal = ti.data.GetInterpolatedNormal(u, v);
        Quaternion modelRot = Quaternion.Euler(e.modelRotation) * e.prefab.transform.rotation;
        Quaternion yaw = Quaternion.Euler(0f, e.randomYaw ? Rand(rng, 0f, 360f) : 0f, 0f);
        Quaternion tilt = Quaternion.identity;
        if (e.randomTilt > 0f)
        {
            Vector3 axis = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f) * Vector3.right;
            tilt = Quaternion.AngleAxis(Rand(rng, 0f, e.randomTilt), axis);
        }
        Quaternion align = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), e.alignToNormal);
        Quaternion rot = align * tilt * yaw * modelRot;

        float y;
        if (e.zone == DecorZone.Superficie)
        {
            y = c.surface + 0.03f - e.sinkOffset * scale;
        }
        else
        {
            // En pendiente se hunde un poco más para que el lado de abajo no quede flotando
            float effSlope = Mathf.Min(60f, slope * (1f - e.alignToNormal));
            float slopeSink = Mathf.Min(1.5f, Mathf.Tan(effSlope * Mathf.Deg2Rad) * radius * 0.3f);
            y = ground - e.sinkOffset * scale - slopeSink;
        }

        GameObject go = Spawn(e.prefab, parent);
        go.name = e.name;
        go.transform.SetPositionAndRotation(new Vector3(x, y, z), rot);
        go.transform.localScale = e.prefab.transform.localScale * (scale * e.modelScale);

        AddCollider(c, e, info, go, scale);
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (!e.castShadows)
        {
            foreach (Renderer rend in rends) rend.shadowCastingMode = ShadowCastingMode.Off;
        }
        if (e.cullScreenSize > 0f && rends.Length > 0 && go.GetComponent<LODGroup>() == null)
        {
            // Un solo LOD que se apaga cuando el objeto se ve diminuto (el bosque sale barato)
            LODGroup lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new LOD[] { new LOD(e.cullScreenSize, rends) });
            lod.RecalculateBounds();
        }
        if (e.sway != DecorSway.Ninguno)
            c.sway.Add(go.transform, e.sway, e.swayAmount, x * 0.07f + z * 0.05f + Rand(rng, 0f, 0.8f));

        AddToHash(c, x, z, sep, layer);
        return true;
    }

    GameObject Spawn(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // En el editor se mantiene el enlace al modelo (la escena pesa menos)
            GameObject linked = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (linked != null) return linked;
        }
#endif
        return Instantiate(prefab, parent);
    }

    Transform Group(Ctx c, DecorZone zone)
    {
        Transform g;
        if (c.groups.TryGetValue(zone, out g)) return g;
        GameObject go = new GameObject(zone.ToString());
        go.transform.SetParent(transform, false);
        g = go.transform;
        c.groups[zone] = g;
#if UNITY_EDITOR
        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Generar decoración");
#endif
        return g;
    }

    static List<Vector2> GetSpots(Ctx c, int group, List<int> cand)
    {
        List<Vector2> list;
        if (c.spots.TryGetValue(group, out list)) return list;
        list = new List<Vector2>();
        System.Random r = new System.Random(unchecked(c.set.seed * 31 + group * 977));
        int want = Mathf.Max(1, c.set.spotsPerGroup);
        float sepSq = c.set.spotSeparation * c.set.spotSeparation;
        for (int i = 0; i < 400 && list.Count < want; i++)
        {
            Vector2 p = CellCenter(c, cand[r.Next(cand.Count)]);
            bool ok = true;
            foreach (Vector2 q in list)
            {
                if ((q - p).sqrMagnitude < sepSq) { ok = false; break; }
            }
            if (ok) list.Add(p);
        }
        c.spots[group] = list;
        return list;
    }

    // ======================= SEPARACIÓN =======================

    static long Key(int i, int k)
    {
        return ((long)i << 32) | (long)(uint)k;
    }

    static bool HasRoom(Ctx c, float x, float z, float r, int layer)
    {
        float reach = r + c.maxRadius;
        int i0 = Mathf.FloorToInt((x - reach) / HashCell), i1 = Mathf.FloorToInt((x + reach) / HashCell);
        int k0 = Mathf.FloorToInt((z - reach) / HashCell), k1 = Mathf.FloorToInt((z + reach) / HashCell);
        for (int k = k0; k <= k1; k++)
        {
            for (int i = i0; i <= i1; i++)
            {
                List<Vector4> list;
                if (!c.hash.TryGetValue(Key(i, k), out list)) continue;
                foreach (Vector4 o in list)
                {
                    if ((int)o.w != layer) continue;
                    float dx = o.x - x, dz = o.y - z, rr = o.z + r;
                    if (dx * dx + dz * dz < rr * rr) return false;
                }
            }
        }
        return true;
    }

    static void AddToHash(Ctx c, float x, float z, float r, int layer)
    {
        long key = Key(Mathf.FloorToInt(x / HashCell), Mathf.FloorToInt(z / HashCell));
        List<Vector4> list;
        if (!c.hash.TryGetValue(key, out list))
        {
            list = new List<Vector4>();
            c.hash[key] = list;
        }
        list.Add(new Vector4(x, z, r, layer));
    }

    // ======================= MODELO =======================

    // Mide el modelo con sus mallas (funciona con el FBX sin instanciar)
    static ModelInfo Measure(DecorationEntry e)
    {
        ModelInfo info = new ModelInfo();
        Transform root = e.prefab.transform;
        Bounds lb = new Bounds();
        bool any = false;
        foreach (MeshFilter mf in e.prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh != null) Encapsulate(root, mf.transform, mf.sharedMesh.bounds, ref lb, ref any);
        }
        foreach (SkinnedMeshRenderer sk in e.prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (sk.sharedMesh != null) Encapsulate(root, sk.transform, sk.sharedMesh.bounds, ref lb, ref any);
        }
        if (!any) lb = new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one);
        info.local = lb;

        // Ya de pie: rotación y escala propias del modelo + corrección de la entrada
        Quaternion rot = Quaternion.Euler(e.modelRotation) * root.rotation;
        Vector3 scl = root.localScale * e.modelScale;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x,
                                         (i & 2) == 0 ? lb.min.y : lb.max.y,
                                         (i & 4) == 0 ? lb.min.z : lb.max.z);
            Vector3 w = rot * Vector3.Scale(corner, scl);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
        info.top = Mathf.Max(0.05f, max.y);
        info.radius = Mathf.Max(0.05f, Mathf.Max(Mathf.Max(-min.x, max.x), Mathf.Max(-min.z, max.z)));
        info.size = Mathf.Max(max.x - min.x, Mathf.Max(max.y - min.y, max.z - min.z));

        Vector3 up = Quaternion.Inverse(rot) * Vector3.up;
        float ax = Mathf.Abs(up.x), ay = Mathf.Abs(up.y), az = Mathf.Abs(up.z);
        info.upAxis = ax > ay && ax > az ? 0 : (ay >= az ? 1 : 2);
        return info;
    }

    static void Encapsulate(Transform root, Transform child, Bounds mb, ref Bounds lb, ref bool any)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x,
                                         (i & 2) == 0 ? mb.min.y : mb.max.y,
                                         (i & 4) == 0 ? mb.min.z : mb.max.z);
            Vector3 p = root.InverseTransformPoint(child.TransformPoint(corner));
            if (!any) { lb = new Bounds(p, Vector3.zero); any = true; }
            else lb.Encapsulate(p);
        }
    }

    static void AddCollider(Ctx c, DecorationEntry e, ModelInfo info, GameObject go, float scale)
    {
        if (e.colliderType == DecorCollider.Ninguno) return;
        // Bajo el agua solo lo grande choca (los peces y el jugador pasan entre algas y piedritas)
        if (e.IsWaterZone && info.size * scale < c.set.underwaterColliderMinSize) return;

        Bounds lb = info.local;
        float unit = Mathf.Max(0.0001f, Mathf.Abs(e.prefab.transform.localScale.x) * e.modelScale);
        switch (e.colliderType)
        {
            case DecorCollider.Capsula:
            {
                // Tronco: cápsula vertical en el pivote
                int axis = info.upAxis;
                CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
                cap.direction = axis;
                Vector3 center = Vector3.zero;
                center[axis] = lb.center[axis];
                cap.center = center;
                cap.height = lb.size[axis] * 0.95f;
                float across = axis == 0 ? Mathf.Min(lb.size.y, lb.size.z) : (axis == 1 ? Mathf.Min(lb.size.x, lb.size.z) : Mathf.Min(lb.size.x, lb.size.y));
                cap.radius = e.colliderRadius > 0f ? e.colliderRadius / unit : across * 0.08f;
                break;
            }
            case DecorCollider.Esfera:
            {
                SphereCollider sph = go.AddComponent<SphereCollider>();
                sph.center = lb.center;
                sph.radius = (lb.size.x + lb.size.y + lb.size.z) / 6f * 0.9f;
                break;
            }
            case DecorCollider.Caja:
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = lb.center;
                box.size = lb.size * 0.95f;
                break;
            }
            case DecorCollider.Malla:
            {
                // La malla necesita Read/Write activo en el modelo; si no, una caja
                bool added = false;
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    added = true;
                }
                if (!added)
                {
                    BoxCollider fallback = go.AddComponent<BoxCollider>();
                    fallback.center = lb.center;
                    fallback.size = lb.size * 0.95f;
                }
                break;
            }
        }
    }

    // ======================= AZAR =======================

    static float Rand(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    // Punto en un disco, más denso hacia el centro (grupos naturales)
    static Vector2 RandomInDisk(System.Random rng, float radius)
    {
        float a = Rand(rng, 0f, Mathf.PI * 2f);
        float r = radius * (float)rng.NextDouble();
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    // ======================= AYUDAS VISUALES =======================

    void OnDrawGizmosSelected()
    {
        LakeWater w = water != null ? water : FindFirstObjectByType<LakeWater>();
        if (w == null || !w.HasData) return;
        DecorationSet s = set != null ? set : Resources.Load<DecorationSet>(ResourcePath);
        if (s == null) return;
        Vector3 center = new Vector3(w.Centroid.x, w.SurfaceY, w.Centroid.z);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(center, s.lakeCenterClear * Mathf.Sqrt(w.Area / Mathf.PI));
        Vector3 pp;
        if (ResolvePlayer(out pp))
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(pp, s.playerClearRadius);
        }
    }
}
