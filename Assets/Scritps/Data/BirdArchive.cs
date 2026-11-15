using System.Collections.Generic;
using UnityEngine;

// Datos del Archivo de aves (pantalla del mapa de niveles): una ficha por especie que aparece en el
// catálogo de niveles, en el orden en que el jugador se las encuentra.
// El dibujo de cada ficha está en Resources/Archivo/<nombre del archivo del ave>.png
// (lo genera Herramientas/Archivo/bocetos.py). Qué especies ya se vieron y cuántas se derribaron
// lo guarda SaveSystem (birdsSeen / birdKills) usando el nombre del ave (BirdType.name).
public static class BirdArchive
{
    public class Attack
    {
        public string name;
        public string text;
        public Attack(string name, string text) { this.name = name; this.text = text; }
    }

    public class Entry
    {
        public BirdDefinition definition;
        public BirdType type;
        public string assetName;        // nombre del archivo del ave (para el dibujo)
        public int firstLevel;          // número del primer nivel donde aparece
        public bool isBoss;

        public string role;             // estilo de combate en una o dos palabras
        public string hunt;             // cómo caza peces
        public string fight;            // cómo pelea contra el jugador
        public string tip;              // consejo
        public string[] tags;
        public Attack[] attacks;        // solo jefes

        public string Species { get { return type != null ? type.name : ""; } }
        public bool Seen { get { return SaveSystem.IsBirdSeen(Species); } }
        public bool IsNew { get { return SaveSystem.IsBirdNew(Species); } }
        public int Kills { get { return SaveSystem.GetBirdKills(Species); } }

        // Estadísticas de 0 a 1 para las barras (los jefes se comparan entre jefes)
        public float Health01 { get { return Mathf.Clamp01(type.health / (isBoss ? 1400f : 210f)); } }
        public float Speed01 { get { return Mathf.Clamp01(type.speedMultiplier / 1.5f); } }
        public float Damage01 { get { return Mathf.Clamp01(type.damage / 35f); } }
        public float Sight01 { get { return Mathf.Clamp01(type.detectRange / 40f); } }
    }

    static List<Entry> cache;

    public static List<Entry> Entries
    {
        get
        {
            if (cache == null) cache = Build();
            return cache;
        }
    }

    public static int SeenCount
    {
        get
        {
            int n = 0;
            foreach (Entry e in Entries) if (e.Seen) n++;
            return n;
        }
    }

    static List<Entry> Build()
    {
        List<Entry> list = new List<Entry>();
        LevelCatalog catalog = LevelCatalog.Load();
        if (catalog == null) return list;

        foreach (LevelDefinition level in catalog.levels)
        {
            if (level == null) continue;
            foreach (WaveDefinition wave in level.waves)
            {
                if (wave == null) continue;
                foreach (BirdSpawn spawn in wave.birds)
                {
                    if (spawn == null || spawn.type == null) continue;
                    Add(list, spawn.type, level.number);
                    // Los refuerzos de un jefe también son especies del Archivo
                    if (spawn.type.bird != null) Add(list, spawn.type.bird.reinforcements, level.number);
                }
            }
        }
        return list;
    }

    static void Add(List<Entry> list, BirdDefinition def, int levelNumber)
    {
        if (def == null || def.bird == null) return;
        // Dos archivos con el mismo nombre de ave cuentan como la misma especie
        foreach (Entry e in list) if (e.definition == def || e.Species == def.bird.name) return;

        Entry entry = new Entry();
        entry.definition = def;
        entry.type = def.bird;
        entry.assetName = def.name;
        entry.firstLevel = levelNumber;
        entry.isBoss = def.bird.boss != BossKind.None;
        if (entry.isBoss) FillBoss(entry);
        else FillBird(entry);
        list.Add(entry);
    }

    // ---- Textos de las aves comunes (según cómo cazan y pelean, ver BirdAI.Combat.cs) ----

    static void FillBird(Entry e)
    {
        switch (e.type.ability)
        {
            case BirdAbility.SurfaceSnatch:
                e.role = "HOSTIGADORA";
                e.hunt = "Pasa rasante y rápida sobre el lago y roba los peces que nadan cerca de la superficie.";
                e.fight = "Nunca repite el ataque: cruza por encima soltando \"regalos\", te los lanza en arco desde lejos o se tira en zigzag a picotearte. Esquiva tus disparos y, si está herida, huye y vuelve furiosa.";
                e.tip = "Dispárale cuando vire de lado después de atacar: es cuando menos se mueve.";
                e.tags = new[] { "A DISTANCIA", "BOMBAS", "ZIGZAG" };
                break;
            case BirdAbility.HoverDive:
                e.role = "RAYO";
                e.hunt = "Se queda quieto en el aire sobre un pez y cae en vertical, sin aviso.";
                e.fight = "Se detiene, te apunta y cruza en línea recta a velocidad altísima. A veces rebota y repite la embestida.";
                e.tip = "Cuando se quede quieto mirándote, sal de su línea. Es frágil: pocos disparos lo derriban.";
                e.tags = new[] { "EMBESTIDA", "MUY VELOZ", "LIVIANA" };
                break;
            case BirdAbility.ChainDive:
                e.role = "ENJAMBRE";
                e.hunt = "Cuando uno se lanza al agua, los charranes cercanos lo siguen en cadena.";
                e.fight = "Embestidas rápidas y cortas. Si uno ataca, los de alrededor lo siguen uno tras otro.";
                e.tip = "Nunca vienen solos: derriba al primero y prepárate para los que siguen.";
                e.tags = new[] { "EN GRUPO", "EMBESTIDA", "LIVIANA" };
                break;
            case BirdAbility.ShoreSpear:
                e.role = "ESTOCADA";
                e.hunt = "Se planta en la orilla y arponea a los peces que pasan cerca.";
                e.fight = "Se acerca, levanta el pico y lanza una estocada larga; a veces encadena una segunda. Los disparos no la empujan.";
                e.tip = "Cuando levante el pico, aléjate. Después de la estocada queda expuesta.";
                e.tags = new[] { "BLINDADA", "CUERPO A CUERPO", "ORILLA" };
                break;
            case BirdAbility.UnderwaterHunter:
                e.role = "TORPEDO";
                e.hunt = "Se mete al agua y persigue a los peces nadando entre ellos.";
                e.fight = "Embestidas rectas y muy rápidas, también por debajo del agua. Los disparos apenas la frenan.";
                e.tip = "Bajar a lo hondo no te salva de ella: guarda distancia y dispara cuando termine su embestida.";
                e.tags = new[] { "BUCEA", "EMBESTIDA", "RESISTENTE" };
                break;
            case BirdAbility.GroupDive:
                e.role = "A DISTANCIA";
                e.hunt = "Varias bucean a la vez desde lados distintos del cardumen para que no tenga escape.";
                e.fight = "Se queda lejos, te rodea y escupe chorros de agua: ráfagas de tres o abanicos de tres a la vez.";
                e.tip = "Entre ráfaga y ráfaga hay una pausa: ese es tu momento para responder.";
                e.tags = new[] { "A DISTANCIA", "BUCEA", "EN GRUPO" };
                break;
            case BirdAbility.Scoop:
                e.role = "APLASTADOR";
                e.hunt = "Atrapa varios peces de un solo bocado y se los lleva en el buche.";
                e.fight = "Embestida pesada que empuja muchísimo. Si estás lejos, te lanza bolas de agua en arco. Los disparos no lo empujan.";
                e.tip = "Si se llevó peces, dispárale sin parar para que los suelte.";
                e.tags = new[] { "BLINDADA", "PESADA", "A DISTANCIA" };
                break;
            case BirdAbility.HighDive:
                e.role = "PICADA";
                e.hunt = "Acecha muy alto y cae en una picada larga y precisa sobre su presa.";
                e.fight = "Sube muy alto sobre ti y cae en picada, rapidísima. Si te alcanza, te lanza por los aires.";
                e.tip = "Cuando deje de volar en círculos sobre ti, cambia de dirección de inmediato.";
                e.tags = new[] { "PICADA", "VELOZ", "TE LANZA" };
                break;
            default:
                e.role = "EMBESTIDA";
                e.hunt = "Acecha en círculos y se lanza en picada sobre los peces.";
                e.fight = "Se coloca, avisa con un aleteo nervioso y se lanza contra ti.";
                e.tip = "Cuando aletee nervioso, prepárate para esquivar.";
                e.tags = new[] { "EMBESTIDA" };
                break;
        }
    }

    // ---- Jefes (ver BirdAI.Boss.cs) ----

    static void FillBoss(Entry e)
    {
        string x = "x" + e.type.vulnerableDamageMultiplier.ToString("0.#");
        e.tip = "Después de cada ataque fuerte queda vulnerable unos segundos y recibe " + x + " de daño: descarga todo ahí.";
        switch (e.type.boss)
        {
            case BossKind.GiantHeron:
                e.role = "SEÑORA DE LA ORILLA";
                e.hunt = "No caza como las demás: domina la orilla y convierte el agua en una trampa para el cardumen.";
                e.fight = "La más rápida en el acoso: te rodea cambiando de sentido, esquiva tus disparos, te lanza dardos y encadena dos o tres embestidas seguidas.";
                e.tags = new[] { "JEFE", "3 FASES", "BLINDADA" };
                e.attacks = new[]
                {
                    new Attack("ESTOCADA", "Un golpe de pico que levanta una ola y aturde a los peces."),
                    new Attack("PISOTÓN", "Golpea el agua y lanza peces fuera del lago, hacia la orilla."),
                    new Attack("LLUVIA DE DARDOS", "Desde la fase 2: una lluvia de dardos desde lo alto.")
                };
                break;
            case BossKind.BottomlessPelican:
                e.role = "BUCHE SIN FONDO";
                e.hunt = "Traga grupos enteros de peces. Cada disparo que le aciertas le saca un pez del buche.";
                e.fight = "Siempre cerca de ti, empujando con su cuerpo enorme entre ataque y ataque.";
                e.tags = new[] { "JEFE", "3 FASES", "BLINDADA", "TRAGA PECES" };
                e.attacks = new[]
                {
                    new Attack("REMOLINO", "Arrastra a los peces hacia el centro y se traga a los que llegan."),
                    new Attack("CAÑONAZO DE AGUA", "Bolas de agua que explotan al caer y aturden a los peces."),
                    new Attack("BOCADO AL CARDUMEN", "Desde la fase 2: se lanza sobre el cardumen con el pico abierto.")
                };
                break;
            case BossKind.CormorantKing:
                e.role = "REY DEL FONDO";
                e.hunt = "Pelea arriba y abajo del agua: no hay lugar del lago donde los peces estén a salvo.";
                e.fight = "Te rodea y embiste sin descanso, entrando y saliendo del agua.";
                e.tags = new[] { "JEFE", "3 FASES", "BLINDADA", "BUCEA" };
                e.attacks = new[]
                {
                    new Attack("TORPEDOS", "De dos a cuatro embestidas seguidas, por encima y por debajo del agua."),
                    new Attack("ONDA EXPANSIVA", "Un estallido submarino que aturde a los peces cercanos y se los lleva.")
                };
                break;
            case BossKind.EagleQueen:
                e.role = "REINA DEL CIELO";
                e.hunt = "Roba las presas de otras aves y llama refuerzos cada vez que cambia de fase.";
                e.fight = "La más peligrosa: tres fases, picadas desde muy alto y un viento capaz de vaciar el lago.";
                e.tags = new[] { "JEFE", "3 FASES", "BLINDADA", "REFUERZOS" };
                e.attacks = new[]
                {
                    new Attack("PICADAS", "De una a tres picadas seguidas desde el cielo."),
                    new Attack("RÁFAGA DE PLUMAS", "Lanza un abanico de plumas afiladas."),
                    new Attack("VENDAVAL", "Desde la fase 2: te empuja y saca a los peces del agua.")
                };
                break;
            default:
                e.role = "JEFE";
                e.hunt = "Un depredador fuera de serie.";
                e.fight = "Te acosa de cerca y alterna ataques fuertes.";
                e.tags = new[] { "JEFE", "3 FASES" };
                e.attacks = new Attack[0];
                break;
        }
    }
}
