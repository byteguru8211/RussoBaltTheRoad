using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public ProfileData Profile { get; private set; }
    public SettingsData Settings { get; private set; }
    public string SelectedVehicleId { get; set; } = "russo_balt_c24_55";
    public string SelectedTrackId { get; set; } = "winter_departure";
    public RaceType SelectedRaceType { get; set; } = RaceType.Circuit;
    public int SelectedCameraMode { get; set; }
    public bool IsRaceActive { get; set; }
    public bool SkipRaceStory { get; set; }

    private const string RaceTypeSelectionKey = "russo_balt_selected_race_type";
    private const string CameraSelectionKey = "russo_balt_selected_camera";

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
        SaveSystem.ApplySelection(Settings);
        RestoreSelections();
        ReconcileTrackUnlocks();
        ValidateSelections();
        AudioListener.volume = Settings.masterVolume;
        if (LanSessionController.Instance == null)
            new GameObject("LanSessionController").AddComponent<LanSessionController>();
        Application.targetFrameRate = 60;
        if (!Application.isEditor)
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
    }

    private void RestoreSelections()
    {
        int raceType = PlayerPrefs.GetInt(RaceTypeSelectionKey, (int)SelectedRaceType);
        SelectedRaceType = Enum.IsDefined(typeof(RaceType), raceType) ? (RaceType)raceType : RaceType.Circuit;
        SelectedCameraMode = Settings.IsValidCameraMode()
            ? (int)Settings.cameraMode
            : Mathf.Clamp(PlayerPrefs.GetInt(CameraSelectionKey, SelectedCameraMode), 0, (int)CameraMode.Cinematic);
    }

    private void SaveSelections()
    {
        PlayerPrefs.SetInt(RaceTypeSelectionKey, (int)SelectedRaceType);
        PlayerPrefs.SetInt(CameraSelectionKey, SelectedCameraMode);
        PlayerPrefs.Save();
    }

    public void Save()
    {
        ReconcileTrackUnlocks();
        ValidateSelections();
        Settings.cameraMode = Enum.IsDefined(typeof(CameraMode), SelectedCameraMode) ? (CameraMode)SelectedCameraMode : CameraMode.Chase;
        SaveSystem.SaveProfile(Profile);
        SaveSystem.SaveSettings(Settings);
        SaveSelections();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    public void RecordDistance(float meters)
    {
        Profile.totalDistanceKm += Mathf.Max(0f, meters) / 1000f;
    }

    public static List<TrackData> GetTracks()
    {
        List<TrackData> tracks = Resources.LoadAll<TrackData>("Tracks").ToList();
        tracks.RemoveAll(track => track == null || string.IsNullOrWhiteSpace(track.id));
        tracks.Sort((a, b) =>
        {
            int order = TrackData.ExpeditionOrder(a.id).CompareTo(TrackData.ExpeditionOrder(b.id));
            return order != 0 ? order : string.Compare(a.id, b.id, StringComparison.Ordinal);
        });
        return tracks;
    }

    public static TrackData LoadTrack(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId)) return null;
        TrackData track = Resources.Load<TrackData>($"Tracks/{trackId}");
        return track != null && track.id == trackId ? track : null;
    }

    public TrackData GetSelectedTrack() => LoadTrack(SelectedTrackId);

    public bool IsVehicleUnlocked(string vehicleId)
    {
        return !string.IsNullOrWhiteSpace(vehicleId) && Profile.unlockedVehicles.Contains(vehicleId);
    }

    public bool HasCompletedTrack(string trackId)
    {
        return !string.IsNullOrWhiteSpace(trackId) && Profile.bestTimes.Exists(record => record != null && record.trackId == trackId && record.seconds > 0f);
    }

    public bool IsTrackUnlocked(string trackId)
    {
        TrackData track = LoadTrack(trackId);
        if (track == null) return false;
        if (Profile.unlockedTracks.Contains(track.id)) return true;
        string prerequisite = track.UnlockPrerequisite;
        return string.IsNullOrWhiteSpace(prerequisite) || HasCompletedTrack(prerequisite);
    }

    public string GetTrackUnlockRequirement(string trackId)
    {
        if (IsTrackUnlocked(trackId)) return "";
        TrackData track = LoadTrack(trackId);
        if (track == null) return "Маршрут недоступен";
        string prerequisite = track.UnlockPrerequisite;
        if (string.IsNullOrWhiteSpace(prerequisite)) return "Маршрут ещё не открыт";
        TrackData previous = LoadTrack(prerequisite);
        string previousName = previous != null ? previous.displayName : prerequisite;
        return $"Завершите «{previousName}»";
    }

    public void ReconcileTrackUnlocks()
    {
        if (Profile == null) return;
        foreach (TrackData track in GetTracks())
        {
            if (!Profile.unlockedTracks.Contains(track.id) && IsTrackUnlocked(track.id))
                Profile.unlockedTracks.Add(track.id);
        }
    }

    // RaceManager can call this immediately after recording a successful finish.
    public void RegisterTrackCompletion(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId)) return;
        if (trackId == "winter_departure" && !Profile.unlockedTracks.Contains("alps_pass"))
            Profile.unlockedTracks.Add("alps_pass");
        if (trackId == "alps_pass" && !Profile.unlockedTracks.Contains("monaco_finish"))
            Profile.unlockedTracks.Add("monaco_finish");
        ReconcileTrackUnlocks();
        Save();
    }

    private void ValidateSelections()
    {
        if (Profile == null) return;
        if (VehicleCatalog.Find(SelectedVehicleId) == null || !IsVehicleUnlocked(SelectedVehicleId))
            SelectedVehicleId = Profile.unlockedVehicles.FirstOrDefault(id => VehicleCatalog.Find(id) != null) ?? "russo_balt_c24_55";
        if (!IsTrackUnlocked(SelectedTrackId))
        {
            TrackData firstOpen = GetTracks().FirstOrDefault(track => IsTrackUnlocked(track.id));
            SelectedTrackId = firstOpen?.id ?? "winter_departure";
        }
        if (!Enum.IsDefined(typeof(RaceType), SelectedRaceType)) SelectedRaceType = RaceType.Circuit;
        SelectedCameraMode = Mathf.Clamp(SelectedCameraMode, 0, (int)CameraMode.Cinematic);
    }

    public bool SelectRace(string trackId, RaceType raceType)
    {
        if (!IsTrackUnlocked(trackId) || !Enum.IsDefined(typeof(RaceType), raceType)) return false;
        SelectedTrackId = trackId;
        SelectedRaceType = raceType;
        Save();
        return true;
    }

    public bool SelectVehicle(string vehicleId)
    {
        if (!IsVehicleUnlocked(vehicleId) || VehicleCatalog.Find(vehicleId) == null) return false;
        SelectedVehicleId = vehicleId;
        Save();
        return true;
    }

    public void SelectCameraMode(int mode)
    {
        SelectedCameraMode = Mathf.Clamp(mode, 0, (int)CameraMode.Cinematic);
        Settings.cameraMode = (CameraMode)SelectedCameraMode;
        Save();
    }

    public bool CanStartRace()
    {
        return IsTrackUnlocked(SelectedTrackId) && IsVehicleUnlocked(SelectedVehicleId) && VehicleCatalog.Find(SelectedVehicleId) != null;
    }

    public int GetRaceLaps() => RaceTypeInfo.Laps(GetSelectedTrack(), SelectedRaceType);
    public int GetRaceAiCount() => RaceTypeInfo.AiCount(GetSelectedTrack(), SelectedRaceType);
    public string GetRaceSceneName() => TrackData.SceneNameForTrack(GetSelectedTrack());

    public float GetBestTime(string trackId)
    {
        BestTimeRecord best = Profile.bestTimes.Find(record => record != null && record.trackId == trackId);
        return best != null && best.seconds > 0f ? best.seconds : 0f;
    }

    public void ResetRaceTransientState()
    {
        IsRaceActive = false;
        SkipRaceStory = false;
    }
}
