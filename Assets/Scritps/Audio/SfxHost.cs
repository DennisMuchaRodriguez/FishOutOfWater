using UnityEngine;

// Las fuentes de audio reutilizables (se crea sola la primera vez que suena algo)
public class SfxHost : MonoBehaviour
{
    AudioSource[] pool;
    int next;

    public void Build(int size)
    {
        pool = new AudioSource[size];
        for (int i = 0; i < size; i++)
        {
            GameObject go = new GameObject("Fuente" + i);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.dopplerLevel = 0f;
            pool[i] = s;
        }
    }

    // Una fuente libre (o la más vieja si todas están sonando)
    public AudioSource Next()
    {
        for (int k = 0; k < pool.Length; k++)
        {
            int i = (next + k) % pool.Length;
            if (!pool[i].isPlaying)
            {
                next = (i + 1) % pool.Length;
                return pool[i];
            }
        }
        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.Stop();
        return s;
    }
}
