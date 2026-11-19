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

    // Cooperativo: en los clientes las aves son "marionetas" del anfitrión. Los golpes no se aplican aquí: se mandan
    // al anfitrión (CoopGame), que tiene el ave de verdad. PendingPush lo deja el proyectil justo antes de golpear
    [System.NonSerialized] public System.Action<int, Vector3, Vector3> NetForward;
    public static Vector3 PendingPush;

    // Cooperativo: la vida que dice el anfitrión (sin eventos ni muerte)
    public void SetHealthSilently(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, Mathf.Max(1, maxHealth));
    }

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
        if (NetForward != null)
        {
            NetForward(amount, hitPoint, PendingPush);
            PendingPush = Vector3.zero;
            return false;
        }
        PendingPush = Vector3.zero;
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
