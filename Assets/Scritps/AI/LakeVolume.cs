using UnityEngine;

// Describe el lago para las IA: superficie, fondo (terreno) y puntos válidos para nadar.
// Lee la forma real del agua desde LakeWater (que se adapta al hueco del terreno),
// así que si cambias el lago o el terreno, peces y pájaros se adaptan solos.
public class LakeVolume : MonoBehaviour
{
    public static LakeVolume Instance { get; private set; }

    [Tooltip("Agua del lago. Si está vacío se busca la primera LakeWater de la escena")]
    public LakeWater water;
    [Tooltip("Distancia mínima a la orilla para elegir puntos de nado")]
    public float edgeMargin = 4f;
    [Tooltip("Profundidad mínima de agua para que una zona cuente como lago")]
    public float minWaterDepth = 1.2f;

    public bool IsValid { get { return water != null && water.HasData; } }
    public Bounds Bounds { get { return IsValid ? water.WaterBounds : new Bounds(transform.position, Vector3.one * 100f); } }
    public float SurfaceY { get { return water != null ? water.SurfaceY : transform.position.y; } }

    // Centro del agua y radio aproximado (para patrullas y cardúmenes)
    public Vector3 Center { get; private set; }
    public float Radius { get; private set; }

    bool computed;

    void Awake()
    {
        Instance = this;
        if (water == null) water = FindFirstObjectByType<LakeWater>();
    }

    void Start()
    {
        if (!computed) Recalculate();
    }

    // Vuelve a leer el agua (llámalo si cambias el terreno o el nivel en tiempo de ejecución)
    public void Recalculate()
    {
        computed = true;
        if (water == null) water = FindFirstObjectByType<LakeWater>();
        if (water == null) return;
        water.EnsureBuilt();
        Center = water.Centroid;
        Radius = Mathf.Max(10f, Mathf.Sqrt(water.Area / Mathf.PI) * 0.85f);
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
        if (float.IsNegativeInfinity(best)) best = water != null ? water.GroundAt(x, z) : SurfaceY - 30f;
        return best;
    }

    public float WaterDepthAt(float x, float z)
    {
        if (!IsValid || !water.IsWaterXZ(x, z)) return 0f;
        return SurfaceY - GroundHeight(x, z);
    }

    // ¿El punto está sobre el agua, a al menos 'margin' metros de la orilla?
    public bool IsInsideXZ(Vector3 p, float margin)
    {
        if (!IsValid || !water.IsWaterXZ(p.x, p.z)) return false;
        if (margin <= 0.01f) return true;
        return water.IsWaterXZ(p.x + margin, p.z) && water.IsWaterXZ(p.x - margin, p.z)
            && water.IsWaterXZ(p.x, p.z + margin) && water.IsWaterXZ(p.x, p.z - margin);
    }

    public bool IsInWater(Vector3 p)
    {
        return IsInsideXZ(p, 0f) && p.y < SurfaceY && p.y > Bounds.min.y - 1f;
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
        return TryGetSwimPoint(new Vector3(b.center.x, SurfaceY, b.center.z), Mathf.Max(b.extents.x, b.extents.z), minDepth, maxDepth, clearance, out point, 60);
    }

    void OnDrawGizmosSelected()
    {
        if (!IsValid) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(Center, 1f);
        Gizmos.DrawWireSphere(new Vector3(Center.x, SurfaceY, Center.z), Radius);
    }
}
