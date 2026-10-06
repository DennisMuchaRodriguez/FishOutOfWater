using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Configuración")]
    public KeyCode shootKey = KeyCode.Mouse0;
    public GameObject projectilePrefab;
    public float shootForce = 20f;
    public float fireRate = 0.3f;
    [Tooltip("Mantener presionado para disparar en ráfaga")]
    public bool holdToFire = true;

    [Header("Punto de disparo")]
    public Transform firePoint;

    [Header("Referencias")]
    public Camera playerCamera;

    [Header("Opciones de puntería")]
    public float maxDistance = 500f;
    public LayerMask aimLayers = ~0;

    [Header("Precisión - NUEVO")]
    [Tooltip("Dispersión base en grados")]
    public float baseSpread = 0.25f;
    public float spreadPerShot = 0.6f;
    public float maxSpread = 3f;
    public float spreadRecovery = 5f;
    [Tooltip("Dispersión extra mientras vuelas")]
    public float airSpread = 1f;

    [Header("Disparo cargado - NUEVO")]
    public KeyCode chargeKey = KeyCode.Mouse1;
    public float chargeTime = 0.9f;
    public int chargedAmmoCost = 3;
    public float chargedDamageMultiplier = 3f;
    public float chargedScale = 2.2f;
    public float chargedSpeedMultiplier = 1.25f;

    [Header("Efectos - NUEVO")]
    public GameObject muzzleFlashPrefab;
    public GameObject impactEffectPrefab;
    public GameObject chargedImpactPrefab;
    public GameObject waterSplashPrefab;
    [Tooltip("Material de partículas usado como plantilla (ej. WaterJek)")]
    public Material fxMaterialTemplate;
    public Color shotColor = new Color(0.35f, 0.9f, 1f, 1f);
    public float recoilKick = 1.2f;
    public float chargedRecoilKick = 4f;

    [Header("Munición - NUEVO")]
    public int maxAmmo = 30;
    public int currentAmmo;
    public bool isInWaterForAmmo = false;
    [Tooltip("Si está activo, el agua también recarga balas. Por defecto solo las cápsulas de munición.")]
    public bool rechargeAmmoInWater = false;
    [Tooltip("Segundos por bala recargada dentro del agua")]
    public float ammoRechargeInterval = 0.2f;

    // ---- Estado público para el HUD ----
    public float ChargeProgress01 { get; private set; }
    public float CurrentSpread { get; private set; }
    public float LastShotTime { get; private set; } = -10f;
    public bool IsCharging { get; private set; }

    public event System.Action<bool> OnShot;          // bool = cargado
    public event System.Action<bool> OnTargetHit;     // bool = objetivo destruido
    public event System.Action<int> OnAmmoPickup;

    private float nextFireTime = 0f;
    private float ammoRechargeTimer = 0f;
    private float chargeStartTime;
    private float spreadHeat = 0f;
    private Light muzzleLight;
    private ParticleSystem muzzleSparks;
    private Material trailMaterial;
    private PlayerController_Base controller;

    void Start()
    {
        currentAmmo = maxAmmo;
        controller = GetComponent<PlayerController_Base>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            Debug.LogError("PlayerShooting: no hay cámara asignada");
        }

        BuildMuzzleEffects();
    }

    void BuildMuzzleEffects()
    {
        if (firePoint == null) return;

        // Luz del fogonazo
        GameObject lightGo = new GameObject("MuzzleLight");
        lightGo.transform.SetParent(firePoint, false);
        muzzleLight = lightGo.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = shotColor;
        muzzleLight.range = 6f;
        muzzleLight.intensity = 0f;
        muzzleLight.shadows = LightShadows.None;

        // Chispas de agua al disparar
        Material sparkMat = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, Color.white);
        muzzleSparks = FXFactory.CreateParticleSystem("MuzzleSparks", firePoint, sparkMat);
        var main = muzzleSparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(shotColor, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;
        var emission = muzzleSparks.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        var shape = muzzleSparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.02f;
        var sizeOverLife = muzzleSparks.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        muzzleSparks.Play();

        trailMaterial = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, Color.white);
    }

    void Update()
    {
        if (PauseMenu.IsPaused || PauseMenu.InputBlockedThisFrame) return;

        float dt = Time.deltaTime;

        // --- Dispersión ---
        spreadHeat = Mathf.MoveTowards(spreadHeat, 0f, spreadRecovery * dt);
        bool airborne = controller != null && controller.IsJetting;
        CurrentSpread = Mathf.Min(baseSpread + spreadHeat + (airborne ? airSpread : 0f), maxSpread);

        // --- Disparo normal ---
        bool wantsFire = holdToFire ? Input.GetKey(shootKey) : Input.GetKeyDown(shootKey);
        if (wantsFire && !IsCharging && Time.time >= nextFireTime && currentAmmo > 0)
        {
            Shoot(false);
            nextFireTime = Time.time + fireRate;
            currentAmmo -= 1;
        }

        // --- Disparo cargado ---
        if (Input.GetKeyDown(chargeKey) && currentAmmo >= chargedAmmoCost)
        {
            IsCharging = true;
            chargeStartTime = Time.time;
        }
        if (IsCharging)
        {
            ChargeProgress01 = Mathf.Clamp01((Time.time - chargeStartTime) / chargeTime);

            if (muzzleLight != null)
            {
                muzzleLight.intensity = Mathf.Max(muzzleLight.intensity, ChargeProgress01 * 6f + Mathf.Sin(Time.time * 40f) * ChargeProgress01);
            }
            if (muzzleSparks != null && Random.value < ChargeProgress01 * 0.5f)
            {
                muzzleSparks.Emit(1);
            }

            if (Input.GetKeyUp(chargeKey))
            {
                if (ChargeProgress01 >= 1f && currentAmmo >= chargedAmmoCost)
                {
                    Shoot(true);
                    currentAmmo -= chargedAmmoCost;
                    nextFireTime = Time.time + fireRate;
                }
                IsCharging = false;
                ChargeProgress01 = 0f;
            }
        }

        // --- Fogonazo ---
        if (muzzleLight != null)
        {
            muzzleLight.intensity = Mathf.MoveTowards(muzzleLight.intensity, 0f, dt * 80f);
        }

        // --- Recarga de munición en agua ---
        if (rechargeAmmoInWater && isInWaterForAmmo && currentAmmo < maxAmmo)
        {
            ammoRechargeTimer += dt;
            while (ammoRechargeTimer >= ammoRechargeInterval && currentAmmo < maxAmmo)
            {
                ammoRechargeTimer -= ammoRechargeInterval;
                currentAmmo++;
            }
        }
        else
        {
            ammoRechargeTimer = 0f;
        }
    }

    void OnDisable()
    {
        IsCharging = false;
        ChargeProgress01 = 0f;
    }

    // Devuelve cuántas balas se agregaron realmente
    public int AddAmmo(int amount)
    {
        int before = currentAmmo;
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
        int added = currentAmmo - before;
        if (added > 0) OnAmmoPickup?.Invoke(added);
        return added;
    }

    public void UpdateWaterState(bool inWater)
    {
        isInWaterForAmmo = inWater;
    }

    void Shoot(bool charged)
    {
        if (projectilePrefab == null || firePoint == null || playerCamera == null)
        {
            Debug.LogError("PlayerShooting: falta el prefab, el FirePoint o la cámara");
            return;
        }

        // PASO 1: Calcular dónde apunta el mouse en el mundo
        Vector3 targetPoint = GetAimTarget();

        // PASO 2: Dirección desde firePoint hacia el objetivo, con un poco de dispersión
        Vector3 direction = (targetPoint - firePoint.position).normalized;
        float spread = charged ? 0f : CurrentSpread;
        if (spread > 0f)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            direction = Quaternion.AngleAxis(r.x, Vector3.up) * Quaternion.AngleAxis(r.y, Vector3.Cross(direction, Vector3.up).normalized) * direction;
        }

        // PASO 3: Crear proyectil
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        if (charged) projectile.transform.localScale *= chargedScale;

        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            int dmg = charged ? Mathf.RoundToInt(proj.damage * chargedDamageMultiplier) : proj.damage;
            proj.Setup(this, dmg, charged ? chargedImpactPrefab : impactEffectPrefab, waterSplashPrefab,
                       shotColor, trailMaterial, charged);
        }

        // PASO 4: Darle velocidad en esa dirección
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = direction * shootForce * (charged ? chargedSpeedMultiplier : 1f);
        }

        // Ignorar colisión con el jugador
        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(projectileCollider, c);
            }
        }

        // Efectos del disparo
        FXFactory.SpawnOneShot(muzzleFlashPrefab, firePoint.position, Quaternion.LookRotation(direction), charged ? 0.8f : 0.35f, 2f);
        if (muzzleLight != null) muzzleLight.intensity = charged ? 14f : 7f;
        if (muzzleSparks != null) muzzleSparks.Emit(charged ? 30 : 8);
        if (controller != null) controller.AddRecoil(charged ? chargedRecoilKick : recoilKick, charged ? 1f : 0.25f);

        spreadHeat = Mathf.Min(spreadHeat + spreadPerShot, maxSpread);
        LastShotTime = Time.time;
        OnShot?.Invoke(charged);

        Debug.DrawLine(firePoint.position, targetPoint, Color.red, 1f);
    }

    public void NotifyHit(bool killed)
    {
        OnTargetHit?.Invoke(killed);
    }

    Vector3 GetAimTarget()
    {
        // Rayo desde la cámara hacia donde está el mouse (ignorando triggers y al propio jugador)
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, aimLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 point = ray.GetPoint(maxDistance);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                point = h.point;
            }
        }
        return point;
    }
}
