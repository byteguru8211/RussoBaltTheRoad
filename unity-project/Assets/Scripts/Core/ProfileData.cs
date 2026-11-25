using System;
using System.Collections.Generic;

[Serializable]
public class BestTimeRecord
{
    public string trackId;
    public float seconds;
}

[Serializable]
public class ProfileData
{
    public string playerName = "Гонщик";
    public float totalDistanceKm;
    public int racesStarted;
    public int racesFinished;
    public int racesWon;
    public int repairs;
    public float credits;
    public List<string> unlockedVehicles = new List<string> { "russo_balt_c24_55", "ford_model_t_1912" };
    public List<string> unlockedTracks = new List<string> { "winter_departure" };
    public List<string> achievements = new List<string>();
    public List<BestTimeRecord> bestTimes = new List<BestTimeRecord>();
}
