#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AdditionalTrackInstaller
{
    [MenuItem("RussoBalt/Install Additional Tracks")]
    public static void InstallAdditionalTracks()
    {
        CreateTrack(new TrackDefinition
        {
            id = "moscow_highway",
            displayName = "Московское шоссе",
            chapter = "Свободный заезд",
            description = "Раннее утро, подснежная просека и старые почтовые опоры.",
            sceneName = "Race_Moscow",
            prerequisiteTrackId = "monaco_finish",
            location = "Подмосковье · 1912",
            scenery = "forest",
            roadWidth = 10f,
            laps = 2,
            aiCount = 4,
            surfaceGrip = 0.8f,
            timeLimit = 360f,
            reward = 900,
            roadColor = new Color(0.3f, 0.27f, 0.22f),
            groundColor = new Color(0.72f, 0.77f, 0.8f),
            curbColor = new Color(0.76f, 0.72f, 0.62f),
            storyTitle = "МОСКОВСКОЕ ШОССЕ",
            storyText = "Рассвет над Подмосковьем. Машины выходят на трассу раньше почтовых дилижансов.",
            waypoints = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 65f), new Vector3(22f, 1f, 125f),
                new Vector3(75f, 2f, 155f), new Vector3(135f, 1f, 125f), new Vector3(165f, 0f, 65f),
                new Vector3(145f, 0f, 0f), new Vector3(95f, -1f, -35f), new Vector3(40f, 0f, -45f)
            }
        });

        CreateTrack(new TrackDefinition
        {
            id = "finland_coast",
            displayName = "Побережье Финского залива",
            chapter = "Свободный заезд",
            description = "Туман, каменные мосты и ветер с Балтийского моря.",
            sceneName = "Race_Finland",
            prerequisiteTrackId = "moscow_highway",
            location = "Финский залив · 1912",
            scenery = "coast",
            roadWidth = 9f,
            laps = 1,
            aiCount = 3,
            surfaceGrip = 0.76f,
            timeLimit = 330f,
            reward = 1200,
            roadColor = new Color(0.28f, 0.25f, 0.21f),
            groundColor = new Color(0.24f, 0.3f, 0.24f),
            curbColor = new Color(0.65f, 0.63f, 0.55f),
            storyTitle = "ПОБЕРЕЖЬЕ ФИНСКОГО ЗАЛИВА",
            storyText = "Соль в воздухе и туман над водой. Здесь решает не только скорость, но и выдержка.",
            waypoints = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 75f), new Vector3(35f, 1f, 135f),
                new Vector3(95f, 2f, 175f), new Vector3(155f, 1f, 150f), new Vector3(205f, 0f, 90f),
                new Vector3(185f, 0f, 20f), new Vector3(125f, -1f, -35f), new Vector3(60f, 0f, -55f)
            }
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Additional tracks installed");
    }

    private static void CreateTrack(TrackDefinition definition)
    {
        const string folder = "Assets/Resources/Tracks";
        System.IO.Directory.CreateDirectory(folder);
        string path = $"{folder}/{definition.id}.asset";
        TrackData track = AssetDatabase.LoadAssetAtPath<TrackData>(path);
        if (track == null)
        {
            track = ScriptableObject.CreateInstance<TrackData>();
            AssetDatabase.CreateAsset(track, path);
        }

        track.id = definition.id;
        track.displayName = definition.displayName;
        track.chapter = definition.chapter;
        track.description = definition.description;
        track.waypoints = definition.waypoints;
        track.roadWidth = definition.roadWidth;
        track.laps = definition.laps;
        track.aiCount = definition.aiCount;
        track.surfaceGrip = definition.surfaceGrip;
        track.timeLimit = definition.timeLimit;
        track.reward = definition.reward;
        track.roadColor = definition.roadColor;
        track.groundColor = definition.groundColor;
        track.curbColor = definition.curbColor;
        track.storyTitle = definition.storyTitle;
        track.storyText = definition.storyText;
        track.previewResource = "UI/Hero";
        track.scenery = definition.scenery;
        track.sceneName = definition.sceneName;
        track.prerequisiteTrackId = definition.prerequisiteTrackId;
        track.location = definition.location;
        EditorUtility.SetDirty(track);

        string scenePath = $"Assets/Scenes/{definition.sceneName}.unity";
        if (!System.IO.File.Exists(scenePath))
            AssetDatabase.CopyAsset("Assets/Scenes/Race_Winter.unity", scenePath);
        AddSceneToBuild(scenePath);
    }

    private static void AddSceneToBuild(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(scene => scene.path == scenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private sealed class TrackDefinition
    {
        public string id;
        public string displayName;
        public string chapter;
        public string description;
        public string sceneName;
        public string prerequisiteTrackId;
        public string location;
        public string scenery;
        public float roadWidth;
        public int laps;
        public int aiCount;
        public float surfaceGrip;
        public float timeLimit;
        public int reward;
        public Color roadColor;
        public Color groundColor;
        public Color curbColor;
        public string storyTitle;
        public string storyText;
        public Vector3[] waypoints;
    }
}
#endif
