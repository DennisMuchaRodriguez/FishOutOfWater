using System.Collections.Generic;
using UnityEngine;

// Utilidades de la escena del Taller: instanciar modelos a un tamaño dado, figuras simples
// (cuando todavía no hay modelo), materiales creados por código y mallas generadas (cueva, suelo, anillos).
public static class WorkshopKit
{
    // ---------- Modelos ----------

    // Instancia el modelo (o null) bajo 'parent', sin colisionadores, con la base apoyada en el origen
    // y escalado para que su medida mayor (o su alto) sea 'size'. Devuelve null si no hay modelo.
    public static GameObject PlaceModel(GameObject model, Transform parent, float size, bool byHeight)
    {
        if (model == null) return null;
        GameObject inst = Object.Instantiate(model, parent, false);
        inst.transform.localPosition = Vector3.zero;
        StripColliders(inst);
        Bounds b;
        if (GetBounds(inst, out b))
        {
            float measure = byHeight ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (measure > 0.0001f)
            {
                float k = size / measure;
                inst.transform.localScale = inst.transform.localScale * k;
            }
            if (GetBounds(inst, out b))
            {
                // Centrado en X/Z y con la base en el suelo del padre
                Vector3 offset = parent.position - new Vector3(b.center.x, b.min.y, b.center.z);
                inst.transform.position += offset;
            }
        }
        return inst;
    }

    public static bool GetBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    public static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
    }

    // Figura simple (esfera, cubo, cilindro...) con material propio y sin colisionador
    public static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
                                   Material mat, Vector3 localEuler = default(Vector3))
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return go;
    }

    public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    // ---------- Materiales ----------

    static Shader litShader;

    // Material opaco con luz (URP Lit). tex puede ser null
    public static Material Lit(Color color, Texture2D tex = null, float smoothness = 0.2f, string name = "Taller_Lit")
    {
        if (litShader == null)
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
        }
        Material m = new Material(litShader);
        m.name = name;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    // Material aditivo (brillos, rayos de luz, hologramas) a partir de la plantilla de partículas
    public static Material Glow(Material template, Texture tex, Color tint)
    {
        return FXFactory.ParticleMaterial(template, tex != null ? tex : FXFactory.SoftDot, tint);
    }

    // Activa la emisión de un material (si el shader la tiene)
    public static void SetEmission(Material m, Color emission)
    {
        if (m == null || !m.HasProperty("_EmissionColor")) return;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", emission);
    }

    // Tiñe la emisión de todos los materiales del modelo (copias propias, no toca los archivos)
    public static void TintEmission(GameObject go, Color emission)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            Material[] mats = r.materials;
            foreach (Material m in mats) SetEmission(m, emission);
        }
    }

    // ---------- Texturas ----------

    // Ruido suave en escala de grises (para dar grano de arcilla a rocas y arena)
    public static Texture2D NoiseTexture(int size, float scale, float contrast, Color dark, Color light, string name, int seed)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Repeat;
        Color[] px = new Color[size * size];
        float ox = seed * 13.17f, oy = seed * 7.31f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                // Ruido que se repite sin costuras (mezcla de 4 muestras)
                float n = Tileable(u, v, scale, ox, oy) * 0.65f + Tileable(u, v, scale * 3.1f, oy, ox) * 0.35f;
                n = Mathf.Clamp01(0.5f + (n - 0.5f) * contrast);
                // Grano fino
                float grain = (Hash(x + seed * 31, y) - 0.5f) * 0.08f;
                px[y * size + x] = Color.Lerp(dark, light, Mathf.Clamp01(n + grain));
            }
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static float Tileable(float u, float v, float scale, float ox, float oy)
    {
        float a = Mathf.PerlinNoise(ox + u * scale, oy + v * scale);
        float b = Mathf.PerlinNoise(ox + (u - 1f) * scale, oy + v * scale);
        float c = Mathf.PerlinNoise(ox + u * scale, oy + (v - 1f) * scale);
        float d = Mathf.PerlinNoise(ox + (u - 1f) * scale, oy + (v - 1f) * scale);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    public static float Hash(int x, int y)
    {
        int h = x * 374761393 + y * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xffff) / 65535f;
    }

    // Degradado vertical (abajo transparente, arriba blanco): rayos de luz y columnas de holograma
    public static Texture2D BeamTexture()
    {
        int w = 32, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "Taller_Rayo";
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float v = (y + 0.5f) / h;
                float a = Mathf.Pow(1f - u, 1.6f) * Mathf.SmoothStep(0f, 1f, v) * Mathf.SmoothStep(1f, 0.85f, v);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    // ---------- Mallas ----------

    // Cueva: elipsoide visto desde dentro, con bultos de roca. El suelo plano lo pone otra malla.
    public static Mesh CaveMesh(Vector3 radii, int seg, int rings, float bumps, int seed)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float phi = Mathf.PI * r / rings;               // 0 = arriba, PI = abajo
            for (int s = 0; s <= seg; s++)
            {
                float th = 2f * Mathf.PI * s / seg;
                Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                // Bultos: ruido en 3 planos (sin costura en th porque usa la dirección, no el ángulo)
                float n = Mathf.PerlinNoise(seed + dir.x * 2.3f + 10f, dir.y * 2.3f + 10f) * 0.5f
                        + Mathf.PerlinNoise(dir.z * 2.9f + 30f, seed + dir.y * 2.9f + 30f) * 0.3f
                        + Mathf.PerlinNoise(dir.x * 6.1f + 50f, dir.z * 6.1f + seed + 50f) * 0.2f;
                float k = 1f - bumps * n;
                Vector3 p = Vector3.Scale(dir, radii) * k;
                if (p.y < -0.4f) p.y = -0.4f;                  // la parte de abajo queda bajo el suelo
                v.Add(p);
                uv.Add(new Vector2((float)s / seg * 6f, (float)r / rings * 3f));
            }
        }
        int row = seg + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                // Orden invertido: las caras miran hacia dentro
                tri.Add(a); tri.Add(c); tri.Add(b);
                tri.Add(b); tri.Add(c); tri.Add(d);
            }
        }
        Mesh m = new Mesh();
        m.name = "Taller_Cueva";
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Disco del suelo con ondulaciones suaves de arena
    public static Mesh FloorMesh(float radius, int rings, int seg, float waves, int seed)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();
        v.Add(Vector3.zero);
        uv.Add(new Vector2(0.5f, 0.5f));
        for (int r = 1; r <= rings; r++)
        {
            float rad = radius * r / rings;
            for (int s = 0; s < seg; s++)
            {
                float th = 2f * Mathf.PI * s / seg;
                float x = Mathf.Cos(th) * rad, z = Mathf.Sin(th) * rad;
                float y = (Mathf.PerlinNoise(seed + x * 0.35f, z * 0.35f) - 0.5f) * waves
                        + Mathf.Sin(x * 1.7f + z * 0.6f) * waves * 0.08f;
                // Hacia el borde el suelo sube un poco (se junta con la pared)
                y += Mathf.Pow((float)r / rings, 4f) * 0.6f;
                v.Add(new Vector3(x, y, z));
                uv.Add(new Vector2(x / 4f, z / 4f));
            }
        }
        for (int s = 0; s < seg; s++)
        {
            tri.Add(0); tri.Add(1 + (s + 1) % seg); tri.Add(1 + s);
        }
        for (int r = 1; r < rings; r++)
        {
            int inner = 1 + (r - 1) * seg, outer = 1 + r * seg;
            for (int s = 0; s < seg; s++)
            {
                int a = inner + s, b = inner + (s + 1) % seg, c = outer + s, d = outer + (s + 1) % seg;
                tri.Add(a); tri.Add(b); tri.Add(c);
                tri.Add(b); tri.Add(d); tri.Add(c);
            }
        }
        Mesh m = new Mesh();
        m.name = "Taller_Suelo";
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Anillo plano (horizontal) de radio interior/exterior dados, visible por las dos caras
    public static Mesh RingMesh(float inner, float outer, int seg)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();
        for (int s = 0; s <= seg; s++)
        {
            float th = 2f * Mathf.PI * s / seg;
            Vector3 d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
            v.Add(d * inner); uv.Add(new Vector2((float)s / seg, 0f));
            v.Add(d * outer); uv.Add(new Vector2((float)s / seg, 1f));
        }
        for (int s = 0; s < seg; s++)
        {
            int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
            tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
            tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
        }
        Mesh m = new Mesh();
        m.name = "Taller_Anillo";
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    // Plano vertical de 1 x 1 con la base en el origen (para rayos de luz); visible por las dos caras
    public static Mesh BeamMesh()
    {
        Mesh m = new Mesh();
        m.name = "Taller_Rayo";
        m.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) });
        m.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        m.SetTriangles(new List<int> { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 }, 0);
        m.RecalculateBounds();
        return m;
    }
}
