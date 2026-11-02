using UnityEngine;
using UnityEngine.SceneManagement;

// Sonidos del jugador y del ambiente. Se agrega solo en las escenas que tienen al jugador (no hay que tocarlas):
//  - Disparos (pistola, cargado, burbujas), "clic" sin munición, cápsulas, aciertos y derribos
//  - Golpes al traje, alarma con poca armadura y la muerte
//  - Propulsor (bucle que sube con el turbo), dash y turbo
//  - Ambiente del lago y ambiente bajo el agua; bajo el agua todo se oye apagado (filtro en el oyente)
[DisallowMultipleComponent]
public class GameAudio : MonoBehaviour
{
    PlayerController_Base player;
    PlayerShooting shooting;
    AudioSource jet, ambience, underwater;
    AudioLowPassFilter muffle;
    float alarmTimer;
    bool wasDead;
    float submerged01;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    static void Install()
    {
        PlayerController_Base p = FindFirstObjectByType<PlayerController_Base>();
        if (p != null && p.GetComponent<GameAudio>() == null) p.gameObject.AddComponent<GameAudio>();
    }

    void Start()
    {
        player = GetComponent<PlayerController_Base>();
        shooting = GetComponent<PlayerShooting>();
        if (player != null)
        {
            player.OnDamaged += OnDamaged;
            player.OnDash += OnDash;
            player.OnTurboStart += OnTurbo;
            player.OnSubmergedChanged += OnSubmerged;
        }
        if (shooting != null)
        {
            shooting.OnShot += OnShot;
            shooting.OnAmmoPickup += OnPickup;
            shooting.OnTargetHit += OnTargetHit;
        }

        jet = Sfx.Loop(Sonido.Propulsor, transform, 0f, 3f, false);
        ambience = Sfx.Loop(Sonido.AmbienteLago, transform, 0.35f, 3f, false);
        underwater = Sfx.Loop(Sonido.AmbienteSubacuatico, transform, 0f, 3f, false);

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener != null)
        {
            muffle = listener.GetComponent<AudioLowPassFilter>();
            if (muffle == null) muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = 22000f;
        }
    }

    void OnDestroy()
    {
        if (player != null)
        {
            player.OnDamaged -= OnDamaged;
            player.OnDash -= OnDash;
            player.OnTurboStart -= OnTurbo;
            player.OnSubmergedChanged -= OnSubmerged;
        }
        if (shooting != null)
        {
            shooting.OnShot -= OnShot;
            shooting.OnAmmoPickup -= OnPickup;
            shooting.OnTargetHit -= OnTargetHit;
        }
        if (muffle != null) muffle.cutoffFrequency = 22000f;
    }

    void OnShot(bool charged)
    {
        if (charged) Sfx.Play2D(Sonido.DisparoCargado, 0.9f);
        else if (shooting != null && shooting.IsBubbleGun) Sfx.Play2D(Sonido.Burbuja, 0.45f, 1f, 0.15f, 0.04f);
        else Sfx.Play2D(Sonido.Disparo, 0.7f, 1f, 0.07f);
    }

    void OnPickup(int amount) { Sfx.Play2D(Sonido.Capsula, 0.6f, 1f, 0.06f, 0.08f); }

    void OnTargetHit(bool killed)
    {
        if (killed) Sfx.Play2D(Sonido.Derribo, 0.7f);
        else Sfx.Play2D(Sonido.Acierto, 0.4f, 1f, 0.05f, 0.05f);
    }

    void OnDamaged(float amount)
    {
        Sfx.Play2D(Sonido.GolpeTraje, Mathf.Clamp(0.55f + amount / 40f, 0.55f, 1f));
    }

    void OnDash() { Sfx.Play2D(Sonido.Dash, 0.7f); }
    void OnTurbo() { Sfx.Play2D(Sonido.Turbo, 0.7f); }

    void OnSubmerged(bool under)
    {
        // La salpicadura la pone LakeWater; aquí solo el cambio de ambiente (en Update)
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        bool paused = PauseMenu.IsPaused;

        // Propulsor: suena mientras propulsa; con turbo más fuerte y agudo
        if (jet != null)
        {
            bool on = !paused && !player.isDead && (player.IsJetting || player.IsTurbo);
            float targetVol = on ? (player.IsTurbo ? 0.75f : 0.5f) : 0f;
            if (player.IsSputtering && on) targetVol *= 0.5f + 0.5f * Mathf.PerlinNoise(Time.time * 20f, 0f);
            jet.volume = Mathf.MoveTowards(jet.volume, targetVol, dt * 4f);
            jet.pitch = Mathf.MoveTowards(jet.pitch, player.IsTurbo ? 1.25f : (player.isInWater ? 0.8f : 1f), dt * 2f);
        }

        // Ambiente: fuera y dentro del agua
        bool under = player.isInWater && player.PlayerCamera != null && LakeWater.IsUnderwater(player.PlayerCamera.transform.position);
        submerged01 = Mathf.MoveTowards(submerged01, under ? 1f : 0f, dt * 4f);
        if (ambience != null) ambience.volume = 0.35f * (1f - submerged01);
        if (underwater != null) underwater.volume = 0.5f * submerged01;
        if (muffle != null) muffle.cutoffFrequency = Mathf.Lerp(22000f, 900f, submerged01);

        // "Clic" si intenta disparar sin munición
        if (shooting != null && !paused && !player.isDead && shooting.enabled && shooting.currentAmmo <= 0 && Input.GetKeyDown(shooting.shootKey))
            Sfx.Play2D(Sonido.SinMunicion, 0.6f, 1f, 0.04f, 0.15f);

        // Alarma del traje con poca armadura
        float armor01 = player.maxArmor > 0f ? player.currentArmor / player.maxArmor : 1f;
        if (!player.isDead && !paused && armor01 <= 0.25f)
        {
            alarmTimer -= dt;
            if (alarmTimer <= 0f)
            {
                Sfx.Play2D(Sonido.AlarmaTraje, 0.45f);
                alarmTimer = armor01 <= 0.1f ? 0.7f : 1.2f;
            }
        }
        else alarmTimer = 0f;

        if (player.isDead && !wasDead) Sfx.Play2D(Sonido.Muerte, 1f);
        wasDead = player.isDead;
    }
}
