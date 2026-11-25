using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameBuilderEditor
{
    private const string VehicleRoot = "Assets/GameAssets/Vehicles";
    private const string EnvironmentRoot = "Assets/GameAssets/Environment/Russia_1912/modules";

    [MenuItem("RussoBalt/Build Missing Game Content")]
    public static void BuildAll()
    {
        CreateFolders();
        ConfigureImporters();
        GameBuilderAssetUtility.ConfigureTextureImporters();
        ConfigurePreRaceImporter();
        ConfigureUIImageImporters();
        BuildVehiclePrefabs();
        BuildNetworkPrefabs();
        BuildEnvironmentPrefabs();
        BuildPreRacePrefab();
        CreateTrackAssets();
        AdditionalTrackInstaller.InstallAdditionalTracks();
        CreateEnvironmentProfiles();
        CreateScenes();
        ConfigurePlayerSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RussoBalt missing game content ensured; existing prefabs and scenes were preserved");
    }

    [MenuItem("RussoBalt/Rebuild Vehicle Prefabs")]
    public static void RebuildVehicles()
    {
        if (!EditorUtility.DisplayDialog("Rebuild vehicle prefabs", "Replace the generated vehicle prefabs from the FBX LOD sources? Manual prefab edits will be lost.", "Rebuild", "Cancel")) return;
        CreateFolders();
        ConfigureImporters();
        GameBuilderAssetUtility.ConfigureTextureImporters();
        BuildVehiclePrefabs(true);
        BuildNetworkPrefabs(true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("RussoBalt/Rebuild Environment Prefabs")]
    public static void RebuildEnvironment()
    {
        if (!EditorUtility.DisplayDialog("Rebuild environment prefabs", "Replace the generated environment prefabs from the FBX LOD sources? Manual prefab edits will be lost.", "Rebuild", "Cancel")) return;
        CreateFolders();
        ConfigureImporters();
        GameBuilderAssetUtility.ConfigureTextureImporters();
        BuildEnvironmentPrefabs(true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("RussoBalt/Upgrade Existing Vehicle Materials")]
    public static void UpgradeVehicleMaterials()
    {
        CreateFolders();
        GameBuilderAssetUtility.ConfigureTextureImporters();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Vehicles" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("network_")) continue;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject instance = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameBuilderAssetUtility.ConvertMaterials(instance, "Vehicle");
                PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Existing vehicle materials upgraded without replacing prefab hierarchy");
    }

    private static void CreateFolders()
    {
        foreach (string folder in new[]
        {
            "Assets/Scripts", "Assets/Scripts/Core", "Assets/Scripts/Vehicle", "Assets/Scripts/Race", "Assets/Scripts/UI", "Assets/Scripts/Editor",
            "Assets/Resources/Vehicles", "Assets/Resources/Tracks", "Assets/Resources/Environment", "Assets/Resources/Environment/Profiles", "Assets/Resources/PreRace", "Assets/Resources/UI", "Assets/Resources/Networking", "Assets/Prefabs", "Assets/Materials", "Assets/Materials/Generated", "Assets/Shaders", "Assets/Shaders/Generated", "Assets/Scenes"
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
            GameBuilderAssetUtility.ConfigureModelImporter(assetPath, path.Contains("Characters"));
        }
    }

    public static void ConfigurePreRaceImporter()
    {
        GameBuilderAssetUtility.ConfigureModelImporter("Assets/Resources/PreRace/PreRace_Paddock.fbx", false);
    }

    public static void ConfigureUIImageImporters()
    {
        foreach (string folder in new[] { "Assets/Resources/UI", "Assets/UI/Images" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool changed = false;
                changed |= SetIfChanged(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
                changed |= SetIfChanged(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
                changed |= SetIfChanged(importer.maxTextureSize, 2048, value => importer.maxTextureSize = value);
                changed |= SetIfChanged(importer.textureCompression, TextureImporterCompression.CompressedHQ, value => importer.textureCompression = value);
                if (changed) importer.SaveAndReimport();
            }
        }
    }

    public static void BuildVehiclePrefabs(bool replaceExisting = false)
    {
        BuildVehicle("russo_balt_c24_55", "RussoBalt_C24_55", "RB", 1.365f, 3.20f, 0.44f, 1300f, 210f, 1.63f, 4.63f, 1.41f, replaceExisting);
        BuildVehicle("benz_1912_racer_concept", "Benz_1912_Racer_Concept", "BENZ", 1.40f, 3.35f, 0.46f, 1450f, 380f, 1.67f, 4.86f, 1.46f, replaceExisting);
        BuildVehicle("ford_model_t_1912", "Ford_ModelT_1912", "Ford_ModelT_1912", 1.43f, 2.54f, 0.381f, 544f, 150f, 1.73f, 3.42f, 1.95f, replaceExisting);
        BuildVehicle("fiat_s76_1910", "Fiat_S76_1910", "Fiat_S76_1910", 1.30f, 3.70f, 0.46f, 1650f, 520f, 1.30f, 3.75f, 1.75f, replaceExisting);
        BuildVehicle("mercedes_37_90_1912", "Mercedes_37_90_1912", "Mercedes_37_90_1912", 1.55f, 3.45f, 0.46f, 1950f, 340f, 1.75f, 4.95f, 1.75f, replaceExisting);
    }

    private static void BuildVehicle(string id, string fileName, string prefix, float trackWidth, float wheelbase, float radius, float mass, float torque, float width, float length, float height, bool replaceExisting)
    {
        string destination = $"Assets/Resources/Vehicles/{id}.prefab";
        if (!replaceExisting && AssetDatabase.LoadAssetAtPath<GameObject>(destination) != null) return;
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
        instance.name = id + "_Model_LOD0";
        GameObject wrapper = new GameObject(id);
        instance.transform.SetParent(wrapper.transform, true);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;
        List<GameObject> models = new List<GameObject> { instance };
        for (int lod = 1; lod <= 2; lod++)
        {
            GameObject lodSource = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath.Replace("_LOD0.fbx", $"_LOD{lod}.fbx"));
            if (lodSource == null) continue;
            GameObject model = UnityEngine.Object.Instantiate(lodSource);
            model.name = id + "_Model_LOD" + lod;
            model.transform.SetParent(wrapper.transform, true);
            model.transform.localPosition = instance.transform.localPosition;
            model.transform.localRotation = instance.transform.localRotation;
            model.transform.localScale = instance.transform.localScale;
            models.Add(model);
        }

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
        VehicleEffects1912 effects = wrapper.AddComponent<VehicleEffects1912>();
        effects.vehicle = controller;
        effects.dustSocket = GameBuilderAssetUtility.CreateSocket(wrapper, "DustTrail", new Vector3(0f, 0.24f, -wheelbase * 0.52f));
        effects.leftExhaustSocket = GameBuilderAssetUtility.CreateSocket(wrapper, "ExhaustSmoke_Left", new Vector3(-0.43f, 0.42f, -length * 0.38f));
        effects.rightExhaustSocket = GameBuilderAssetUtility.CreateSocket(wrapper, "ExhaustSmoke_Right", new Vector3(0.43f, 0.42f, -length * 0.38f));
        effects.leftHeadlampSocket = GameBuilderAssetUtility.CreateSocket(wrapper, "Headlamp_Left", new Vector3(-trackWidth * 0.38f, height * 0.55f, length * 0.44f));
        effects.rightHeadlampSocket = GameBuilderAssetUtility.CreateSocket(wrapper, "Headlamp_Right", new Vector3(trackWidth * 0.38f, height * 0.55f, length * 0.44f));

        foreach (GameObject model in models) GameBuilderAssetUtility.ConvertMaterials(model, "Vehicle");
        float[] transitions = models.Count == 3 ? new[] { 0.28f, 0.09f, 0.025f } : models.Count == 2 ? new[] { 0.32f, 0.08f } : new[] { 1f };
        GameBuilderAssetUtility.AddLODGroup(wrapper, models.ToArray(), transitions);
        PrefabUtility.SaveAsPrefabAsset(wrapper, destination);
        UnityEngine.Object.DestroyImmediate(wrapper);
        AssetDatabase.SaveAssets();
        Debug.Log($"Built vehicle prefab {id} with {models.Count} LODs");
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

    public static void BuildNetworkPrefabs(bool replaceExisting = false)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/russo_balt_c24_55.prefab");
        if (source == null) return;
        string networkPath = "Assets/Resources/Vehicles/network_russo_balt.prefab";
        if (replaceExisting || AssetDatabase.LoadAssetAtPath<GameObject>(networkPath) == null)
        {
            GameObject instance = UnityEngine.Object.Instantiate(source);
            instance.name = "NetworkPlayerCar";
            if (instance.GetComponent<NetworkObject>() == null) instance.AddComponent<NetworkObject>();
            NetworkTransform transform = instance.GetComponent<NetworkTransform>();
            if (transform == null) transform = instance.AddComponent<NetworkTransform>();
            transform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            transform.Interpolate = true;
            transform.SyncScaleX = false;
            transform.SyncScaleY = false;
            transform.SyncScaleZ = false;
            if (instance.GetComponent<NetworkPlayerCar>() == null) instance.AddComponent<NetworkPlayerCar>();
            PrefabUtility.SaveAsPrefabAsset(instance, networkPath);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        string lobbyPath = "Assets/Resources/Networking/LobbyManager.prefab";
        if (replaceExisting || AssetDatabase.LoadAssetAtPath<GameObject>(lobbyPath) == null)
        {
            GameObject lobby = new GameObject("LobbyManager");
            lobby.AddComponent<NetworkObject>();
            lobby.AddComponent<LobbyManager>();
            PrefabUtility.SaveAsPrefabAsset(lobby, lobbyPath);
            UnityEngine.Object.DestroyImmediate(lobby);
        }
    }

    private static void BuildEnvironmentPrefabs(bool replaceExisting = false)
    {
        BuildEnvironmentPrefab("Building_Wood_House_01", "House", replaceExisting);
        BuildEnvironmentPrefab("Tree_Birch_01", "Birch", replaceExisting);
        BuildEnvironmentPrefab("Tree_Pine_01", "Pine", replaceExisting);
        BuildEnvironmentPrefab("Signpost_Russia_1912", "Sign", replaceExisting);
        BuildEnvironmentPrefab("Prop_Horse_Cart_01", "Cart", replaceExisting);
    }

    private static void BuildEnvironmentPrefab(string fileName, string resourceName, bool replaceExisting)
    {
        string destination = $"Assets/Resources/Environment/{resourceName}.prefab";
        if (!replaceExisting && AssetDatabase.LoadAssetAtPath<GameObject>(destination) != null) return;
        string path = $"{EnvironmentRoot}/{fileName}_LOD0.fbx";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) return;
        GameObject instance = UnityEngine.Object.Instantiate(source);
        GameObject wrapper = new GameObject(resourceName);
        instance.transform.SetParent(wrapper.transform, true);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;
        List<GameObject> models = new List<GameObject> { instance };
        GameObject lodSource = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentRoot}/{fileName}_LOD1.fbx");
        if (lodSource != null)
        {
            GameObject lod = UnityEngine.Object.Instantiate(lodSource);
            lod.transform.SetParent(wrapper.transform, true);
            lod.transform.localPosition = instance.transform.localPosition;
            lod.transform.localRotation = instance.transform.localRotation;
            lod.transform.localScale = instance.transform.localScale;
            models.Add(lod);
        }
        foreach (GameObject model in models) GameBuilderAssetUtility.ConvertMaterials(model, "Vehicle");
        if (models.Count > 1) GameBuilderAssetUtility.AddLODGroup(wrapper, models.ToArray(), new[] { 0.35f, 0.08f });
        PrefabUtility.SaveAsPrefabAsset(wrapper, destination);
        UnityEngine.Object.DestroyImmediate(wrapper);
    }

    public static void BuildPreRacePrefab(bool replaceExisting = false)
    {
        string destination = "Assets/Resources/PreRace/PreRace_Paddock.prefab";
        if (!replaceExisting && AssetDatabase.LoadAssetAtPath<GameObject>(destination) != null) return;
        string sourcePath = "Assets/Resources/PreRace/PreRace_Paddock.fbx";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) return;
        GameObject instance = UnityEngine.Object.Instantiate(source);
        instance.name = "PreRace_Paddock_Model";
        GameObject wrapper = new GameObject("PreRace_Paddock");
        instance.transform.SetParent(wrapper.transform, true);
        wrapper.transform.position = Vector3.zero;
        wrapper.transform.rotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;
        GameBuilderAssetUtility.ConvertMaterials(instance, "PreRace");
        PrefabUtility.SaveAsPrefabAsset(wrapper, destination);
        UnityEngine.Object.DestroyImmediate(wrapper);
        AssetDatabase.SaveAssets();
    }

    private static bool SetIfChanged<T>(T current, T desired, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired)) return false;
        setter(desired);
        return true;
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
        string path = $"Assets/Resources/Tracks/{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<TrackData>(path) != null) return;
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
        AssetDatabase.CreateAsset(track, path);
    }

    private static void CreateEnvironmentProfiles()
    {
        foreach (string id in new[] { "winter_departure", "alps_pass", "monaco_finish", "moscow_highway", "finland_coast" })
        {
            string path = $"Assets/Resources/Environment/Profiles/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<TrackEnvironmentProfile>(path) != null) continue;
            TrackEnvironmentProfile profile = TrackEnvironmentProfile.CreatePreset(id);
            AssetDatabase.CreateAsset(profile, path);
        }
    }

    public static void CreateScenes()
    {
        CreateMenuScene("MainMenu", "MainMenuUI");
        CreateMenuScene("Garage", "GarageUI");
        CreatePreRaceScene();
        CreateLobbyScene();
        CreateRaceScene("Race_Winter");
        CreateRaceScene("Race_Alps");
        CreateRaceScene("Race_Monaco");
        CreateRaceScene("Race_Moscow");
        CreateRaceScene("Race_Finland");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
    }

    private static Scene OpenOrCreateScene(string sceneName)
    {
        string path = $"Assets/Scenes/{sceneName}.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static GameObject EnsureGameObject(Scene scene, string name)
    {
        GameObject gameObject = scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
        return gameObject != null ? gameObject : new GameObject(name);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void CreateMenuScene(string sceneName, string uiType)
    {
        Scene scene = OpenOrCreateScene(sceneName);
        bool newScene = scene.GetRootGameObjects().Length == 0;
        GameBuilderAssetUtility.ConfigureSceneRendering(scene, false);
        if (newScene && Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0f, 2f, -8f);
            Camera.main.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        }
        EnsureComponent<GameManager>(EnsureGameObject(scene, "GameManager"));
        GameObject ui = EnsureGameObject(scene, "UI");
        if (uiType == "MainMenuUI") EnsureComponent<MainMenuUI>(ui);
        else EnsureComponent<GarageUI>(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneName}.unity");
    }

    public static void CreatePreRaceScene()
    {
        Scene scene = OpenOrCreateScene("PreRace");
        bool newScene = scene.GetRootGameObjects().Length == 0;
        GameBuilderAssetUtility.ConfigureSceneRendering(scene, false);
        if (newScene && Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(4f, 2.55f, 6.4f);
            Camera.main.transform.LookAt(new Vector3(-0.55f, 0.9f, 0.35f));
        }
        EnsureComponent<GameManager>(EnsureGameObject(scene, "GameManager"));
        EnsureComponent<PreRaceController>(EnsureGameObject(scene, "PreRace"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PreRace.unity");
    }

    private static void CreateLobbyScene()
    {
        Scene scene = OpenOrCreateScene("Lobby");
        bool newScene = scene.GetRootGameObjects().Length == 0;
        GameBuilderAssetUtility.ConfigureSceneRendering(scene, false);
        if (newScene && Camera.main != null) Camera.main.transform.position = new Vector3(0f, 2f, -8f);
        EnsureComponent<GameManager>(EnsureGameObject(scene, "GameManager"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Lobby.unity");
    }

    private static void CreateRaceScene(string sceneName)
    {
        Scene scene = OpenOrCreateScene(sceneName);
        GameBuilderAssetUtility.ConfigureSceneRendering(scene, true);
        EnsureComponent<GameManager>(EnsureGameObject(scene, "GameManager"));
        EnsureComponent<RaceManager>(EnsureGameObject(scene, "RaceManager"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneName}.unity");
    }

    public static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "RussoBalt The Road";
        PlayerSettings.productName = "RussoBalt The Road";
        PlayerSettings.bundleVersion = "0.6.0";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.russobalt.theroad");
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes != null ? EditorBuildSettings.scenes.ToList() : new List<EditorBuildSettingsScene>();
        HashSet<string> existingPaths = new HashSet<string>(scenes.Where(s => s.enabled).Select(s => s.path));
        foreach (string path in new[]
        {
            "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Garage.unity", "Assets/Scenes/PreRace.unity", "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/Race_Winter.unity", "Assets/Scenes/Race_Alps.unity", "Assets/Scenes/Race_Monaco.unity",
            "Assets/Scenes/Race_Moscow.unity", "Assets/Scenes/Race_Finland.unity"
        })
        {
            if (existingPaths.Add(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
