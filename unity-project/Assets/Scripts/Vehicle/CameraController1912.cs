using UnityEngine;

public class CameraController1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public Camera targetCamera;
    public int mode;

    private Vector3 velocity;
    private float shake;

    public void CycleCamera()
    {
        mode = (mode + 1) % 3;
    }

    private void LateUpdate()
    {
        if (vehicle == null || targetCamera == null)
            return;

        Vector3 targetPosition;
        Vector3 lookTarget;
        if (mode == 0)
        {
            targetPosition = vehicle.transform.TransformPoint(new Vector3(0f, 2.7f, -6.2f));
            lookTarget = vehicle.transform.TransformPoint(new Vector3(0f, 0.9f, 2.0f));
        }
        else if (mode == 1)
        {
            targetPosition = vehicle.transform.TransformPoint(new Vector3(-0.31f, 1.28f, 0.15f));
            lookTarget = vehicle.transform.TransformPoint(new Vector3(-0.31f, 1.15f, 12f));
        }
        else
        {
            targetPosition = vehicle.transform.TransformPoint(new Vector3(0f, 1.18f, 0.85f));
            lookTarget = vehicle.transform.TransformPoint(new Vector3(0f, 1.05f, 15f));
        }

        targetCamera.transform.position = Vector3.SmoothDamp(targetCamera.transform.position, targetPosition, ref velocity, mode == 0 ? 0.12f : 0.04f);
        Quaternion lookRotation = Quaternion.LookRotation(lookTarget - targetCamera.transform.position, Vector3.up);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, lookRotation, Time.deltaTime * 6f);

        bool shakeEnabled = GameManager.Instance == null || GameManager.Instance.Settings.cameraShake;
        float targetShake = shakeEnabled ? Mathf.Clamp01(vehicle.SpeedKmh / 100f) * 0.012f + vehicle.damage * 0.00003f : 0f;
        shake = Mathf.Lerp(shake, targetShake, Time.deltaTime * 2f);
        targetCamera.transform.position += Random.insideUnitSphere * shake;
    }
}
