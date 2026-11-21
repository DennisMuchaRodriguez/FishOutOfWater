using System.Collections.Generic;
using UnityEngine;

// Sonidos del juego. Uso:
//   Sfx.Play(Sonido.GolpeAve, posición)      -> sonido en el mundo (se oye más fuerte cerca)
//   Sfx.Play2D(Sonido.UIClick)               -> sonido "en la cabeza" (interfaz, el propio traje)
//   AudioSource s = Sfx.Loop(Sonido.Remolino, transform, 0.8f)  -> bucle que sigue a un objeto (Sfx.StopLoop(s))
// Los clips están en Assets/Resources/Sonidos/<Nombre>.wav (variantes al azar: <Nombre>_2, <Nombre>_3...).
// Para cambiar un sonido basta con reemplazar el archivo manteniendo el nombre. Si falta, no suena y no da error.
// Volúmenes (Ajustes > Sonido, se guardan en PlayerPrefs):
//   Volume         "Volumen"         general: todo lo que se oye (también la música)
//   EffectsVolume  "VolumenEfectos"  los efectos (todo lo que pasa por Sfx). La música tiene el suyo (Music.Volume)
public static class Sfx
{
    const string Folder = "Sonidos/";
    const int PoolSize = 32;
    const string VolumeKey = "Volumen";
    const string EffectsKey = "VolumenEfectos";

    static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    // Bucles vivos y su volumen pedido (para seguir el volumen de efectos al moverlo)
    static readonly Dictionary<AudioSource, float> loops = new Dictionary<AudioSource, float>();
    static SfxHost host;
    static float effects = 1f;

    public static float Volume
    {
        get { return PlayerPrefs.GetFloat(VolumeKey, 0.8f); }
        set
        {
            float v = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, v);
            AudioListener.volume = v;
        }
    }

    public static float EffectsVolume
    {
        get { return effects; }
        set
        {
            effects = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(EffectsKey, effects);
            RefreshLoops();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        AudioListener.volume = Volume;
        effects = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 1f));
        loops.Clear();
    }

    static void RefreshLoops()
    {
        List<AudioSource> gone = null;
        foreach (KeyValuePair<AudioSource, float> kv in loops)
        {
            if (kv.Key == null)
            {
                if (gone == null) gone = new List<AudioSource>();
                gone.Add(kv.Key);
                continue;
            }
            kv.Key.volume = kv.Value * effects;
        }
        if (gone != null) foreach (AudioSource s in gone) loops.Remove(s);
    }

    // Un clip al azar entre sus variantes (null si no existe)
    public static AudioClip Clip(string id)
    {
        AudioClip[] list = Clips(id);
        return list.Length == 0 ? null : list[Random.Range(0, list.Length)];
    }

    static AudioClip[] Clips(string id)
    {
        AudioClip[] list;
        if (clips.TryGetValue(id, out list)) return list;
        List<AudioClip> found = new List<AudioClip>();
        AudioClip c = Resources.Load<AudioClip>(Folder + id);
        if (c != null) found.Add(c);
        for (int i = 2; i <= 6; i++)
        {
            c = Resources.Load<AudioClip>(Folder + id + "_" + i);
            if (c == null) break;
            found.Add(c);
        }
        list = found.ToArray();
        clips[id] = list;
        return list;
    }

    // Evita que el mismo sonido se apile muchas veces en el mismo instante
    static bool Ready(string id, float minInterval)
    {
        float last;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out last) && now - last < minInterval) return false;
        lastPlayed[id] = now;
        return true;
    }

    static SfxHost Host
    {
        get
        {
            if (host == null)
            {
                GameObject go = new GameObject("Sonidos");
                Object.DontDestroyOnLoad(go);
                host = go.AddComponent<SfxHost>();
                host.Build(PoolSize);
            }
            return host;
        }
    }

    // Sonido en un punto del mundo. minDistance: hasta dónde se oye a todo volumen (luego se apaga con la distancia)
    public static AudioSource Play(string id, Vector3 position, float volume = 1f, float pitch = 1f, float minDistance = 5f,
                                   float pitchSpread = 0.06f, float minInterval = 0.04f)
    {
        if (string.IsNullOrEmpty(id) || !Ready(id, minInterval)) return null;
        AudioClip clip = Clip(id);
        if (clip == null) return null;
        AudioSource s = Host.Next();
        s.transform.position = position;
        s.spatialBlend = 1f;
        s.minDistance = Mathf.Max(0.5f, minDistance);
        s.maxDistance = Mathf.Max(150f, minDistance * 40f);
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume) * effects;
        s.pitch = Mathf.Clamp(pitch * Random.Range(1f - pitchSpread, 1f + pitchSpread), 0.1f, 3f);
        s.Play();
        return s;
    }

    // Sonido sin posición (interfaz, el propio traje del jugador)
    public static AudioSource Play2D(string id, float volume = 1f, float pitch = 1f, float pitchSpread = 0.03f, float minInterval = 0.03f)
    {
        if (string.IsNullOrEmpty(id) || !Ready(id, minInterval)) return null;
        AudioClip clip = Clip(id);
        if (clip == null) return null;
        AudioSource s = Host.Next();
        s.spatialBlend = 0f;
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume) * effects;
        s.pitch = Mathf.Clamp(pitch * Random.Range(1f - pitchSpread, 1f + pitchSpread), 0.1f, 3f);
        s.Play();
        return s;
    }

    // Bucle que sigue a un objeto (remolino, viento, propulsor). spatial = false -> se oye igual en todas partes
    public static AudioSource Loop(string id, Transform follow, float volume = 1f, float minDistance = 6f, bool spatial = true)
    {
        AudioClip clip = Clip(id);
        if (clip == null) return null;
        GameObject go = new GameObject("Bucle_" + id);
        if (follow != null) go.transform.SetParent(follow, false);
        else Object.DontDestroyOnLoad(go);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = minDistance;
        s.maxDistance = Mathf.Max(150f, minDistance * 40f);
        s.dopplerLevel = 0f;
        s.volume = Mathf.Clamp01(volume) * effects;
        if (loops.Count > 64) RefreshLoops();   // quita los bucles que se destruyeron con su objeto
        loops[s] = Mathf.Clamp01(volume);
        s.time = Random.Range(0f, clip.length * 0.9f);
        s.Play();
        return s;
    }

    // Cambia el volumen de un bucle respetando el volumen de efectos
    public static void SetLoopVolume(AudioSource s, float volume)
    {
        if (s == null) return;
        loops[s] = Mathf.Clamp01(volume);
        s.volume = Mathf.Clamp01(volume) * effects;
    }

    public static void StopLoop(AudioSource s)
    {
        if (s == null) return;
        loops.Remove(s);
        Object.Destroy(s.gameObject);
    }
}
