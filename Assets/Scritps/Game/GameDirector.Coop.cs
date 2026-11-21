using UnityEngine;

// Modo COOPERATIVO del GameDirector (ver CoopGame y CoopSession).
//  - ANFITRIÓN: juega Supervivencia como siempre, pero las aves persiguen a cualquiera de los jugadores, no hay
//    cinemáticas (cada uno sigue jugando), se pierde si caen todos (o si cazan demasiados peces), espera a que todos
//    carguen el lago antes de la primera oleada y cada 5 oleadas manda a todos al Taller.
//  - CLIENTE: el GameDirector no simula nada. Arma las aves, los peces, los defensores y las cápsulas que le manda
//    el anfitrión (marionetas) y copia el estado del juego para el casco (oleada, cuenta atrás, peces).
public partial class GameDirector
{
    // ¿Partida en una sala? (anfitrión o cliente)
    public bool IsCoop { get; private set; }
    // Cliente: solo muestra lo que manda el anfitrión
    public bool IsCoopClient { get; private set; }

    CoopGame coop;
    static bool localOnlyMessage;
    // Un aviso que es solo de este jugador (no se manda a los demás)
    public static bool IsLocalOnlyMessage { get { return localOnlyMessage; } }

    void SetupCoop()
    {
        IsCoop = CoopSession.InGame && IsSurvival;
        IsCoopClient = IsCoop && !CoopSession.IsHost;
        if (IsCoop && GetComponent<CoopGame>() == null) coop = gameObject.AddComponent<CoopGame>();
        // En cooperativo nadie se queda viendo una cinemática mientras los demás juegan
        if (IsCoop) playArrivalCinematic = false;
    }

    // Cliente: los cardúmenes (los usan los peces aunque sean marionetas) y los totales iniciales
    void StartCoopClient()
    {
        FishAI.SchoolAnchors = new Vector3[Mathf.Max(1, schoolCount)];
        for (int s = 0; s < FishAI.SchoolAnchors.Length; s++)
        {
            Vector3 p;
            lake.TryGetRandomSwimPoint(0.5f, 1.5f, 0.6f, out p);
            FishAI.SchoolAnchors[s] = p;
        }
        TotalFish = Mathf.Max(1, survivalFish);
        FishLost = 0;
        State = GameState.Calm;
        Banner?.Invoke("COOPERATIVO", "Esperando al anfitrión…", Cyan);
    }

    public void LocalToast(string text, Color color)
    {
        localOnlyMessage = true;
        try { Toast?.Invoke(text, color); }
        finally { localOnlyMessage = false; }
    }

    // Sonido de la partida (en cooperativo, también para los demás)
    void Play2DAll(string sound, float volume, float pitch = 1f)
    {
        Sfx.Play2D(sound, volume, pitch);
        if (IsCoop && coop != null) coop.HostSound(sound, volume, pitch);
    }

    // Alerta del casco (en cooperativo, también para los demás)
    void AlertAll(string text, float seconds)
    {
        HelmetVisorHUD.ShowAlert(text, seconds);
        if (IsCoop && coop != null) coop.HostAlert(text, seconds);
    }

    // Anfitrión: a qué jugador persigue un ave nueva
    PlayerController_Base CoopTarget(int index)
    {
        if (IsCoop && coop != null)
        {
            PlayerController_Base p = coop.PickTarget(index + Random.Range(0, 4));
            if (p != null) return p;
        }
        return player;
    }

    // ======================= CLIENTE: LO QUE ARMA CoopGame =======================

    public void RaiseBanner(string title, string sub, Color color) { Banner?.Invoke(title, sub, color); }
    public void RaiseToast(string text, Color color) { Toast?.Invoke(text, color); }

    public void MirrorState(GameState state, int wave, float countdown, int totalFish, int fishLost, int birdsKilled, int fishRescued, int pendingGroups)
    {
        if (!IsCoopClient || ended) return;
        State = state;
        CurrentWave = wave;
        Countdown = countdown;
        TotalFish = Mathf.Max(1, totalFish);
        FishLost = Mathf.Clamp(fishLost, 0, TotalFish);
        BirdsKilled = birdsKilled;
        FishRescued = fishRescued;
        PendingGroups = pendingGroups;
    }

    public BirdAI CreatePuppetBird(BirdType type, Vector3 position, Quaternion rotation, int health, int maxHealth)
    {
        if (type == null) return null;
        BirdAI bird = BuildBirdBody("Ave_" + type.name, position, type, Mathf.Max(1, maxHealth), true);
        bird.transform.rotation = rotation;
        Damageable d = bird.GetComponent<Damageable>();
        if (d != null) d.SetHealthSilently(health);
        birds.Add(bird);
        return bird;
    }

    public FishAI CreatePuppetFish(FishType type, Vector3 position, Quaternion rotation)
    {
        if (type == null) return null;
        FishAI fish = SpawnOneFish(type, 0, 0, LayerMask.NameToLayer("Fish"), false);
        fish.transform.SetPositionAndRotation(position, rotation);
        fish.MakePuppet();
        return fish;
    }

    public FishAI CreatePuppetDefender(FishType type, int index, int count, DefenderFish.Weapon gun, Vector3 position, Quaternion rotation)
    {
        if (type == null) return null;
        DefenderFish d = SpawnDefender(type, index, Mathf.Max(1, count), position, LayerMask.NameToLayer("Fish"), gun, false);
        if (d == null) return null;
        d.transform.rotation = rotation;
        d.MakePuppet();
        if (d.Fish != null) d.Fish.MakePuppet();
        return d.Fish;
    }

    public AmmoPickup CreatePuppetPickup(Vector3 position, bool underwater)
    {
        return BuildPickup(position, underwater);
    }

    // Anfitrión: otro jugador recogió esta cápsula (se quita de la lista y vuelve a aparecer otra más tarde)
    public void HostPickupTakenRemotely(AmmoPickup pickup)
    {
        if (pickup == null) return;
        pickups.Remove(pickup);
        if (!ended) StartCoroutine(RespawnPickup());
    }

    // Cliente: el anfitrión dice que se terminó la partida
    public void MirrorGameOver(int wave, int wavesCleared, int birdsKilled, int fishRescued, int fishAlive, int totalFish)
    {
        if (ended) return;
        ended = true;
        StopCinematic();
        State = GameState.Defeat;
        CurrentWave = wave;
        BirdsKilled = birdsKilled;
        FishRescued = fishRescued;
        TotalFish = Mathf.Max(1, totalFish);
        FishLost = Mathf.Clamp(TotalFish - fishAlive, 0, TotalFish);
        SurvivalRun.SyncFromHost(wavesCleared, false, SurvivalRun.TimeOfDay);
        SurvivalRun.SaveTotals(FishLost, BirdsKilled, FishRescued);
        SaveSystem.Save();
        Banner?.Invoke("FIN DE LA PARTIDA", "Llegaron a la oleada " + Mathf.Max(1, wave) + "  //  " + wavesCleared + " superadas", Danger);
        Music.PlayEnd(false);
        PauseMenu menu = FindFirstObjectByType<PauseMenu>();
        if (menu != null) menu.ShowEndScreen(false);
    }
}
