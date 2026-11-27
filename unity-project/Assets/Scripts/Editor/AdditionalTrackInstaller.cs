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
            timeLimit = 900f,
            reward = 900,
            roadColor = new Color(0.3f, 0.27f, 0.22f),
            groundColor = new Color(0.72f, 0.77f, 0.8f),
            curbColor = new Color(0.76f, 0.72f, 0.62f),
            storyTitle = "МОСКОВСКОЕ ШОССЕ",
            storyText = "Рассвет над Подмосковьем. Машины выходят на трассу раньше почтовых дилижансов.",
            waypoints = new[]
            {
                new Vector3(-77.389f, 0f, -75.6f), new Vector3(-71.397f, 0f, -75.6f), new Vector3(-65.405f, 0f, -75.6f),
                new Vector3(-59.413f, 0f, -75.6f), new Vector3(-53.42f, 0f, -75.6f), new Vector3(-47.428f, 0f, -75.6f),
                new Vector3(-41.436f, 0f, -75.6f), new Vector3(-35.444f, 0f, -75.6f), new Vector3(-29.452f, 0f, -75.6f),
                new Vector3(-23.46f, 0f, -75.6f), new Vector3(-17.468f, 0f, -75.6f), new Vector3(-11.475f, 0f, -75.6f),
                new Vector3(-5.483f, 0f, -75.6f), new Vector3(0.509f, 0f, -75.6f), new Vector3(6.501f, 0f, -75.6f),
                new Vector3(12.493f, 0.003f, -75.6f), new Vector3(18.485f, 0.022f, -75.6f), new Vector3(24.477f, 0.057f, -75.6f),
                new Vector3(30.469f, 0.107f, -75.6f), new Vector3(36.462f, 0.171f, -75.6f), new Vector3(42.454f, 0.248f, -75.6f),
                new Vector3(48.446f, 0.336f, -75.6f), new Vector3(54.438f, 0.435f, -75.6f), new Vector3(60.43f, 0.543f, -75.6f),
                new Vector3(66.422f, 0.659f, -75.6f), new Vector3(72.414f, 0.783f, -75.6f), new Vector3(78.406f, 0.912f, -75.6f),
                new Vector3(84.399f, 1.046f, -75.6f), new Vector3(90.391f, 1.184f, -75.6f), new Vector3(96.383f, 1.324f, -75.6f),
                new Vector3(102.375f, 1.466f, -75.6f), new Vector3(108.367f, 1.608f, -75.6f), new Vector3(114.359f, 1.749f, -75.575f),
                new Vector3(120.339f, 1.888f, -75.229f), new Vector3(126.234f, 2.024f, -74.184f), new Vector3(131.881f, 2.156f, -72.201f),
                new Vector3(137.062f, 2.282f, -69.21f), new Vector3(141.59f, 2.402f, -65.298f), new Vector3(145.384f, 2.514f, -60.668f),
                new Vector3(148.527f, 2.617f, -55.57f), new Vector3(151.277f, 2.711f, -50.246f), new Vector3(153.957f, 2.793f, -44.887f),
                new Vector3(156.636f, 2.864f, -39.527f), new Vector3(159.316f, 2.92f, -34.168f), new Vector3(161.988f, 2.963f, -28.804f),
                new Vector3(164.397f, 2.99f, -23.32f), new Vector3(166.087f, 3f, -17.578f), new Vector3(166.745f, 3f, -11.631f),
                new Vector3(166.276f, 3f, -5.666f), new Vector3(164.825f, 3f, 0.143f), new Vector3(162.754f, 3f, 5.764f),
                new Vector3(160.529f, 3f, 11.328f), new Vector3(158.303f, 3f, 16.892f), new Vector3(156.078f, 3f, 22.455f),
                new Vector3(153.853f, 3f, 28.019f), new Vector3(151.579f, 2.999f, 33.563f), new Vector3(148.905f, 2.983f, 38.922f),
                new Vector3(145.505f, 2.947f, 43.847f), new Vector3(141.279f, 2.892f, 48.083f), new Vector3(136.333f, 2.821f, 51.449f),
                new Vector3(130.889f, 2.734f, 53.94f), new Vector3(125.184f, 2.634f, 55.769f), new Vector3(119.405f, 2.522f, 57.353f),
                new Vector3(113.624f, 2.399f, 58.93f), new Vector3(107.843f, 2.266f, 60.506f), new Vector3(102.062f, 2.126f, 62.083f),
                new Vector3(96.282f, 1.98f, 63.66f), new Vector3(90.486f, 1.829f, 65.182f), new Vector3(84.591f, 1.675f, 66.234f),
                new Vector3(78.609f, 1.519f, 66.386f), new Vector3(72.702f, 1.363f, 65.432f), new Vector3(67.066f, 1.208f, 63.419f),
                new Vector3(61.771f, 1.056f, 60.62f), new Vector3(56.673f, 0.908f, 57.472f), new Vector3(51.525f, 0.767f, 54.406f),
                new Vector3(46.161f, 0.632f, 51.741f), new Vector3(40.513f, 0.507f, 49.757f), new Vector3(34.624f, 0.392f, 48.688f),
                new Vector3(28.641f, 0.288f, 48.694f), new Vector3(22.767f, 0.198f, 49.832f), new Vector3(17.209f, 0.123f, 52.048f),
                new Vector3(12.112f, 0.065f, 55.187f), new Vector3(7.522f, 0.024f, 59.033f), new Vector3(3.36f, 0.003f, 63.341f),
                new Vector3(-0.57f, 0f, 67.864f), new Vector3(-4.476f, 0f, 72.408f), new Vector3(-8.635f, 0f, 76.717f),
                new Vector3(-13.395f, 0f, 80.343f), new Vector3(-18.745f, 0f, 83.025f), new Vector3(-24.448f, 0f, 84.849f),
                new Vector3(-30.259f, 0f, 86.315f), new Vector3(-36.072f, 0f, 87.768f), new Vector3(-41.885f, 0f, 89.221f),
                new Vector3(-47.698f, 0.009f, 90.675f), new Vector3(-53.512f, 0.041f, 92.128f), new Vector3(-59.325f, 0.095f, 93.581f),
                new Vector3(-65.138f, 0.167f, 95.034f), new Vector3(-70.965f, 0.256f, 96.43f), new Vector3(-76.879f, 0.36f, 97.374f),
                new Vector3(-82.863f, 0.475f, 97.461f), new Vector3(-88.764f, 0.599f, 96.47f), new Vector3(-94.374f, 0.731f, 94.389f),
                new Vector3(-99.565f, 0.867f, 91.406f), new Vector3(-104.384f, 1.006f, 87.847f), new Vector3(-109.065f, 1.144f, 84.108f),
                new Vector3(-113.745f, 1.28f, 80.364f), new Vector3(-118.424f, 1.411f, 76.621f), new Vector3(-123.103f, 1.535f, 72.878f),
                new Vector3(-127.782f, 1.649f, 69.135f), new Vector3(-132.461f, 1.751f, 65.391f), new Vector3(-137.14f, 1.839f, 61.648f),
                new Vector3(-141.774f, 1.91f, 57.851f), new Vector3(-146.032f, 1.962f, 53.641f), new Vector3(-149.532f, 1.993f, 48.788f),
                new Vector3(-152.067f, 2f, 43.368f), new Vector3(-153.721f, 2f, 37.613f), new Vector3(-154.931f, 2f, 31.744f),
                new Vector3(-156.106f, 2f, 25.868f), new Vector3(-157.282f, 2f, 19.992f), new Vector3(-158.457f, 2f, 14.117f),
                new Vector3(-159.632f, 2f, 8.241f), new Vector3(-160.807f, 2f, 2.365f), new Vector3(-161.915f, 1.988f, -3.523f),
                new Vector3(-162.419f, 1.956f, -9.488f), new Vector3(-161.868f, 1.905f, -15.446f), new Vector3(-160.281f, 1.837f, -21.218f),
                new Vector3(-158.118f, 1.755f, -26.805f), new Vector3(-155.892f, 1.66f, -32.369f), new Vector3(-153.667f, 1.554f, -37.933f),
                new Vector3(-151.442f, 1.439f, -43.496f), new Vector3(-149.212f, 1.318f, -49.058f), new Vector3(-146.809f, 1.192f, -54.546f),
                new Vector3(-143.897f, 1.062f, -59.779f), new Vector3(-140.264f, 0.932f, -64.535f), new Vector3(-135.856f, 0.803f, -68.581f),
                new Vector3(-130.763f, 0.677f, -71.722f), new Vector3(-125.177f, 0.556f, -73.867f), new Vector3(-119.313f, 0.442f, -75.072f),
                new Vector3(-113.341f, 0.336f, -75.538f), new Vector3(-107.35f, 0.242f, -75.6f), new Vector3(-101.357f, 0.16f, -75.6f),
                new Vector3(-95.365f, 0.093f, -75.6f), new Vector3(-89.373f, 0.043f, -75.6f), new Vector3(-83.381f, 0.011f, -75.6f)
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
            timeLimit = 1200f,
            reward = 1200,
            roadColor = new Color(0.28f, 0.25f, 0.21f),
            groundColor = new Color(0.24f, 0.3f, 0.24f),
            curbColor = new Color(0.65f, 0.63f, 0.55f),
            storyTitle = "ПОБЕРЕЖЬЕ ФИНСКОГО ЗАЛИВА",
            storyText = "Соль в воздухе и туман над водой. Здесь решает не только скорость, но и выдержка.",
            waypoints = new[]
            {
                new Vector3(-50.578f, 0f, -78f), new Vector3(-44.575f, 0f, -78f), new Vector3(-38.572f, 0f, -78f),
                new Vector3(-32.569f, 0f, -78f), new Vector3(-26.566f, 0f, -78f), new Vector3(-20.563f, 0f, -78f),
                new Vector3(-14.56f, 0f, -78f), new Vector3(-8.557f, 0f, -78f), new Vector3(-2.554f, 0f, -78f),
                new Vector3(3.45f, 0f, -78f), new Vector3(9.453f, 0f, -78f), new Vector3(15.456f, 0f, -78f),
                new Vector3(21.459f, 0f, -78f), new Vector3(27.462f, 0f, -78f), new Vector3(33.465f, 0f, -78f),
                new Vector3(39.468f, 0f, -78f), new Vector3(45.471f, 0f, -78f), new Vector3(51.474f, 0f, -78f),
                new Vector3(57.477f, 0f, -78f), new Vector3(63.48f, 0f, -78f), new Vector3(69.483f, 0f, -78f),
                new Vector3(75.487f, 0f, -78f), new Vector3(81.49f, 0f, -78f), new Vector3(87.493f, 0f, -78f),
                new Vector3(93.488f, 0f, -77.772f), new Vector3(99.344f, 0f, -76.506f), new Vector3(104.841f, 0f, -74.107f),
                new Vector3(110.149f, 0f, -71.304f), new Vector3(115.454f, 0f, -68.495f), new Vector3(120.759f, 0f, -65.686f),
                new Vector3(126.065f, 0f, -62.877f), new Vector3(131.37f, 0f, -60.069f), new Vector3(136.676f, 0f, -57.26f),
                new Vector3(141.981f, 0f, -54.451f), new Vector3(147.287f, 0.003f, -51.642f), new Vector3(152.592f, 0.013f, -48.834f),
                new Vector3(157.898f, 0.028f, -46.025f), new Vector3(163.203f, 0.048f, -43.216f), new Vector3(168.509f, 0.074f, -40.407f),
                new Vector3(173.814f, 0.104f, -37.598f), new Vector3(179.119f, 0.138f, -34.789f), new Vector3(184.357f, 0.177f, -31.858f),
                new Vector3(189.295f, 0.22f, -28.451f), new Vector3(193.648f, 0.267f, -24.329f), new Vector3(197.116f, 0.317f, -19.443f),
                new Vector3(199.447f, 0.37f, -13.924f), new Vector3(200.517f, 0.425f, -8.029f), new Vector3(200.372f, 0.484f, -2.037f),
                new Vector3(199.224f, 0.544f, 3.85f), new Vector3(197.418f, 0.607f, 9.574f), new Vector3(195.375f, 0.671f, 15.218f),
                new Vector3(193.324f, 0.736f, 20.86f), new Vector3(191.272f, 0.803f, 26.502f), new Vector3(189.221f, 0.871f, 32.143f),
                new Vector3(187.215f, 0.939f, 37.801f), new Vector3(185.716f, 1.007f, 43.609f), new Vector3(185.256f, 1.075f, 49.584f),
                new Vector3(186.089f, 1.143f, 55.516f), new Vector3(188.133f, 1.21f, 61.151f), new Vector3(191.018f, 1.277f, 66.412f),
                new Vector3(194.192f, 1.342f, 71.507f), new Vector3(197.37f, 1.406f, 76.6f), new Vector3(200.375f, 1.468f, 81.795f),
                new Vector3(202.819f, 1.528f, 87.273f), new Vector3(204.351f, 1.586f, 93.069f), new Vector3(204.704f, 1.641f, 99.05f),
                new Vector3(203.751f, 1.694f, 104.965f), new Vector3(201.532f, 1.743f, 110.53f), new Vector3(198.242f, 1.789f, 115.541f),
                new Vector3(194.171f, 1.831f, 119.946f), new Vector3(189.638f, 1.869f, 123.88f), new Vector3(184.955f, 1.903f, 127.636f),
                new Vector3(180.268f, 1.932f, 131.386f), new Vector3(175.58f, 1.956f, 135.136f), new Vector3(170.892f, 1.976f, 138.886f),
                new Vector3(166.137f, 1.99f, 142.549f), new Vector3(161.058f, 1.998f, 145.741f), new Vector3(155.532f, 2f, 148.063f),
                new Vector3(149.652f, 2f, 149.21f), new Vector3(143.663f, 2f, 149.042f), new Vector3(137.839f, 2f, 147.627f),
                new Vector3(132.342f, 2f, 145.228f), new Vector3(127.138f, 2f, 142.238f), new Vector3(122.025f, 2f, 139.092f),
                new Vector3(116.913f, 2f, 135.946f), new Vector3(111.8f, 2f, 132.8f), new Vector3(106.688f, 2f, 129.654f),
                new Vector3(101.575f, 2f, 126.508f), new Vector3(96.462f, 2f, 123.361f), new Vector3(91.35f, 2f, 120.215f),
                new Vector3(86.22f, 2f, 117.098f), new Vector3(80.9f, 2f, 114.322f), new Vector3(75.251f, 2f, 112.312f),
                new Vector3(69.328f, 2f, 111.405f), new Vector3(63.35f, 2f, 111.792f), new Vector3(57.598f, 2f, 113.47f),
                new Vector3(52.285f, 2f, 116.248f), new Vector3(47.452f, 2f, 119.803f), new Vector3(42.929f, 2f, 123.748f),
                new Vector3(38.455f, 2f, 127.751f), new Vector3(33.981f, 2f, 131.754f), new Vector3(29.507f, 2f, 135.757f),
                new Vector3(24.998f, 1.996f, 139.718f), new Vector3(20.225f, 1.987f, 143.355f), new Vector3(15.007f, 1.974f, 146.31f),
                new Vector3(9.342f, 1.956f, 148.266f), new Vector3(3.395f, 1.935f, 148.988f), new Vector3(-2.566f, 1.909f, 148.386f),
                new Vector3(-8.267f, 1.88f, 146.538f), new Vector3(-13.533f, 1.848f, 143.671f), new Vector3(-18.354f, 1.812f, 140.099f),
                new Vector3(-22.899f, 1.773f, 136.178f), new Vector3(-27.401f, 1.732f, 132.207f), new Vector3(-32.009f, 1.688f, 128.361f),
                new Vector3(-36.969f, 1.641f, 124.987f), new Vector3(-42.391f, 1.592f, 122.43f), new Vector3(-48.206f, 1.541f, 120.981f),
                new Vector3(-54.194f, 1.488f, 120.824f), new Vector3(-60.073f, 1.433f, 121.978f), new Vector3(-65.6f, 1.377f, 124.3f),
                new Vector3(-70.66f, 1.32f, 127.52f), new Vector3(-75.32f, 1.262f, 131.301f), new Vector3(-79.813f, 1.202f, 135.282f),
                new Vector3(-84.427f, 1.143f, 139.121f), new Vector3(-89.38f, 1.082f, 142.507f), new Vector3(-94.767f, 1.022f, 145.139f),
                new Vector3(-100.541f, 0.961f, 146.745f), new Vector3(-106.521f, 0.9f, 147.122f), new Vector3(-112.437f, 0.84f, 146.182f),
                new Vector3(-118.006f, 0.78f, 143.972f), new Vector3(-123.005f, 0.721f, 140.665f), new Vector3(-127.33f, 0.663f, 136.512f),
                new Vector3(-131.029f, 0.606f, 131.788f), new Vector3(-134.291f, 0.551f, 126.749f), new Vector3(-137.409f, 0.497f, 121.62f),
                new Vector3(-140.525f, 0.444f, 116.488f), new Vector3(-143.64f, 0.394f, 111.357f), new Vector3(-146.756f, 0.345f, 106.226f),
                new Vector3(-149.871f, 0.299f, 101.094f), new Vector3(-152.987f, 0.256f, 95.963f), new Vector3(-156.091f, 0.215f, 90.825f),
                new Vector3(-158.969f, 0.177f, 85.557f), new Vector3(-161.844f, 0.142f, 80.287f), new Vector3(-164.718f, 0.111f, 75.017f),
                new Vector3(-167.593f, 0.083f, 69.747f), new Vector3(-170.467f, 0.059f, 64.477f), new Vector3(-173.342f, 0.038f, 59.206f),
                new Vector3(-176.217f, 0.022f, 53.936f), new Vector3(-179.091f, 0.01f, 48.666f), new Vector3(-181.962f, 0.003f, 43.394f),
                new Vector3(-184.639f, 0f, 38.023f), new Vector3(-186.683f, 0f, 32.384f), new Vector3(-187.72f, 0f, 26.481f),
                new Vector3(-187.507f, 0f, 20.494f), new Vector3(-185.986f, 0f, 14.699f), new Vector3(-183.297f, 0f, 9.343f),
                new Vector3(-179.724f, 0f, 4.525f), new Vector3(-175.632f, 0f, 0.136f), new Vector3(-171.389f, 0f, -4.111f),
                new Vector3(-167.144f, 0f, -8.356f), new Vector3(-162.899f, 0f, -12.601f), new Vector3(-158.654f, 0f, -16.846f),
                new Vector3(-154.409f, 0f, -21.091f), new Vector3(-150.165f, 0f, -25.335f), new Vector3(-145.92f, 0f, -29.58f),
                new Vector3(-141.675f, 0f, -33.825f), new Vector3(-137.43f, 0f, -38.07f), new Vector3(-133.185f, 0f, -42.315f),
                new Vector3(-128.941f, 0f, -46.559f), new Vector3(-124.696f, 0f, -50.804f), new Vector3(-120.451f, 0f, -55.049f),
                new Vector3(-116.206f, 0f, -59.294f), new Vector3(-111.961f, 0f, -63.539f), new Vector3(-107.712f, 0f, -67.779f),
                new Vector3(-103.228f, 0f, -71.765f), new Vector3(-98.158f, 0f, -74.961f), new Vector3(-92.526f, 0f, -77.002f),
                new Vector3(-86.594f, 0f, -77.874f), new Vector3(-80.594f, 0f, -78f), new Vector3(-74.591f, 0f, -78f),
                new Vector3(-68.587f, 0f, -78f), new Vector3(-62.584f, 0f, -78f), new Vector3(-56.581f, 0f, -78f)
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
