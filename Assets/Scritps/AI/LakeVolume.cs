using UnityEngine;

// Describe el lago para las IA: superficie, fondo (terreno) y puntos válidos para nadar.
// Se basa en el trigger con tag "Water" y en raycasts al terreno, así que si cambias
// la forma del lago o el terreno, peces y pájaros se adaptan solos.
public class LakeVolume : MonoBehaviour
{
    public static LakeVolume Instance { get; private set; }

    [Tooltip("Trigger del agua. Si está vacío se busca el collider con tag Water más grande")]
    public Collider waterTrigger;
    [Tooltip("Mismo ajuste que 'Water Surface Offset' del jugador")]
    public float surfaceOffset = 0f;
    [Tooltip("Margen con los bordes del trigger")]
    public float edgeMargin = 4f;
    [Tooltip("Profundidad mínima de agua para que una zona cuente como lago")]
    public float minWaterDepth = 1.2f;

    public Bounds Bounds { get { return waterTrigger != null ? waterTrigger.bounds : new Bounds(transform.position, Vector3.one * 100f); } }
    public float SurfaceY { get { return Bounds.max.y + surfaceOffset; } }

    // Centro aproximado del agua (promedio de puntos válidos)
    public Vector3 Center { get; private set; }
    public float Radius { get; private set; }

    void Awake()
    {
        Instance = this;
        if (waterTrigger == null) waterTrigger = FindWaterTrigger();
    }

    void Start()
    {
        if (!computed) Recalculate();
    }

    bool computed;

    public static Collider FindWaterTrigger()
    {
        Collider best = null;
        float bestSize = 0f;
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Water"))
        {
            foreach (Collider c in go.GetComponents<Collider>())
            {
                if (!c.isTrigger) continue;
                float size = c.bounds.size.x * c.bounds.size.z;
                if (size > bestSize) { bestSize = size; best = c; }
            }
        }
        return best;
    }

    // Vuelve a calcular el centro del agua (llámalo si cambias el terreno en tiempo de ejecución)
    public void Recalculate()
    {
        computed = true;
        if (waterTrigger == null) waterTrigger = FindWaterTrigger();
        Bounds b = Bounds;
        Vector3 sum = Vector3.zero;
        int count = 0;
        // Muestreo en rejilla para encontrar dónde hay agua de verdad
        for (int ix = 0; ix < 12; ix++)
        {
            for (int iz = 0; iz < 12; iz++)
            {
                float x = Mathf.Lerp(b.min.x + edgeMargin, b.max.x - edgeMargin, (ix + 0.5f) / 12f);
                float z = Mathf.Lerp(b.min.z + edgeMargin, b.max.z - edgeMargin, (iz + 0.5f) / 12f);
                if (WaterDepthAt(x, z) >= minWaterDepth)
                {
                    sum += new Vector3(x, SurfaceY, z);
                    count++;
                }
            }
        }
        Center = count > 0 ? sum / count : new Vector3(b.center.x, SurfaceY, b.center.z);
        Radius = Mathf.Max(10f, Mathf.Min(b.extents.x, b.extents.z) - edgeMargin);
    }

    // Altura del fondo / terreno (ignora objetos con Rigidbody y triggers)
    public float GroundHeight(float x, float z)
    {
        Vector3 origin = new Vector3(x, SurfaceY + 60f, z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        if (float.IsNegativeInfinity(best)) best = Bounds.min.y;
        return Mathf.Max(best, Bounds.min.y);
    }

    public float WaterDepthAt(float x, float z)
    {
        Bounds b = Bounds;
        if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z) return 0f;
        return SurfaceY - GroundHeight(x, z);
    }

    public bool IsInsideXZ(Vector3 p, float margin)
    {
        Bounds b = Bounds;
        return p.x > b.min.x + margin && p.x < b.max.x - margin && p.z > b.min.z + margin && p.z < b.max.z - margin;
    }

    public bool IsInWater(Vector3 p)
    {
        return IsInsideXZ(p, 0f) && p.y < SurfaceY && p.y > Bounds.min.y;
    }

    // ¿Puede un pez estar en este punto? (dentro del agua, bajo la superficie y sobre el fondo)
    public bool IsSwimmable(Vector3 p, float clearance)
    {
        if (!IsInsideXZ(p, edgeMargin * 0.5f)) return false;
        if (p.y > SurfaceY - 0.3f) return false;
        return p.y > GroundHeight(p.x, p.z) + clearance;
    }

    // Busca un punto nadable cerca de 'near', a una profundidad entre minDepth y maxDepth.
    // Si el lago es poco profundo ahí, usa la mayor profundidad disponible.
    public bool TryGetSwimPoint(Vector3 near, float radius, float minDepth, float maxDepth, float clearance, out Vector3 point, int tries = 10)
    {
        float surface = SurfaceY;
        for (int i = 0; i < tries; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            float x = near.x + r.x;
            float z = near.z + r.y;
            if (!IsInsideXZ(new Vector3(x, 0f, z), edgeMargin)) continue;

            float ground = GroundHeight(x, z);
            float available = surface - ground - clearance;
            if (available < minWaterDepth * 0.6f) continue;

            float depth = Random.Range(minDepth, maxDepth);
            depth = Mathf.Min(depth, available);
            depth = Mathf.Max(depth, 0.4f);
            point = new Vector3(x, surface - depth, z);
            return true;
        }
        point = near;
        return false;
    }

    // Punto válido aleatorio en todo el lago
    public bool TryGetRandomSwimPoint(float minDepth, float maxDepth, float clearance, out Vector3 point)
    {
        if (TryGetSwimPoint(Center, Radius, minDepth, maxDepth, clearance, out point, 25)) return true;
        Bounds b = Bounds;
        return TryGetSwimPoint(new Vector3(b.center.x, SurfaceY, b.center.z), Mathf.Max(b.extents.x, b.extents.z), minDepth, maxDepth, clearance, out point, 40);
    }

    void OnDrawGizmosSelected()
    {
        if (waterTrigger == null) return;
        Bounds b = Bounds;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, SurfaceY, b.center.z), new Vector3(b.size.x, 0.05f, b.size.z));
        Gizmos.DrawWireSphere(Center, 1f);
    }
}
