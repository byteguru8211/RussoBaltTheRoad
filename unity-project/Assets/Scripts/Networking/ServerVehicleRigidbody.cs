using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Runtime fallback for vehicle prefabs that do not yet have a configured NetworkRigidbody.
/// The settings are applied before NetworkRigidbody registers itself with NetworkTransform.
/// </summary>
public class ServerVehicleRigidbody : NetworkRigidbody
{
    protected override void Awake()
    {
        UseRigidBodyForMotion = true;
        AutoUpdateKinematicState = false;
        NetworkTransform transformSync = GetComponent<NetworkTransform>();
        if (transformSync != null)
            transformSync.AuthorityMode = NetworkTransform.AuthorityModes.Server;
        base.Awake();
    }
}
