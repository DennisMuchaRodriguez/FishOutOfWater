using UnityEngine;
using UnityEngine.Events;
public class Damageable : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Eventos")]
    public UnityEvent onDie;
    public UnityEvent onDamage;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount, Vector3 hitPoint)
    {
        currentHealth -= amount;
        onDamage?.Invoke();

        Debug.Log($"{gameObject.name} recibió {amount} daño. Vida: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        onDie?.Invoke();
        Destroy(gameObject);
    }
}
