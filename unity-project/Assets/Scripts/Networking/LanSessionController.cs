using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class LanSessionController : MonoBehaviour
{
    public static LanSessionController Instance { get; private set; }
    public NetworkManager Network { get; private set; }
    public UnityTransport Transport { get; private set; }
    public ushort Port = 7777;
    public const string BuildVersion = "0.4.0";
    public const int MaxPlayers = 6;

    private GameObject lobbyPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Network = GetComponent<NetworkManager>();
        if (Network == null) Network = gameObject.AddComponent<NetworkManager>();
        Transport = GetComponent<UnityTransport>();
        if (Transport == null) Transport = gameObject.AddComponent<UnityTransport>();
        if (Network.NetworkConfig == null) Network.NetworkConfig = new NetworkConfig();
        Network.NetworkConfig.NetworkTransport = Transport;
        Network.NetworkConfig.PlayerPrefab = Resources.Load<GameObject>("Vehicles/network_russo_balt");
        lobbyPrefab = Resources.Load<GameObject>("Networking/LobbyManager");
        if (lobbyPrefab != null)
        {
            bool registered = false;
            foreach (NetworkPrefab prefab in Network.NetworkConfig.Prefabs.Prefabs)
                if (prefab.Prefab == lobbyPrefab) registered = true;
            if (!registered) Network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = lobbyPrefab });
        }
        Network.NetworkConfig.TickRate = 30;
        Network.NetworkConfig.EnableSceneManagement = true;
        Network.NetworkConfig.ConnectionApproval = true;
        Network.ConnectionApprovalCallback += ApproveConnection;
        Network.OnClientConnectedCallback += OnClientConnected;
        Network.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-lanhost") Host("127.0.0.1");
            if (args[i] == "-lanjoin" && i + 1 < args.Length) Join(args[i + 1]);
        }
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool allowed = Network.ConnectedClientsIds.Count < MaxPlayers;
        response.Approved = allowed;
        response.CreatePlayerObject = true;
        if (!allowed) response.Reason = "Лобби заполнено";
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[LAN] Игрок подключился: {clientId}");
    }

    private void OnClientDisconnect(ulong clientId)
    {
        Debug.Log($"[LAN] Игрок отключился: {clientId}");
        if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshRoster();
    }

    public void Host(string address = "127.0.0.1")
    {
        if (Network.IsListening) return;
        Transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        if (!Network.StartHost()) return;
        if (lobbyPrefab != null)
        {
            GameObject lobby = Instantiate(lobbyPrefab);
            DontDestroyOnLoad(lobby);
            lobby.GetComponent<NetworkObject>().Spawn(true);
        }
        if (GameManager.Instance != null) GameManager.Instance.SelectedTrackId = "winter_departure";
        Network.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    public void Join(string address)
    {
        if (Network.IsListening || string.IsNullOrWhiteSpace(address)) return;
        Transport.SetConnectionData(address.Trim(), Port);
        Network.StartClient();
    }

    public void ShutdownSession()
    {
        if (Network != null && Network.IsListening) Network.Shutdown();
        if (SceneManager.GetActiveScene().name != "MainMenu")
            SceneManager.LoadScene("MainMenu");
    }

    public bool IsHost => Network != null && Network.IsHost;
    public bool IsClient => Network != null && Network.IsClient;
    public int PlayerCount => Network != null ? Network.ConnectedClientsIds.Count : 0;
    public string LocalAddress => Network != null && Network.IsHost ? Transport.ConnectionData.Address : "—";

    private void OnDestroy()
    {
        if (Network != null && Network.IsListening) Network.Shutdown();
    }
}
