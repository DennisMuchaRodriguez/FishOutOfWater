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

    public bool IsDead { get; private set; }
    public int CurrentHealth { get { return currentHealth; } }

    // Devuelve true si este golpe lo destruyó
    public bool TakeDamage(int amount, Vector3 hitPoint)
    {
        if (IsDead) return false;
        currentHealth -= amount;
        onDamage?.Invoke();

        Debug.Log($"{gameObject.name} recibió {amount} daño. Vida: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    void Die()
    {
        IsDead = true;
        onDie?.Invoke();
        Destroy(gameObject);
    }
}
