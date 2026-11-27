using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
//  PARTIDA COOPERATIVA EN EL LAGO (lo agrega el GameDirector cuando se juega en una sala)
//  ANFITRIÓN: simula todo como siempre (oleadas, aves, peces, defensores, cápsulas) y manda:
//    - Cuando aparece algo (ave, pez, defensor, cápsula) y cuando muere o se recoge (mensajes seguros).
//    - Cada 0,1 s dónde están las aves y cada 0,15 s los peces (mensajes rápidos que pueden perderse).
//    - El estado del juego (oleada, cuenta atrás, peces, hora del día), los carteles, avisos y sonidos.
//    - Los golpes que las aves le dan a cada compañero (al dueño de ese pez).
//  CADA JUGADOR: mueve su propio pez y manda cómo está 20 veces por segundo. Sus disparos se ven en las demás
//    pantallas (copias sin daño) y, si le da a un ave, se lo avisa al anfitrión, que le quita la vida de verdad.
//  Las aves del anfitrión persiguen a cualquiera de los jugadores (a veces cambian al más cercano o a quien les
//  disparó). Si tu traje se destruye miras a un compañero (clic para cambiar) hasta el próximo Taller, donde vuelves
//  reparado. Se pierde si caen todos o si cazan demasiados peces.
// ============================================================================
[DefaultExecutionOrder(500)]
public class CoopGame : MonoBehaviour
{
    public static CoopGame Instance { get; private set; }

    const float PlayerRate = 0.05f, BirdRate = 0.1f, FishRate = 0.15f, DirectorRate = 0.25f;
    const float ScanRate = 0.1f, RetargetRate = 1.5f, CurrentRate = 0.1f;
    const int BirdsPerPacket = 40, FishPerPacket = 44;

    struct PlayerSnap
    {
        public bool valid;
        public Vector3 pos;
        public Quaternion body;
        public Quaternion model;
        public Vector3 vel;
        public byte flags;
        public byte armor;
    }

    GameDirector director;
    bool host;
    PlayerController_Base local;
    PlayerShooting localShooting;
    readonly Dictionary<int, BirdAI> birdsById = new Dictionary<int, BirdAI>();
    readonly Dictionary<int, FishAI> fishById = new Dictionary<int, FishAI>();
    readonly Dictionary<int, AmmoPickup> pickupsById = new Dictionary<int, AmmoPickup>();
    readonly Dictionary<int, int> lastHitter = new Dictionary<int, int>();
    readonly CoopRemotePlayer[] remotes = new CoopRemotePlayer[CoopSession.MaxPlayers];
    readonly PlayerSnap[] snaps = new PlayerSnap[CoopSession.MaxPlayers];
    readonly Vector3[] pendingCurrent = new Vector3[CoopSession.MaxPlayers];
    readonly List<BirdAI> birdBuffer = new List<BirdAI>();
    readonly List<FishAI> fishBuffer = new List<FishAI>();
    readonly List<PlayerController_Base> aliveBuffer = new List<PlayerController_Base>();
    int nextId = 1;
    float playerTimer, birdTimer, fishTimer, dirTimer, scanTimer, retargetTimer, currentTimer;
    ulong processingSender = ulong.MaxValue;
    int processingSlot = -1;

    // ======================= CICLO DE VIDA =======================

    void Awake()
    {
        Instance = this;
        director = GetComponent<GameDirector>();
        host = CoopSession.IsHost;
        if (host)
        {
            CoopSession.On(CoopOp.PlayerState, OnPlayerState);
            CoopSession.On(CoopOp.HitBird, OnHitBird);
            CoopSession.On(CoopOp.TakePickup, OnTakePickup);
            CoopSession.On(CoopOp.Shot, OnShotFromClient);
        }
        else
        {
            CoopSession.On(CoopOp.PlayersState, OnPlayersState);
            CoopSession.On(CoopOp.SpawnBird, OnSpawnBird);
            CoopSession.On(CoopOp.BirdsState, OnBirdsState);
            CoopSession.On(CoopOp.BirdHit, OnBirdHit);
            CoopSession.On(CoopOp.BirdDie, OnBirdDie);
            CoopSession.On(CoopOp.SpawnFish, OnSpawnFish);
            CoopSession.On(CoopOp.FishState, OnFishState);
            CoopSession.On(CoopOp.FishDie, OnFishDie);
            CoopSession.On(CoopOp.DefenderBroken, OnDefenderBroken);
            CoopSession.On(CoopOp.SpawnPickup, OnSpawnPickup);
            CoopSession.On(CoopOp.RemovePickup, OnRemovePickup);
            CoopSession.On(CoopOp.Director, OnDirector);
            CoopSession.On(CoopOp.Banner, OnBanner);
            CoopSession.On(CoopOp.Toast, OnToast);
            CoopSession.On(CoopOp.Alert, OnAlert);
            CoopSession.On(CoopOp.Sound, OnSound);
            CoopSession.On(CoopOp.DamagePlayer, OnDamagePlayer);
            CoopSession.On(CoopOp.KnockPlayer, OnKnockPlayer);
            CoopSession.On(CoopOp.CurrentPlayer, OnCurrentPlayer);
            CoopSession.On(CoopOp.Shot, OnShotFromHost);
            CoopSession.On(CoopOp.EnemyShot, OnEnemyShot);
            CoopSession.On(CoopOp.GameOver, OnGameOver);
            CoopSession.On(CoopOp.GroupIncoming, OnGroupIncoming);
            CoopSession.On(CoopOp.Revive, OnRevive);
        }
    }

    void Start()
    {
        local = director != null && director.player != null ? director.player : PlayerController_Base.FindLocal();
        if (local != null)
        {
            local.NetSlot = CoopSession.LocalSlot;
            localShooting = local.GetComponent<PlayerShooting>();
        }
        if (host)
        {
            director.Banner += OnHostBanner;
            director.Toast += OnHostToast;
            PlayerController_Base.ProxyHit += OnProxyHit;
            PlayerController_Base.ProxyKnockback += OnProxyKnockback;
            PlayerController_Base.ProxyCurrent += OnProxyCurrent;
            CoopSession.SlotArrivedInScene += OnSlotArrived;
        }
        CoopSession.PlayerLeft += OnPlayerLeft;
        CoopSession.RosterChanged += OnRosterChanged;
        BuildTeamHud();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (host)
        {
            CoopSession.Off(CoopOp.PlayerState, OnPlayerState);
            CoopSession.Off(CoopOp.HitBird, OnHitBird);
            CoopSession.Off(CoopOp.TakePickup, OnTakePickup);
            CoopSession.Off(CoopOp.Shot, OnShotFromClient);
            if (director != null)
            {
                director.Banner -= OnHostBanner;
                director.Toast -= OnHostToast;
            }
            PlayerController_Base.ProxyHit -= OnProxyHit;
            PlayerController_Base.ProxyKnockback -= OnProxyKnockback;
            PlayerController_Base.ProxyCurrent -= OnProxyCurrent;
            CoopSession.SlotArrivedInScene -= OnSlotArrived;
        }
        else
        {
            CoopSession.Off(CoopOp.PlayersState, OnPlayersState);
            CoopSession.Off(CoopOp.SpawnBird, OnSpawnBird);
            CoopSession.Off(CoopOp.BirdsState, OnBirdsState);
            CoopSession.Off(CoopOp.BirdHit, OnBirdHit);
            CoopSession.Off(CoopOp.BirdDie, OnBirdDie);
            CoopSession.Off(CoopOp.SpawnFish, OnSpawnFish);
            CoopSession.Off(CoopOp.FishState, OnFishState);
            CoopSession.Off(CoopOp.FishDie, OnFishDie);
            CoopSession.Off(CoopOp.DefenderBroken, OnDefenderBroken);
            CoopSession.Off(CoopOp.SpawnPickup, OnSpawnPickup);
            CoopSession.Off(CoopOp.RemovePickup, OnRemovePickup);
            CoopSession.Off(CoopOp.Director, OnDirector);
            CoopSession.Off(CoopOp.Banner, OnBanner);
            CoopSession.Off(CoopOp.Toast, OnToast);
            CoopSession.Off(CoopOp.Alert, OnAlert);
            CoopSession.Off(CoopOp.Sound, OnSound);
            CoopSession.Off(CoopOp.DamagePlayer, OnDamagePlayer);
            CoopSession.Off(CoopOp.KnockPlayer, OnKnockPlayer);
            CoopSession.Off(CoopOp.CurrentPlayer, OnCurrentPlayer);
            CoopSession.Off(CoopOp.Shot, OnShotFromHost);
            CoopSession.Off(CoopOp.EnemyShot, OnEnemyShot);
            CoopSession.Off(CoopOp.GameOver, OnGameOver);
            CoopSession.Off(CoopOp.GroupIncoming, OnGroupIncoming);
            CoopSession.Off(CoopOp.Revive, OnRevive);
        }
        CoopSession.PlayerLeft -= OnPlayerLeft;
        CoopSession.RosterChanged -= OnRosterChanged;
    }

    void Update()
    {
        if (!CoopSession.Active) return;
        // Tiempo real: la red sigue aunque el juego vaya en cámara lenta
        float dt = Time.unscaledDeltaTime;
        if (host)
        {
            if ((scanTimer -= dt) <= 0f) { scanTimer = ScanRate; ScanNewEntities(); }
            if ((playerTimer -= dt) <= 0f) { playerTimer = PlayerRate; CaptureLocal(); SendPlayers(); }
            if ((birdTimer -= dt) <= 0f) { birdTimer = BirdRate; SendBirds(); }
            if ((fishTimer -= dt) <= 0f) { fishTimer = FishRate; SendFish(); }
            if ((dirTimer -= dt) <= 0f) { dirTimer = DirectorRate; SendDirector(ulong.MaxValue); }
            if ((retargetTimer -= dt) <= 0f) { retargetTimer = RetargetRate; Retarget(); }
            if ((currentTimer -= dt) <= 0f) { currentTimer = CurrentRate; FlushCurrents(); }
        }
        else if ((playerTimer -= dt) <= 0f)
        {
            playerTimer = PlayerRate;
            SendMyState();
        }
        UpdateTeamHud();
    }

    // ======================= JUGADORES =======================

    // Banderas: 1 caído, 2 en el agua, 4 propulsor, 8 turbo, 16 esquive
    PlayerSnap Capture()
    {
        PlayerSnap s = new PlayerSnap();
        if (local == null) return s;
        s.valid = true;
        s.pos = local.transform.position;
        s.body = local.transform.rotation;
        s.model = local.modeloPez != null ? local.modeloPez.rotation : local.transform.rotation;
        s.vel = local.Velocity;
        byte f = 0;
        if (local.isDead) f |= 1;
        if (local.isInWater) f |= 2;
        if (local.IsJetting) f |= 4;
        if (local.IsTurbo) f |= 8;
        if (local.IsDodging) f |= 16;
        s.flags = f;
        s.armor = CoopWire.Unit(local.maxArmor > 0f ? local.currentArmor / local.maxArmor : 0f);
        return s;
    }

    void CaptureLocal()
    {
        int slot = CoopSession.LocalSlot;
        if (slot >= 0) snaps[slot] = Capture();
    }

    static void WriteSnap(FastBufferWriter w, byte slot, PlayerSnap s)
    {
        w.WriteValueSafe(slot);
        w.WriteValueSafe(s.pos);
        CoopWire.WriteEuler(w, s.body);
        w.WriteValueSafe(s.model);
        w.WriteValueSafe(s.vel);
        w.WriteValueSafe(s.flags);
        w.WriteValueSafe(s.armor);
    }

    static PlayerSnap ReadSnap(FastBufferReader r, out byte slot)
    {
        PlayerSnap s = new PlayerSnap { valid = true };
        r.ReadValueSafe(out slot);
        r.ReadValueSafe(out s.pos);
        s.body = CoopWire.ReadEuler(r);
        r.ReadValueSafe(out s.model);
        r.ReadValueSafe(out s.vel);
        r.ReadValueSafe(out s.flags);
        r.ReadValueSafe(out s.armor);
        return s;
    }

    void SendMyState()
    {
        int slot = CoopSession.LocalSlot;
        if (local == null || slot < 0) return;
        PlayerSnap s = Capture();
        CoopSession.SendToHost(CoopOp.PlayerState, w => WriteSnap(w, (byte)slot, s), NetworkDelivery.UnreliableSequenced);
    }

    void OnPlayerState(ulong sender, FastBufferReader r)
    {
        int slot = CoopSession.SlotOf(sender);
        byte ignored;
        PlayerSnap s = ReadSnap(r, out ignored);
        if (slot < 0) return;
        snaps[slot] = s;
        ApplyRemote(slot, s);
    }

    void SendPlayers()
    {
        int count = 0;
        for (int i = 0; i < snaps.Length; i++) if (snaps[i].valid && CoopSession.ClientOf(i) != ulong.MaxValue) count++;
        if (count == 0) return;
        CoopSession.SendToClients(CoopOp.PlayersState, w =>
        {
            w.WriteValueSafe((byte)count);
            for (int i = 0; i < snaps.Length; i++)
                if (snaps[i].valid && CoopSession.ClientOf(i) != ulong.MaxValue) WriteSnap(w, (byte)i, snaps[i]);
        }, NetworkDelivery.UnreliableSequenced);
    }

    void OnPlayersState(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte count);
        for (int i = 0; i < count; i++)
        {
            byte slot;
            PlayerSnap s = ReadSnap(r, out slot);
            if (slot != CoopSession.LocalSlot && slot < CoopSession.MaxPlayers) ApplyRemote(slot, s);
        }
    }

    void ApplyRemote(int slot, PlayerSnap s)
    {
        if (slot < 0 || slot >= CoopSession.MaxPlayers || slot == CoopSession.LocalSlot) return;
        if (CoopSession.ClientOf(slot) == ulong.MaxValue) return;
        if (remotes[slot] == null) remotes[slot] = CoopRemotePlayer.Create(slot, CoopSession.NameOf(slot), local);
        remotes[slot].ApplyState(s.pos, s.body, s.model, s.vel, s.flags, CoopWire.Unit(s.armor));
    }

    void OnPlayerLeft(int slot)
    {
        if (slot < 0 || slot >= CoopSession.MaxPlayers) return;
        snaps[slot] = new PlayerSnap();
        CoopRemotePlayer rp = remotes[slot];
        if (rp == null) return;
        // Las aves que lo perseguían pasan a otro jugador antes de que desaparezca
        if (host && rp.Proxy != null)
        {
            foreach (BirdAI b in BirdAI.All)
                if (b != null && b.Target == rp.Proxy) b.SetTarget(PickTarget(b.GetInstanceID()) ?? local);
        }
        remotes[slot] = null;
        Destroy(rp.gameObject);
    }

    void OnRosterChanged()
    {
        for (int i = 0; i < remotes.Length; i++)
            if (remotes[i] != null) remotes[i].SetName(CoopSession.NameOf(i));
    }

    // Anfitrión: a quién persigue un ave nueva (se reparten entre los que siguen vivos)
    public PlayerController_Base PickTarget(int index)
    {
        CollectAlive();
        if (aliveBuffer.Count == 0) return null;
        return aliveBuffer[Mathf.Abs(index) % aliveBuffer.Count];
    }

    void CollectAlive()
    {
        aliveBuffer.Clear();
        if (local != null && !local.isDead) aliveBuffer.Add(local);
        foreach (CoopRemotePlayer rp in remotes)
            if (rp != null && rp.HasState && !rp.Dead && rp.Proxy != null) aliveBuffer.Add(rp.Proxy);
    }

    // ¿Cayeron todos? (los que todavía no mandaron su estado cuentan como vivos)
    public bool AllPlayersDead
    {
        get
        {
            if (local != null && !local.isDead) return false;
            for (int i = 0; i < CoopSession.MaxPlayers; i++)
            {
                if (i == CoopSession.LocalSlot || CoopSession.ClientOf(i) == ulong.MaxValue) continue;
                if (remotes[i] == null || !remotes[i].Dead) return false;
            }
            return true;
        }
    }

    // Anfitrión: cada ave sigue a alguien vivo; a veces cambia al más cercano
    void Retarget()
    {
        CollectAlive();
        if (aliveBuffer.Count == 0) return;
        foreach (BirdAI b in BirdAI.All)
        {
            if (b == null || !b.IsAlive || b.IsNetPuppet) continue;
            Vector3 p = b.transform.position;
            PlayerController_Base nearest = null;
            float best = float.MaxValue;
            foreach (PlayerController_Base c in aliveBuffer)
            {
                float d = (c.transform.position - p).sqrMagnitude;
                if (d < best) { best = d; nearest = c; }
            }
            PlayerController_Base t = b.Target;
            if (t == null || t.isDead) { b.SetTarget(nearest); continue; }
            float current = (t.transform.position - p).sqrMagnitude;
            if (best < current * 0.25f && current > 625f) b.SetTarget(nearest);
        }
    }

    // Golpes, empujones y corrientes que reciben los compañeros en el anfitrión: van al dueño
    void OnProxyHit(PlayerController_Base proxy, float damage, Vector3 knock)
    {
        ulong client = CoopSession.ClientOf(proxy.NetSlot);
        if (client == ulong.MaxValue) return;
        CoopSession.SendToClient(client, CoopOp.DamagePlayer, w => { w.WriteValueSafe(damage); w.WriteValueSafe(knock); });
    }

    void OnProxyKnockback(PlayerController_Base proxy, Vector3 velocity, float controlLoss)
    {
        ulong client = CoopSession.ClientOf(proxy.NetSlot);
        if (client == ulong.MaxValue) return;
        CoopSession.SendToClient(client, CoopOp.KnockPlayer, w => { w.WriteValueSafe(velocity); w.WriteValueSafe(controlLoss); });
    }

    void OnProxyCurrent(PlayerController_Base proxy, Vector3 velocity)
    {
        int slot = proxy.NetSlot;
        if (slot >= 0 && slot < pendingCurrent.Length) pendingCurrent[slot] += velocity;
    }

    void FlushCurrents()
    {
        for (int i = 0; i < pendingCurrent.Length; i++)
        {
            Vector3 v = pendingCurrent[i];
            if (v.sqrMagnitude < 0.0001f) continue;
            pendingCurrent[i] = Vector3.zero;
            ulong client = CoopSession.ClientOf(i);
            if (client == ulong.MaxValue) continue;
            CoopSession.SendToClient(client, CoopOp.CurrentPlayer, w => w.WriteValueSafe(v), NetworkDelivery.UnreliableSequenced);
        }
    }

    void OnDamagePlayer(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out float damage);
        r.ReadValueSafe(out Vector3 knock);
        if (local != null) local.TakeHit(damage, knock);
    }

    void OnKnockPlayer(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out Vector3 velocity);
        r.ReadValueSafe(out float controlLoss);
        if (local != null) local.ApplyKnockback(velocity, controlLoss);
    }

    void OnCurrentPlayer(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out Vector3 velocity);
        if (local != null) local.AddCurrent(velocity);
    }

    // ======================= ANFITRIÓN: LO QUE APARECE =======================

    void ScanNewEntities()
    {
        birdBuffer.Clear();
        birdBuffer.AddRange(BirdAI.All);
        foreach (BirdAI b in birdBuffer)
        {
            if (b == null || b.NetId >= 0 || b.IsNetPuppet || !b.IsAlive) continue;
            if (CoopCatalog.BirdIndex(b.Type) < 0) continue;
            RegisterHostBird(b);
            BirdAI bird = b;
            CoopSession.SendToClients(CoopOp.SpawnBird, w => WriteBirdSpawn(w, bird));
        }
        fishBuffer.Clear();
        fishBuffer.AddRange(FishAI.All);
        foreach (FishAI f in fishBuffer)
        {
            if (f == null || f.NetId >= 0 || f.IsNetPuppet || !f.IsAlive) continue;
            if (CoopCatalog.FishIndex(f.Type) < 0) continue;
            RegisterHostFish(f);
            FishAI fish = f;
            CoopSession.SendToClients(CoopOp.SpawnFish, w => WriteFishSpawn(w, fish));
        }
        if (director != null)
        {
            foreach (AmmoPickup p in director.Pickups)
            {
                if (p == null || p.NetId >= 0) continue;
                RegisterHostPickup(p);
                AmmoPickup pickup = p;
                CoopSession.SendToClients(CoopOp.SpawnPickup, w => WritePickupSpawn(w, pickup));
            }
        }
    }

    void RegisterHostBird(BirdAI b)
    {
        int id = nextId++;
        b.NetId = id;
        birdsById[id] = b;
        Damageable d = b.GetComponent<Damageable>();
        if (d != null) d.Damaged += (amount, point) => OnHostBirdDamaged(id, amount, point);
        b.Died += OnHostBirdDied;
    }

    void RegisterHostFish(FishAI f)
    {
        int id = nextId++;
        f.NetId = id;
        fishById[id] = f;
        f.Died += OnHostFishDied;
        if (f.Defender != null) f.Defender.SuitBroken += OnHostDefenderBroken;
    }

    void RegisterHostPickup(AmmoPickup p)
    {
        int id = nextId++;
        p.NetId = id;
        pickupsById[id] = p;
        p.Collected += OnHostPickupCollected;
    }

    static void WriteBirdSpawn(FastBufferWriter w, BirdAI b)
    {
        Damageable d = b.GetComponent<Damageable>();
        w.WriteValueSafe(b.NetId);
        w.WriteValueSafe((byte)CoopCatalog.BirdIndex(b.Type));
        w.WriteValueSafe(b.transform.position);
        CoopWire.WriteEuler(w, b.transform.rotation);
        w.WriteValueSafe(d != null ? d.maxHealth : 1);
        w.WriteValueSafe(d != null ? d.CurrentHealth : 1);
    }

    static void WriteFishSpawn(FastBufferWriter w, FishAI f)
    {
        w.WriteValueSafe(f.NetId);
        w.WriteValueSafe((byte)CoopCatalog.FishIndex(f.Type));
        w.WriteValueSafe(f.transform.position);
        CoopWire.WriteEuler(w, f.transform.rotation);
        DefenderFish def = f.Defender;
        w.WriteValueSafe(def != null);
        w.WriteValueSafe((byte)(def != null ? def.SectorIndex : 0));
        w.WriteValueSafe((byte)(def != null ? def.SectorCount : 1));
        w.WriteValueSafe((byte)(def != null ? (int)def.weapon : 0));
        w.WriteValueSafe(def != null && def.SuitIntact);
    }

    static void WritePickupSpawn(FastBufferWriter w, AmmoPickup p)
    {
        w.WriteValueSafe(p.NetId);
        w.WriteValueSafe(p.transform.position);
        w.WriteValueSafe(p.IsUnderwater);
    }

    // Alguien llegó al lago: se le manda todo lo que ya hay
    void OnSlotArrived(int slot, string scene)
    {
        if (scene != gameObject.scene.name || slot == CoopSession.LocalSlot) return;
        ulong client = CoopSession.ClientOf(slot);
        if (client == ulong.MaxValue) return;
        ScanNewEntities();
        foreach (KeyValuePair<int, BirdAI> kv in birdsById)
        {
            BirdAI b = kv.Value;
            if (b == null || !b.IsAlive) continue;
            CoopSession.SendToClient(client, CoopOp.SpawnBird, w => WriteBirdSpawn(w, b));
        }
        foreach (KeyValuePair<int, FishAI> kv in fishById)
        {
            FishAI f = kv.Value;
            if (f == null || !f.IsAlive) continue;
            CoopSession.SendToClient(client, CoopOp.SpawnFish, w => WriteFishSpawn(w, f));
        }
        foreach (KeyValuePair<int, AmmoPickup> kv in pickupsById)
        {
            AmmoPickup p = kv.Value;
            if (p == null) continue;
            CoopSession.SendToClient(client, CoopOp.SpawnPickup, w => WritePickupSpawn(w, p));
        }
        SendDirector(client);
    }

    void OnHostBirdDamaged(int id, int amount, Vector3 point)
    {
        if (processingSlot < 0) lastHitter[id] = CoopSession.LocalSlot;
        short shown = (short)Mathf.Clamp(amount, 0, short.MaxValue);
        CoopSession.SendToClients(CoopOp.BirdHit, w =>
        {
            w.WriteValueSafe(id);
            w.WriteValueSafe(shown);
            w.WriteValueSafe(point);
        }, NetworkDelivery.Unreliable, processingSender);
    }

    void OnHostBirdDied(BirdAI b)
    {
        if (b == null) return;
        int id = b.NetId;
        int killer;
        if (!lastHitter.TryGetValue(id, out killer)) killer = -1;
        birdsById.Remove(id);
        lastHitter.Remove(id);
        sbyte k = (sbyte)killer;
        CoopSession.SendToClients(CoopOp.BirdDie, w => { w.WriteValueSafe(id); w.WriteValueSafe(k); });
    }

    void OnHostFishDied(FishAI f)
    {
        if (f == null) return;
        int id = f.NetId;
        bool eaten = f.LastKillEaten;
        fishById.Remove(id);
        CoopSession.SendToClients(CoopOp.FishDie, w => { w.WriteValueSafe(id); w.WriteValueSafe(eaten); });
    }

    void OnHostDefenderBroken(DefenderFish d)
    {
        if (d == null || d.Fish == null) return;
        int id = d.Fish.NetId;
        CoopSession.SendToClients(CoopOp.DefenderBroken, w => w.WriteValueSafe(id));
    }

    void OnHostPickupCollected(AmmoPickup p)
    {
        if (p == null) return;
        int id = p.NetId;
        pickupsById.Remove(id);
        CoopSession.SendToClients(CoopOp.RemovePickup, w => w.WriteValueSafe(id));
    }

    // ======================= ANFITRIÓN: CADA RATO =======================

    void SendBirds()
    {
        birdBuffer.Clear();
        foreach (KeyValuePair<int, BirdAI> kv in birdsById)
            if (kv.Value != null && kv.Value.IsAlive) birdBuffer.Add(kv.Value);
        for (int start = 0; start < birdBuffer.Count; start += BirdsPerPacket)
        {
            int from = start;
            int count = Mathf.Min(BirdsPerPacket, birdBuffer.Count - start);
            CoopSession.SendToClients(CoopOp.BirdsState, w =>
            {
                w.WriteValueSafe((byte)count);
                for (int i = from; i < from + count; i++)
                {
                    BirdAI b = birdBuffer[i];
                    w.WriteValueSafe(b.NetId);
                    w.WriteValueSafe(b.transform.position);
                    CoopWire.WriteEuler(w, b.transform.rotation);
                    w.WriteValueSafe(b.NetFlags);
                    w.WriteValueSafe(CoopWire.Unit(b.Health01));
                }
            }, NetworkDelivery.UnreliableSequenced);
        }
    }

    void SendFish()
    {
        fishBuffer.Clear();
        foreach (KeyValuePair<int, FishAI> kv in fishById)
            if (kv.Value != null && kv.Value.IsAlive) fishBuffer.Add(kv.Value);
        for (int start = 0; start < fishBuffer.Count; start += FishPerPacket)
        {
            int from = start;
            int count = Mathf.Min(FishPerPacket, fishBuffer.Count - start);
            CoopSession.SendToClients(CoopOp.FishState, w =>
            {
                w.WriteValueSafe((byte)count);
                for (int i = from; i < from + count; i++)
                {
                    FishAI f = fishBuffer[i];
                    w.WriteValueSafe(f.NetId);
                    w.WriteValueSafe(f.transform.position);
                    CoopWire.WriteEuler(w, f.transform.rotation);
                    w.WriteValueSafe(f.NetFlags);
                }
            }, NetworkDelivery.UnreliableSequenced);
        }
    }

    void SendDirector(ulong onlyClient)
    {
        if (director == null) return;
        byte state = (byte)director.State;
        short wave = (short)director.CurrentWave;
        float countdown = director.Countdown;
        short totalFish = (short)director.TotalFish;
        short fishLost = (short)director.FishLost;
        int killed = director.BirdsKilled;
        short rescued = (short)director.FishRescued;
        byte pending = (byte)Mathf.Clamp(director.PendingGroups, 0, 255);
        float time = LakeAmbience.TimeOfDay;
        Action<FastBufferWriter> write = w =>
        {
            w.WriteValueSafe(state);
            w.WriteValueSafe(wave);
            w.WriteValueSafe(countdown);
            w.WriteValueSafe(totalFish);
            w.WriteValueSafe(fishLost);
            w.WriteValueSafe(killed);
            w.WriteValueSafe(rescued);
            w.WriteValueSafe(pending);
            w.WriteValueSafe(time);
        };
        if (onlyClient == ulong.MaxValue) CoopSession.SendToClients(CoopOp.Director, write, NetworkDelivery.UnreliableSequenced);
        else CoopSession.SendToClient(onlyClient, CoopOp.Director, write);
    }

    // ======================= ANFITRIÓN: LO QUE MANDAN LOS CLIENTES =======================

    void OnHitBird(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out int amount);
        r.ReadValueSafe(out Vector3 point);
        r.ReadValueSafe(out Vector3 push);
        int slot = CoopSession.SlotOf(sender);
        BirdAI bird;
        if (!birdsById.TryGetValue(id, out bird) || bird == null || !bird.IsAlive) return;
        Damageable d = bird.GetComponent<Damageable>();
        if (d == null) return;
        processingSender = sender;
        processingSlot = slot;
        lastHitter[id] = slot;
        try
        {
            d.TakeDamage(Mathf.Clamp(amount, 0, 5000), point);
            if (bird != null && bird.IsAlive)
            {
                Rigidbody body = bird.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic && push.sqrMagnitude > 0.0001f)
                    body.AddForceAtPosition(push * bird.ProjectilePushScale, point, ForceMode.Impulse);
                // A veces va por quien le disparó
                if (slot >= 0 && remotes[slot] != null && remotes[slot].Proxy != null && !remotes[slot].Dead && UnityEngine.Random.value < 0.5f)
                    bird.SetTarget(remotes[slot].Proxy);
            }
        }
        finally
        {
            processingSender = ulong.MaxValue;
            processingSlot = -1;
        }
    }

    void OnTakePickup(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        AmmoPickup p;
        if (!pickupsById.TryGetValue(id, out p)) return;
        pickupsById.Remove(id);
        if (p != null)
        {
            p.Collected -= OnHostPickupCollected;
            if (director != null) director.HostPickupTakenRemotely(p);
            p.NetRemove(true);
        }
        CoopSession.SendToClients(CoopOp.RemovePickup, w => w.WriteValueSafe(id), NetworkDelivery.ReliableSequenced, sender);
    }

    // ======================= DISPAROS (solo se ven) =======================

    // Lo llama Projectile al salir un disparo de verdad (de este jugador o de un defensor en el anfitrión)
    public static void NotifyProjectile(Projectile p, Vector3 velocity)
    {
        CoopGame g = Instance;
        if (g == null || p == null || !CoopSession.Active) return;
        byte flags = (byte)((p.IsCharged ? 1 : 0) | (p.IsBubble ? 2 : 0));
        Vector3 pos = p.transform.position;
        Color color = p.ShotColor;
        float scale = p.transform.localScale.x;
        Action<FastBufferWriter> write = w =>
        {
            w.WriteValueSafe(pos);
            w.WriteValueSafe(velocity);
            w.WriteValueSafe(flags);
            CoopWire.WriteColor(w, color);
            w.WriteValueSafe(scale);
        };
        if (g.host) CoopSession.SendToClients(CoopOp.Shot, write, NetworkDelivery.Unreliable);
        else CoopSession.SendToHost(CoopOp.Shot, write, NetworkDelivery.Unreliable);
    }

    void OnShotFromClient(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out Vector3 pos);
        r.ReadValueSafe(out Vector3 vel);
        r.ReadValueSafe(out byte flags);
        Color color = CoopWire.ReadColor(r);
        r.ReadValueSafe(out float scale);
        if (localShooting != null) localShooting.SpawnReplica(pos, vel, (flags & 1) != 0, (flags & 2) != 0, color, scale);
        CoopSession.SendToClients(CoopOp.Shot, w =>
        {
            w.WriteValueSafe(pos);
            w.WriteValueSafe(vel);
            w.WriteValueSafe(flags);
            CoopWire.WriteColor(w, color);
            w.WriteValueSafe(scale);
        }, NetworkDelivery.Unreliable, sender);
    }

    void OnShotFromHost(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out Vector3 pos);
        r.ReadValueSafe(out Vector3 vel);
        r.ReadValueSafe(out byte flags);
        Color color = CoopWire.ReadColor(r);
        r.ReadValueSafe(out float scale);
        if (localShooting != null) localShooting.SpawnReplica(pos, vel, (flags & 1) != 0, (flags & 2) != 0, color, scale);
    }

    // Lo llama EnemyProjectile.Fire: en el anfitrión, los demás lo ven
    public static void NotifyEnemyProjectile(EnemyProjectile.Kind kind, Vector3 from, Vector3 velocity)
    {
        CoopGame g = Instance;
        if (g == null || !g.host || !CoopSession.Active) return;
        byte k = (byte)kind;
        CoopSession.SendToClients(CoopOp.EnemyShot, w =>
        {
            w.WriteValueSafe(k);
            w.WriteValueSafe(from);
            w.WriteValueSafe(velocity);
        }, NetworkDelivery.Unreliable);
    }

    void OnEnemyShot(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte kind);
        r.ReadValueSafe(out Vector3 from);
        r.ReadValueSafe(out Vector3 velocity);
        EnemyProjectile.Fire((EnemyProjectile.Kind)kind, from, velocity, 0f, null);
    }

    // ======================= CLIENTE: LO QUE MANDA EL ANFITRIÓN =======================

    void OnSpawnBird(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out byte species);
        r.ReadValueSafe(out Vector3 pos);
        Quaternion rot = CoopWire.ReadEuler(r);
        r.ReadValueSafe(out int maxHealth);
        r.ReadValueSafe(out int health);
        if (birdsById.ContainsKey(id) || director == null) return;
        BirdType type = CoopCatalog.Bird(species);
        if (type == null) return;
        BirdAI b = director.CreatePuppetBird(type, pos, rot, health, maxHealth);
        if (b == null) return;
        b.NetId = id;
        birdsById[id] = b;
        Damageable d = b.GetComponent<Damageable>();
        if (d != null) d.NetForward = (amount, point, push) => OnLocalHitPuppet(b, id, amount, point, push);
    }

    // Este jugador le dio a un ave (marioneta): el efecto ya, el daño lo pone el anfitrión
    void OnLocalHitPuppet(BirdAI b, int id, int amount, Vector3 point, Vector3 push)
    {
        if (b != null) b.PuppetHitFx(amount, point);
        CoopSession.SendToHost(CoopOp.HitBird, w =>
        {
            w.WriteValueSafe(id);
            w.WriteValueSafe(amount);
            w.WriteValueSafe(point);
            w.WriteValueSafe(push);
        });
    }

    void OnBirdsState(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte count);
        for (int i = 0; i < count; i++)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 pos);
            Quaternion rot = CoopWire.ReadEuler(r);
            r.ReadValueSafe(out byte flags);
            r.ReadValueSafe(out byte health);
            BirdAI b;
            if (birdsById.TryGetValue(id, out b) && b != null) b.PuppetState(pos, rot, flags, CoopWire.Unit(health));
        }
    }

    void OnBirdHit(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out short amount);
        r.ReadValueSafe(out Vector3 point);
        BirdAI b;
        if (birdsById.TryGetValue(id, out b) && b != null) b.PuppetHitFx(amount, point);
    }

    void OnBirdDie(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out sbyte killer);
        BirdAI b;
        if (!birdsById.TryGetValue(id, out b)) return;
        birdsById.Remove(id);
        if (b == null) return;
        // El Archivo de aves cuenta las que derribó este jugador
        if (killer == CoopSession.LocalSlot && b.Type != null) SaveSystem.AddBirdKill(b.Type.name);
        b.PuppetDie();
    }

    void OnSpawnFish(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out byte typeIndex);
        r.ReadValueSafe(out Vector3 pos);
        Quaternion rot = CoopWire.ReadEuler(r);
        r.ReadValueSafe(out bool isDefender);
        r.ReadValueSafe(out byte defIndex);
        r.ReadValueSafe(out byte defCount);
        r.ReadValueSafe(out byte weapon);
        r.ReadValueSafe(out bool suitIntact);
        if (fishById.ContainsKey(id) || director == null) return;
        FishType type = CoopCatalog.Fish(typeIndex);
        if (type == null) return;
        FishAI f = isDefender
            ? director.CreatePuppetDefender(type, defIndex, defCount, (DefenderFish.Weapon)weapon, pos, rot)
            : director.CreatePuppetFish(type, pos, rot);
        if (f == null) return;
        f.NetId = id;
        fishById[id] = f;
        if (isDefender && !suitIntact && f.Defender != null) f.Defender.PuppetBreakSuit();
    }

    void OnFishState(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte count);
        for (int i = 0; i < count; i++)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 pos);
            Quaternion rot = CoopWire.ReadEuler(r);
            r.ReadValueSafe(out byte flags);
            FishAI f;
            if (fishById.TryGetValue(id, out f) && f != null) f.PuppetState(pos, rot, flags);
        }
    }

    void OnFishDie(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out bool eaten);
        FishAI f;
        if (!fishById.TryGetValue(id, out f)) return;
        fishById.Remove(id);
        if (f != null) f.PuppetKill(eaten);
    }

    void OnDefenderBroken(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        FishAI f;
        if (fishById.TryGetValue(id, out f) && f != null && f.Defender != null) f.Defender.PuppetBreakSuit();
    }

    void OnSpawnPickup(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        r.ReadValueSafe(out Vector3 pos);
        r.ReadValueSafe(out bool underwater);
        if (pickupsById.ContainsKey(id) || director == null) return;
        AmmoPickup p = director.CreatePuppetPickup(pos, underwater);
        if (p == null) return;
        p.NetId = id;
        pickupsById[id] = p;
        p.Collected += OnClientPickupCollected;
    }

    // Este jugador recogió una cápsula: la munición ya la tiene; el anfitrión la saca para los demás
    void OnClientPickupCollected(AmmoPickup p)
    {
        if (p == null) return;
        int id = p.NetId;
        pickupsById.Remove(id);
        if (director != null) director.LocalToast("¡MUNICIÓN LLENA!", GameDirector.Cyan);
        CoopSession.SendToHost(CoopOp.TakePickup, w => w.WriteValueSafe(id));
    }

    void OnRemovePickup(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int id);
        AmmoPickup p;
        if (!pickupsById.TryGetValue(id, out p)) return;
        pickupsById.Remove(id);
        if (p != null)
        {
            p.Collected -= OnClientPickupCollected;
            p.NetRemove(true);
        }
    }

    void OnDirector(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte state);
        r.ReadValueSafe(out short wave);
        r.ReadValueSafe(out float countdown);
        r.ReadValueSafe(out short totalFish);
        r.ReadValueSafe(out short fishLost);
        r.ReadValueSafe(out int killed);
        r.ReadValueSafe(out short rescued);
        r.ReadValueSafe(out byte pending);
        r.ReadValueSafe(out float time);
        if (director != null)
            director.MirrorState((GameDirector.GameState)state, wave, countdown, totalFish, fishLost, killed, rescued, pending);
        SurvivalRun.TimeOfDay = time;
        LakeAmbience.SyncTime(time);
    }

    // ---- Carteles, avisos y sonidos ----

    void OnHostBanner(string title, string sub, Color color)
    {
        CoopSession.SendToClients(CoopOp.Banner, w =>
        {
            CoopWire.WriteText(w, title);
            CoopWire.WriteText(w, sub);
            CoopWire.WriteColor(w, color);
        });
    }

    void OnHostToast(string text, Color color)
    {
        if (GameDirector.IsLocalOnlyMessage) return;
        CoopSession.SendToClients(CoopOp.Toast, w =>
        {
            CoopWire.WriteText(w, text);
            CoopWire.WriteColor(w, color);
        });
    }

    public void HostSound(string sound, float volume, float pitch)
    {
        if (!host) return;
        CoopSession.SendToClients(CoopOp.Sound, w =>
        {
            CoopWire.WriteText(w, sound);
            w.WriteValueSafe(volume);
            w.WriteValueSafe(pitch);
        }, NetworkDelivery.Unreliable);
    }

    public void HostAlert(string text, float seconds)
    {
        if (!host) return;
        CoopSession.SendToClients(CoopOp.Alert, w =>
        {
            CoopWire.WriteText(w, text);
            w.WriteValueSafe(seconds);
        });
    }

    void OnBanner(ulong sender, FastBufferReader r)
    {
        string title = CoopWire.ReadText(r);
        string sub = CoopWire.ReadText(r);
        Color color = CoopWire.ReadColor(r);
        if (director != null) director.RaiseBanner(title, sub, color);
    }

    void OnToast(ulong sender, FastBufferReader r)
    {
        string text = CoopWire.ReadText(r);
        Color color = CoopWire.ReadColor(r);
        if (director != null) director.RaiseToast(text, color);
    }

    void OnAlert(ulong sender, FastBufferReader r)
    {
        string text = CoopWire.ReadText(r);
        r.ReadValueSafe(out float seconds);
        HelmetVisorHUD.ShowAlert(text, seconds);
    }

    void OnSound(ulong sender, FastBufferReader r)
    {
        string sound = CoopWire.ReadText(r);
        r.ReadValueSafe(out float volume);
        r.ReadValueSafe(out float pitch);
        Sfx.Play2D(sound, volume, pitch);
    }

    public void HostGroupIncoming(float yaw, bool boss, int count)
    {
        if (!host) return;
        byte c = (byte)Mathf.Clamp(count, 0, 255);
        CoopSession.SendToClients(CoopOp.GroupIncoming, w =>
        {
            w.WriteValueSafe(yaw);
            w.WriteValueSafe(boss);
            w.WriteValueSafe(c);
        });
    }

    void OnGroupIncoming(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out float yaw);
        r.ReadValueSafe(out bool boss);
        r.ReadValueSafe(out byte count);
        if (director != null) director.AnnounceGroupLocal(yaw, boss, count);
    }

    // ---- Fin de la partida ----

    public void HostGameOver()
    {
        if (!host || director == null) return;
        int wave = director.CurrentWave;
        int cleared = SurvivalRun.WavesCleared;
        int killed = director.BirdsKilled;
        int rescued = director.FishRescued;
        int alive = director.FishAlive;
        int total = director.TotalFish;
        CoopSession.SendToClients(CoopOp.GameOver, w =>
        {
            w.WriteValueSafe(wave);
            w.WriteValueSafe(cleared);
            w.WriteValueSafe(killed);
            w.WriteValueSafe(rescued);
            w.WriteValueSafe(alive);
            w.WriteValueSafe(total);
        });
    }

    void OnGameOver(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int wave);
        r.ReadValueSafe(out int cleared);
        r.ReadValueSafe(out int killed);
        r.ReadValueSafe(out int rescued);
        r.ReadValueSafe(out int alive);
        r.ReadValueSafe(out int total);
        if (director != null) director.MirrorGameOver(wave, cleared, killed, rescued, alive, total);
    }

    // ======================= ESPECTADOR Y EQUIPO =======================

    int spectateSlot = -1;
    Vector3 specPos;
    Quaternion specRot;
    bool spectating;
    Canvas hudCanvas;
    readonly TextMeshProUGUI[] teamNames = new TextMeshProUGUI[CoopSession.MaxPlayers];
    readonly Image[] teamBars = new Image[CoopSession.MaxPlayers];
    readonly Image[] teamBack = new Image[CoopSession.MaxPlayers];
    TextMeshProUGUI spectateText;

    void LateUpdate()
    {
        if (local == null || !local.isDead || local.PlayerCamera == null || !CoopSession.Active)
        {
            spectating = false;
            if (spectateText != null && spectateText.gameObject.activeSelf) spectateText.gameObject.SetActive(false);
            return;
        }
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)) spectateSlot = NextAlive(spectateSlot);
        if (spectateSlot < 0 || remotes[spectateSlot] == null || remotes[spectateSlot].Dead) spectateSlot = NextAlive(spectateSlot);
        if (spectateSlot < 0)
        {
            if (spectateText != null) spectateText.gameObject.SetActive(false);
            return;
        }
        Transform target = remotes[spectateSlot].transform;
        Vector3 back = target.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 0.01f) back = Vector3.forward;
        Vector3 desired = target.position - back.normalized * 7f + Vector3.up * 3f;
        Quaternion look = Quaternion.LookRotation(target.position + Vector3.up * 0.6f - desired, Vector3.up);
        float k = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
        if (!spectating)
        {
            specPos = desired;
            specRot = look;
            spectating = true;
        }
        specPos = Vector3.Lerp(specPos, desired, k);
        specRot = Quaternion.Slerp(specRot, look, k);
        Transform cam = local.PlayerCamera.transform;
        cam.SetPositionAndRotation(specPos, specRot);
        if (spectateText != null)
        {
            spectateText.gameObject.SetActive(true);
            spectateText.text = "MIRANDO A " + remotes[spectateSlot].PlayerName.ToUpperInvariant() +
                                "  ·  clic para cambiar  ·  vuelves a la pelea al terminar la oleada";
        }
    }

    // ======================= LOS CAÍDOS VUELVEN =======================

    // Anfitrión: al terminar una oleada, los que cayeron vuelven a la pelea con parte de la armadura. Devuelve cuántos
    public int HostReviveFallen(float armor01)
    {
        int fallen = 0;
        if (local != null && local.isDead)
        {
            fallen++;
            ReviveLocal(armor01);
        }
        for (int i = 0; i < remotes.Length; i++)
            if (remotes[i] != null && remotes[i].HasState && remotes[i].Dead) fallen++;
        if (fallen > 0)
        {
            byte a = CoopWire.Unit(armor01);
            CoopSession.SendToClients(CoopOp.Revive, w => w.WriteValueSafe(a));
        }
        return fallen;
    }

    void OnRevive(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte a);
        if (local != null && local.isDead) ReviveLocal(CoopWire.Unit(a));
    }

    // Vuelve junto a un compañero vivo (o en el centro del lago), un poco sobre el agua
    void ReviveLocal(float armor01)
    {
        LakeVolume lake = LakeVolume.Instance;
        Vector3 near = lake != null && lake.IsValid ? lake.Center : local.transform.position;
        for (int i = 0; i < remotes.Length; i++)
        {
            if (remotes[i] == null || remotes[i].Dead || !remotes[i].HasState) continue;
            near = remotes[i].transform.position;
            break;
        }
        Vector3 pos = near + new Vector3(UnityEngine.Random.Range(-5f, 5f), 0f, UnityEngine.Random.Range(-5f, 5f));
        float surface = lake != null && lake.IsValid ? lake.SurfaceY : pos.y;
        pos.y = surface + 3f;
        if (lake != null && lake.IsValid) pos.y = Mathf.Max(pos.y, lake.GroundHeight(pos.x, pos.z) + 3f);
        local.Revive(armor01, pos);
        spectating = false;
        if (director != null)
            director.LocalToast("¡VUELVES A LA PELEA!  Traje al " + Mathf.RoundToInt(armor01 * 100f) + " %", GameDirector.Cyan);
    }

    int NextAlive(int from)
    {
        for (int step = 1; step <= CoopSession.MaxPlayers; step++)
        {
            int i = ((from < 0 ? -1 : from) + step + CoopSession.MaxPlayers) % CoopSession.MaxPlayers;
            if (remotes[i] != null && !remotes[i].Dead && remotes[i].HasState) return i;
        }
        return -1;
    }

    // Lista de compañeros arriba a la derecha: nombre, armadura y si cayeron
    void BuildTeamHud()
    {
        hudCanvas = MenuUI.CreateCanvas("EquipoCanvas", 40);
        RectTransform root = (RectTransform)hudCanvas.transform;
        for (int i = 0; i < CoopSession.MaxPlayers; i++)
        {
            float y = -150f - i * 34f;
            Color c = CoopSession.SlotColors[i];
            teamNames[i] = MenuUI.Text("Nombre" + i, root, "", 17f, new Vector2(1f, 1f), new Vector2(-150f, y), new Vector2(260f, 24f), c);
            teamNames[i].alignment = TextAlignmentOptions.Right;
            teamBack[i] = MenuUI.Img("Fondo" + i, root, MenuUI.Pixel, new Color(0f, 0.05f, 0.12f, 0.7f), new Vector2(1f, 1f), new Vector2(-90f, y - 15f), new Vector2(140f, 4f));
            teamBars[i] = MenuUI.Img("Barra" + i, root, MenuUI.Pixel, c, new Vector2(1f, 1f), new Vector2(-90f, y - 15f), new Vector2(140f, 4f));
            teamBars[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            teamBars[i].rectTransform.anchoredPosition = new Vector2(-160f, y - 15f);
        }
        spectateText = MenuUI.Text("Espectador", root, "", 20f, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1100f, 30f), MenuUI.Cream);
        spectateText.gameObject.SetActive(false);
    }

    void UpdateTeamHud()
    {
        if (hudCanvas == null) return;
        bool show = !GameDirector.InCinematic;
        for (int i = 0; i < CoopSession.MaxPlayers; i++)
        {
            bool used = CoopSession.ClientOf(i) != ulong.MaxValue && i != CoopSession.LocalSlot;
            bool visible = show && used;
            if (teamNames[i] == null) continue;
            if (teamNames[i].gameObject.activeSelf != visible) teamNames[i].gameObject.SetActive(visible);
            if (teamBars[i].gameObject.activeSelf != visible) teamBars[i].gameObject.SetActive(visible);
            if (teamBack[i].gameObject.activeSelf != visible) teamBack[i].gameObject.SetActive(visible);
            if (!visible) continue;
            CoopRemotePlayer rp = remotes[i];
            bool dead = rp != null && rp.Dead;
            float armor = rp != null ? rp.Armor01 : 1f;
            string n = CoopSession.NameOf(i).ToUpperInvariant();
            teamNames[i].text = dead ? n + "  <color=#FF5A4A>CAÍDO</color>" : n;
            teamBars[i].rectTransform.sizeDelta = new Vector2(140f * (dead ? 0f : armor), 4f);
        }
    }
}

// Lista fija de aves y peces del juego (la misma en todas las computadoras): así basta mandar un número
public static class CoopCatalog
{
    static List<BirdType> birds;
    static List<FishType> fish;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { birds = null; fish = null; }

    static void Ensure()
    {
        if (birds != null) return;
        birds = new List<BirdType>();
        fish = new List<FishType>();
        LevelCatalog catalog = GameSession.Catalog;
        if (catalog == null) return;
        for (int i = 0; i < catalog.Count; i++)
        {
            LevelDefinition level = catalog.Get(i);
            if (level == null) continue;
            if (level.fish != null)
                foreach (FishSpawn f in level.fish)
                    if (f != null && f.type != null && f.type.fish != null && !fish.Contains(f.type.fish)) fish.Add(f.type.fish);
            if (level.waves == null) continue;
            foreach (WaveDefinition w in level.waves)
            {
                if (w == null || w.birds == null) continue;
                foreach (BirdSpawn b in w.birds)
                    if (b != null && b.type != null) AddBird(b.type.bird);
            }
        }
    }

    static void AddBird(BirdType t)
    {
        if (t == null || birds.Contains(t)) return;
        birds.Add(t);
        if (t.reinforcements != null) AddBird(t.reinforcements.bird);
    }

    public static int BirdIndex(BirdType t) { Ensure(); return t != null ? birds.IndexOf(t) : -1; }
    public static BirdType Bird(int i) { Ensure(); return i >= 0 && i < birds.Count ? birds[i] : null; }
    public static int FishIndex(FishType t) { Ensure(); return t != null ? fish.IndexOf(t) : -1; }
    public static FishType Fish(int i) { Ensure(); return i >= 0 && i < fish.Count ? fish[i] : null; }
}
