using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
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
    public const string BuildVersion = "0.6.0";
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
        if (Network.NetworkConfig.PlayerPrefab != null)
            EnsureNetworkPrefab(Network.NetworkConfig.PlayerPrefab);
        if (lobbyPrefab != null)
            EnsureNetworkPrefab(lobbyPrefab);
        Network.NetworkConfig.TickRate = 50;
        Network.NetworkConfig.EnableSceneManagement = true;
        Network.NetworkConfig.ConnectionApproval = true;
        Network.NetworkConfig.ConnectionData = BuildConnectionPayload();
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
        byte[] expected = BuildConnectionPayload();
        bool versionMatches = request.Payload != null && request.Payload.SequenceEqual(expected);
        bool lobbyOpen = LobbyManager.Instance == null || !LobbyManager.Instance.RaceStarted.Value;
        bool allowed = versionMatches && lobbyOpen && Network.ConnectedClientsIds.Count < MaxPlayers;
        response.Approved = allowed;
        response.CreatePlayerObject = allowed;
        if (!versionMatches)
            response.Reason = $"Несовместимая версия сборки. Требуется {BuildVersion}";
        else if (!lobbyOpen)
            response.Reason = "Гонка уже началась";
        else if (!allowed)
            response.Reason = "Лобби заполнено";
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[LAN] Игрок подключился: {clientId}");
        if (Network.IsServer && LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshRoster();
    }

    private void OnClientDisconnect(ulong clientId)
    {
        Debug.Log($"[LAN] Игрок отключился: {clientId}");
        if (Network.IsServer && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RefreshRoster();
            LobbyManager.Instance.TryScheduleCountdown();
        }
    }

    public void Host(string address = "127.0.0.1")
    {
        if (Network.IsListening || !ValidateNetworkPrefabs()) return;
        Network.NetworkConfig.ConnectionData = BuildConnectionPayload();
        Transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        if (!Network.StartHost()) return;
        if (lobbyPrefab == null)
        {
            Debug.LogError("[LAN] Отсутствует Resources/Networking/LobbyManager");
            Network.Shutdown();
            return;
        }
        GameObject lobby = Instantiate(lobbyPrefab);
        DontDestroyOnLoad(lobby);
        lobby.GetComponent<NetworkObject>().Spawn(true);
        Network.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    public void Join(string address)
    {
        if (Network.IsListening || string.IsNullOrWhiteSpace(address) || !ValidateNetworkPrefabs()) return;
        Network.NetworkConfig.ConnectionData = BuildConnectionPayload();
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
    public string LocalAddress => Network != null && Network.IsHost ? GetAdvertisedAddress() : "—";

    private static string GetAdvertisedAddress()
    {
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;
                foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address.Address))
                        continue;
                    return address.Address.ToString();
                }
            }
        }
        catch (System.Exception)
        {
        }
        return "127.0.0.1";
    }

    private void EnsureNetworkPrefab(GameObject prefab)
    {
        if (prefab == null || Network.NetworkConfig.Prefabs.Contains(prefab))
            return;
        if (!Network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab }))
            Debug.LogError($"[LAN] Не удалось зарегистрировать сетевой префаб {prefab.name}");
    }

    private bool ValidateNetworkPrefabs()
    {
        if (Network.NetworkConfig.PlayerPrefab == null || lobbyPrefab == null)
        {
            Debug.LogError("[LAN] Не задан сетевой префаб игрока или лобби");
            return false;
        }
        uint playerHash = new NetworkPrefab { Prefab = Network.NetworkConfig.PlayerPrefab }.SourcePrefabGlobalObjectIdHash;
        uint lobbyHash = new NetworkPrefab { Prefab = lobbyPrefab }.SourcePrefabGlobalObjectIdHash;
        if (playerHash == lobbyHash)
        {
            Debug.LogError($"[LAN] GlobalObjectIdHash {playerHash} совпадает у network_russo_balt и LobbyManager. Пересоздайте сетевые префабы в Unity.");
            return false;
        }
        Dictionary<uint, string> hashes = new Dictionary<uint, string>();
        foreach (NetworkPrefab entry in Network.NetworkConfig.Prefabs.Prefabs)
        {
            if (entry.Override != NetworkPrefabOverride.None || entry.Prefab == null)
                continue;
            uint hash = entry.SourcePrefabGlobalObjectIdHash;
            if (hashes.TryGetValue(hash, out string previous))
            {
                Debug.LogError($"[LAN] Дубликат GlobalObjectIdHash {hash}: {previous} и {entry.Prefab.name}. Пересоздайте сетевые префабы в Unity.");
                return false;
            }
            hashes.Add(hash, entry.Prefab.name);
        }
        return true;
    }

    private static byte[] BuildConnectionPayload() => Encoding.UTF8.GetBytes("RussoBaltTheRoad/" + BuildVersion);

    private void OnDestroy()
    {
        if (Network == null) return;
        Network.ConnectionApprovalCallback -= ApproveConnection;
        Network.OnClientConnectedCallback -= OnClientConnected;
        Network.OnClientDisconnectCallback -= OnClientDisconnect;
        if (Network.IsListening) Network.Shutdown();
    }
}
