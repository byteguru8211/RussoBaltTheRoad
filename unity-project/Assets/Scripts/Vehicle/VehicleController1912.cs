using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController1912 : MonoBehaviour
{
    [Serializable]
    public struct VehicleInput
    {
        public float throttle;
        public float brake;
        public float steer;
        public float clutch;
        public float handbrake;
    }

    [Header("Wheels")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;
    public Transform frontLeftSteerVisual;
    public Transform frontRightSteerVisual;
    public Transform frontLeftSpinVisual;
    public Transform frontRightSpinVisual;
    public Transform rearLeftSpinVisual;
    public Transform rearRightSpinVisual;
    public Transform steeringWheelVisual;

    [Header("Period setup")]
    public float mass = 1300f;
    public float engineTorque = 310f;
    public float maxSteerAngle = 30f;
    public float maxBrakeTorque = 2600f;
    public float finalDrive = 3.8f;
    public float wheelRadius = 0.44f;
    public float surfaceGrip = 1f;
    public bool isPlayer = true;

    [Header("State")]
    public int gear = 1;
    public bool engineRunning;
    public float rpm = 600f;
    public float fuel = 100f;
    public float temperature = 20f;
    public float damage;
    public float distanceSinceReset;
    public float handCrankTimer;

    private Rigidbody body;
    private VehicleInput input;
    private float clutchEngagement = 1f;
    private float steeringVisualAngle;
    private float lastPosition;
    private float repairCooldown;

    public float SpeedKmh => body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
    public float Throttle => input.throttle;
    public float GearDisplay => gear < 0 ? 0f : gear;
    public bool Reverse => gear < 0;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.mass = mass;
        body.centerOfMass = new Vector3(0f, 0.42f, 0f);
        body.linearDamping = 0.06f;
        body.angularDamping = 0.85f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ConfigureWheel(frontLeft, 0.18f, 0.55f);
        ConfigureWheel(frontRight, 0.18f, 0.55f);
        ConfigureWheel(rearLeft, 0.22f, 0.60f);
        ConfigureWheel(rearRight, 0.22f, 0.60f);
    }

    private void ConfigureWheel(WheelCollider wheel, float forwardStiffness, float sideStiffness)
    {
        if (wheel == null)
            return;
        wheel.radius = wheelRadius;
        wheel.suspensionDistance = 0.16f;
        wheel.suspensionSpring = new JointSpring { spring = 42000f, damper = 4500f, targetPosition = 0.5f };
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = forwardStiffness * surfaceGrip;
        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = sideStiffness * surfaceGrip;
        wheel.forwardFriction = forward;
        wheel.sidewaysFriction = sideways;
    }

    public void SetSurfaceGrip(float value)
    {
        surfaceGrip = value;
        ConfigureWheel(frontLeft, 0.18f, 0.55f);
        ConfigureWheel(frontRight, 0.18f, 0.55f);
        ConfigureWheel(rearLeft, 0.22f, 0.60f);
        ConfigureWheel(rearRight, 0.22f, 0.60f);
    }

    public void SetInput(VehicleInput value)
    {
        input = value;
    }

    public void TryStartEngine()
    {
        if (engineRunning || fuel <= 0f || handCrankTimer > 0f)
            return;
        handCrankTimer = 1.4f;
        GetComponent<EngineAudio1912>()?.PlayStarter();
        Invoke(nameof(CompleteStart), 1.1f);
    }

    private void CompleteStart()
    {
        engineRunning = true;
        rpm = 650f;
        handCrankTimer = 0f;
        if (isPlayer && AchievementSystem.Instance != null)
            AchievementSystem.Instance.Unlock("first_start");
    }

    public void ShiftUp() => ShiftTo(gear + 1);
    public void ShiftDown() => ShiftTo(gear - 1);

    public void ShiftTo(int nextGear)
    {
        nextGear = Mathf.Clamp(nextGear, -1, 3);
        if (nextGear == gear)
            return;
        gear = nextGear;
        clutchEngagement = 0f;
        rpm = Mathf.Max(rpm, 750f);
    }

    public void Repair(float amount = 35f)
    {
        damage = Mathf.Max(0f, damage - amount);
        temperature = Mathf.Max(20f, temperature - 35f);
        fuel = Mathf.Min(100f, fuel + 15f);
        repairCooldown = 8f;
        if (isPlayer && AchievementSystem.Instance != null)
            AchievementSystem.Instance.Unlock("repair");
    }

    public void ResetVehicle()
    {
        Vector3 position = transform.position;
        Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.SetPositionAndRotation(position + Vector3.up * 0.8f, rotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        distanceSinceReset = 0f;
        lastPosition = position.z;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;
        repairCooldown = Mathf.Max(0f, repairCooldown - Time.fixedDeltaTime);
        handCrankTimer = Mathf.Max(0f, handCrankTimer - Time.fixedDeltaTime);

        float speed = SpeedKmh;
        float steerTarget = input.steer * maxSteerAngle / (1f + speed * 0.035f);
        frontLeft.steerAngle = Mathf.Lerp(frontLeft.steerAngle, steerTarget, Time.fixedDeltaTime * 5f);
        frontRight.steerAngle = Mathf.Lerp(frontRight.steerAngle, steerTarget, Time.fixedDeltaTime * 5f);
        steeringVisualAngle = Mathf.Lerp(steeringVisualAngle, frontLeft.steerAngle, Time.fixedDeltaTime * 6f);

        bool automaticClutch = GameManager.Instance != null && GameManager.Instance.Settings.automaticClutch;
        float targetClutch = automaticClutch ? 1f : 1f - input.clutch;
        if (automaticClutch && speed < 3f && input.brake > 0.5f)
            targetClutch = 0.15f;
        clutchEngagement = Mathf.MoveTowards(clutchEngagement, targetClutch, Time.fixedDeltaTime * 1.8f);

        float ratio = gear switch
        {
            -1 => -3.0f,
            1 => 3.2f,
            2 => 1.8f,
            3 => 1.0f,
            _ => 1.0f
        };

        float wheelRpm = rearLeft.rpm;
        float targetRpm = Mathf.Clamp(650f + Mathf.Abs(wheelRpm) * ratio * finalDrive * 0.65f, 600f, 2400f);
        if (input.clutch > 0.7f && engineRunning)
            targetRpm = 650f + input.throttle * 1500f;
        rpm = Mathf.Lerp(rpm, targetRpm, Time.fixedDeltaTime * 3.2f);

        float torqueFactor = Mathf.Clamp(1f - damage / 140f, 0.35f, 1f);
        float torque = engineRunning ? engineTorque * torqueFactor * input.throttle * clutchEngagement : 0f;
        if (fuel <= 0f || temperature >= 118f)
        {
            engineRunning = false;
            torque = 0f;
        }

        float motorTorque = torque * ratio * finalDrive * 0.72f;
        motorTorque = Mathf.Clamp(motorTorque, -1600f, 2000f);
        rearLeft.motorTorque = motorTorque * 0.5f;
        rearRight.motorTorque = motorTorque * 0.5f;
        rearLeft.brakeTorque = input.brake * maxBrakeTorque + input.handbrake * maxBrakeTorque * 0.75f;
        rearRight.brakeTorque = rearLeft.brakeTorque;
        frontLeft.brakeTorque = 0f;
        frontRight.brakeTorque = 0f;

        if (engineRunning)
        {
            fuel = Mathf.Max(0f, fuel - (0.012f + input.throttle * 0.055f) * Time.fixedDeltaTime);
            float heatTarget = 72f + input.throttle * 38f - speed * 0.18f;
            temperature = Mathf.Lerp(temperature, heatTarget, Time.fixedDeltaTime * (temperature > heatTarget ? 0.025f : 0.008f));
        }
        else
        {
            temperature = Mathf.Lerp(temperature, 20f, Time.fixedDeltaTime * 0.01f);
        }

        float travelled = Mathf.Abs(transform.position.z - lastPosition);
        if (travelled < 20f)
        {
            distanceSinceReset += travelled;
            if (isPlayer && GameManager.Instance != null)
                GameManager.Instance.RecordDistance(travelled);
            if (isPlayer && distanceSinceReset > 100000f && AchievementSystem.Instance != null)
                AchievementSystem.Instance.Unlock("hundred_km");
        }
        lastPosition = transform.position.z;

        UpdateVisuals(Time.fixedDeltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact > 4f)
            damage = Mathf.Clamp(damage + (impact - 4f) * 1.8f, 0f, 100f);
    }

    private void UpdateVisuals(float dt)
    {
        RotateWheel(frontLeftSpinVisual, frontLeft.rpm, dt);
        RotateWheel(frontRightSpinVisual, frontRight.rpm, dt);
        RotateWheel(rearLeftSpinVisual, rearLeft.rpm, dt);
        RotateWheel(rearRightSpinVisual, rearRight.rpm, dt);
        if (frontLeftSteerVisual != null) frontLeftSteerVisual.localRotation = Quaternion.Euler(0f, steeringVisualAngle, 0f);
        if (frontRightSteerVisual != null) frontRightSteerVisual.localRotation = Quaternion.Euler(0f, steeringVisualAngle, 0f);
        if (steeringWheelVisual != null)
            steeringWheelVisual.localRotation = Quaternion.Euler(0f, 0f, -steeringVisualAngle * 4f);
    }

    private void RotateWheel(Transform visual, float wheelRpm, float dt)
    {
        if (visual != null)
            visual.Rotate(Vector3.right, wheelRpm / 60f * 360f * dt, Space.Self);
    }
}
