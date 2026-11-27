#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VehicleReleaseFixup
{
    private sealed class Mount
    {
        public Vector3 axis;
        public float rimPitch;
        public Vector3 chase;
        public Vector3 chaseLook;
        public Vector3 hood;
        public Vector3 hoodLook;
        public Vector3 cockpit;
        public Vector3 cockpitLook;
        public float orbit;
    }

    private static readonly Mount RussoBalt = new Mount
    {
        axis = new Vector3(0f, 0.777f, 0.630f), rimPitch = -50.9f,
        chase = new Vector3(0f, 2.65f, -6.5f), chaseLook = new Vector3(0f, 0.95f, 2.1f),
        hood = new Vector3(0.31f, 1.58f, 0.62f), hoodLook = new Vector3(0.31f, 1.36f, 14f),
        cockpit = new Vector3(0.27f, 1.55f, 0.10f), cockpitLook = new Vector3(0.27f, 1.38f, 15f), orbit = 7.6f
    };

    private static readonly Mount Benz = new Mount
    {
        axis = new Vector3(0f, 0.910f, 0.414f), rimPitch = -65.5f,
        chase = new Vector3(0f, 2.85f, -7.3f), chaseLook = new Vector3(0f, 1.0f, 2.2f),
        hood = new Vector3(-0.29f, 1.62f, 0.78f), hoodLook = new Vector3(-0.29f, 1.40f, 15f),
        cockpit = new Vector3(-0.33f, 1.58f, 0.05f), cockpitLook = new Vector3(-0.33f, 1.40f, 15f), orbit = 8.2f
    };

    private static readonly Mount Ford = new Mount
    {
        axis = new Vector3(0f, 0.674f, 0.739f), rimPitch = -42.3f,
        chase = new Vector3(0f, 2.45f, -5.9f), chaseLook = new Vector3(0f, 0.95f, 1.8f),
        hood = new Vector3(0.24f, 1.58f, 0.52f), hoodLook = new Vector3(0.24f, 1.36f, 13f),
        cockpit = new Vector3(0.20f, 1.62f, -0.05f), cockpitLook = new Vector3(0.20f, 1.42f, 14f), orbit = 6.8f
    };

    private static readonly Mount Fiat = new Mount
    {
        axis = new Vector3(0f, 0.910f, 0.414f), rimPitch = -65.5f,
        chase = new Vector3(0f, 2.95f, -7.7f), chaseLook = new Vector3(0f, 1.05f, 2.4f),
        hood = new Vector3(0.23f, 1.72f, 0.82f), hoodLook = new Vector3(0.23f, 1.48f, 16f),
        cockpit = new Vector3(0.19f, 1.66f, -0.12f), cockpitLook = new Vector3(0.19f, 1.46f, 16f), orbit = 8.5f
    };

    private static readonly Mount Mercedes = new Mount
    {
        axis = new Vector3(0f, 0.910f, 0.414f), rimPitch = -65.5f,
        chase = new Vector3(0f, 2.90f, -7.4f), chaseLook = new Vector3(0f, 1.02f, 2.2f),
        hood = new Vector3(0.27f, 1.70f, 0.74f), hoodLook = new Vector3(0.27f, 1.47f, 15f),
        cockpit = new Vector3(0.23f, 1.66f, -0.08f), cockpitLook = new Vector3(0.23f, 1.46f, 15f), orbit = 8.2f
    };

    [MenuItem("RussoBalt/Apply Vehicle Release Fixes")]
    public static void Apply()
    {
        ApplyDriverPrefab("Assets/Resources/Characters/Driver_1912.prefab");
        ApplyVehicle("Assets/Resources/Vehicles/russo_balt_c24_55.prefab", RussoBalt);
        ApplyVehicle("Assets/Resources/Vehicles/benz_1912_racer_concept.prefab", Benz);
        ApplyVehicle("Assets/Resources/Vehicles/ford_model_t_1912.prefab", Ford);
        ApplyVehicle("Assets/Resources/Vehicles/fiat_s76_1910.prefab", Fiat);
        ApplyVehicle("Assets/Resources/Vehicles/mercedes_37_90_1912.prefab", Mercedes);
        ApplyVehicle("Assets/Resources/Vehicles/network_russo_balt.prefab", RussoBalt);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vehicle release fixes applied");
    }

    private static void ApplyDriverPrefab(string path)
    {
        if (!File.Exists(path)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        DriverRigController[] controllers = root.GetComponentsInChildren<DriverRigController>(true);
        foreach (DriverRigController controller in controllers) ConfigureDriver(controller, root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ApplyVehicle(string path, Mount mount)
    {
        if (!File.Exists(path)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        VehicleController1912 controller = root.GetComponent<VehicleController1912>();
        if (controller != null)
        {
            controller.frontLeftSteerVisual = FindWheel(root.transform, true, true, true);
            controller.frontRightSteerVisual = FindWheel(root.transform, true, false, true);
            controller.frontLeftSpinVisual = FindWheel(root.transform, true, true, false);
            controller.frontRightSpinVisual = FindWheel(root.transform, true, false, false);
            controller.rearLeftSpinVisual = FindWheel(root.transform, false, true, false);
            controller.rearRightSpinVisual = FindWheel(root.transform, false, false, false);
            controller.steeringWheelAxis = mount.axis;
            controller.cameraChasePosition = mount.chase;
            controller.cameraChaseLookAt = mount.chaseLook;
            controller.cameraHoodPosition = mount.hood;
            controller.cameraHoodLookAt = mount.hoodLook;
            controller.cameraCockpitPosition = mount.cockpit;
            controller.cameraCockpitLookAt = mount.cockpitLook;
            controller.cameraOrbitDistance = mount.orbit;
            controller.engineRunning = true;
            controller.handCrankTimer = 0f;
            controller.rpm = controller.idleRpm;
            controller.temperature = 72f;

            Transform steering = FindContains(root.transform, "SteeringWheel_Pivot");
            if (steering != null)
            {
                controller.steeringWheelVisual = steering;
                Transform rim = FindContains(steering, "Rim");
                if (rim == null) rim = FindContains(steering, "SteeringWheel");
                if (rim != null && rim != steering)
                {
                    Vector3 euler = rim.localEulerAngles;
                    rim.localRotation = Quaternion.Euler(mount.rimPitch, euler.y, euler.z);
                }
            }
        }

        DriverRigController[] drivers = root.GetComponentsInChildren<DriverRigController>(true);
        foreach (DriverRigController driver in drivers) ConfigureDriver(driver, root.transform);

        VehicleEffects1912 effects = root.GetComponent<VehicleEffects1912>();
        if (effects != null)
        {
            BoxCollider hull = root.GetComponent<BoxCollider>();
            float length = hull != null ? hull.size.z : 4.2f;
            float rear = -length * 0.43f;
            float rearAxle = controller != null && controller.rearLeft != null ? controller.rearLeft.transform.localPosition.z : rear;
            effects.dustSocket = EnsureSocket(root.transform, "DustTrail", new Vector3(0f, 0.16f, rearAxle));
            effects.leftExhaustSocket = EnsureSocket(root.transform, "ExhaustSmoke_Left", new Vector3(-0.40f, 0.38f, rear));
            effects.rightExhaustSocket = EnsureSocket(root.transform, "ExhaustSmoke_Right", new Vector3(0.40f, 0.38f, rear));
            effects.leftHeadlampSocket = EnsureSocket(root.transform, "Headlamp_Left", new Vector3(-0.52f, 0.78f, length * 0.44f));
            effects.rightHeadlampSocket = EnsureSocket(root.transform, "Headlamp_Right", new Vector3(0.52f, 0.78f, length * 0.44f));
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ConfigureDriver(DriverRigController driver, Transform root)
    {
        driver.useSeatAnchor = true;
        driver.hideHeadInCockpit = true;
        driver.leftGripLocal = new Vector3(-0.22f, 0f, 0f);
        driver.rightGripLocal = new Vector3(0.22f, 0f, 0f);
        driver.leftElbowPoleLocal = new Vector3(-0.85f, -0.3f, 0.2f);
        driver.rightElbowPoleLocal = new Vector3(0.85f, -0.3f, 0.2f);
        driver.leftKneeLocal = new Vector3(-0.17f, 0.64f, -0.22f);
        driver.rightKneeLocal = new Vector3(0.17f, 0.64f, -0.22f);
        driver.leftFootLocal = new Vector3(-0.18f, 0.24f, 0.24f);
        driver.rightFootLocal = new Vector3(0.18f, 0.24f, 0.24f);
        driver.leftHandTarget = null;
        driver.rightHandTarget = null;
        driver.leftKneeTarget = null;
        driver.rightKneeTarget = null;
        driver.leftFootTarget = null;
        driver.rightFootTarget = null;
        Transform head = FindContains(driver.transform, "Head");
        if (head != null)
        {
            driver.headRenderer = head.GetComponent<Renderer>();
            if (driver.headRenderer == null) driver.headRenderer = head.GetComponentInChildren<Renderer>(true);
        }
        if (driver.headRenderer == null)
        {
            foreach (Renderer renderer in driver.GetComponentsInChildren<Renderer>(true))
                if (renderer is SkinnedMeshRenderer) { driver.headRenderer = renderer; break; }
        }
        if (driver.vehicle == null) driver.vehicle = root.GetComponent<VehicleController1912>();
        if (driver.seatAnchor == null) driver.seatAnchor = FindContains(root, "RB_DriverSeat");
    }

    private static Transform FindWheel(Transform root, bool front, bool left, bool steer)
    {
        string suffix = steer ? "_steer" : "_spin";
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string name = child.name;
            if (name.IndexOf("wheel_", System.StringComparison.OrdinalIgnoreCase) < 0
                || name.IndexOf("spare", System.StringComparison.OrdinalIgnoreCase) >= 0
                || !name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                continue;
            Vector3 local = root.InverseTransformPoint(child.position);
            if ((local.z >= 0f) == front && (local.x < 0f) == left) return child;
        }
        return null;
    }

    private static Transform EnsureSocket(Transform root, string name, Vector3 position)
    {
        Transform existing = FindContains(root, name);
        if (existing != null) return existing;
        GameObject socket = new GameObject(name);
        socket.transform.SetParent(root, false);
        socket.transform.localPosition = position;
        return socket.transform;
    }

    private static Transform FindContains(Transform root, string namePart)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.Contains(namePart)) return child;
        return null;
    }
}
#endif
