using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class NetworkPlayerCar : NetworkBehaviour
{
    public VehicleController1912 vehicle;
    public VehicleInput1912 input;
    public EngineAudio1912 engineAudio;
    public CameraController1912 cameraController;
    public NetworkVariable<int> GridSlot = new NetworkVariable<int>(-1);
    public NetworkVariable<float> RaceProgress = new NetworkVariable<float>(0f);

    private Rigidbody body;
    private float progressTimer;

    public override void OnNetworkSpawn()
    {
        body = GetComponent<Rigidbody>();
        vehicle = GetComponent<VehicleController1912>();
        input = GetComponent<VehicleInput1912>();
        engineAudio = GetComponent<EngineAudio1912>();
        DontDestroyOnLoad(gameObject);
        if (body != null) body.isKinematic = !IsOwner;
        if (vehicle != null)
        {
            vehicle.isPlayer = IsOwner;
            vehicle.enabled = IsOwner;
        }
        if (input != null) input.enabled = IsOwner;
        if (engineAudio != null) engineAudio.enabled = IsOwner;

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
            GridSlot.OnValueChanged += OnGridSlotChanged;
            if (GridSlot.Value >= 0) OnGridSlotChanged(-1, GridSlot.Value);
        }
    }

    public override void OnDestroy()
    {
        if (IsOwner) GridSlot.OnValueChanged -= OnGridSlotChanged;
        base.OnDestroy();
    }

    private void OnGridSlotChanged(int previous, int slot)
    {
        if (!IsOwner || slot < 0 || RaceManager.Instance == null || RaceManager.Instance.TrackBuilder == null) return;
        TrackBuilder track = RaceManager.Instance.TrackBuilder;
        Vector3 tangent = track.StartTangent;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Vector3 position = track.StartPoint - tangent * (2.5f + slot * 3.2f) + right * (slot % 2 == 0 ? -1.2f : 1.2f) + Vector3.up * 0.8f;
        Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
        if (body != null)
        {
            body.isKinematic = false;
            body.position = position;
            body.rotation = rotation;
        }
        transform.position = position;
        transform.rotation = rotation;
        GetComponent<NetworkTransform>()?.Teleport(position, rotation, Vector3.one);
    }

    private void Update()
    {
        if (!IsOwner || vehicle == null || RaceManager.Instance == null) return;
        progressTimer += Time.deltaTime;
        if (progressTimer < 0.25f) return;
        progressTimer = 0f;
        float progress = RaceManager.Instance.CompletedLaps + RaceManager.Instance.CheckpointIndex / (float)RaceManager.Instance.TrackBuilder.checkpointCount;
        RaceProgress.Value = progress;
    }
}
