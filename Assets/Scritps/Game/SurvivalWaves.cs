using System.Collections.Generic;
using UnityEngine;

// Generador de las oleadas del modo SUPERVIVENCIA (sin fin). Usa las mismas aves y jefes del modo Historia (las saca
// del LevelCatalog): cada especie llega en el mismo orden que en la historia, pero mucho antes.
// Reglas (los números están en las constantes de abajo, para ajustarlos fácil):
//  - Pensado en tramos de 5 oleadas: el traje solo se repara (y se mejora) en el Taller, que llega después de cada
//    jefe; dentro de un tramo la armadura que pierdes no vuelve.
//  - Cantidad: 4 o 5 aves en la oleada 1 (no empieza suave), unas 0,6 más por oleada, hasta 18 a la vez (oleada 24).
//    Desde ahí sigue subiendo la vida y cada vez hay más aves fuertes en la mezcla.
//  - Vida: x1,05 en la oleada 1 y +4 % por oleada (+7 % por oleada desde la 21).
//  - Especies: oleada 1 gaviotas y martines pescadores; 2 garzas; 3 charranes; 4 pelícanos; 6 cormoranes;
//    8 serretas; 11 águilas pescadoras. Una especie recién llegada sale más seguido; las gaviotas, cada vez menos;
//    las pesadas (garza, pelícano, cormorán, águila) un poco menos. Cada especie tiene un tope por oleada (lo que
//    llega junto en la historia) que sube cada 8 oleadas.
//  - Jefes: cada 5 oleadas. 5 Garza Gris, 10 Pelícano Saco, 15 Cormorán Rey, 20 Reina Águila. Desde la 25 llegan DOS
//    jefes distintos (cada uno por su lado), desde la 45 TRES; y en cada vuelta tienen más vida (+15 %).
//  - Grupos: desde la oleada 2, a veces no llegan todos juntos (cada vez más seguido, hasta 3 de cada 4 oleadas):
//      TENAZA: dos grupos a la vez por lados opuestos.
//      EXPLORADORES: un par primero; después un grupo por otro lado y otro grupo por otro lado.
//      ESCALONADA: tres grupos por tres lados, uno detrás de otro.
//  - Después de un jefe, la oleada siguiente es un poco más corta (un respiro).
public static class SurvivalWaves
{
    public enum Pattern { Together, Pincer, Scouts, Staggered, Boss }

    // Una bandada: llega en V desde un lado del lago
    public class Group
    {
        public float yawOffset;   // grados, respecto a la dirección de la oleada
        public float delay;       // segundos desde que empieza la oleada
        public readonly List<BirdType> birds = new List<BirdType>();
        public bool HasBoss
        {
            get { foreach (BirdType b in birds) if (b != null && b.boss != BossKind.None) return true; return false; }
        }
    }

    public class Wave
    {
        public int number;
        public Pattern pattern;
        public float healthMultiplier = 1f;
        public float bossHealthMultiplier = 1f;
        public readonly List<Group> groups = new List<Group>();
        public readonly List<BirdType> bosses = new List<BirdType>();
        public bool IsBossWave { get { return bosses.Count > 0; } }
        public int BirdCount
        {
            get { int n = 0; foreach (Group g in groups) n += g.birds.Count; return n; }
        }
    }

    // ---- Ajustes ----
    public const int BossEvery = 5;
    const float BaseBirds = 3.8f;          // aves = BaseBirds + BirdsPerWave · oleada (± 1)
    const float BirdsPerWave = 0.6f;
    const int MinBirds = 4;
    const int MaxBirds = 18;               // tope de aves a la vez (por rendimiento y para que se pueda jugar)
    const float BaseHealth = 1.05f;        // vida de la oleada 1
    const float HealthPerWave = 0.04f;
    const int LateWave = 20;               // desde aquí la vida sube más rápido (ya no suben las aves)
    const float LateHealthPerWave = 0.03f;
    const float UnlockScale = 0.85f;       // oleada en la que llega una especie = (nivel de la historia - 4) · esto
    const float BossRepeatHealth = 0.15f;  // vida extra de los jefes en cada vuelta
    const float MaxSplitChance = 0.75f;

    class Species
    {
        public BirdType type;
        public int firstLevel;     // nivel de la historia donde aparece por primera vez (1, 2...)
        public int maxPerWave;     // la mayor cantidad que llega junta en una oleada de la historia
        public int unlockWave;     // oleada de supervivencia desde la que aparece
        public bool boss;
    }

    static List<Species> species;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { species = null; }

    // Peces defensores en el lago: 2 desde la oleada 3 y 2 más cada 4 oleadas (4 en la 7, 6 en la 11, 8 desde la 15)
    public static int DefendersFor(int wave)
    {
        if (wave < 3) return 0;
        return Mathf.Min(8, 2 * (1 + (wave - 3) / 4));
    }

    public static float HealthFor(int wave)
    {
        float h = BaseHealth + HealthPerWave * (wave - 1);
        if (wave > LateWave) h += LateHealthPerWave * (wave - LateWave);
        return h;
    }

    public static bool IsBossWave(int wave) { return wave > 0 && wave % BossEvery == 0; }

    // Oleadas que faltan para el próximo jefe (0 = esta oleada tiene jefe)
    public static int WavesToBoss(int wave)
    {
        if (wave <= 0) return BossEvery;
        int r = wave % BossEvery;
        return r == 0 ? 0 : BossEvery - r;
    }

    // ======================= ARMADO =======================

    public static Wave Build(int number, int seed)
    {
        if (species == null) Gather();
        int n = Mathf.Max(1, number);
        System.Random rng = new System.Random(seed * 31 + n * 7919);
        Wave wave = new Wave { number = n, healthMultiplier = HealthFor(n) };

        List<Species> normal = new List<Species>();
        List<Species> bosses = new List<Species>();
        foreach (Species s in species)
        {
            if (s.boss) bosses.Add(s);
            else normal.Add(s);
        }
        if (normal.Count == 0 && bosses.Count == 0) return wave;

        int count = Mathf.RoundToInt(BaseBirds + BirdsPerWave * n + (float)(rng.NextDouble() * 2.0 - 1.0));
        count = Mathf.Clamp(count, MinBirds, MaxBirds);
        // Respiro después de un jefe
        if (n > BossEvery && n % BossEvery == 1) count = Mathf.Max(MinBirds, Mathf.RoundToInt(count * 0.85f));

        // Jefes: uno en las primeras vueltas (en el orden de la historia); después dos o tres distintos
        if (IsBossWave(n) && bosses.Count > 0)
        {
            int cycle = n / BossEvery;
            if (cycle <= bosses.Count) wave.bosses.Add(bosses[cycle - 1].type);
            else
            {
                int howMany = Mathf.Min(cycle <= bosses.Count * 2 ? 2 : 3, bosses.Count);
                List<Species> pool = new List<Species>(bosses);
                for (int k = 0; k < howMany; k++)
                {
                    int i = rng.Next(pool.Count);
                    wave.bosses.Add(pool[i].type);
                    pool.RemoveAt(i);
                }
                wave.bossHealthMultiplier = 1f + BossRepeatHealth * (cycle - bosses.Count);
            }
            // La escolta: la mitad de una oleada normal (menos si hay varios jefes)
            count = Mathf.Max(3, Mathf.RoundToInt(count * (wave.bosses.Count > 1 ? 0.4f : 0.5f)));
        }

        List<BirdType> flock = PickFlock(normal, n, count, rng);
        Arrange(wave, flock, rng);
        return wave;
    }

    // Lee las especies del catálogo de niveles (en orden de aparición)
    static void Gather()
    {
        species = new List<Species>();
        LevelCatalog catalog = GameSession.Catalog;
        if (catalog == null) return;
        for (int li = 0; li < catalog.Count; li++)
        {
            LevelDefinition level = catalog.Get(li);
            if (level == null || level.waves == null) continue;
            foreach (WaveDefinition w in level.waves)
            {
                if (w == null || w.birds == null) continue;
                foreach (BirdSpawn bs in w.birds)
                {
                    if (bs == null || bs.type == null || bs.type.bird == null || bs.count <= 0) continue;
                    BirdType t = bs.type.bird;
                    Species s = null;
                    foreach (Species e in species) if (e.type == t) { s = e; break; }
                    if (s == null)
                    {
                        s = new Species { type = t, firstLevel = li + 1, boss = t.boss != BossKind.None };
                        species.Add(s);
                    }
                    s.maxPerWave = Mathf.Max(s.maxPerWave, bs.count);
                }
            }
        }
        foreach (Species s in species)
            s.unlockWave = Mathf.Max(1, Mathf.CeilToInt((s.firstLevel - 4) * UnlockScale));
    }

    // Elige las aves (sin jefes) por paquetes: los charranes y las serretas de a tres, las pesadas de a una
    static List<BirdType> PickFlock(List<Species> pool, int n, int count, System.Random rng)
    {
        List<BirdType> result = new List<BirdType>();
        List<Species> open = new List<Species>();
        foreach (Species s in pool) if (s.unlockWave <= n) open.Add(s);
        if (open.Count == 0 && pool.Count > 0) open.Add(pool[0]);
        if (open.Count == 0) return result;

        int[] taken = new int[open.Count];
        float[] weights = new float[open.Count];
        int guard = 0;
        while (result.Count < count && guard++ < 200)
        {
            float total = 0f;
            for (int i = 0; i < open.Count; i++)
            {
                weights[i] = taken[i] >= Cap(open[i], n) ? 0f : Weight(open[i], n, i == 0);
                total += weights[i];
            }
            if (total <= 0f) break;

            double r = rng.NextDouble() * total;
            int pick = 0;
            for (int i = 0; i < open.Count; i++)
            {
                r -= weights[i];
                if (r <= 0.0) { pick = i; break; }
                pick = i;
            }
            Species s = open[pick];
            int pack = rng.Next(1, Pack(s) + 1);
            pack = Mathf.Min(pack, count - result.Count, Cap(s, n) - taken[pick]);
            for (int k = 0; k < pack; k++) result.Add(s.type);
            taken[pick] += Mathf.Max(1, pack);
        }
        // Si todas llegaron a su tope, se completa con las más comunes (las primeras de la lista)
        for (int i = 0; result.Count < count; i++) result.Add(open[i % Mathf.Min(open.Count, 3)].type);
        return result;
    }

    // Tope por oleada: lo que llega junto en la historia, y crece cada 8 oleadas
    static int Cap(Species s, int n)
    {
        int b = Mathf.Max(1, s.maxPerWave);
        return b + b * Mathf.Max(0, n - s.unlockWave) / 8;
    }

    static int Pack(Species s)
    {
        return Mathf.Clamp(s.maxPerWave, 1, 3);
    }

    static float Weight(Species s, int n, bool first)
    {
        float w = 1f;
        if (first) w *= Mathf.Max(0.35f, 1f - 0.05f * (n - 1));   // la primera especie (gaviota): cada vez menos
        if (n - s.unlockWave <= 1) w *= 1.8f;                       // recién llegada: sale más
        if (s.type.health >= 90) w *= 0.6f;                         // pesadas: un poco menos
        return w;
    }

    // Reparte las aves en grupos (por qué lados y cuándo llegan)
    static void Arrange(Wave wave, List<BirdType> flock, System.Random rng)
    {
        Shuffle(flock, rng);
        int n = wave.number;

        if (wave.IsBossWave)
        {
            wave.pattern = Pattern.Boss;
            int groups = Mathf.Max(wave.bosses.Count, flock.Count >= 6 ? 2 : 1);
            float baseAngle = 360f / groups;
            for (int g = 0; g < groups; g++)
            {
                Group group = new Group
                {
                    yawOffset = g == 0 ? 0f : g * baseAngle + Jitter(rng, 20f),
                    delay = g == 0 ? 0f : 9f + 11f * (g - 1) + (float)rng.NextDouble() * 4f
                };
                wave.groups.Add(group);
            }
            // Cada jefe al frente de su grupo (el primero llega con la cinemática; los demás, por otros lados)
            for (int b = 0; b < wave.bosses.Count; b++) wave.groups[b % groups].birds.Add(wave.bosses[b]);
            // La escolta: más de la mitad con el primer jefe
            int firstShare = Mathf.CeilToInt(flock.Count * (groups > 1 ? 0.55f : 1f));
            for (int i = 0; i < flock.Count; i++)
            {
                int g = i < firstShare ? 0 : 1 + (i - firstShare) % Mathf.Max(1, groups - 1);
                wave.groups[Mathf.Min(g, groups - 1)].birds.Add(flock[i]);
            }
            return;
        }

        float split = n <= 1 ? 0f : Mathf.Min(MaxSplitChance, 0.3f + 0.04f * n);
        if (flock.Count < 4 || rng.NextDouble() >= split)
        {
            wave.pattern = Pattern.Together;
            Group all = new Group();
            all.birds.AddRange(flock);
            wave.groups.Add(all);
            return;
        }

        // Exploradores sale más seguido; con pocas aves no hay escalonada
        double roll = rng.NextDouble() * (flock.Count >= 8 ? 3.5 : 2.5);
        if (roll < 1.0) wave.pattern = Pattern.Pincer;
        else if (roll < 2.5) wave.pattern = Pattern.Scouts;
        else wave.pattern = Pattern.Staggered;

        switch (wave.pattern)
        {
            case Pattern.Pincer:
            {
                // Dos mitades a la vez, por lados opuestos
                Group a = new Group();
                Group b = new Group { yawOffset = 180f + Jitter(rng, 25f), delay = 0f };
                for (int i = 0; i < flock.Count; i++) (i % 2 == 0 ? a : b).birds.Add(flock[i]);
                wave.groups.Add(a);
                wave.groups.Add(b);
                break;
            }
            case Pattern.Scouts:
            {
                // Un par de las más livianas primero; después un grupo por otro lado y otro grupo por otro lado
                flock.Sort((x, y) => x.health.CompareTo(y.health));
                Group scouts = new Group();
                scouts.birds.Add(flock[0]);
                scouts.birds.Add(flock[1]);
                List<BirdType> rest = flock.GetRange(2, flock.Count - 2);
                Shuffle(rest, rng);
                wave.groups.Add(scouts);
                if (rest.Count >= 6)
                {
                    Group b = new Group { yawOffset = 120f + Jitter(rng, 20f), delay = 9f + (float)rng.NextDouble() * 3f };
                    Group c = new Group { yawOffset = 240f + Jitter(rng, 20f), delay = 19f + (float)rng.NextDouble() * 4f };
                    for (int i = 0; i < rest.Count; i++) (i % 2 == 0 ? b : c).birds.Add(rest[i]);
                    wave.groups.Add(b);
                    wave.groups.Add(c);
                }
                else
                {
                    Group b = new Group { yawOffset = 150f + Jitter(rng, 30f), delay = 9f + (float)rng.NextDouble() * 3f };
                    b.birds.AddRange(rest);
                    wave.groups.Add(b);
                }
                break;
            }
            default:
            {
                // Tres grupos parecidos, por tres lados, uno detrás de otro
                Group a = new Group();
                Group b = new Group { yawOffset = 120f + Jitter(rng, 20f), delay = 10f + (float)rng.NextDouble() * 3f };
                Group c = new Group { yawOffset = 240f + Jitter(rng, 20f), delay = 20f + (float)rng.NextDouble() * 5f };
                for (int i = 0; i < flock.Count; i++) (i % 3 == 0 ? a : (i % 3 == 1 ? b : c)).birds.Add(flock[i]);
                wave.groups.Add(a);
                wave.groups.Add(b);
                wave.groups.Add(c);
                break;
            }
        }
    }

    // Texto corto para el cartel de la oleada
    public static string Describe(Wave wave)
    {
        if (wave == null) return "";
        if (wave.IsBossWave)
        {
            if (wave.bosses.Count == 1) return "¡JEFE: " + BossName(wave.bosses[0]) + "!";
            if (wave.bosses.Count == 2) return "¡DOS JEFES: " + BossName(wave.bosses[0]) + " Y " + BossName(wave.bosses[1]) + "!";
            return "¡" + wave.bosses.Count + " JEFES A LA VEZ, CADA UNO POR SU LADO!";
        }
        switch (wave.pattern)
        {
            case Pattern.Pincer: return "¡LLEGAN POR DOS LADOS A LA VEZ!";
            case Pattern.Scouts: return "Primero un par de exploradores… y detrás vienen más";
            case Pattern.Staggered: return "Llegan en grupos, cada uno por un lado distinto";
            default: return "¡DEPREDADORES EN CAMINO!";
        }
    }

    static string BossName(BirdType t)
    {
        return t != null && !string.IsNullOrEmpty(t.name) ? t.name.ToUpperInvariant() : "JEFE";
    }

    static float Jitter(System.Random rng, float range)
    {
        return (float)(rng.NextDouble() * 2.0 - 1.0) * range;
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
