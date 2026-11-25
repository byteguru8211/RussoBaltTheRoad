using System;
using UnityEngine;

public enum CameraMode
{
    Chase = 0,
    Hood = 1,
    Cockpit = 2,
    Cinematic = 3
}

[Serializable]
public class SettingsData
{
    public float masterVolume = 0.8f;
    public float engineVolume = 0.85f;
    public float ambientVolume = 0.65f;
    public float musicVolume = 0.45f;
    public float steeringSensitivity = 1.0f;
    public bool cameraShake = true;
    public bool automaticClutch = true;
    public bool automaticTransmission = true;
    public bool subtitles = true;
    public int qualityLevel = 2;
    public CameraMode cameraMode = CameraMode.Chase;
    public string selectedVehicleId = "russo_balt_c24_55";
    public string selectedTrackId = "winter_departure";

    public bool IsValidCameraMode()
    {
        return Enum.IsDefined(typeof(CameraMode), cameraMode);
    }

    public void ApplyRuntimeSettings()
    {
        AudioListener.volume = FiniteVolume(masterVolume);
        int qualityCount = QualitySettings.names.Length;
        if (qualityCount > 0)
        {
            int qualityLevel = Mathf.Clamp(this.qualityLevel, 0, qualityCount - 1);
            if (QualitySettings.GetQualityLevel() != qualityLevel)
                QualitySettings.SetQualityLevel(qualityLevel, true);
        }
    }

    private static float FiniteVolume(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
    }
}
