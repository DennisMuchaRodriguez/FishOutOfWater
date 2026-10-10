using UnityEngine;
using System.Collections.Generic;

// Piezas que se ven en el traje según las mejoras instaladas (GDD: "cada mejora cambia el aspecto del traje"):
//  - Misiles teledirigidos: dos lanzadores en los hombros (con 3 o 4 tubos).
//  - Tanque extra: dos tanques en la espalda (más grandes en el nivel II).
//  - Placas reforzadas: placas a los costados.
//  - Escudo de burbuja: un pequeño emisor cian sobre la cabeza.
// Se colocan según el tamaño del modelo del pez (modeloPez) y lo siguen al moverse.
// Solo se ven en tercera persona (en primera persona tapaban la vista).
public class SuitUpgradeVisuals : MonoBehaviour
{
    [Tooltip("Tamaño general de las piezas (1 = normal)")]
    public float partScale = 1f;
    public Color metal = new Color(0.22f, 0.26f, 0.3f, 1f);
    public Color trim = new Color(1f, 0.48f, 0.18f, 1f);
    public Color glow = new Color(0.41f, 0.87f, 0.9f, 1f);

    PlayerController_Base player;
    Transform root;
    readonly List<Renderer> renderers = new List<Renderer>();
    Material metalMat, trimMat, glowMat;
    bool visible = true;
    bool pending;

    void Awake()
    {
        player = GetComponent<PlayerController_Base>();
    }

    void Start()
    {
        if (pending) Refresh();
    }

    // Rehace las piezas según los niveles actuales
    public void Refresh()
    {
        Transform model = player != null ? player.modeloPez : null;
        if (model == null)
        {
            pending = true;   // el jugador busca su modelo en Start
            return;
        }
        pending = false;

        if (root != null)
        {
            root.SetParent(null, false);   // fuera del modelo antes de medirlo (Destroy espera al final del cuadro)
            Destroy(root.gameObject);
        }
        renderers.Clear();
        root = new GameObject("PiezasMejoras").transform;
        root.SetParent(model, false);

        // Medidas del modelo (en el espacio del modelo)
        Bounds b;
        if (!LocalBounds(model, out b)) b = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1.4f));
        Vector3 c = b.center, e = b.extents;
        float k = Mathf.Min(e.x, Mathf.Min(e.y, e.z)) * partScale;

        int missiles = Upgrades.Level(UpgradeId.HomingMissiles);
        if (missiles > 0)
        {
            int tubes = missiles >= 2 ? 4 : 3;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = c + new Vector3(side * e.x * 0.78f, e.y * 0.62f, e.z * 0.1f);
                Part(PrimitiveType.Cube, "Lanzador", p, new Vector3(0.42f, 0.32f, 0.75f) * k, metalMat = Mat(metalMat, metal, 0.5f));
                for (int t = 0; t < tubes; t++)
                {
                    float off = (t - (tubes - 1) * 0.5f) * 0.11f * k;
                    Part(PrimitiveType.Cylinder, "Tubo", p + new Vector3(off, 0.04f * k, 0.38f * k), new Vector3(0.09f, 0.04f, 0.09f) * k,
                         trimMat = Mat(trimMat, trim, 0.4f), new Vector3(90f, 0f, 0f));
                }
            }
        }

        int tank = Upgrades.Level(UpgradeId.ExtraTank);
        if (tank > 0)
        {
            float s = tank >= 2 ? 1.25f : 1f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = c + new Vector3(side * e.x * 0.3f, e.y * 0.35f, -e.z * 0.72f);
                Part(PrimitiveType.Capsule, "Tanque", p, new Vector3(0.3f, 0.36f, 0.3f) * k * s, metalMat = Mat(metalMat, metal, 0.5f), new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, "Franja", p, new Vector3(0.32f, 0.03f, 0.32f) * k * s, trimMat = Mat(trimMat, trim, 0.4f), new Vector3(90f, 0f, 0f));
            }
        }

        int plates = Upgrades.Level(UpgradeId.ReinforcedPlates);
        if (plates > 0)
        {
            int count = plates >= 2 ? 2 : 1;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = c + new Vector3(side * e.x * 0.98f, -e.y * 0.05f - i * e.y * 0.3f, e.z * (0.15f - i * 0.35f));
                    Part(PrimitiveType.Cube, "Placa", p, new Vector3(0.06f, 0.42f, 0.62f) * k, metalMat = Mat(metalMat, metal, 0.6f), new Vector3(0f, 0f, side * 8f));
                }
        }

        if (Upgrades.Has(UpgradeId.BubbleShield))
        {
            Vector3 p = c + new Vector3(0f, e.y * 1.02f, e.z * 0.05f);
            Part(PrimitiveType.Cylinder, "Emisor", p, new Vector3(0.18f, 0.04f, 0.18f) * k, metalMat = Mat(metalMat, metal, 0.5f));
            Part(PrimitiveType.Sphere, "Luz", p + Vector3.up * 0.05f * k, Vector3.one * 0.12f * k, glowMat = Mat(glowMat, glow, 0.9f, true));
        }

        visible = true;
        SetVisible(player == null || player.isThirdPerson);
    }

    void LateUpdate()
    {
        if (pending && player != null && player.modeloPez != null) Refresh();
        if (player != null) SetVisible(player.isThirdPerson && !player.isDead);
    }

    void SetVisible(bool show)
    {
        if (show == visible) return;
        visible = show;
        foreach (Renderer r in renderers) if (r != null) r.enabled = show;
    }

    void Part(PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default(Vector3))
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = localScale;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderers.Add(r);
    }

    static Material Mat(Material existing, Color color, float smoothness, bool emissive = false)
    {
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        m.name = "PiezaMejora";
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (emissive && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
        }
        return m;
    }

    // Límites de los renderers del modelo en el espacio local del modelo
    static bool LocalBounds(Transform model, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            Bounds wb = r.bounds;
            Vector3 min = wb.min, max = wb.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 local = model.InverseTransformPoint(corner);
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                else bounds.Encapsulate(local);
            }
        }
        return any;
    }
}
