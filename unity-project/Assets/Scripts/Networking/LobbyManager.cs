using System;
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

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (IsServer)
            RefreshRoster();
        else if (GameManager.Instance != null)
            SetNameServerRpc(new FixedString32Bytes(GameManager.Instance.Profile.playerName));
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetNameServerRpc(FixedString32Bytes name, ServerRpcParams rpcParams = default)
    {
        RefreshRoster();
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].ClientId != rpcParams.Receive.SenderClientId) continue;
            LobbyEntry entry = Entries[i];
            entry.Name = name;
            Entries[i] = entry;
            break;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].ClientId != rpcParams.Receive.SenderClientId) continue;
            LobbyEntry entry = Entries[i];
            entry.Ready = ready;
            Entries[i] = entry;
            break;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartRaceServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != 0 || Entries.Count == 0) return;
        foreach (LobbyEntry entry in Entries)
            if (!entry.Ready) return;
        RaceStarted.Value = true;
        if (GameManager.Instance != null) GameManager.Instance.SelectedTrackId = "winter_departure";
        NetworkManager.Singleton.SceneManager.LoadScene("Race_Winter", LoadSceneMode.Single);
    }

    public void RefreshRoster()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;
        Entries.Clear();
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            string name = clientId == 0 && GameManager.Instance != null ? GameManager.Instance.Profile.playerName : $"Гонщик {clientId + 1}";
            Entries.Add(new LobbyEntry { ClientId = clientId, Name = new FixedString32Bytes(name), Ready = clientId == 0 });
        }
    }
}
