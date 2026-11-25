using System;

[Serializable]
public class SettingsData
{
    public float masterVolume = 0.8f;
    public float engineVolume = 0.85f;
    public float ambientVolume = 0.65f;
    public float steeringSensitivity = 1.0f;
    public bool cameraShake = true;
    public bool automaticClutch = true;
    public bool subtitles = true;
    public int qualityLevel = 2;
}
