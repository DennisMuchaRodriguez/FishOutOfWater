using UnityEngine;

// Supervivencia del pez fuera del agua y límites de la zona (parte de PlayerController_Base):
//  - CHOQUES: chocar a gran velocidad contra el suelo, un árbol o una roca quita armadura; cuanto más fuerte el golpe,
//    más quita (y rebota). Bajo el agua el golpe se amortigua.
//  - VARADO: en tierra, fuera del agua y sin carga para encender el propulsor, a los 4 s empieza a asfixiarse y
//    pierde armadura poco a poco (cada vez más rápido). Al volver al agua se acaba.
//  - ARRASTRARSE: varado, cada pulsación de ESPACIO da un coletazo que lo empuja un poco hacia adelante (hay que
//    pulsar varias veces para llegar al agua). Varado, caminar es muy lento: el traje sin agua apenas responde.
//  - LÍMITE DE LA ZONA: si se aleja demasiado del lago, el casco avisa; si cruza el límite, el traje hace solo un
//    giro en U (sube, da la vuelta y vuelve hacia el lago), como las naves de los juegos de combate aéreo.
public partial class PlayerController_Base
{
    [Header("Choques - NUEVO")]
    [Tooltip("Velocidad de choque (m/s, contra la superficie) a partir de la cual el golpe hace daño")]
    public float safeImpactSpeed = 13f;
    [Tooltip("Velocidad de choque con la que se pierde el máximo de armadura de un golpe")]
    public float severeImpactSpeed = 38f;
    [Tooltip("Fracción de la armadura máxima que quita el choque más fuerte")]
    [Range(0f, 1f)] public float maxImpactDamage = 0.6f;

    [Header("Fuera del agua - NUEVO")]
    [Tooltip("Segundos varado (en tierra y sin carga para el propulsor) antes de empezar a asfixiarse")]
    public float strandedGrace = 4f;
    [Tooltip("Armadura por segundo que pierde al empezar a asfixiarse")]
    public float suffocationRate = 3.5f;
    [Tooltip("Armadura por segundo que pierde después de un rato asfixiándose")]
    public float suffocationRateMax = 9f;
    [Tooltip("Segundos asfixiándose hasta llegar al máximo")]
    public float suffocationRamp = 8f;
    [Tooltip("Varado, caminar es así de lento (el traje sin agua apenas responde)")]
    [Range(0.05f, 1f)] public float strandedWalkFactor = 0.35f;
    [Tooltip("Coletazo al arrastrarse: velocidad hacia adelante y hacia arriba")]
    public float flopForward = 4.5f;
    public float flopUp = 4f;
    [Tooltip("Espera mínima entre coletazos")]
    public float flopCooldown = 0.28f;

    [Header("Límite de la zona - NUEVO")]
    [Tooltip("Hasta cuántos metros más allá de la orilla del lago se puede ir")]
    public float boundaryMargin = 170f;
    [Tooltip("El casco avisa desde estos metros antes del límite")]
    public float boundaryWarnBand = 30f;
    [Tooltip("Duración del giro en U automático (s)")]
    public float uTurnDuration = 1.3f;
    [Tooltip("Altura que gana en el giro (m/s hacia arriba en la primera mitad)")]
    public float uTurnRise = 7f;

    // ---- Estado público para el HUD ----
    public bool IsStranded { get; private set; }
    public float StrandedTime { get; private set; }
    public bool IsSuffocating { get { return IsStranded && StrandedTime > strandedGrace; } }
    public bool BoundaryWarning { get; private set; }
    public bool IsTurningBack { get { return uTurnTimer > 0f; } }

    bool flopRequested;
    float nextFlopTime;
    float flopTimer;
    float flopSide = 1f;
    float suffocateTick;
    float nextCrashTime;
    float lastEnemyKnockTime = -10f;
    float uTurnTimer;
    float uTurnFromYaw, uTurnToYaw, uTurnSpeed;
    float nextUTurnTime;
    bool strandedWarned;

    // ======================= CHOQUES =======================

    void CheckCrash(Collision collision)
    {
        if (isDead || InputLocked || rb == null || Time.time < nextCrashTime) return;
        // Solo el escenario (suelo, árboles, rocas): aves, peces y cápsulas tienen su propio cuerpo físico
        if (collision.rigidbody != null || collision.collider.isTrigger) return;
        if (collision.contactCount == 0) return;

        ContactPoint cp = collision.GetContact(0);
        float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, cp.normal));
        if (isInWater) impact *= 0.4f;                 // el agua amortigua (fondo del lago)
        if (impact < safeImpactSpeed) return;

        float k = Mathf.Clamp01((impact - safeImpactSpeed) / Mathf.Max(1f, severeImpactSpeed - safeImpactSpeed));
        float damage = maxArmor * maxImpactDamage * Mathf.Pow(k, 1.2f) + 2f;
        // Si un ave te acaba de lanzar, la caída cuenta la mitad (ya pagaste el golpe del ave)
        if (Time.time - lastEnemyKnockTime < 2f) damage *= 0.5f;
        nextCrashTime = Time.time + 0.5f;

        // Rebote: pierde casi toda la velocidad y sale despedido un poco hacia afuera de la superficie
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = v * 0.35f + cp.normal * Mathf.Min(impact * 0.22f, 6f);
        knockbackEndTime = Time.time + 0.3f;
        AddRecoil(3f + 6f * k, 0.5f + k);
        Sfx.Play(Sonido.Pisoton, cp.point, Mathf.Lerp(0.45f, 1f, k), Mathf.Lerp(1.35f, 0.95f, k), 8f, 0.05f, 0.15f);

        int pct = Mathf.RoundToInt(damage / Mathf.Max(1f, maxArmor) * 100f);
        TakeEnvironmentDamage(damage);
        if (!isDead && pct >= 4)
        {
            GameDirector gd = GameDirector.Instance;
            if (gd != null) gd.ShowToast("¡CHOQUE!  TRAJE −" + pct + "%", GameDirector.Danger);
        }
    }

    // Daño del entorno (choques, asfixia): no lo frena el escudo de burbuja ni empuja
    void TakeEnvironmentDamage(float damage)
    {
        if (isDead || damage <= 0f) return;
        currentArmor = Mathf.Max(currentArmor - damage, 0f);
        OnDamaged?.Invoke(damage);
        shakeTimer = shakeDuration;
        if (uiStatus != null) uiStatus.StartDamageFlash();
        if (currentArmor <= 0f) Die();
    }

    // ======================= VARADO Y ARRASTRARSE =======================

    // Varado = fuera del agua, sin el propulsor encendido y sin carga para encenderlo
    bool CannotFly { get { return currentJetpackEnergy < jetpackIgnitionCost; } }

    void UpdateStranded(float dt)
    {
        bool stranded = !isInWater && !IsJetting && CannotFly && !InputLocked && !isDead;
        if (!stranded)
        {
            if (IsStranded && isInWater && StrandedTime > strandedGrace)
            {
                GameDirector gd = GameDirector.Instance;
                if (gd != null) gd.ShowToast("¡AGUA!  EL TRAJE RESPIRA DE NUEVO", GameDirector.Cyan);
            }
            IsStranded = false;
            StrandedTime = 0f;
            suffocateTick = 0f;
            strandedWarned = false;
            return;
        }

        IsStranded = true;
        StrandedTime += dt;
        if (!strandedWarned && isGrounded)
        {
            strandedWarned = true;
            GameDirector gd = GameDirector.Instance;
            if (gd != null) gd.ShowToast("SIN AGUA PARA EL PROPULSOR  //  ARRÁSTRATE AL LAGO CON [ESPACIO]", GameDirector.Danger);
        }
        if (StrandedTime <= strandedGrace) return;

        // Asfixia: pierde armadura a golpes de un segundo (cada golpe se nota en el casco), cada vez más rápido
        suffocateTick += dt;
        if (suffocateTick >= 1f)
        {
            suffocateTick -= 1f;
            float t = Mathf.Clamp01((StrandedTime - strandedGrace) / Mathf.Max(0.1f, suffocationRamp));
            float rate = Mathf.Lerp(suffocationRate, suffocationRateMax, t);
            Sfx.Play2D(Sonido.Aviso, 0.5f, Mathf.Lerp(1f, 1.3f, t), 0f, 0.5f);
            TakeEnvironmentDamage(rate * maxArmor / 100f);
        }
    }

    // ESPACIO varado: coletazo hacia adelante (se lee en Update, se aplica en FixedUpdate)
    void TryFlop()
    {
        if (!flopRequested) return;
        flopRequested = false;
        if (!IsStranded || isInWater || IsKnockedBack || Time.time < nextFlopTime) return;
        // Solo apoyado (en el aire no hay de dónde empujar)
        if (!isGrounded && Mathf.Abs(rb.linearVelocity.y) > 1.5f) return;

        nextFlopTime = Time.time + flopCooldown;
        Vector3 v = rb.linearVelocity;
        Vector3 fwd = transform.forward;
        Vector3 flat = new Vector3(v.x, 0f, v.z) * 0.5f;
        rb.linearVelocity = flat + fwd * flopForward + Vector3.up * flopUp;
        flopTimer = 0.45f;
        flopSide = -flopSide;
        Sfx.Play(Sonido.Plaf, transform.position, 0.7f, Random.Range(0.9f, 1.15f), 6f, 0.1f, 0.1f);
    }

    // ======================= LÍMITE DE LA ZONA =======================

    // true mientras hace el giro en U (el movimiento normal no se aplica)
    bool UpdateBoundary(float dt)
    {
        if (isDead || InputLocked)
        {
            BoundaryWarning = false;
            if (uTurnTimer > 0f) EndUTurn();
            return false;
        }
        LakeVolume lake = LakeVolume.Instance;
        if (lake == null || lake.Radius <= 0f)
        {
            BoundaryWarning = false;
            return false;
        }

        Vector3 c = lake.Center;
        Vector3 p = rb.position;
        Vector3 flat = new Vector3(p.x - c.x, 0f, p.z - c.z);
        float dist = flat.magnitude;
        float limit = lake.Radius + boundaryMargin;
        BoundaryWarning = dist > limit - boundaryWarnBand;

        if (uTurnTimer <= 0f && dist > limit && Time.time >= nextUTurnTime && !isInWater) StartUTurn(flat);
        if (uTurnTimer <= 0f) return false;

        // Giro en U: sube en la primera mitad, gira el rumbo hacia el lago y baja en la segunda mitad
        uTurnTimer -= dt;
        float t = 1f - Mathf.Clamp01(uTurnTimer / Mathf.Max(0.1f, uTurnDuration));
        float e = Mathf.SmoothStep(0f, 1f, t);
        float yaw = Mathf.LerpAngle(uTurnFromYaw, uTurnToYaw, e);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rot);
        rb.angularVelocity = Vector3.zero;
        Vector3 fwd = rot * Vector3.forward;
        rb.useGravity = false;
        rb.linearVelocity = fwd * uTurnSpeed + Vector3.up * Mathf.Cos(Mathf.PI * t) * uTurnRise;
        if (uTurnTimer <= 0f) EndUTurn();
        return true;
    }

    void StartUTurn(Vector3 fromCenter)
    {
        Vector3 home = -fromCenter.normalized;
        uTurnFromYaw = transform.eulerAngles.y;
        uTurnToYaw = Mathf.Atan2(home.x, home.z) * Mathf.Rad2Deg;
        Vector3 v = rb.linearVelocity;
        uTurnSpeed = Mathf.Clamp(new Vector3(v.x, 0f, v.z).magnitude, 12f, 22f);
        uTurnTimer = uTurnDuration;
        SetJetting(false, 0f);
        SetTurbo(false);
        fovKick = Mathf.Max(fovKick, 8f);
        Sfx.Play2D(Sonido.Aviso, 0.8f, 1.1f, 0f, 0.3f);
        GameDirector gd = GameDirector.Instance;
        if (gd != null) gd.ShowToast("¡FUERA DE LA ZONA!  //  VOLVIENDO AL LAGO", GameDirector.Danger);
    }

    void EndUTurn()
    {
        uTurnTimer = 0f;
        nextUTurnTime = Time.time + 1f;
        if (rb != null) rb.useGravity = true;
    }

    // Postura extra del modelo: nariz arriba en el giro en U y coleteo de lado a lado al arrastrarse
    void SurvivalPose(ref float targetPitch, ref float wiggle, ref float roll)
    {
        if (uTurnTimer > 0f)
        {
            float t = 1f - Mathf.Clamp01(uTurnTimer / Mathf.Max(0.1f, uTurnDuration));
            targetPitch = Mathf.Sin(Mathf.PI * t) * 65f;
            roll = 0f;
        }
        if (flopTimer > 0f)
        {
            flopTimer -= Time.fixedDeltaTime;
            float k = Mathf.Clamp01(flopTimer / 0.45f);
            wiggle += Mathf.Sin((1f - k) * Mathf.PI * 3f) * 28f * k * flopSide;
            roll += Mathf.Sin((1f - k) * Mathf.PI * 2f) * 22f * k * flopSide;
            targetPitch = Mathf.Max(targetPitch, 14f * k);
        }
    }
}
