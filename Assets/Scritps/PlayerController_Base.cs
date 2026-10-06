using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    public float horizontalTiltX = 90f;
    public float verticalTiltX = 0f;

    [Header("Efectos bajo agua - NUEVO")]
    public Volume underwaterVolume;
    public float underwaterTransitionSpeed = 2f;
    private bool isUnderwater = false;

    [Header("Energía del Jetpack - NUEVO")]
    public float maxJetpackEnergy = 100f;
    public float currentJetpackEnergy;
    public float jetpackDrainRate = 20f;
    public float jetpackRechargeRate = 30f;
    public bool isInWater = false;
    [Header("Física en Agua - NUEVO")]
    public float waterEntryThreshold = 5f;
    public float waterDrag = 3f;
    public float waterBuoyancy = 5f;
    public float waterSinkSpeed = 2f;
    public float waterNormalSpeed = 1f;
    private bool hasEnteredWater = false;
    private float waterEntryTime = 0f;
    private float waterEntryVelocity = 0f;
    [Header("Armadura - NUEVO")]
    public float maxArmor = 100f;
    public float currentArmor;
    public float minDamage = 13f;
    public float maxDamage = 17f;
    public string enemyTag = "Enemy";
    private bool isLowHealth = false;
    private Rigidbody rb;
    private Vector3 moveInput;
    private bool isGrounded;
    private float targetTiltX;
    private Quaternion initialModelRotation;
    public ParticleSystem jetpackWaterEffect;
    public ParticleSystem splashEffect;
    private Vector3 cameraOffset;
    [Header("Referencias UI")]
    public UI_PlayerStatus uiStatus;

    [Header("Efectos de Impacto - NUEVO")]
    public RectTransform uiPanelRect;
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.3f;
    private float shakeTimer = 0f;
    private Vector3 originalCameraPos;
    private Vector2 originalUIPosition;
    public bool isDead = false;

    void Start()
    {
        if (isDead) return;
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        currentArmor = maxArmor;

        currentJetpackEnergy = maxJetpackEnergy;
        if (uiPanelRect != null)
            originalUIPosition = uiPanelRect.anchoredPosition;
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
            cameraOffset = cameraHolder.position - transform.position;
            cameraHolder.SetParent(null);
        }




        if (underwaterVolume != null)
        {
            underwaterVolume.weight = 0f;
        }

    }

    void Update()
    {
        if (isDead) return;
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            if (cameraHolder != null)
            {
                float intensity = shakeMagnitude * (shakeTimer / shakeDuration);
                Vector3 shakeOffset = new Vector3(
                    Random.Range(-1f, 1f) * intensity,
                    Random.Range(-1f, 1f) * intensity,
                    Random.Range(-1f, 1f) * intensity * 0.5f
                );
                cameraHolder.position = transform.position + cameraOffset + shakeOffset;

                if (shakeTimer <= 0)
                {
                    cameraHolder.position = transform.position + cameraOffset;
                }
            }
        }


        float moveVertical = Input.GetAxis("Vertical");
        float moveHorizontal = Input.GetAxis("Horizontal");
        moveInput = new Vector3(0, 0, moveVertical).normalized;

        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f,
                                    Vector3.down,
                                    groundCheckDistance,
                                    groundLayer);


        if (Input.GetKeyDown(toggleCameraKey))
        {
            isThirdPerson = !isThirdPerson;
            if (cameraHolder != null)
            {
                Vector3 targetOffset = isThirdPerson ? thirdPersonOffset : firstPersonOffset;
                cameraOffset = transform.TransformDirection(targetOffset);
            }
        }

        if (cameraHolder != null)
        {
            cameraHolder.position = transform.position + cameraOffset;
            cameraHolder.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }

        if (!isThirdPerson && cameraHolder != null)
        {
            HandleCameraEffects(moveVertical, moveHorizontal);
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;
        bool wantsToFly = Input.GetKey(KeyCode.Space);
        bool hasEnergy = currentJetpackEnergy > 0;
        bool canFly = wantsToFly && (currentJetpackEnergy > 0 || isInWater);
        if (isInWater)
        {
            HandleWaterPhysics();
        }
        if (canFly)
        {
            // Consumir energía solo si NO está en agua
            if (!isInWater)
            {
                currentJetpackEnergy -= jetpackDrainRate * Time.fixedDeltaTime;
                currentJetpackEnergy = Mathf.Max(currentJetpackEnergy, 0);

                // Activar efecto de fuego solo fuera del agua
                if (jetpackWaterEffect != null && !jetpackWaterEffect.isPlaying)
                    jetpackWaterEffect.Play();
            }
            else
            {
                // En agua, asegurar que el efecto de fuego esté apagado
                if (jetpackWaterEffect != null && jetpackWaterEffect.isPlaying)
                    jetpackWaterEffect.Stop();
            }

            rb.useGravity = false;
            rb.AddForce(Vector3.up * flyForce, ForceMode.Force);

            if (rb.linearVelocity.magnitude > maxFlySpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * maxFlySpeed;
        }
        else
        {
            // Apagar efecto de fuego si estaba activo
            if (jetpackWaterEffect != null && jetpackWaterEffect.isPlaying)
                jetpackWaterEffect.Stop();

            rb.useGravity = true;

            // Recargar energía solo en agua y cuando no vuela
            if (isInWater)
            {
                currentJetpackEnergy += jetpackRechargeRate * Time.fixedDeltaTime;
                currentJetpackEnergy = Mathf.Min(currentJetpackEnergy, maxJetpackEnergy);
            }

            if (rb.linearVelocity.y < 0 && !isGrounded && !isInWater)
            {
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            }
        }

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

        float turnInput = Input.GetAxis("Horizontal");
        if (Mathf.Abs(turnInput) > 0.1f)
        {
            float turnAmount = turnInput * turnSpeed * Time.fixedDeltaTime;
            transform.Rotate(0, turnAmount, 0);
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            rb.angularVelocity = new Vector3(0, Mathf.Lerp(rb.angularVelocity.y, 0, deceleration * Time.fixedDeltaTime), 0);
        }



        if (moveInput.magnitude < 0.1f && isGrounded)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            horizontalVel = Vector3.Lerp(horizontalVel, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
        }
        float maxHorizontalSpeed = isGrounded ? walkSpeed : (canFly ? walkSpeed * 1.5f : walkSpeed);
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (flatVel.magnitude > maxHorizontalSpeed)
        {
            flatVel = flatVel.normalized * maxHorizontalSpeed;
            rb.linearVelocity = new Vector3(flatVel.x, rb.linearVelocity.y, flatVel.z);
        }
        UpdateModelTilt(wantsToFly);
    }

    void UpdateModelTilt(bool isFlying)
    {
        if (modeloPez == null) return;

        if (isFlying)
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 direction = velocity.normalized;
            float verticalness = Mathf.Abs(direction.y);

            if (verticalness > 0.9f && velocity.magnitude > 1f)
            {
                targetTiltX = verticalTiltX;
            }
            else
            {
                targetTiltX = horizontalTiltX;
            }
        }
        else
        {
            targetTiltX = horizontalTiltX;
        }

        Quaternion targetRotation = Quaternion.Euler(
            targetTiltX,
            modeloPez.localEulerAngles.y,
            modeloPez.localEulerAngles.z
        );

        modeloPez.localRotation = Quaternion.Slerp(
            modeloPez.localRotation,
            targetRotation,
            tiltSpeed * Time.deltaTime
        );
    }

    void HandleCameraEffects(float vertical, float horizontal)
    {
        if (cameraHolder == null) return;

        float tilt = -horizontal * cameraTiltAmount;

        cameraHolder.rotation = Quaternion.Euler(0, transform.eulerAngles.y, tilt);

        if (isGrounded && moveInput.magnitude > 0.1f && !Input.GetKey(KeyCode.Space))
        {
            float bobTimer = Time.time * cameraBobSpeed;
            float bobY = Mathf.Sin(bobTimer) * cameraBobAmount;
            cameraHolder.position += new Vector3(0, bobY, 0);
        }
    }

    void LateUpdate()
    {
        if (cameraHolder != null)
        {
            Vector3 targetOffset = isThirdPerson ? thirdPersonOffset : firstPersonOffset;
            cameraOffset = transform.TransformDirection(targetOffset);
            cameraHolder.position = transform.position + cameraOffset;
        }
    }

    void HandleWaterPhysics()
    {
        if (!isInWater) return;

        rb.linearVelocity *= (1 - waterDrag * Time.fixedDeltaTime);

        float tiempoEnAgua = Time.time - waterEntryTime;

        if (hasEnteredWater && tiempoEnAgua < 2f)
        {
            if (tiempoEnAgua < 1f)
            {
                float sinkForce = Mathf.Lerp(waterEntryVelocity * 0.5f, 2f, tiempoEnAgua);
                rb.AddForce(Vector3.down * sinkForce, ForceMode.Acceleration);
            }
            else
            {
                float transition = (tiempoEnAgua - 1f);
                float buoyancyForce = Mathf.Lerp(0f, waterBuoyancy, transition);

                float sinkForce = Mathf.Lerp(2f, 0f, transition);
                rb.AddForce(Vector3.down * sinkForce, ForceMode.Acceleration);
                rb.AddForce(Vector3.up * buoyancyForce, ForceMode.Acceleration);
            }
        }
        else
        {
            hasEnteredWater = false;

            float depth = transform.position.y - GetWaterSurface();

            if (depth < -1f)
            {
                rb.AddForce(Vector3.up * waterBuoyancy * 0.5f, ForceMode.Acceleration);
            }
            else if (depth > -0.5f)
            {
                if (rb.linearVelocity.y > 0.5f)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 0.95f, rb.linearVelocity.z);
                }
            }

            rb.AddForce(Vector3.up * waterBuoyancy * 0.3f, ForceMode.Acceleration);
        }
    }
    float GetWaterSurface()
    {
        return 0f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            isInWater = true;

            waterEntryVelocity = Mathf.Abs(rb.linearVelocity.y);
            waterEntryTime = Time.time;
            hasEnteredWater = true;

            Vector3 newVelocity = rb.linearVelocity;
            newVelocity.y *= 0.3f;
            rb.linearVelocity = newVelocity;

            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            horizontalVelocity *= 0.8f;
            rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);

            if (rb.linearVelocity.magnitude > waterEntryThreshold && splashEffect != null)
            {
                splashEffect.Play();
            }

            ActivarEfectoAgua(true);
            Debug.Log("Entró al agua - Velocidad vertical: " + waterEntryVelocity);
        }

    }
    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(enemyTag))
        {
            float damage = Random.Range(minDamage, maxDamage);
            currentArmor -= damage;
            currentArmor = Mathf.Max(currentArmor, 0);

            // Si la armadura llega a 0, morir
            if (currentArmor <= 0)
            {
                Die();
            }
            else
            {
                shakeTimer = shakeDuration;
                if (uiStatus != null) uiStatus.StartDamageFlash();
            }

            Debug.Log($"!Impacto enemigo! Daño: {damage}, Armadura restante: {currentArmor}");
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            isInWater = false;
            hasEnteredWater = false;
            ActivarEfectoAgua(false);
        }
    }


    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Water") && !isInWater)
        {
            isInWater = true;
            hasEnteredWater = true;
            waterEntryTime = Time.time;
            waterEntryVelocity = Mathf.Abs(rb.linearVelocity.y);
            ActivarEfectoAgua(true);
        }
    }


    void ActivarEfectoAgua(bool activar)
    {
        isUnderwater = activar;

        if (underwaterVolume != null)
        {

            StopAllCoroutines();

            StartCoroutine(TransicionEfectoAgua(activar));
        }
    }


    System.Collections.IEnumerator TransicionEfectoAgua(bool activar)
    {
        float tiempo = 0f;
        float pesoInicial = underwaterVolume.weight;
        float pesoFinal = activar ? 1f : 0f;

        while (tiempo < 1f)
        {
            tiempo += Time.deltaTime * underwaterTransitionSpeed;
            underwaterVolume.weight = Mathf.Lerp(pesoInicial, pesoFinal, tiempo);
            yield return null;
        }

        underwaterVolume.weight = pesoFinal;
    }

    void Die()
    {
        isDead = true;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Activar filtro rojo permanente
        if (uiStatus != null)
        {
            uiStatus.SetDeadState(true);
        }

        // Opcional: desactivar scripts de disparo
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null) shooting.enabled = false;

        Debug.Log("¡Has muerto!");
    }


}