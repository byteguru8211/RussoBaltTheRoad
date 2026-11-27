using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ReleaseValidation
{
    [MenuItem("RussoBalt/Validate v0.7 Content")]
    private static void ValidateMenu() => Run();

    public static string Run()
    {
        var report = new StringBuilder();
        var errors = new List<string>();
        var warnings = new List<string>();

        TrackData[] tracks = Resources.LoadAll<TrackData>("Tracks");
        report.AppendLine($"Tracks: {tracks.Length}");
        if (tracks.Length < 8) errors.Add("Expected at least eight tracks.");
        foreach (TrackData track in tracks)
        {
            if (track == null || track.waypoints == null || track.waypoints.Length < 4)
            {
                errors.Add($"{track?.name}: invalid waypoints.");
                continue;
            }
            try
            {
                TrackRoute route = new TrackRoute(track);
                string validation = route.Validate();
                if (!string.IsNullOrWhiteSpace(validation))
                    warnings.Add($"{track.id}: {validation.Trim()}");
                report.AppendLine($"  {track.id}: {route.Length:F1} m, width {track.roadWidth:F1} m, AI {track.aiCount}");
            }
            catch (Exception exception)
            {
                errors.Add($"{track.id}: {exception.Message}");
            }
        }

        string[] vehiclePaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Vehicles" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        report.AppendLine($"Vehicle prefabs: {vehiclePaths.Length}");
        foreach (string path in vehiclePaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            Transform[] wheels = prefab.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name.IndexOf("wheel_", StringComparison.OrdinalIgnoreCase) >= 0
                    && transform.name.IndexOf("spare", StringComparison.OrdinalIgnoreCase) < 0
                    && (transform.name.EndsWith("_spin", StringComparison.OrdinalIgnoreCase)
                        || transform.name.EndsWith("_steer", StringComparison.OrdinalIgnoreCase))).ToArray();
            if (wheels.Length != 6) errors.Add($"{prefab.name}: expected 6 wheel pivots, found {wheels.Length}.");
            Transform driver = prefab.transform.Find("Driver_1912");
            Transform seat = prefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => string.Equals(transform.name, "RB_DriverSeat", StringComparison.OrdinalIgnoreCase));
            if (driver == null || seat == null)
                errors.Add($"{prefab.name}: missing driver or seat anchor.");
            else if (Vector3.Distance(driver.localPosition, seat.localPosition) > 0.02f
                || Quaternion.Angle(driver.localRotation, seat.localRotation) > 1f)
                errors.Add($"{prefab.name}: driver is not aligned with its seat.");
            if (prefab.GetComponent<VehicleController1912>() == null)
                errors.Add($"{prefab.name}: missing VehicleController1912.");
            if (prefab.GetComponent<EngineAudio1912>() == null)
                errors.Add($"{prefab.name}: missing EngineAudio1912.");
            report.AppendLine($"  {prefab.name}: wheels {wheels.Length}, driver {(driver != null ? "yes" : "no")}");
        }

        GameObject[] regional = Resources.LoadAll<GameObject>("Environment/RegionalKit");
        report.AppendLine($"Regional models: {regional.Length}");
        if (regional.Length < 20) errors.Add("RegionalKit should contain detailed and LOD1 models.");

        string[] audioNames = { "engine_synthetic_4cyl_600rpm", "engine_synthetic_4cyl_2100rpm", "rain_soft_loop", "logo_chime_1912" };
        foreach (string audioName in audioNames)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + audioName);
            if (clip == null) errors.Add($"Missing audio clip: {audioName}");
            else report.AppendLine($"  Audio {audioName}: {clip.length:F2}s, {clip.frequency} Hz");
        }

        if (!Enum.IsDefined(typeof(CameraMode), CameraMode.Rear))
            errors.Add("Rear camera mode is missing.");

        foreach (string warning in warnings) report.AppendLine("WARN " + warning);
        foreach (string error in errors) report.AppendLine("ERROR " + error);
        report.AppendLine(errors.Count == 0 ? "VALIDATION_OK" : $"VALIDATION_FAILED errors={errors.Count} warnings={warnings.Count}");
        Debug.Log(report.ToString());
        return report.ToString();
    }
}
