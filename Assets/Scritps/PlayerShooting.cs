using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Configuración")]
    public KeyCode shootKey = KeyCode.Mouse0;
    public GameObject projectilePrefab;
    [Tooltip("Velocidad de la bala en m/s (más alto = más recto y llega antes a donde apuntas)")]
    public float shootForce = 150f;
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
    public float baseSpread = 0.1f;
    public float spreadPerShot = 0.25f;
    public float maxSpread = 1.2f;
    public float spreadRecovery = 8f;
    [Tooltip("Dispersión extra mientras vuelas")]
    public float airSpread = 0.2f;

    [Header("Disparo recto y asistido")]
    [Tooltip("Suma a la bala la velocidad con la que avanzas, así nunca se queda atrás ni parece que cae")]
    public bool inheritForwardSpeed = true;
    [Tooltip("Grados alrededor de la mira en los que el disparo busca un ave (0 = sin ayuda)")]
    [Range(0f, 10f)] public float aimAssistAngle = 4f;
    [Tooltip("Cuánto corrige la puntería hacia esa ave (0 = nada, 1 = justo a donde va a estar)")]
    [Range(0f, 1f)] public float aimAssistStrength = 0.9f;
    [Tooltip("Apunta adelante del ave según su velocidad, para que la bala llegue a donde VA a estar")]
    public bool leadTargets = true;
    [Tooltip("La dispersión se multiplica por esto mientras la mira tiene un ave fijada")]
    [Range(0f, 1f)] public float lockedSpreadFactor = 0.3f;

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
    // Ave que la mira tiene fijada ahora mismo (null = ninguna): el HUD cierra la retícula sobre ella
    public BirdAI AimTarget { get; private set; }
    [Header("Metralleta de burbujas (mejora del Taller)")]
    [Tooltip("Disparos por segundo de la metralleta")]
    public float bubbleShotsPerSecond = 10f;
    [Tooltip("Daño de cada burbuja comparado con la bala de la pistola")]
    public float bubbleDamageFactor = 0.4f;
    public float bubbleSpeedMultiplier = 1.35f;
    public float bubbleScale = 0.55f;
    [Tooltip("Cuando el tanque de burbujas está vacío, burbujas que salen por cada bala del cargador")]
    public int bubblesPerAmmo = 8;
    [Tooltip("Burbujas que caben en el tanque propio de la metralleta (se gasta antes que las balas)")]
    public int bubbleCapacity = 240;
    [Tooltip("Burbujas por segundo que recarga el tanque dentro del agua")]
    public float bubbleWaterRefill = 30f;
    [Tooltip("Burbujas que da cada bala de una cápsula de munición")]
    public int bubblesPerPickupAmmo = 8;
    [Tooltip("Teclas para cambiar de arma principal (también la rueda del mouse)")]
    public KeyCode pistolKey = KeyCode.Alpha1;
    public KeyCode bubbleKey = KeyCode.Alpha2;

    // Lo ajustan las mejoras (PlayerUpgrades): aves que atraviesa el disparo cargado
    [HideInInspector] public int chargedPierce = 0;

    // CONTRATO para el HUD: nombre del arma principal activa y si es la metralleta de burbujas
    public string CurrentWeaponName { get { return usingBubbleGun ? "METRALLETA DE BURBUJAS" : "PISTOLA DE AGUA"; } }
    public bool IsBubbleGun { get { return usingBubbleGun; } }
    public bool HasBubbleGun { get { return bubbleUnlocked; } }
    // Burbujas en el tanque de la metralleta
    public int CurrentBubbles { get; private set; }
    // Lo que muestra el HUD: con la metralleta, el tanque de burbujas (si se vació, las balas que está gastando)
    public bool ShowingBubbles { get { return usingBubbleGun && CurrentBubbles > 0; } }
    public int HudAmmo { get { return ShowingBubbles ? CurrentBubbles : currentAmmo; } }
    public int HudMaxAmmo { get { return ShowingBubbles ? Mathf.Max(1, bubbleCapacity) : maxAmmo; } }
    // ¿El arma elegida puede disparar? (la metralleta usa su tanque y, si se vació, las balas)
    public bool CanFireSelected { get { return usingBubbleGun ? (CurrentBubbles > 0 || currentAmmo > 0) : currentAmmo > 0; } }
    // ¿Le sirve una cápsula? (balas que faltan o, con la metralleta, burbujas que faltan)
    public bool WantsAmmo { get { return currentAmmo < maxAmmo || (bubbleUnlocked && CurrentBubbles < bubbleCapacity); } }
    public event System.Action<string> OnWeaponChanged;

    bool bubbleUnlocked;
    bool usingBubbleGun;
    float bubbleDamageBonus = 1f;
    int bubbleShotsSinceAmmo;
    float bubbleRefillAcc;
    Material bubbleMaterial, bubbleGlowMaterial;
    ParticleSystem bubbleTrail;

    public event System.Action<bool> OnShot;          // bool = cargado
    public event System.Action<bool> OnTargetHit;     // bool = objetivo destruido
    public event System.Action<int> OnAmmoPickup;
    // Cada disparo del jugador (origen, dirección y velocidad): las aves lo usan para esquivar
    public static event System.Action<Vector3, Vector3, float> ShotFired;

    // Hacia dónde apunta ahora el jugador (rayo desde la cámara por el puntero): las aves notan que las siguen
    public Ray AimRay
    {
        get
        {
            if (playerCamera == null) return new Ray(transform.position, transform.forward);
            return playerCamera.ScreenPointToRay(Input.mousePosition);
        }
    }

    private float nextFireTime = 0f;
    private float ammoRechargeTimer = 0f;
    private float chargeStartTime;
    private float spreadHeat = 0f;
    private Light muzzleLight;
    private ParticleSystem muzzleSparks;
    private Material trailMaterial;
    private PlayerController_Base controller;
    private Rigidbody body;

    void Start()
    {
        currentAmmo = maxAmmo;
        controller = GetComponent<PlayerController_Base>();
        body = GetComponent<Rigidbody>();

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

        // --- Ave bajo la mira (para la ayuda de puntería y la retícula) ---
        AimTarget = playerCamera != null && aimAssistAngle > 0f && aimAssistStrength > 0f ? FindAssistTarget(AimRay) : null;
        if (AimTarget != null) CurrentSpread *= lockedSpreadFactor;

        // --- Cambio de arma principal (1 / 2 o la rueda del mouse) ---
        if (bubbleUnlocked && !IsCharging && (controller == null || !controller.isDead))
        {
            if (Input.GetKeyDown(pistolKey)) SelectWeapon(false);
            else if (Input.GetKeyDown(bubbleKey)) SelectWeapon(true);
            else if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) SelectWeapon(!usingBubbleGun);
        }

        // --- Disparo normal ---
        bool wantsFire = (holdToFire || usingBubbleGun) ? Input.GetKey(shootKey) : Input.GetKeyDown(shootKey);
        bool hasShot = usingBubbleGun ? (CurrentBubbles > 0 || currentAmmo > 0) : currentAmmo > 0;
        if (wantsFire && !IsCharging && Time.time >= nextFireTime && hasShot)
        {
            if (usingBubbleGun)
            {
                // Metralleta: 10 burbujas por segundo. Primero gasta su propio tanque de burbujas;
                // vacío, cada bubblesPerAmmo burbujas gastan una bala del cargador
                Shoot(false, true);
                nextFireTime = Time.time + 1f / Mathf.Max(1f, bubbleShotsPerSecond);
                if (CurrentBubbles > 0)
                {
                    CurrentBubbles--;
                }
                else
                {
                    bubbleShotsSinceAmmo++;
                    if (bubbleShotsSinceAmmo >= Mathf.Max(1, bubblesPerAmmo))
                    {
                        bubbleShotsSinceAmmo = 0;
                        currentAmmo -= 1;
                    }
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

        // --- El tanque de burbujas se rellena solo dentro del agua ---
        if (bubbleUnlocked && isInWaterForAmmo && CurrentBubbles < bubbleCapacity)
        {
            bubbleRefillAcc += bubbleWaterRefill * dt;
            int add = Mathf.FloorToInt(bubbleRefillAcc);
            if (add > 0)
            {
                bubbleRefillAcc -= add;
                CurrentBubbles = Mathf.Min(bubbleCapacity, CurrentBubbles + add);
            }
        }
        else
        {
            bubbleRefillAcc = 0f;
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
        // Las cápsulas también llenan bastante el tanque de burbujas
        int bubblesBefore = CurrentBubbles;
        if (bubbleUnlocked && amount > 0) CurrentBubbles = Mathf.Min(bubbleCapacity, CurrentBubbles + amount * Mathf.Max(0, bubblesPerPickupAmmo));
        if (added > 0 || CurrentBubbles > bubblesBefore) OnAmmoPickup?.Invoke(Mathf.Max(added, 1));
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
        if (unlocked && !was) CurrentBubbles = bubbleCapacity;   // recién instalada: tanque lleno
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
        // Halo de energía (aditivo) alrededor de cada burbuja
        if (bubbleGlowMaterial == null)
            bubbleGlowMaterial = FXFactory.ParticleMaterial(fxMaterialTemplate, FXFactory.SoftDot, new Color(0.45f, 0.95f, 1f, 1f));
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

    // Lo que hace falta para disparar burbujas de energía (los peces defensores con metralleta lo comparten)
    public void GetBubbleFX(out Material bubble, out ParticleSystem trail, out Material glow)
    {
        EnsureBubbleFX();
        bubble = bubbleMaterial;
        trail = bubbleTrail;
        glow = bubbleGlowMaterial;
    }

    void Shoot(bool charged, bool bubble = false)
    {
        if (projectilePrefab == null || firePoint == null || playerCamera == null)
        {
            Debug.LogError("PlayerShooting: falta el prefab, el FirePoint o la cámara");
            return;
        }

        // PASO 1: Velocidad de la bala (más la que ya lleva el pez hacia adelante, para que nunca se quede atrás)
        float shotSpeed = shootForce * (charged ? chargedSpeedMultiplier : (bubble ? bubbleSpeedMultiplier : 1f));
        Ray aim = AimRay;
        if (inheritForwardSpeed && body != null) shotSpeed += Mathf.Max(0f, Vector3.Dot(body.linearVelocity, aim.direction));

        // PASO 2: Dirección hacia lo que hay bajo la mira (o hacia donde va a estar el ave fijada), con poca dispersión
        BirdAI locked;
        Vector3 direction = GetAimDirection(aim, shotSpeed, out locked);
        float spread = charged || bubble ? 0f : CurrentSpread;   // la metralleta y el cargado van rectos
        if (spread > 0f)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            direction = Quaternion.AngleAxis(r.x, Vector3.up) * Quaternion.AngleAxis(r.y, right.normalized) * direction;
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
            if (bubble) proj.MakeBubble(bubbleMaterial, bubbleTrail, bubbleGlowMaterial);
            if (charged && chargedPierce > 0) proj.pierceCount = chargedPierce;
        }

        // PASO 4: Darle velocidad en esa dirección (en línea recta: sin gravedad)
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = direction * shotSpeed;
        }
        ShotFired?.Invoke(firePoint.position, direction, shotSpeed);

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

        Debug.DrawRay(firePoint.position, direction * 60f, locked != null ? Color.yellow : Color.red, 1f);
    }

    // Cooperativo: copia de un disparo de otro jugador (o de un defensor del anfitrión). Solo se ve: no hace daño
    public Projectile SpawnReplica(Vector3 position, Vector3 velocity, bool charged, bool bubble, Color color, float scale)
    {
        if (projectilePrefab == null) return null;
        Vector3 dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.forward;
        GameObject projectile = Instantiate(projectilePrefab, position, Quaternion.LookRotation(dir));
        if (scale > 0.01f) projectile.transform.localScale = Vector3.one * scale;
        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.IsReplica = true;
            proj.Setup(null, 0, charged ? chargedImpactPrefab : impactEffectPrefab, color, trailMaterial, charged);
            if (bubble) proj.MakeBubble(bubbleMaterial, bubbleTrail, bubbleGlowMaterial);
        }
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.linearVelocity = velocity;
        }
        // No choca con el pez de este jugador
        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
            foreach (Collider c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(projectileCollider, c);
        if (!bubble) Sfx.Play(Sonido.Disparo, position, charged ? 0.6f : 0.35f, charged ? 0.8f : 1.1f, 8f, 0.08f, 0.05f);
        return proj;
    }

    public void NotifyHit(bool killed)
    {
        OnTargetHit?.Invoke(killed);
    }

    // ======================= PUNTERÍA =======================

    // Dirección de la bala desde el punto de disparo: hacia lo que hay bajo la mira y, si cerca de la mira hay un
    // ave, hacia donde va a estar cuando la bala llegue (según aimAssistStrength)
    Vector3 GetAimDirection(Ray aim, float shotSpeed, out BirdAI locked)
    {
        Vector3 origin = firePoint.position;
        Vector3 dir = AimPoint(aim) - origin;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : aim.direction;

        locked = aimAssistAngle > 0f && aimAssistStrength > 0f ? FindAssistTarget(aim) : null;
        if (locked != null)
        {
            Vector3 target = leadTargets ? PredictPosition(locked, origin, shotSpeed) : locked.transform.position;
            Vector3 toTarget = target - origin;
            if (toTarget.sqrMagnitude > 0.01f) dir = Vector3.Slerp(dir, toTarget.normalized, aimAssistStrength);
        }
        return dir;
    }

    // Lo que hay bajo la mira (ignora triggers, al propio jugador y lo que queda entre la cámara y el pez:
    // en tercera persona eso hacía que la bala saliera torcida)
    Vector3 AimPoint(Ray ray)
    {
        float minDist = Mathf.Max(0f, Vector3.Dot(firePoint.position - ray.origin, ray.direction)) + 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, aimLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 point = ray.GetPoint(maxDistance);
        foreach (RaycastHit h in hits)
        {
            if (h.distance < minDist || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                point = h.point;
            }
        }
        return point;
    }

    // El ave viva más cerca de la mira dentro de aimAssistAngle (más un margen por su tamaño) y a la vista
    BirdAI FindAssistTarget(Ray ray)
    {
        BirdAI best = null;
        float bestScore = float.MaxValue;
        float maxSqr = maxDistance * maxDistance;
        for (int i = 0; i < BirdAI.All.Count; i++)
        {
            BirdAI b = BirdAI.All[i];
            if (b == null || !b.IsAlive) continue;
            Vector3 to = b.transform.position - ray.origin;
            float sqr = to.sqrMagnitude;
            if (sqr > maxSqr || sqr < 1f || Vector3.Dot(to, ray.direction) <= 0f) continue;
            float dist = Mathf.Sqrt(sqr);
            float limit = aimAssistAngle + Mathf.Atan2(1.5f, dist) * Mathf.Rad2Deg;
            float angle = Vector3.Angle(ray.direction, to);
            if (angle > limit) continue;
            float score = angle / limit + dist / maxDistance * 0.15f;   // la más centrada; a igualdad, la más cercana
            if (score < bestScore)
            {
                bestScore = score;
                best = b;
            }
        }
        if (best != null && !InSight(ray.origin, best)) best = null;
        return best;
    }

    // Que no haya un árbol, una roca o el suelo entre la cámara y el ave
    bool InSight(Vector3 from, BirdAI bird)
    {
        Vector3 to = bird.transform.position - from;
        float dist = to.magnitude;
        if (dist < 0.1f) return true;
        RaycastHit[] hits = Physics.RaycastAll(from, to / dist, dist, aimLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(bird.transform)) continue;
            if (h.distance < dist - 2f) return false;
        }
        return true;
    }

    // Dónde va a estar el ave cuando llegue una bala a esta velocidad (intercepción con su velocidad actual)
    static Vector3 PredictPosition(BirdAI bird, Vector3 origin, float speed)
    {
        Vector3 pos = bird.transform.position;
        Rigidbody brb = bird.GetComponent<Rigidbody>();
        Vector3 v = brb != null ? brb.linearVelocity : Vector3.zero;
        Vector3 d = pos - origin;
        float a = Vector3.Dot(v, v) - speed * speed;
        float bq = 2f * Vector3.Dot(d, v);
        float c = Vector3.Dot(d, d);
        float t;
        if (Mathf.Abs(a) < 0.001f)
        {
            t = bq < 0f ? -c / bq : d.magnitude / Mathf.Max(1f, speed);
        }
        else
        {
            float disc = bq * bq - 4f * a * c;
            if (disc < 0f) t = d.magnitude / Mathf.Max(1f, speed);
            else
            {
                float sq = Mathf.Sqrt(disc);
                float t1 = (-bq - sq) / (2f * a);
                float t2 = (-bq + sq) / (2f * a);
                t = Mathf.Min(t1, t2);
                if (t < 0f) t = Mathf.Max(t1, t2);
                if (t < 0f) t = d.magnitude / Mathf.Max(1f, speed);
            }
        }
        return pos + v * Mathf.Clamp(t, 0f, 1.5f);
    }
}
