using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameBuilderEditor
{
    private const string VehicleRoot = "Assets/GameAssets/Vehicles";
    private const string EnvironmentRoot = "Assets/GameAssets/Environment/Russia_1912/modules";

    [MenuItem("RussoBalt/Build Game Content")]
    public static void BuildAll()
    {
        CreateFolders();
        ConfigureImporters();
        BuildVehiclePrefabs();
        BuildEnvironmentPrefabs();
        CreateTrackAssets();
        CreateScenes();
        ConfigurePlayerSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RussoBalt game content built");
    }

    private static void CreateFolders()
    {
        foreach (string folder in new[]
        {
            "Assets/Scripts", "Assets/Scripts/Core", "Assets/Scripts/Vehicle", "Assets/Scripts/Race", "Assets/Scripts/UI", "Assets/Scripts/Editor",
            "Assets/Resources/Vehicles", "Assets/Resources/Tracks", "Assets/Resources/Environment", "Assets/Resources/UI", "Assets/Prefabs", "Assets/Materials", "Assets/Scenes"
        })
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }
    }

    private static void ConfigureImporters()
    {
        foreach (string path in Directory.GetFiles(Application.dataPath + "/GameAssets", "*.fbx", SearchOption.AllDirectories))
        {
            string assetPath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) continue;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.animationType = path.Contains("Characters") ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.SaveAndReimport();
        }
    }

    public static void BuildVehiclePrefabs()
    {
        BuildVehicle("russo_balt_c24_55", "RussoBalt_C24_55", "RB", 1.365f, 3.20f, 0.44f, 1300f, 210f, 1.63f, 4.63f, 1.41f);
        BuildVehicle("benz_1912_racer_concept", "Benz_1912_Racer_Concept", "BENZ", 1.40f, 3.35f, 0.46f, 1450f, 380f, 1.67f, 4.86f, 1.46f);
        BuildVehicle("ford_model_t_1912", "Ford_ModelT_1912", "Ford_ModelT_1912", 1.43f, 2.54f, 0.381f, 544f, 150f, 1.73f, 3.42f, 1.95f);
        BuildVehicle("fiat_s76_1910", "Fiat_S76_1910", "Fiat_S76_1910", 1.30f, 3.70f, 0.46f, 1650f, 520f, 1.30f, 3.75f, 1.75f);
        BuildVehicle("mercedes_37_90_1912", "Mercedes_37_90_1912", "Mercedes_37_90_1912", 1.55f, 3.45f, 0.46f, 1950f, 340f, 1.75f, 4.95f, 1.75f);
    }

    private static void BuildVehicle(string id, string fileName, string prefix, float trackWidth, float wheelbase, float radius, float mass, float torque, float width, float length, float height)
    {
        string sourcePath = fileName == "Benz_1912_Racer_Concept"
            ? $"{VehicleRoot}/Benz_1912_Prototype/{fileName}_LOD0.fbx"
            : $"{VehicleRoot}/{fileName}/{fileName}_LOD0.fbx";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"Missing vehicle asset {sourcePath}");
            return;
        }
        GameObject instance = UnityEngine.Object.Instantiate(source);
        instance.name = id + "_Model";
        GameObject wrapper = new GameObject(id);
        instance.transform.SetParent(wrapper.transform, true);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;

        Rigidbody body = wrapper.AddComponent<Rigidbody>();
        body.mass = mass;
        body.centerOfMass = new Vector3(0f, 0.42f, 0f);
        body.linearDamping = 0.06f;
        body.angularDamping = 0.85f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BoxCollider hull = wrapper.AddComponent<BoxCollider>();
        hull.center = new Vector3(0f, height * 0.62f, 0f);
        hull.size = new Vector3(width * 0.92f, height * 0.78f, length * 0.94f);

        VehicleController1912 controller = wrapper.AddComponent<VehicleController1912>();
        controller.mass = mass;
        controller.engineTorque = torque;
        controller.wheelRadius = radius;
        controller.frontLeft = CreateWheel(wrapper, "FL", -trackWidth / 2f, wheelbase / 2f, radius);
        controller.frontRight = CreateWheel(wrapper, "FR", trackWidth / 2f, wheelbase / 2f, radius);
        controller.rearLeft = CreateWheel(wrapper, "RL", -trackWidth / 2f, -wheelbase / 2f, radius);
        controller.rearRight = CreateWheel(wrapper, "RR", trackWidth / 2f, -wheelbase / 2f, radius);
        controller.frontLeftSteerVisual = FindTransform(instance.transform, $"{prefix}_Wheel_FL_Steer");
        controller.frontRightSteerVisual = FindTransform(instance.transform, $"{prefix}_Wheel_FR_Steer");
        controller.frontLeftSpinVisual = FindTransform(instance.transform, $"{prefix}_Wheel_FL_Spin");
        controller.frontRightSpinVisual = FindTransform(instance.transform, $"{prefix}_Wheel_FR_Spin");
        controller.rearLeftSpinVisual = FindTransform(instance.transform, $"{prefix}_Wheel_RL_Spin");
        controller.rearRightSpinVisual = FindTransform(instance.transform, $"{prefix}_Wheel_RR_Spin");
        controller.steeringWheelVisual = FindTransformContains(instance.transform, "SteeringWheel_Pivot");
        wrapper.AddComponent<VehicleInput1912>().vehicle = controller;
        wrapper.AddComponent<EngineAudio1912>().vehicle = controller;

        ConvertMaterials(instance);
        PrefabUtility.SaveAsPrefabAsset(wrapper, $"Assets/Resources/Vehicles/{id}.prefab");
        UnityEngine.Object.DestroyImmediate(wrapper);
        AssetDatabase.SaveAssets();
        Debug.Log($"Built vehicle prefab {id}");
    }

    private static WheelCollider CreateWheel(GameObject parent, string code, float x, float z, float radius)
    {
        GameObject wheel = new GameObject("WheelCollider_" + code);
        wheel.transform.SetParent(parent.transform, false);
        wheel.transform.localPosition = new Vector3(x, radius, z);
        WheelCollider collider = wheel.AddComponent<WheelCollider>();
        collider.radius = radius;
        collider.suspensionDistance = 0.16f;
        collider.suspensionSpring = new JointSpring { spring = 42000f, damper = 4500f, targetPosition = 0.5f };
        return collider;
    }

    private static void BuildEnvironmentPrefabs()
    {
        BuildEnvironmentPrefab("Building_Wood_House_01", "House");
        BuildEnvironmentPrefab("Tree_Birch_01", "Birch");
        BuildEnvironmentPrefab("Tree_Pine_01", "Pine");
        BuildEnvironmentPrefab("Signpost_Russia_1912", "Sign");
        BuildEnvironmentPrefab("Prop_Horse_Cart_01", "Cart");
    }

    private static void BuildEnvironmentPrefab(string fileName, string resourceName)
    {
        string path = $"{EnvironmentRoot}/{fileName}_LOD0.fbx";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) return;
        GameObject instance = UnityEngine.Object.Instantiate(source);
        GameObject wrapper = new GameObject(resourceName);
        instance.transform.SetParent(wrapper.transform, true);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;
        ConvertMaterials(instance);
        PrefabUtility.SaveAsPrefabAsset(wrapper, $"Assets/Resources/Environment/{resourceName}.prefab");
        UnityEngine.Object.DestroyImmediate(wrapper);
    }

    private static void ConvertMaterials(GameObject instance)
    {
        foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] converted = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material source = renderer.sharedMaterials[i];
                if (source == null) continue;
                string safe = string.Concat(source.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
                string materialPath = $"Assets/Materials/Vehicle_{safe}.asset";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.name = "Vehicle_" + safe;
                    if (source.HasProperty("_Color")) material.SetColor("_BaseColor", source.color);
                    if (source.HasProperty("_MainTex") && source.mainTexture != null) material.SetTexture("_BaseMap", source.mainTexture);
                    float metallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0.2f;
                    float smoothness = source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.3f;
                    material.SetFloat("_Metallic", metallic);
                    material.SetFloat("_Smoothness", smoothness);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                converted[i] = material;
            }
            renderer.sharedMaterials = converted;
        }
    }

    private static Transform FindTransform(Transform root, string exactName)
    {
        return root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == exactName);
    }

    private static Transform FindTransformContains(Transform root, string partial)
    {
        return root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.Contains(partial));
    }

    private static void CreateTrackAssets()
    {
        CreateTrack("winter_departure", "Зимний выезд", "Глава I · 1912", "Первый маршрут от рижской мастерской к северной дороге.", new[]
        {
            new Vector3(-55,0,35), new Vector3(-20,0,5), new Vector3(15,0,-30), new Vector3(55,0,-15), new Vector3(50,0,35), new Vector3(10,0,60), new Vector3(-40,0,55)
        }, new Color(0.42f,0.34f,0.24f), new Color(0.26f,0.32f,0.23f), 0.72f, 1, 4, 420f, 600, "Первый оборот", "Морозный воздух, ручной старт и первая передача. Доберитесь до контрольных пунктов, не перегревая двигатель и следя за топливом.", "UI/Russia", "forest");
        CreateTrack("alps_pass", "Альпийский перевал", "Глава II · 1912", "Серпантины, туман и торможение двигателем.", new[]
        {
            new Vector3(-70,0,45), new Vector3(-45,7,15), new Vector3(-10,14,-25), new Vector3(30,10,-40), new Vector3(65,4,-5), new Vector3(45,0,40), new Vector3(0,2,65), new Vector3(-45,0,55)
        }, new Color(0.28f,0.27f,0.24f), new Color(0.20f,0.27f,0.18f), 0.62f, 1, 5, 360f, 900, "Через Альпы", "Дорога сужается, повороты становятся круче, а туман скрывает следующую точку. Не забывайте о задних тормозах и теплорежиме.", "UI/Hero", "forest");
        CreateTrack("monaco_finish", "Финиш в Монако", "Глава III · 1912", "Финальный этап экспедиции у Средиземного моря.", new[]
        {
            new Vector3(-60,0,30), new Vector3(-25,0,-10), new Vector3(20,0,-35), new Vector3(65,0,-10), new Vector3(55,0,40), new Vector3(5,0,55), new Vector3(-45,0,50)
        }, new Color(0.38f,0.34f,0.28f), new Color(0.24f,0.30f,0.18f), 0.9f, 2, 6, 420f, 1400, "Монако", "Финишная арка уже видна. Соперники идут вплотную, а последний повод решает исход экспедиции.", "UI/Hero", "town");
    }

    private static void CreateTrack(string id, string name, string chapter, string description, Vector3[] waypoints, Color road, Color ground, float grip, int laps, int ai, float limit, int reward, string storyTitle, string story, string preview, string scenery)
    {
        TrackData track = ScriptableObject.CreateInstance<TrackData>();
        track.id = id;
        track.displayName = name;
        track.chapter = chapter;
        track.description = description;
        track.waypoints = waypoints;
        track.roadColor = road;
        track.groundColor = ground;
        track.surfaceGrip = grip;
        track.laps = laps;
        track.aiCount = ai;
        track.timeLimit = limit;
        track.reward = reward;
        track.storyTitle = storyTitle;
        track.storyText = story;
        track.previewResource = preview;
        track.scenery = scenery;
        AssetDatabase.CreateAsset(track, $"Assets/Resources/Tracks/{id}.asset");
    }

    public static void CreateScenes()
    {
        CreateMenuScene("MainMenu", "MainMenuUI");
        CreateMenuScene("Garage", "GarageUI");
        CreateRaceScene("Race_Winter");
        CreateRaceScene("Race_Alps");
        CreateRaceScene("Race_Monaco");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
    }

    private static void CreateMenuScene(string sceneName, string uiType)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 2, -8);
        camera.transform.rotation = Quaternion.Euler(8, 0, 0);
        GameObject light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        new GameObject("GameManager").AddComponent<GameManager>();
        GameObject ui = new GameObject("UI");
        if (uiType == "MainMenuUI") ui.AddComponent<MainMenuUI>();
        else ui.AddComponent<GarageUI>();
        EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneName}.unity");
    }

    private static void CreateRaceScene(string sceneName)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        GameObject light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("RaceManager").AddComponent<RaceManager>();
        EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneName}.unity");
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "RussoBalt The Road";
        PlayerSettings.productName = "RussoBalt The Road";
        PlayerSettings.bundleVersion = "0.3.0";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.russobalt.theroad");
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Garage.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Race_Winter.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Race_Alps.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Race_Monaco.unity", true)
        };
    }
}
