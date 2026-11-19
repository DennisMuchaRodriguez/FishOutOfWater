using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================================
//  SALA DEL MODO COOPERATIVO (hasta 4 jugadores, con código de sala)
//  - EN LÍNEA (por internet): usa los servicios de Unity (Multiplayer Services: Lobby + Relay). El anfitrión crea la
//    sala y recibe un código de 6 letras; los demás escriben ese código. No hace falta abrir puertos ni saber IPs.
//    Requiere que el proyecto esté vinculado a Unity Cloud (Edit > Project Settings > Services) con Lobby y Relay
//    activados (ver la guía).
//  - RED LOCAL (misma Wi-Fi): sin servicios. El código ("L" + 7 letras) lleva la IP del anfitrión. Sirve para probar
//    con dos computadoras en la misma red o con varias copias del juego en una sola.
//  El anfitrión manda: asigna las casillas (0 = él), decide cuándo empezar, cuándo ir al Taller y cuándo terminar.
//  Todo viaja como mensajes con nombre de Netcode (CoopProtocol). La red (NetworkManager) se crea sola, por código.
// ============================================================================
public class CoopSession : MonoBehaviour
{
    public const int MaxPlayers = 4;
    public const ushort LanPort = 7778;
    public const string PlayerNameKey = "fow_nombre";
    const string MessageName = "FOW";

    public enum Phase { Idle, Connecting, Lobby, InGame }

    // Una casilla de la sala
    public class Slot
    {
        public bool used;
        public ulong clientId;
        public string name = "";
        public bool ready;
        public bool workshopDone;
        public string scene = "";   // escena que ya terminó de cargar (lo sabe el anfitrión)
    }

    // Colores de cada casilla (los mismos de los peces de colores del juego)
    public static readonly Color[] SlotColors =
    {
        new Color(1f, 0.55f, 0.18f, 1f),    // naranja (anfitrión)
        new Color(0.35f, 0.95f, 0.45f, 1f), // verde
        new Color(0.72f, 0.48f, 1f, 1f),    // violeta
        new Color(0.25f, 0.92f, 0.95f, 1f)  // turquesa
    };

    public static CoopSession Instance { get; private set; }
    // En una sala (en el lobby o jugando)
    public static bool Active { get { return Instance != null && (Instance.phase == Phase.Lobby || Instance.phase == Phase.InGame); } }
    public static bool IsHost { get { return Active && Instance.host; } }
    public static bool IsClient { get { return Active && !Instance.host; } }
    public static bool InGame { get { return Instance != null && Instance.phase == Phase.InGame; } }
    public static bool IsConnecting { get { return Instance != null && Instance.phase == Phase.Connecting; } }
    public static int LocalSlot { get { return Instance != null ? Instance.localSlot : -1; } }
    public static string RoomCode { get { return Instance != null ? Instance.roomCode : ""; } }
    public static bool IsLan { get { return Instance != null && Instance.lan; } }
    public static Slot[] Slots { get { return Instance != null ? Instance.slots : null; } }
    public static string LastStatus { get; private set; } = "";
    // Mensaje para mostrar al volver al menú (por qué se cerró la sala)
    public static string PendingMenuMessage;

    public static int PlayerCount
    {
        get
        {
            if (Instance == null) return 0;
            int n = 0;
            foreach (Slot s in Instance.slots) if (s.used) n++;
            return n;
        }
    }

    public static string LocalName
    {
        get
        {
            string n = PlayerPrefs.GetString(PlayerNameKey, "");
            if (string.IsNullOrWhiteSpace(n)) n = "Pez" + UnityEngine.Random.Range(100, 999);
            return n;
        }
        set { PlayerPrefs.SetString(PlayerNameKey, CleanName(value)); }
    }

    public static event Action RosterChanged;
    public static event Action<string> StatusChanged;
    public static event Action<int> PlayerLeft;

    public delegate void Handler(ulong sender, FastBufferReader reader);
    static readonly Dictionary<byte, Handler> handlers = new Dictionary<byte, Handler>();

    readonly Slot[] slots = { new Slot(), new Slot(), new Slot(), new Slot() };
    Phase phase = Phase.Idle;
    bool host, lan, leaving;
    int localSlot = -1;
    string roomCode = "";
    string localName = "";
    ISession session;
    NetworkManager net;
    UnityTransport transport;
    float connectDeadline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        handlers.Clear();
        RosterChanged = null;
        StatusChanged = null;
        PlayerLeft = null;
        LastStatus = "";
        PendingMenuMessage = null;
    }

    static CoopSession Ensure()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("SesionCooperativa");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CoopSession>();
        Instance.RegisterCoreHandlers();
        SceneManager.sceneLoaded += Instance.OnSceneLoaded;
        return Instance;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    static string CleanName(string n)
    {
        n = (n ?? "").Trim();
        if (n.Length > 14) n = n.Substring(0, 14);
        return n;
    }

    void Report(string text)
    {
        LastStatus = text ?? "";
        StatusChanged?.Invoke(LastStatus);
    }

    // ======================= MENSAJES =======================

    // Los componentes del juego se suscriben a un tipo de mensaje
    public static void On(byte op, Handler handler) { handlers[op] = handler; }

    public static void Off(byte op, Handler handler)
    {
        Handler current;
        if (handlers.TryGetValue(op, out current) && current == handler) handlers.Remove(op);
    }

    void RegisterMessageHandler()
    {
        if (net == null || net.CustomMessagingManager == null) return;
        net.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
    }

    void OnNamedMessage(ulong sender, FastBufferReader reader)
    {
        byte op;
        reader.ReadValueSafe(out op);
        Handler h;
        if (!handlers.TryGetValue(op, out h)) return;
        try { h(sender, reader); }
        catch (Exception e) { Debug.LogWarning("Cooperativo: error al leer el mensaje " + op + ": " + e.Message); }
    }

    static bool CanSend
    {
        get
        {
            return Instance != null && Instance.net != null && Instance.net.IsListening &&
                   Instance.net.CustomMessagingManager != null && Instance.phase != Phase.Idle;
        }
    }

    static int MaxSize(NetworkDelivery delivery)
    {
        return delivery == NetworkDelivery.ReliableFragmentedSequenced ? 60000 : 1200;
    }

    static void Send(IReadOnlyList<ulong> targets, ulong single, byte op, Action<FastBufferWriter> write, NetworkDelivery delivery)
    {
        if (!CanSend) return;
        if (targets != null && targets.Count == 0) return;
        FastBufferWriter w = new FastBufferWriter(256, Allocator.Temp, MaxSize(delivery));
        try
        {
            w.WriteValueSafe(op);
            write?.Invoke(w);
            CustomMessagingManager cmm = Instance.net.CustomMessagingManager;
            if (targets != null) cmm.SendNamedMessage(MessageName, targets, w, delivery);
            else cmm.SendNamedMessage(MessageName, single, w, delivery);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Cooperativo: no se pudo enviar el mensaje " + op + ": " + e.Message);
        }
        finally
        {
            w.Dispose();
        }
    }

    // Cliente -> anfitrión (en el anfitrión no hace nada: lo que haga falta se procesa ahí mismo)
    public static void SendToHost(byte op, Action<FastBufferWriter> write, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
    {
        if (!CanSend || Instance.host) return;
        Send(null, NetworkManager.ServerClientId, op, write, delivery);
    }

    // Anfitrión -> todos los clientes (except: un cliente al que no se le manda, por ejemplo quien lo envió)
    static readonly List<ulong> targetBuffer = new List<ulong>(4);

    public static void SendToClients(byte op, Action<FastBufferWriter> write, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced,
                                     ulong except = ulong.MaxValue)
    {
        if (!CanSend || !Instance.host) return;
        targetBuffer.Clear();
        foreach (Slot s in Instance.slots)
            if (s.used && s.clientId != Instance.net.LocalClientId && s.clientId != except) targetBuffer.Add(s.clientId);
        Send(targetBuffer, 0, op, write, delivery);
    }

    // Anfitrión -> un cliente
    public static void SendToClient(ulong clientId, byte op, Action<FastBufferWriter> write, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
    {
        if (!CanSend || !Instance.host || clientId == Instance.net.LocalClientId) return;
        Send(null, clientId, op, write, delivery);
    }

    // Casilla de un cliente (-1 si no está)
    public static int SlotOf(ulong clientId)
    {
        if (Instance == null) return -1;
        for (int i = 0; i < MaxPlayers; i++)
            if (Instance.slots[i].used && Instance.slots[i].clientId == clientId) return i;
        return -1;
    }

    public static ulong ClientOf(int slot)
    {
        if (Instance == null || slot < 0 || slot >= MaxPlayers || !Instance.slots[slot].used) return ulong.MaxValue;
        return Instance.slots[slot].clientId;
    }

    public static string NameOf(int slot)
    {
        if (Instance == null || slot < 0 || slot >= MaxPlayers || !Instance.slots[slot].used) return "";
        return Instance.slots[slot].name;
    }

    // ======================= CREAR Y UNIRSE =======================

    public static void CreateOnline(string playerName) { Ensure().CreateOnlineAsync(playerName); }
    public static void JoinOnline(string code, string playerName) { Ensure().JoinOnlineAsync(code, playerName); }
    public static void CreateLan(string playerName) { Ensure().CreateLanRoom(playerName); }
    public static void JoinLan(string code, string playerName) { Ensure().JoinLanRoom(code, playerName); }

    // Unirse con el código que sea (de red local o en línea)
    public static void Join(string code, string playerName)
    {
        code = (code ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        if (IsLanCode(code)) JoinLan(code, playerName);
        else JoinOnline(code, playerName);
    }

    bool BeginConnect(string playerName, bool asHost, bool asLan)
    {
        if (phase != Phase.Idle)
        {
            Report("Ya estás en una sala.");
            return false;
        }
        localName = CleanName(playerName);
        if (localName == "") localName = LocalName;
        LocalName = localName;
        host = asHost;
        lan = asLan;
        localSlot = -1;
        roomCode = "";
        foreach (Slot s in slots) Clear(s);
        phase = Phase.Connecting;
        return true;
    }

    async void CreateOnlineAsync(string playerName)
    {
        if (!BeginConnect(playerName, true, false)) return;
        try
        {
            Report("Conectando con los servidores de Unity…");
            await EnsureServicesAsync();
            EnsureNetworkManager();
            Report("Creando la sala…");
            SessionOptions options = new SessionOptions
            {
                MaxPlayers = MaxPlayers,
                IsPrivate = true,
                Name = "Fish Out Of Water - " + localName
            }.WithRelayNetwork();
            IHostSession created = await MultiplayerService.Instance.CreateSessionAsync(options);
            session = created;
            HookSession();
            roomCode = created.Code;
            OnNetworkReady();
        }
        catch (Exception e)
        {
            FailConnect(Explain(e));
        }
    }

    async void JoinOnlineAsync(string code, string playerName)
    {
        if (!BeginConnect(playerName, false, false)) return;
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4)
        {
            FailConnect("Escribe el código de la sala.");
            return;
        }
        try
        {
            Report("Conectando con los servidores de Unity…");
            await EnsureServicesAsync();
            EnsureNetworkManager();
            Report("Buscando la sala " + code + "…");
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            HookSession();
            roomCode = code;
            OnNetworkReady();
        }
        catch (Exception e)
        {
            FailConnect(Explain(e));
        }
    }

    void CreateLanRoom(string playerName)
    {
        if (!BeginConnect(playerName, true, true)) return;
        try
        {
            EnsureNetworkManager();
            string ip = LocalIPv4();
            transport.SetConnectionData(ip, LanPort, "0.0.0.0");
            if (!net.StartHost())
            {
                FailConnect("No se pudo abrir la sala en la red local (¿el puerto " + LanPort + " está ocupado?).");
                return;
            }
            roomCode = EncodeLan(ip);
            OnNetworkReady();
        }
        catch (Exception e)
        {
            FailConnect("No se pudo abrir la sala: " + e.Message);
        }
    }

    void JoinLanRoom(string code, string playerName)
    {
        if (!BeginConnect(playerName, false, true)) return;
        string ip;
        if (!TryDecodeLan(code, out ip))
        {
            FailConnect("Ese código de red local no es válido.");
            return;
        }
        try
        {
            EnsureNetworkManager();
            transport.SetConnectionData(ip, LanPort);
            Report("Conectando con " + ip + "…");
            roomCode = code.ToUpperInvariant();
            connectDeadline = Time.unscaledTime + 12f;
            if (!net.StartClient()) FailConnect("No se pudo conectar.");
            // Sigue en OnClientConnected
        }
        catch (Exception e)
        {
            FailConnect("No se pudo conectar: " + e.Message);
        }
    }

    async Task EnsureServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            // Un perfil distinto en cada copia del juego: así dos copias en la misma computadora son jugadores distintos
            InitializationOptions options = new InitializationOptions();
            options.SetProfile("pez" + UnityEngine.Random.Range(10000, 99999));
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    void EnsureNetworkManager()
    {
        if (net == null && NetworkManager.Singleton != null) net = NetworkManager.Singleton;
        if (net == null)
        {
            GameObject go = new GameObject("RedCooperativa");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            transport = go.AddComponent<UnityTransport>();
            net = go.AddComponent<NetworkManager>();
            net.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = true,
                TickRate = 30
            };
            go.SetActive(true);
        }
        if (transport == null) transport = net.GetComponent<UnityTransport>();
        if (transport == null) transport = net.gameObject.AddComponent<UnityTransport>();
        if (net.NetworkConfig == null) net.NetworkConfig = new NetworkConfig();
        net.NetworkConfig.NetworkTransport = transport;
        net.NetworkConfig.EnableSceneManagement = false;
        net.NetworkConfig.ConnectionApproval = true;
        net.ConnectionApprovalCallback = Approve;

        net.OnClientConnectedCallback -= OnClientConnected;
        net.OnClientConnectedCallback += OnClientConnected;
        net.OnClientDisconnectCallback -= OnClientDisconnected;
        net.OnClientDisconnectCallback += OnClientDisconnected;
        net.OnTransportFailure -= OnTransportFailure;
        net.OnTransportFailure += OnTransportFailure;
        net.OnServerStarted -= RegisterMessageHandler;
        net.OnServerStarted += RegisterMessageHandler;
        net.OnClientStarted -= RegisterMessageHandler;
        net.OnClientStarted += RegisterMessageHandler;
    }

    // Solo entran 4, y no a mitad de partida
    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;
        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            return;
        }
        int used = PlayerCount;
        response.Approved = phase == Phase.Lobby && used < MaxPlayers;
        response.Reason = phase == Phase.InGame ? "La partida ya empezó." : "La sala está llena.";
    }

    void HookSession()
    {
        if (session == null) return;
        session.RemovedFromSession += OnRemovedFromSession;
        session.Deleted += OnSessionDeleted;
    }

    void UnhookSession(ISession s)
    {
        if (s == null) return;
        s.RemovedFromSession -= OnRemovedFromSession;
        s.Deleted -= OnSessionDeleted;
    }

    void OnRemovedFromSession() { if (!host) LostConnection("Saliste de la sala."); }
    void OnSessionDeleted() { if (!host) LostConnection("El anfitrión cerró la sala."); }

    // La red quedó lista (anfitrión: sala abierta; cliente: conectado)
    void OnNetworkReady()
    {
        RegisterMessageHandler();
        phase = Phase.Lobby;
        if (host)
        {
            Slot me = slots[0];
            me.used = true;
            me.clientId = net.LocalClientId;
            me.name = localName;
            me.ready = true;
            localSlot = 0;
            Report("");
            BroadcastRoster();
        }
        else
        {
            Report("Entrando a la sala…");
            SendToHost(CoopOp.Hello, w => CoopWire.WriteText(w, localName));
        }
        GoToLobbyScene();
    }

    void FailConnect(string message)
    {
        Debug.LogWarning("Cooperativo: " + message);
        StopNetwork();
        phase = Phase.Idle;
        Report(message);
    }

    static string Explain(Exception e)
    {
        Exception inner = e;
        while (inner is AggregateException && inner.InnerException != null) inner = inner.InnerException;
        SessionException se = inner as SessionException;
        if (se != null)
        {
            switch (se.Error)
            {
                case SessionError.SessionNotFound:
                    return "No existe una sala con ese código (revisa las letras).";
                case SessionError.NotAuthorized:
                case SessionError.Forbidden:
                    return "Unity no dejó usar las salas en línea: revisa que el proyecto esté vinculado a Unity Cloud " +
                           "con Lobby y Relay activados (ver la guía). Mientras tanto puedes usar RED LOCAL.";
                case SessionError.RateLimitExceeded:
                    return "Demasiados intentos seguidos: espera unos segundos.";
            }
            return "No se pudo conectar (" + se.Error + "): " + se.Message;
        }
        string msg = inner.Message ?? "";
        if (msg.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0) return "La sala está llena (máximo 4).";
        if (msg.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("cloud", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Los servicios de Unity no están configurados: vincula el proyecto a Unity Cloud y activa Lobby y Relay " +
                   "(ver la guía). Mientras tanto puedes usar RED LOCAL.";
        return "No se pudo conectar: " + msg;
    }

    void Update()
    {
        // Cliente de red local que no logra conectar
        if (phase == Phase.Connecting && lan && !host && connectDeadline > 0f && Time.unscaledTime > connectDeadline)
        {
            connectDeadline = 0f;
            FailConnect("No se encontró la sala. Revisa el código y que estén en la misma red.");
        }
    }

    // ======================= CONEXIONES =======================

    void OnClientConnected(ulong clientId)
    {
        // Cliente de red local: ya entró
        if (!host && lan && phase == Phase.Connecting && net != null && clientId == net.LocalClientId)
        {
            connectDeadline = 0f;
            OnNetworkReady();
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (net == null) return;
        if (host)
        {
            if (clientId == net.LocalClientId) return;
            int slot = SlotOf(clientId);
            if (slot < 0) return;
            string who = slots[slot].name;
            Clear(slots[slot]);
            BroadcastRoster();
            PlayerLeft?.Invoke(slot);
            if (phase == Phase.InGame && GameDirector.Instance != null)
                GameDirector.Instance.ShowToast(who.ToUpperInvariant() + " SALIÓ DE LA PARTIDA", GameDirector.Danger);
            CheckWorkshopDone();
            return;
        }
        // Cliente: se cortó la conexión con el anfitrión (o nunca se pudo conectar)
        if (clientId == net.LocalClientId || clientId == NetworkManager.ServerClientId)
        {
            if (phase == Phase.Connecting)
            {
                string reason = net.DisconnectReason;
                FailConnect(string.IsNullOrEmpty(reason) ? "No se pudo entrar a la sala." : reason);
            }
            else LostConnection("Se perdió la conexión con el anfitrión.");
        }
    }

    void OnTransportFailure()
    {
        if (phase == Phase.Connecting) FailConnect("Falló la conexión de red.");
        else LostConnection("Falló la conexión de red.");
    }

    void LostConnection(string message)
    {
        if (phase == Phase.Idle || leaving) return;
        LeaveInternal(true, message);
    }

    // ======================= SALA =======================

    static void Clear(Slot s)
    {
        s.used = false;
        s.clientId = 0;
        s.name = "";
        s.ready = false;
        s.workshopDone = false;
        s.scene = "";
    }

    void RegisterCoreHandlers()
    {
        On(CoopOp.Hello, OnHello);
        On(CoopOp.Roster, OnRoster);
        On(CoopOp.Ready, OnReady);
        On(CoopOp.StartGame, OnStartGame);
        On(CoopOp.LoadScene, OnLoadScene);
        On(CoopOp.InScene, OnInScene);
        On(CoopOp.GoWorkshop, OnGoWorkshop);
        On(CoopOp.WorkshopDone, OnWorkshopDone);
        On(CoopOp.BackToLobby, OnBackToLobby);
        On(CoopOp.HostLeaving, OnHostLeaving);
    }

    void OnHello(ulong sender, FastBufferReader r)
    {
        if (!host) return;
        string name = CleanName(CoopWire.ReadText(r));
        if (SlotOf(sender) >= 0) { BroadcastRoster(); return; }
        int free = -1;
        for (int i = 1; i < MaxPlayers; i++) if (!slots[i].used) { free = i; break; }
        if (free < 0 || phase != Phase.Lobby)
        {
            net.DisconnectClient(sender, phase != Phase.Lobby ? "La partida ya empezó." : "La sala está llena.");
            return;
        }
        Slot s = slots[free];
        s.used = true;
        s.clientId = sender;
        s.name = name == "" ? "Pez " + (free + 1) : name;
        s.ready = false;
        BroadcastRoster();
    }

    void BroadcastRoster()
    {
        if (!host) return;
        SendToClients(CoopOp.Roster, w =>
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                Slot s = slots[i];
                w.WriteValueSafe(s.used);
                w.WriteValueSafe(s.clientId);
                CoopWire.WriteText(w, s.name);
                w.WriteValueSafe(s.ready);
                w.WriteValueSafe(s.workshopDone);
            }
        });
        RosterChanged?.Invoke();
    }

    void OnRoster(ulong sender, FastBufferReader r)
    {
        if (host) return;
        localSlot = -1;
        for (int i = 0; i < MaxPlayers; i++)
        {
            Slot s = slots[i];
            r.ReadValueSafe(out s.used);
            r.ReadValueSafe(out s.clientId);
            s.name = CoopWire.ReadText(r);
            r.ReadValueSafe(out s.ready);
            r.ReadValueSafe(out s.workshopDone);
            if (s.used && net != null && s.clientId == net.LocalClientId) localSlot = i;
        }
        if (localSlot >= 0 && LastStatus != "") Report("");
        RosterChanged?.Invoke();
    }

    public static bool LocalReady
    {
        get { return Instance != null && Instance.localSlot >= 0 && Instance.slots[Instance.localSlot].ready; }
    }

    // Cliente: marcar listo / no listo (el anfitrión siempre está listo)
    public static void SetReady(bool ready)
    {
        if (!Active || Instance.host || Instance.localSlot < 0) return;
        Instance.slots[Instance.localSlot].ready = ready;
        SendToHost(CoopOp.Ready, w => w.WriteValueSafe(ready));
        RosterChanged?.Invoke();
    }

    void OnReady(ulong sender, FastBufferReader r)
    {
        if (!host) return;
        r.ReadValueSafe(out bool ready);
        int slot = SlotOf(sender);
        if (slot < 0) return;
        slots[slot].ready = ready;
        BroadcastRoster();
    }

    // ¿Se puede empezar? (anfitrión y todos listos; con 1 solo también, para probar)
    public static bool CanStart
    {
        get
        {
            if (!IsHost || Instance.phase != Phase.Lobby) return false;
            foreach (Slot s in Instance.slots) if (s.used && !s.ready) return false;
            return true;
        }
    }

    // ======================= PARTIDA =======================

    public static void HostStartGame()
    {
        if (!CanStart) return;
        CoopSession s = Instance;
        s.phase = Phase.InGame;
        s.LockSession(true);
        int seed = UnityEngine.Random.Range(1, 1000000);
        foreach (Slot slot in s.slots) { slot.scene = ""; slot.workshopDone = false; }
        SendToClients(CoopOp.StartGame, w => w.WriteValueSafe(seed));
        s.BeginRun(seed);
    }

    // Anfitrión: otra partida desde la pantalla final (todos vuelven al lago)
    public static void HostRestart()
    {
        if (!IsHost) return;
        CoopSession s = Instance;
        s.phase = Phase.InGame;
        int seed = UnityEngine.Random.Range(1, 1000000);
        foreach (Slot slot in s.slots) { slot.scene = ""; slot.workshopDone = false; }
        SendToClients(CoopOp.StartGame, w => w.WriteValueSafe(seed));
        s.BeginRun(seed);
    }

    void OnStartGame(ulong sender, FastBufferReader r)
    {
        if (host) return;
        r.ReadValueSafe(out int seed);
        phase = Phase.InGame;
        BeginRun(seed);
    }

    void BeginRun(int seed)
    {
        GameSession.CurrentMode = GameSession.Mode.Cooperativo;
        GameSession.LevelIndex = -1;
        WorkshopFlow.ClearVisit();
        SurvivalRun.Begin(seed);
        Time.timeScale = 1f;
        LoadingScreen.Load(GameSession.LakeScene);
    }

    // Anfitrión: todos cargan una escena (el anfitrión también)
    public static void HostLoadScene(string scene)
    {
        if (!IsHost) return;
        foreach (Slot s in Instance.slots) s.scene = "";
        SendToClients(CoopOp.LoadScene, w => CoopWire.WriteText(w, scene));
        Instance.LoadLocal(scene);
    }

    void OnLoadScene(ulong sender, FastBufferReader r)
    {
        if (host) return;
        LoadLocal(CoopWire.ReadText(r));
    }

    void LoadLocal(string scene)
    {
        if (scene == GameSession.LakeScene && WorkshopFlow.IsSurvivalVisit)
        {
            // Vuelta del Taller: la visita cuenta
            WorkshopFlow.ClearVisit();
            SurvivalRun.MarkWorkshopVisit();
        }
        Time.timeScale = 1f;
        LoadingScreen.Load(scene);
    }

    // Cada uno avisa cuando su escena terminó de cargar (el anfitrión espera a todos antes de la primera oleada)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Active) return;
        StartCoroutine(ReportWhenSettled(scene.name));
    }

    IEnumerator ReportWhenSettled(string scene)
    {
        yield return null;
        while (LoadingScreen.IsLoading) yield return null;
        if (!Active || SceneManager.GetActiveScene().name != scene) yield break;
        if (host) MarkInScene(localSlot, scene);
        else SendToHost(CoopOp.InScene, w => CoopWire.WriteText(w, scene));
    }

    void OnInScene(ulong sender, FastBufferReader r)
    {
        if (!host) return;
        MarkInScene(SlotOf(sender), CoopWire.ReadText(r));
    }

    // Lo escucha CoopGame (el anfitrión le manda todo el lago al que llega)
    public static event Action<int, string> SlotArrivedInScene;

    void MarkInScene(int slot, string scene)
    {
        if (slot < 0) return;
        slots[slot].scene = scene;
        SlotArrivedInScene?.Invoke(slot, scene);
    }

    public static bool AllInScene(string scene)
    {
        if (!IsHost) return true;
        foreach (Slot s in Instance.slots) if (s.used && s.scene != scene) return false;
        return true;
    }

    // ---- Taller cada 5 oleadas ----

    public static void HostGoWorkshop(int wavesCleared, bool lastWasBoss, float timeOfDay)
    {
        if (!IsHost) return;
        foreach (Slot s in Instance.slots) { s.workshopDone = false; s.scene = ""; }
        SendToClients(CoopOp.GoWorkshop, w =>
        {
            w.WriteValueSafe(wavesCleared);
            w.WriteValueSafe(lastWasBoss);
            w.WriteValueSafe(timeOfDay);
        });
        Instance.GoWorkshopLocal(wavesCleared, lastWasBoss, timeOfDay);
    }

    void OnGoWorkshop(ulong sender, FastBufferReader r)
    {
        if (host) return;
        r.ReadValueSafe(out int waves);
        r.ReadValueSafe(out bool boss);
        r.ReadValueSafe(out float time);
        foreach (Slot s in slots) s.workshopDone = false;
        GoWorkshopLocal(waves, boss, time);
    }

    void GoWorkshopLocal(int wavesCleared, bool lastWasBoss, float timeOfDay)
    {
        SurvivalRun.SyncFromHost(wavesCleared, lastWasBoss, timeOfDay);
        WorkshopFlow.OpenSurvival();
    }

    // Este jugador ya eligió su mejora en el Taller (espera a los demás)
    public static void LocalWorkshopDone()
    {
        if (!Active || Instance.localSlot < 0) return;
        Instance.slots[Instance.localSlot].workshopDone = true;
        if (Instance.host)
        {
            Instance.BroadcastRoster();
            Instance.CheckWorkshopDone();
        }
        else
        {
            SendToHost(CoopOp.WorkshopDone, null);
            RosterChanged?.Invoke();
        }
    }

    void OnWorkshopDone(ulong sender, FastBufferReader r)
    {
        if (!host) return;
        int slot = SlotOf(sender);
        if (slot < 0) return;
        slots[slot].workshopDone = true;
        BroadcastRoster();
        CheckWorkshopDone();
    }

    void CheckWorkshopDone()
    {
        if (!host || phase != Phase.InGame || !WorkshopFlow.IsSurvivalVisit) return;
        foreach (Slot s in slots) if (s.used && !s.workshopDone) return;
        HostLoadScene(GameSession.LakeScene);
    }

    public static int WorkshopDoneCount
    {
        get
        {
            if (Instance == null) return 0;
            int n = 0;
            foreach (Slot s in Instance.slots) if (s.used && s.workshopDone) n++;
            return n;
        }
    }

    // ---- Volver a la sala (después de perder) ----

    public static void HostBackToLobby()
    {
        if (!IsHost) return;
        SendToClients(CoopOp.BackToLobby, null);
        Instance.BackToLobbyLocal();
    }

    void OnBackToLobby(ulong sender, FastBufferReader r)
    {
        if (host) return;
        BackToLobbyLocal();
    }

    void BackToLobbyLocal()
    {
        phase = Phase.Lobby;
        foreach (Slot s in slots) { s.ready = false; s.workshopDone = false; s.scene = ""; }
        if (host && localSlot >= 0) slots[localSlot].ready = true;
        LockSession(false);
        if (host) BroadcastRoster();
        GoToLobbyScene();
    }

    void GoToLobbyScene()
    {
        SurvivalRun.End();
        GameSession.CurrentMode = GameSession.Mode.Cooperativo;
        GameSession.LevelIndex = -1;
        WorkshopFlow.OpenCoopLobby();
    }

    async void LockSession(bool locked)
    {
        if (!host || session == null) return;
        try
        {
            IHostSession h = session.AsHost();
            h.IsLocked = locked;
            await h.SavePropertiesAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Cooperativo: no se pudo " + (locked ? "cerrar" : "abrir") + " la sala: " + e.Message);
        }
    }

    // ======================= SALIR =======================

    // Sale de la sala y vuelve al menú principal (el anfitrión la cierra para todos)
    public static void Leave()
    {
        if (Instance == null || Instance.phase == Phase.Idle)
        {
            GameSession.GoToMainMenu();
            return;
        }
        Instance.LeaveInternal(true, null);
    }

    void OnHostLeaving(ulong sender, FastBufferReader r)
    {
        if (!host) LostConnection("El anfitrión cerró la sala.");
    }

    async void LeaveInternal(bool goToMenu, string message)
    {
        if (leaving) return;
        leaving = true;
        if (host && phase != Phase.Idle)
        {
            SendToClients(CoopOp.HostLeaving, null);
            await Task.Delay(200);
        }
        ISession s = session;
        session = null;
        UnhookSession(s);
        phase = Phase.Idle;
        localSlot = -1;
        roomCode = "";
        foreach (Slot slot in slots) Clear(slot);
        RosterChanged?.Invoke();
        try
        {
            if (s != null)
            {
                if (host) await s.AsHost().DeleteAsync();
                else await s.LeaveAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Cooperativo: al salir de la sala: " + e.Message);
        }
        StopNetwork();
        leaving = false;
        if (goToMenu)
        {
            PendingMenuMessage = message;
            SurvivalRun.End();
            GameSession.CurrentMode = GameSession.Mode.Historia;
            WorkshopFlow.ClearVisit();
            Time.timeScale = 1f;
            LoadingScreen.Load(GameSession.MenuScene);
        }
    }

    void StopNetwork()
    {
        if (net != null && net.IsListening && !net.ShutdownInProgress)
        {
            try { net.Shutdown(); }
            catch (Exception e) { Debug.LogWarning("Cooperativo: " + e.Message); }
        }
    }

    // ======================= RED LOCAL =======================

    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static bool IsLanCode(string code)
    {
        string ip;
        return code != null && code.Length == 8 && code[0] == 'L' && TryDecodeLan(code, out ip);
    }

    static string EncodeLan(string ip)
    {
        IPAddress addr;
        if (!IPAddress.TryParse(ip, out addr)) return "L0000000";
        byte[] b = addr.GetAddressBytes();
        if (b.Length != 4) return "L0000000";
        ulong v = ((ulong)b[0] << 24) | ((ulong)b[1] << 16) | ((ulong)b[2] << 8) | b[3];
        char[] c = new char[7];
        for (int i = 6; i >= 0; i--)
        {
            c[i] = Alphabet[(int)(v % 32)];
            v /= 32;
        }
        return "L" + new string(c);
    }

    static bool TryDecodeLan(string code, out string ip)
    {
        ip = null;
        if (string.IsNullOrEmpty(code)) return false;
        code = code.Trim().ToUpperInvariant().Replace("-", "");
        if (code.Length != 8 || code[0] != 'L') return false;
        ulong v = 0;
        for (int i = 1; i < 8; i++)
        {
            int d = Alphabet.IndexOf(code[i]);
            if (d < 0) return false;
            v = v * 32 + (ulong)d;
        }
        if (v > 0xFFFFFFFFUL) return false;
        ip = ((v >> 24) & 255) + "." + ((v >> 16) & 255) + "." + ((v >> 8) & 255) + "." + (v & 255);
        return true;
    }

    // IP de esta computadora en la red local (la primera interfaz activa con IPv4)
    static string LocalIPv4()
    {
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                        return ua.Address.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Cooperativo: no se pudo leer la IP local: " + e.Message);
        }
        return "127.0.0.1";
    }
}
