using UnityEngine;

// ============================================================================
//  APARTADOS PARA TUS MODELOS
//  Se configuran en el Inspector del objeto "GameDirector":
//    - "Tipos de peces": un elemento por cada especie de pez del lago.
//    - "Tipos de aves enemigas": un elemento por cada ave depredadora.
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

    [Header("Cantidad en el lago")]
    public int count = 16;

    [Header("Física")]
    public float colliderRadius = 0.7f;

    [Header("Comportamiento")]
    [Tooltip("1 = normal. Más alto = nada y huye más rápido")]
    public float speedMultiplier = 1f;
    [Tooltip("1 = normal. Más alto = aguanta más tiempo escondido en lo profundo")]
    public float staminaMultiplier = 1f;
}

[System.Serializable]
public class BirdType
{
    [Tooltip("Nombre del tipo (solo para identificarlo en el Inspector)")]
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
    public int health = 40;
    [Tooltip("1 = normal. Afecta patrulla, persecución y picada")]
    public float speedMultiplier = 1f;
    [Tooltip("Daño a tu armadura al embestirte")]
    public float damage = 15f;
    [Tooltip("Distancia a la que te ve y te ataca")]
    public float detectRange = 26f;
}
