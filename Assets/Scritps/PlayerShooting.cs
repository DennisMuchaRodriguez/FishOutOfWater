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
    [Header("Metralleta de burbujas (mejora del Taller)")]
    [Tooltip("Disparos por segundo de la metralleta")]
    public float bubbleShotsPerSecond = 10f;
    [Tooltip("Daño de cada burbuja comparado con la bala de la pistola")]
    public float bubbleDamageFactor = 0.4f;
    public float bubbleSpeedMultiplier = 1.35f;
    public float bubbleScale = 0.55f;
    [Tooltip("Burbujas que salen por cada bala del cargador")]
    public int bubblesPerAmmo = 3;
    [Tooltip("Teclas para cambiar de arma principal (también la rueda del mouse)")]
    public KeyCode pistolKey = KeyCode.Alpha1;
    public KeyCode bubbleKey = KeyCode.Alpha2;

    // Lo ajustan las mejoras (PlayerUpgrades): aves que atraviesa el disparo cargado
    [HideInInspector] public int chargedPierce = 0;

    // CONTRATO para el HUD: nombre del arma principal activa y si es la metralleta de burbujas
    public string CurrentWeaponName { get { return usingBubbleGun ? "METRALLETA DE BURBUJAS" : "PISTOLA DE AGUA"; } }
    public bool IsBubbleGun { get { return usingBubbleGun; } }
    public bool HasBubbleGun { get { return bubbleUnlocked; } }
    public event System.Action<string> OnWeaponChanged;

    bool bubbleUnlocked;
    bool usingBubbleGun;
    float bubbleDamageBonus = 1f;
    int bubbleShotsSinceAmmo;
    Material bubbleMaterial;
    ParticleSystem bubbleTrail;

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
        if (PauseMenu.IsPaused || PauseMenu.InputBlockedThisFrame || GameDirector.InCinematic) return;

        float dt = Time.deltaTime;

        // --- Dispersión ---
        spreadHeat = Mathf.MoveTowards(spreadHeat, 0f, spreadRecovery * dt);
        bool airborne = controller != null && controller.IsJetting;
        CurrentSpread = Mathf.Min(baseSpread + spreadHeat + (airborne ? airSpread : 0f), maxSpread);

        // --- Cambio de arma principal (1 / 2 o la rueda del mouse) ---
        if (bubbleUnlocked && !IsCharging && (controller == null || !controller.isDead))
        {
            if (Input.GetKeyDown(pistolKey)) SelectWeapon(false);
            else if (Input.GetKeyDown(bubbleKey)) SelectWeapon(true);
            else if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) SelectWeapon(!usingBubbleGun);
        }

        // --- Disparo normal ---
        bool wantsFire = (holdToFire || usingBubbleGun) ? Input.GetKey(shootKey) : Input.GetKeyDown(shootKey);
        if (wantsFire && !IsCharging && Time.time >= nextFireTime && currentAmmo > 0)
        {
            if (usingBubbleGun)
            {
                // Metralleta: 10 burbujas por segundo; cada 3 burbujas gastan una bala
                Shoot(false, true);
                nextFireTime = Time.time + 1f / Mathf.Max(1f, bubbleShotsPerSecond);
                bubbleShotsSinceAmmo++;
                if (bubbleShotsSinceAmmo >= Mathf.Max(1, bubblesPerAmmo))
                {
                    bubbleShotsSinceAmmo = 0;
                    currentAmmo -= 1;
                }
            }
            else
            {
                Shoot(false);
                nextFireTime = Time.time + fireRate;
                currentAmmo -= 1;
            }
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

    // ---- Metralleta de burbujas (la desbloquea PlayerUpgrades) ----

    public void SetBubbleGun(bool unlocked, float damageBonus)
    {
        bool was = bubbleUnlocked;
        bubbleUnlocked = unlocked;
        bubbleDamageBonus = Mathf.Max(0.1f, damageBonus);
        if (unlocked && !was) SelectWeapon(true);      // recién instalada: se estrena
        if (!unlocked && usingBubbleGun) SelectWeapon(false);
    }

    void SelectWeapon(bool bubble)
    {
        bubble = bubble && bubbleUnlocked;
        if (bubble == usingBubbleGun) return;
        usingBubbleGun = bubble;
        bubbleShotsSinceAmmo = 0;
        if (bubble) EnsureBubbleFX();
        OnWeaponChanged?.Invoke(CurrentWeaponName);
    }

    // Material de la burbuja y la estela de burbujitas (una sola para todas las balas)
    void EnsureBubbleFX()
    {
        if (bubbleMaterial == null)
            bubbleMaterial = FXFactory.ParticleMaterialAlpha(fxMaterialTemplate, FXFactory.Bubble, new Color(0.85f, 0.97f, 1f, 0.95f));
        if (bubbleTrail == null)
        {
            Material mat = FXFactory.ParticleMaterialAlpha(fxMaterialTemplate, FXFactory.Bubble, new Color(0.8f, 0.96f, 1f, 0.8f));
            bubbleTrail = FXFactory.CreateParticleSystem("EstelaBurbujas", null, mat);
            var main = bubbleTrail.main;
            main.maxParticles = 800;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startSpeed = 0f;
            main.gravityModifier = -0.06f;
            var size = bubbleTrail.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            bubbleTrail.Play();
        }
    }

    void OnDestroy()
    {
        if (bubbleTrail != null) Destroy(bubbleTrail.gameObject);
    }

    void Shoot(bool charged, bool bubble = false)
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
        float spread = charged || bubble ? 0f : CurrentSpread;   // la metralleta dispara recto
        if (spread > 0f)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            direction = Quaternion.AngleAxis(r.x, Vector3.up) * Quaternion.AngleAxis(r.y, Vector3.Cross(direction, Vector3.up).normalized) * direction;
        }

        // PASO 3: Crear proyectil
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        if (charged) projectile.transform.localScale *= chargedScale;
        else if (bubble) projectile.transform.localScale *= bubbleScale;

        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            int dmg = charged ? Mathf.RoundToInt(proj.damage * chargedDamageMultiplier) : proj.damage;
            if (bubble) dmg = Mathf.Max(1, Mathf.RoundToInt(proj.damage * bubbleDamageFactor * bubbleDamageBonus));
            proj.Setup(this, dmg, charged ? chargedImpactPrefab : impactEffectPrefab,
                       shotColor, trailMaterial, charged);
            if (bubble) proj.MakeBubble(bubbleMaterial, bubbleTrail);
            if (charged && chargedPierce > 0) proj.pierceCount = chargedPierce;
        }

        // PASO 4: Darle velocidad en esa dirección
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = direction * shootForce * (charged ? chargedSpeedMultiplier : (bubble ? bubbleSpeedMultiplier : 1f));
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
        FXFactory.SpawnOneShot(muzzleFlashPrefab, firePoint.position, Quaternion.LookRotation(direction), charged ? 0.8f : (bubble ? 0.18f : 0.35f), 2f);
        if (muzzleLight != null) muzzleLight.intensity = charged ? 14f : (bubble ? 3f : 7f);
        if (muzzleSparks != null) muzzleSparks.Emit(charged ? 30 : (bubble ? 2 : 8));
        if (controller != null) controller.AddRecoil(charged ? chargedRecoilKick : (bubble ? recoilKick * 0.3f : recoilKick), charged ? 1f : 0.25f);

        if (!bubble) spreadHeat = Mathf.Min(spreadHeat + spreadPerShot, maxSpread);
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
