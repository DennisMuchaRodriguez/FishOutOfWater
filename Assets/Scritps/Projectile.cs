using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 10;
    public float lifeTime = 3f;

    void Start()
    {
        Destroy(gameObject, lifeTime);

    
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    void OnCollisionEnter(Collision collision)  
    {
       
        Damageable damageable = collision.collider.GetComponent<Damageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, collision.contacts[0].point);
        }

        
        Destroy(gameObject);
    }

 

}
