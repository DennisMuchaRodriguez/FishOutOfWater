using UnityEngine;

// Vaivén suave para las piezas del Taller (algas, rayos de luz): inclina el objeto de un lado a otro
// alrededor de su base, como si lo moviera la corriente.
public class WorkshopSway : MonoBehaviour
{
    [Tooltip("Grados de inclinación máxima")]
    public float amount = 5f;
    [Tooltip("Velocidad del vaivén")]
    public float speed = 0.8f;

    Quaternion baseRotation;
    float phase;

    void Start()
    {
        baseRotation = transform.localRotation;
        phase = Random.Range(0f, 10f);
    }

    void Update()
    {
        float t = Time.time * speed + phase;
        float x = Mathf.Sin(t * 1.3f) * amount * 0.4f;
        float z = Mathf.Sin(t) * amount;
        transform.localRotation = baseRotation * Quaternion.Euler(x, 0f, z);
    }
}
