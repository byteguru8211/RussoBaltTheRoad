using UnityEngine;

public class AIDriver1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public TrackBuilder track;
    public float skill = 0.8f;
    public float progress;
    public int lap;
    public float laneOffset;

    private float speed;
    private float seed;

    private void Start()
    {
        seed = Random.Range(0f, 1000f);
        laneOffset = Random.Range(-0.75f, 0.75f);
        if (vehicle != null)
        {
            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            vehicle.engineRunning = true;
        }
    }

    private void FixedUpdate()
    {
        if (vehicle == null || track == null)
            return;

        Vector3 tangent = track.TangentAt(progress);
        Vector3 ahead = track.TangentAt(progress + 0.025f);
        float curvature = Vector3.Angle(tangent, ahead);
        float targetSpeed = (48f + skill * 30f) / (1f + curvature * 0.028f);
        targetSpeed *= 1f - Mathf.Sin(progress * 37f + seed) * (1f - skill) * 0.08f;
        speed = Mathf.MoveTowards(speed, targetSpeed, Time.fixedDeltaTime * (speed < targetSpeed ? 7f : 16f));

        progress += speed / 3.6f * Time.fixedDeltaTime / Mathf.Max(1f, track.TrackLength);
        if (progress >= 1f)
        {
            progress -= 1f;
            lap++;
        }

        Vector3 point = track.PointAt(progress);
        Vector3 forward = track.TangentAt(progress);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float racingLine = laneOffset + Mathf.Sin(progress * Mathf.PI * 6f + seed) * (1f - skill) * 0.55f;
        Vector3 position = point + right * racingLine + Vector3.up * 0.10f;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
        }
        vehicle.transform.position = position;
        vehicle.transform.rotation = rotation;

        vehicle.rpm = 650f + speed * 17f;
        vehicle.gear = speed < 16f ? 1 : speed < 34f ? 2 : 3;
        vehicle.SetInput(new VehicleController1912.VehicleInput { throttle = 0.65f, brake = 0f, steer = 0f, clutch = 0f, handbrake = 0f });
        float wheelRotation = speed / 3.6f / Mathf.Max(0.1f, vehicle.wheelRadius) * Time.fixedDeltaTime;
        Rotate(vehicle.frontLeftSpinVisual, wheelRotation);
        Rotate(vehicle.frontRightSpinVisual, wheelRotation);
        Rotate(vehicle.rearLeftSpinVisual, wheelRotation);
        Rotate(vehicle.rearRightSpinVisual, wheelRotation);
    }

    private void Rotate(Transform visual, float radians)
    {
        if (visual != null)
            visual.Rotate(Vector3.right, radians, Space.Self);
    }

    public float RaceProgress => lap + progress;
}
