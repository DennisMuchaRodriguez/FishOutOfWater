using UnityEngine;
using UnityEngine.Events;
public class Damageable : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    [Tooltip("Si está desactivado, quien escuche el evento Died decide qué hacer (animación de muerte, etc.)")]
    public bool destroyOnDeath = true;

    [Header("Eventos")]
    public UnityEvent onDie;
    public UnityEvent onDamage;

    // Eventos de código (los usan las IA)
    public event System.Action<int, Vector3> Damaged;
    public event System.Action Died;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public bool IsDead { get; private set; }
    public int CurrentHealth { get { return currentHealth; } }
    public float Health01 { get { return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f; } }

    public void SetMaxHealth(int value)
    {
        maxHealth = value;
        currentHealth = value;
    }

    // Devuelve true si este golpe lo destruyó
    public bool TakeDamage(int amount, Vector3 hitPoint)
    {
        if (IsDead) return false;
        currentHealth -= amount;
        onDamage?.Invoke();
        Damaged?.Invoke(amount, hitPoint);

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
        Died?.Invoke();
        if (destroyOnDeath) Destroy(gameObject);
    }
}
