using System.Collections.Generic;
using UnityEngine;

// Vaivén barato de juncos, hierba, algas y nenúfares: mueve el transform (no toca la malla).
// Solo se mueven los que están cerca de la cámara y como mucho 'budget' por frame,
// así cientos de plantas no cuestan casi nada. Lo crea LakeDecorator en su raíz.
[DisallowMultipleComponent]
public class DecorationSway : MonoBehaviour
{
    [System.Serializable]
    public struct Item
    {
        public Transform t;
        public DecorSway mode;
        public float amount;
        public float phase;
        public Vector3 basePos;
        public Quaternion baseRot;
    }

    [Tooltip("Distancia a la cámara hasta la que se mueven (m)")]
    public float maxDistance = 70f;
    [Tooltip("Máximo de objetos que se actualizan por frame")]
    [Min(10)] public int budget = 300;
    [Tooltip("Fuerza del viento (multiplica el vaivén de las plantas de tierra)")]
    [Range(0f, 3f)] public float wind = 1f;
    [Tooltip("Fuerza de la corriente (multiplica el vaivén de las algas)")]
    [Range(0f, 3f)] public float current = 1f;

    [SerializeField, HideInInspector] List<Item> items = new List<Item>();
    int cursor;
    Camera cam;
    float camTimer;

    public int Count { get { return items.Count; } }

    public void Add(Transform t, DecorSway mode, float amount, float phase)
    {
        if (t == null || mode == DecorSway.Ninguno) return;
        Item it = new Item();
        it.t = t;
        it.mode = mode;
        it.amount = amount;
        it.phase = phase;
        it.basePos = t.position;
        it.baseRot = t.rotation;
        items.Add(it);
    }

    public void Clear()
    {
        items.Clear();
        cursor = 0;
    }

    void Update()
    {
        int n = items.Count;
        if (n == 0) return;

        // La cámara se busca de vez en cuando (puede cambiar con la cinemática)
        camTimer -= Time.deltaTime;
        if (cam == null || camTimer <= 0f)
        {
            cam = Camera.main;
            camTimer = 1f;
        }
        bool useDistance = cam != null;
        Vector3 camPos = useDistance ? cam.transform.position : Vector3.zero;
        float maxSq = maxDistance * maxDistance;
        float time = Time.time;

        // Recorre la lista por turnos: los lejanos solo cuestan una resta
        int checks = Mathf.Min(n, budget * 4);
        int moved = 0;
        for (int i = 0; i < checks && moved < budget; i++)
        {
            if (cursor >= n) cursor = 0;
            Item it = items[cursor++];
            if (it.t == null) continue;
            if (useDistance && (it.basePos - camPos).sqrMagnitude > maxSq) continue;
            Apply(it, time);
            moved++;
        }
    }

    void Apply(Item it, float time)
    {
        float p = it.phase;
        switch (it.mode)
        {
            case DecorSway.Viento:
            {
                // Ráfagas que recorren el lago (fase según la posición) + temblor rápido
                float a = it.amount * wind;
                float gust = 0.65f + 0.35f * Mathf.Sin(time * 0.45f + p * 0.3f);
                float sx = (Mathf.Sin(time * 1.6f + p) + 0.3f * Mathf.Sin(time * 3.7f + p * 2.1f)) * a * gust;
                float sz = Mathf.Sin(time * 1.15f + p * 1.3f) * a * 0.6f * gust;
                it.t.rotation = Quaternion.Euler(sx, 0f, sz) * it.baseRot;
                break;
            }
            case DecorSway.Agua:
            {
                // Corriente lenta y amplia
                float a = it.amount * current;
                float sx = Mathf.Sin(time * 0.7f + p) * a;
                float sz = Mathf.Sin(time * 0.53f + p * 1.7f) * a * 0.8f;
                it.t.rotation = Quaternion.Euler(sx, 0f, sz) * it.baseRot;
                break;
            }
            case DecorSway.Flotar:
            {
                // Sube y baja con las ondas y gira despacio
                float bob = Mathf.Sin(time * 0.9f + p) * 0.025f;
                float a = it.amount;
                Quaternion r = Quaternion.Euler(Mathf.Sin(time * 0.8f + p) * a * 0.5f,
                                                Mathf.Sin(time * 0.21f + p) * a * 2f,
                                                Mathf.Sin(time * 0.67f + p * 1.4f) * a * 0.5f);
                it.t.SetPositionAndRotation(it.basePos + Vector3.up * bob, r * it.baseRot);
                break;
            }
        }
    }
}
