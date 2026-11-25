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

    private readonly Collider[] nearby = new Collider[24];
    private Rigidbody body;
    private float continuousProgress;
    private float speed;
    private float seed;
    private float stuckTimer;
    private float progressStallTimer;
    private float progressAnchor;
    private float reverseTimer;
    private float reverseSteer;
    private int recoveryCount;
    private bool finished;

    private void Start()
    {
        seed = Random.Range(0f, 1000f);
        skill = Mathf.Clamp(skill, 0.35f, 1f);
        if (Mathf.Abs(laneOffset) < 0.01f)
            laneOffset = Random.Range(-1.4f, 1.4f);
        if (vehicle == null || track == null)
            return;
        body = vehicle.GetComponent<Rigidbody>();
        vehicle.SetTransmissionMode(true);
        vehicle.TryStartEngine();
        float nearest = NearestTrackProgress(body.position, out _);
        continuousProgress = nearest > 0.75f ? nearest - 1f : nearest;
        progress = Mathf.Repeat(continuousProgress, 1f);
        lap = Mathf.FloorToInt(continuousProgress);
        progressAnchor = continuousProgress;
    }

    private void FixedUpdate()
    {
        if (vehicle == null || track == null || body == null)
            return;
        if (race != null && race.State != RaceManager.RaceState.Racing)
        {
            speed = 0f;
            vehicle.SetInput(default);
            return;
        }
        if (finished)
        {
            vehicle.SetInput(new VehicleController1912.VehicleInput { brake = 0.45f });
            return;
        }

        float nearest = NearestTrackProgress(body.position, out float nearestDistance);
        float delta = Mathf.Repeat(nearest - progress + 0.5f, 1f) - 0.5f;
        if (Mathf.Abs(delta) < 0.08f)
            continuousProgress += delta;
        progress = Mathf.Repeat(continuousProgress, 1f);
        lap = Mathf.FloorToInt(continuousProgress);
        if (continuousProgress - progressAnchor > 0.01f)
        {
            progressAnchor = continuousProgress;
            progressStallTimer = 0f;
        }
        else
        {
            progressStallTimer += Time.fixedDeltaTime;
        }
        if (progressStallTimer > 4.5f && recoveryCount < 8)
        {
            RecoverToTrack();
            progressAnchor = continuousProgress;
            progressStallTimer = 0f;
            return;
        }

        float forwardSpeed = vehicle.ForwardSpeedMps;
        speed = Mathf.Abs(forwardSpeed) * 3.6f;
        Vector3 forward = track.TangentAt(progress);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float lookaheadMeters = Mathf.Clamp(10f + speed * 0.28f, 12f, 20f);
        float racingLine = laneOffset + Mathf.Sin(progress * Mathf.PI * 6f + seed) * (1f - skill) * 0.45f;
        Vector3 target = track.PointAt(continuousProgress + lookaheadMeters / Mathf.Max(1f, track.TrackLength)) + right * racingLine;
        Vector3 localTarget = body.transform.InverseTransformPoint(target);
        float wheelbase = EstimateWheelbase();
        float purePursuitAngle = Mathf.Atan2(2f * wheelbase * localTarget.x, Mathf.Max(0.1f, localTarget.x * localTarget.x + localTarget.z * localTarget.z));
        float steer = Mathf.Clamp(purePursuitAngle / Mathf.Max(1f, vehicle.maxSteerAngle * Mathf.Deg2Rad), -1f, 1f);

        Vector3 aheadTangent = track.TangentAt(continuousProgress + 12f / Mathf.Max(1f, track.TrackLength));
        float curvature = Vector3.Angle(forward, aheadTangent);
        float targetSpeed = Mathf.Lerp(46f, 92f, skill) / (1f + curvature * 0.012f);
        targetSpeed *= 1f - Mathf.Sin(continuousProgress * 37f + seed) * (1f - skill) * 0.06f;
        targetSpeed = Mathf.Max(24f, targetSpeed);

        float avoidanceSteer;
        float avoidanceBrake;
        float obstacleSpeed;
        if (TryFindObstacle(forward, right, out avoidanceSteer, out avoidanceBrake, out obstacleSpeed))
        {
            steer = Mathf.Clamp(steer + avoidanceSteer, -1f, 1f);
            if (obstacleSpeed > 0f)
                targetSpeed = Mathf.Min(targetSpeed, obstacleSpeed + 8f);
            if (avoidanceBrake > 0.4f && speed < 4f)
                stuckTimer += Time.fixedDeltaTime * 2f;
        }

        float throttle;
        float brake = Mathf.Max(0f, avoidanceBrake);
        if (speed < targetSpeed - 2f)
        {
            throttle = 1f;
            brake = Mathf.Max(brake, avoidanceBrake);
        }
        else if (speed > targetSpeed + 3f)
        {
            throttle = 0f;
            brake = Mathf.Max(brake, Mathf.Clamp((speed - targetSpeed) / 14f, 0.15f, 0.8f));
        }
        else
        {
            throttle = 0.35f;
        }

        bool severelyStuck = body.transform.up.y < 0.3f || nearestDistance > track.track.roadWidth * 0.65f || stuckTimer > 4.5f;
        if (severelyStuck)
        {
            RecoverToTrack();
            return;
        }

        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.fixedDeltaTime;
            throttle = 0.65f;
            brake = 0f;
            steer = reverseSteer;
            if (vehicle.gear >= 0)
                vehicle.ShiftTo(-1);
        }
        else
        {
            if (vehicle.gear < 0)
                vehicle.ShiftTo(1);
            if (speed < 1.5f && throttle > 0.01f)
                stuckTimer += Time.fixedDeltaTime;
            else
                stuckTimer = Mathf.Max(0f, stuckTimer - Time.fixedDeltaTime * 0.5f);
            if (stuckTimer > 2.5f)
            {
                reverseTimer = 1.25f;
                reverseSteer = racingLine >= 0f ? 0.65f : -0.65f;
                stuckTimer = 0f;
            }
        }

        if (!vehicle.engineRunning)
            vehicle.TryStartEngine();
        if (throttle > 0.01f)
            body.WakeUp();
        vehicle.SetInput(new VehicleController1912.VehicleInput
        {
            throttle = throttle,
            brake = brake,
            steer = steer,
            clutch = 0f,
            handbrake = 0f
        });

        if (race != null && race.TrackData != null && continuousProgress >= GameManager.Instance.GetRaceLaps())
            finished = true;
    }

    private float EstimateWheelbase()
    {
        if (vehicle.frontLeft == null || vehicle.rearLeft == null)
            return 2.8f;
        Vector3 front = vehicle.transform.InverseTransformPoint(vehicle.frontLeft.transform.position);
        Vector3 rear = vehicle.transform.InverseTransformPoint(vehicle.rearLeft.transform.position);
        return Mathf.Max(1f, Vector3.Distance(new Vector3(front.x, 0f, front.z), new Vector3(rear.x, 0f, rear.z)));
    }

    private bool TryFindObstacle(Vector3 forward, Vector3 right, out float steer, out float brake, out float obstacleSpeed)
    {
        steer = 0f;
        brake = 0f;
        obstacleSpeed = 0f;
        int count = Physics.OverlapSphereNonAlloc(body.position + forward * 4f + Vector3.up * 0.6f, 3.5f, nearby, ~0, QueryTriggerInteraction.Ignore);
        float nearestGap = float.PositiveInfinity;
        float nearestLateral = 0f;
        Rigidbody obstacle = null;
        for (int i = 0; i < count; i++)
        {
            Rigidbody other = nearby[i].attachedRigidbody;
            if (other == null || other == body)
                continue;
            VehicleController1912 candidateVehicle = other.GetComponent<VehicleController1912>();
            if (candidateVehicle != null && !candidateVehicle.isPlayer)
                continue;
            Vector3 offset = other.position - body.position;
            float gap = Vector3.Dot(offset, forward);
            float lateral = Vector3.Dot(offset, right);
            if (gap <= 0f || gap > 10f || Mathf.Abs(lateral) > 1.25f || gap >= nearestGap)
                continue;
            nearestGap = gap;
            nearestLateral = lateral;
            obstacle = other;
        }
        if (obstacle == null)
            return false;
        float side = Mathf.Abs(nearestLateral) > 0.2f ? -Mathf.Sign(nearestLateral) : (laneOffset >= 0f ? 1f : -1f);
        steer = side * Mathf.Lerp(0.9f, 0.35f, Mathf.Clamp01(nearestGap / 12f));
        VehicleController1912 otherVehicle = obstacle.GetComponent<VehicleController1912>();
        obstacleSpeed = otherVehicle != null ? Mathf.Abs(otherVehicle.SpeedKmh) : 0f;
        if (nearestGap < 5.5f)
        {
            float closingSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity - obstacle.linearVelocity, forward) * 3.6f);
            brake = Mathf.Lerp(0.8f, 0.05f, Mathf.Clamp01(nearestGap / 5.5f)) * Mathf.Clamp01(closingSpeed / 10f);
        }
        return true;
    }

    private float NearestTrackProgress(Vector3 position, out float distance)
    {
        float bestDistance = float.PositiveInfinity;
        float bestProgress = 0f;
        for (int i = 0; i < track.Samples.Count; i++)
        {
            Vector3 a = track.Samples[i];
            Vector3 b = track.Samples[(i + 1) % track.Samples.Count];
            Vector3 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector3.Dot(position - a, segment) / lengthSquared) : 0f;
            Vector3 closest = a + segment * t;
            float candidateDistance = Vector3.SqrMagnitude(position - closest);
            if (candidateDistance >= bestDistance)
                continue;
            bestDistance = candidateDistance;
            bestProgress = (i + t) / track.Samples.Count;
        }
        distance = Mathf.Sqrt(bestDistance);
        return bestProgress;
    }

    private void RecoverToTrack()
    {
        // Emergency recovery only after rollover, leaving the road, or a prolonged stall.
        // Normal racing never writes the transform or rigidbody pose.
        if (recoveryCount >= 8)
            return;
        recoveryCount++;
        float recoveredProgress = Mathf.Repeat(continuousProgress + 0.008f, 1f);
        Vector3 tangent = track.TangentAt(recoveredProgress);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        vehicle.PlaceAt(track.PointAt(recoveredProgress) + right * Mathf.Clamp(laneOffset, -1f, 1f) + Vector3.up * 0.75f, Quaternion.LookRotation(tangent, Vector3.up));
        continuousProgress = Mathf.Floor(continuousProgress) + recoveredProgress;
        progress = recoveredProgress;
        vehicle.ShiftTo(1);
        stuckTimer = 0f;
        reverseTimer = 0f;
        speed = 0f;
    }

    public float RaceProgress => continuousProgress;
}
