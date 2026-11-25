using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public ProfileData Profile { get; private set; }
    public SettingsData Settings { get; private set; }
    public string SelectedVehicleId { get; set; } = "russo_balt_c24_55";
    public string SelectedTrackId { get; set; } = "winter_departure";
    public bool IsRaceActive { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Profile = SaveSystem.LoadProfile();
        Settings = SaveSystem.LoadSettings();
        AudioListener.volume = Settings.masterVolume;
        Application.targetFrameRate = 60;
        Screen.SetResolution(1600, 900, false);
    }

    public void Save()
    {
        SaveSystem.SaveProfile(Profile);
        SaveSystem.SaveSettings(Settings);
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    public void RecordDistance(float meters)
    {
        Profile.totalDistanceKm += Mathf.Max(0f, meters) / 1000f;
    }
}
