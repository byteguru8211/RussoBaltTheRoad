using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraController1912 : MonoBehaviour
{
    [Header("References")]
    public VehicleController1912 vehicle;
    public Camera targetCamera;

    [Header("Chase")]
    public Vector3 chasePosition = new Vector3(0f, 2.7f, -6.2f);
    public Vector3 chaseLookAt = new Vector3(0f, 0.9f, 2f);
    public float chaseFieldOfView = 57f;

    [Header("Hood")]
    public Vector3 hoodPosition = new Vector3(-0.31f, 1.28f, 0.15f);
    public Vector3 hoodLookAt = new Vector3(-0.31f, 1.15f, 12f);
    public float hoodFieldOfView = 63f;

    [Header("Cockpit / first person")]
    public Vector3 cockpitPosition = new Vector3(0f, 1.38f, 0.58f);
    public Vector3 cockpitLookAt = new Vector3(0f, 1.26f, 15f);
    public Vector3 cockpitSeatOffset = new Vector3(0f, 1.16f, 0.14f);
    public float cockpitFieldOfView = 69f;

    [Header("Cinematic orbit")]
    public float orbitDistance = 7.4f;
    public float orbitHeight = 3.1f;
    public float orbitLookHeight = 0.95f;
    public float orbitFieldOfView = 51f;
    public float orbitSpeed = 0.12f;

    [Header("Dynamics")]
    public float maximumSpeedKmh = 140f;
    public float speedFieldOfViewGain = 9f;
    public float transitionSmoothTime = 0.16f;

    private CameraMode currentMode = CameraMode.Chase;
    private Vector3 positionVelocity;
    private Vector3 unshakenPosition;
    private Vector3 previousVelocity;
    private Vector3 orbitInput;
    private bool cockpitOffsetResolved;
    private float fieldOfViewVelocity;
    private float nearClipVelocity;
    private float targetAccelerationShake;
    private float accelerationShake;
    private float shake;
    private int lastResetRequestId;
    private float orbitAngle = 0.35f;
    private float orbitHeightOffset;
    private float originalNearClipPlane = 0.1f;
    private Rigidbody body;
    private bool initialized;
    private bool snapRequested;

    public CameraMode CurrentMode => currentMode;

    // Compatibility bridge for older scene/runtime code that assigned an integer mode.
    // New code should use CurrentMode, SetCamera or CycleCamera.
    public int mode
    {
        get => (int)currentMode;
        set
        {
            CameraMode legacyMode = (CameraMode)value;
            if (Enum.IsDefined(typeof(CameraMode), legacyMode))
                SetCamera(legacyMode, false);
        }
    }

    public event Action<CameraMode> CameraChanged;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        ResolveCockpitOffset();
        if (targetCamera != null)
            originalNearClipPlane = targetCamera.nearClipPlane;

        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : SaveSystem.LoadSettings();
        if (settings != null && settings.IsValidCameraMode())
        {
            if (GameManager.Instance != null && settings.cameraMode == CameraMode.Chase)
            {
                int legacyMode = GameManager.Instance.SelectedCameraMode;
                if (legacyMode >= (int)CameraMode.Hood && legacyMode <= (int)CameraMode.Cockpit)
                    settings.cameraMode = (CameraMode)legacyMode;
            }
            ApplyMode(settings.cameraMode);
            if (GameManager.Instance != null)
                GameManager.Instance.SelectedCameraMode = (int)settings.cameraMode;
        }

        initialized = true;
        lastResetRequestId = vehicle != null ? vehicle.ResetRequestId : 0;
        SnapToVehicle();
    }

    public void SetCamera(CameraMode cameraMode, bool persist = true)
    {
        if (!Enum.IsDefined(typeof(CameraMode), cameraMode))
        {
            Debug.LogWarning($"Ignoring invalid camera mode '{cameraMode}'.");
            return;
        }

        bool changed = currentMode != cameraMode;
        ApplyMode(cameraMode);
        if (persist && GameManager.Instance != null && GameManager.Instance.Settings != null)
        {
            GameManager.Instance.Settings.cameraMode = cameraMode;
            GameManager.Instance.SelectedCameraMode = (int)cameraMode;
            SaveSystem.SaveSettings(GameManager.Instance.Settings);
        }
        if (changed)
            CameraChanged?.Invoke(cameraMode);
    }

    public void SetCamera(int cameraMode, bool persist = true)
    {
        SetCamera((CameraMode)cameraMode, persist);
    }

    public void CycleCamera(int direction = 1)
    {
        int modeCount = Enum.GetValues(typeof(CameraMode)).Length;
        int next = ((int)currentMode + direction % modeCount + modeCount) % modeCount;
        SetCamera((CameraMode)next);
    }

    public void SetOrbitInput(Vector2 rightStick)
    {
        orbitInput = Vector2.ClampMagnitude(rightStick, 1f);
    }

    public void SnapToVehicle()
    {
        if (vehicle == null || targetCamera == null)
            return;

        ResolvePose(out Vector3 targetPosition, out Vector3 lookTarget, Time.unscaledDeltaTime);
        unshakenPosition = targetPosition;
        targetCamera.transform.SetPositionAndRotation(targetPosition, Quaternion.LookRotation(lookTarget - targetPosition, Vector3.up));
        targetCamera.fieldOfView = BaseFieldOfView();
        positionVelocity = Vector3.zero;
        fieldOfViewVelocity = 0f;
        ResetShakeState();
    }

    private void ApplyMode(CameraMode cameraMode)
    {
        currentMode = cameraMode;
        orbitHeightOffset = 0f;
        if (cameraMode == CameraMode.Cockpit || cameraMode == CameraMode.Hood || cameraMode == CameraMode.Rear)
            snapRequested = true;
    }

    private void ResolveReferences()
    {
        if (vehicle == null)
        {
            VehicleController1912[] candidates = FindObjectsByType<VehicleController1912>();
            foreach (VehicleController1912 candidate in candidates)
            {
                if (vehicle == null)
                    vehicle = candidate;
                if (candidate.isPlayer)
                {
                    vehicle = candidate;
                    break;
                }
            }
        }
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (body == null && vehicle != null)
            body = vehicle.GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (vehicle == null || body == null)
        {
            ResetShakeState();
            return;
        }

        Vector3 velocity = body.linearVelocity;
        targetAccelerationShake = 0f;
        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        bool shakeEnabled = settings == null || settings.cameraShake;
        if (shakeEnabled && vehicle.PlanarSpeedMps >= 0.05f)
        {
            // Use the physics timestep so render stalls do not create shake spikes.
            Vector3 acceleration = (velocity - previousVelocity) / Time.fixedDeltaTime;
            float longitudinal = Mathf.Abs(Vector3.Dot(acceleration, vehicle.transform.forward));
            float lateral = Mathf.Abs(Vector3.Dot(acceleration, vehicle.transform.right));
            targetAccelerationShake = Mathf.Clamp(Mathf.Max(0f, longitudinal - 1.5f) * 0.0015f + lateral * 0.0006f, 0f, 0.018f);
        }
        previousVelocity = velocity;
        if (vehicle.PlanarSpeedMps < 0.05f)
            targetAccelerationShake = 0f;
    }

    private void LateUpdate()
    {
        if (vehicle == null || targetCamera == null)
            return;
        if (lastResetRequestId != vehicle.ResetRequestId)
        {
            lastResetRequestId = vehicle.ResetRequestId;
            SnapToVehicle();
            return;
        }
        if (!initialized)
        {
            initialized = true;
            unshakenPosition = targetCamera.transform.position;
            previousVelocity = body != null ? body.linearVelocity : Vector3.zero;
        }

        float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        ResolvePose(out Vector3 targetPosition, out Vector3 lookTarget, deltaTime);
        float smoothTime = currentMode == CameraMode.Cockpit ? 0.035f
            : currentMode == CameraMode.Hood ? 0.065f
            : currentMode == CameraMode.Cinematic ? 0.2f
            : transitionSmoothTime;

        if (snapRequested || currentMode == CameraMode.Rear)
        {
            unshakenPosition = targetPosition;
            positionVelocity = Vector3.zero;
            snapRequested = false;
        }
        else
        {
            unshakenPosition = Vector3.SmoothDamp(unshakenPosition, targetPosition, ref positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
        }
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - unshakenPosition, Vector3.up);
        float rotationSpeed = currentMode == CameraMode.Cockpit ? 14f : currentMode == CameraMode.Cinematic ? 3.5f : 7f;
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSpeed * deltaTime));

        float speedRatio = Mathf.Clamp01(Mathf.Abs(vehicle.SpeedKmh) / Mathf.Max(1f, maximumSpeedKmh));
        float targetFieldOfView = BaseFieldOfView() + speedRatio * speedFieldOfViewGain + vehicle.Throttle * 1.25f;
        targetCamera.fieldOfView = Mathf.SmoothDamp(targetCamera.fieldOfView, targetFieldOfView, ref fieldOfViewVelocity, 0.28f, Mathf.Infinity, deltaTime);
        float targetNearClip = currentMode == CameraMode.Cockpit ? 0.025f : Mathf.Max(0.03f, originalNearClipPlane);
        targetCamera.nearClipPlane = Mathf.SmoothDamp(targetCamera.nearClipPlane, targetNearClip, ref nearClipVelocity, 0.12f, Mathf.Infinity, deltaTime);

        UpdateShake(deltaTime, speedRatio);
        targetCamera.transform.position = unshakenPosition + ShakeOffset();
    }

    private void ResolvePose(out Vector3 targetPosition, out Vector3 lookTarget, float deltaTime)
    {
        Transform transform = vehicle.transform;
        switch (currentMode)
        {
            case CameraMode.Hood:
                targetPosition = transform.TransformPoint(vehicle.cameraHoodPosition);
                lookTarget = transform.TransformPoint(vehicle.cameraHoodLookAt);
                break;
            case CameraMode.Cockpit:
                Transform seatAnchor = ResolveSeatAnchor();
                if (seatAnchor != null)
                {
                    Vector3 seatLocal = transform.InverseTransformPoint(seatAnchor.position);
                    targetPosition = transform.TransformPoint(seatLocal + cockpitSeatOffset);
                    lookTarget = targetPosition + transform.forward * 15f + transform.up * 0.08f;
                }
                else
                {
                    targetPosition = transform.TransformPoint(vehicle.cameraCockpitPosition);
                    lookTarget = transform.TransformPoint(vehicle.cameraCockpitLookAt);
                }
                break;
            case CameraMode.Rear:
                lookTarget = transform.TransformPoint(vehicle.cameraChaseLookAt);
                targetPosition = ResolveRearPosition(lookTarget);
                break;
            case CameraMode.Cinematic:
                orbitAngle += (orbitSpeed + orbitInput.x * 1.35f) * deltaTime;
                orbitHeightOffset = Mathf.Clamp(orbitHeightOffset + orbitInput.y * deltaTime * 3f, -1.4f, 2.2f);
                Vector3 orbitOffset = new Vector3(
                    Mathf.Sin(orbitAngle) * vehicle.cameraOrbitDistance,
                    Mathf.Max(1.2f, orbitHeight + orbitHeightOffset),
                    -Mathf.Cos(orbitAngle) * vehicle.cameraOrbitDistance);
                targetPosition = transform.TransformPoint(orbitOffset);
                lookTarget = transform.TransformPoint(new Vector3(0f, orbitLookHeight, 0.4f));
                targetPosition = ClampToEnvironment(targetPosition, lookTarget);
                break;
            default:
                targetPosition = transform.TransformPoint(vehicle.cameraChasePosition);
                lookTarget = transform.TransformPoint(vehicle.cameraChaseLookAt);
                targetPosition = ClampToEnvironment(targetPosition, lookTarget);
                break;
        }
    }

    private void ResolveCockpitOffset()
    {
        if (cockpitOffsetResolved || vehicle == null) return;
        DriverRigController driver = vehicle.GetComponentInChildren<DriverRigController>();
        Transform seat = ResolveSeatAnchor();
        if (driver == null || driver.head == null || seat == null) return;
        Vector3 headLocal = vehicle.transform.InverseTransformPoint(driver.head.position);
        Vector3 seatLocal = vehicle.transform.InverseTransformPoint(seat.position);
        Vector3 offset = headLocal - seatLocal + Vector3.forward * 0.12f;
        if (offset.y > 0.65f && offset.y < 1.8f && offset.magnitude < 2.2f)
        {
            cockpitSeatOffset = offset;
            cockpitOffsetResolved = true;
        }
    }

    private Transform ResolveSeatAnchor()
    {
        if (vehicle == null)
            return null;
        DriverRigController driver = vehicle.GetComponentInChildren<DriverRigController>();
        if (driver != null && driver.seatAnchor != null && driver.seatAnchor.IsChildOf(vehicle.transform))
            return driver.seatAnchor;
        foreach (Transform candidate in vehicle.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(candidate.name, "RB_DriverSeat", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.name, "DriverSeat", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.name, "SeatAnchor", System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    private Vector3 ResolveRearPosition(Vector3 lookTarget)
    {
        Transform vehicleTransform = vehicle.transform;
        BoxCollider hull = vehicle.GetComponent<BoxCollider>();
        float rearSurface = hull != null ? hull.center.z - hull.size.z * 0.5f : -2.4f;
        float hullTop = hull != null ? hull.center.y + hull.size.y * 0.5f + 0.2f : 1.9f;
        float rearHeight = Mathf.Max(1.9f, Mathf.Max(vehicle.cameraChasePosition.y, hullTop));
        float rearPosition = Mathf.Min(vehicle.cameraChasePosition.z - 0.8f, rearSurface - 1.2f);
        Vector3 desired = vehicleTransform.TransformPoint(new Vector3(0f, rearHeight, rearPosition));

        // Sweep from behind the hull so obstacles shorten the rear camera boom.
        Vector3 probeOrigin = vehicleTransform.TransformPoint(new Vector3(0f, rearHeight, rearSurface - 0.25f));
        Vector3 direction = (desired - probeOrigin).normalized;
        float maximumDistance = Vector3.Distance(probeOrigin, desired);
        RaycastHit[] hits = Physics.RaycastAll(probeOrigin, direction, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
        float nearest = maximumDistance;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.attachedRigidbody == body)
                continue;
            nearest = Mathf.Min(nearest, hit.distance);
        }
        return probeOrigin + direction * Mathf.Clamp(nearest - 0.22f, 0.2f, maximumDistance);
    }

    private Vector3 ClampToEnvironment(Vector3 desired, Vector3 lookTarget)
    {
        Vector3 direction = desired - lookTarget;
        float distance = direction.magnitude;
        if (distance < 0.2f) return desired;
        direction /= distance;
        RaycastHit[] hits = Physics.RaycastAll(lookTarget + direction * 0.6f, direction, Mathf.Max(0.1f, distance - 0.6f), ~0, QueryTriggerInteraction.Ignore);
        float nearest = distance;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.attachedRigidbody == body) continue;
            nearest = Mathf.Min(nearest, hit.distance + 0.6f);
        }
        return PushOutsideHull(lookTarget + direction * Mathf.Max(1.2f, nearest - 0.22f));
    }

    private Vector3 PushOutsideHull(Vector3 position)
    {
        if (vehicle == null) return position;
        BoxCollider hull = vehicle.GetComponent<BoxCollider>();
        if (hull == null) return position;
        Vector3 local = hull.transform.InverseTransformPoint(position);
        Vector3 center = hull.center;
        Vector3 half = hull.size * 0.5f + Vector3.one * 0.24f;
        Vector3 delta = local - center;
        if (Mathf.Abs(delta.x) >= half.x || Mathf.Abs(delta.y) >= half.y || Mathf.Abs(delta.z) >= half.z)
            return position;
        float pushX = half.x - Mathf.Abs(delta.x);
        float pushY = half.y - Mathf.Abs(delta.y);
        float pushZ = half.z - Mathf.Abs(delta.z);
        if (pushX <= pushY && pushX <= pushZ)
            local.x = center.x + (delta.x < 0f ? -half.x : half.x);
        else if (pushY <= pushZ)
            local.y = center.y + (delta.y < 0f ? -half.y : half.y);
        else
            local.z = center.z + (delta.z < 0f ? -half.z : half.z);
        return hull.transform.TransformPoint(local);
    }

    private float BaseFieldOfView()
    {
        switch (currentMode)
        {
            case CameraMode.Hood: return hoodFieldOfView;
            case CameraMode.Cockpit: return cockpitFieldOfView;
            case CameraMode.Cinematic: return orbitFieldOfView;
            default: return chaseFieldOfView;
        }
    }

    private void UpdateShake(float deltaTime, float speedRatio)
    {
        if (vehicle.PlanarSpeedMps < 0.05f)
        {
            ResetShakeState();
            return;
        }

        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        bool shakeEnabled = settings == null || settings.cameraShake;
        float accelerationTarget = shakeEnabled ? targetAccelerationShake : 0f;
        accelerationShake = Mathf.Lerp(accelerationShake, accelerationTarget, 1f - Mathf.Exp(-6f * deltaTime));

        float damageShake = shakeEnabled ? Mathf.Clamp01(vehicle.damage / 100f) * 0.009f : 0f;
        float speedShake = shakeEnabled ? speedRatio * 0.004f : 0f;
        float modeScale = currentMode == CameraMode.Cinematic ? 0.35f : 1f;
        shake = Mathf.Lerp(shake, (accelerationShake + damageShake + speedShake) * modeScale, 1f - Mathf.Exp(-5f * deltaTime));
    }

    private void ResetShakeState()
    {
        previousVelocity = body != null ? body.linearVelocity : Vector3.zero;
        targetAccelerationShake = 0f;
        accelerationShake = 0f;
        shake = 0f;
    }

    private Vector3 ShakeOffset()
    {
        if (shake <= 0.00001f)
            return Vector3.zero;
        float time = Time.unscaledTime;
        return new Vector3(
            Mathf.Sin(time * 17.3f) + 0.45f * Mathf.Sin(time * 31.7f),
            Mathf.Sin(time * 23.1f + 1.7f),
            0.5f * Mathf.Sin(time * 19.9f + 0.4f)) * shake;
    }
}
