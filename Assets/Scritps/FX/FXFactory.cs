using UnityEngine;

// Utilidades para crear texturas, materiales y sistemas de partículas en tiempo de ejecución.
// Así los efectos nuevos (burbujas, estelas, visor) no dependen de assets extra.
public static class FXFactory
{
    static Texture2D softDot;
    static Texture2D bubble;
    static Texture2D streak;
    static Texture2D white;
    static Texture2D ring;

    // Anillo suave (ondas de espuma, retícula)
    public static Texture2D Ring
    {
        get
        {
            if (ring == null)
            {
                ring = MakeRadial(128, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f) * Mathf.Clamp01((1f - d) * 8f), "FX_Ring");
            }
            return ring;
        }
    }

    public static Texture2D White
    {
        get
        {
            if (white == null)
            {
                white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color[] px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                white.SetPixels(px);
                white.Apply();
                white.name = "FX_White";
            }
            return white;
        }
    }

    // Punto suave (para destellos, partículas de luz, polvo marino)
    public static Texture2D SoftDot
    {
        get
        {
            if (softDot == null)
            {
                softDot = MakeRadial(64, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f), "FX_SoftDot");
            }
            return softDot;
        }
    }

    // Burbuja: anillo con brillo en la esquina superior
    public static Texture2D Bubble
    {
        get
        {
            if (bubble == null)
            {
                int size = 64;
                bubble = new Texture2D(size, size, TextureFormat.RGBA32, false);
                bubble.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f;
                        float v = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(u * u + v * v);
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f);
                        float fill = d < 0.82f ? 0.12f : 0f;
                        float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.32f, 0.35f)) / 0.22f);
                        float a = Mathf.Clamp01(ring + fill + hl * 1.2f);
                        if (d > 1f) a = 0f;
                        bubble.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                bubble.Apply();
                bubble.name = "FX_Bubble";
            }
            return bubble;
        }
    }

    // Raya alargada y suave (estelas de velocidad)
    public static Texture2D Streak
    {
        get
        {
            if (streak == null)
            {
                int w = 16, h = 64;
                streak = new Texture2D(w, h, TextureFormat.RGBA32, false);
                streak.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                        float a = Mathf.Pow(1f - u, 2f) * Mathf.Pow(1f - v, 1.5f);
                        streak.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                streak.Apply();
                streak.name = "FX_Streak";
            }
            return streak;
        }
    }

    public static Texture2D MakeRadial(int size, System.Func<float, float> alphaFromDist, string name)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaFromDist(d)));
            }
        }
        tex.Apply();
        tex.name = name;
        return tex;
    }

    // Crea un material de partículas a partir de una plantilla (por ejemplo WaterJek.mat).
    // Si no hay plantilla, usa el shader de partículas de URP.
    public static Material ParticleMaterial(Material template, Texture texture, Color tint)
    {
        Material mat;
        if (template != null)
        {
            mat = new Material(template);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        mat.name = "FX_" + texture.name;
        return mat;
    }

    // Igual que ParticleMaterial pero con transparencia normal (no aditiva):
    // se ve bien sobre fondos claros (cielo, espuma)
    public static Material ParticleMaterialAlpha(Material template, Texture texture, Color tint)
    {
        Material mat = ParticleMaterial(template, texture, tint);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.name = "FX_Alpha_" + texture.name;
        return mat;
    }

    // Sistema de partículas vacío y listo para configurar
    public static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // Instancia un efecto (prefab) y lo destruye solo
    public static GameObject SpawnOneShot(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, float life = 3f)
    {
        if (prefab == null) return null;
        GameObject fx = Object.Instantiate(prefab, position, rotation);
        if (!Mathf.Approximately(scale, 1f)) fx.transform.localScale *= scale;
        Object.Destroy(fx, life);
        return fx;
    }

    public static Sprite SpriteFrom(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
