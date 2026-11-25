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
    private float fieldOfViewVelocity;
    private float nearClipVelocity;
    private float accelerationShake;
    private float shake;
    private float orbitAngle = 0.35f;
    private float orbitHeightOffset;
    private float originalNearClipPlane = 0.1f;
    private Rigidbody body;
    private bool initialized;

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
        previousVelocity = body != null ? body.linearVelocity : Vector3.zero;
    }

    private void ApplyMode(CameraMode cameraMode)
    {
        currentMode = cameraMode;
        orbitHeightOffset = 0f;
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

    private void LateUpdate()
    {
        if (vehicle == null || targetCamera == null)
            return;
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

        unshakenPosition = Vector3.SmoothDamp(unshakenPosition, targetPosition, ref positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
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
                targetPosition = transform.TransformPoint(hoodPosition);
                lookTarget = transform.TransformPoint(hoodLookAt);
                break;
            case CameraMode.Cockpit:
                DriverRigController driver = vehicle.GetComponentInChildren<DriverRigController>();
                if (driver != null && driver.head != null)
                {
                    targetPosition = driver.head.position + transform.forward * 0.18f + transform.up * 0.015f;
                    lookTarget = targetPosition + transform.forward * 15f + transform.up * 0.12f;
                }
                else
                {
                    targetPosition = transform.TransformPoint(cockpitPosition);
                    lookTarget = transform.TransformPoint(cockpitLookAt);
                }
                break;
            case CameraMode.Cinematic:
                orbitAngle += (orbitSpeed + orbitInput.x * 1.35f) * deltaTime;
                orbitHeightOffset = Mathf.Clamp(orbitHeightOffset + orbitInput.y * deltaTime * 3f, -1.4f, 2.2f);
                Vector3 orbitOffset = new Vector3(
                    Mathf.Sin(orbitAngle) * orbitDistance,
                    Mathf.Max(1.2f, orbitHeight + orbitHeightOffset),
                    -Mathf.Cos(orbitAngle) * orbitDistance);
                targetPosition = transform.TransformPoint(orbitOffset);
                lookTarget = transform.TransformPoint(new Vector3(0f, orbitLookHeight, 0.4f));
                break;
            default:
                targetPosition = transform.TransformPoint(chasePosition);
                lookTarget = transform.TransformPoint(chaseLookAt);
                break;
        }
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
        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        bool shakeEnabled = settings == null || settings.cameraShake;
        float targetAccelerationShake = 0f;
        if (shakeEnabled && body != null)
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 acceleration = (velocity - previousVelocity) / deltaTime;
            float longitudinal = Mathf.Abs(Vector3.Dot(acceleration, vehicle.transform.forward));
            float lateral = Mathf.Abs(Vector3.Dot(acceleration, vehicle.transform.right));
            targetAccelerationShake = Mathf.Clamp(Mathf.Max(0f, longitudinal - 1.5f) * 0.0015f + lateral * 0.0006f, 0f, 0.018f);
            previousVelocity = velocity;
        }
        accelerationShake = Mathf.Lerp(accelerationShake, targetAccelerationShake, 1f - Mathf.Exp(-6f * deltaTime));

        float damageShake = shakeEnabled ? Mathf.Clamp01(vehicle.damage / 100f) * 0.009f : 0f;
        float speedShake = shakeEnabled ? speedRatio * 0.004f : 0f;
        float modeScale = currentMode == CameraMode.Cinematic ? 0.35f : 1f;
        shake = Mathf.Lerp(shake, (accelerationShake + damageShake + speedShake) * modeScale, 1f - Mathf.Exp(-5f * deltaTime));
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
