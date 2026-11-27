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

    private void OnTriggerEnter(Collider other) => Report(other);

    private void OnTriggerStay(Collider other) => Report(other);

    private void Report(Collider other)
    {
        VehicleController1912 vehicle = other.GetComponentInParent<VehicleController1912>();
        if (raceManager != null && vehicle != null && vehicle.isPlayer)
            raceManager.PlayerCheckpoint(index, vehicle);
    }
}
