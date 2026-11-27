#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReleaseContentInstaller
{
    private sealed class TrackDefinition
    {
        public string id;
        public string name;
        public string chapter;
        public string description;
        public string location;
        public string prerequisite;
        public float width;
        public int laps;
        public int ai;
        public float grip;
        public float timeLimit;
        public int reward;
        public Vector3[] points;
        public TrackData.TrackSection[] sections;
    }

    [MenuItem("RussoBalt/Install Release Tracks")]
    public static void Install()
    {
        CreateTrack(new TrackDefinition
        {
            id = "tver_forest", name = "Тверской лес", chapter = "Свободный заезд",
            description = "Грунтовая колея, дубовые рощи и длинные S-образные повороты после весеннего дождя.",
            location = "Тверь · 1912", prerequisite = "finland_coast", width = 8.5f, laps = 2, ai = 5, grip = 0.78f, timeLimit = 420f, reward = 1400,
            points = new[]
            {
                new Vector3(-80f, 0f, 5f), new Vector3(-55f, 0f, 48f), new Vector3(5f, 1f, 68f),
                new Vector3(60f, 3f, 52f), new Vector3(78f, 1f, 5f), new Vector3(58f, 4f, -48f),
                new Vector3(5f, 2f, -65f), new Vector3(-50f, 0f, -50f), new Vector3(-75f, 0f, -20f)
            }
        });
        CreateTrack(new TrackDefinition
        {
            id = "riga_speedway", name = "Рижская кольцевая", chapter = "Скоростное кольцо",
            description = "Широкая приморская трасса с длинными прямыми, быстрыми дугами и опасной шиканой.",
            location = "Рига · 1912", prerequisite = "tver_forest", width = 12f, laps = 3, ai = 6, grip = 0.88f, timeLimit = 450f, reward = 1800,
            points = new[]
            {
                new Vector3(-90f, 0f, 0f), new Vector3(-60f, 0f, 70f), new Vector3(20f, 1f, 110f),
                new Vector3(100f, 0f, 70f), new Vector3(120f, 0f, -20f), new Vector3(70f, -1f, -90f),
                new Vector3(-20f, 0f, -100f), new Vector3(-90f, 0f, -60f)
            }
        });
        CreateTrack(new TrackDefinition
        {
            id = "crimean_coast", name = "Крымское побережье", chapter = "Южная экспедиция",
            description = "Каменные мосты, резкие подъёмы и морской ветер на извилистой дороге над заливом.",
            location = "Крым · 1912", prerequisite = "riga_speedway", width = 8.5f, laps = 2, ai = 5, grip = 0.82f, timeLimit = 450f, reward = 2200,
            points = new[]
            {
                new Vector3(-45f, 0f, 10f), new Vector3(-30f, 2f, 65f), new Vector3(20f, 8f, 110f),
                new Vector3(70f, 12f, 100f), new Vector3(115f, 7f, 60f), new Vector3(125f, 3f, 0f),
                new Vector3(95f, 2f, -55f), new Vector3(40f, 4f, -70f), new Vector3(-10f, 1f, -45f),
                new Vector3(-40f, 0f, -10f)
            }
        });
        EnsureRuntimeScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Release tracks installed");
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
        track.displayName = definition.name;
        track.chapter = definition.chapter;
        track.description = definition.description;
        track.location = definition.location;
        track.prerequisiteTrackId = definition.prerequisite;
        track.waypoints = definition.points;
        track.roadWidth = definition.width;
        track.laps = definition.laps;
        track.aiCount = definition.ai;
        track.surfaceGrip = definition.grip;
        track.timeLimit = definition.timeLimit;
        track.reward = definition.reward;
        track.sceneName = "Race_Runtime";
        track.scenery = definition.id switch
        {
            "crimean_coast" => "coast",
            "riga_speedway" => "speedway",
            _ => "forest"
        };
        if (track.sections == null || track.sections.Length == 0)
            track.sections = definition.sections ?? TrackData.DefaultSectionsFor(definition.id);
        track.storyTitle = definition.name.ToUpperInvariant();
        track.storyText = definition.description;
        track.previewResource = "UI/Hero";
        track.roadColor = definition.id switch
        {
            "tver_forest" => new Color(0.42f, 0.29f, 0.16f),
            "riga_speedway" => new Color(0.36f, 0.3f, 0.2f),
            "crimean_coast" => new Color(0.5f, 0.39f, 0.25f),
            _ => new Color(0.34f, 0.25f, 0.15f)
        };
        track.groundColor = definition.id switch
        {
            "tver_forest" => new Color(0.22f, 0.33f, 0.13f),
            "riga_speedway" => new Color(0.28f, 0.35f, 0.18f),
            "crimean_coast" => new Color(0.36f, 0.38f, 0.2f),
            _ => new Color(0.24f, 0.30f, 0.16f)
        };
        track.curbColor = definition.id == "crimean_coast" ? new Color(0.66f, 0.58f, 0.45f) : new Color(0.72f, 0.68f, 0.55f);
        EditorUtility.SetDirty(track);
    }

    private static void EnsureRuntimeScene()
    {
        const string path = "Assets/Scenes/Race_Runtime.unity";
        if (!System.IO.File.Exists(path))
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject race = new GameObject("Race");
            race.AddComponent<RaceManager>();
            EditorSceneManager.SaveScene(scene, path);
        }
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
