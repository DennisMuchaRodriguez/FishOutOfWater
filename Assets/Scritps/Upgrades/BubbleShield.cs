using UnityEngine;

// Escudo de burbuja: absorbe un golpe entero y vuelve a cargarse (I: 20 s, II: 12 s).
// PlayerController_Base.TakeHit llama a TryAbsorb antes de restar armadura.
// En tercera persona se ve una burbuja tenue alrededor del pez mientras está listo; al romperse
// revienta en burbujitas. En primera persona lo muestra el visor (VisorAmmoHUD).
// CONTRATO para el HUD: Instance, Unlocked, Ready, Recharge01 y Popped.
// Lo agrega PlayerUpgrades al jugador; sin la mejora no hace nada.
public class BubbleShield : MonoBehaviour
{
    public static BubbleShield Instance { get; private set; }

    public bool Unlocked { get; protected set; }
    public bool Ready { get; protected set; }
    // Progreso de la recarga (0..1); 1 si está listo
    public float Recharge01 { get; protected set; }

    public event System.Action Popped;

    [Header("Escudo de burbuja")]
    [Tooltip("Tamaño de la burbuja visible (metros)")]
    public float bubbleSize = 3.4f;
    public Color tint = new Color(0.7f, 0.95f, 1f, 0.35f);

    float rechargeTime = 20f;
    float timer;
    float popFlash;
    PlayerController_Base player;
    MeshRenderer bubbleRenderer;
    ParticleSystem burst;
    Material bubbleMaterial;

    protected virtual void Awake() { Instance = this; }
    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (burst != null) Destroy(burst.gameObject);
    }

    protected void RaisePopped() { if (Popped != null) Popped(); }

    // level 0 = sin escudo, 1 = vuelve en 20 s, 2 = vuelve en 12 s
    public void Configure(int level)
    {
        bool was = Unlocked;
        Unlocked = level > 0;
        rechargeTime = level >= 2 ? 12f : 20f;
        if (Unlocked && !was)
        {
            Ready = true;
            Recharge01 = 1f;
        }
        if (!Unlocked)
        {
            Ready = false;
            Recharge01 = 0f;
        }
    }

    // Absorbe el golpe si está listo. Devuelve true si lo absorbió (no hay daño)
    public bool TryAbsorb()
    {
        if (!Unlocked || !Ready || !isActiveAndEnabled) return false;
        Ready = false;
        timer = 0f;
        Recharge01 = 0f;
        popFlash = 1f;
        if (burst != null)
        {
            burst.transform.position = transform.position + Vector3.up * 0.4f;
            burst.Emit(40);
        }
        RaisePopped();
        return true;
    }

    void Start()
    {
        player = GetComponent<PlayerController_Base>();
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        Material template = shooting != null ? shooting.fxMaterialTemplate : null;

        bubbleMaterial = FXFactory.ParticleMaterialAlpha(template, FXFactory.Bubble, tint);
        FXBillboard bubble = FXBillboard.Create("EscudoBurbuja", transform, bubbleMaterial, bubbleSize, 0.03f);
        bubble.wobbleSpeed = 3f;
        bubble.transform.localPosition = Vector3.up * 0.4f;
        bubbleRenderer = bubble.GetComponent<MeshRenderer>();
        bubbleRenderer.enabled = false;

        Material burstMat = FXFactory.ParticleMaterial(template, FXFactory.Bubble, Color.white);
        burst = FXFactory.CreateParticleSystem("EscudoRoto", null, burstMat);
        var main = burst.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
        main.startColor = new Color(0.8f, 0.97f, 1f, 0.9f);
        main.gravityModifier = -0.2f;
        var shape = burst.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = bubbleSize * 0.4f;
        var size = burst.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        burst.Play();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Unlocked && !Ready)
        {
            timer += dt;
            Recharge01 = Mathf.Clamp01(timer / Mathf.Max(0.1f, rechargeTime));
            if (timer >= rechargeTime)
            {
                Ready = true;
                Recharge01 = 1f;
                popFlash = 0.5f;   // destello suave: "escudo listo"
            }
        }
        popFlash = Mathf.Max(0f, popFlash - dt * 2.5f);

        if (bubbleRenderer != null)
        {
            bool thirdPerson = player != null && player.isThirdPerson && !player.isDead;
            bool show = Unlocked && thirdPerson && (Ready || popFlash > 0f) && !GameDirector.InCinematic;
            if (bubbleRenderer.enabled != show) bubbleRenderer.enabled = show;
            if (show)
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.2f);
                float a = Ready ? tint.a * pulse + popFlash * 0.4f : popFlash * 0.6f;
                Color c = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(a));
                if (bubbleMaterial.HasProperty("_BaseColor")) bubbleMaterial.SetColor("_BaseColor", c);
                if (bubbleMaterial.HasProperty("_Color")) bubbleMaterial.SetColor("_Color", c);
            }
        }
    }

    void OnDisable()
    {
        if (bubbleRenderer != null) bubbleRenderer.enabled = false;
    }
}
