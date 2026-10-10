using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Pinta los terrenos con las texturas pintadas a mano (Assets/Terreno) siguiendo reglas, para que el suelo
// combine con el agua y los personajes de plastilina:
//   - bajo el agua (solo la cuenca conectada al lago) -> lecho de colores (arena y limo, nunca gris)
//   - la orilla (de 0.4 m bajo el agua a 1.3 m sobre ella, cerca del lago) -> arena, y una franja de tierra
//   - el suelo plano -> pasto mezclado con su variante por manchas grandes
//   - pendientes medias y senderos -> tierra; pendientes fuertes -> roca (la ribera del lago es tierra)
// Al dar Play trabaja sobre una COPIA de cada TerrainData (los archivos no cambian). Para dejarlo fijo y
// no gastar tiempo al cargar: menú Herramientas > Terreno > Pintar terreno (permanente).
[DefaultExecutionOrder(-200)]
public class TerrainStylizer : MonoBehaviour
{
    [Header("Capas (en este orden)")]
    [Tooltip("0 Pasto, 1 Pasto variante, 2 Tierra, 3 Arena, 4 Lecho, 5 Roca")]
    public TerrainLayer[] layers = new TerrainLayer[6];
    [Tooltip("El agua del lago (Lago_Agua). Su altura es el nivel del agua y su posición, el centro del lago")]
    public Transform water;

    [Header("Cuándo")]
    [Tooltip("Pinta al dar Play (sobre una copia). Si el terreno ya está pintado con estas capas, no hace nada")]
    public bool paintOnPlay = true;

    [Header("Orilla y lecho")]
    public float shoreBelow = 0.4f;
    public float shoreAbove = 1.3f;
    [Tooltip("Distancia al lago (m) hasta donde llega la arena")]
    public float shoreMaxDist = 30f;
    public float dirtBand = 1.2f;
    [Range(0f, 1f)] public float dirtShoreAmount = 0.55f;
    [Tooltip("Hasta cuántos metros sobre la arena la ribera es tierra y no roca")]
    public float bankHeight = 6f;
    [Tooltip("En la ribera solo es roca la pared más empinada que esto (grados)")]
    public float bankRockSlope = 66f;

    [Header("Pendientes (grados)")]
    public float rockSlopeMin = 30f;
    public float rockSlopeMax = 40f;
    public float dirtSlopeMin = 20f;
    public float dirtSlopeMax = 30f;
    [Range(0f, 1f)] public float dirtSlopeAmount = 0.65f;

    [Header("Variación y senderos")]
    public int seed = 7;
    [Tooltip("Tamaño de las manchas de pasto variante (m)")]
    public float varScale = 60f;
    public float varMin = 0.40f;
    public float varMax = 0.62f;
    [Range(0f, 1f)] public float varAmount = 0.9f;
    [Tooltip("Tamaño del ruido de los bordes entre capas (m)")]
    public float edgeScale = 9f;
    public float pathScale = 120f;
    public float pathWidth = 0.022f;
    [Range(0f, 1f)] public float pathAmount = 0.75f;
    public float pathMaxSlope = 14f;

    [Header("Cuenca del lago")]
    public float lakeExtent = 450f;
    public float lakeCell = 1.5f;

    const int NoiseStep = 2;
    const int LayerCount = 6;

    void Awake()
    {
        if (!paintOnPlay || !LayersReady()) return;
        // (FindObjectsByType y no Terrain.activeTerrains: en Awake puede que no todos estén registrados aún)
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains == null || terrains.Length == 0) return;
        bool any = false;
        foreach (Terrain t in terrains) if (t != null && t.terrainData != null && !AlreadyPainted(t.terrainData)) any = true;
        if (!any) return;

        // Copias: así el Play nunca modifica los archivos del proyecto
        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null || AlreadyPainted(t.terrainData)) continue;
            TerrainData copy = Instantiate(t.terrainData);
            copy.name = t.terrainData.name + " (pintado)";
            t.terrainData = copy;
            TerrainCollider col = t.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = copy;
        }
        PaintAll(terrains);
    }

    bool LayersReady()
    {
        if (layers == null || layers.Length < LayerCount) return false;
        for (int i = 0; i < LayerCount; i++) if (layers[i] == null) return false;
        return true;
    }

    bool AlreadyPainted(TerrainData data)
    {
        TerrainLayer[] current = data.terrainLayers;
        if (current == null || current.Length != LayerCount) return false;
        for (int i = 0; i < LayerCount; i++) if (current[i] != layers[i]) return false;
        return true;
    }

    // ======================= PINTADO =======================

    class Tile
    {
        public Terrain terrain;
        public Vector3 pos, size;
        public int res;
        public float[,] h;       // alturas del mundo en la rejilla del heightmap [z, x]
    }

    List<Tile> tiles;
    float[,,] weights;   // se reutiliza entre terrenos de la misma resolución

    public void PaintAll(Terrain[] terrains)
    {
        if (!LayersReady()) return;
        float waterY = WaterY();
        Vector2 lakeCenter = LakeCenter();

        tiles = new List<Tile>();
        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null) continue;
            TerrainData d = t.terrainData;
            Tile tile = new Tile { terrain = t, pos = t.transform.position, size = d.size, res = d.heightmapResolution };
            float[,] raw = d.GetHeights(0, 0, tile.res, tile.res);
            tile.h = new float[tile.res, tile.res];
            for (int z = 0; z < tile.res; z++)
                for (int x = 0; x < tile.res; x++)
                    tile.h[z, x] = raw[z, x] * tile.size.y + tile.pos.y;
            tiles.Add(tile);
        }

        LakeMap lake = new LakeMap(this, lakeCenter, waterY, lakeExtent, lakeCell);
        foreach (Tile tile in tiles) PaintTile(tile, lake, waterY);
        tiles = null;
        weights = null;
    }

    float WaterY()
    {
        Transform w = FindWater();
        return w != null ? w.position.y : 0f;
    }

    Vector2 LakeCenter()
    {
        Transform w = FindWater();
        return w != null ? new Vector2(w.position.x, w.position.z) : Vector2.zero;
    }

    Transform FindWater()
    {
        if (water != null) return water;
        GameObject go = GameObject.Find("Lago_Agua");
        return go != null ? go.transform : null;
    }

    // Altura del mundo en (x, z): la del terreno más alto que cubre ese punto (NaN si ninguno)
    float WorldHeight(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (Tile t in tiles)
        {
            if (x < t.pos.x || x > t.pos.x + t.size.x || z < t.pos.z || z > t.pos.z + t.size.z) continue;
            float fi = (x - t.pos.x) / t.size.x * (t.res - 1);
            float fk = (z - t.pos.z) / t.size.z * (t.res - 1);
            best = Mathf.Max(best, Bilinear(t.h, fi, fk));
        }
        return best;
    }

    static float Bilinear(float[,] g, float fi, float fk)
    {
        int r1 = g.GetLength(0), r0 = g.GetLength(1);
        int i0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, r0 - 2);
        int k0 = Mathf.Clamp(Mathf.FloorToInt(fk), 0, r1 - 2);
        float fx = Mathf.Clamp01(fi - i0), fz = Mathf.Clamp01(fk - k0);
        float a = g[k0, i0], b = g[k0, i0 + 1], c = g[k0 + 1, i0], d = g[k0 + 1, i0 + 1];
        return (a + (b - a) * fx) * (1f - fz) + (c + (d - c) * fx) * fz;
    }

    // Pendiente (grados) en cada vértice del heightmap; en los bordes usa el terreno vecino (sin costuras)
    float[,] SlopeGrid(Tile t)
    {
        int r = t.res;
        float dx = t.size.x / (r - 1), dz = t.size.z / (r - 1);
        float[,] s = new float[r, r];
        for (int k = 0; k < r; k++)
        {
            for (int i = 0; i < r; i++)
            {
                float own = t.h[k, i];
                float l = i > 0 ? t.h[k, i - 1] : Edge(t.pos.x - dx, t.pos.z + k * dz, own);
                float rr = i < r - 1 ? t.h[k, i + 1] : Edge(t.pos.x + t.size.x + dx, t.pos.z + k * dz, own);
                float d = k > 0 ? t.h[k - 1, i] : Edge(t.pos.x + i * dx, t.pos.z - dz, own);
                float u = k < r - 1 ? t.h[k + 1, i] : Edge(t.pos.x + i * dx, t.pos.z + t.size.z + dz, own);
                float gx = (rr - l) / (2f * dx), gz = (u - d) / (2f * dz);
                s[k, i] = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
            }
        }
        return s;
    }

    float Edge(float x, float z, float own)
    {
        float w = WorldHeight(x, z);
        return float.IsInfinity(w) ? own : w;
    }

    void PaintTile(Tile t, LakeMap lake, float waterY)
    {
        TerrainData data = t.terrain.terrainData;
        data.terrainLayers = layers;
        int a = data.alphamapResolution;
        if (weights == null || weights.GetLength(0) != a) weights = new float[a, a, LayerCount];
        float[,,] w = weights;
        float[,] slope = SlopeGrid(t);

        // Ruido en una rejilla gruesa (cada 2 texeles) y luego interpolado
        int cn = a / NoiseStep + 2;
        float[,] nVarC = new float[cn, cn], nEdgeC = new float[cn, cn], nPathC = new float[cn, cn];
        for (int k = 0; k < cn; k++)
        {
            for (int i = 0; i < cn; i++)
            {
                float cx = t.pos.x + (i * NoiseStep + 0.5f) / a * t.size.x;
                float cz = t.pos.z + (k * NoiseStep + 0.5f) / a * t.size.z;
                nVarC[k, i] = Fbm(cx / varScale, cz / varScale, seed, 3);
                nEdgeC[k, i] = Fbm(cx / edgeScale, cz / edgeScale, seed + 101, 2) - 0.5f;
                nPathC[k, i] = Fbm(cx / pathScale, cz / pathScale, seed + 202, 2);
            }
        }

        float sa = shoreAbove;
        float[] layer = new float[LayerCount];
        for (int z = 0; z < a; z++)
        {
            for (int x = 0; x < a; x++)
            {
                float fci = x / (float)NoiseStep, fck = z / (float)NoiseStep;
                float nVar = Bilinear(nVarC, fci, fck), nEdge = Bilinear(nEdgeC, fci, fck), nPath = Bilinear(nPathC, fci, fck);
                float u = (x + 0.5f) / a, v = (z + 0.5f) / a;
                float wx = t.pos.x + u * t.size.x, wz = t.pos.z + v * t.size.z;
                float h = Bilinear(t.h, u * (t.res - 1), v * (t.res - 1));
                float sl = Bilinear(slope, u * (t.res - 1), v * (t.res - 1));
                float lk, dist;
                lake.Sample(wx, wz, out lk, out dist);
                float hRel = h - waterY;
                float near = 1f - Smooth(shoreMaxDist * 0.5f, shoreMaxDist, dist);

                // Pasto y su variante
                float wVar = Smooth(varMin, varMax, nVar) * varAmount;
                for (int l = 0; l < LayerCount; l++) layer[l] = 0f;
                layer[0] = 1f - wVar;
                layer[1] = wVar;

                // Tierra: pendientes medias, senderos y una franja sobre la arena de la orilla
                float dSlope = Smooth(dirtSlopeMin, dirtSlopeMax, sl + nEdge * 12f) * dirtSlopeAmount;
                float pathLine = 1f - Smooth(pathWidth * 0.5f, pathWidth, Mathf.Abs(nPath - 0.5f));
                float dPath = pathLine * (1f - Smooth(pathMaxSlope * 0.6f, pathMaxSlope, sl)) * pathAmount;
                float he = hRel + nEdge * 0.6f;
                float dShore = near * Smooth(sa - 0.2f, sa + 0.3f, he) * (1f - Smooth(sa + dirtBand - 0.3f, sa + dirtBand + 0.4f, he)) * dirtShoreAmount;
                Over(layer, 2, Mathf.Max(Mathf.Max(dSlope, dPath), dShore));

                // Arena de la orilla
                Over(layer, 3, near * (1f - Smooth(sa - 0.25f, sa + 0.25f, hRel + nEdge * 0.7f)));

                // Roca en las pendientes fuertes
                float wRock = Smooth(rockSlopeMin, rockSlopeMax, sl + nEdge * 12f);
                // Ribera: junto al lago la pendiente es tierra húmeda, no roca (salvo paredes casi verticales)
                float bank = near * (1f - Smooth(sa + bankHeight - 1f, sa + bankHeight + 1f, hRel + nEdge * 0.8f));
                wRock *= 1f - 0.9f * bank * (1f - Smooth(bankRockSlope - 5f, bankRockSlope + 5f, sl));
                Over(layer, 5, wRock);

                // Lecho del lago (solo la cuenca conectada al agua)
                float depth = -hRel;
                Over(layer, 4, lk * Smooth(shoreBelow - 0.25f, shoreBelow + 0.5f, depth + nEdge * 0.5f) * (1f - 0.5f * wRock));

                float sum = 0f;
                for (int l = 0; l < LayerCount; l++) sum += layer[l];
                sum = Mathf.Max(sum, 1e-6f);
                for (int l = 0; l < LayerCount; l++) w[z, x, l] = layer[l] / sum;
            }
        }
        data.SetAlphamaps(0, 0, w);
        data.SetBaseMapDirty();
    }

    static void Over(float[] layer, int k, float amount)
    {
        amount = Mathf.Clamp01(amount);
        for (int l = 0; l < layer.Length; l++) layer[l] *= 1f - amount;
        layer[k] += amount;
    }

    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    // ---- Ruido de valor determinista (mismo resultado en todas las máquinas) ----

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = ((uint)x * 0x27d4eb2du) ^ ((uint)y * 0x165667b1u) ^ ((uint)seed * 0x9e3779b9u);
            h ^= h >> 15; h *= 0x85ebca6bu; h ^= h >> 13; h *= 0xc2b2ae35u; h ^= h >> 16;
            return (h & 0xFFFFFFu) * (1f / 16777215f);
        }
    }

    static float ValueNoise(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
        float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        float ab = a + (b - a) * ux, cd = c + (d - c) * ux;
        return ab + (cd - ab) * uy;
    }

    static float Fbm(float x, float y, int seed, int octaves)
    {
        float sum = 0f, amp = 1f, total = 0f, f = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * ValueNoise(x * f, y * f, seed + o * 31);
            total += amp;
            amp *= 0.5f;
            f *= 2f;
        }
        return sum / total;
    }

    // ---- Cuenca del lago: celdas bajo el agua conectadas al centro y distancia a ellas ----

    class LakeMap
    {
        readonly float cell, x0, z0;
        readonly int n;
        readonly float[,] mask, dist;

        public LakeMap(TerrainStylizer owner, Vector2 center, float waterY, float extent, float cellSize)
        {
            cell = Mathf.Max(0.5f, cellSize);
            n = Mathf.CeilToInt(2f * extent / cell) + 1;
            x0 = center.x - extent;
            z0 = center.y - extent;
            mask = new float[n, n];
            dist = new float[n, n];

            bool[,] under = new bool[n, n];
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                {
                    float h = owner.WorldHeight(x0 + (i + 0.5f) * cell, z0 + (k + 0.5f) * cell);
                    under[k, i] = !float.IsInfinity(h) && h < waterY;
                }

            // Relleno desde el centro: solo el agua conectada al lago (no los charcos de otras hondonadas)
            int si = Mathf.Clamp((int)((center.x - x0) / cell), 0, n - 1), sk = Mathf.Clamp((int)((center.y - z0) / cell), 0, n - 1);
            bool[,] lake = new bool[n, n];
            if (under[sk, si])
            {
                Queue<Vector2Int> q = new Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(si, sk));
                lake[sk, si] = true;
                while (q.Count > 0)
                {
                    Vector2Int p = q.Dequeue();
                    TryAdd(p.x + 1, p.y, under, lake, q);
                    TryAdd(p.x - 1, p.y, under, lake, q);
                    TryAdd(p.x, p.y + 1, under, lake, q);
                    TryAdd(p.x, p.y - 1, under, lake, q);
                }
            }

            // Distancia en metros (chaflán 1 / 1.414, dos pasadas)
            const float Inf = 1e9f, S2 = 1.41421356f;
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                {
                    mask[k, i] = lake[k, i] ? 1f : 0f;
                    dist[k, i] = lake[k, i] ? 0f : Inf;
                }
            for (int k = 0; k < n; k++)
            {
                if (k > 0)
                    for (int i = 0; i < n; i++)
                    {
                        float m = dist[k - 1, i] + 1f;
                        if (i > 0) m = Mathf.Min(m, dist[k - 1, i - 1] + S2);
                        if (i < n - 1) m = Mathf.Min(m, dist[k - 1, i + 1] + S2);
                        dist[k, i] = Mathf.Min(dist[k, i], m);
                    }
                for (int i = 1; i < n; i++) dist[k, i] = Mathf.Min(dist[k, i], dist[k, i - 1] + 1f);
            }
            for (int k = n - 1; k >= 0; k--)
            {
                if (k < n - 1)
                    for (int i = 0; i < n; i++)
                    {
                        float m = dist[k + 1, i] + 1f;
                        if (i > 0) m = Mathf.Min(m, dist[k + 1, i - 1] + S2);
                        if (i < n - 1) m = Mathf.Min(m, dist[k + 1, i + 1] + S2);
                        dist[k, i] = Mathf.Min(dist[k, i], m);
                    }
                for (int i = n - 2; i >= 0; i--) dist[k, i] = Mathf.Min(dist[k, i], dist[k, i + 1] + 1f);
            }
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++) dist[k, i] *= cell;
        }

        static void TryAdd(int i, int k, bool[,] under, bool[,] lake, Queue<Vector2Int> q)
        {
            int n = under.GetLength(0);
            if (i < 0 || k < 0 || i >= n || k >= n || lake[k, i] || !under[k, i]) return;
            lake[k, i] = true;
            q.Enqueue(new Vector2Int(i, k));
        }

        public void Sample(float x, float z, out float lakeAmount, out float distance)
        {
            float fi = (x - x0) / cell - 0.5f, fk = (z - z0) / cell - 0.5f;
            if (fi < 0f || fk < 0f || fi > n - 1 || fk > n - 1)
            {
                lakeAmount = 0f;
                distance = 1e9f;
                return;
            }
            lakeAmount = Bilinear(mask, fi, fk);
            distance = Bilinear(dist, fi, fk);
        }
    }

#if UNITY_EDITOR
    // Pinta los TerrainData de verdad (se puede deshacer con Ctrl+Z) y los guarda
    [MenuItem("Herramientas/Terreno/Pintar terreno (permanente)")]
    static void BakeMenu()
    {
        TerrainStylizer s = FindFirstObjectByType<TerrainStylizer>();
        if (s == null)
        {
            EditorUtility.DisplayDialog("Pintar terreno", "Abre la escena del lago (SampleScene): no hay un TerrainStylizer en la escena.", "OK");
            return;
        }
        if (!s.LayersReady())
        {
            EditorUtility.DisplayDialog("Pintar terreno", "Faltan capas en el TerrainStylizer (deben ser 6).", "OK");
            return;
        }
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (Terrain t in terrains)
            if (t.terrainData != null) Undo.RegisterCompleteObjectUndo(t.terrainData, "Pintar terreno");
        s.PaintAll(terrains);
        foreach (Terrain t in terrains)
            if (t.terrainData != null) EditorUtility.SetDirty(t.terrainData);
        AssetDatabase.SaveAssets();
        Debug.Log("TerrainStylizer: " + terrains.Length + " terrenos pintados y guardados.");
    }
#endif
}
