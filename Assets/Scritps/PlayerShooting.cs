using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Configuración")]
    public KeyCode shootKey = KeyCode.Mouse0;
    public GameObject projectilePrefab;
    public float shootForce = 20f;
    public float fireRate = 0.3f;

    [Header("Punto de disparo")]
    public Transform firePoint;  

    [Header("Referencias")]
    public Camera playerCamera;  

    [Header("Opciones de puntería")]
    public float maxDistance = 500f;  
    public LayerMask aimLayers = ~0;

    [Header("Munición - NUEVO")]
    public int maxAmmo = 20;
    public int currentAmmo;
    public bool isInWaterForAmmo = false;

    private float nextFireTime = 0f;

    void Start()
    {
        currentAmmo = maxAmmo;
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
         

        if (playerCamera == null)
        {
            Debug.LogError("No hay cámara tmr");
        }

    }

    void Update()
    {
        if (Input.GetKeyDown(shootKey) && Time.time >= nextFireTime && 0 < currentAmmo)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;

            currentAmmo = currentAmmo - 1;
        }

        if (isInWaterForAmmo && currentAmmo < maxAmmo)
        {
           
            if (Time.frameCount % 60 == 0) 
            {
                currentAmmo = Mathf.Min(currentAmmo + 1, maxAmmo);
            }
        }
    }
    public void UpdateWaterState(bool inWater)
    {
        isInWaterForAmmo = inWater;
    }
    void Shoot()
    {
    
       if (projectilePrefab == null || firePoint == null || playerCamera == null)
        {
            Debug.LogError("No hay cámara asignada");
        }
        // PASO 1: Calcular dónde apunta el mouse en el mundo
        Vector3 targetPoint = GetAimTarget();

        // PASO 2: Calcular dirección desde firePoint hacia targetPoint
        Vector3 direction = (targetPoint - firePoint.position).normalized;

        // PASO 3: Crear proyectil
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));

        // PASO 4: Darle velocidad en esa dirección
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = direction * shootForce;
        }

        // Opcional: ignorar colisión con el jugador
        Collider playerCollider = GetComponent<Collider>();
        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (playerCollider != null && projectileCollider != null)
        {
            Physics.IgnoreCollision(projectileCollider, playerCollider);
        }

        Debug.DrawLine(firePoint.position, targetPoint, Color.red, 1f);
    }

    Vector3 GetAimTarget()
    {
        // Crear un rayo desde la cámara hacia donde está el mouse
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Si golpea algo, ese es el punto objetivo
        if (Physics.Raycast(ray, out hit, maxDistance, aimLayers))
        {
            return hit.point;
        }
        else
        {
            // Si no golpea nada, disparar hacia el infinito en esa dirección
            return ray.GetPoint(maxDistance);
        }
    }
}