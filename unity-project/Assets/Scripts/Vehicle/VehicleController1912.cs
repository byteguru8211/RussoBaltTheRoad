using System;
using System.Collections.Generic;
using Unity.Netcode.Components;
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
        public bool boost;
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
    public Vector3 steeringWheelAxis = new Vector3(0f, 0.777f, 0.630f);
    public float steeringWheelRatio = 4f;

    [Header("Camera mounts")]
    public Vector3 cameraChasePosition = new Vector3(0f, 2.7f, -6.2f);
    public Vector3 cameraChaseLookAt = new Vector3(0f, 0.9f, 2f);
    public Vector3 cameraHoodPosition = new Vector3(0.31f, 1.55f, 0.62f);
    public Vector3 cameraHoodLookAt = new Vector3(0.31f, 1.35f, 14f);
    public Vector3 cameraCockpitPosition = new Vector3(0.27f, 1.52f, 0.12f);
    public Vector3 cameraCockpitLookAt = new Vector3(0.27f, 1.36f, 15f);
    public float cameraOrbitDistance = 7.4f;

    [Header("Period setup")]
    public float mass = 1300f;
    public float engineTorque = 310f;
    public float maxSteerAngle = 30f;
    public float maxBrakeTorque = 2600f;
    public float finalDrive = 3.8f;
    public float wheelRadius = 0.44f;
    public float surfaceGrip = 1f;
    public bool isPlayer = true;

    [Header("Transmission")]
    public bool automaticTransmission = true;
    public float idleRpm = 650f;
    public float launchRpm = 1200f;
    public float rpmLimiter = 2300f;
    public float shiftUpRpm = 2050f;
    public float shiftDownRpm = 850f;
    public AnimationCurve torqueCurve;

    [Header("Road and aerodynamics")]
    public float frontBrakeShare = 0.6f;
    public float drivelineEfficiency = 0.85f;
    public float dragArea = 0.85f;
    public float downforceCoefficient = 1.25f;
    public float rollingResistance = 0.018f;
    public float speedSensitiveRollStability = 1.8f;
    public float speedSensitiveYawStability = 0.65f;

    [Header("State")]
    public int gear = 1;
    public bool engineRunning;
    public float rpm = 600f;
    public float fuel = 100f;
    public float temperature = 20f;
    public float damage;
    public float distanceSinceReset;
    public float handCrankTimer;

    // Indexed by gear + 1: reverse, neutral, then four forward ratios.
    private static readonly float[] GearRatios = { -3.2f, 0f, 3.4f, 2.05f, 1.3f, 0.76f };
    private const float FrontForwardStiffness = 2f;
    private const float RearForwardStiffness = 2.1f;
    private const float FrontSideStiffness = 1.55f;
    private const float RearSideStiffness = 1.65f;
    private const float AirDensity = 1.225f;
    private const float LaunchAssistMaxSpeedMps = 2f;
    private const float BoostDrainPerSecond = 0.35f;
    private const float BoostRechargePerSecond = 0.08f;
    private const float BoostRechargeDelay = 1.2f;
    private const float BoostTorqueMultiplier = 1.25f;

    private Rigidbody body;
    private VehicleInput input;
    private float clutchEngagement = 1f;
    private float steeringVisualAngle;
    private Vector3 lastPosition;
    private float repairCooldown;
    private float shiftCooldown;
    private bool simulationEnabled = true;
    private int requestedGear = 1;
    private float boostCharge = 1f;
    private float boostRechargeTimer;
    private bool boostActive;
    private Quaternion steeringWheelRestRotation;
    private TrackSurface1912.SurfaceKind currentSurface = TrackSurface1912.SurfaceKind.Dirt;
    private float surfaceRollingMultiplier = 1f;
    private readonly List<Transform> frontLeftSteerVisuals = new List<Transform>();
    private readonly List<Transform> frontRightSteerVisuals = new List<Transform>();
    private readonly List<Transform> frontLeftSpinVisuals = new List<Transform>();
    private readonly List<Transform> frontRightSpinVisuals = new List<Transform>();
    private readonly List<Transform> rearLeftSpinVisuals = new List<Transform>();
    private readonly List<Transform> rearRightSpinVisuals = new List<Transform>();

    public float SteeringVisualAngle => steeringVisualAngle;
    public float SpeedKmh => body != null ? ForwardSpeedMps * 3.6f : 0f;
    public float ForwardSpeedMps => body != null ? Vector3.Dot(body.linearVelocity, transform.forward) : 0f;
    public float PlanarSpeedMps
    {
        get
        {
            if (body == null)
                return 0f;
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    public VehicleInput CurrentInput => input;
    public float Throttle => input.throttle;
    public float GearDisplay => gear < 0 ? 0f : gear;
    public bool Reverse => gear < 0;
    public bool SimulationEnabled => simulationEnabled;
    public float BoostCharge => Mathf.Clamp01(Finite(boostCharge));
    public bool BoostActive => boostActive;
    public int ShiftRequestId { get; private set; }
    public int RequestedGear => requestedGear;
    public int StartRequestId { get; private set; }
    public int ResetRequestId { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (GameManager.Instance != null && GameManager.Instance.Settings != null)
            automaticTransmission = GameManager.Instance.Settings.automaticTransmission;
        body.mass = Mathf.Max(100f, mass);
        body.centerOfMass = new Vector3(0f, 0.42f, 0f);
        body.linearDamping = 0.025f;
        body.angularDamping = 0.85f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        lastPosition = body.position;
        requestedGear = gear;
        boostCharge = 1f;
        boostActive = false;
        boostRechargeTimer = 0f;
        if (launchRpm <= 0f)
            launchRpm = 1200f;
        if (torqueCurve == null || torqueCurve.length == 0)
        {
            // A broad, naturally aspirated period torque curve, followed by a soft rev limit.
            torqueCurve = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(idleRpm, 0.76f),
                new Keyframe(1200f, 0.96f),
                new Keyframe(1700f, 1f),
                new Keyframe(2100f, 0.88f),
                new Keyframe(rpmLimiter, 0f));
        }
        CanonicalizeWheelVisuals();
        if (steeringWheelVisual != null)
            steeringWheelRestRotation = steeringWheelVisual.localRotation;
        ConfigureWheels();
    }

    private void CanonicalizeWheelVisuals()
    {
        frontLeftSteerVisuals.Clear();
        frontRightSteerVisuals.Clear();
        frontLeftSpinVisuals.Clear();
        frontRightSpinVisuals.Clear();
        rearLeftSpinVisuals.Clear();
        rearRightSpinVisuals.Clear();
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            string name = candidate.name;
            bool steer = name.EndsWith("_steer", System.StringComparison.OrdinalIgnoreCase);
            bool spin = name.EndsWith("_spin", System.StringComparison.OrdinalIgnoreCase);
            if (name.IndexOf("wheel_", System.StringComparison.OrdinalIgnoreCase) < 0
                || name.IndexOf("spare", System.StringComparison.OrdinalIgnoreCase) >= 0
                || (!steer && !spin))
                continue;
            Vector3 local = transform.InverseTransformPoint(candidate.position);
            bool front = local.z >= 0f;
            bool left = local.x < 0f;
            if (steer)
            {
                if (front && left) frontLeftSteerVisuals.Add(candidate);
                else if (front) frontRightSteerVisuals.Add(candidate);
            }
            else
            {
                if (front && left) frontLeftSpinVisuals.Add(candidate);
                else if (front) frontRightSpinVisuals.Add(candidate);
                else if (left) rearLeftSpinVisuals.Add(candidate);
                else rearRightSpinVisuals.Add(candidate);
            }
        }
        if (frontLeftSteerVisuals.Count > 0) frontLeftSteerVisual = frontLeftSteerVisuals[0];
        if (frontRightSteerVisuals.Count > 0) frontRightSteerVisual = frontRightSteerVisuals[0];
        if (frontLeftSpinVisuals.Count > 0) frontLeftSpinVisual = frontLeftSpinVisuals[0];
        if (frontRightSpinVisuals.Count > 0) frontRightSpinVisual = frontRightSpinVisuals[0];
        if (rearLeftSpinVisuals.Count > 0) rearLeftSpinVisual = rearLeftSpinVisuals[0];
        if (rearRightSpinVisuals.Count > 0) rearRightSpinVisual = rearRightSpinVisuals[0];
        AddValidWheelVisual(frontLeftSteerVisuals, frontLeftSteerVisual, true);
        AddValidWheelVisual(frontRightSteerVisuals, frontRightSteerVisual, true);
        AddValidWheelVisual(frontLeftSpinVisuals, frontLeftSpinVisual, false);
        AddValidWheelVisual(frontRightSpinVisuals, frontRightSpinVisual, false);
        AddValidWheelVisual(rearLeftSpinVisuals, rearLeftSpinVisual, false);
        AddValidWheelVisual(rearRightSpinVisuals, rearRightSpinVisual, false);
        AlignWheelCollider(frontLeft, frontLeftSpinVisual);
        AlignWheelCollider(frontRight, frontRightSpinVisual);
        AlignWheelCollider(rearLeft, rearLeftSpinVisual);
        AlignWheelCollider(rearRight, rearRightSpinVisual);
    }

    private static void AddValidWheelVisual(List<Transform> visuals, Transform assigned, bool steer)
    {
        if (visuals.Count > 0 || assigned == null)
            return;
        string suffix = steer ? "_steer" : "_spin";
        string name = assigned.name;
        if (name.IndexOf("wheel_", System.StringComparison.OrdinalIgnoreCase) >= 0
            && name.IndexOf("spare", System.StringComparison.OrdinalIgnoreCase) < 0
            && name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            visuals.Add(assigned);
    }

    private void AlignWheelCollider(WheelCollider wheel, Transform visual)
    {
        if (wheel == null || visual == null) return;
        Vector3 local = transform.InverseTransformPoint(visual.position);
        Vector3 colliderPosition = wheel.transform.localPosition;
        colliderPosition.z = local.z;
        wheel.transform.localPosition = colliderPosition;
    }

    private void ConfigureWheels()
    {
        ConfigureWheel(frontLeft, FrontForwardStiffness, FrontSideStiffness);
        ConfigureWheel(frontRight, FrontForwardStiffness, FrontSideStiffness);
        ConfigureWheel(rearLeft, RearForwardStiffness, RearSideStiffness);
        ConfigureWheel(rearRight, RearForwardStiffness, RearSideStiffness);
    }

    private void ConfigureWheel(WheelCollider wheel, float forwardStiffness, float sideStiffness)
    {
        if (wheel == null)
            return;
        wheel.radius = Mathf.Max(0.1f, wheelRadius);
        wheel.suspensionDistance = 0.18f;
        float snowFactor = currentSurface == TrackSurface1912.SurfaceKind.Snow ? 0.92f : 1f;
        float damperFactor = currentSurface == TrackSurface1912.SurfaceKind.Snow ? 0.86f : 1f;
        wheel.suspensionSpring = new JointSpring { spring = Mathf.Max(18000f, mass * 32f) * snowFactor, damper = Mathf.Max(3000f, mass * 5.5f) * damperFactor, targetPosition = 0.5f };
        float surfaceFactor = currentSurface switch
        {
            TrackSurface1912.SurfaceKind.Ice => 0.55f,
            TrackSurface1912.SurfaceKind.Snow => 0.9f,
            TrackSurface1912.SurfaceKind.Grass => 0.7f,
            TrackSurface1912.SurfaceKind.Cobblestone => 0.96f,
            _ => 1f
        };
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = forwardStiffness * Mathf.Clamp(surfaceGrip * surfaceFactor, 0.12f, 1.5f);
        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = sideStiffness * Mathf.Clamp(surfaceGrip * surfaceFactor, 0.12f, 1.5f);
        wheel.forwardFriction = forward;
        wheel.sidewaysFriction = sideways;
    }

    public void SetSurfaceGrip(float value)
    {
        surfaceGrip = Mathf.Clamp(value, 0.15f, 1.5f);
        ConfigureWheels();
    }

    public void SetInput(VehicleInput value)
    {
        input = new VehicleInput
        {
            throttle = SanitizeAxis(value.throttle),
            brake = SanitizeAxis(value.brake),
            steer = Mathf.Clamp(Finite(value.steer), -1f, 1f),
            clutch = SanitizeAxis(value.clutch),
            handbrake = SanitizeAxis(value.handbrake),
            boost = value.boost
        };
    }

    private static float SanitizeAxis(float value)
    {
        return Mathf.Clamp01(Finite(value));
    }

    private static float Finite(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    public void SetSimulationEnabled(bool value)
    {
        simulationEnabled = value;
        if (!value)
        {
            SetInput(default);
            boostActive = false;
            ClearWheelControls();
        }
    }

    public void SetTransmissionMode(bool automatic)
    {
        automaticTransmission = automatic;
        shiftCooldown = 0f;
    }

    public void ApplyNetworkState(int networkGear, bool running, float networkRpm, float networkThrottle,
        float networkFuel = 100f, float networkTemperature = 20f, float networkDamage = 0f, float networkSteer = 0f, float networkDistance = 0f,
        int networkRequestedGear = int.MinValue, float networkBoostCharge = 1f, bool networkBoostActive = false)
    {
        gear = Mathf.Clamp(networkGear, -1, 4);
        requestedGear = networkRequestedGear >= -1 ? Mathf.Clamp(networkRequestedGear, -1, 4) : gear;
        engineRunning = running;
        rpm = Mathf.Clamp(networkRpm, 0f, rpmLimiter);
        fuel = Mathf.Clamp(networkFuel, 0f, 100f);
        temperature = Mathf.Clamp(networkTemperature, 20f, 150f);
        damage = Mathf.Clamp(networkDamage, 0f, 100f);
        distanceSinceReset = Mathf.Max(0f, networkDistance);
        boostCharge = Mathf.Clamp01(Finite(networkBoostCharge));
        boostActive = networkBoostActive && boostCharge > 0f;
        boostRechargeTimer = 0f;
        if (running)
            handCrankTimer = 0f;
        input.throttle = SanitizeAxis(networkThrottle);
        steeringVisualAngle = Mathf.Clamp(Finite(networkSteer), -1f, 1f) * maxSteerAngle;
    }

    public void TryStartEngine()
    {
        if (engineRunning || fuel <= 0f || handCrankTimer > 0f)
            return;
        StartRequestId++;
        handCrankTimer = 1.4f;
        GetComponent<EngineAudio1912>()?.PlayStarter();
        Invoke(nameof(CompleteStart), 1.1f);
    }

    public void StartEngineImmediately()
    {
        if (fuel <= 0f || temperature >= 118f)
            return;
        CancelInvoke(nameof(CompleteStart));
        engineRunning = true;
        handCrankTimer = 0f;
        rpm = Mathf.Max(idleRpm, rpm);
        temperature = Mathf.Max(72f, temperature);
        StartRequestId++;
        if (isPlayer && AchievementSystem.Instance != null)
            AchievementSystem.Instance.Unlock("first_start");
    }

    private void CompleteStart()
    {
        engineRunning = true;
        rpm = idleRpm;
        handCrankTimer = 0f;
        if (isPlayer && AchievementSystem.Instance != null)
            AchievementSystem.Instance.Unlock("first_start");
    }

    public void ShiftUp() => ShiftTo(gear + 1);
    public void ShiftDown() => ShiftTo(gear - 1);

    public void ShiftTo(int nextGear)
    {
        nextGear = Mathf.Clamp(nextGear, -1, 4);
        if (nextGear == gear && nextGear == requestedGear)
            return;
        ShiftRequestId++;
        ShiftGear(nextGear);
    }

    private void ShiftGear(int nextGear)
    {
        requestedGear = nextGear;
        if (nextGear == gear)
            return;
        gear = nextGear;
        GetComponent<EngineAudio1912>()?.PlayGearShift();
        clutchEngagement = 0f;
        shiftCooldown = 0.65f;
        rpm = Mathf.Max(rpm, idleRpm);
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
        ResetRequestId++;
        Vector3 position = body.position + Vector3.up * 0.65f;
        Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        PlaceAt(position, rotation);
        distanceSinceReset = 0f;
        lastPosition = position;
    }

    /// <summary>Places a vehicle through the same networked rigidbody path used for normal motion.</summary>
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (body == null)
            return;
        NetworkRigidbody networkedBody = GetComponent<NetworkRigidbody>();
        if (networkedBody != null)
        {
            networkedBody.SetPosition(position);
            networkedBody.SetRotation(rotation);
            if (!body.isKinematic)
            {
                networkedBody.SetLinearVelocity(Vector3.zero);
                networkedBody.SetAngularVelocity(Vector3.zero);
            }
        }
        else
        {
            body.position = position;
            body.rotation = rotation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
        transform.SetPositionAndRotation(position, rotation);
        NetworkTransform networkedTransform = GetComponent<NetworkTransform>();
        if (networkedTransform != null && networkedTransform.CanCommitToTransform)
            networkedTransform.Teleport(position, rotation, transform.lossyScale);
        body.WakeUp();
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;
        float dt = Time.fixedDeltaTime;
        repairCooldown = Mathf.Max(0f, repairCooldown - dt);
        handCrankTimer = Mathf.Max(0f, handCrankTimer - dt);
        shiftCooldown = Mathf.Max(0f, shiftCooldown - dt);

        if (!simulationEnabled || body.isKinematic)
        {
            // Disabled client copies only mirror replicated boost state; they never spend or regenerate it.
            if (simulationEnabled)
                boostActive = false;
            ClearWheelControls();
            if (isPlayer)
            {
                float visualSteer = Mathf.Clamp(input.steer, -1f, 1f) * maxSteerAngle;
                steeringVisualAngle = Mathf.Lerp(steeringVisualAngle, visualSteer, dt * 8f);
                if (frontLeft != null) frontLeft.steerAngle = steeringVisualAngle;
                if (frontRight != null) frontRight.steerAngle = steeringVisualAngle;
            }
            UpdateVisuals(dt);
            return;
        }

        float forwardSpeed = ForwardSpeedMps;
        float planarSpeed = PlanarSpeedMps;
        UpdateSurfaceFromWheels();
        float steerTarget = input.steer * maxSteerAngle / (1f + planarSpeed * 0.028f);
        steerTarget = Mathf.Clamp(steerTarget, -maxSteerAngle, maxSteerAngle);
        if (frontLeft != null)
            frontLeft.steerAngle = Mathf.Lerp(frontLeft.steerAngle, steerTarget, dt * 6f);
        if (frontRight != null)
            frontRight.steerAngle = Mathf.Lerp(frontRight.steerAngle, steerTarget, dt * 6f);
        steeringVisualAngle = Mathf.Lerp(steeringVisualAngle, steerTarget, dt * 6f);

        UpdateAutomaticTransmission(forwardSpeed);
        float ratio = GearRatio(gear);

        bool automaticClutch = GameManager.Instance != null && GameManager.Instance.Settings.automaticClutch;
        float targetClutch = 1f - input.clutch;
        if (automaticClutch && input.clutch <= 0.05f)
            targetClutch = 1f;
        if (automaticClutch && Mathf.Abs(forwardSpeed) < 1f && input.brake > 0.5f)
            targetClutch = 0.15f;
        if (shiftCooldown > 0f)
            targetClutch = Mathf.Min(targetClutch, 0.25f);
        clutchEngagement = Mathf.MoveTowards(clutchEngagement, targetClutch, dt * 2.4f);

        float wheelRpm = forwardSpeed / Mathf.Max(0.1f, wheelRadius) * 60f / (2f * Mathf.PI);
        float targetRpm = idleRpm + Mathf.Abs(wheelRpm * ratio * finalDrive);
        if (input.clutch > 0.7f && engineRunning)
            targetRpm = idleRpm + input.throttle * (rpmLimiter - idleRpm) * 0.78f;
        bool launchAssist = gear == 1 && Mathf.Abs(forwardSpeed) < LaunchAssistMaxSpeedMps
            && input.throttle > 0.05f && input.brake <= 0.05f && input.handbrake <= 0.05f && shiftCooldown <= 0f;
        if (launchAssist)
            targetRpm = Mathf.Max(targetRpm, launchRpm);
        targetRpm = Mathf.Clamp(targetRpm, idleRpm, rpmLimiter);
        rpm = Mathf.Lerp(rpm, targetRpm, dt * 6f);

        if (fuel <= 0f || temperature >= 118f)
            engineRunning = false;

        UpdateBoost(dt);
        float torqueFactor = Mathf.Clamp(1f - damage / 140f, 0.35f, 1f);
        float curveTorque = torqueCurve != null ? torqueCurve.Evaluate(rpm) : 1f;
        float limiterFactor = rpm >= rpmLimiter * 0.995f ? 0f : 1f;
        float engineTorqueNow = engineRunning
            ? engineTorque * Mathf.Max(0f, curveTorque) * torqueFactor * input.throttle * clutchEngagement * limiterFactor * (boostActive ? BoostTorqueMultiplier : 1f)
            : 0f;
        float motorTorque = engineTorqueNow * ratio * finalDrive * drivelineEfficiency;
        float rearMotorTorque = motorTorque * 0.5f;
        if (rearLeft != null) rearLeft.motorTorque = rearMotorTorque;
        if (rearRight != null) rearRight.motorTorque = rearMotorTorque;
        if (frontLeft != null) frontLeft.motorTorque = 0f;
        if (frontRight != null) frontRight.motorTorque = 0f;

        float serviceBrake = input.brake * maxBrakeTorque;
        float frontBrake = serviceBrake * frontBrakeShare + input.handbrake * maxBrakeTorque * 0.15f;
        float rearBrake = serviceBrake * (1f - frontBrakeShare) + input.handbrake * maxBrakeTorque * 0.85f;
        if (frontLeft != null) frontLeft.brakeTorque = frontBrake;
        if (frontRight != null) frontRight.brakeTorque = frontBrake;
        if (rearLeft != null) rearLeft.brakeTorque = rearBrake;
        if (rearRight != null) rearRight.brakeTorque = rearBrake;

        ApplyAeroAndStability(planarSpeed);

        if (engineRunning)
        {
            fuel = Mathf.Max(0f, fuel - (0.012f + input.throttle * 0.055f) * dt);
            float heatTarget = 72f + input.throttle * 38f - Mathf.Abs(forwardSpeed) * 3.6f * 0.18f;
            temperature = Mathf.Lerp(temperature, heatTarget, dt * (temperature > heatTarget ? 0.025f : 0.008f));
        }
        else
        {
            temperature = Mathf.Lerp(temperature, 20f, dt * 0.01f);
        }

        Vector3 position = body.position;
        Vector3 movement = position - lastPosition;
        movement.y = 0f;
        float travelled = movement.magnitude;
        if (travelled < 20f)
        {
            distanceSinceReset += travelled;
            if (isPlayer && GameManager.Instance != null)
                GameManager.Instance.RecordDistance(travelled);
            if (isPlayer && distanceSinceReset > 100000f && AchievementSystem.Instance != null)
                AchievementSystem.Instance.Unlock("hundred_km");
        }
        lastPosition = position;
        UpdateVisuals(dt);
    }

    private void UpdateBoost(float dt)
    {
        bool canBoost = input.boost && engineRunning && input.throttle > 0.05f && gear > 0
            && input.brake <= 0.05f && input.handbrake <= 0.05f && shiftCooldown <= 0f
            && simulationEnabled && body != null && !body.isKinematic && boostCharge > 0f;
        boostActive = canBoost;
        if (boostActive)
        {
            boostCharge = Mathf.Max(0f, boostCharge - BoostDrainPerSecond * dt);
            boostRechargeTimer = 0f;
            if (boostCharge <= 0f)
                boostActive = false;
        }
        else
        {
            boostRechargeTimer = Mathf.Min(BoostRechargeDelay, boostRechargeTimer + dt);
            if (boostRechargeTimer >= BoostRechargeDelay)
                boostCharge = Mathf.Min(1f, boostCharge + BoostRechargePerSecond * dt);
        }
    }

    private void UpdateAutomaticTransmission(float forwardSpeed)
    {
        if (!automaticTransmission)
            return;
        if (gear == 0 && input.throttle > 0.05f)
        {
            ShiftGear(1);
            return;
        }
        if (gear < 0)
        {
            if (forwardSpeed > 2f)
                ShiftGear(1);
            return;
        }
        if (gear <= 0)
            return;
        float ratio = GearRatio(gear);
        float wheelRpm = Mathf.Abs(forwardSpeed) / Mathf.Max(0.1f, wheelRadius) * 60f / (2f * Mathf.PI);
        float coupledRpm = idleRpm + wheelRpm * ratio * finalDrive;
        if (coupledRpm < shiftDownRpm && gear > 1)
        {
            // Select the highest lower gear that keeps the engine above the downshift threshold.
            // This can skip several ratios in one physics step instead of waiting for each cooldown.
            int targetGear = 1;
            for (int candidate = gear - 1; candidate >= 1; candidate--)
            {
                float candidateRpm = idleRpm + wheelRpm * GearRatio(candidate) * finalDrive;
                if (candidateRpm >= shiftDownRpm)
                {
                    targetGear = candidate;
                    break;
                }
            }
            ShiftGear(targetGear);
            return;
        }
        if (shiftCooldown > 0f)
            return;
        if (coupledRpm >= shiftUpRpm && gear < 4 && input.throttle > 0.05f)
            ShiftGear(gear + 1);
    }

    private static float GearRatio(int selectedGear)
    {
        int index = Mathf.Clamp(selectedGear + 1, 0, GearRatios.Length - 1);
        return GearRatios[index];
    }

    private void UpdateSurfaceFromWheels()
    {
        WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
        foreach (WheelCollider wheel in wheels)
        {
            if (wheel == null || !wheel.GetGroundHit(out WheelHit hit)) continue;
            TrackSurface1912 surface = hit.collider != null ? hit.collider.GetComponent<TrackSurface1912>() : null;
            TrackSurface1912.SurfaceKind next = surface != null ? surface.GetSurfaceKindAt(hit.point) : TrackSurface1912.SurfaceKind.Dirt;
            float rolling = surface != null ? surface.RollingResistanceMultiplier : 1f;
            if (next == currentSurface && Mathf.Abs(rolling - surfaceRollingMultiplier) < 0.01f) return;
            currentSurface = next;
            surfaceRollingMultiplier = rolling;
            ConfigureWheels();
            return;
        }
    }

    private void ApplyAeroAndStability(float planarSpeed)
    {
        if (planarSpeed < 0.05f)
            return;
        Vector3 planarVelocity = body.linearVelocity;
        planarVelocity.y = 0f;
        Vector3 direction = planarVelocity / planarSpeed;
        float groundedFactor = GroundedWheelCount / 4f;
        float rollingForce = rollingResistance * surfaceRollingMultiplier * body.mass * Mathf.Abs(Physics.gravity.y) * groundedFactor;
        float aeroForce = 0.5f * AirDensity * dragArea * planarSpeed * planarSpeed;
        body.AddForce(-direction * (rollingForce + aeroForce), ForceMode.Force);
        body.AddForce(-Vector3.up * downforceCoefficient * planarSpeed * planarSpeed, ForceMode.Force);

        float speedFactor = Mathf.Clamp01(planarSpeed / 35f);
        Vector3 localAngularVelocity = Quaternion.Inverse(body.rotation) * body.angularVelocity;
        body.AddTorque(-transform.forward * localAngularVelocity.x * planarSpeed * speedSensitiveRollStability * speedFactor);
        body.AddTorque(-transform.up * localAngularVelocity.y * planarSpeed * speedSensitiveYawStability * speedFactor);
    }

    private int GroundedWheelCount
    {
        get
        {
            int count = 0;
            if (frontLeft != null && frontLeft.isGrounded) count++;
            if (frontRight != null && frontRight.isGrounded) count++;
            if (rearLeft != null && rearLeft.isGrounded) count++;
            if (rearRight != null && rearRight.isGrounded) count++;
            return count;
        }
    }

    private void ClearWheelControls()
    {
        if (frontLeft != null) { frontLeft.motorTorque = 0f; frontLeft.brakeTorque = 0f; frontLeft.steerAngle = 0f; }
        if (frontRight != null) { frontRight.motorTorque = 0f; frontRight.brakeTorque = 0f; frontRight.steerAngle = 0f; }
        if (rearLeft != null) { rearLeft.motorTorque = 0f; rearLeft.brakeTorque = 0f; }
        if (rearRight != null) { rearRight.motorTorque = 0f; rearRight.brakeTorque = 0f; }
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact > 4f && simulationEnabled)
            damage = Mathf.Clamp(damage + (impact - 4f) * 1.8f, 0f, 100f);
    }

    private void UpdateVisuals(float dt)
    {
        RotateWheels(frontLeftSpinVisuals, frontLeft != null ? frontLeft.rpm : 0f, dt);
        RotateWheels(frontRightSpinVisuals, frontRight != null ? frontRight.rpm : 0f, dt);
        RotateWheels(rearLeftSpinVisuals, rearLeft != null ? rearLeft.rpm : 0f, dt);
        RotateWheels(rearRightSpinVisuals, rearRight != null ? rearRight.rpm : 0f, dt);
        foreach (Transform visual in frontLeftSteerVisuals)
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, steeringVisualAngle);
        foreach (Transform visual in frontRightSteerVisuals)
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, steeringVisualAngle);
        if (steeringWheelVisual != null)
        {
            Vector3 axis = steeringWheelAxis.sqrMagnitude > 0.001f ? steeringWheelAxis.normalized : Vector3.up;
            steeringWheelVisual.localRotation = steeringWheelRestRotation * Quaternion.AngleAxis(steeringVisualAngle * steeringWheelRatio, axis);
        }
    }

    private void RotateWheels(List<Transform> visuals, float wheelRpm, float dt)
    {
        foreach (Transform visual in visuals)
            RotateWheel(visual, wheelRpm, dt);
    }

    private void RotateWheel(Transform visual, float wheelRpm, float dt)
    {
        if (visual != null)
            visual.Rotate(Vector3.right, wheelRpm / 60f * 360f * dt, Space.Self);
    }
}
