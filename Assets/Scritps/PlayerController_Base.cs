using UnityEngine;
using UnityEngine.Rendering;

public class PlayerController_Base : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 5f;
    public float turnSpeed = 120f;
    public float acceleration = 8f;
    public float deceleration = 6f;

    [Header("Vuelo")]
    public float flyForce = 15f;
    public float maxFlySpeed = 8f;
    public float airControl = 0.7f;

    [Header("Referencias")]
    public Transform modeloPez;
    public Transform cameraHolder;

    [Header("Cámara y efectos")]
    public float cameraTiltAmount = 3f;
    public float cameraBobSpeed = 8f;
    public float cameraBobAmount = 0.05f;

    [Header("Cámara en tercera persona")]
    public bool isThirdPerson = false;
    public KeyCode toggleCameraKey = KeyCode.C;
    public Vector3 thirdPersonOffset = new Vector3(0, 1.7f, -5f);
    public Vector3 firstPersonOffset = new Vector3(0, 1.7f, 0);

    [Header("Caída")]
    public float fallMultiplier = 2.5f;
    public float groundCheckDistance = 0.2f;
    public LayerMask groundLayer;

    [Header("Efectos de pez")]
    public float tiltSpeed = 5f;
    [Tooltip("Rotación X que deja al modelo acostado (horizontal)")]
    public float horizontalTiltX = 90f;
    [Tooltip("(Sin uso) antes ponía al pez vertical al subir")]
    public float verticalTiltX = 0f;
    [Tooltip("Máxima inclinación de la nariz hacia arriba al subir")]
    public float maxRisePitch = 30f;
    [Tooltip("Máxima inclinación de la nariz hacia abajo al caer")]
    public float maxFallPitch = 22f;
    [Tooltip("Inclinación lateral al girar")]
    public float turnBank = 18f;

    [Header("Efectos bajo agua - NUEVO")]
    public Volume underwaterVolume;
    [Tooltip("Rapidez con la que se quita el filtro al sacar la cámara del agua (al meterla es inmediato)")]
    public float underwaterTransitionSpeed = 2f;
    private bool isUnderwater = false;

    [Header("Energía del Jetpack - NUEVO")]
    public float maxJetpackEnergy = 100f;
    public float currentJetpackEnergy;
    [Tooltip("Energía por segundo mientras el propulsor está encendido")]
    public float jetpackDrainRate = 7f;
    [Tooltip("Energía por segundo que recarga el traje dentro del agua")]
    public float jetpackRechargeRate = 35f;
    [Tooltip("Coste extra al encender el propulsor (evita spamear la barra espaciadora)")]
    public float jetpackIgnitionCost = 3f;
    [Tooltip("Impulso hacia arriba al encender el propulsor")]
    public float jetpackIgnitionBoost = 3f;
    [Tooltip("Empuje extra cuando vienes cayendo, para que frenar la caída se sienta responsivo")]
    public float jetpackFallBrake = 1.5f;
    [Tooltip("Debajo de este porcentaje el propulsor falla y tose")]
    [Range(0f, 1f)] public float jetpackSputterThreshold = 0.12f;
    public bool isInWater = false;

    [Header("Turbo - NUEVO")]
    public KeyCode turboKey = KeyCode.LeftShift;
    [Tooltip("Velocidad horizontal máxima con turbo fuera del agua")]
    public float turboAirSpeed = 24f;
    [Tooltip("Empuje hacia adelante del turbo")]
    public float turboThrust = 40f;
    [Tooltip("Combustible extra por segundo con turbo (se suma al del propulsor)")]
    public float turboDrainRate = 22f;
    [Tooltip("Multiplicador de velocidad de nado con turbo")]
    public float turboSwimMultiplier = 1.7f;
    [Tooltip("Combustible por segundo del turbo bajo el agua (y no recarga mientras lo usas)")]
    public float turboSwimDrainRate = 10f;
    public float turboFovKick = 14f;

    [Header("Física en Agua - NUEVO")]
    public float waterEntryThreshold = 5f;
    public float waterDrag = 3f;
    public float waterBuoyancy = 5f;
    public float waterSinkSpeed = 2f;
    public float waterNormalSpeed = 1f;
    [Tooltip("Ajuste fino de la altura de la superficie (se calcula con el trigger de agua)")]
    public float waterSurfaceOffset = 0f;

    [Header("Nado - NUEVO")]
    public float swimSpeed = 11f;
    [Tooltip("Qué tan rápido alcanza la velocidad de nado")]
    public float swimResponsiveness = 4f;
    [Tooltip("Qué tan rápido se frena al soltar (más bajo = planea más)")]
    public float swimGlide = 1.2f;
    public float swimTurnSpeed = 170f;
    [Tooltip("Profundidad de la cámara bajo la superficie al nadar")]
    public float swimEyeDepth = 0.7f;
    public KeyCode diveKey = KeyCode.LeftControl;
    public float diveDepth = 3f;
    [Tooltip("Fuerza con la que se mantiene a la profundidad de nado")]
    public float buoyancySpring = 6f;
    public float buoyancyDamping = 3.5f;
    [Tooltip("Aceleración hacia arriba al usar el propulsor bajo el agua")]
    public float swimRiseAcceleration = 22f;
    [Tooltip("Impulso extra al salir del agua con el propulsor (salto de delfín)")]
    public float breachBoost = 6f;

    [Header("Impulso de nado (dash) - NUEVO")]
    public KeyCode swimDashKey = KeyCode.LeftShift;
    public float swimDashSpeed = 22f;
    public float swimDashDuration = 0.35f;
    public float swimDashCooldown = 1.1f;

    [Header("Cámara de nado - NUEVO")]
    public float swimCameraSway = 1.2f;
    public float swimFovBoost = 10f;
    public float dashFovKick = 12f;

    private bool hasEnteredWater = false;
    private float waterEntryTime = 0f;
    private float waterEntryVelocity = 0f;
    private LakeWater currentWater;
    [Tooltip("Qué tan abajo del centro del jugador empieza a 'tocar' el agua (m)")]
    public float waterContactHeight = 0.45f;

    [Header("Armadura - NUEVO")]
    public float maxArmor = 100f;
    public float currentArmor;
    public float minDamage = 13f;
    public float maxDamage = 17f;
    public string enemyTag = "Enemy";
    private Rigidbody rb;
    private Vector3 moveInput;
    private bool isGrounded;
    private Quaternion initialModelRotation;
    public ParticleSystem jetpackWaterEffect;
    [Tooltip("(Ya no se usa: las salpicaduras las hace LakeWater)")]
    public ParticleSystem splashEffect;
    private Vector3 cameraOffset;

    [Header("Referencias UI")]
    public UI_PlayerStatus uiStatus;

    [Header("Efectos de Impacto - NUEVO")]
    public RectTransform uiPanelRect;
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.3f;
    private float shakeTimer = 0f;
    public bool isDead = false;

    // ---- Estado público para efectos y HUD ----
    public bool IsJetting { get; private set; }
    public bool IsSputtering { get; private set; }
    public bool IsTurbo { get; private set; }
    public float JetThrust01 { get; private set; }
    public bool IsDashing { get { return Time.time < dashEndTime; } }
    public bool IsDiving { get; private set; }
    public bool IsEyeSubmerged { get { return isUnderwater; } }
    public float SwimSpeed01 { get; private set; }
    public float WaterSurfaceY { get; private set; }
    public float TurnInput { get; private set; }
    public Camera PlayerCamera { get; private set; }
    public Vector3 Velocity { get { return rb != null ? rb.linearVelocity : Vector3.zero; } }
    public bool IsKnockedBack { get { return Time.time < knockbackEndTime; } }

    // Cinemáticas: bloquea controles (la cinemática usa su propia cámara)
    public bool InputLocked { get; private set; }

    public event System.Action OnDash;
    public event System.Action OnBreach;
    public event System.Action OnJetIgnite;
    public event System.Action OnTurboStart;
    public event System.Action<bool> OnSubmergedChanged;
    public event System.Action<float> OnDamaged;

    private float dashEndTime = -10f;
    private float nextDashTime = 0f;
    private bool dashRequested = false;
    private bool turboHeld = false;
    private float swimPhase = 0f;
    private float baseFov = 60f;
    private float smoothFov = 60f;
    private float fovKick = 0f;
    private float recoilPitch = 0f;
    private float recoilShake = 0f;
    private float currentRoll = 0f;
    private float modelPitch = 0f;
    private float knockbackEndTime = -10f;

    void Start()
    {
        if (isDead) return;
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        currentArmor = maxArmor;

        currentJetpackEnergy = maxJetpackEnergy;
        if (modeloPez == null)
        {
            modeloPez = transform.Find("Capsule") ?? transform.GetChild(0);
        }

        if (cameraHolder == null)
        {
            cameraHolder = GetComponentInChildren<Camera>()?.transform;
        }

        if (modeloPez != null)
        {
            initialModelRotation = modeloPez.localRotation;
        }

        if (cameraHolder != null)
        {
            PlayerCamera = cameraHolder.GetComponent<Camera>();
            if (PlayerCamera != null) baseFov = smoothFov = PlayerCamera.fieldOfView;
            cameraOffset = cameraHolder.position - transform.position;
            cameraHolder.SetParent(null);
        }

        if (jetpackWaterEffect != null) jetpackWaterEffect.Stop();

        // Salpicaduras y anillos al entrar/salir del agua
        if (GetComponent<WaterInteractor>() == null)
        {
            WaterInteractor wi = gameObject.AddComponent<WaterInteractor>();
            wi.size = 1.3f;
            wi.wakeMinSpeed = 4f;
        }

        if (underwaterVolume != null)
        {
            underwaterVolume.weight = 0f;
        }

        // El terreno siempre cuenta como suelo (si "Ground Layer" no lo incluye, nunca se pisa tierra)
        foreach (Terrain t in Terrain.activeTerrains)
        {
            if (t == null) continue;
            int bit = 1 << t.gameObject.layer;
            if ((groundLayer.value & bit) != 0) continue;
            groundLayer.value |= bit;
            Debug.LogWarning("PlayerController: 'Ground Layer' no incluía la capa del terreno (" +
                             LayerMask.LayerToName(t.gameObject.layer) + "). Se agregó automáticamente.");
        }
    }

    void Update()
    {
        if (isDead) return;
        if (PauseMenu.IsPaused) return;

        if (InputLocked)
        {
            moveInput = Vector3.zero;
            TurnInput = 0f;
            turboHeld = false;
            IsDiving = false;
            return;
        }

        float moveVertical = Input.GetAxis("Vertical");
        TurnInput = Input.GetAxis("Horizontal");
        moveInput = new Vector3(0, 0, moveVertical);
        if (moveInput.magnitude > 1f) moveInput.Normalize();

        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f,
                                    Vector3.down,
                                    groundCheckDistance,
                                    groundLayer);

        if (Input.GetKeyDown(toggleCameraKey))
        {
            isThirdPerson = !isThirdPerson;
        }

        // El dash se lee en Update para no perder la pulsación
        if (isInWater && Input.GetKeyDown(swimDashKey) && Time.time >= nextDashTime)
        {
            dashRequested = true;
        }

        turboHeld = Input.GetKey(turboKey);
        IsDiving = isInWater && Input.GetKey(diveKey);
    }

    void FixedUpdate()
    {
        if (isDead || rb == null || rb.isKinematic) return;
        UpdateWaterContact();
        bool wantsToFly = !InputLocked && Input.GetKey(KeyCode.Space);

        if (isInWater)
        {
            SetJetting(false, 0f);
            bool turbo = turboHeld && currentJetpackEnergy > 0f;
            SetTurbo(turbo);
            HandleSwimming(wantsToFly);
            HandleTurning(swimTurnSpeed);

            // El traje se recarga con agua (el turbo consume y pausa la recarga)
            if (turbo)
                currentJetpackEnergy = Mathf.Max(currentJetpackEnergy - turboSwimDrainRate * Time.fixedDeltaTime, 0f);
            else
                currentJetpackEnergy = Mathf.Min(currentJetpackEnergy + jetpackRechargeRate * Time.fixedDeltaTime, maxJetpackEnergy);
        }
        else
        {
            bool canFly = HandleJetpack(wantsToFly);
            HandleTurbo(canFly);
            HandleGroundAndAirMovement(canFly);
            HandleTurning(turnSpeed);
            SwimSpeed01 = Mathf.MoveTowards(SwimSpeed01, 0f, Time.fixedDeltaTime * 2f);
        }

        UpdateModelTilt();
    }

    // ---------------- JETPACK ----------------

    bool HandleJetpack(bool wantsToFly)
    {
        bool hasEnergy = currentJetpackEnergy > 0f;
        // Para encender hace falta pagar el coste de ignición; una vez encendido aguanta hasta 0
        bool canFly = wantsToFly && hasEnergy && (IsJetting || currentJetpackEnergy >= jetpackIgnitionCost);

        if (canFly)
        {
            if (!IsJetting)
            {
                currentJetpackEnergy -= jetpackIgnitionCost;
                Vector3 v = rb.linearVelocity;
                if (v.y < 0f) v.y *= 0.4f;
                v.y += jetpackIgnitionBoost;
                rb.linearVelocity = v;
                OnJetIgnite?.Invoke();
            }

            currentJetpackEnergy = Mathf.Max(currentJetpackEnergy - jetpackDrainRate * Time.fixedDeltaTime, 0f);

            float energy01 = currentJetpackEnergy / maxJetpackEnergy;
            IsSputtering = energy01 < jetpackSputterThreshold;
            float thrust01 = 1f;
            if (IsSputtering)
            {
                // Con poca carga el propulsor tose: el empuje parpadea
                float noise = Mathf.PerlinNoise(Time.time * 9f, 0.37f);
                thrust01 = noise > 0.45f ? 0.85f : 0.25f;
            }

            SetJetting(true, thrust01);

            rb.useGravity = false;
            float force = flyForce * thrust01;
            if (rb.linearVelocity.y < 0f) force += Mathf.Min(-rb.linearVelocity.y * jetpackFallBrake * flyForce * 0.25f, flyForce * 2f);
            rb.AddForce(Vector3.up * force, ForceMode.Force);

            Vector3 vel = rb.linearVelocity;
            if (vel.y > maxFlySpeed) vel.y = maxFlySpeed;
            rb.linearVelocity = vel;
        }
        else
        {
            SetJetting(false, 0f);
            rb.useGravity = true;

            if (rb.linearVelocity.y < 0 && !isGrounded)
            {
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            }
        }

        return canFly;
    }

    void SetJetting(bool on, float thrust01)
    {
        IsJetting = on;
        JetThrust01 = on ? thrust01 : 0f;
        if (!on) IsSputtering = false;
    }

    void SetTurbo(bool on)
    {
        if (on && !IsTurbo)
        {
            fovKick = Mathf.Max(fovKick, turboFovKick * 0.6f);
            OnTurboStart?.Invoke();
        }
        IsTurbo = on;
    }

    // Turbo fuera del agua: mucho más rápido hacia adelante, pero gasta combustible muy rápido
    void HandleTurbo(bool canFly)
    {
        bool airborneOrMoving = canFly || !isGrounded || moveInput.z > 0.1f;
        bool turbo = turboHeld && currentJetpackEnergy > 0f && airborneOrMoving;
        SetTurbo(turbo);
        if (!turbo) return;

        currentJetpackEnergy = Mathf.Max(currentJetpackEnergy - turboDrainRate * Time.fixedDeltaTime, 0f);
        rb.AddForce(transform.forward * turboThrust, ForceMode.Acceleration);
        // Un poco de sustentación para no clavarse en el suelo al ir rápido
        if (!canFly && rb.linearVelocity.y < 0f) rb.AddForce(Vector3.up * flyForce * 0.4f, ForceMode.Acceleration);
    }

    // ---------------- TIERRA / AIRE ----------------

    void HandleGroundAndAirMovement(bool canFly)
    {
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = transform.forward * moveInput.z;
            float currentSpeed = walkSpeed;
            if (!isGrounded)
            {
                currentSpeed = canFly ? walkSpeed * 1.5f : walkSpeed * airControl;
            }

            rb.AddForce(moveDirection * currentSpeed * acceleration, ForceMode.Force);
        }

        if (IsKnockedBack) return;

        if (moveInput.magnitude < 0.1f && isGrounded && !IsTurbo)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            horizontalVel = Vector3.Lerp(horizontalVel, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
        }

        float maxHorizontalSpeed = IsTurbo ? turboAirSpeed : (isGrounded ? walkSpeed : (canFly ? walkSpeed * 1.5f : walkSpeed));
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (flatVel.magnitude > maxHorizontalSpeed)
        {
            // Al salir del agua o del turbo se conserva la inercia y se va frenando poco a poco
            flatVel = Vector3.MoveTowards(flatVel, flatVel.normalized * maxHorizontalSpeed, 12f * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(flatVel.x, rb.linearVelocity.y, flatVel.z);
        }
    }

    void HandleTurning(float speed)
    {
        if (Mathf.Abs(TurnInput) > 0.1f)
        {
            float turnAmount = TurnInput * speed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, turnAmount, 0));
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            rb.angularVelocity = new Vector3(0, Mathf.Lerp(rb.angularVelocity.y, 0, deceleration * Time.fixedDeltaTime), 0);
        }
    }

    // ---------------- NADO ----------------

    void HandleSwimming(bool wantsToRise)
    {
        float dt = Time.fixedDeltaTime;
        WaterSurfaceY = GetWaterSurface();
        Vector3 v = rb.linearVelocity;
        rb.useGravity = false;

        // Dash de nado
        if (dashRequested)
        {
            dashRequested = false;
            dashEndTime = Time.time + swimDashDuration;
            nextDashTime = Time.time + swimDashCooldown;
            fovKick = dashFovKick;
            OnDash?.Invoke();
        }

        float speedMul = IsTurbo ? turboSwimMultiplier : 1f;

        // --- Horizontal: rápido y con inercia, como un pez de verdad ---
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (!IsKnockedBack)
        {
            Vector3 desired;
            float response;
            if (IsDashing)
            {
                desired = transform.forward * swimDashSpeed * Mathf.Max(1f, speedMul * 0.8f);
                response = 14f;
            }
            else if (moveInput.magnitude > 0.1f)
            {
                float dirSpeed = moveInput.z >= 0f ? swimSpeed * speedMul : swimSpeed * 0.45f;
                desired = transform.forward * moveInput.z * dirSpeed;
                response = swimResponsiveness * (IsTurbo ? 1.5f : 1f);
            }
            else if (IsTurbo)
            {
                // Con turbo avanza aunque no aprietes W
                desired = transform.forward * swimSpeed * speedMul;
                response = swimResponsiveness;
            }
            else
            {
                desired = Vector3.zero;
                response = swimGlide;
            }

            // Al girar, la velocidad se curva hacia el frente (sensación ágil, sin derrapar)
            if (flat.sqrMagnitude > 0.01f && (moveInput.z > 0.1f || IsTurbo))
            {
                flat = Vector3.RotateTowards(flat, transform.forward * flat.magnitude, 4f * dt, 0f);
            }
            flat = Vector3.Lerp(flat, desired, 1f - Mathf.Exp(-response * dt));
        }
        else
        {
            flat = Vector3.Lerp(flat, Vector3.zero, 1f - Mathf.Exp(-1.5f * dt));
        }

        // --- Vertical: se mantiene sumergido a una profundidad estable ---
        float vy = v.y;
        float tiempoEnAgua = Time.time - waterEntryTime;
        if (wantsToRise)
        {
            // Propulsor bajo el agua: sube rápido para salir disparado
            vy += swimRiseAcceleration * dt;
            vy = Mathf.Min(vy, maxFlySpeed * 1.4f);
        }
        else
        {
            float targetY = WaterSurfaceY - swimEyeDepth - firstPersonOffset.y - (IsDiving ? diveDepth : 0f);
            float error = targetY - transform.position.y;
            float spring = buoyancySpring;

            // Justo al caer al agua se deja hundir un poco por la inercia antes de estabilizar
            if (hasEnteredWater && tiempoEnAgua < 0.6f)
            {
                spring *= Mathf.Lerp(0.25f, 1f, tiempoEnAgua / 0.6f);
            }
            else
            {
                hasEnteredWater = false;
            }

            float accel = error * spring - vy * buoyancyDamping;
            accel = Mathf.Clamp(accel, -20f, 20f);
            vy += accel * dt;
        }

        rb.linearVelocity = new Vector3(flat.x, vy, flat.z);

        float speed01 = Mathf.Clamp01(flat.magnitude / swimSpeed);
        SwimSpeed01 = Mathf.Lerp(SwimSpeed01, speed01, 1f - Mathf.Exp(-6f * dt));
        swimPhase += dt * Mathf.Lerp(3f, 13f, Mathf.Clamp01(SwimSpeed01));
    }

    // El filtro submarino depende de dónde está la CÁMARA de verdad (con balanceo, tercera persona
    // o cinemática), no de la altura de los ojos del jugador. Además la cámara nunca queda
    // partida por la superficie: media pantalla bajo el agua sin filtro se veía gris.
    Vector3 ResolveCameraWaterline(Vector3 camPos, float fov)
    {
        float surface;
        if (!LakeWater.TryGetSurface(camPos, out surface))
        {
            if (isUnderwater) SetSubmerged(false);
            return camPos;
        }
        surface += waterSurfaceOffset;

        float near = PlayerCamera != null ? PlayerCamera.nearClipPlane : 0.3f;
        float gap = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.15f + 0.04f;
        float d = camPos.y - surface;
        if (Mathf.Abs(d) < gap) camPos.y = surface + (d >= 0f ? gap : -gap);

        bool submerged = camPos.y < surface;
        if (submerged != isUnderwater) SetSubmerged(submerged);
        return camPos;
    }

    void UpdateUnderwaterFx(float dt)
    {
        if (underwaterVolume == null) return;
        // Al entrar: inmediato (si no, se ve el fondo sin color). Al salir: un desvanecido corto.
        underwaterVolume.weight = isUnderwater
            ? 1f
            : Mathf.MoveTowards(underwaterVolume.weight, 0f, dt * Mathf.Max(0.5f, underwaterTransitionSpeed) * 4f);
    }

    void SetSubmerged(bool value)
    {
        ActivarEfectoAgua(value);
        OnSubmergedChanged?.Invoke(value);
    }

    // ---------------- MODELO ----------------

    // Se trabaja con cuaterniones: con ángulos Euler la rotación inicial (~100° en X)
    // se descomponía como (78, 180, 180) y al subir el modelo quedaba boca abajo.
    void UpdateModelTilt()
    {
        if (modeloPez == null) return;
        float dt = Time.fixedDeltaTime;
        Vector3 vel = rb.linearVelocity;

        // Nariz arriba al subir, abajo al caer; siempre mantiene la pose horizontal de base
        float targetPitch = 0f;
        if (!isGrounded)
        {
            float vy = vel.y;
            targetPitch = vy > 0f
                ? Mathf.Clamp(vy * 4f, 0f, maxRisePitch)
                : -Mathf.Clamp(-vy * 2.5f, 0f, maxFallPitch);
        }
        if (isInWater && IsDiving) targetPitch = -15f;
        modelPitch = Mathf.Lerp(modelPitch, targetPitch, 1f - Mathf.Exp(-tiltSpeed * dt));

        float wiggle = 0f;
        float roll = -TurnInput * turnBank;
        if (isInWater)
        {
            // Ondulación del cuerpo al nadar
            float amp = Mathf.Lerp(4f, 16f, Mathf.Clamp01(SwimSpeed01)) * (IsDashing || IsTurbo ? 1.6f : 1f);
            wiggle = Mathf.Sin(swimPhase) * amp;
        }

        // Pose horizontal fija (solo el eje X de la pose de base)
        Quaternion rest = Quaternion.Euler(horizontalTiltX, 0f, 0f);
        // Las inclinaciones se aplican en el espacio del jugador: X = cabeceo, Y = coleteo, Z = alabeo
        Quaternion target = Quaternion.Euler(-modelPitch, wiggle, roll) * rest;

        modeloPez.localRotation = Quaternion.Slerp(modeloPez.localRotation, target, 1f - Mathf.Exp(-tiltSpeed * 2f * dt));
    }

    // ---------------- CÁMARA ----------------

    public void AddRecoil(float pitchKick, float shake)
    {
        recoilPitch += pitchKick;
        recoilShake = Mathf.Max(recoilShake, shake);
    }

    // Pose "normal" de la cámara (la usan las cinemáticas para volver suavemente)
    public void GetDefaultCameraPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 targetOffset = isThirdPerson ? thirdPersonOffset : firstPersonOffset;
        position = transform.position + transform.TransformDirection(targetOffset);
        rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    public void SetCinematicLock(bool locked)
    {
        InputLocked = locked;
        if (rb == null) return;
        if (locked)
        {
            SetJetting(false, 0f);
            SetTurbo(false);
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        else
        {
            rb.isKinematic = false;
        }
    }

    void LateUpdate()
    {
        if (cameraHolder == null) return;

        // Seguro: fuera de una cinemática la cámara del jugador siempre está encendida
        if (!WaveCinematic.IsPlaying)
        {
            if (PlayerCamera != null && !PlayerCamera.enabled) PlayerCamera.enabled = true;
            if (InputLocked) SetCinematicLock(false);
        }

        Vector3 targetOffset = isThirdPerson ? thirdPersonOffset : firstPersonOffset;
        cameraOffset = transform.TransformDirection(targetOffset);
        Vector3 pos = transform.position + cameraOffset;

        float dt = Time.deltaTime;
        float yaw = transform.eulerAngles.y;
        float pitch = 0f;
        float rollTarget = -TurnInput * cameraTiltAmount;

        if (!isThirdPerson)
        {
            // Balanceo al caminar
            if (isGrounded && !isInWater && moveInput.magnitude > 0.1f && !IsJetting)
            {
                pos.y += Mathf.Sin(Time.time * cameraBobSpeed) * cameraBobAmount;
            }

            // Vaivén suave al nadar
            if (isInWater)
            {
                float sway = swimCameraSway * (0.4f + SwimSpeed01);
                pos.y += Mathf.Sin(swimPhase * 0.5f) * 0.04f * sway;
                rollTarget = -TurnInput * cameraTiltAmount * 2.2f + Mathf.Sin(swimPhase * 0.5f) * 0.8f * sway;
                pitch += Mathf.Sin(swimPhase * 0.25f + 1.3f) * 0.6f * sway;
                if (IsDiving) pitch += 8f;
            }

            // Vibración del propulsor / turbo
            if (IsJetting || IsTurbo)
            {
                float j = IsSputtering ? 0.05f : (IsTurbo ? 0.03f : 0.015f);
                pos += Random.insideUnitSphere * j * Mathf.Max(JetThrust01, IsTurbo ? 1f : 0f);
            }
        }

        // Sacudida por daño
        if (shakeTimer > 0)
        {
            shakeTimer -= dt;
            float intensity = shakeMagnitude * 0.01f * (shakeTimer / shakeDuration);
            pos += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f)) * intensity;
        }

        // Retroceso del disparo
        if (recoilShake > 0f)
        {
            pos += Random.insideUnitSphere * recoilShake * 0.03f;
            recoilShake = Mathf.MoveTowards(recoilShake, 0f, dt * 6f);
        }
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, 1f - Mathf.Exp(-12f * dt));
        pitch -= recoilPitch;

        currentRoll = Mathf.Lerp(currentRoll, rollTarget, 1f - Mathf.Exp(-6f * dt));
        Quaternion rot = Quaternion.Euler(pitch, yaw, currentRoll);

        // FOV dinámico: se abre con la velocidad de nado, el dash y el turbo
        fovKick = Mathf.Lerp(fovKick, 0f, 1f - Mathf.Exp(-3f * dt));
        float targetFov = baseFov + (isInWater ? SwimSpeed01 * swimFovBoost : 0f) + fovKick
                          + (IsJetting ? 3f : 0f) + (IsTurbo ? turboFovKick : 0f);
        smoothFov = Mathf.Lerp(smoothFov, targetFov, 1f - Mathf.Exp(-5f * dt));
        float fov = smoothFov;

        // Cinemática de oleada: se mezcla con la vista normal (la cámara siempre es esta)
        Vector3 cinePos;
        Quaternion cineRot;
        float cineFov, cineWeight;
        if (WaveCinematic.TryGetPose(out cinePos, out cineRot, out cineFov, out cineWeight))
        {
            pos = Vector3.Lerp(pos, cinePos, cineWeight);
            rot = Quaternion.Slerp(rot, cineRot, cineWeight);
            fov = Mathf.Lerp(fov, cineFov, cineWeight);
        }

        pos = ResolveCameraWaterline(pos, fov);
        cameraHolder.position = pos;
        cameraHolder.rotation = rot;
        if (PlayerCamera != null) PlayerCamera.fieldOfView = fov;
        UpdateUnderwaterFx(dt);
    }

    // ---------------- AGUA ----------------

    float GetWaterSurface()
    {
        if (currentWater != null) return currentWater.SurfaceY + waterSurfaceOffset;
        return WaterSurfaceY;
    }

    // El agua se consulta a LakeWater (se adapta a la forma del hueco del terreno)
    void UpdateWaterContact()
    {
        Vector3 pos = rb.position;
        LakeWater water = LakeWater.FindAt(pos);
        bool nowIn = false;
        if (water != null)
        {
            float surface = water.SurfaceY + waterSurfaceOffset;
            float bottom = pos.y - waterContactHeight;
            // Pequeña histéresis para no entrar/salir sin parar justo en la superficie
            nowIn = isInWater ? bottom < surface + 0.1f : bottom < surface;
            currentWater = water;
        }

        if (nowIn && !isInWater) EnterWater();
        else if (!nowIn && isInWater) ExitWater();
        if (isInWater) WaterSurfaceY = GetWaterSurface();
    }

    void EnterWater()
    {
        isInWater = true;
        WaterSurfaceY = GetWaterSurface();
        waterEntryVelocity = Mathf.Abs(rb.linearVelocity.y);
        waterEntryTime = Time.time;
        hasEnteredWater = true;

        // Frenazo al entrar (la salpicadura la hace WaterInteractor)
        Vector3 newVelocity = rb.linearVelocity;
        newVelocity.y *= 0.45f;
        newVelocity.x *= 0.85f;
        newVelocity.z *= 0.85f;
        rb.linearVelocity = newVelocity;

        NotifyWaterState(true);
    }

    void ExitWater()
    {
        isInWater = false;
        hasEnteredWater = false;

        // Salto de delfín: salir con el propulsor da un empujón extra
        if (rb.linearVelocity.y > 2f)
        {
            Vector3 v = rb.linearVelocity;
            v.y += breachBoost;
            rb.linearVelocity = v;
            OnBreach?.Invoke();
        }

        NotifyWaterState(false);
    }

    void NotifyWaterState(bool inWater)
    {
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null) shooting.UpdateWaterState(inWater);
    }

    // ---------------- DAÑO ----------------

    // Empujón fuerte (por ejemplo cuando un pájaro te embiste)
    public void ApplyKnockback(Vector3 velocityChange, float controlLossTime = 0.45f)
    {
        if (rb == null || rb.isKinematic) return;
        Vector3 v = rb.linearVelocity;
        if (v.y < 0f) v.y = 0f;
        rb.linearVelocity = v + velocityChange;
        knockbackEndTime = Time.time + controlLossTime;
    }

    public void TakeHit(float damage, Vector3 knockbackVelocity)
    {
        if (isDead) return;
        currentArmor = Mathf.Max(currentArmor - damage, 0f);
        OnDamaged?.Invoke(damage);

        if (currentArmor <= 0f)
        {
            Die();
            return;
        }

        shakeTimer = shakeDuration;
        if (uiStatus != null) uiStatus.StartDamageFlash();
        if (knockbackVelocity.sqrMagnitude > 0.01f) ApplyKnockback(knockbackVelocity);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag(enemyTag)) return;
        // Los pájaros con IA aplican su propio golpe y empujón
        if (collision.collider.GetComponentInParent<BirdAI>() != null) return;

        float damage = Random.Range(minDamage, maxDamage);
        TakeHit(damage, Vector3.zero);
        Debug.Log($"!Impacto enemigo! Daño: {damage}, Armadura restante: {currentArmor}");
    }

    void ActivarEfectoAgua(bool activar)
    {
        // El peso del Volume lo actualiza UpdateUnderwaterFx cada frame
        isUnderwater = activar;
    }

    void Die()
    {
        isDead = true;
        SetJetting(false, 0f);
        SetTurbo(false);
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // Activar filtro rojo permanente
        if (uiStatus != null)
        {
            uiStatus.SetDeadState(true);
        }

        // Desactivar disparo
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null) shooting.enabled = false;

        Debug.Log("¡Has muerto!");
    }

    void OnDrawGizmosSelected()
    {
        if (currentWater == null) return;
        Gizmos.color = Color.cyan;
        Vector3 p = transform.position;
        float y = GetWaterSurface();
        Gizmos.DrawLine(new Vector3(p.x - 3f, y, p.z), new Vector3(p.x + 3f, y, p.z));
        Gizmos.DrawLine(new Vector3(p.x, y, p.z - 3f), new Vector3(p.x, y, p.z + 3f));
    }
}
