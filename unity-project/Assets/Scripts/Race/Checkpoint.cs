using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public int index;
    public RaceManager raceManager;

    private void Awake()
    {
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 8f;
    }

    private void OnTriggerEnter(Collider other)
    {
        VehicleController1912 vehicle = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<VehicleController1912>() : null;
        if (raceManager != null && vehicle != null && vehicle.isPlayer)
            raceManager.PlayerCheckpoint(index);
    }
}
