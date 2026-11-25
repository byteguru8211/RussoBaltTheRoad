using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct LobbyEntry : INetworkSerializable, IEquatable<LobbyEntry>
{
    public ulong ClientId;
    public FixedString32Bytes Name;
    public bool Ready;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Ready);
    }

    public bool Equals(LobbyEntry other) => ClientId == other.ClientId && Name.Equals(other.Name) && Ready == other.Ready;
    public override bool Equals(object obj) => obj is LobbyEntry other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(ClientId, Name.GetHashCode(), Ready);
}

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }
    public NetworkList<LobbyEntry> Entries { get; } = new NetworkList<LobbyEntry>();
    public NetworkVariable<bool> RaceStarted = new NetworkVariable<bool>(false);
    public NetworkVariable<double> RaceStartServerTime = new NetworkVariable<double>(0d);
    public NetworkVariable<FixedString32Bytes> TrackId = new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes("winter_departure"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RaceType = new NetworkVariable<int>((int)global::RaceType.Circuit, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly HashSet<ulong> raceReadyClients = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Instance = this;
        if (IsServer)
        {
            if (GameManager.Instance != null && GameManager.Instance.CanStartRace())
            {
                TrackId.Value = new FixedString32Bytes(GameManager.Instance.SelectedTrackId);
                RaceType.Value = (int)GameManager.Instance.SelectedRaceType;
            }
            RefreshRoster();
        }
        else if (GameManager.Instance != null)
            SetNameServerRpc(new FixedString32Bytes(SanitizeName(GameManager.Instance.Profile.playerName)));
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
        raceReadyClients.Clear();
        base.OnNetworkDespawn();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetNameServerRpc(FixedString32Bytes name, RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClientsIds.Contains(rpcParams.Receive.SenderClientId))
            return;
        int index = EnsureEntry(rpcParams.Receive.SenderClientId);
        LobbyEntry entry = Entries[index];
        entry.Name = new FixedString32Bytes(SanitizeName(name.ToString()));
        Entries[index] = entry;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ToggleReadyServerRpc(bool ready, RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClientsIds.Contains(rpcParams.Receive.SenderClientId))
            return;
        int index = EnsureEntry(rpcParams.Receive.SenderClientId);
        LobbyEntry entry = Entries[index];
        entry.Ready = ready;
        Entries[index] = entry;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartRaceServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null || rpcParams.Receive.SenderClientId != 0 || RaceStarted.Value || Entries.Count == 0)
            return;
        if (Entries.Count != NetworkManager.Singleton.ConnectedClientsIds.Count)
            return;
        foreach (LobbyEntry entry in Entries)
            if (!entry.Ready) return;

        if (GameManager.Instance == null || !GameManager.Instance.CanStartRace())
            return;
        TrackId.Value = new FixedString32Bytes(GameManager.Instance.SelectedTrackId);
        RaceType.Value = (int)GameManager.Instance.SelectedRaceType;
        RaceStarted.Value = true;
        // The server assigns this only after every peer has built its track and placed its cars.
        RaceStartServerTime.Value = 0d;
        raceReadyClients.Clear();
        NetworkManager.Singleton.SceneManager.LoadScene(GameManager.Instance.GetRaceSceneName(), LoadSceneMode.Single);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void MarkRaceReadyServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null || !RaceStarted.Value)
            return;
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClientsIds.Contains(clientId))
            return;
        raceReadyClients.Add(clientId);
        TryScheduleCountdown();
    }

    public void ServerMarkRaceReady(ulong clientId)
    {
        if (!IsServer || NetworkManager.Singleton == null || !RaceStarted.Value)
            return;
        if (!NetworkManager.Singleton.ConnectedClientsIds.Contains(clientId))
            return;
        raceReadyClients.Add(clientId);
        TryScheduleCountdown();
    }

    public void TryScheduleCountdown()
    {
        if (!IsServer || NetworkManager.Singleton == null || !RaceStarted.Value || RaceStartServerTime.Value > 0d)
            return;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            if (!raceReadyClients.Contains(clientId)) return;
        RaceStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time + 4d;
    }

    public void RefreshRoster()
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        // Remove only departed clients. Existing entries keep their names and ready state.
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            ulong clientId = Entries[i].ClientId;
            bool connected = NetworkManager.Singleton.ConnectedClientsIds.Contains(clientId);
            bool duplicate = false;
            for (int j = 0; j < i; j++)
                if (Entries[j].ClientId == clientId) duplicate = true;
            if (!connected || duplicate)
                Entries.RemoveAt(i);
        }

        int rosterIndex = 0;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            int index = FindEntryIndex(clientId);
            if (index < 0)
            {
                string name = clientId == 0 && GameManager.Instance != null
                    ? SanitizeName(GameManager.Instance.Profile.playerName)
                    : $"Гонщик {clientId + 1}";
                Entries.Insert(Mathf.Min(rosterIndex, Entries.Count), new LobbyEntry
                {
                    ClientId = clientId,
                    Name = new FixedString32Bytes(name),
                    Ready = clientId == 0
                });
            }
            rosterIndex++;
        }
        TryScheduleCountdown();
    }

    private int EnsureEntry(ulong clientId)
    {
        int index = FindEntryIndex(clientId);
        if (index >= 0)
            return index;
        string name = clientId == 0 && GameManager.Instance != null
            ? SanitizeName(GameManager.Instance.Profile.playerName)
            : $"Гонщик {clientId + 1}";
        Entries.Add(new LobbyEntry { ClientId = clientId, Name = new FixedString32Bytes(name), Ready = clientId == 0 });
        return Entries.Count - 1;
    }

    private int FindEntryIndex(ulong clientId)
    {
        for (int i = 0; i < Entries.Count; i++)
            if (Entries[i].ClientId == clientId) return i;
        return -1;
    }

    public static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Гонщик";
        StringBuilder clean = new StringBuilder();
        bool pendingSpace = false;
        foreach (char character in value.Trim())
        {
            if (char.IsControl(character))
                continue;
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = clean.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                clean.Append(' ');
                pendingSpace = false;
            }
            clean.Append(character);
        }
        string name = clean.ToString().Trim();
        if (name.Length == 0)
            return "Гонщик";
        // FixedString32Bytes needs room for its terminating zero; Cyrillic occupies two UTF-8 bytes.
        while (Encoding.UTF8.GetByteCount(name) > 31)
            name = name.Substring(0, name.Length - 1);
        return name;
    }
}
