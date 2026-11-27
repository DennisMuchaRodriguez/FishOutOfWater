using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ESTILOS (skins) del pez: cambian los colores de la textura del pez (cara y aletas) y de su traje.
// Se elige en el menú COOPERATIVO > ESTILOS (se guarda en PlayerPrefs) y se ve en todos los modos: en el pez del menú,
// en la sala del cooperativo y en el lago. En cooperativo viaja con tu nombre, así los demás te ven con tu estilo.
// Las texturas están en Assets/Resources/Skins/<Estilo>/ (Cuerpo.png, Armadura.png y la vista previa Vista.png); las
// arma Herramientas/Estilos/estilos.py (y las vistas, Herramientas/Logo/render_pez_3d.py estilos).
//
// Cómo se aplica: el modelo del pez viene de Maya con materiales "aiStandardSurface" (Arnold). Unity les pone su shader
// "Arnold Standard Surface", que guarda la textura en _BASE_COLOR_MAP y el color en _BASE_COLOR (no en _BaseMap /
// _BaseColor como el Lit de URP). Por eso aquí no se usan nombres fijos: se buscan en el shader del material las
// propiedades de textura y color base (sirve para Arnold, Lit, Simple Lit o un shader propio).
// Los materiales se reconocen por su textura ("..._Body_BaseColor" / "..._Armor_BaseColor") o por su nombre
// ("Body_Color" es el cuerpo del pez y "aiStandardSurface2" el traje). El vidrio de la escafandra no se toca.
// Cada modelo recibe sus propias copias de los materiales (el del jugador, la copia de un compañero y los trajes de la
// sala no se pisan entre sí) y el Original vuelve a una copia guardada del material tal como venía.
public static class SkinCatalog
{
    public class Skin
    {
        public string id;          // carpeta en Resources/Skins
        public string name;        // como sale en el menú
        public string description;
        public Color accent;       // color del marco de su tarjeta
    }

    public static readonly Skin[] All =
    {
        new Skin { id = "Original", name = "ORIGINAL", description = "Cara naranja, aletas azules y traje azul marino",
                   accent = new Color(1f, 0.55f, 0.2f, 1f) },
        new Skin { id = "NeonAbisal", name = "NEÓN ABISAL", description = "Cara magenta, aletas turquesa y traje morado oscuro",
                   accent = new Color(0.95f, 0.35f, 0.9f, 1f) },
        new Skin { id = "SolDorado", name = "SOL DORADO", description = "Cara violeta, aletas doradas y traje dorado",
                   accent = new Color(1f, 0.8f, 0.3f, 1f) },
    };

    const string Pref = "FOW_Estilo";

    public static int Count { get { return All.Length; } }
    public static int Clamp(int skin) { return Mathf.Clamp(skin, 0, All.Length - 1); }

    // El estilo elegido (de este jugador)
    public static int Selected
    {
        get { return Clamp(PlayerPrefs.GetInt(Pref, 0)); }
        set
        {
            PlayerPrefs.SetInt(Pref, Clamp(value));
            PlayerPrefs.Save();
        }
    }

    // Propiedades de "color base" de un shader: las texturas y los colores que lo tiñen
    class BaseProps
    {
        public int[] textures;
        public int[] colors;
    }

    static readonly int[] NoIds = new int[0];
    static readonly BaseProps NoProps = new BaseProps { textures = NoIds, colors = NoIds };

    static Texture2D[] bodies, armors;
    static Sprite[] previews;
    static readonly Dictionary<Shader, BaseProps> shaderProps = new Dictionary<Shader, BaseProps>();
    // Para cada material (el del modelo o una copia nuestra): una copia de cómo venía. El Original vuelve a ella
    static readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();
    // Copias que hicimos y el renderer al que pertenecen (un modelo nunca cambia las copias de otro)
    static readonly Dictionary<Material, Renderer> owners = new Dictionary<Material, Renderer>();
    static bool warned;

    static void Load()
    {
        if (bodies != null) return;
        bodies = new Texture2D[All.Length];
        armors = new Texture2D[All.Length];
        previews = new Sprite[All.Length];
        for (int i = 0; i < All.Length; i++)
        {
            string folder = "Skins/" + All[i].id + "/";
            if (i > 0)
            {
                bodies[i] = Resources.Load<Texture2D>(folder + "Cuerpo");
                armors[i] = Resources.Load<Texture2D>(folder + "Armadura");
                if (bodies[i] == null || armors[i] == null)
                    Debug.LogWarning("SkinCatalog: faltan texturas del estilo '" + All[i].id + "' en Resources/" + folder);
            }
            previews[i] = Resources.Load<Sprite>(folder + "Vista");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        bodies = null;
        armors = null;
        previews = null;
        shaderProps.Clear();
        originals.Clear();
        owners.Clear();
        warned = false;
    }

    public static Sprite Preview(int skin)
    {
        Load();
        return previews[Clamp(skin)];
    }

    // ---- Propiedades del shader ----

    static BaseProps PropsOf(Shader s)
    {
        if (s == null) return NoProps;
        BaseProps p;
        if (shaderProps.TryGetValue(s, out p)) return p;
        List<int> tex = new List<int>();
        List<int> col = new List<int>();
        int n = s.GetPropertyCount();
        for (int i = 0; i < n; i++)
        {
            // "_BASE_COLOR_MAP" → "BASECOLORMAP", "_BaseMap" → "BASEMAP"...
            string k = s.GetPropertyName(i).Replace("_", "").ToUpperInvariant();
            ShaderPropertyType type = s.GetPropertyType(i);
            if (type == ShaderPropertyType.Texture)
            {
                if (s.GetPropertyTextureDimension(i) != TextureDimension.Tex2D) continue;
                if (k == "BASEMAP" || k == "MAINTEX" || k == "BASECOLORMAP" || k == "BASECOLORTEXTURE" ||
                    k == "ALBEDOMAP" || k == "ALBEDO" || k == "DIFFUSEMAP" || k == "COLORMAP")
                    tex.Add(s.GetPropertyNameId(i));
            }
            else if (type == ShaderPropertyType.Color)
            {
                if (k == "BASECOLOR" || k == "COLOR" || k == "ALBEDOCOLOR" || k == "DIFFUSECOLOR")
                    col.Add(s.GetPropertyNameId(i));
            }
        }
        p = new BaseProps { textures = tex.ToArray(), colors = col.ToArray() };
        shaderProps[s] = p;
        return p;
    }

    // Los colores base del material (para teñirlo con un MaterialPropertyBlock)
    public static int[] BaseColorIds(Material m)
    {
        return m != null ? PropsOf(m.shader).colors : NoIds;
    }

    static Texture TexOf(Material m)
    {
        if (m == null) return null;
        foreach (int id in PropsOf(m.shader).textures)
        {
            Texture t = m.GetTexture(id);
            if (t != null) return t;
        }
        return null;
    }

    static string BaseName(Material m)
    {
        return m.name.Replace(" (Instance)", "").Replace(" (Estilo)", "").Trim();
    }

    static bool Has(string text, string part)
    {
        return text.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsGlass(string n)
    {
        return Has(n, "Glass") || Has(n, "Vidrio") || Has(n, "Eskafandra") || Has(n, "Escafandra");
    }

    // 1 = cuerpo del pez, 2 = traje, 0 = otra cosa (vidrio, luces...)
    public static int PartOf(Material m)
    {
        if (m == null) return 0;
        string n = BaseName(m);
        if (IsGlass(n)) return 0;
        Texture t = TexOf(m);
        string tn = t != null ? t.name : "";
        if (Has(tn, "Body_BaseColor") || tn == "Cuerpo" || Has(n, "Body")) return 1;
        if (Has(tn, "Armor_BaseColor") || tn == "Armadura" || Has(n, "Armor") || Has(n, "aiStandardSurface")) return 2;
        return 0;
    }

    // La copia del material tal como venía (se hace la primera vez que se ve un material sin tocar). Va por material y
    // no por nombre: el pez del lago (Fish_3DModel) y el de la sala (PESCAO) tienen materiales con el mismo nombre
    static Material OriginalOf(Material shared)
    {
        Material o;
        if (originals.TryGetValue(shared, out o) && o != null) return o;
        if (owners.ContainsKey(shared)) return null;   // una copia nuestra cuyo original ya no está
        o = new Material(shared) { name = BaseName(shared) + " (Original)" };
        originals[shared] = o;
        return o;
    }

    // Suelta las copias de modelos que ya no existen
    static void Prune()
    {
        if (owners.Count < 96) return;
        List<Material> dead = new List<Material>();
        foreach (KeyValuePair<Material, Renderer> kv in owners)
            if (kv.Key == null || kv.Value == null) dead.Add(kv.Key);
        foreach (Material m in dead)
        {
            owners.Remove(m);
            originals.Remove(m);   // la copia de cómo venía se queda: la usa el material del modelo
            if (m != null) Object.Destroy(m);
        }
    }

    // Pone el estilo 'skin' en un modelo del pez (el del jugador, la copia de un compañero, un traje de la sala o el pez
    // del menú). Con 0 (Original) vuelve a las texturas y colores de siempre. Se puede llamar otra vez con otro estilo.
    // Quita el tinte de color de casilla (MaterialPropertyBlock) que tuviera: si se quiere, se vuelve a poner después.
    public static void Apply(GameObject model, int skin)
    {
        if (model == null) return;
        Load();
        Prune();
        skin = Clamp(skin);
        int parts = 0, changed = 0;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material[] mats = r.sharedMaterials;
            bool swapped = false, any = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material shared = mats[i];
                int part = PartOf(shared);
                if (part == 0) continue;
                any = true;
                parts++;
                Material original = OriginalOf(shared);

                // Una copia propia de este renderer
                Material m = shared;
                Renderer owner;
                if (!owners.TryGetValue(m, out owner) || owner != r)
                {
                    m = new Material(shared) { name = BaseName(shared) + " (Estilo)" };
                    owners[m] = r;
                    if (original != null) originals[m] = original;
                    mats[i] = m;
                    swapped = true;
                }

                // Primero como venía, luego la textura del estilo
                if (original != null) m.CopyPropertiesFromMaterial(original);
                if (skin == 0) continue;
                Texture tex = part == 1 ? bodies[skin] : armors[skin];
                if (tex == null) continue;
                BaseProps p = PropsOf(m.shader);
                if (p.textures.Length == 0) continue;
                bool hadTexture = TexOf(m) != null;
                foreach (int id in p.textures) m.SetTexture(id, tex);
                // El color base se queda con su brillo pero sin tinte (la textura ya trae los colores); si no tenía
                // textura, en blanco para que se vea tal cual
                foreach (int id in p.colors)
                {
                    Color c = m.GetColor(id);
                    float g = hadTexture ? Mathf.Clamp(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), 0.6f, 1f) : 1f;
                    m.SetColor(id, new Color(g, g, g, c.a));
                }
                changed++;
            }
            if (swapped) r.sharedMaterials = mats;
            if (any) r.SetPropertyBlock(null);
        }

        if (skin > 0 && changed == 0 && !warned)
        {
            warned = true;
            Debug.LogWarning("SkinCatalog: no se pudo poner el estilo '" + All[skin].id + "' en '" + model.name + "' (" +
                             parts + " materiales del pez o del traje). Materiales: " + Describe(model));
        }
    }

    // Para el aviso: los materiales del modelo y sus shaders
    static string Describe(GameObject model)
    {
        HashSet<string> seen = new HashSet<string>();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m != null) seen.Add(BaseName(m) + " [" + (m.shader != null ? m.shader.name : "sin shader") + "]");
        return string.Join(", ", seen);
    }
}
