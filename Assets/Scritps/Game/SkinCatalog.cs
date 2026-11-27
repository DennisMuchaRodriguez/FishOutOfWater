using System.Collections.Generic;
using UnityEngine;

// ESTILOS (skins) del pez: cambian los colores de la textura del pez (cara y aletas) y de su traje.
// Se elige en el menú COOPERATIVO > ESTILOS (se guarda en PlayerPrefs) y se ve en todos los modos; en cooperativo
// viaja con tu nombre, así los demás te ven con tu estilo (en la sala y en el lago).
// Las texturas están en Assets/Resources/Skins/<Estilo>/ (Cuerpo.png, Armadura.png y la vista previa Vista.png); las
// arma Herramientas/Estilos/estilos.py (y las vistas, Herramientas/Logo/render_pez_3d.py estilos).
// Se aplica cambiando la textura de los materiales del pez y del traje. Se reconocen por su textura
// ("..._Body_BaseColor" / "..._Armor_BaseColor") o por el nombre del material que trae el modelo ("Body_Color" para el
// cuerpo del pez, "aiStandardSurface2" para el traje): en la escena el cuerpo usa el material de dentro del FBX. Con un
// estilo, la textura nueva va con el color base en blanco (se ve tal cual); con el Original vuelve lo de antes. El vidrio
// de la escafandra no se toca.
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

    static Texture2D[] bodies, armors;
    static Sprite[] previews;
    // Lo original de cada material (textura y color), por nombre: se anota la primera vez que se ve sin tocar
    static readonly Dictionary<string, Texture> origTex = new Dictionary<string, Texture>();
    static readonly Dictionary<string, Color> origColor = new Dictionary<string, Color>();
    static readonly HashSet<Material> touched = new HashSet<Material>();

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
        origTex.Clear();
        origColor.Clear();
        touched.Clear();
    }

    public static Sprite Preview(int skin)
    {
        Load();
        return previews[Clamp(skin)];
    }

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    static Texture TexOf(Material m)
    {
        if (m == null) return null;
        if (m.HasProperty(BaseMapId)) return m.GetTexture(BaseMapId);
        if (m.HasProperty(MainTexId)) return m.GetTexture(MainTexId);
        return null;
    }

    static string BaseName(Material m)
    {
        return m.name.Replace(" (Instance)", "").Trim();
    }

    static bool Has(string text, string part)
    {
        return text.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // 1 = cuerpo del pez, 2 = traje, 0 = otra cosa (vidrio, luces...)
    static int RoleOf(Material m)
    {
        if (m == null) return 0;
        string n = BaseName(m);
        if (Has(n, "Glass") || Has(n, "Vidrio") || Has(n, "Eskafandra") || Has(n, "Escafandra")) return 0;
        Texture t = TexOf(m);
        string tn = t != null ? t.name : "";
        if (Has(tn, "Body_BaseColor") || tn == "Cuerpo" || Has(n, "Body")) return 1;
        if (Has(tn, "Armor_BaseColor") || tn == "Armadura" || Has(n, "Armor") || Has(n, "aiStandardSurface")) return 2;
        return 0;
    }

    static Color ColorOf(Material m)
    {
        if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
        if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
        return Color.white;
    }

    static void Set(Material m, Texture tex, Color color)
    {
        if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, tex);
        if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, tex);
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
    }

    // Pone el estilo 'skin' en un modelo del pez (el del jugador, la copia de un compañero o un traje de la sala).
    // Con 0 (Original) vuelve a las texturas y colores de siempre.
    public static void Apply(GameObject model, int skin)
    {
        if (model == null) return;
        Load();
        skin = Clamp(skin);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material[] shared = r.sharedMaterials;
            bool any = false;
            for (int i = 0; i < shared.Length && !any; i++) any = RoleOf(shared[i]) != 0;
            if (!any) continue;

            // Lo original se anota antes de tocar nada (si el material compartido no es uno que ya cambiamos)
            for (int i = 0; i < shared.Length; i++)
            {
                Material sm = shared[i];
                int role = RoleOf(sm);
                if (role == 0 || touched.Contains(sm)) continue;
                string key = BaseName(sm) + "#" + role;
                if (origTex.ContainsKey(key)) continue;
                origTex[key] = TexOf(sm);
                origColor[key] = ColorOf(sm);
            }

            Material[] mats = r.materials;   // copias propias de este modelo (no cambia el material del proyecto)
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                int role = RoleOf(m);
                if (role == 0) continue;
                if (skin == 0)
                {
                    string key = BaseName(m) + "#" + role;
                    Texture t;
                    Color c;
                    if (origTex.TryGetValue(key, out t) && origColor.TryGetValue(key, out c)) Set(m, t, c);
                }
                else
                {
                    Texture tex = role == 1 ? bodies[skin] : armors[skin];
                    if (tex != null) Set(m, tex, Color.white);
                }
                touched.Add(m);
            }
            r.materials = mats;
        }
    }
}
