using System.Collections.Generic;
using UnityEngine;

public class AIDriver1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public TrackBuilder track;
    public RaceManager race;
    public float skill = 0.8f;
    public float progress;
    public int lap;
    public float laneOffset;
    public int gridSlot = -1;
    public LayerMask vehiclePerceptionMask = Physics.DefaultRaycastLayers & ~((1 << 4) | (1 << 5));
    private const int CheckpointFrameLayer = 1 << 6;
    public LayerMask checkpointFrameMask = CheckpointFrameLayer;
    public LayerMask recoveryGroundMask = Physics.DefaultRaycastLayers & ~CheckpointFrameLayer;

    private const float StartProtectionMeters = 15f;
    private const float StartProtectionSeconds = 2.5f;
    private const float LaneChangeRate = 0.85f;
    private const float MinimumFollowingGap = 2.2f;
    private const float ChassisGroundProbeDistance = 3f;
    private const float ChassisGroundContactDistance = 1.75f;
    private const float CheckpointFrameSafetyMargin = 0.45f;
    private const float CheckpointFrameSideCommitmentSeconds = 0.75f;
    // Recovery may move the physical pose backwards, never advance credited race progress.
    private static readonly float[] RecoveryProgressOffsets = { 0f, -4f, -8f, -12f };

    private readonly Collider[] nearby = new Collider[64];
    private readonly RaycastHit[] recoveryHits = new RaycastHit[32];
    private readonly HashSet<VehicleController1912> perceivedVehicles = new HashSet<VehicleController1912>();
    private readonly List<PerceivedVehicle> perceived = new List<PerceivedVehicle>();
    private readonly Vector3[] satAxes = new Vector3[15];

    private struct PerceivedVehicle
    {
        public VehicleController1912 vehicle;
        public float gap;
        public float lateral;
        public float closingSpeed;
        public float forwardSpeed;
        public float halfWidth;
        public float halfLength;
    }

    private struct TrafficInfo
    {
        public VehicleController1912 obstacle;
        public float gap;
        public float lateral;
        public float closingSpeed;
        public float timeToCollision;
        public float safeSpeedMps;
        public float brake;
        public int side;
        public bool blocked;
    }

    private struct CheckpointFrameInfo
    {
        public bool detected;
        public float safeSpeedMps;
        public float brake;
        public int side;
    }

    private TrackRoute route;
    private Rigidbody body;
    private float continuousProgress;
    private float speed;
    private float seed;
    private float wheelbase;
    private float progressStallTimer;
    private float progressAnchor;
    private float rolloverTimer;
    private float stuckTimer;
    private float reverseTimer;
    private float reverseSteer;
    private Vector3 reverseStartPosition;
    private float recoveryCooldown;
    private float lastValidProgress;
    private float nearestDistance;
    private float laneTarget;
    private int avoidanceSide;
    private float sideCommitment;
    private int checkpointFrameAvoidanceSide;
    private float checkpointFrameSideCommitment;
    private int recoveryCount;
    private bool recoveryPending;
    private bool settling;
    private float settleTimer;
    private Vector3 settlePosition;
    private Quaternion settleRotation;
    private bool finished;
    private float finishTimer;
    private float finishSide;

    private void Start()
    {
        // Keep older serialized masks safe after checkpoint frames move to their own layer.
        recoveryGroundMask = recoveryGroundMask.value & ~checkpointFrameMask.value;
        seed = Mathf.Abs(name.GetHashCode()) % 1000f;
        skill = Mathf.Clamp(skill, 0.35f, 1f);
        if (vehicle == null || track == null || track.track == null)
            return;
        body = vehicle.GetComponent<Rigidbody>();
        if (body == null)
            return;
        route = new TrackRoute(track.track);
        wheelbase = EstimateWheelbase();
        vehicle.SetTransmissionMode(true);
        vehicle.StartEngineImmediately();
        if (Mathf.Abs(laneOffset) < 0.01f)
            laneOffset = (Mathf.Sin(seed) >= 0f ? 1f : -1f) * Mathf.Clamp(track.track.roadWidth * 0.18f, 1.25f, 1.8f);
        laneTarget = laneOffset;
        float nearest = route.NearestProgress(body.position, out _, out _);
        continuousProgress = RaceManager.SignedTrackProgress(nearest);
        lastValidProgress = continuousProgress;
        progress = Mathf.Repeat(continuousProgress, 1f);
        lap = Mathf.FloorToInt(continuousProgress);
        progressAnchor = continuousProgress;
    }

    private void FixedUpdate()
    {
        if (vehicle == null || track == null || body == null || route == null)
            return;
        if (race != null && race.State == RaceManager.RaceState.Finished && !finished)
            BeginFinished();
        if (finished)
        {
            UpdateFinished(Time.fixedDeltaTime);
            return;
        }
        if (race != null && race.State != RaceManager.RaceState.Racing)
        {
            speed = 0f;
            vehicle.SetInput(default);
            return;
        }
        if (settling)
        {
            UpdateSettle(Time.fixedDeltaTime);
            return;
        }

        float dt = Time.fixedDeltaTime;
        recoveryCooldown = Mathf.Max(0f, recoveryCooldown - dt);
        UpdateTrackProgress();
        speed = Mathf.Abs(vehicle.ForwardSpeedMps) * 3.6f;
        Vector3 forward = Horizontal(track.TangentAt(progress));
        Vector3 right = track.RightAt(progress);
        TrafficInfo traffic = EvaluateTraffic(forward, right);
        CheckpointFrameInfo checkpointFrame = EvaluateCheckpointFrameAvoidance(forward, right, laneOffset);
        float chassisGroundDistance = FindChassisGroundDistance();
        bool chassisNearGround = chassisGroundDistance <= ChassisGroundContactDistance;
        float planarSpeed = vehicle.PlanarSpeedMps;
        bool stableRollover = body.transform.up.y < 0.15f && chassisNearGround && planarSpeed < 1.2f;
        rolloverTimer = stableRollover ? rolloverTimer + dt : Mathf.Max(0f, rolloverTimer - dt * 2f);

        bool offRoadStuck = nearestDistance > track.track.roadWidth * 0.72f && chassisNearGround && planarSpeed < 0.9f;
        bool blockedStuck = progressStallTimer > 6f && traffic.blocked && chassisNearGround && planarSpeed < 0.7f;
        bool stalledStuck = !IsStartProtected() && progressStallTimer > 8f && chassisNearGround && planarSpeed < 0.7f;
        bool genuinelyStuck = offRoadStuck || blockedStuck || stalledStuck;
        stuckTimer = genuinelyStuck ? stuckTimer + dt : Mathf.Max(0f, stuckTimer - dt * 2f);

        if (rolloverTimer > 1.2f && recoveryCooldown <= 0f)
        {
            recoveryPending = true;
            recoveryCooldown = 8f;
            rolloverTimer = 0f;
            if (TryRecoverToTrack())
                return;
            recoveryPending = false;
        }

        if (reverseTimer > 0f)
        {
            reverseTimer -= dt;
            if (vehicle.gear >= 0)
                vehicle.ShiftTo(-1);
            vehicle.SetInput(new VehicleController1912.VehicleInput { throttle = 0.58f, steer = reverseSteer });
            if (reverseTimer <= 0f)
            {
                bool reversedClear = Vector3.Distance(body.position, reverseStartPosition) > 2f;
                bool stillStuck = vehicle.PlanarSpeedMps < 1.2f &&
                    (nearestDistance > track.track.roadWidth * 0.72f || progressStallTimer > 5.5f);
                if (!reversedClear && stillStuck && TryRecoverToTrack())
                    return;
                recoveryPending = false;
            }
            return;
        }

        if (genuinelyStuck && stuckTimer > 4f && recoveryCooldown <= 0f)
        {
            recoveryPending = true;
            reverseTimer = 1.35f;
            reverseStartPosition = body.position;
            reverseSteer = traffic.side != 0 ? -traffic.side * 0.65f : (laneOffset >= 0f ? 0.7f : -0.7f);
            stuckTimer = 0f;
            recoveryCooldown = 5f;
            return;
        }
        if (recoveryPending)
        {
            if (TryRecoverToTrack())
                return;
            recoveryPending = false;
        }

        // A tilted airborne car is not a rollover recovery case. Never write its pose in the air.
        if (body.transform.up.y < 0.2f)
        {
            vehicle.SetInput(new VehicleController1912.VehicleInput { brake = chassisNearGround ? 1f : 0f });
            return;
        }
        if (vehicle.gear < 0)
            vehicle.ShiftTo(1);

        bool startProtected = IsStartProtected();
        if (startProtected)
        {
            avoidanceSide = 0;
            sideCommitment = 0f;
        }
        UpdateLaneTarget(dt, traffic, checkpointFrame, startProtected);

        float lookaheadMeters = Mathf.Clamp(9f + speed * 0.3f, 11f, 22f);
        float targetProgress = continuousProgress + lookaheadMeters / Mathf.Max(1f, route.Length);
        Vector3 target = route.PointAt(targetProgress) + route.RightAt(targetProgress) * laneTarget;
        float steer = ComputePurePursuitSteer(target);
        float targetSpeed = ComputeTargetSpeed();
        float releaseAge = race != null ? race.Elapsed - RaceManager.GridReleaseDelay(gridSlot) : StartProtectionSeconds;
        float ramp = Mathf.Clamp01(releaseAge / 2.4f);
        float throttleCap = Mathf.Lerp(0.22f, 1f, ramp);
        ApplySpeedTarget(steer, targetSpeed, throttleCap,
            Mathf.Max(traffic.brake, checkpointFrame.brake),
            Mathf.Min(traffic.safeSpeedMps, checkpointFrame.safeSpeedMps));

        if (race != null && race.TrackData != null && continuousProgress >= GameManager.Instance.GetRaceLaps() + 0.002f)
            BeginFinished();
    }

    private void UpdateTrackProgress()
    {
        float nearest = route.NearestProgress(body.position, out _, out nearestDistance);
        float delta = Mathf.Repeat(nearest - progress + 0.5f, 1f) - 0.5f;
        float alignment = Vector3.Dot(body.transform.forward, route.TangentAt(nearest));
        if (Mathf.Abs(delta) < 0.035f && nearestDistance < track.track.roadWidth * 1.35f && alignment > 0.1f)
        {
            continuousProgress += delta;
            if (delta > 0f && nearestDistance < track.track.roadWidth * 0.75f && alignment > 0.35f)
                lastValidProgress = continuousProgress;
        }
        progress = Mathf.Repeat(continuousProgress, 1f);
        lap = Mathf.FloorToInt(continuousProgress);
        if (continuousProgress - progressAnchor > 0.002f)
        {
            progressAnchor = continuousProgress;
            progressStallTimer = 0f;
        }
        else
        {
            progressStallTimer += Time.fixedDeltaTime;
        }
    }

    private TrafficInfo EvaluateTraffic(Vector3 forward, Vector3 right)
    {
        TrafficInfo result = new TrafficInfo { safeSpeedMps = float.PositiveInfinity };
        float perceptionRange = Mathf.Clamp(16f + speed * 0.45f, 18f, 30f);
        Collider[] overlapResults = nearby;
        int count = Physics.OverlapSphereNonAlloc(
            body.position + forward * perceptionRange * 0.45f + Vector3.up * 0.6f,
            perceptionRange * 0.55f, overlapResults, vehiclePerceptionMask, QueryTriggerInteraction.Ignore);
        if (count == overlapResults.Length)
        {
            overlapResults = Physics.OverlapSphere(
                body.position + forward * perceptionRange * 0.45f + Vector3.up * 0.6f,
                perceptionRange * 0.55f, vehiclePerceptionMask, QueryTriggerInteraction.Ignore);
            count = overlapResults.Length;
        }
        perceivedVehicles.Clear();
        perceived.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapResults[i];
            if (collider == null || collider.isTrigger)
                continue;
            VehicleController1912 otherVehicle = collider.GetComponentInParent<VehicleController1912>();
            if (otherVehicle == null || otherVehicle == vehicle || !perceivedVehicles.Add(otherVehicle))
                continue;
            Rigidbody otherBody = otherVehicle.GetComponent<Rigidbody>();
            if (otherBody == null)
                continue;
            Vector3 offset = otherBody.position - body.position;
            float gap = Vector3.Dot(offset, forward);
            float lateral = Vector3.Dot(offset, right);
            if (gap < -3f || gap > perceptionRange || Mathf.Abs(lateral) > track.track.roadWidth)
                continue;
            perceived.Add(new PerceivedVehicle
            {
                vehicle = otherVehicle,
                gap = gap,
                lateral = lateral,
                closingSpeed = Vector3.Dot(body.linearVelocity - otherBody.linearVelocity, forward),
                forwardSpeed = Vector3.Dot(otherBody.linearVelocity, forward),
                halfWidth = EstimateHalfWidth(otherVehicle, right),
                halfLength = EstimateHalfLength(otherVehicle, forward)
            });
        }

        float ownHalfWidth = EstimateHalfWidth(vehicle, right);
        float ownHalfLength = EstimateHalfLength(vehicle, forward);
        PerceivedVehicle primary = default;
        bool found = false;
        float nearestGap = float.PositiveInfinity;
        for (int i = 0; i < perceived.Count; i++)
        {
            PerceivedVehicle candidate = perceived[i];
            float clearance = ownHalfWidth + candidate.halfWidth + 0.35f;
            if (candidate.gap <= 0.1f || candidate.gap >= nearestGap || Mathf.Abs(candidate.lateral - laneTarget) >= clearance)
                continue;
            nearestGap = candidate.gap;
            primary = candidate;
            found = true;
        }
        if (!found)
            return result;

        result.obstacle = primary.vehicle;
        // Perception starts with center-to-center distance; following decisions use bumper-to-bumper clearance.
        float bumperGap = primary.gap - ownHalfLength - primary.halfLength;
        result.gap = bumperGap;
        result.lateral = primary.lateral;
        result.closingSpeed = primary.closingSpeed;
        result.timeToCollision = primary.closingSpeed > 0.1f && bumperGap > 0f
            ? bumperGap / primary.closingSpeed
            : float.PositiveInfinity;
        float headway = 1.1f + skill * 0.35f;
        float desiredGap = MinimumFollowingGap + Mathf.Max(0f, primary.forwardSpeed) * headway;
        if (bumperGap < desiredGap)
            result.safeSpeedMps = Mathf.Max(0f, primary.forwardSpeed + (bumperGap - MinimumFollowingGap) / headway);

        float comfortableTtc = 1.6f + skill * 0.7f;
        if (result.timeToCollision < comfortableTtc)
        {
            float ttcSpeed = Mathf.Max(0f, primary.forwardSpeed + (bumperGap - MinimumFollowingGap) / comfortableTtc);
            result.safeSpeedMps = Mathf.Min(result.safeSpeedMps, ttcSpeed);
            result.brake = Mathf.Max(result.brake, Mathf.Lerp(0.9f, 0.12f, Mathf.Clamp01(result.timeToCollision / comfortableTtc)));
        }

        if (!IsStartProtected())
        {
            if (avoidanceSide != 0)
            {
                // Keep a committed side until its timer expires. If that lane is blocked, brake instead of flipping sides.
                if (IsSideFeasible(avoidanceSide, primary, ownHalfWidth, forward, right))
                    result.side = avoidanceSide;
            }
            else
            {
                int preferredSide = PreferredPassSide(primary, ownHalfWidth);
                if (IsSideFeasible(preferredSide, primary, ownHalfWidth, forward, right))
                    result.side = preferredSide;
                else if (IsSideFeasible(-preferredSide, primary, ownHalfWidth, forward, right))
                    result.side = -preferredSide;
            }
        }

        float candidateLane = avoidanceSide != 0 ? LaneForSide(avoidanceSide) : laneTarget;
        float laneChangeSeconds = Mathf.Abs(candidateLane - laneTarget) / LaneChangeRate + 0.4f;
        bool enoughTimeToPass = result.side != 0 &&
            (primary.closingSpeed <= 0.1f || bumperGap > MinimumFollowingGap + primary.closingSpeed * laneChangeSeconds + 1.5f);
        if ((result.side == 0 || !enoughTimeToPass) && bumperGap < 14f)
        {
            float stoppingSpeed = Mathf.Sqrt(2f * EstimateBrakeDeceleration() * Mathf.Max(0f, bumperGap - MinimumFollowingGap));
            result.safeSpeedMps = Mathf.Min(result.safeSpeedMps, stoppingSpeed);
        }
        if (bumperGap < MinimumFollowingGap + 0.8f)
        {
            result.safeSpeedMps = 0f;
            result.brake = Mathf.Max(result.brake, 0.85f);
        }
        result.blocked = bumperGap < MinimumFollowingGap + 1.5f ||
            (result.safeSpeedMps < vehicle.PlanarSpeedMps - 0.5f && bumperGap < 6f);
        return result;
    }

    private CheckpointFrameInfo EvaluateCheckpointFrameAvoidance(Vector3 forward, Vector3 right, float baseLane)
    {
        CheckpointFrameInfo result = new CheckpointFrameInfo { safeSpeedMps = float.PositiveInfinity };
        float detectionRange = Mathf.Clamp(8f + vehicle.PlanarSpeedMps * 0.5f, 8f, 18f);
        float ownHalfLength = EstimateHalfLength(vehicle, forward);
        float ownHalfWidth = EstimateHalfWidth(vehicle, right);
        float castRadius = Mathf.Clamp(ownHalfWidth + CheckpointFrameSafetyMargin, 0.75f, 1.3f);
        Vector3 origin = body.position + forward * ownHalfLength + Vector3.up * 0.65f;
        if (!Physics.SphereCast(origin, castRadius, forward, out RaycastHit hit, detectionRange,
                checkpointFrameMask, QueryTriggerInteraction.Ignore))
            return result;

        result.detected = true;
        float gap = Mathf.Max(0f, hit.distance - CheckpointFrameSafetyMargin);
        result.safeSpeedMps = Mathf.Sqrt(2f * EstimateBrakeDeceleration() * gap);

        float obstacleLateral = Vector3.Dot(hit.point - body.position, right);
        int preferredSide = obstacleLateral > 0.05f ? -1 : obstacleLateral < -0.05f ? 1 : (baseLane >= 0f ? -1 : 1);
        if (checkpointFrameAvoidanceSide != 0 &&
            IsCheckpointFramePassLaneClear(checkpointFrameAvoidanceSide, baseLane, forward, right,
                detectionRange, ownHalfLength, ownHalfWidth, castRadius))
            preferredSide = checkpointFrameAvoidanceSide;

        if (IsCheckpointFramePassLaneClear(preferredSide, baseLane, forward, right,
                detectionRange, ownHalfLength, ownHalfWidth, castRadius))
            result.side = preferredSide;
        else if (IsCheckpointFramePassLaneClear(-preferredSide, baseLane, forward, right,
                detectionRange, ownHalfLength, ownHalfWidth, castRadius))
            result.side = -preferredSide;
        else if (gap < 3f)
            result.brake = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(gap / 3f));
        return result;
    }

    private bool IsCheckpointFramePassLaneClear(int side, float baseLane, Vector3 forward, Vector3 right,
        float detectionRange, float ownHalfLength, float ownHalfWidth, float castRadius)
    {
        float targetLane = CheckpointFrameLaneForSide(side, baseLane, right);
        float currentLane = Vector3.Dot(body.position - track.PointAt(progress), right);
        Vector3 origin = body.position + right * (targetLane - currentLane) + forward * ownHalfLength + Vector3.up * 0.65f;
        if (Physics.SphereCast(origin, castRadius, forward, out _, detectionRange,
                checkpointFrameMask, QueryTriggerInteraction.Ignore))
            return false;

        float relativeTargetLane = targetLane - currentLane;
        for (int i = 0; i < perceived.Count; i++)
        {
            PerceivedVehicle other = perceived[i];
            if (other.gap < -3f || other.gap > detectionRange + ownHalfLength)
                continue;
            float clearance = ownHalfWidth + other.halfWidth + 0.45f;
            if (Mathf.Abs(relativeTargetLane - other.lateral) < clearance)
                return false;
        }
        return true;
    }

    private float CheckpointFrameLaneForSide(int side, float baseLane, Vector3 right)
    {
        float limit = RoadLaneLimit();
        float avoidanceOffset = Mathf.Clamp(EstimateHalfWidth(vehicle, right) + 0.55f, 1.25f, 1.8f);
        return Mathf.Clamp(baseLane + side * avoidanceOffset, -limit, limit);
    }

    private int PreferredPassSide(PerceivedVehicle obstacle, float ownHalfWidth)
    {
        float sameLaneClearance = ownHalfWidth + obstacle.halfWidth + 0.35f;
        if (Mathf.Abs(obstacle.lateral - laneOffset) < sameLaneClearance)
            return laneOffset >= 0f ? 1 : -1;
        return obstacle.lateral > 0f ? -1 : 1;
    }

    private bool IsSideFeasible(int side, PerceivedVehicle primary, float ownHalfWidth, Vector3 forward, Vector3 right)
    {
        if (side == 0)
            return false;
        float lane = LaneForSide(side);
        float primaryClearance = ownHalfWidth + primary.halfWidth + 0.4f;
        if (Mathf.Abs(lane - primary.lateral) < primaryClearance)
            return false;
        for (int i = 0; i < perceived.Count; i++)
        {
            PerceivedVehicle other = perceived[i];
            if (other.vehicle == primary.vehicle || other.gap < -3f || other.gap > Mathf.Max(18f, primary.gap + 8f))
                continue;
            float clearance = ownHalfWidth + other.halfWidth + 0.4f;
            if (Mathf.Abs(lane - other.lateral) < clearance)
                return false;
        }
        return true;
    }

    private float LaneForSide(int side)
    {
        float limit = RoadLaneLimit();
        float passOffset = Mathf.Clamp(track.track.roadWidth * 0.24f, 2f, 2.7f);
        return Mathf.Clamp(laneOffset + side * passOffset, -limit, limit);
    }

    private void UpdateLaneTarget(float dt, TrafficInfo traffic, CheckpointFrameInfo checkpointFrame, bool startProtected)
    {
        sideCommitment = Mathf.Max(0f, sideCommitment - dt);
        UpdateCheckpointFrameAvoidance(dt, checkpointFrame);
        if (startProtected)
        {
            avoidanceSide = 0;
        }
        else if (traffic.side != 0)
        {
            if (avoidanceSide != traffic.side)
            {
                avoidanceSide = traffic.side;
                sideCommitment = 3f;
            }
        }
        else if (sideCommitment <= 0f)
        {
            avoidanceSide = 0;
        }

        float desiredLane;
        if (checkpointFrame.detected && checkpointFrame.side == 0)
            desiredLane = laneTarget;
        else if (checkpointFrameAvoidanceSide != 0)
            desiredLane = CheckpointFrameLaneForSide(checkpointFrameAvoidanceSide, laneOffset, track.RightAt(progress));
        else
            desiredLane = avoidanceSide != 0 ? LaneForSide(avoidanceSide) : laneOffset;
        desiredLane = Mathf.Clamp(desiredLane, -RoadLaneLimit(), RoadLaneLimit());
        laneTarget = Mathf.MoveTowards(laneTarget, desiredLane, dt * LaneChangeRate);
    }

    private void UpdateCheckpointFrameAvoidance(float dt, CheckpointFrameInfo checkpointFrame)
    {
        if (checkpointFrame.detected)
        {
            checkpointFrameAvoidanceSide = checkpointFrame.side;
            checkpointFrameSideCommitment = checkpointFrame.side != 0 ? CheckpointFrameSideCommitmentSeconds : 0f;
            return;
        }
        checkpointFrameSideCommitment = Mathf.Max(0f, checkpointFrameSideCommitment - dt);
        if (checkpointFrameSideCommitment <= 0f)
            checkpointFrameAvoidanceSide = 0;
    }

    private float ComputeTargetSpeed()
    {
        float speedMps = vehicle.PlanarSpeedMps;
        float grip = Mathf.Clamp(Mathf.Min(track.track.surfaceGrip, vehicle.surfaceGrip), 0.15f, 1.5f);
        float lateralAcceleration = 9.81f * grip * Mathf.Lerp(0.5f, 0.75f, skill);
        float brakingDeceleration = EstimateBrakeDeceleration();
        float lookaheadMeters = Mathf.Clamp(12f + speedMps * 0.6f, 12f, 50f);
        float powerToMass = vehicle.engineTorque / Mathf.Max(100f, vehicle.mass);
        float capability = Mathf.Clamp(powerToMass / 0.25f, 0.7f, 1.25f);
        float targetSpeedMps = Mathf.Lerp(58f, 102f, skill) * capability / 3.6f;
        float maxSteerRadians = Mathf.Max(1f, vehicle.maxSteerAngle) * Mathf.Deg2Rad;

        for (int i = 0; i <= 4; i++)
        {
            float distance = lookaheadMeters * i / 4f;
            float sampleProgress = continuousProgress + distance / Mathf.Max(1f, route.Length);
            float curvature = route.Curvature(sampleProgress, Mathf.Max(8f, lookaheadMeters * 0.55f));
            float curvatureForSpeed = Mathf.Max(0.00005f, curvature);
            float cornerSpeed = Mathf.Sqrt(lateralAcceleration / curvatureForSpeed);
            float requiredSteer = Mathf.Atan(wheelbase * curvatureForSpeed);
            if (requiredSteer >= maxSteerRadians)
                cornerSpeed = 0f;
            else
                cornerSpeed = Mathf.Min(cornerSpeed, Mathf.Max(0f, (maxSteerRadians / requiredSteer - 1f) / 0.028f));

            // SteeringVisualAngle is the controller's filtered, speed-reduced wheel angle.
            if (distance < 0.1f && Mathf.Abs(vehicle.SteeringVisualAngle) > 2f && Mathf.Abs(vehicle.CurrentInput.steer) > 0.85f)
            {
                float actualAngle = Mathf.Min(Mathf.Abs(vehicle.SteeringVisualAngle), maxSteerRadians) * Mathf.Deg2Rad;
                float actualCurvatureCapacity = Mathf.Tan(actualAngle) / Mathf.Max(1f, wheelbase);
                cornerSpeed = Mathf.Min(cornerSpeed, Mathf.Sqrt(lateralAcceleration / Mathf.Max(0.00005f, actualCurvatureCapacity)));
            }

            float reactionDistance = speedMps * 0.3f;
            float allowedSpeed = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * brakingDeceleration * Mathf.Max(0f, distance - reactionDistance));
            targetSpeedMps = Mathf.Min(targetSpeedMps, allowedSpeed);
        }
        targetSpeedMps *= 1f - Mathf.Sin(continuousProgress * 31f + seed) * (1f - skill) * 0.035f;
        return Mathf.Clamp(targetSpeedMps * 3.6f, 0f, 108f);
    }

    private float EstimateBrakeDeceleration()
    {
        float grip = Mathf.Clamp(Mathf.Min(track.track.surfaceGrip, vehicle.surfaceGrip), 0.15f, 1.5f);
        float torqueDeceleration = vehicle.maxBrakeTorque / Mathf.Max(100f, vehicle.mass) / Mathf.Max(0.1f, vehicle.wheelRadius) * 0.75f;
        return Mathf.Clamp(Mathf.Min(9.81f * grip * 0.7f, torqueDeceleration), 2.5f, 8f);
    }

    private void ApplySpeedTarget(float steer, float targetSpeedKmh, float throttleCap, float trafficBrake, float safeSpeedMps)
    {
        targetSpeedKmh = Mathf.Min(targetSpeedKmh, safeSpeedMps * 3.6f);
        float throttle;
        float brake = Mathf.Max(0f, trafficBrake);
        if (speed < targetSpeedKmh - 1.5f)
        {
            throttle = Mathf.Clamp01(0.25f + (targetSpeedKmh - speed) / 8f) * throttleCap;
        }
        else if (speed > targetSpeedKmh + 1f)
        {
            throttle = 0f;
            brake = Mathf.Max(brake, Mathf.Clamp((speed - targetSpeedKmh) / 9f, 0.08f, 1f));
        }
        else
        {
            throttle = 0.18f * throttleCap;
        }
        if (brake > 0.02f)
            throttle = 0f;
        if (!vehicle.engineRunning)
            vehicle.StartEngineImmediately();
        if (throttle > 0.01f)
            body.WakeUp();
        vehicle.SetInput(new VehicleController1912.VehicleInput { throttle = throttle, brake = brake, steer = steer });
    }

    private float ComputePurePursuitSteer(Vector3 target)
    {
        Vector3 localTarget = body.transform.InverseTransformPoint(target);
        float purePursuitAngle = Mathf.Atan2(2f * wheelbase * localTarget.x,
            Mathf.Max(0.1f, localTarget.x * localTarget.x + localTarget.z * localTarget.z));
        float speedReducedMaxAngle = vehicle.maxSteerAngle / (1f + vehicle.PlanarSpeedMps * 0.028f);
        return Mathf.Clamp(purePursuitAngle / Mathf.Max(0.01f, speedReducedMaxAngle * Mathf.Deg2Rad), -1f, 1f);
    }

    private void BeginFinished()
    {
        finished = true;
        finishTimer = 0f;
        finishSide = laneOffset >= 0f ? 1f : -1f;
        avoidanceSide = 0;
        sideCommitment = 0f;
        checkpointFrameAvoidanceSide = 0;
        checkpointFrameSideCommitment = 0f;
    }

    private void UpdateFinished(float dt)
    {
        finishTimer += dt;
        UpdateTrackProgress();
        speed = Mathf.Abs(vehicle.ForwardSpeedMps) * 3.6f;
        Vector3 forward = Horizontal(track.TangentAt(progress));
        Vector3 right = track.RightAt(progress);
        float currentLane = Vector3.Dot(body.position - track.PointAt(progress), right);
        float shoulderLane = finishSide * RoadLaneLimit();
        TrafficInfo traffic = EvaluateTraffic(forward, right);
        CheckpointFrameInfo checkpointFrame = EvaluateCheckpointFrameAvoidance(forward, right, shoulderLane);
        UpdateCheckpointFrameAvoidance(dt, checkpointFrame);
        float desiredLane = checkpointFrame.detected && checkpointFrame.side == 0
            ? laneTarget
            : checkpointFrameAvoidanceSide != 0
                ? CheckpointFrameLaneForSide(checkpointFrameAvoidanceSide, shoulderLane, right)
                : shoulderLane;
        laneTarget = Mathf.MoveTowards(laneTarget, desiredLane, dt * 0.65f);
        float lookaheadMeters = Mathf.Clamp(7f + speed * 0.22f, 8f, 15f);
        float targetProgress = continuousProgress + lookaheadMeters / Mathf.Max(1f, route.Length);
        Vector3 target = track.PointAt(targetProgress) + track.RightAt(targetProgress) * laneTarget;
        float steer = ComputePurePursuitSteer(target);
        float targetSpeed = Mathf.Lerp(16f, 6f, Mathf.Clamp01(finishTimer / 6f));
        if (finishTimer > 3f && Mathf.Abs(currentLane) > track.track.roadWidth * 0.33f)
            targetSpeed = 0f;
        ApplySpeedTarget(steer, targetSpeed, 0.55f,
            Mathf.Max(traffic.brake, checkpointFrame.brake),
            Mathf.Min(traffic.safeSpeedMps, checkpointFrame.safeSpeedMps));
    }

    private bool IsStartProtected()
    {
        if (race != null && race.Elapsed < StartProtectionSeconds)
            return true;
        if (continuousProgress >= 1f)
            return false;
        float startDistance = (progress > 0.5f ? progress - 1f : progress) * route.Length;
        return startDistance > -12f && startDistance < StartProtectionMeters;
    }

    private float RoadLaneLimit()
    {
        return Mathf.Max(0f, track.track.roadWidth * 0.5f - EstimateHalfWidth(vehicle, track.RightAt(progress)) - 0.2f);
    }

    private float FindChassisGroundDistance()
    {
        RaycastHit[] hits = RaycastGround(body.position, Vector3.down, ChassisGroundProbeDistance, out int count);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<VehicleController1912>() != null)
                continue;
            nearest = Mathf.Min(nearest, hits[i].distance);
        }
        return nearest;
    }

    private RaycastHit[] RaycastGround(Vector3 origin, Vector3 direction, float maxDistance, out int count)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin, direction, recoveryHits, maxDistance, recoveryGroundMask, QueryTriggerInteraction.Ignore);
        if (hitCount == recoveryHits.Length)
        {
            RaycastHit[] allHits = Physics.RaycastAll(origin, direction, maxDistance, recoveryGroundMask, QueryTriggerInteraction.Ignore);
            count = allHits.Length;
            return allHits;
        }
        count = hitCount;
        return recoveryHits;
    }

    private bool TryRecoverToTrack()
    {
        if (recoveryCount >= 20 || !FindRecoveryPose(out float recoveredProgress, out Vector3 position, out Quaternion rotation))
            return false;
        recoveryPending = false;
        reverseTimer = 0f;
        recoveryCount++;
        recoveryCooldown = 6f;
        settling = true;
        settleTimer = 0.65f;
        settlePosition = position;
        settleRotation = rotation;
        body.isKinematic = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        vehicle.SetSimulationEnabled(false);
        vehicle.SetInput(default);
        vehicle.PlaceAt(position, rotation);
        continuousProgress = recoveredProgress;
        progress = Mathf.Repeat(recoveredProgress, 1f);
        lap = Mathf.FloorToInt(recoveredProgress);
        lastValidProgress = recoveredProgress;
        progressAnchor = recoveredProgress;
        progressStallTimer = 0f;
        stuckTimer = 0f;
        rolloverTimer = 0f;
        speed = 0f;
        laneTarget = Vector3.Dot(position - track.PointAt(progress), track.RightAt(progress));
        return true;
    }

    private void UpdateSettle(float dt)
    {
        body.isKinematic = true;
        vehicle.SetSimulationEnabled(false);
        vehicle.SetInput(default);
        settleTimer -= dt;
        if (settleTimer > 0f)
            return;
        if (IsRecoveryPoseClear(settlePosition, settleRotation))
        {
            settling = false;
            body.isKinematic = false;
            vehicle.SetSimulationEnabled(true);
            body.WakeUp();
            vehicle.ShiftTo(1);
            vehicle.StartEngineImmediately();
            return;
        }

        // Another car may have entered the cell during the settle. Stay kinematic and find a free cell.
        if (FindRecoveryPose(out float recoveredProgress, out Vector3 position, out Quaternion rotation))
        {
            settlePosition = position;
            settleRotation = rotation;
            settleTimer = 0.5f;
            vehicle.PlaceAt(position, rotation);
            continuousProgress = recoveredProgress;
            progress = Mathf.Repeat(recoveredProgress, 1f);
            lap = Mathf.FloorToInt(recoveredProgress);
            lastValidProgress = recoveredProgress;
            progressAnchor = recoveredProgress;
            laneTarget = Vector3.Dot(position - track.PointAt(progress), track.RightAt(progress));
        }
        else
        {
            settleTimer = 0.25f;
        }
    }

    private bool FindRecoveryPose(out float recoveredProgress, out Vector3 position, out Quaternion rotation)
    {
        GetLocalColliderBounds(vehicle, out Vector3 boundsCenter, out Vector3 boundsExtents);
        float laneLimit = RoadLaneLimit();
        for (int i = 0; i < RecoveryProgressOffsets.Length; i++)
        {
            float candidateProgress = lastValidProgress + RecoveryProgressOffsets[i] / Mathf.Max(1f, route.Length);
            Vector3 center = route.PointAt(candidateProgress);
            Vector3 tangent = route.TangentAt(candidateProgress);
            Vector3 right = route.RightAt(candidateProgress);
            Vector3 roadNormal = route.NormalAt(candidateProgress);
            for (int laneIndex = 0; laneIndex < 5; laneIndex++)
            {
                float lane = laneIndex switch
                {
                    0 => Mathf.Clamp(laneOffset, -laneLimit, laneLimit),
                    1 => 0f,
                    2 => Mathf.Clamp(-laneOffset, -laneLimit, laneLimit),
                    3 => laneLimit * 0.75f,
                    _ => -laneLimit * 0.75f
                };
                Vector3 laneCenter = center + right * lane;
                Vector3 groundNormal = roadNormal;
                Vector3 candidatePosition;
                if (TryFindRecoveryGround(laneCenter, roadNormal, out Vector3 hitPoint, out Vector3 hitNormal))
                {
                    candidatePosition = hitPoint;
                    groundNormal = hitNormal;
                }
                else
                {
                    candidatePosition = laneCenter;
                }
                Vector3 slopeTangent = tangent - groundNormal * Vector3.Dot(tangent, groundNormal);
                if (slopeTangent.sqrMagnitude < 0.001f)
                    slopeTangent = tangent;
                Quaternion candidateRotation = Quaternion.LookRotation(slopeTangent.normalized, groundNormal.normalized);
                float lowest = LowestBoundsProjection(candidatePosition, candidateRotation, boundsCenter, boundsExtents, groundNormal);
                candidatePosition += groundNormal.normalized * (0.09f - lowest);
                if (!IsRecoveryPoseClear(candidatePosition, candidateRotation))
                    continue;
                recoveredProgress = candidateProgress;
                position = candidatePosition;
                rotation = candidateRotation;
                return true;
            }
        }
        recoveredProgress = lastValidProgress;
        position = body.position;
        rotation = body.rotation;
        return false;
    }

    private bool TryFindRecoveryGround(Vector3 center, Vector3 normal, out Vector3 point, out Vector3 hitNormal)
    {
        RaycastHit[] hits = RaycastGround(center + normal * 3.5f, -normal, 7f, out int count);
        float nearest = float.PositiveInfinity;
        point = center;
        hitNormal = normal;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<VehicleController1912>() != null)
                continue;
            if (hits[i].distance >= nearest || Vector3.Dot(hits[i].normal, normal) < 0.1f)
                continue;
            nearest = hits[i].distance;
            point = hits[i].point;
            hitNormal = hits[i].normal;
            found = true;
        }
        return found;
    }

    private bool IsRecoveryPoseClear(Vector3 position, Quaternion rotation)
    {
        GetLocalColliderBounds(vehicle, out Vector3 ownCenter, out Vector3 ownExtents);
        Vector3 candidateCenter = position + rotation * ownCenter;
        if (Physics.CheckBox(candidateCenter, ownExtents + Vector3.one * 0.35f, rotation,
                checkpointFrameMask, QueryTriggerInteraction.Ignore))
            return false;
        foreach (VehicleController1912 other in FindObjectsByType<VehicleController1912>())
        {
            if (other == null || other == vehicle || !other.gameObject.activeInHierarchy)
                continue;
            Rigidbody otherBody = other.GetComponent<Rigidbody>();
            if (otherBody == null)
                continue;
            Vector3 planarOffset = otherBody.position - candidateCenter;
            planarOffset.y = 0f;
            if (planarOffset.sqrMagnitude < 16f)
                return false;
            GetLocalColliderBounds(other, out Vector3 otherCenter, out Vector3 otherExtents);
            Vector3 otherWorldCenter = otherBody.position + otherBody.rotation * otherCenter;
            if (OrientedBoxesOverlap(candidateCenter, ownExtents, rotation, otherWorldCenter, otherExtents, otherBody.rotation, 0.35f))
                return false;
        }
        return true;
    }

    private bool OrientedBoxesOverlap(Vector3 centerA, Vector3 extentsA, Quaternion rotationA,
        Vector3 centerB, Vector3 extentsB, Quaternion rotationB, float margin)
    {
        Vector3[] axesA = { rotationA * Vector3.right, rotationA * Vector3.up, rotationA * Vector3.forward };
        Vector3[] axesB = { rotationB * Vector3.right, rotationB * Vector3.up, rotationB * Vector3.forward };
        int axisCount = 0;
        for (int i = 0; i < 3; i++)
        {
            satAxes[axisCount++] = axesA[i];
            satAxes[axisCount++] = axesB[i];
        }
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Vector3 axis = Vector3.Cross(axesA[i], axesB[j]);
                if (axis.sqrMagnitude > 0.000001f)
                    satAxes[axisCount++] = axis.normalized;
            }
        }
        Vector3 delta = centerB - centerA;
        for (int i = 0; i < axisCount; i++)
        {
            Vector3 axis = satAxes[i];
            float radiusA = extentsA.x * Mathf.Abs(Vector3.Dot(axis, axesA[0])) +
                extentsA.y * Mathf.Abs(Vector3.Dot(axis, axesA[1])) +
                extentsA.z * Mathf.Abs(Vector3.Dot(axis, axesA[2]));
            float radiusB = extentsB.x * Mathf.Abs(Vector3.Dot(axis, axesB[0])) +
                extentsB.y * Mathf.Abs(Vector3.Dot(axis, axesB[1])) +
                extentsB.z * Mathf.Abs(Vector3.Dot(axis, axesB[2]));
            if (Mathf.Abs(Vector3.Dot(delta, axis)) > radiusA + radiusB + margin)
                return false;
        }
        return true;
    }

    private void GetLocalColliderBounds(VehicleController1912 target, out Vector3 center, out Vector3 extents)
    {
        Vector3 minimum = Vector3.positiveInfinity;
        Vector3 maximum = Vector3.negativeInfinity;
        bool found = false;
        foreach (Collider collider in target.GetComponentsInChildren<Collider>())
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            Bounds bounds = collider.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 local = target.transform.InverseTransformPoint(world);
                minimum = Vector3.Min(minimum, local);
                maximum = Vector3.Max(maximum, local);
            }
            found = true;
        }
        if (!found)
        {
            center = new Vector3(0f, 0.6f, 0f);
            extents = new Vector3(0.9f, 0.7f, 2f);
            return;
        }
        center = (minimum + maximum) * 0.5f;
        extents = (maximum - minimum) * 0.5f;
    }

    private float LowestBoundsProjection(Vector3 position, Quaternion rotation, Vector3 center, Vector3 extents, Vector3 normal)
    {
        float lowest = float.PositiveInfinity;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 signs = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
            Vector3 world = position + rotation * (center + Vector3.Scale(extents, signs));
            lowest = Mathf.Min(lowest, Vector3.Dot(world - position, normal));
        }
        return lowest;
    }

    private float EstimateHalfWidth(VehicleController1912 target, Vector3 right)
    {
        float halfWidth = 0.75f;
        foreach (Collider collider in target.GetComponentsInChildren<Collider>())
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            Vector3 extents = collider.bounds.extents;
            float projected = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.y) * extents.y + Mathf.Abs(right.z) * extents.z;
            halfWidth = Mathf.Max(halfWidth, projected);
        }
        return halfWidth;
    }

    private float EstimateHalfLength(VehicleController1912 target, Vector3 forward)
    {
        float halfLength = 2f;
        foreach (Collider collider in target.GetComponentsInChildren<Collider>())
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            Vector3 extents = collider.bounds.extents;
            float projected = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.y) * extents.y + Mathf.Abs(forward.z) * extents.z;
            halfLength = Mathf.Max(halfLength, projected);
        }
        return halfLength;
    }

    private float EstimateWheelbase()
    {
        WheelCollider front = vehicle.frontLeft != null ? vehicle.frontLeft : vehicle.frontRight;
        WheelCollider rear = vehicle.rearLeft != null ? vehicle.rearLeft : vehicle.rearRight;
        if (front == null || rear == null)
            return 2.8f;
        Vector3 frontPosition = vehicle.transform.InverseTransformPoint(front.transform.position);
        Vector3 rearPosition = vehicle.transform.InverseTransformPoint(rear.transform.position);
        return Mathf.Max(1f, Vector3.Distance(new Vector3(frontPosition.x, 0f, frontPosition.z), new Vector3(rearPosition.x, 0f, rearPosition.z)));
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
    }

    public float RaceProgress => continuousProgress;
}
