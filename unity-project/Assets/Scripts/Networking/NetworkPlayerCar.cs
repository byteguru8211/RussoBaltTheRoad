using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Server-authoritative vehicle: every car is simulated on the server and replicated by
/// NetworkRigidbody. Owners only sample/send controls; they never write transforms or velocities.
/// There is deliberately no client-side vehicle prediction, so owner input has network latency.
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkPlayerCar : NetworkBehaviour
{
    [Serializable]
    public struct NetworkDriveInput : INetworkSerializable
    {
        public float Throttle;
        public float Brake;
        public float Steer;
        public float Clutch;
        public float Handbrake;
        public int ShiftRequestId;
        public int RequestedGear;
        public int StartRequestId;
        public int ResetRequestId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Throttle);
            serializer.SerializeValue(ref Brake);
            serializer.SerializeValue(ref Steer);
            serializer.SerializeValue(ref Clutch);
            serializer.SerializeValue(ref Handbrake);
            serializer.SerializeValue(ref ShiftRequestId);
            serializer.SerializeValue(ref RequestedGear);
            serializer.SerializeValue(ref StartRequestId);
            serializer.SerializeValue(ref ResetRequestId);
        }
    }

    [Serializable]
    public struct VehicleNetworkState : INetworkSerializable
    {
        public int Gear;
        public bool EngineRunning;
        public float Rpm;
        public float Throttle;
        public float Steer;
        public float Fuel;
        public float Temperature;
        public float Damage;
        public float Distance;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Gear);
            serializer.SerializeValue(ref EngineRunning);
            serializer.SerializeValue(ref Rpm);
            serializer.SerializeValue(ref Throttle);
            serializer.SerializeValue(ref Steer);
            serializer.SerializeValue(ref Fuel);
            serializer.SerializeValue(ref Temperature);
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref Distance);
        }
    }

    public VehicleController1912 vehicle;
    public VehicleInput1912 input;
    public EngineAudio1912 engineAudio;
    public CameraController1912 cameraController;
    public NetworkVariable<int> GridSlot = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RaceProgress = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CheckpointIndex = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CompletedLaps = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> Finished = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<VehicleNetworkState> State = new NetworkVariable<VehicleNetworkState>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> DisplayName = new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes("Гонщик"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody body;
    private NetworkTransform networkTransform;
    private NetworkRigidbody networkRigidbody;
    private NetworkDriveInput serverInput;
    private double lastInputServerTime;
    private int processedShiftRequest;
    private int processedStartRequest;
    private int processedResetRequest;
    private int nextCheckpoint = 1;
    private bool displayNameResolved;
    private float continuousProgress;
    private float lastTrackProgress = -1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        vehicle = GetComponent<VehicleController1912>();
        input = GetComponent<VehicleInput1912>();
        engineAudio = GetComponent<EngineAudio1912>();
        networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform == null)
            networkTransform = gameObject.AddComponent<NetworkTransform>();
        networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
        networkTransform.Interpolate = true;
        networkTransform.SyncScaleX = false;
        networkTransform.SyncScaleY = false;
        networkTransform.SyncScaleZ = false;

        networkRigidbody = GetComponent<NetworkRigidbody>();
        if (networkRigidbody == null)
            networkRigidbody = gameObject.AddComponent<ServerVehicleRigidbody>();
        networkRigidbody.UseRigidBodyForMotion = true;
        networkRigidbody.AutoUpdateKinematicState = false;
        body.isKinematic = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        body = GetComponent<Rigidbody>();
        vehicle = GetComponent<VehicleController1912>();
        input = GetComponent<VehicleInput1912>();
        engineAudio = GetComponent<EngineAudio1912>();
        DontDestroyOnLoad(gameObject);
        if (GetComponent<NetworkNameplateUI>() == null)
            gameObject.AddComponent<NetworkNameplateUI>();
        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = true;
        if (vehicle != null)
        {
            vehicle.isPlayer = IsOwner;
            vehicle.SetTransmissionMode(true);
            // Clients keep visuals/state alive, but only the server may simulate forces.
            vehicle.enabled = true;
            vehicle.SetSimulationEnabled(IsServer);
        }
        if (input != null)
            input.enabled = false;
        if (engineAudio != null)
            engineAudio.enabled = IsOwner;
        networkRigidbody.SetIsKinematic(true);

        if (!IsServer)
        {
            State.OnValueChanged += OnVehicleStateChanged;
            OnVehicleStateChanged(default, State.Value);
        }
        if (IsOwner)
        {
            Camera main = Camera.main;
            if (main != null)
            {
                cameraController = main.GetComponent<CameraController1912>();
                if (cameraController == null) cameraController = main.gameObject.AddComponent<CameraController1912>();
                cameraController.vehicle = vehicle;
                cameraController.targetCamera = main;
            }
        }
        if (IsServer)
        {
            ResolveDisplayName();
            SetGridPhase(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        State.OnValueChanged -= OnVehicleStateChanged;
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        State.OnValueChanged -= OnVehicleStateChanged;
        base.OnDestroy();
    }

    private void OnVehicleStateChanged(VehicleNetworkState previous, VehicleNetworkState current)
    {
        vehicle?.ApplyNetworkState(current.Gear, current.EngineRunning, current.Rpm, current.Throttle,
            current.Fuel, current.Temperature, current.Damage, current.Steer, current.Distance);
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || vehicle == null)
            return;
        if (IsServer)
        {
            if (!displayNameResolved)
                ResolveDisplayName();
            if (IsOwner)
            {
                serverInput = ToDriveInput(vehicle.CurrentInput);
                ObserveOwnerActions();
            }
            else
            {
                double now = NetworkManager.Singleton.ServerTime.Time;
                if (now - lastInputServerTime > 0.5d)
                    serverInput = default;
            }

            bool raceRunning = IsServerRaceRunning();
            if (!raceRunning || Finished.Value)
                serverInput = new NetworkDriveInput { RequestedGear = vehicle.gear, Brake = Finished.Value ? 0.35f : 0f };
            vehicle.SetInput(ToVehicleInput(serverInput));
            if (raceRunning && !Finished.Value)
                UpdateServerProgress();
            PublishState();
        }
        else if (IsOwner)
        {
            // VehicleInput1912 remains the sole local control sampler. The controller is disabled here,
            // so this copy cannot move the rigidbody; only sanitized controls cross the ServerRpc.
            VehicleController1912.VehicleInput controls = input != null && input.enabled ? vehicle.CurrentInput : default;
            SubmitInputServerRpc(new NetworkDriveInput
            {
                Throttle = controls.throttle,
                Brake = controls.brake,
                Steer = controls.steer,
                Clutch = controls.clutch,
                Handbrake = controls.handbrake,
                ShiftRequestId = vehicle.ShiftRequestId,
                RequestedGear = vehicle.RequestedGear,
                StartRequestId = vehicle.StartRequestId,
                ResetRequestId = vehicle.ResetRequestId
            });
        }
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitInputServerRpc(NetworkDriveInput controls, RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            return;
        controls.Throttle = Sanitize01(controls.Throttle);
        controls.Brake = Sanitize01(controls.Brake);
        controls.Steer = Mathf.Clamp(Finite(controls.Steer), -1f, 1f);
        controls.Clutch = Sanitize01(controls.Clutch);
        controls.Handbrake = Sanitize01(controls.Handbrake);
        controls.RequestedGear = Mathf.Clamp(controls.RequestedGear, -1, 4);
        if (controls.ShiftRequestId > processedShiftRequest && controls.RequestedGear != vehicle.gear)
            vehicle.ShiftTo(controls.RequestedGear);
        processedShiftRequest = Mathf.Max(processedShiftRequest, controls.ShiftRequestId);
        if (controls.StartRequestId > processedStartRequest)
            vehicle.TryStartEngine();
        processedStartRequest = Mathf.Max(processedStartRequest, controls.StartRequestId);
        if (controls.ResetRequestId > processedResetRequest)
            vehicle.ResetVehicle();
        processedResetRequest = Mathf.Max(processedResetRequest, controls.ResetRequestId);
        serverInput = controls;
        lastInputServerTime = NetworkManager.Singleton.ServerTime.Time;
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestRepairServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId || vehicle == null)
            return;
        vehicle.Repair(45f);
        PublishState();
    }

    private void ObserveOwnerActions()
    {
        if (vehicle.ShiftRequestId > processedShiftRequest && vehicle.RequestedGear != vehicle.gear)
            vehicle.ShiftTo(vehicle.RequestedGear);
        processedShiftRequest = Mathf.Max(processedShiftRequest, vehicle.ShiftRequestId);
        if (vehicle.StartRequestId > processedStartRequest)
            vehicle.TryStartEngine();
        processedStartRequest = Mathf.Max(processedStartRequest, vehicle.StartRequestId);
        if (vehicle.ResetRequestId > processedResetRequest)
            vehicle.ResetVehicle();
        processedResetRequest = Mathf.Max(processedResetRequest, vehicle.ResetRequestId);
    }

    private NetworkDriveInput ToDriveInput(VehicleController1912.VehicleInput controls)
    {
        if (vehicle == null)
            return default;
        return new NetworkDriveInput
        {
            Throttle = controls.throttle,
            Brake = controls.brake,
            Steer = controls.steer,
            Clutch = controls.clutch,
            Handbrake = controls.handbrake,
            ShiftRequestId = vehicle.ShiftRequestId,
            RequestedGear = vehicle.RequestedGear,
            StartRequestId = vehicle.StartRequestId,
            ResetRequestId = vehicle.ResetRequestId
        };
    }

    private static VehicleController1912.VehicleInput ToVehicleInput(NetworkDriveInput controls)
    {
        return new VehicleController1912.VehicleInput
        {
            throttle = controls.Throttle,
            brake = controls.Brake,
            steer = controls.Steer,
            clutch = controls.Clutch,
            handbrake = controls.Handbrake
        };
    }

    private static float Finite(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    private static float Sanitize01(float value) => Mathf.Clamp01(Finite(value));

    private void ResolveDisplayName()
    {
        if (!IsServer || displayNameResolved)
            return;
        string name = IsOwner && GameManager.Instance != null
            ? GameManager.Instance.Profile.playerName
            : $"Гонщик {OwnerClientId + 1}";
        if (LobbyManager.Instance != null)
        {
            foreach (LobbyEntry entry in LobbyManager.Instance.Entries)
            {
                if (entry.ClientId == OwnerClientId)
                {
                    DisplayName.Value = new FixedString32Bytes(LobbyManager.SanitizeName(entry.Name.ToString()));
                    displayNameResolved = true;
                    return;
                }
            }
        }
        if (IsOwner && GameManager.Instance != null)
        {
            DisplayName.Value = new FixedString32Bytes(LobbyManager.SanitizeName(GameManager.Instance.Profile.playerName));
            displayNameResolved = true;
            return;
        }
        if (LobbyManager.Instance == null)
            return;
        DisplayName.Value = new FixedString32Bytes(LobbyManager.SanitizeName(name));
    }

    public void PlaceOnGrid(int slot)
    {
        if (!IsServer || RaceManager.Instance == null || RaceManager.Instance.TrackBuilder == null)
            return;
        GridSlot.Value = Mathf.Max(0, slot);
        TrackBuilder track = RaceManager.Instance.TrackBuilder;
        Vector3 tangent = track.StartTangent;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        int row = slot / 2;
        int column = slot % 2;
        Vector3 position = track.StartPoint - tangent * (2.5f + row * 7f) + right * (column == 0 ? -1.9f : 1.9f) + Vector3.up * 0.75f;
        Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        SetGridPhase(true);
        networkRigidbody.SetPosition(position);
        networkRigidbody.SetRotation(rotation);
        transform.SetPositionAndRotation(position, rotation);
        networkTransform.Teleport(position, rotation, transform.lossyScale);
        nextCheckpoint = 1;
        continuousProgress = 0f;
        lastTrackProgress = -1f;
        RaceProgress.Value = 0f;
        CheckpointIndex.Value = 1;
        CompletedLaps.Value = 0;
        Finished.Value = false;
    }

    public void SetGridPhase(bool locked)
    {
        if (!IsServer)
            return;
        if (vehicle != null)
            vehicle.SetSimulationEnabled(!locked);
        networkRigidbody.SetIsKinematic(locked);
        if (locked)
        {
            serverInput = default;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            serverInput = default;
            body.WakeUp();
        }
    }

    private bool IsServerRaceRunning()
    {
        if (RaceManager.Instance == null || NetworkManager.Singleton == null)
            return false;
        if (LobbyManager.Instance != null)
        {
            double start = LobbyManager.Instance.RaceStartServerTime.Value;
            return start > 0d && NetworkManager.Singleton.ServerTime.Time >= start;
        }
        return RaceManager.Instance.State == RaceManager.RaceState.Racing;
    }

    private void UpdateServerProgress()
    {
        RaceManager race = RaceManager.Instance;
        TrackBuilder track = race.TrackBuilder;
        if (track == null || track.Samples.Count == 0)
            return;
        int checkpointCount = Mathf.Max(1, track.checkpointCount);
        int targetIndex = nextCheckpoint >= checkpointCount ? 0 : nextCheckpoint;
        Vector3 target = track.PointAt(targetIndex / (float)checkpointCount);
        Vector3 offset = body.position - target;
        offset.y = 0f;
        float checkpointRadius = Mathf.Max(6f, track.track.roadWidth * 0.85f);
        if (offset.sqrMagnitude <= checkpointRadius * checkpointRadius)
        {
            if (targetIndex == 0)
            {
                CompletedLaps.Value++;
                nextCheckpoint = 1;
            }
            else if (targetIndex == checkpointCount - 1)
            {
                nextCheckpoint = checkpointCount;
            }
            else
            {
                nextCheckpoint = targetIndex + 1;
            }
            CheckpointIndex.Value = nextCheckpoint;
            if (CompletedLaps.Value >= GameManager.Instance.GetRaceLaps())
            {
                Finished.Value = true;
                serverInput = new NetworkDriveInput { Brake = 0.35f };
            }
        }

        UpdateContinuousProgress();
        if (Finished.Value)
            continuousProgress = CompletedLaps.Value;
        RaceProgress.Value = continuousProgress;
    }

    private void UpdateContinuousProgress()
    {
        float nearest = NearestTrackProgress(body.position);
        if (lastTrackProgress < 0f)
        {
            lastTrackProgress = nearest;
            return;
        }
        float delta = Mathf.Repeat(nearest - lastTrackProgress + 0.5f, 1f) - 0.5f;
        if (Mathf.Abs(delta) < 0.1f)
            continuousProgress += delta;
        lastTrackProgress = nearest;
    }

    private float NearestTrackProgress(Vector3 position)
    {
        float bestDistance = float.PositiveInfinity;
        float bestProgress = 0f;
        for (int i = 0; i < RaceManager.Instance.TrackBuilder.Samples.Count; i++)
        {
            Vector3 a = RaceManager.Instance.TrackBuilder.Samples[i];
            Vector3 b = RaceManager.Instance.TrackBuilder.Samples[(i + 1) % RaceManager.Instance.TrackBuilder.Samples.Count];
            Vector3 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector3.Dot(position - a, segment) / lengthSquared) : 0f;
            float candidateDistance = Vector3.SqrMagnitude(position - (a + segment * t));
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                bestProgress = (i + t) / RaceManager.Instance.TrackBuilder.Samples.Count;
            }
        }
        return bestProgress;
    }

    private void PublishState()
    {
        State.Value = new VehicleNetworkState
        {
            Gear = vehicle.gear,
            EngineRunning = vehicle.engineRunning,
            Rpm = vehicle.rpm,
            Throttle = vehicle.Throttle,
            Steer = vehicle.CurrentInput.steer,
            Fuel = vehicle.fuel,
            Temperature = vehicle.temperature,
            Damage = vehicle.damage,
            Distance = vehicle.distanceSinceReset
        };
    }
}
