using UnityEngine;

// Efectos visuales del traje: estela de burbujas al nadar, partículas de velocidad
// bajo el agua, dash, y el propulsor (luz, chispas, ignición y "tos" con poca carga).
[RequireComponent(typeof(PlayerController_Base))]
public class SuitFX : MonoBehaviour
{
    [Header("Materiales")]
    [Tooltip("Material de partículas usado como plantilla (ej. WaterJek)")]
    public Material fxMaterialTemplate;

    [Header("Nado")]
    public Color bubbleColor = new Color(0.75f, 0.95f, 1f, 0.8f);
    public float bubblesIdle = 3f;
    public float bubblesAtFullSpeed = 45f;
    public Vector3 bubbleOffset = new Vector3(0f, 0f, -0.7f);
    public GameObject dashBurstPrefab;
    [Tooltip("Partículas que flotan en el agua y se estiran al moverse rápido")]
    public bool speedMotes = true;

    [Header("Propulsor")]
    public ParticleSystem jetpackEffect;
    public Color jetColor = new Color(0.3f, 0.85f, 1f, 1f);
    public float jetLightIntensity = 4f;
    public GameObject jetIgnitePrefab;

    private PlayerController_Base controller;
    private ParticleSystem bubbleTrail;
    private ParticleSystem motes;
    private ParticleSystem jetDroplets;
    private Light jetLight;
    private float jetBaseRate = -1f;
    private float jetBaseSpeed = 1f;
    private float lightSpike;

    void Start()
    {
        controller = GetComponent<PlayerController_Base>();
        if (jetpackEffect == null) jetpackEffect = controller.jetpackWaterEffect;

        BuildBubbleTrail();
        BuildJetFX();

        controller.OnDash += HandleDash;
        controller.OnJetIgnite += HandleIgnite;
        controller.OnTurboStart += HandleTurboStart;
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnDash -= HandleDash;
            controller.OnJetIgnite -= HandleIgnite;
            controller.OnTurboStart -= HandleTurboStart;
        }
    }

    // ---------------- Construcción ----------------

    void BuildBubbleTrail()
    {
        Material mat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.Bubble, Color.white);
        bubbleTrail = FXFactory.CreateParticleSystem("BubbleTrail", transform, mat);
        bubbleTrail.transform.localPosition = bubbleOffset;

        var main = bubbleTrail.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.17f);
        main.startColor = bubbleColor;
        main.gravityModifier = -0.12f;
        main.maxParticles = 400;

        var emission = bubbleTrail.emission;
        emission.rateOverTime = 0f;

        var shape = bubbleTrail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var noise = bubbleTrail.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 1.2f;
        noise.scrollSpeed = 0.5f;

        var col = bubbleTrail.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = bubbleTrail.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));

        bubbleTrail.Play();

        if (speedMotes && controller.cameraHolder != null)
        {
            Material moteMat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, Color.white);
            motes = FXFactory.CreateParticleSystem("WaterMotes", controller.cameraHolder, moteMat);
            motes.transform.localPosition = new Vector3(0f, 0f, 7f);

            var mm = motes.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            mm.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            mm.startColor = new Color(0.7f, 0.95f, 1f, 0.35f);
            mm.maxParticles = 250;

            var me = motes.emission;
            me.rateOverTime = 0f;

            var ms = motes.shape;
            ms.enabled = true;
            ms.shapeType = ParticleSystemShapeType.Box;
            ms.scale = new Vector3(14f, 8f, 12f);

            var mc = motes.colorOverLifetime;
            mc.enabled = true;
            Gradient mg = new Gradient();
            mg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            mc.color = mg;

            ParticleSystemRenderer mr = motes.GetComponent<ParticleSystemRenderer>();
            mr.renderMode = ParticleSystemRenderMode.Stretch;
            mr.cameraVelocityScale = 0.08f;
            mr.lengthScale = 1f;
            mr.velocityScale = 0f;

            motes.Play();
        }
    }

    void BuildJetFX()
    {
        if (jetpackEffect == null) return;

        jetBaseRate = jetpackEffect.emission.rateOverTimeMultiplier;
        jetBaseSpeed = jetpackEffect.main.startSpeedMultiplier;
        jetpackEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Luz del propulsor
        GameObject lightGo = new GameObject("JetLight");
        lightGo.transform.SetParent(transform, false);
        jetLight = lightGo.AddComponent<Light>();
        jetLight.type = LightType.Point;
        jetLight.color = jetColor;
        jetLight.range = 5f;
        jetLight.intensity = 0f;
        jetLight.shadows = LightShadows.None;

        // Gotas que salen despedidas del propulsor y caen
        Material dropMat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, Color.white);
        // Se cuelga del jugador (escala 1) y no del modelo (que está muy escalado)
        jetDroplets = FXFactory.CreateParticleSystem("JetDroplets", transform, dropMat);
        var main = jetDroplets.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(jetColor, Color.white);
        main.gravityModifier = 1.2f;
        main.maxParticles = 300;

        var emission = jetDroplets.emission;
        emission.rateOverTime = 0f;

        var shape = jetDroplets.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.05f;

        var size = jetDroplets.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer r = jetDroplets.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1.5f;

        jetDroplets.Play();
    }

    // ---------------- Actualización ----------------

    void Update()
    {
        if (controller == null) return;
        UpdateSwimFX();
        UpdateJetFX();
    }

    void UpdateSwimFX()
    {
        bool swimming = controller.isInWater && !controller.isDead;

        if (bubbleTrail != null)
        {
            var e = bubbleTrail.emission;
            float rate = 0f;
            if (swimming)
            {
                rate = Mathf.Lerp(bubblesIdle, bubblesAtFullSpeed, controller.SwimSpeed01);
                if (controller.IsDashing) rate *= 2.5f;
            }
            e.rateOverTime = rate;
        }

        if (motes != null)
        {
            var e = motes.emission;
            // Bajo el agua: partículas flotando; con turbo en el aire: estelas de velocidad
            e.rateOverTime = controller.IsEyeSubmerged ? 40f : (controller.IsTurbo ? 70f : 0f);
        }
    }

    void LateUpdate()
    {
        if (jetpackEffect == null) return;
        // El chorro siempre sale hacia abajo y un poco hacia atrás
        Vector3 p = jetpackEffect.transform.position;
        Quaternion down = Quaternion.LookRotation(Vector3.down * 0.85f - transform.forward * 0.3f);
        if (jetDroplets != null) jetDroplets.transform.SetPositionAndRotation(p, down);
        if (jetLight != null) jetLight.transform.position = p - Vector3.up * 0.3f;
    }

    void UpdateJetFX()
    {
        if (jetpackEffect == null) return;

        bool turboAir = controller.IsTurbo && !controller.isInWater;
        bool jetting = (controller.IsJetting || turboAir) && !controller.isDead;
        float thrust = turboAir ? 1.3f : controller.JetThrust01;

        if (jetting && !jetpackEffect.isPlaying) jetpackEffect.Play(true);
        if (!jetting && jetpackEffect.isPlaying) jetpackEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (jetBaseRate >= 0f)
        {
            var e = jetpackEffect.emission;
            e.rateOverTimeMultiplier = jetBaseRate * Mathf.Lerp(0.3f, 1.2f, thrust);
            var m = jetpackEffect.main;
            m.startSpeedMultiplier = jetBaseSpeed * Mathf.Lerp(0.5f, 1.15f, thrust);
        }

        if (jetDroplets != null)
        {
            var e = jetDroplets.emission;
            e.rateOverTime = jetting ? Mathf.Lerp(10f, 60f, thrust) : 0f;
        }

        if (jetLight != null)
        {
            lightSpike = Mathf.MoveTowards(lightSpike, 0f, Time.deltaTime * 20f);
            float target = 0f;
            if (jetting)
            {
                float flicker = controller.IsSputtering
                    ? (Random.value > 0.5f ? 1f : 0.15f)
                    : 0.85f + Mathf.PerlinNoise(Time.time * 18f, 0.1f) * 0.3f;
                target = jetLightIntensity * thrust * flicker;
            }
            jetLight.intensity = Mathf.Lerp(jetLight.intensity, target, 1f - Mathf.Exp(-20f * Time.deltaTime)) + lightSpike;
        }
    }

    // ---------------- Eventos ----------------

    void HandleDash()
    {
        if (bubbleTrail != null) bubbleTrail.Emit(45);
        FXFactory.SpawnOneShot(dashBurstPrefab, transform.TransformPoint(bubbleOffset), Quaternion.LookRotation(-transform.forward), 0.7f, 3f);
    }

    void HandleTurboStart()
    {
        if (controller.isInWater)
        {
            if (bubbleTrail != null) bubbleTrail.Emit(30);
        }
        else
        {
            HandleIgnite();
        }
    }

    void HandleIgnite()
    {
        if (jetpackEffect != null) jetpackEffect.Emit(20);
        if (jetDroplets != null) jetDroplets.Emit(25);
        lightSpike = jetLightIntensity * 2f;
        if (jetpackEffect != null)
        {
            FXFactory.SpawnOneShot(jetIgnitePrefab, jetpackEffect.transform.position, jetpackEffect.transform.rotation, 0.4f, 2f);
        }
    }
}
