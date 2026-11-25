using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class SaveSystem
{
    private const string ProfileFile = "russo_balt_profile.json";
    private const string SettingsFile = "russo_balt_settings.json";
    private const int NetworkNameByteLimit = 32;
    private const string DefaultPlayerName = "Гонщик";
    private const string DefaultVehicleId = "russo_balt_c24_55";
    private const string DefaultTrackId = "winter_departure";
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false);

    public static ProfileData LoadProfile()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, ProfileFile);
            if (!File.Exists(path))
                return new ProfileData();

            string json = File.ReadAllText(path, Utf8);
            ProfileData profile = new ProfileData();
            JsonUtility.FromJsonOverwrite(json, profile);
            return RepairProfile(profile);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Profile load failed: {e.Message}");
        }
        return new ProfileData();
    }

    public static void SaveProfile(ProfileData profile)
    {
        if (profile == null)
        {
            Debug.LogError("Profile save failed: profile is null.");
            return;
        }

        try
        {
            RepairProfile(profile);
            string path = Path.Combine(Application.persistentDataPath, ProfileFile);
            WriteAtomic(path, JsonUtility.ToJson(profile, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"Profile save failed: {e.Message}");
        }
    }

    public static SettingsData LoadSettings()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, SettingsFile);
            SettingsData settings = new SettingsData();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Utf8);
                JsonUtility.FromJsonOverwrite(json, settings);
            }
            RepairSettings(settings);
            settings.ApplyRuntimeSettings();
            return settings;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Settings load failed: {e.Message}");
            SettingsData settings = new SettingsData();
            settings.ApplyRuntimeSettings();
            return settings;
        }
    }

    public static void SaveSettings(SettingsData settings)
    {
        if (settings == null)
        {
            Debug.LogError("Settings save failed: settings are null.");
            return;
        }

        try
        {
            CaptureSelection(settings);
            RepairSettings(settings);
            string path = Path.Combine(Application.persistentDataPath, SettingsFile);
            WriteAtomic(path, JsonUtility.ToJson(settings, true));
            settings.ApplyRuntimeSettings();
        }
        catch (Exception e)
        {
            Debug.LogError($"Settings save failed: {e.Message}");
        }
    }

    // GameManager owns the live selection properties. These helpers keep the persisted copy in sync
    // without requiring changes to that component.
    public static void ApplySelection(SettingsData settings)
    {
        if (settings == null || GameManager.Instance == null)
            return;

        RepairSettings(settings);
        GameManager.Instance.SelectedVehicleId = settings.selectedVehicleId;
        GameManager.Instance.SelectedTrackId = settings.selectedTrackId;
    }

    public static void SaveSelection()
    {
        SaveSettings(GameManager.Instance != null ? GameManager.Instance.Settings : LoadSettings());
    }

    public static void SetSelectedVehicle(string vehicleId)
    {
        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : LoadSettings();
        string normalized = NormalizeIdentifier(vehicleId, DefaultVehicleId);
        settings.selectedVehicleId = normalized;
        if (GameManager.Instance != null)
            GameManager.Instance.SelectedVehicleId = normalized;
        SaveSettings(settings);
    }

    public static void SetSelectedTrack(string trackId)
    {
        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : LoadSettings();
        string normalized = NormalizeIdentifier(trackId, DefaultTrackId);
        settings.selectedTrackId = normalized;
        if (GameManager.Instance != null)
            GameManager.Instance.SelectedTrackId = normalized;
        SaveSettings(settings);
    }

    public static string NormalizePlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultPlayerName;

        string source = value.Trim();
        StringBuilder result = new StringBuilder(Mathf.Min(source.Length, NetworkNameByteLimit));
        for (int i = 0; i < source.Length; i++)
        {
            char character = source[i];
            string textElement;
            if (char.IsHighSurrogate(character) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
            {
                textElement = source.Substring(i, 2);
                i++;
            }
            else if (char.IsSurrogate(character))
            {
                textElement = "\uFFFD";
            }
            else
            {
                if (char.IsControl(character))
                    character = ' ';
                textElement = character.ToString();
            }

            if (Utf8.GetByteCount(result.ToString() + textElement) > NetworkNameByteLimit)
                break;
            result.Append(textElement);
        }

        string normalized = result.ToString().Trim();
        return normalized.Length == 0 ? DefaultPlayerName : normalized;
    }

    private static ProfileData RepairProfile(ProfileData profile)
    {
        if (profile == null)
            return new ProfileData();

        profile.playerName = NormalizePlayerName(profile.playerName);
        profile.totalDistanceKm = NonNegativeFinite(profile.totalDistanceKm);
        profile.credits = NonNegativeFinite(profile.credits);
        profile.racesStarted = Mathf.Max(0, profile.racesStarted);
        profile.racesFinished = Mathf.Max(0, profile.racesFinished);
        profile.racesWon = Mathf.Max(0, profile.racesWon);
        profile.repairs = Mathf.Max(0, profile.repairs);

        profile.unlockedVehicles = RepairIdentifierList(profile.unlockedVehicles, DefaultVehicleId);
        if (!profile.unlockedVehicles.Contains("ford_model_t_1912"))
            profile.unlockedVehicles.Add("ford_model_t_1912");
        profile.unlockedTracks = RepairIdentifierList(profile.unlockedTracks, DefaultTrackId);
        profile.achievements = RepairIdentifierList(profile.achievements, null);
        profile.bestTimes = RepairBestTimes(profile.bestTimes);
        return profile;
    }

    private static SettingsData RepairSettings(SettingsData settings)
    {
        if (settings == null)
            return new SettingsData();

        settings.masterVolume = NormalizeVolume(settings.masterVolume, 0.8f);
        settings.engineVolume = NormalizeVolume(settings.engineVolume, 0.85f);
        settings.ambientVolume = NormalizeVolume(settings.ambientVolume, 0.65f);
        settings.musicVolume = NormalizeVolume(settings.musicVolume, 0.45f);
        settings.steeringSensitivity = float.IsNaN(settings.steeringSensitivity) || float.IsInfinity(settings.steeringSensitivity)
            ? 1f
            : Mathf.Clamp(settings.steeringSensitivity, 0.5f, 1.5f);

        if (!settings.IsValidCameraMode())
            settings.cameraMode = CameraMode.Chase;
        settings.selectedVehicleId = NormalizeIdentifier(settings.selectedVehicleId, DefaultVehicleId);
        settings.selectedTrackId = NormalizeIdentifier(settings.selectedTrackId, DefaultTrackId);
        int qualityCount = QualitySettings.names.Length;
        settings.qualityLevel = qualityCount > 0 ? Mathf.Clamp(settings.qualityLevel, 0, qualityCount - 1) : 0;
        return settings;
    }

    private static List<string> RepairIdentifierList(List<string> source, string requiredId)
    {
        List<string> result = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (string value in source)
            {
                string id = NormalizeIdentifier(value, null);
                if (id != null && seen.Add(id))
                    result.Add(id);
            }
        }

        if (requiredId != null && !seen.Contains(requiredId))
            result.Insert(0, requiredId);
        return result;
    }

    private static List<BestTimeRecord> RepairBestTimes(List<BestTimeRecord> source)
    {
        List<BestTimeRecord> result = new List<BestTimeRecord>();
        if (source == null)
            return result;

        Dictionary<string, BestTimeRecord> fastest = new Dictionary<string, BestTimeRecord>(StringComparer.Ordinal);
        foreach (BestTimeRecord record in source)
        {
            if (record == null || float.IsNaN(record.seconds) || float.IsInfinity(record.seconds) || record.seconds <= 0f)
                continue;
            string trackId = NormalizeIdentifier(record.trackId, null);
            if (trackId == null)
                continue;

            if (!fastest.TryGetValue(trackId, out BestTimeRecord existing) || record.seconds < existing.seconds)
            {
                record.trackId = trackId;
                fastest[trackId] = record;
            }
        }

        foreach (BestTimeRecord record in fastest.Values)
            result.Add(record);
        result.Sort((left, right) => string.CompareOrdinal(left.trackId, right.trackId));
        return result;
    }

    private static string NormalizeIdentifier(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string trimmed = value.Trim();
        if (trimmed.Length > 64)
            return fallback;
        foreach (char character in trimmed)
        {
            if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                return fallback;
        }
        return trimmed;
    }

    private static float NormalizeVolume(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        return Mathf.Clamp01(value);
    }

    private static float NonNegativeFinite(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    private static void CaptureSelection(SettingsData settings)
    {
        if (GameManager.Instance == null)
            return;
        string vehicleId = NormalizeIdentifier(GameManager.Instance.SelectedVehicleId, null);
        string trackId = NormalizeIdentifier(GameManager.Instance.SelectedTrackId, null);
        if (vehicleId != null)
            settings.selectedVehicleId = vehicleId;
        if (trackId != null)
            settings.selectedTrackId = trackId;
    }

    private static void WriteAtomic(string path, string contents)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string backupPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".bak";
        try
        {
            using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (StreamWriter writer = new StreamWriter(stream, Utf8))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, backupPath);
                TryDelete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
            TryDelete(backupPath);
        }
    }

    private static void TryDelete(string path)
    {
        if (path == null)
            return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not remove temporary save file '{path}': {e.Message}");
        }
    }
}
