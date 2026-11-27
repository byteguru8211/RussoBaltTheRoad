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
            location = "Тверь · 1912", prerequisite = "finland_coast", width = 8.5f, laps = 2, ai = 5, grip = 0.78f, timeLimit = 900f, reward = 1400,
            points = new[]
            {
                new Vector3(-30f, 0f, -90f), new Vector3(-23.983f, 0f, -90f), new Vector3(-17.966f, 0f, -90f),
                new Vector3(-11.949f, 0f, -90f), new Vector3(-5.931f, 0f, -90f), new Vector3(0.086f, 0f, -90f),
                new Vector3(6.103f, 0f, -90f), new Vector3(12.12f, 0f, -90f), new Vector3(18.137f, 0f, -90f),
                new Vector3(24.154f, 0f, -90f), new Vector3(30.171f, 0f, -90f), new Vector3(36.189f, 0f, -90f),
                new Vector3(42.206f, 0f, -90f), new Vector3(48.223f, 0f, -90f), new Vector3(54.24f, 0f, -90f),
                new Vector3(60.257f, 0f, -90f), new Vector3(66.274f, 0f, -90f), new Vector3(72.291f, 0.017f, -90f),
                new Vector3(78.309f, 0.058f, -90f), new Vector3(84.326f, 0.121f, -90f), new Vector3(90.343f, 0.205f, -90f),
                new Vector3(96.36f, 0.307f, -90f), new Vector3(102.377f, 0.427f, -90f), new Vector3(108.394f, 0.562f, -90f),
                new Vector3(114.411f, 0.712f, -89.963f), new Vector3(120.41f, 0.873f, -89.537f), new Vector3(126.291f, 1.044f, -88.295f),
                new Vector3(131.826f, 1.224f, -85.964f), new Vector3(136.704f, 1.411f, -82.464f), new Vector3(140.621f, 1.603f, -77.916f),
                new Vector3(143.392f, 1.799f, -72.589f), new Vector3(145.021f, 1.996f, -66.805f), new Vector3(145.71f, 2.193f, -60.832f),
                new Vector3(145.835f, 2.389f, -54.817f), new Vector3(145.717f, 2.581f, -48.802f), new Vector3(144.863f, 2.769f, -42.853f),
                new Vector3(142.943f, 2.949f, -37.159f), new Vector3(140.182f, 3.121f, -31.816f), new Vector3(137.213f, 3.282f, -26.583f),
                new Vector3(134.591f, 3.432f, -21.17f), new Vector3(132.686f, 3.568f, -15.469f), new Vector3(131.775f, 3.688f, -9.53f),
                new Vector3(131.992f, 3.792f, -3.525f), new Vector3(133.307f, 3.876f, 2.339f), new Vector3(135.523f, 3.94f, 7.928f),
                new Vector3(138.326f, 3.982f, 13.25f), new Vector3(141.298f, 3.999f, 18.482f), new Vector3(143.797f, 4f, 23.95f),
                new Vector3(145.309f, 4f, 29.764f), new Vector3(145.812f, 4f, 35.756f), new Vector3(145.824f, 4f, 41.773f),
                new Vector3(145.537f, 4f, 47.781f), new Vector3(144.527f, 4f, 53.707f), new Vector3(142.476f, 3.999f, 59.353f),
                new Vector3(139.258f, 3.982f, 64.421f), new Vector3(134.944f, 3.94f, 68.596f), new Vector3(129.778f, 3.876f, 71.654f),
                new Vector3(124.077f, 3.792f, 73.55f), new Vector3(118.132f, 3.688f, 74.44f), new Vector3(112.12f, 3.568f, 74.66f),
                new Vector3(106.103f, 3.432f, 74.664f), new Vector3(100.086f, 3.282f, 74.664f), new Vector3(94.069f, 3.121f, 74.664f),
                new Vector3(88.051f, 2.949f, 74.664f), new Vector3(82.034f, 2.769f, 74.664f), new Vector3(76.017f, 2.581f, 74.664f),
                new Vector3(70f, 2.389f, 74.664f), new Vector3(63.983f, 2.193f, 74.664f), new Vector3(57.966f, 1.996f, 74.664f),
                new Vector3(51.949f, 1.799f, 74.664f), new Vector3(45.931f, 1.603f, 74.664f), new Vector3(39.914f, 1.411f, 74.664f),
                new Vector3(33.897f, 1.224f, 74.664f), new Vector3(27.88f, 1.044f, 74.664f), new Vector3(21.863f, 0.873f, 74.664f),
                new Vector3(15.846f, 0.712f, 74.664f), new Vector3(9.829f, 0.562f, 74.664f), new Vector3(3.811f, 0.427f, 74.664f),
                new Vector3(-2.206f, 0.307f, 74.664f), new Vector3(-8.223f, 0.205f, 74.664f), new Vector3(-14.24f, 0.121f, 74.664f),
                new Vector3(-20.257f, 0.058f, 74.664f), new Vector3(-26.274f, 0.017f, 74.664f), new Vector3(-32.291f, 0f, 74.664f),
                new Vector3(-38.309f, 0f, 74.664f), new Vector3(-44.326f, 0f, 74.664f), new Vector3(-50.343f, 0f, 74.664f),
                new Vector3(-56.36f, 0f, 74.664f), new Vector3(-62.377f, 0f, 74.664f), new Vector3(-68.394f, 0f, 74.664f),
                new Vector3(-74.411f, 0.001f, 74.627f), new Vector3(-80.41f, 0.034f, 74.202f), new Vector3(-86.291f, 0.107f, 72.96f),
                new Vector3(-91.826f, 0.215f, 70.629f), new Vector3(-96.704f, 0.354f, 67.129f), new Vector3(-100.621f, 0.519f, 62.58f),
                new Vector3(-103.392f, 0.706f, 57.253f), new Vector3(-105.021f, 0.91f, 51.469f), new Vector3(-105.71f, 1.126f, 45.496f),
                new Vector3(-105.835f, 1.35f, 39.481f), new Vector3(-105.954f, 1.577f, 33.466f), new Vector3(-106.807f, 1.803f, 27.517f),
                new Vector3(-108.727f, 2.022f, 21.824f), new Vector3(-111.488f, 2.23f, 16.481f), new Vector3(-114.457f, 2.423f, 11.247f),
                new Vector3(-117.08f, 2.596f, 5.835f), new Vector3(-118.984f, 2.744f, 0.133f), new Vector3(-119.896f, 2.863f, -5.806f),
                new Vector3(-119.678f, 2.947f, -11.81f), new Vector3(-118.364f, 2.993f, -17.674f), new Vector3(-116.147f, 3f, -23.263f),
                new Vector3(-113.344f, 3f, -28.586f), new Vector3(-110.372f, 3f, -33.818f), new Vector3(-107.873f, 3f, -39.285f),
                new Vector3(-106.361f, 3f, -45.1f), new Vector3(-105.859f, 2.999f, -51.091f), new Vector3(-105.824f, 2.961f, -57.108f),
                new Vector3(-105.537f, 2.869f, -63.117f), new Vector3(-104.527f, 2.732f, -69.042f), new Vector3(-102.476f, 2.557f, -74.688f),
                new Vector3(-99.258f, 2.349f, -79.757f), new Vector3(-94.944f, 2.118f, -83.931f), new Vector3(-89.778f, 1.869f, -86.99f),
                new Vector3(-84.077f, 1.61f, -88.886f), new Vector3(-78.132f, 1.348f, -89.775f), new Vector3(-72.12f, 1.09f, -89.996f),
                new Vector3(-66.103f, 0.844f, -90f), new Vector3(-60.086f, 0.616f, -90f), new Vector3(-54.069f, 0.413f, -90f),
                new Vector3(-48.051f, 0.243f, -90f), new Vector3(-42.034f, 0.113f, -90f), new Vector3(-36.017f, 0.029f, -90f)
            }
        });
        CreateTrack(new TrackDefinition
        {
            id = "riga_speedway", name = "Рижская кольцевая", chapter = "Скоростное кольцо",
            description = "Широкая приморская трасса с длинными прямыми, быстрыми дугами и опасной шиканой.",
            location = "Рига · 1912", prerequisite = "tver_forest", width = 12f, laps = 3, ai = 6, grip = 0.88f, timeLimit = 1200f, reward = 1800,
            points = new[]
            {
                new Vector3(-132.5f, 0f, 0f), new Vector3(-126.492f, 0f, 0f), new Vector3(-120.484f, 0f, 0f),
                new Vector3(-114.476f, 0f, 0f), new Vector3(-108.468f, 0f, 0f), new Vector3(-102.46f, 0f, 0f),
                new Vector3(-96.452f, 0f, 0f), new Vector3(-90.443f, 0f, 0f), new Vector3(-84.435f, 0f, 0f),
                new Vector3(-78.427f, 0f, 0f), new Vector3(-72.419f, 0f, 0f), new Vector3(-66.411f, 0f, 0f),
                new Vector3(-60.403f, 0f, 0f), new Vector3(-54.395f, 0f, 0f), new Vector3(-48.387f, 0f, 0f),
                new Vector3(-42.379f, 0f, 0f), new Vector3(-36.371f, 0f, 0f), new Vector3(-30.363f, 0f, 0f),
                new Vector3(-24.355f, 0f, 0f), new Vector3(-18.346f, 0f, 0f), new Vector3(-12.338f, 0f, 0f),
                new Vector3(-6.33f, 0f, 0f), new Vector3(-0.322f, 0f, 0f), new Vector3(5.686f, 0f, 0f),
                new Vector3(11.694f, 0f, 0f), new Vector3(17.702f, 0f, 0f), new Vector3(23.71f, 0f, 0f),
                new Vector3(29.718f, 0f, 0f), new Vector3(35.726f, 0f, 0f), new Vector3(41.734f, 0f, 0f),
                new Vector3(47.742f, 0f, 0f), new Vector3(53.751f, 0f, 0f), new Vector3(59.759f, 0f, 0f),
                new Vector3(65.767f, 0f, 0f), new Vector3(71.775f, 0f, 0f), new Vector3(77.783f, 0f, 0f),
                new Vector3(83.791f, 0f, 0f), new Vector3(89.799f, 0f, 0f), new Vector3(95.807f, 0f, 0f),
                new Vector3(101.815f, 0f, 0f), new Vector3(107.823f, 0f, 0f), new Vector3(113.831f, 0f, 0f),
                new Vector3(119.839f, 0f, 0f), new Vector3(125.848f, 0f, 0f), new Vector3(131.856f, 0f, 0f),
                new Vector3(137.864f, 0f, 0f), new Vector3(143.872f, 0f, 0f), new Vector3(149.88f, 0f, 0f),
                new Vector3(155.888f, 0f, 0f), new Vector3(161.896f, 0f, 0f), new Vector3(167.904f, 0f, 0f),
                new Vector3(173.912f, 0f, 0f), new Vector3(179.92f, 0f, 0.042f), new Vector3(185.924f, 0f, 0.245f),
                new Vector3(191.912f, 0f, 0.729f), new Vector3(197.855f, 0f, 1.603f), new Vector3(203.705f, 0f, 2.962f),
                new Vector3(209.395f, 0f, 4.884f), new Vector3(214.835f, 0f, 7.425f), new Vector3(219.921f, 0f, 10.617f),
                new Vector3(224.534f, 0f, 14.458f), new Vector3(228.554f, 0f, 18.916f), new Vector3(231.861f, 0f, 23.925f),
                new Vector3(234.349f, 0f, 29.387f), new Vector3(235.935f, 0f, 35.175f), new Vector3(236.561f, 0f, 41.143f),
                new Vector3(236.206f, 0f, 47.134f), new Vector3(234.883f, 0f, 52.988f), new Vector3(232.637f, 0f, 58.554f),
                new Vector3(229.548f, 0f, 63.7f), new Vector3(225.715f, 0f, 68.319f), new Vector3(221.254f, 0f, 72.336f),
                new Vector3(216.287f, 0f, 75.709f), new Vector3(210.934f, 0f, 78.429f), new Vector3(205.304f, 0f, 80.518f),
                new Vector3(199.49f, 0f, 82.026f), new Vector3(193.568f, 0f, 83.026f), new Vector3(187.589f, 0f, 83.608f),
                new Vector3(181.587f, 0f, 83.877f), new Vector3(175.58f, 0f, 83.951f), new Vector3(169.572f, 0f, 83.954f),
                new Vector3(163.563f, 0f, 83.954f), new Vector3(157.555f, 0f, 83.954f), new Vector3(151.547f, 0f, 83.954f),
                new Vector3(145.539f, 0f, 83.954f), new Vector3(139.531f, 0f, 83.954f), new Vector3(133.523f, 0f, 83.954f),
                new Vector3(127.515f, 0f, 83.954f), new Vector3(121.507f, 0f, 83.954f), new Vector3(115.499f, 0f, 83.954f),
                new Vector3(109.491f, 0f, 83.954f), new Vector3(103.483f, 0f, 83.954f), new Vector3(97.475f, 0f, 83.954f),
                new Vector3(91.466f, 0f, 83.954f), new Vector3(85.458f, 0f, 83.954f), new Vector3(79.45f, 0f, 83.954f),
                new Vector3(73.442f, 0f, 83.957f), new Vector3(67.438f, 0f, 84.145f), new Vector3(61.483f, 0f, 84.908f),
                new Vector3(55.696f, 0f, 86.502f), new Vector3(50.24f, 0f, 89.001f), new Vector3(45.234f, 0f, 92.315f),
                new Vector3(40.674f, 0f, 96.223f), new Vector3(36.387f, 0f, 100.431f), new Vector3(32.094f, 0f, 104.634f),
                new Vector3(27.608f, 0f, 108.63f), new Vector3(22.81f, 0f, 112.242f), new Vector3(17.642f, 0f, 115.299f),
                new Vector3(12.114f, 0f, 117.638f), new Vector3(6.299f, 0f, 119.125f), new Vector3(0.322f, 0f, 119.667f),
                new Vector3(-5.663f, 0f, 119.229f), new Vector3(-11.502f, 0f, 117.84f), new Vector3(-17.066f, 0f, 115.587f),
                new Vector3(-22.274f, 0f, 112.6f), new Vector3(-27.11f, 0f, 109.039f), new Vector3(-31.624f, 0f, 105.076f),
                new Vector3(-35.931f, 0f, 100.887f), new Vector3(-40.206f, 0f, 96.666f), new Vector3(-44.725f, 0f, 92.71f),
                new Vector3(-49.68f, 0f, 89.321f), new Vector3(-55.092f, 0f, 86.727f), new Vector3(-60.851f, 0f, 85.037f),
                new Vector3(-66.796f, 0f, 84.193f), new Vector3(-72.798f, 0f, 83.961f), new Vector3(-78.806f, 0f, 83.954f),
                new Vector3(-84.814f, 0f, 83.954f), new Vector3(-90.822f, 0f, 83.954f), new Vector3(-96.83f, 0f, 83.954f),
                new Vector3(-102.838f, 0f, 83.954f), new Vector3(-108.846f, 0f, 83.954f), new Vector3(-114.854f, 0f, 83.954f),
                new Vector3(-120.862f, 0f, 83.954f), new Vector3(-126.871f, 0f, 83.954f), new Vector3(-132.879f, 0f, 83.954f),
                new Vector3(-138.887f, 0f, 83.954f), new Vector3(-144.895f, 0f, 83.954f), new Vector3(-150.903f, 0f, 83.954f),
                new Vector3(-156.911f, 0f, 83.954f), new Vector3(-162.919f, 0f, 83.954f), new Vector3(-168.927f, 0f, 83.954f),
                new Vector3(-174.935f, 0f, 83.953f), new Vector3(-180.943f, 0f, 83.892f), new Vector3(-186.946f, 0f, 83.65f),
                new Vector3(-192.928f, 0f, 83.107f), new Vector3(-198.859f, 0f, 82.156f), new Vector3(-204.687f, 0f, 80.706f),
                new Vector3(-210.341f, 0f, 78.682f), new Vector3(-215.729f, 0f, 76.032f), new Vector3(-220.743f, 0f, 72.729f),
                new Vector3(-225.264f, 0f, 68.78f), new Vector3(-229.17f, 0f, 64.222f), new Vector3(-232.345f, 0f, 59.128f),
                new Vector3(-234.685f, 0f, 53.601f), new Vector3(-236.11f, 0f, 47.771f), new Vector3(-236.57f, 0f, 41.788f),
                new Vector3(-236.048f, 0f, 35.809f), new Vector3(-234.564f, 0f, 29.994f), new Vector3(-232.168f, 0f, 24.491f),
                new Vector3(-228.945f, 0f, 19.428f), new Vector3(-224.996f, 0f, 14.907f), new Vector3(-220.44f, 0f, 10.998f),
                new Vector3(-215.399f, 0f, 7.736f), new Vector3(-209.992f, 0f, 5.126f), new Vector3(-204.324f, 0f, 3.14f),
                new Vector3(-198.488f, 0f, 1.724f), new Vector3(-192.552f, 0f, 0.802f), new Vector3(-186.568f, 0f, 0.281f),
                new Vector3(-180.564f, 0f, 0.054f), new Vector3(-174.557f, 0f, 0.001f), new Vector3(-168.548f, 0f, 0f),
                new Vector3(-162.54f, 0f, 0f), new Vector3(-156.532f, 0f, 0f), new Vector3(-150.524f, 0f, 0f),
                new Vector3(-144.516f, 0f, 0f), new Vector3(-138.508f, 0f, 0f)
            }
        });
        CreateTrack(new TrackDefinition
        {
            id = "crimean_coast", name = "Крымское побережье", chapter = "Южная экспедиция",
            description = "Каменные мосты, резкие подъёмы и морской ветер на извилистой дороге над заливом.",
            location = "Крым · 1912", prerequisite = "riga_speedway", width = 8.5f, laps = 2, ai = 5, grip = 0.82f, timeLimit = 1080f, reward = 2200,
            points = new[]
            {
                new Vector3(-68.899f, 0f, -103.5f), new Vector3(-62.909f, 0.023f, -103.5f), new Vector3(-56.918f, 0.09f, -103.5f),
                new Vector3(-50.928f, 0.202f, -103.5f), new Vector3(-44.937f, 0.357f, -103.5f), new Vector3(-38.947f, 0.555f, -103.5f),
                new Vector3(-32.956f, 0.793f, -103.5f), new Vector3(-26.966f, 1.07f, -103.5f), new Vector3(-20.976f, 1.384f, -103.5f),
                new Vector3(-14.985f, 1.732f, -103.5f), new Vector3(-8.995f, 2.112f, -103.5f), new Vector3(-3.004f, 2.52f, -103.5f),
                new Vector3(2.986f, 2.953f, -103.5f), new Vector3(8.977f, 3.409f, -103.5f), new Vector3(14.967f, 3.884f, -103.5f),
                new Vector3(20.958f, 4.374f, -103.5f), new Vector3(26.948f, 4.877f, -103.5f), new Vector3(32.938f, 5.387f, -103.5f),
                new Vector3(38.929f, 5.903f, -103.5f), new Vector3(44.919f, 6.42f, -103.5f), new Vector3(50.91f, 6.935f, -103.5f),
                new Vector3(56.9f, 7.445f, -103.5f), new Vector3(62.891f, 7.946f, -103.5f), new Vector3(68.881f, 8.436f, -103.5f),
                new Vector3(74.871f, 8.913f, -103.5f), new Vector3(80.862f, 9.372f, -103.5f), new Vector3(86.852f, 9.813f, -103.5f),
                new Vector3(92.843f, 10.233f, -103.5f), new Vector3(98.833f, 10.63f, -103.498f), new Vector3(104.818f, 11.003f, -103.275f),
                new Vector3(110.722f, 11.351f, -102.305f), new Vector3(116.347f, 11.673f, -100.273f), new Vector3(121.454f, 11.968f, -97.16f),
                new Vector3(125.936f, 12.236f, -93.194f), new Vector3(129.946f, 12.477f, -88.746f), new Vector3(133.845f, 12.692f, -84.198f),
                new Vector3(137.743f, 12.881f, -79.649f), new Vector3(141.638f, 13.044f, -75.098f), new Vector3(145.367f, 13.183f, -70.412f),
                new Vector3(148.57f, 13.3f, -65.355f), new Vector3(150.919f, 13.394f, -59.853f), new Vector3(152.17f, 13.469f, -54.005f),
                new Vector3(152.22f, 13.526f, -48.025f), new Vector3(151.139f, 13.567f, -42.141f), new Vector3(149.159f, 13.593f, -36.492f),
                new Vector3(146.63f, 13.607f, -31.062f), new Vector3(143.952f, 13.611f, -25.704f), new Vector3(141.295f, 13.607f, -20.335f),
                new Vector3(138.951f, 13.596f, -14.825f), new Vector3(137.336f, 13.581f, -9.063f), new Vector3(136.761f, 13.564f, -3.109f),
                new Vector3(137.38f, 13.546f, 2.839f), new Vector3(139.158f, 13.53f, 8.55f), new Vector3(141.888f, 13.516f, 13.875f),
                new Vector3(145.242f, 13.505f, 18.837f), new Vector3(148.827f, 13.5f, 23.636f), new Vector3(152.413f, 13.501f, 28.435f),
                new Vector3(155.808f, 13.51f, 33.369f), new Vector3(158.692f, 13.525f, 38.615f), new Vector3(160.769f, 13.549f, 44.226f),
                new Vector3(161.801f, 13.581f, 50.118f), new Vector3(161.648f, 13.621f, 56.096f), new Vector3(160.295f, 13.669f, 61.921f),
                new Vector3(157.86f, 13.725f, 67.386f), new Vector3(154.562f, 13.787f, 72.38f), new Vector3(150.672f, 13.857f, 76.933f),
                new Vector3(146.484f, 13.932f, 81.216f), new Vector3(142.249f, 14.012f, 85.451f), new Vector3(137.94f, 14.096f, 89.613f),
                new Vector3(133.278f, 14.182f, 93.367f), new Vector3(128.099f, 14.269f, 96.363f), new Vector3(122.448f, 14.357f, 98.321f),
                new Vector3(116.52f, 14.443f, 99.105f), new Vector3(110.546f, 14.527f, 98.773f), new Vector3(104.68f, 14.608f, 97.574f),
                new Vector3(98.924f, 14.683f, 95.917f), new Vector3(93.186f, 14.753f, 94.196f), new Vector3(87.448f, 14.815f, 92.474f),
                new Vector3(81.71f, 14.87f, 90.753f), new Vector3(75.945f, 14.916f, 89.127f), new Vector3(70.065f, 14.952f, 88.001f),
                new Vector3(64.088f, 14.979f, 87.749f), new Vector3(58.169f, 14.995f, 88.606f), new Vector3(52.542f, 15f, 90.632f),
                new Vector3(47.414f, 14.995f, 93.712f), new Vector3(42.861f, 14.979f, 97.597f), new Vector3(38.775f, 14.952f, 101.975f),
                new Vector3(34.87f, 14.916f, 106.518f), new Vector3(30.95f, 14.87f, 111.048f), new Vector3(26.784f, 14.815f, 115.349f),
                new Vector3(22.128f, 14.753f, 119.11f), new Vector3(16.911f, 14.683f, 122.036f), new Vector3(11.226f, 14.608f, 123.894f),
                new Vector3(5.284f, 14.527f, 124.57f), new Vector3(-0.681f, 14.443f, 124.112f), new Vector3(-6.502f, 14.357f, 122.719f),
                new Vector3(-12.146f, 14.269f, 120.714f), new Vector3(-17.71f, 14.182f, 118.496f), new Vector3(-23.272f, 14.096f, 116.271f),
                new Vector3(-28.834f, 14.012f, 114.046f), new Vector3(-34.396f, 13.932f, 111.821f), new Vector3(-39.964f, 13.857f, 109.612f),
                new Vector3(-45.654f, 13.787f, 107.748f), new Vector3(-51.551f, 13.725f, 106.738f), new Vector3(-57.529f, 13.669f, 106.881f),
                new Vector3(-63.366f, 13.621f, 108.192f), new Vector3(-68.927f, 13.581f, 110.408f), new Vector3(-74.302f, 13.549f, 113.051f),
                new Vector3(-79.66f, 13.525f, 115.73f), new Vector3(-85.033f, 13.51f, 118.379f), new Vector3(-90.545f, 13.501f, 120.719f),
                new Vector3(-96.294f, 13.5f, 122.385f), new Vector3(-102.235f, 13.505f, 123.086f), new Vector3(-108.198f, 13.516f, 122.638f),
                new Vector3(-113.949f, 13.53f, 120.998f), new Vector3(-119.273f, 13.546f, 118.271f), new Vector3(-124.058f, 13.564f, 114.677f),
                new Vector3(-128.346f, 13.581f, 110.496f), new Vector3(-132.337f, 13.596f, 106.029f), new Vector3(-136.281f, 13.607f, 101.521f),
                new Vector3(-140.226f, 13.611f, 97.013f), new Vector3(-144.171f, 13.607f, 92.505f), new Vector3(-148.039f, 13.593f, 87.931f),
                new Vector3(-151.447f, 13.567f, 83.01f), new Vector3(-154.004f, 13.526f, 77.603f), new Vector3(-155.448f, 13.469f, 71.8f),
                new Vector3(-155.732f, 13.394f, 65.825f), new Vector3(-155.053f, 13.3f, 59.877f), new Vector3(-153.836f, 13.183f, 54.012f),
                new Vector3(-152.537f, 13.044f, 48.164f), new Vector3(-151.237f, 12.881f, 42.317f), new Vector3(-149.938f, 12.692f, 36.469f),
                new Vector3(-148.638f, 12.477f, 30.621f), new Vector3(-147.362f, 12.236f, 24.768f), new Vector3(-146.549f, 11.968f, 18.838f),
                new Vector3(-146.772f, 11.673f, 12.862f), new Vector3(-148.158f, 11.351f, 7.043f), new Vector3(-150.376f, 11.003f, 1.48f),
                new Vector3(-152.808f, 10.63f, -3.994f), new Vector3(-155.241f, 10.233f, -9.468f), new Vector3(-157.674f, 9.813f, -14.942f),
                new Vector3(-160.107f, 9.372f, -20.416f), new Vector3(-162.498f, 8.913f, -25.909f), new Vector3(-164.52f, 8.436f, -31.545f),
                new Vector3(-165.787f, 7.946f, -37.393f), new Vector3(-166.014f, 7.445f, -43.37f), new Vector3(-165.058f, 6.935f, -49.274f),
                new Vector3(-162.942f, 6.42f, -54.868f), new Vector3(-159.845f, 5.903f, -59.987f), new Vector3(-156.049f, 5.387f, -64.616f),
                new Vector3(-151.881f, 4.877f, -68.919f), new Vector3(-147.645f, 4.374f, -73.155f), new Vector3(-143.409f, 3.884f, -77.391f),
                new Vector3(-139.173f, 3.409f, -81.627f), new Vector3(-134.938f, 2.953f, -85.862f), new Vector3(-130.702f, 2.52f, -90.098f),
                new Vector3(-126.411f, 2.112f, -94.278f), new Vector3(-121.737f, 1.732f, -98.016f), new Vector3(-116.491f, 1.384f, -100.889f),
                new Vector3(-110.779f, 1.07f, -102.661f), new Vector3(-104.84f, 0.793f, -103.397f), new Vector3(-98.851f, 0.555f, -103.5f),
                new Vector3(-92.861f, 0.357f, -103.5f), new Vector3(-86.87f, 0.202f, -103.5f), new Vector3(-80.88f, 0.09f, -103.5f),
                new Vector3(-74.889f, 0.023f, -103.5f)
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
