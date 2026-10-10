using System.Collections.Generic;
using UnityEngine;

// Los planos que ofrece el mecánico en cada visita al Taller del modo Historia (elegidos a mano).
// Vive en Assets/Resources/TallerPlan.asset.
// Reglas al armar la visita (ResolveOffers):
//  - Nunca se ofrece algo que ya está al máximo.
//  - El nivel II solo aparece si ya tienes el nivel I. Si el plan pide nivel I y ya lo tienes, se ofrece el II.
//  - Si un plano no sirve se reemplaza: primero por otra opción de la misma categoría, después por cualquiera.
[CreateAssetMenu(menuName = "Fish Out Of Water/Plan del Taller", fileName = "TallerPlan", order = 22)]
public class WorkshopPlan : ScriptableObject
{
    public const string ResourcePath = "TallerPlan";
    public const int OffersPerVisit = 3;

    [Tooltip("Cómic de la primera visita (se ve una sola vez, antes de elegir)")]
    public ComicDefinition firstVisitComic;
    [Tooltip("Una entrada por visita, en orden. Se busca por el índice del nivel; si no está, por el número de visita")]
    public List<WorkshopVisit> visits = new List<WorkshopVisit>();

    static WorkshopPlan cached;

    public static WorkshopPlan Load()
    {
        if (cached == null) cached = Resources.Load<WorkshopPlan>(ResourcePath);
        return cached;
    }

    // visitNumber empieza en 1 (ver WorkshopFlow.VisitNumber)
    public WorkshopVisit FindVisit(int levelIndex, int visitNumber)
    {
        foreach (WorkshopVisit v in visits)
            if (v != null && v.levelIndex == levelIndex) return v;
        int i = visitNumber - 1;
        return i >= 0 && i < visits.Count ? visits[i] : null;
    }

    // Los planos que se muestran de verdad, según lo que el jugador ya tiene instalado.
    // level de cada resultado = el nivel que se instalaría (1 o 2).
    public static List<WorkshopOffer> ResolveOffers(WorkshopVisit visit, int visitNumber)
    {
        List<UpgradeId> order = UpgradeCatalog.OrderedIds();
        List<WorkshopOffer> result = new List<WorkshopOffer>();
        List<WorkshopOffer> wanted = new List<WorkshopOffer>();
        if (visit != null && visit.offers != null)
            foreach (WorkshopOffer o in visit.offers)
                if (o != null) wanted.Add(o);

        // Sin plan para esta visita: opciones repartidas por el catálogo (siempre las mismas para esa visita)
        if (wanted.Count == 0)
        {
            int start = Mathf.Abs(visitNumber * 5) % Mathf.Max(1, order.Count);
            for (int k = 0; k < order.Count && wanted.Count < OffersPerVisit; k++)
            {
                UpgradeId id = order[(start + k * 4) % order.Count];
                bool repeated = false;
                foreach (WorkshopOffer w in wanted) if (w.upgrade == id) repeated = true;
                if (!repeated) wanted.Add(new WorkshopOffer { upgrade = id, level = 1 });
            }
        }

        foreach (WorkshopOffer o in wanted)
        {
            if (result.Count >= OffersPerVisit) break;
            WorkshopOffer pick = IsValid(o.upgrade, o.level, result) ? Next(o.upgrade) : Replacement(o.upgrade, result, order);
            if (pick != null) result.Add(pick);
        }
        return result;
    }

    // ¿Se puede ofrecer esta mejora (pedida en ese nivel) sin repetir otra de la misma visita?
    static bool IsValid(UpgradeId id, int plannedLevel, List<WorkshopOffer> taken)
    {
        int current = Upgrades.Level(id);
        if (current >= UpgradeCatalog.MaxLevelOf(id)) return false;   // ya al máximo
        if (plannedLevel >= 2 && current < 1) return false;           // nivel II sin el I
        foreach (WorkshopOffer t in taken) if (t.upgrade == id) return false;
        return true;
    }

    static WorkshopOffer Next(UpgradeId id)
    {
        return new WorkshopOffer { upgrade = id, level = Upgrades.Level(id) + 1 };
    }

    // Reemplazo: misma categoría primero (empezando por la misma mejora), después cualquiera
    static WorkshopOffer Replacement(UpgradeId original, List<WorkshopOffer> taken, List<UpgradeId> order)
    {
        UpgradeCategory category = UpgradeCatalog.CategoryOf(original);
        int start = Mathf.Max(0, order.IndexOf(original));
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < order.Count; k++)
            {
                UpgradeId id = order[(start + k) % order.Count];
                if (pass == 0 && UpgradeCatalog.CategoryOf(id) != category) continue;
                if (IsValid(id, 1, taken)) return Next(id);
            }
        }
        return null;
    }
}

[System.Serializable]
public class WorkshopVisit
{
    [Tooltip("Índice del nivel en el LevelCatalog (nivel 3 = índice 2). Debe tener 'Workshop After'")]
    public int levelIndex;
    [Tooltip("Lo que dice el mecánico al llegar")]
    [TextArea(2, 3)] public string mechanicLine;
    [Tooltip("Los 3 planos de esta visita")]
    public List<WorkshopOffer> offers = new List<WorkshopOffer>();
}

[System.Serializable]
public class WorkshopOffer
{
    public UpgradeId upgrade;
    [Tooltip("Nivel que se ofrece (el II solo aparece si ya tienes el I)")]
    [Range(1, 2)] public int level = 1;
}
