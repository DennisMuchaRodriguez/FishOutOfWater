using UnityEngine;

public class EnemyBird : MonoBehaviour
{
    [Header("Patrulla")]
    public float patrolRadius = 20f;
    public float patrolSpeed = 3f;
    public float waitTime = 2f;
    private Vector3 targetPatrolPoint;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    [Header("Detección")]
    public float detectionRange = 30f;
    public float attackRange = 5f;
    public LayerMask fishLayer;      // Asigna la capa "Fish" en el Inspector
    public LayerMask playerLayer;    // Asigna la capa "Player" en el Inspector
    private Transform currentTarget;

    [Header("Ataque")]
    public int damageAmount = 10;
    public float attackCooldown = 1f;
    private float attackTimer = 0f;

    [Header("Movimiento")]
    public float flySpeed = 8f;
    public float rotationSpeed = 5f;
    private Rigidbody rb;

    [Header("Referencias")]
    public Damageable damageable; // Arrastra el componente Damageable del pájaro
    private float aggroTimer = 0f;
    private float aggroDuration = 5f;

    public float hitCooldownTimer = 0f;     // Temporizador de "congelamiento"
    public bool isHitCooldown = false;      // ¿Está congelado por golpear?
    public float hitCooldownDuration = 2f;
    public bool isPlayerAggro = false; // Se activa si el jugador ataca al pájaro

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (damageable != null)
        {
            damageable.onDamage.AddListener(OnTakeDamage);
        }

        SetNewPatrolPoint();
    }

    void Update()
    {
        if (IsPlayerDead())
        {
            if (currentTarget != null) currentTarget = null;
            Patrol();
            return;
        }

        // Congelamiento tras golpear al jugador
        if (isHitCooldown)
        {
            hitCooldownTimer -= Time.deltaTime;
            rb.linearVelocity = Vector3.zero;
            if (hitCooldownTimer <= 0)
            {
                isHitCooldown = false;
            }
            return; // No hace nada más durante el cooldown
        }

        DetectTargets();

        if (currentTarget != null)
        {
            ChaseTarget();
        }
        else
        {
            Patrol();
        }

        if (attackTimer > 0) attackTimer -= Time.deltaTime;
    }
    void Patrol()
    {
        // Si está esperando, cuenta el tiempo
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                isWaiting = false;
                SetNewPatrolPoint(); // Busca nuevo punto aleatorio
            }
            return;
        }

        // Moverse hacia el punto de patrulla
        Vector3 direction = (targetPatrolPoint - transform.position).normalized;
        rb.linearVelocity = direction * patrolSpeed;
        RotateTowards(direction);

        // Si llegó al punto, empieza a esperar
        if (Vector3.Distance(transform.position, targetPatrolPoint) < 1f)
        {
            isWaiting = true;
            waitTimer = waitTime;
            rb.linearVelocity = Vector3.zero;
        }
    }
    void DetectTargets()
    {
        // Si el jugador nos agredió, solo perseguimos al jugador (ignoramos peces)
        if (isPlayerAggro)
        {
            Collider[] playerHits = Physics.OverlapSphere(transform.position, detectionRange, playerLayer);
            bool playerInRange = false;
            foreach (Collider hit in playerHits)
            {
                if (hit.CompareTag("Player"))
                {
                    playerInRange = true;
                    currentTarget = hit.transform;
                    aggroTimer = 0f; // Reiniciar timer porque está en rango
                    break;
                }
            }

            if (!playerInRange)
            {
                // El jugador no está en rango: contar tiempo para perder el aggro
                aggroTimer += Time.deltaTime;
                if (aggroTimer >= aggroDuration)
                {
                    isPlayerAggro = false;
                    aggroTimer = 0f;
                    currentTarget = null;
                }
            }
            return; // Salir: no buscar peces
        }

        // --- Modo normal (sin aggro): priorizar peces ---
        Collider[] fishHits = Physics.OverlapSphere(transform.position, detectionRange, fishLayer);
        Transform closestFish = null;
        float closestFishDist = Mathf.Infinity;

        foreach (Collider hit in fishHits)
        {
            if (hit.CompareTag("Fish"))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestFishDist)
                {
                    closestFishDist = dist;
                    closestFish = hit.transform;
                }
            }
        }

        // Si hay peces, priorizarlos siempre
        if (closestFish != null)
        {
            if (currentTarget == null || !currentTarget.CompareTag("Fish") ||
                Vector3.Distance(transform.position, currentTarget.position) > closestFishDist)
            {
                currentTarget = closestFish;
            }
            return;
        }

        // No hay peces: buscar jugador
        Collider[] playerHits2 = Physics.OverlapSphere(transform.position, detectionRange, playerLayer);
        foreach (Collider hit in playerHits2)
        {
            if (hit.CompareTag("Player"))
            {
                currentTarget = hit.transform;
                return;
            }
        }

        // Si no hay nada, perder objetivo
        currentTarget = null;
    }

    void ChaseTarget()
    {
        if (currentTarget == null) return;

        Vector3 direction = (currentTarget.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, currentTarget.position);

        rb.linearVelocity = direction * flySpeed;
        RotateTowards(direction);

        if (distance <= attackRange && attackTimer <= 0)
        {
            Attack(currentTarget);
            attackTimer = attackCooldown;
        }

        // Si el objetivo se alejó del rango de detección, perderlo
        if (distance > detectionRange)
        {
            currentTarget = null;
            if (isPlayerAggro) isPlayerAggro = false;
        }
    }

    void Attack(Transform target)
    {
        Damageable dmg = target.GetComponent<Damageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damageAmount, target.position);

            // Si el objetivo es el jugador, activar congelamiento
            if (target.CompareTag("Player"))
            {
                isHitCooldown = true;
                hitCooldownTimer = hitCooldownDuration;
            }
        }
    }
    bool IsPlayerDead()
    {
        // Referencia al jugador (solo cuando sea necesario)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return true;

        PlayerController_Base controller = player.GetComponent<PlayerController_Base>();
        if (controller == null) return true;

        // Asume que tienes una variable pública "isDead" en PlayerController_Base
        // O usa un método público como "IsDead()"
        return controller.isDead; // Necesitas hacer "isDead" público o crear un getter
    }
    void RotateTowards(Vector3 direction)
    {
        if (direction == Vector3.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void SetNewPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir.y = Mathf.Abs(randomDir.y);
        targetPatrolPoint = transform.position + randomDir;
        targetPatrolPoint.y = Mathf.Clamp(targetPatrolPoint.y, 5f, 50f);
    }

    // Respuesta al daño: activa aggro hacia el jugador
    void OnTakeDamage()
    {
        isPlayerAggro = true;
        aggroTimer = 0f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            currentTarget = player.transform;
            // Desactivar límite de rango para que lo persiga aunque se aleje
            // (se perderá después de aggroDuration segundos sin verlo)
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player") || collision.collider.CompareTag("Fish"))
        {
            Damageable dmg = collision.collider.GetComponent<Damageable>();
            if (dmg != null && attackTimer <= 0)
            {
                dmg.TakeDamage(damageAmount, collision.contacts[0].point);
                attackTimer = attackCooldown;

             
                    isHitCooldown = true;
                    hitCooldownTimer = hitCooldownDuration;
                
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}