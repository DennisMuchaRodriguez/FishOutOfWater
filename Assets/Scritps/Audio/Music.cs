using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Música del juego. Las pistas están en Assets/Resources/Musica (compuestas con Herramientas/Musica/componer_musica.py):
//  - "Menu"     (El Traje, épica): menú principal y el Taller (visitas y sala cooperativa).
//  - "Juego"    (Batalla en el lago): la partida.
//  - "Carga"    (Bajo la superficie): pantalla de carga (si la escena que viene usa otra pista).
//  - "Derrota" / "Victoria": una vez al terminar la partida; después, silencio hasta la escena siguiente.
//  - "Historia" (Cuentos del lago): los cómics. "Amenaza" (Sombra en el cielo): los cómics de antes de un jefe.
//  - "Creditos" (El lago a salvo): el cómic final y los créditos.
// Cambia de pista con un fundido cruzado; en la pausa baja a menos de la mitad. La música no pasa por el filtro de
// "bajo el agua" del oyente (se oye igual dentro y fuera del agua).
// Volumen propio (Ajustes > MÚSICA), aparte del general (Sfx.Volume) y de los efectos (Sfx.EffectsVolume).
// Para cambiar una pista basta con reemplazar el archivo manteniendo el nombre; si falta, no suena y no da error.
[DisallowMultipleComponent]
public class Music : MonoBehaviour
{
    public const string Menu = "Menu";
    public const string Game = "Juego";
    public const string Loading = "Carga";
    public const string Defeat = "Derrota";
    public const string Victory = "Victoria";
    public const string Story = "Historia";
    public const string Threat = "Amenaza";
    public const string Credits = "Creditos";

    const string Folder = "Musica/";
    const string VolumeKey = "VolumenMusica";
    const float PausedLevel = 0.4f;

    static Music instance;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static float volume = -1f;

    readonly AudioSource[] sources = new AudioSource[2];
    readonly float[] level = new float[2];     // 0..1: fundido de cada fuente
    readonly float[] target = new float[2];
    readonly float[] gain = new float[2];      // nivel propio de cada pista (la de la partida va más baja)
    readonly float[] fadeTime = { 1f, 1f };
    int active = -1;
    string current;
    float duck = 1f;

    // ======================= API =======================

    public static float Volume
    {
        get
        {
            if (volume < 0f) volume = PlayerPrefs.GetFloat(VolumeKey, 0.7f);
            return volume;
        }
        set
        {
            volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, volume);
        }
    }

    // Pista que suena (o la última que se pidió)
    public static string Current { get { return instance != null ? instance.current : null; } }

    static bool IsPlayingNow { get { return instance != null && instance.active >= 0 && instance.sources[instance.active].isPlaying; } }

    // Pone una pista en bucle (si ya suena, sigue sin cortarse)
    public static void Play(string track, float fade = 1.5f)
    {
        Music m = Instance;
        if (m.current == track && IsPlayingNow) return;
        m.Begin(track, IsLoop(track), fade);
    }

    // Remate de una sola vez (derrota, victoria). false si no está el archivo
    public static bool PlaySting(string track)
    {
        if (Clip(track) == null) return false;
        Instance.Begin(track, false, 0.35f);
        return true;
    }

    // Fin de la partida: el remate musical (o, si falta, el sonido de siempre)
    public static void PlayEnd(bool victory)
    {
        if (!PlaySting(victory ? Victory : Defeat))
            Sfx.Play2D(victory ? Sonido.Victoria : Sonido.Derrota, 0.9f);
    }

    public static void Stop(float fade = 1f)
    {
        if (instance == null) return;
        instance.FadeAllOut(fade);
        instance.current = null;
    }

    // La pantalla de carga: si la escena que viene usa la misma pista que suena, sigue; si no, la de carga
    public static void OnLoading(string sceneName)
    {
        string next = TrackFor(sceneName);
        if (next != null && next == Current && IsPlayingNow) return;
        Play(Loading, 0.8f);
    }

    // ======================= ARRANQUE Y ESCENAS =======================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        clips.Clear();
        volume = -1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayFor(SceneManager.GetActiveScene().name);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // El Taller que se carga de fondo en el menú no cambia la música
        if (mode == LoadSceneMode.Additive) return;
        PlayFor(scene.name);
    }

    static void PlayFor(string sceneName)
    {
        string t = TrackFor(sceneName);
        if (t != null) Play(t);
    }

    static string TrackFor(string sceneName)
    {
        if (sceneName == GameSession.MenuScene || sceneName == WorkshopFlow.WorkshopScene) return Menu;
        if (sceneName == GameSession.LakeScene) return Game;
        return null;   // otra escena: sigue lo que suena
    }

    static bool IsLoop(string track)
    {
        return track != Defeat && track != Victory;
    }

    // true si la pista suena en bucle (para volver a ella después de un cómic); false para los remates y "nada"
    public static bool IsLoopTrack(string track)
    {
        return !string.IsNullOrEmpty(track) && IsLoop(track);
    }

    static float GainOf(string track)
    {
        switch (track)
        {
            case Game: return 0.62f;      // debajo de los disparos, las aves y el traje
            case Loading: return 0.8f;
            case Story: return 0.85f;
            case Defeat:
            case Victory: return 0.95f;
            default: return 1f;
        }
    }

    static AudioClip Clip(string track)
    {
        AudioClip c;
        if (clips.TryGetValue(track, out c)) return c;
        c = Resources.Load<AudioClip>(Folder + track);
        clips[track] = c;
        return c;
    }

    static Music Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Musica");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Music>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        for (int i = 0; i < 2; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.priority = 0;
            s.bypassListenerEffects = true;   // el filtro de "bajo el agua" no apaga la música
            s.bypassReverbZones = true;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            sources[i] = s;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ======================= FUNDIDOS =======================

    void Begin(string track, bool loop, float fade)
    {
        current = track;
        AudioClip clip = Clip(track);
        if (clip == null)
        {
            FadeAllOut(fade);
            return;
        }
        int next = active < 0 ? 0 : 1 - active;
        AudioSource s = sources[next];
        s.Stop();
        s.clip = clip;
        s.loop = loop;
        s.time = 0f;
        level[next] = 0f;
        target[next] = 1f;
        gain[next] = GainOf(track);
        fadeTime[next] = Mathf.Max(0.05f, fade);
        s.volume = 0f;
        s.Play();
        if (active >= 0)
        {
            target[active] = 0f;
            fadeTime[active] = Mathf.Max(0.05f, fade);
        }
        active = next;
    }

    void FadeAllOut(float fade)
    {
        for (int i = 0; i < 2; i++)
        {
            target[i] = 0f;
            fadeTime[i] = Mathf.Max(0.05f, fade);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        // En la pausa baja (no en la pantalla final: ahí suena el remate)
        bool paused = PauseMenu.IsPaused && !PauseMenu.GameEnded;
        duck = Mathf.MoveTowards(duck, paused ? PausedLevel : 1f, dt * 2f);
        float vol = Volume * duck;
        for (int i = 0; i < 2; i++)
        {
            AudioSource s = sources[i];
            if (s == null) continue;
            level[i] = Mathf.MoveTowards(level[i], target[i], dt / fadeTime[i]);
            // Curva de igual potencia: el cruce no hace un "bache" de volumen en el medio
            float k = Mathf.Sin(level[i] * Mathf.PI * 0.5f);
            s.volume = k * gain[i] * vol;
            if (target[i] <= 0f && level[i] <= 0f && s.isPlaying) s.Stop();
        }
    }
}
