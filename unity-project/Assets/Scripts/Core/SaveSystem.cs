using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string ProfileFile = "russo_balt_profile.json";
    private const string SettingsFile = "russo_balt_settings.json";

    public static ProfileData LoadProfile()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, ProfileFile);
            if (File.Exists(path))
                return JsonUtility.FromJson<ProfileData>(File.ReadAllText(path)) ?? new ProfileData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Profile load failed: {e.Message}");
        }
        return new ProfileData();
    }

    public static void SaveProfile(ProfileData profile)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, ProfileFile);
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
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
            if (File.Exists(path))
                return JsonUtility.FromJson<SettingsData>(File.ReadAllText(path)) ?? new SettingsData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Settings load failed: {e.Message}");
        }
        return new SettingsData();
    }

    public static void SaveSettings(SettingsData settings)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, SettingsFile);
            File.WriteAllText(path, JsonUtility.ToJson(settings, true));
            AudioListener.volume = settings.masterVolume;
            QualitySettings.SetQualityLevel(Mathf.Clamp(settings.qualityLevel, 0, QualitySettings.names.Length - 1), true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Settings save failed: {e.Message}");
        }
    }
}
