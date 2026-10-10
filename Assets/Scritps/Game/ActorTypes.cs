using UnityEngine;

// ============================================================================
//  APARTADOS PARA TUS MODELOS
//  Se editan dentro de los archivos de datos (Create > Fish Out Of Water > Ave / Pez).
//  Cada Nivel (LevelDefinition) dice qué peces y aves aparecen y cuántos.
//  Solo arrastra tu modelo (FBX o prefab) al campo "Model" y ajusta rotación /
//  escala hasta que mire hacia +Z (adelante). Los tipos sin modelo se ignoran.
// ============================================================================

[System.Serializable]
public class FishType
{
    [Tooltip("Nombre del tipo (solo para identificarlo en el Inspector)")]
    public string name = "Pez";

    [Header("Modelo (pon aquí tu modelado)")]
    public GameObject model;
    [Tooltip("Rotación del modelo para que la cabeza mire hacia +Z (adelante)")]
    public Vector3 modelRotation = new Vector3(90f, 0f, 0f);
    public Vector3 modelOffset = Vector3.zero;
    public float modelScale = 28f;
    [Tooltip("Opcional: material que reemplaza al del modelo")]
    public Material materialOverride;
    [Tooltip("Opcional: controlador de animación (nado). Si lo pones, se usa en vez del coleteo por código")]
    public RuntimeAnimatorController animatorController;
    [Tooltip("Coleteo hecho por código (si el modelo no trae animación)")]
    public bool proceduralWiggle = true;
    public float wiggleAmount = 14f;

    [Header("Física")]
    public float colliderRadius = 0.7f;

    [Header("Comportamiento")]
    [Tooltip("1 = normal. Más alto = nada y huye más rápido")]
    public float speedMultiplier = 1f;
    [Tooltip("1 = normal. Más alto = aguanta más tiempo escondido en lo profundo")]
    public float staminaMultiplier = 1f;
}

// Forma de cazar propia de cada especie (copia cómo pesca el ave real)
public enum BirdAbility
{
    None,               // acecha en círculos y se lanza en picada (ave básica)
    SurfaceSnatch,      // gaviota: pasada rasante y rápida, roba peces de la superficie
    HoverDive,          // martín pescador: se queda quieto en el aire y cae en vertical
    ChainDive,          // charrán: cuando uno se lanza, los cercanos lo siguen en cadena
    ShoreSpear,         // garza: se planta en la orilla y arponea a los peces que pasan
    UnderwaterHunter,   // cormorán: se mete al agua y persigue peces nadando
    GroupDive,          // serreta: varias bucean a la vez desde lados distintos del cardumen
    Scoop,              // pelícano: atrapa varios peces de un bocado
    HighDive            // águila pescadora: acecha muy alto y cae en picada larga y precisa
}

// Jefes del modo Historia (oleada 3 de los niveles 5, 10, 15 y 20)
public enum BossKind
{
    None,
    GiantHeron,         // La Garza Gris Gigante: estocadas desde la orilla que levantan olas
    BottomlessPelican,  // El Pelícano Saco Sin Fondo: traga grupos enteros; cada disparo le saca un pez
    CormorantKing,      // El Cormorán Rey: pelea arriba y abajo del agua
    EagleQueen          // La Reina Águila Marina: 3 fases, roba presas, llama refuerzos, picadas largas
}

[System.Serializable]
public class BirdType
{
    [Tooltip("Nombre del tipo. En los jefes es el nombre que se ve sobre su barra de vida")]
    public string name = "Ave";

    [Header("Modelo (pon aquí tu modelado)")]
    public GameObject model;
    [Tooltip("Rotación del modelo para que el pico mire hacia +Z (adelante)")]
    public Vector3 modelRotation = Vector3.zero;
    public Vector3 modelOffset = Vector3.zero;
    public float modelScale = 1f;
    [Tooltip("Opcional: material que reemplaza al del modelo")]
    public Material materialOverride;
    [Tooltip("Opcional: controlador de animación (vuelo). Si lo pones, se desactiva el aleteo por código")]
    public RuntimeAnimatorController animatorController;

    [Header("Aleteo por código (si el modelo no trae animación)")]
    public bool proceduralWingFlap = true;
    [Tooltip("Nombres de los huesos de las alas, separados por coma (el lado se detecta solo)")]
    public string wingBones = "LeftArm,RightArm,L_wing,R_wing";
    [Tooltip("Huesos de las patas/garras donde cuelga el pez atrapado, separados por coma")]
    public string talonBones = "LeftFoot,RightFoot";
    [Tooltip("Si no encuentra las garras, el pez cuelga en este punto (local)")]
    public Vector3 catchPointOffset = new Vector3(0f, -1.1f, 0f);

    [Header("Física")]
    public float colliderRadius = 1.3f;
    public Vector3 colliderCenter = new Vector3(0f, 0.3f, 0f);

    [Header("Estadísticas")]
    [Tooltip("Vida. En las oleadas se multiplica por el 'Health Multiplier' de la oleada, " +
             "salvo en los jefes: ellos usan este valor tal cual")]
    public int health = 40;
    [Tooltip("1 = normal. Afecta patrulla, persecución y picada")]
    public float speedMultiplier = 1f;
    [Tooltip("Daño a tu armadura al embestirte")]
    public float damage = 15f;
    [Tooltip("Distancia a la que te ve y te ataca")]
    public float detectRange = 26f;
    [Tooltip("Segundos que tarda en comerse un pez atrapado (tiempo que tienes para rescatarlo)")]
    public float carryTime = 3.5f;
    [Tooltip("Distancia a la que te golpea. La garza tiene una estocada larga; los jefes, más")]
    public float attackReach = 2.6f;

    [Header("Caza")]
    [Tooltip("Ataque especial de la especie (cómo caza peces)")]
    public BirdAbility ability = BirdAbility.None;
    [Tooltip("Distancia de las garras al pez para atraparlo")]
    public float catchRadius = 2.4f;
    [Tooltip("Profundidad máxima (m bajo la superficie) a la que alcanza un pez")]
    public float catchDepth = 2f;
    [Tooltip("Altura sobre el agua a la que acecha antes de lanzarse")]
    public float stalkAltitude = 9f;
    [Tooltip("Pelícano: cuántos peces atrapa como máximo de un bocado")]
    [Min(1)] public int scoopCount = 3;
    [Tooltip("Cormorán / serreta: segundos que aguanta bajo el agua persiguiendo peces")]
    public float underwaterTime = 4f;

    [Header("Jefe")]
    [Tooltip("Si no es None, es un jefe: barra de vida arriba, fases y pausa vulnerable")]
    public BossKind boss = BossKind.None;
    [Tooltip("Segundos de la pausa vulnerable después de cada ataque fuerte")]
    public float vulnerableTime = 2.5f;
    [Tooltip("Multiplica el daño que recibe durante la pausa vulnerable")]
    public float vulnerableDamageMultiplier = 2f;
    [Tooltip("Reina Águila: ave que llama como refuerzo al cambiar de fase")]
    public BirdDefinition reinforcements;
    [Tooltip("Cuántas aves de refuerzo llegan en cada cambio de fase")]
    public int reinforcementCount = 3;
}
