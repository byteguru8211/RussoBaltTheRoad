#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DriverPrefabInstaller
{
    private const string DriverModelPath = "Assets/GameAssets/Characters/Driver_1912.fbx";
    private const string DriverPrefabPath = "Assets/Resources/Characters/Driver_1912.prefab";

    [MenuItem("RussoBalt/Install Detailed Driver")]
    public static void InstallDetailedDriver()
    {
        AssetDatabase.Refresh();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(DriverModelPath);
        if (model == null)
        {
            Debug.LogError("Driver model not found: " + DriverModelPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Driver_1912";
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Transform rig = FindTransform(instance.transform, "CHR_Driver_1912_Rig");
        GameObject controllerOwner = rig != null ? rig.gameObject : instance;
        DriverRigController controller = controllerOwner.GetComponent<DriverRigController>();
        if (controller == null)
            controller = controllerOwner.AddComponent<DriverRigController>();
        controller.rigRoot = rig;
        controller.animator = animator;
        controller.hideHeadInCockpit = true;
        controller.useSeatAnchor = true;
        controller.leftGripLocal = new Vector3(-0.22f, 0f, 0f);
        controller.rightGripLocal = new Vector3(0.22f, 0f, 0f);
        controller.leftElbowPoleLocal = new Vector3(-0.85f, -0.3f, 0.2f);
        controller.rightElbowPoleLocal = new Vector3(0.85f, -0.3f, 0.2f);
        controller.leftKneeLocal = new Vector3(-0.17f, 0.64f, -0.22f);
        controller.rightKneeLocal = new Vector3(0.17f, 0.64f, -0.22f);
        controller.leftFootLocal = new Vector3(-0.18f, 0.24f, 0.24f);
        controller.rightFootLocal = new Vector3(0.18f, 0.24f, 0.24f);

        Directory.CreateDirectory(Path.GetDirectoryName(DriverPrefabPath));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, DriverPrefabPath);
        Object.DestroyImmediate(instance);
        if (prefab == null)
        {
            Debug.LogError("Failed to save driver prefab");
            return;
        }

        CreateCharacterPrefab("Assets/GameAssets/Characters/Mechanic_1912.fbx", "Assets/Resources/Characters/Mechanic_1912.prefab", "Mechanic_1912");
        CreateCharacterPrefab("Assets/GameAssets/Characters/Lady_1912.fbx", "Assets/Resources/Characters/Lady_1912.prefab", "Lady_1912");

        string[] vehicles =
        {
            "Assets/Resources/Vehicles/russo_balt_c24_55.prefab",
            "Assets/Resources/Vehicles/fiat_s76_1910.prefab",
            "Assets/Resources/Vehicles/ford_model_t_1912.prefab",
            "Assets/Resources/Vehicles/mercedes_37_90_1912.prefab",
            "Assets/Resources/Vehicles/benz_1912_racer_concept.prefab",
            "Assets/Resources/Vehicles/network_russo_balt.prefab"
        };

        foreach (string path in vehicles)
        {
            if (!File.Exists(path))
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                continue;
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name.StartsWith("Driver_1912"))
                    Object.DestroyImmediate(child.gameObject);
            }

            Transform steering = FindTransform(root.transform, "SteeringWheel_Pivot");
            Vector3 seat = steering != null
                ? root.transform.InverseTransformPoint(steering.position) + new Vector3(-0.04f, -0.72f, -0.42f)
                : new Vector3(0.12f, 0.42f, -0.72f);
            GameObject driver = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            driver.name = "Driver_1912";
            driver.transform.SetParent(root.transform, false);
            driver.transform.localPosition = seat;
            driver.transform.localRotation = Quaternion.identity;
            driver.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
            Transform existingSeat = FindTransform(root.transform, "RB_DriverSeat");
            if (existingSeat != null)
                Object.DestroyImmediate(existingSeat.gameObject);
            GameObject seatObject = new GameObject("RB_DriverSeat");
            seatObject.transform.SetParent(root.transform, false);
            seatObject.transform.localPosition = seat;
            seatObject.transform.localRotation = Quaternion.identity;
            DriverRigController driverController = driver.GetComponentInChildren<DriverRigController>();
            if (driverController != null)
            {
                driverController.seatAnchor = seatObject.transform;
                driverController.useSeatAnchor = true;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Detailed driver installed in vehicle prefabs");
    }

    private static void CreateCharacterPrefab(string modelPath, string prefabPath, string objectName)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
            return;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = objectName;
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
    }

    private static Transform FindTransform(Transform root, string namePart)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.Contains(namePart))
                return child;
        return null;
    }
}
#endif
