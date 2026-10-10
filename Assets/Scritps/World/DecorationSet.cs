using System.Collections.Generic;
using UnityEngine;

// Lista de la decoración del lago (árboles, plantas, rocas, chatarra...) y dónde va cada cosa.
// Crear: clic derecho en la carpeta > Create > Fish Out Of Water > Decoración.
// La usa LakeDecorator para repartir los modelos sobre el terreno y dentro del lago.
// La del juego está en Assets/Resources/Decoracion/Decoracion_Lago.
[CreateAssetMenu(menuName = "Fish Out Of Water/Decoración", fileName = "Decoracion_Nueva", order = 30)]
public class DecorationSet : ScriptableObject
{
    [Header("Reparto")]
    [Tooltip("Semilla: el mismo número da siempre el mismo reparto. Cámbiala para probar otro")]
    public int seed = 1234;
    [Tooltip("Hasta qué distancia de la orilla se decora (m)")]
    public float maxDistanceFromShore = 250f;
    [Tooltip("Tamaño de la rejilla con la que se analiza el terreno (m). Más pequeño = más preciso y más lento")]
    [Range(1f, 8f)] public float gridCell = 2f;

    [Header("Zonas libres")]
    [Tooltip("Radio libre alrededor del punto de salida del jugador (m)")]
    public float playerClearRadius = 15f;
    [Tooltip("Centro del lago libre para jugar, en fracción del radio del lago: ahí no asoma nada del agua (juncos, nenúfares, rocas)")]
    [Range(0f, 1f)] public float lakeCenterClear = 0.5f;
    [Tooltip("Lo que va en el fondo queda al menos a esta distancia bajo la superficie (m)")]
    public float underwaterTopClearance = 0.6f;
    [Tooltip("Bajo el agua solo tienen colisión los objetos más grandes que esto (m), para no estorbar a los peces")]
    public float underwaterColliderMinSize = 1.5f;

    [Header("Espacio para las aves")]
    [Tooltip("Junto a la orilla, nada alto pasa de esta altura sobre el agua (m)")]
    public float maxTopAboveWater = 12f;
    [Tooltip("Cuánto más alto puede ser un objeto por cada metro que se aleja de la orilla (1 = rampa de 45°)")]
    public float clearanceSlope = 1f;
    [Tooltip("Los objetos más altos que esto (m) cuentan como 'altos' para las reglas de las aves")]
    public float tallHeight = 4f;

    [Header("Rincones compartidos (chatarra)")]
    [Tooltip("Cuántos rincones tiene cada grupo (las entradas con el mismo 'Grupo de rincón' van juntas)")]
    [Min(1)] public int spotsPerGroup = 3;
    [Tooltip("Radio de cada rincón (m)")]
    public float spotRadius = 7f;
    [Tooltip("Distancia mínima entre rincones del mismo grupo (m)")]
    public float spotSeparation = 45f;

    [Header("Rendimiento")]
    [Tooltip("Distancia a la cámara hasta la que se mueven juncos, algas y nenúfares (m)")]
    public float swayDistance = 70f;
    [Tooltip("Máximo de objetos que se mueven por frame")]
    [Min(10)] public int swayBudget = 300;
    [Tooltip("Tope de seguridad de objetos por entrada")]
    [Min(1)] public int maxPerEntry = 1500;

    [Header("Modelos")]
    public List<DecorationEntry> entries = new List<DecorationEntry>();

    void OnValidate()
    {
        if (entries == null) return;
        foreach (DecorationEntry e in entries)
        {
            if (e == null) continue;
            // Entrada nueva sin rangos: se rellenan con los de su zona
            if (e.distanceRange == Vector2.zero && e.depthRange == Vector2.zero && e.slopeRange == Vector2.zero)
                e.ApplyZoneDefaults();
            if (e.scaleRange.y < e.scaleRange.x) e.scaleRange.y = e.scaleRange.x;
            if (e.modelScale <= 0f) e.modelScale = 1f;
        }
    }
}

// Dónde se coloca cada modelo
public enum DecorZone
{
    Bosque,       // anillo de bosque lejos de la orilla
    BordeBosque,  // entre la pradera y el bosque
    Pradera,      // terreno llano cerca de la orilla
    Orilla,       // justo en la línea del agua (tierra o agua muy baja)
    AguaBaja,     // sale del agua poco profunda (juncos, totoras)
    Superficie,   // flota en el agua (nenúfares)
    FondoLago,    // en el fondo, siempre bajo la superficie
    Rocas         // laderas empinadas
}

public enum DecorCollider { Ninguno, Capsula, Esfera, Caja, Malla }

public enum DecorSway { Ninguno, Viento, Agua, Flotar }

[System.Serializable]
public class DecorationEntry
{
    [Tooltip("Nombre (solo para el Inspector y la jerarquía)")]
    public string name = "Decoración";
    [Tooltip("Desmárcalo para no colocar esta entrada")]
    public bool enabled = true;
    [Tooltip("Modelo (FBX o prefab) con el pivote en la base. Si falta, la entrada se salta")]
    public GameObject prefab;
    [Tooltip("Zona del lago donde se coloca")]
    public DecorZone zone = DecorZone.Pradera;

    [Header("Cantidad")]
    [Tooltip("Número total de objetos. 0 = usar la densidad")]
    [Min(0)] public int count = 20;
    [Tooltip("Objetos por cada 100 m² de zona válida (solo si 'Count' es 0)")]
    [Min(0f)] public float densityPer100m2 = 0f;
    [Tooltip("Objetos por grupo (1 = sueltos). Ej.: 4 pinos juntos, 6 matas de hierba")]
    [Min(1)] public int clusterSize = 1;
    [Tooltip("Radio de cada grupo (m)")]
    [Min(0f)] public float clusterRadius = 0f;
    [Tooltip("Distancia mínima a otros objetos (m)")]
    [Min(0f)] public float minSpacing = 1f;
    [Tooltip("Grupo de rincón compartido (0 = ninguno). Las entradas con el mismo número aparecen juntas en los mismos rincones (ej. la chatarra)")]
    [Min(0)] public int spotGroup = 0;

    [Header("Dónde")]
    [Tooltip("Distancia a la orilla (m): positiva en tierra, negativa dentro del agua")]
    public Vector2 distanceRange;
    [Tooltip("Profundidad del agua (m). Solo cuenta en zonas de agua y en la orilla")]
    public Vector2 depthRange;
    [Tooltip("Inclinación del terreno permitida (grados)")]
    public Vector2 slopeRange;

    [Header("Forma")]
    [Tooltip("Escala aleatoria (mín, máx)")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
    [Tooltip("Giro aleatorio alrededor del eje vertical")]
    public bool randomYaw = true;
    [Tooltip("Cuánto se inclina con el terreno (0 = recto, 1 = pegado a la pendiente)")]
    [Range(0f, 1f)] public float alignToNormal = 0f;
    [Tooltip("Inclinación aleatoria extra (grados). Ej.: latas o la bici tiradas en el fondo")]
    [Range(0f, 90f)] public float randomTilt = 0f;
    [Tooltip("Cuánto se hunde en el suelo (m, a escala 1). En 'Superficie' lo hunde bajo el agua")]
    public float sinkOffset = 0f;
    [Tooltip("Corrección del modelo: rotación (grados). Normalmente 0")]
    public Vector3 modelRotation = Vector3.zero;
    [Tooltip("Corrección del modelo: escala. Normalmente 1")]
    public float modelScale = 1f;

    [Header("Física y render")]
    [Tooltip("Colisión: cápsula para troncos de árbol, esfera/caja/malla para rocas grandes, nada para plantas")]
    public DecorCollider colliderType = DecorCollider.Ninguno;
    [Tooltip("Radio de la cápsula del tronco (m, a escala 1). 0 = automático")]
    [Min(0f)] public float colliderRadius = 0f;
    [Tooltip("Proyecta sombras (desactívalo en plantas pequeñas, ahorra mucho)")]
    public bool castShadows = true;
    [Tooltip("Se oculta cuando ocupa menos de esta fracción de la altura de la pantalla (0 = nunca)")]
    [Range(0f, 0.2f)] public float cullScreenSize = 0.01f;

    [Header("Movimiento")]
    [Tooltip("Vaivén: viento (juncos, hierba), agua (algas) o flotar (nenúfares)")]
    public DecorSway sway = DecorSway.Ninguno;
    [Tooltip("Amplitud del vaivén (grados)")]
    [Range(0f, 20f)] public float swayAmount = 4f;

    // ¿Va dentro del agua?
    public bool IsWaterZone
    {
        get { return zone == DecorZone.AguaBaja || zone == DecorZone.Superficie || zone == DecorZone.FondoLago; }
    }

    // Rangos típicos de cada zona (se aplican solos a las entradas nuevas)
    public void ApplyZoneDefaults()
    {
        depthRange = Vector2.zero;
        switch (zone)
        {
            case DecorZone.Bosque:
                distanceRange = new Vector2(50f, 250f);
                slopeRange = new Vector2(0f, 32f);
                break;
            case DecorZone.BordeBosque:
                distanceRange = new Vector2(20f, 90f);
                slopeRange = new Vector2(0f, 28f);
                break;
            case DecorZone.Pradera:
                distanceRange = new Vector2(4f, 60f);
                slopeRange = new Vector2(0f, 25f);
                break;
            case DecorZone.Orilla:
                distanceRange = new Vector2(-3f, 8f);
                depthRange = new Vector2(0f, 0.8f);
                slopeRange = new Vector2(0f, 55f);
                break;
            case DecorZone.AguaBaja:
                distanceRange = new Vector2(-12f, 0.5f);
                depthRange = new Vector2(0.05f, 1.4f);
                slopeRange = new Vector2(0f, 55f);
                break;
            case DecorZone.Superficie:
                distanceRange = new Vector2(-30f, -1.5f);
                depthRange = new Vector2(0.6f, 8f);
                slopeRange = new Vector2(0f, 90f);
                break;
            case DecorZone.FondoLago:
                distanceRange = new Vector2(-1000f, -2f);
                depthRange = new Vector2(1.5f, 100f);
                slopeRange = new Vector2(0f, 40f);
                break;
            case DecorZone.Rocas:
                distanceRange = new Vector2(3f, 250f);
                slopeRange = new Vector2(28f, 90f);
                break;
        }
    }
}
