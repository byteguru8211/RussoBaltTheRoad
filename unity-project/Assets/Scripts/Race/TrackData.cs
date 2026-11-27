using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RussoBalt/Track", fileName = "Track")]
public class TrackData : ScriptableObject
{
    public string id;
    public string displayName;
    public string chapter;
    public string description;
    public Vector3[] waypoints;
    public float roadWidth = 9f;
    public int laps = 1;
    public int aiCount = 4;
    public float surfaceGrip = 0.85f;
    public float timeLimit = 300f;
    public int reward = 500;
    public Color roadColor = new Color(0.34f, 0.25f, 0.15f);
    public Color groundColor = new Color(0.20f, 0.27f, 0.16f);
    public Color curbColor = new Color(0.72f, 0.68f, 0.55f);
    public string storyTitle;
    [TextArea] public string storyText;
    public string previewResource = "UI/Hero";
    public string scenery = "forest";

    public enum TrackSectionKind
    {
        Chikane,
        StoneBridge,
        StoneSection,
        RangerStation,
        Grandstand
    }

    [System.Serializable]
    public class TrackSection
    {
        public string id;
        public TrackSectionKind kind;
        [Range(0f, 1f)] public float startProgress = 0.2f;
        public float lengthMeters = 60f;
        public float side = 1f;
        public float amplitudeMeters = 1.6f;
    }

    [Header("Optional route sections and landmarks")]
    public TrackSection[] sections = new TrackSection[0];

    public TrackSection[] GetSections()
    {
        return sections != null && sections.Length > 0 ? sections : DefaultSectionsFor(id);
    }

    public static TrackSection[] DefaultSectionsFor(string trackId)
    {
        switch (trackId)
        {
            case "tver_forest":
                return new[]
                {
                    new TrackSection { id = "Tver_RangerStation", kind = TrackSectionKind.RangerStation, startProgress = 0.43f, lengthMeters = 0f, side = -1f }
                };
            case "riga_speedway":
                return new[]
                {
                    new TrackSection { id = "Riga_Grandstand", kind = TrackSectionKind.Grandstand, startProgress = 0.12f, lengthMeters = 0f, side = -1f },
                    new TrackSection { id = "Riga_Chikane", kind = TrackSectionKind.Chikane, startProgress = 0.57f, lengthMeters = 88f, amplitudeMeters = 1.65f }
                };
            case "crimean_coast":
                return new[]
                {
                    new TrackSection { id = "Crimea_StoneBridge", kind = TrackSectionKind.StoneBridge, startProgress = 0.17f, lengthMeters = 48f },
                    new TrackSection { id = "Crimea_StoneSection", kind = TrackSectionKind.StoneSection, startProgress = 0.61f, lengthMeters = 72f }
                };
            default:
                return new TrackSection[0];
        }
    }

    // Optional for future tracks. Existing expedition IDs use the canonical mapping below.
    public string sceneName = "Race_Winter";
    public string prerequisiteTrackId;
    public string location;

    public static string SceneNameForTrack(string trackId)
    {
        return trackId switch
        {
            "winter_departure" => "Race_Winter",
            "alps_pass" => "Race_Alps",
            "monaco_finish" => "Race_Monaco",
            "moscow_highway" => "Race_Moscow",
            "finland_coast" => "Race_Finland",
            "tver_forest" or "riga_speedway" or "crimean_coast" => "Race_Runtime",
            _ => "Race_Winter"
        };
    }

    public static string SceneNameForTrack(TrackData track)
    {
        if (track == null) return SceneNameForTrack("winter_departure");
        bool canonical = track.id == "winter_departure" || track.id == "alps_pass" || track.id == "monaco_finish";
        return canonical || string.IsNullOrWhiteSpace(track.sceneName) ? SceneNameForTrack(track.id) : track.sceneName;
    }

    public static string PrerequisiteForTrack(string trackId)
    {
        return trackId switch
        {
            "alps_pass" => "winter_departure",
            "monaco_finish" => "alps_pass",
            "moscow_highway" => "monaco_finish",
            "finland_coast" => "moscow_highway",
            "tver_forest" => "finland_coast",
            "riga_speedway" => "tver_forest",
            "crimean_coast" => "riga_speedway",
            _ => ""
        };
    }

    public string UnlockPrerequisite => string.IsNullOrWhiteSpace(prerequisiteTrackId) ? PrerequisiteForTrack(id) : prerequisiteTrackId;

    public static string LocationForTrack(string trackId)
    {
        return trackId switch
        {
            "winter_departure" => "Россия · 1912",
            "alps_pass" => "Альпы · 1912",
            "monaco_finish" => "Монако · 1912",
            "moscow_highway" => "Подмосковье · 1912",
            "finland_coast" => "Финский залив · 1912",
            "tver_forest" => "Тверь · 1912",
            "riga_speedway" => "Рига · 1912",
            "crimean_coast" => "Крым · 1912",
            _ => "1912"
        };
    }

    public string Location => string.IsNullOrWhiteSpace(location) ? LocationForTrack(id) : location;

    public static int ExpeditionOrder(string trackId)
    {
        return trackId switch
        {
            "winter_departure" => 0,
            "alps_pass" => 1,
            "monaco_finish" => 2,
            "moscow_highway" => 3,
            "finland_coast" => 4,
            "tver_forest" => 5,
            "riga_speedway" => 6,
            "crimean_coast" => 7,
            _ => 100
        };
    }

    public float ApproximateLengthMeters
    {
        get
        {
            if (waypoints == null || waypoints.Length < 4) return 0f;
            try
            {
                return new TrackRoute(this).Length;
            }
            catch (ArgumentException)
            {
                float length = 0f;
                for (int i = 0; i < waypoints.Length; i++)
                    length += Vector3.Distance(waypoints[i], waypoints[(i + 1) % waypoints.Length]);
                return length;
            }
        }
    }
}
