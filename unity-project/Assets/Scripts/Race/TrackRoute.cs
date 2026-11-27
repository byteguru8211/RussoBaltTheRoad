using System;
using UnityEngine;

public sealed class TrackRoute
{
    private const float MinimumSegmentLengthSquared = 0.0001f;
    private const float MinimumRouteLength = 0.001f;

    private readonly Vector3[] points;
    private readonly float[] distances;
    public float Length { get; private set; }

    public TrackRoute(TrackData data)
    {
        if (data == null || data.waypoints == null || data.waypoints.Length < 4)
            throw new ArgumentException("Track route requires at least four waypoints.", nameof(data));
        for (int i = 0; i < data.waypoints.Length; i++)
        {
            if (!IsFinite(data.waypoints[i]))
                throw new ArgumentException($"Track waypoint {i} must be finite.", nameof(data));
            for (int j = 0; j < i; j++)
            {
                if ((data.waypoints[i] - data.waypoints[j]).sqrMagnitude == 0f)
                    throw new ArgumentException($"Track waypoints {j} and {i} coincide.", nameof(data));
            }
        }

        points = new Vector3[data.waypoints.Length * 48];
        distances = new float[points.Length + 1];
        for (int segment = 0; segment < data.waypoints.Length; segment++)
        {
            for (int step = 0; step < 48; step++)
            {
                float t = step / 48f;
                Vector3 point = CatmullRom(data.waypoints, segment, t);
                if (!IsFinite(point))
                    throw new ArgumentException($"Track route produced a non-finite sample at segment {segment}, step {step}.", nameof(data));
                points[segment * 48 + step] = point;
            }
        }
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 segment = points[i] - points[i - 1];
            float lengthSquared = segment.sqrMagnitude;
            if (!IsFinite(lengthSquared) || lengthSquared <= MinimumSegmentLengthSquared)
                throw new ArgumentException($"Track route segment {i - 1} has zero or invalid length.", nameof(data));
            Length += Mathf.Sqrt(lengthSquared);
            if (!IsFinite(Length))
                throw new ArgumentException("Track route length must be finite.", nameof(data));
            distances[i] = Length;
        }
        Vector3 closingSegment = points[0] - points[points.Length - 1];
        float closingLengthSquared = closingSegment.sqrMagnitude;
        if (!IsFinite(closingLengthSquared) || closingLengthSquared <= MinimumSegmentLengthSquared)
            throw new ArgumentException("Track route closing segment has zero or invalid length.", nameof(data));
        Length += Mathf.Sqrt(closingLengthSquared);
        distances[points.Length] = Length;
        if (!IsFinite(Length) || Length <= MinimumRouteLength)
            throw new ArgumentException("Track route length must be finite and greater than 0.001 m.", nameof(data));
    }

    public Vector3 PointAt(float progress)
    {
        return PointAtDistance(WrapProgress(progress) * Length);
    }

    public Vector3 TangentAt(float progress)
    {
        return TangentAtDistance(WrapProgress(progress) * Length);
    }

    public Vector3 RightAt(float progress)
    {
        Vector3 tangent = TangentAt(progress);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        return right.sqrMagnitude > 0.001f ? right : Vector3.right;
    }

    public Vector3 NormalAt(float progress)
    {
        Vector3 tangent = TangentAt(progress);
        return Vector3.Cross(tangent, RightAt(progress)).normalized;
    }

    public Vector3 PointAtDistance(float distance)
    {
        if (points.Length == 0 || Length <= MinimumRouteLength) return Vector3.zero;
        distance = Mathf.Repeat(distance, Length);
        int index = FindSegment(distance);
        float segmentLength = distances[index + 1] - distances[index];
        float t = segmentLength > MinimumSegmentLengthSquared ? (distance - distances[index]) / segmentLength : 0f;
        return Vector3.Lerp(points[index], points[(index + 1) % points.Length], t);
    }

    public Vector3 TangentAtDistance(float distance)
    {
        if (points.Length == 0 || Length <= MinimumRouteLength) return Vector3.forward;
        distance = Mathf.Repeat(distance, Length);
        int index = FindSegment(distance);
        return (points[(index + 1) % points.Length] - points[index]).normalized;
    }

    public void FrameAt(float progress, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal)
    {
        point = PointAt(progress);
        tangent = TangentAt(progress);
        right = RightAt(progress);
        normal = Vector3.Cross(tangent, right).normalized;
    }

    public float Curvature(float progress, float lookAheadMeters = 24f)
    {
        Vector3 current = TangentAt(progress);
        Vector3 ahead = TangentAtDistance(WrapProgress(progress) * Length + lookAheadMeters);
        return Mathf.Abs(Vector3.SignedAngle(current, ahead, Vector3.up)) * Mathf.Deg2Rad / Mathf.Max(1f, lookAheadMeters);
    }

    public float NearestProgress(Vector3 position, out float lateralDistance, out float centerDistance)
    {
        float bestSquared = float.PositiveInfinity;
        float bestProgress = 0f;
        float bestLateral = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Length];
            Vector3 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > MinimumSegmentLengthSquared ? Mathf.Clamp01(Vector3.Dot(position - a, segment) / lengthSquared) : 0f;
            Vector3 closest = a + segment * t;
            float candidateSquared = Vector3.SqrMagnitude(position - closest);
            if (candidateSquared >= bestSquared) continue;
            bestSquared = candidateSquared;
            float segmentLength = Mathf.Sqrt(lengthSquared);
            float distance = distances[i] + segmentLength * t;
            bestProgress = WrapProgress(distance / Length);
            Vector3 tangent = segment / Mathf.Max(0.0001f, segmentLength);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            bestLateral = Vector3.Dot(position - closest, right);
        }
        centerDistance = Mathf.Sqrt(bestSquared);
        lateralDistance = bestLateral;
        return bestProgress;
    }

    public void GridPose(int slot, float roadWidth, out Vector3 position, out Quaternion rotation)
    {
        int row = Mathf.Max(0, slot) / 2;
        int column = Mathf.Max(0, slot) % 2;
        float lateral = column == 0 ? -1f : 1f;
        lateral *= Mathf.Clamp(roadWidth * 0.24f, 1.65f, roadWidth * 0.5f - 1.25f);
        float progress = Mathf.Repeat((-6.5f - row * 6.8f) / Length, 1f);
        FrameAt(progress, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
        position = point + right * lateral + normal * 0.055f;
        rotation = Quaternion.LookRotation(tangent, normal);
    }

    public string Validate(float roadWidth = 9f, float roadsideClearance = 6f)
    {
        var issues = new System.Text.StringBuilder();
        int count = points.Length;
        float minimumSeparation = float.PositiveInfinity;
        int minimumSeparationIndex = 0;
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            for (int j = i + 2; j < count; j++)
            {
                int jNext = (j + 1) % count;
                float alongSeparation = distances[j] - distances[i];
                float circularSeparation = System.Math.Min(alongSeparation, Length - alongSeparation);
                if (circularSeparation < 30f) continue;
                if (SegmentsCross(points[i], points[next], points[j], points[jNext]))
                {
                    issues.AppendLine($"Self-intersection near samples {i} and {j}.");
                    break;
                }
                Vector2 a = new Vector2(points[i].x, points[i].z);
                Vector2 b = new Vector2(points[j].x, points[j].z);
                float separation = Vector2.Distance(a, b);
                if (separation >= minimumSeparation) continue;
                minimumSeparation = separation;
                minimumSeparationIndex = i;
            }
        }

        float minimumRadius = float.PositiveInfinity;
        float maximumGrade = 0f;
        int minimumRadiusIndex = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 previous = points[(i - 1 + count) % count];
            Vector3 current = points[i];
            Vector3 next = points[(i + 1) % count];
            Vector3 a = current - previous;
            Vector3 b = next - current;
            Vector2 horizontalA = new Vector2(a.x, a.z);
            Vector2 horizontalB = new Vector2(b.x, b.z);
            float horizontal = horizontalA.magnitude * horizontalB.magnitude;
            if (horizontal > 0.001f)
            {
                float cross = Mathf.Abs(horizontalA.x * horizontalB.y - horizontalA.y * horizontalB.x);
                float radius = horizontal * (horizontalA + horizontalB).magnitude / Mathf.Max(0.0001f, cross * 2f);
                if (radius < minimumRadius)
                {
                    minimumRadius = radius;
                    minimumRadiusIndex = i;
                }
            }
            float run = new Vector2(b.x, b.z).magnitude;
            if (run > 0.001f)
                maximumGrade = Mathf.Max(maximumGrade, Mathf.Abs(b.y) / run);
        }

        float requiredRadius = Mathf.Max(12f, roadWidth + roadsideClearance + 2f);
        if (minimumRadius < requiredRadius)
            issues.AppendLine($"Tight radius {minimumRadius:F2} m near sample {minimumRadiusIndex}; requires at least {requiredRadius:F2} m.");
        float requiredSeparation = roadWidth + roadsideClearance + 8f;
        if (minimumSeparation < requiredSeparation)
            issues.AppendLine($"Non-adjacent route clearance {minimumSeparation:F2} m near sample {minimumSeparationIndex}; requires at least {requiredSeparation:F2} m.");
        if (maximumGrade > 0.16f)
            issues.AppendLine($"Maximum grade {maximumGrade * 100f:F1}% exceeds 16%.");
        return issues.ToString();
    }

    private static bool SegmentsCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        const float epsilon = 0.02f;
        float abX = b.x - a.x;
        float abZ = b.z - a.z;
        float cdX = d.x - c.x;
        float cdZ = d.z - c.z;
        float denominator = abX * cdZ - abZ * cdX;
        if (Mathf.Abs(denominator) < 0.0001f) return false;
        float acX = c.x - a.x;
        float acZ = c.z - a.z;
        float t = (acX * cdZ - acZ * cdX) / denominator;
        float u = (acX * abZ - acZ * abX) / denominator;
        return t > epsilon && t < 1f - epsilon && u > epsilon && u < 1f - epsilon;
    }

    private int FindSegment(float distance)
    {
        int low = 0;
        int high = points.Length - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (distances[middle + 1] < distance) low = middle + 1;
            else high = middle;
        }
        return Mathf.Clamp(low, 0, points.Length - 1);
    }

    private static float WrapProgress(float progress)
    {
        return Mathf.Repeat(progress, 1f);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static Vector3 CatmullRom(Vector3[] waypoints, int segment, float t)
    {
        int count = waypoints.Length;
        Vector3 p0 = waypoints[(segment - 1 + count) % count];
        Vector3 p1 = waypoints[segment];
        Vector3 p2 = waypoints[(segment + 1) % count];
        Vector3 p3 = waypoints[(segment + 2) % count];
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
    }
}
