using UnityEngine;

// ESTILOS (skins) del pez: cambian los colores de la textura del pez (cara y aletas) y de su traje.
// Se elige en el menú COOPERATIVO > ESTILOS (se guarda en PlayerPrefs) y se ve en todos los modos; en cooperativo
// viaja con tu nombre, así los demás te ven con tu estilo (en la sala y en el lago).
// Las texturas están en Assets/Resources/Skins/<Estilo>/ (Cuerpo.png, Armadura.png y la vista previa Vista.png); las
// arma Herramientas/Estilos/estilos.py (y las vistas, Herramientas/Logo/render_pez_3d.py estilos).
// Se aplica cambiando la textura de los materiales del modelo que usan las texturas del pez ("..._Body_BaseColor") o
// del traje ("..._Armor_BaseColor"); el resto del modelo (vidrio, luces) no se toca.
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
    static Texture origBody, origArmor;    // las texturas originales (se anotan la primera vez que se ven)

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
        origBody = null;
        origArmor = null;
    }

    public static Sprite Preview(int skin)
    {
        Load();
        return previews[Clamp(skin)];
    }

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    static Texture TexOf(Material m)
    {
        if (m == null) return null;
        if (m.HasProperty(BaseMapId)) return m.GetTexture(BaseMapId);
        if (m.HasProperty(MainTexId)) return m.GetTexture(MainTexId);
        return null;
    }

    // 1 = cuerpo del pez, 2 = traje, 0 = otra cosa
    static int RoleOf(Texture t)
    {
        if (t == null) return 0;
        string n = t.name;
        if (n.Contains("Body_BaseColor") || n == "Cuerpo") return 1;
        if (n.Contains("Armor_BaseColor") || n == "Armadura") return 2;
        return 0;
    }

    // Pone el estilo 'skin' en un modelo del pez (el del jugador, la copia de un compañero o un traje de la sala).
    // Con 0 (Original) vuelve a las texturas de siempre.
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
            for (int i = 0; i < shared.Length && !any; i++) any = RoleOf(TexOf(shared[i])) != 0;
            if (!any) continue;

            Material[] mats = r.materials;   // copias propias de este modelo (no cambia el material del proyecto)
            for (int i = 0; i < mats.Length; i++)
            {
                Texture t = TexOf(mats[i]);
                int role = RoleOf(t);
                if (role == 0) continue;
                if (t.name.Contains("_BaseColor"))
                {
                    if (role == 1 && origBody == null) origBody = t;
                    if (role == 2 && origArmor == null) origArmor = t;
                }
                Texture target = skin == 0 ? (role == 1 ? origBody : origArmor) : (role == 1 ? bodies[skin] : armors[skin]);
                if (target == null || target == t) continue;
                if (mats[i].HasProperty(BaseMapId)) mats[i].SetTexture(BaseMapId, target);
                if (mats[i].HasProperty(MainTexId)) mats[i].SetTexture(MainTexId, target);
            }
            r.materials = mats;
        }
    }
}
