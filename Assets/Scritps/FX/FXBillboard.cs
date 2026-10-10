using UnityEngine;

// Hace que un plano (quad) mire siempre a la cámara, como una partícula.
// 'wobble' lo estira y encoge un poco (burbujas, llamas).
public class FXBillboard : MonoBehaviour
{
    public float wobble = 0f;
    public float wobbleSpeed = 18f;

    Vector3 baseScale;
    float phase;

    void Awake()
    {
        baseScale = transform.localScale;
        phase = Random.Range(0f, 10f);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
        if (wobble > 0f)
        {
            float s = Mathf.Sin(Time.time * wobbleSpeed + phase) * wobble;
            transform.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z);
        }
    }

    // Plano de 1x1 compartido (para burbujas, llamas y destellos)
    static Mesh quad;
    public static Mesh QuadMesh
    {
        get
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "FX_Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }

    // Crea un plano que mira a la cámara, hijo de 'parent'
    public static FXBillboard Create(string name, Transform parent, Material material, float size, float wobbleAmount)
    {
        GameObject go = new GameObject(name);
        go.layer = parent != null ? parent.gameObject.layer : 0;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        FXBillboard b = go.AddComponent<FXBillboard>();
        b.wobble = wobbleAmount;
        return b;
    }

    // Por si se cambia la escala después de agregarlo
    public void SetBaseScale(Vector3 scale)
    {
        baseScale = scale;
        transform.localScale = scale;
    }
}
