using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural seated-driver pose for the project's Generic rigs. The authored bone names are
/// Root, Pelvis, Spine, Chest, Neck, Head, Clavicle.L/R, UpperArm.L/R, Forearm.L/R, Hand.L/R,
/// Thigh.L/R, Shin.L/R and Foot.L/R. Mixamo and Humanoid aliases are also resolved.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class DriverRigController : MonoBehaviour
{
    public static readonly string[] ExpectedBoneNames =
    {
        "Root", "Pelvis", "Spine", "Chest", "Neck", "Head",
        "Clavicle.L", "Clavicle.R", "UpperArm.L", "UpperArm.R",
        "Forearm.L", "Forearm.R", "Hand.L", "Hand.R",
        "Thigh.L", "Thigh.R", "Shin.L", "Shin.R", "Foot.L", "Foot.R"
    };

    [Header("Vehicle and animation")]
    public VehicleController1912 vehicle;
    public VehicleInput1912 input;
    public Animator animator;
    public Transform rigRoot;

    [Header("Authored bones (auto-resolved when left empty)")]
    public Transform root;
    public Transform pelvis;
    public Transform spine;
    public Transform chest;
    public Transform neck;
    public Transform head;
    public Transform leftClavicle;
    public Transform rightClavicle;
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftForearm;
    public Transform rightForearm;
    public Transform leftHand;
    public Transform rightHand;
    public Transform leftThigh;
    public Transform rightThigh;
    public Transform leftShin;
    public Transform rightShin;
    public Transform leftFoot;
    public Transform rightFoot;

    [Header("Vehicle anchors")]
    public Transform seatAnchor;
    public Transform steeringWheel;
    public Transform gearLever;
    public bool useSeatAnchor = true;

    [Header("Optional IK targets")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public Transform leftKneeTarget;
    public Transform rightKneeTarget;
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Seated pose")]
    public Vector3 boneAxis = Vector3.up;
    public Vector3 leftGripLocal = new Vector3(0.22f, 0f, 0f);
    public Vector3 rightGripLocal = new Vector3(-0.22f, 0f, 0f);
    public Vector3 leftElbowPoleLocal = new Vector3(0.85f, -0.3f, 0.2f);
    public Vector3 rightElbowPoleLocal = new Vector3(-0.85f, -0.3f, 0.2f);
    public Vector3 leftKneeLocal = new Vector3(0.17f, 0.56f, 0.42f);
    public Vector3 rightKneeLocal = new Vector3(-0.17f, 0.56f, 0.42f);
    public Vector3 leftFootLocal = new Vector3(0.18f, 0.2f, 0.82f);
    public Vector3 rightFootLocal = new Vector3(-0.18f, 0.2f, 0.82f);
    public Vector3 gearGripLocal = new Vector3(0f, 0.12f, 0f);
    public float armResponse = 12f;
    public float bodyResponse = 6f;
    public float shiftDuration = 0.42f;
    public float maximumSteerAngle = 30f;

    [Header("First-person head")]
    public Renderer headRenderer;
    public bool hideHeadInCockpit = true;

    private Quaternion pelvisRest;
    private Quaternion spineRest;
    private Quaternion chestRest;
    private Quaternion neckRest;
    private Quaternion headRest;
    private Quaternion leftClavicleRest;
    private Quaternion rightClavicleRest;
    private Quaternion gearLeverRest;
    private Vector3 previousVelocity;
    private Vector3 leftHandPosition;
    private Vector3 rightHandPosition;
    private bool handPositionsInitialized;
    private int lastGear;
    private float shiftTimer;

    public bool CanPoseArms => leftUpperArm != null && leftForearm != null && leftHand != null
        && rightUpperArm != null && rightForearm != null && rightHand != null;
    public bool CanPoseLegs => leftThigh != null && leftShin != null && leftFoot != null
        && rightThigh != null && rightShin != null && rightFoot != null;

    private void Awake()
    {
        Rebind();
        lastGear = vehicle != null ? vehicle.gear : 0;
        previousVelocity = Vector3.zero;
        if (vehicle != null)
        {
            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            if (body != null)
                previousVelocity = body.linearVelocity;
        }
        if (animator != null)
            animator.applyRootMotion = false;
    }

    public void Rebind()
    {
        if (vehicle == null)
            vehicle = GetComponentInParent<VehicleController1912>();
        if (input == null)
            input = GetComponentInParent<VehicleInput1912>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            animator = GetComponentInParent<Animator>();
        if (rigRoot == null)
            rigRoot = transform;

        Dictionary<string, Transform> rigBones = BuildTransformMap(rigRoot);
        root = ResolveBone(root, rigBones, "Root", "mixamorig:Hips", HumanBodyBones.Hips);
        pelvis = ResolveBone(pelvis, rigBones, "Pelvis", "mixamorig:Hips", HumanBodyBones.Hips);
        spine = ResolveBone(spine, rigBones, "Spine", "mixamorig:Spine", HumanBodyBones.Spine);
        chest = ResolveBone(chest, rigBones, "Chest", "mixamorig:Spine1", HumanBodyBones.Chest);
        neck = ResolveBone(neck, rigBones, "Neck", "mixamorig:Neck", HumanBodyBones.Neck);
        head = ResolveBone(head, rigBones, "Head", "mixamorig:Head", HumanBodyBones.Head);
        leftClavicle = ResolveBone(leftClavicle, rigBones, "Clavicle.L", "mixamorig:LeftShoulder", HumanBodyBones.LeftShoulder);
        rightClavicle = ResolveBone(rightClavicle, rigBones, "Clavicle.R", "mixamorig:RightShoulder", HumanBodyBones.RightShoulder);
        leftUpperArm = ResolveBone(leftUpperArm, rigBones, "UpperArm.L", "mixamorig:LeftArm", HumanBodyBones.LeftUpperArm);
        rightUpperArm = ResolveBone(rightUpperArm, rigBones, "UpperArm.R", "mixamorig:RightArm", HumanBodyBones.RightUpperArm);
        leftForearm = ResolveBone(leftForearm, rigBones, "Forearm.L", "mixamorig:LeftForeArm", HumanBodyBones.LeftLowerArm);
        rightForearm = ResolveBone(rightForearm, rigBones, "Forearm.R", "mixamorig:RightForeArm", HumanBodyBones.RightLowerArm);
        leftHand = ResolveBone(leftHand, rigBones, "Hand.L", "mixamorig:LeftHand", HumanBodyBones.LeftHand);
        rightHand = ResolveBone(rightHand, rigBones, "Hand.R", "mixamorig:RightHand", HumanBodyBones.RightHand);
        leftThigh = ResolveBone(leftThigh, rigBones, "Thigh.L", "mixamorig:LeftUpLeg", HumanBodyBones.LeftUpperLeg);
        rightThigh = ResolveBone(rightThigh, rigBones, "Thigh.R", "mixamorig:RightUpLeg", HumanBodyBones.RightUpperLeg);
        leftShin = ResolveBone(leftShin, rigBones, "Shin.L", "mixamorig:LeftLeg", HumanBodyBones.LeftLowerLeg);
        rightShin = ResolveBone(rightShin, rigBones, "Shin.R", "mixamorig:RightLeg", HumanBodyBones.RightLowerLeg);
        leftFoot = ResolveBone(leftFoot, rigBones, "Foot.L", "mixamorig:LeftFoot", HumanBodyBones.LeftFoot);
        rightFoot = ResolveBone(rightFoot, rigBones, "Foot.R", "mixamorig:RightFoot", HumanBodyBones.RightFoot);

        if (vehicle != null)
        {
            Dictionary<string, Transform> vehicleParts = BuildTransformMap(vehicle.transform);
            if (steeringWheel == null)
                steeringWheel = FindContains(vehicleParts, "SteeringWheel_Pivot") ?? vehicle.steeringWheelVisual;
            if (gearLever == null)
                gearLever = FindContains(vehicleParts, "GearLever_Pivot");
            if (seatAnchor == null)
                seatAnchor = FindExact(vehicleParts, "RB_DriverSeat") ?? FindExact(vehicleParts, "DriverSeat") ?? FindExact(vehicleParts, "SeatAnchor");
            if (useSeatAnchor && seatAnchor != null && !rigRoot.IsChildOf(vehicle.transform))
            {
                rigRoot.position = seatAnchor.position;
                rigRoot.rotation = seatAnchor.rotation;
            }
        }

        if (pelvis != null) pelvisRest = pelvis.localRotation;
        if (spine != null) spineRest = spine.localRotation;
        if (chest != null) chestRest = chest.localRotation;
        if (neck != null) neckRest = neck.localRotation;
        if (head != null) headRest = head.localRotation;
        if (leftClavicle != null) leftClavicleRest = leftClavicle.localRotation;
        if (rightClavicle != null) rightClavicleRest = rightClavicle.localRotation;
        if (gearLever != null) gearLeverRest = gearLever.localRotation;
        handPositionsInitialized = false;
    }

    private void LateUpdate()
    {
        if (vehicle == null)
            return;
        float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float steer = input != null ? input.SteeringVisualAngle : vehicle.frontLeft != null ? vehicle.frontLeft.steerAngle : 0f;
        steer = Mathf.Clamp(steer, -maximumSteerAngle, maximumSteerAngle);
        float steerNormalized = steer / Mathf.Max(1f, maximumSteerAngle);
        float speedRatio = Mathf.Clamp01(Mathf.Abs(vehicle.SpeedKmh) / 100f);

        if (vehicle.gear != lastGear)
        {
            lastGear = vehicle.gear;
            shiftTimer = shiftDuration;
        }
        shiftTimer = Mathf.Max(0f, shiftTimer - deltaTime);

        ApplyBodyMotion(deltaTime, steerNormalized, speedRatio);
        if (CanPoseLegs)
            ApplySeatedLegs();
        if (CanPoseArms && (steeringWheel != null || (leftHandTarget != null && rightHandTarget != null)))
            ApplyArms(deltaTime, steerNormalized, speedRatio);
        ApplyGearLever(deltaTime);
        UpdateHeadVisibility();
    }

    private void ApplyBodyMotion(float deltaTime, float steer, float speedRatio)
    {
        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        Vector3 acceleration = (velocity - previousVelocity) / deltaTime;
        previousVelocity = velocity;
        float accelerationPitch = Mathf.Clamp(-Vector3.Dot(acceleration, vehicle.transform.forward) * 0.0025f, -0.055f, 0.055f);
        float roadMotion = vehicle.engineRunning || speedRatio > 0.02f
            ? Mathf.Sin(Time.unscaledTime * (5f + speedRatio * 9f)) * speedRatio * 0.012f
            : 0f;
        float engineVibration = vehicle.engineRunning ? Mathf.Sin(Time.unscaledTime * 29f) * 0.0025f : 0f;
        float response = 1f - Mathf.Exp(-bodyResponse * deltaTime);

        if (pelvis != null)
            pelvis.localRotation = Quaternion.Slerp(pelvis.localRotation, pelvisRest * Quaternion.Euler(accelerationPitch * 0.35f, steer * 0.025f, -steer * 0.035f + roadMotion * 0.4f), response);
        if (spine != null)
            spine.localRotation = Quaternion.Slerp(spine.localRotation, spineRest * Quaternion.Euler(accelerationPitch + roadMotion + engineVibration, -steer * 0.075f, -steer * 0.085f), response);
        if (chest != null)
            chest.localRotation = Quaternion.Slerp(chest.localRotation, chestRest * Quaternion.Euler(accelerationPitch * 0.4f + roadMotion * 0.5f, -steer * 0.11f, -steer * 0.12f), response);
        if (neck != null)
            neck.localRotation = Quaternion.Slerp(neck.localRotation, neckRest * Quaternion.Euler(-accelerationPitch * 0.25f, -steer * 0.16f, steer * 0.035f), response);
        if (head != null)
            head.localRotation = Quaternion.Slerp(head.localRotation, headRest * Quaternion.Euler(-0.025f - accelerationPitch * 0.2f + engineVibration, -steer * 0.22f, steer * 0.05f), response);
    }

    private void ApplySeatedLegs()
    {
        Vector3 leftFootPosition = leftFootTarget != null ? leftFootTarget.position : vehicle.transform.TransformPoint(leftFootLocal);
        Vector3 rightFootPosition = rightFootTarget != null ? rightFootTarget.position : vehicle.transform.TransformPoint(rightFootLocal);
        Vector3 leftPole = leftKneeTarget != null ? leftKneeTarget.position : vehicle.transform.TransformPoint(leftKneeLocal);
        Vector3 rightPole = rightKneeTarget != null ? rightKneeTarget.position : vehicle.transform.TransformPoint(rightKneeLocal);

        SolveLeg(leftThigh, leftShin, leftFoot, leftKneeTarget, leftFootPosition, leftPole);
        SolveLeg(rightThigh, rightShin, rightFoot, rightKneeTarget, rightFootPosition, rightPole);
        Vector3 leftToe = vehicle.transform.TransformPoint(new Vector3(0.18f, 0.16f, 1.04f));
        Vector3 rightToe = vehicle.transform.TransformPoint(new Vector3(-0.18f, 0.16f, 1.04f));
        AimBone(leftFoot, leftToe - leftFoot.position);
        AimBone(rightFoot, rightToe - rightFoot.position);
    }

    private void SolveLeg(Transform thigh, Transform shin, Transform foot, Transform kneeTarget, Vector3 footPosition, Vector3 pole)
    {
        if (kneeTarget != null)
        {
            AimBone(thigh, kneeTarget.position - thigh.position);
            AimBone(shin, footPosition - shin.position);
            return;
        }
        SolveTwoBone(thigh, shin, foot, footPosition, pole);
    }

    private void ApplyArms(float deltaTime, float steer, float speedRatio)
    {
        Vector3 leftTarget = leftHandTarget != null
            ? leftHandTarget.position
            : steeringWheel.TransformPoint(leftGripLocal);
        Vector3 rightTarget = rightHandTarget != null
            ? rightHandTarget.position
            : steeringWheel.TransformPoint(rightGripLocal);

        if (gearLever != null && shiftTimer > 0f)
        {
            float progress = 1f - shiftTimer / Mathf.Max(0.001f, shiftDuration);
            float reach = Mathf.Sin(progress * Mathf.PI);
            rightTarget = Vector3.Lerp(rightTarget, gearLever.TransformPoint(gearGripLocal), reach);
        }

        float response = 1f - Mathf.Exp(-armResponse * deltaTime);
        if (!handPositionsInitialized)
        {
            leftHandPosition = leftTarget;
            rightHandPosition = rightTarget;
            handPositionsInitialized = true;
        }
        leftHandPosition = Vector3.Lerp(leftHandPosition, leftTarget, response);
        rightHandPosition = Vector3.Lerp(rightHandPosition, rightTarget, response);
        Vector3 leftPole = chest != null ? chest.TransformDirection(leftElbowPoleLocal) + chest.position : vehicle.transform.TransformPoint(leftElbowPoleLocal);
        Vector3 rightPole = chest != null ? chest.TransformDirection(rightElbowPoleLocal) + chest.position : vehicle.transform.TransformPoint(rightElbowPoleLocal);

        SolveTwoBone(leftUpperArm, leftForearm, leftHand, leftHandPosition, leftPole);
        SolveTwoBone(rightUpperArm, rightForearm, rightHand, rightHandPosition, rightPole);
        Vector3 wheelCenter = steeringWheel != null ? steeringWheel.position : leftHandPosition + vehicle.transform.forward;
        AimBone(leftHand, wheelCenter - leftHand.position);
        AimBone(rightHand, (shiftTimer > 0f && gearLever != null ? gearLever.position : wheelCenter) - rightHand.position);

        if (leftClavicle != null)
            leftClavicle.localRotation = Quaternion.Slerp(leftClavicle.localRotation, leftClavicleRest * Quaternion.Euler(0f, 0f, -steer * 1.2f - speedRatio * 1.5f), response);
        if (rightClavicle != null)
            rightClavicle.localRotation = Quaternion.Slerp(rightClavicle.localRotation, rightClavicleRest * Quaternion.Euler(0f, 0f, steer * 1.2f - speedRatio * 1.5f), response);
    }

    private void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        Vector3 start = upper.position;
        float upperLength = Mathf.Max(0.001f, Vector3.Distance(start, lower.position));
        float lowerLength = Mathf.Max(0.001f, Vector3.Distance(lower.position, end.position));
        Vector3 direction = target - start;
        float distance = Mathf.Clamp(direction.magnitude, 0.001f, upperLength + lowerLength - 0.0005f);
        direction.Normalize();
        Vector3 bendDirection = pole - start;
        bendDirection -= direction * Vector3.Dot(bendDirection, direction);
        if (bendDirection.sqrMagnitude < 0.00001f)
            bendDirection = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.95f ? Vector3.forward : Vector3.up);
        bendDirection.Normalize();

        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 joint = start + direction * along + bendDirection * height;
        AimBone(upper, joint - start);
        AimBone(lower, target - lower.position);
    }

    private void AimBone(Transform bone, Vector3 worldDirection)
    {
        if (bone == null || worldDirection.sqrMagnitude < 0.000001f)
            return;
        Vector3 axis = boneAxis.sqrMagnitude > 0.0001f ? boneAxis.normalized : Vector3.up;
        Quaternion desiredWorldRotation = Quaternion.FromToRotation(axis, worldDirection.normalized) * bone.rotation;
        Quaternion parentRotation = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
        bone.rotation = Quaternion.Inverse(parentRotation) * desiredWorldRotation;
    }

    private void ApplyGearLever(float deltaTime)
    {
        if (gearLever == null)
            return;
        float pitch = vehicle.gear < 0 ? 24f : vehicle.gear == 0 ? 0f : vehicle.gear <= 2 ? -19f : 4f;
        float yaw = vehicle.gear == 1 || vehicle.gear == 3 ? -15f : vehicle.gear == 2 || vehicle.gear == 4 ? 15f : 0f;
        Quaternion target = gearLeverRest * Quaternion.Euler(pitch, yaw, 0f);
        gearLever.localRotation = Quaternion.Slerp(gearLever.localRotation, target, 1f - Mathf.Exp(-10f * deltaTime));
    }

    private void UpdateHeadVisibility()
    {
        if (headRenderer == null)
            return;
        CameraController1912 camera = FindAnyObjectByType<CameraController1912>();
        bool hidden = hideHeadInCockpit && camera != null && camera.CurrentMode == CameraMode.Cockpit;
        if (headRenderer.enabled != !hidden)
            headRenderer.enabled = !hidden;
    }

    private void OnDisable()
    {
        if (headRenderer != null)
            headRenderer.enabled = true;
    }

    private Transform ResolveBone(Transform assigned, Dictionary<string, Transform> bones, string exactName, string alias, HumanBodyBones humanBone)
    {
        if (assigned != null)
            return assigned;
        if (bones.TryGetValue(exactName, out Transform bone))
            return bone;
        if (bones.TryGetValue(alias, out bone))
            return bone;
        if (animator != null && animator.isHuman)
            return animator.GetBoneTransform(humanBone);
        return null;
    }

    private static Dictionary<string, Transform> BuildTransformMap(Transform rootTransform)
    {
        Dictionary<string, Transform> result = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
        if (rootTransform == null)
            return result;
        foreach (Transform child in rootTransform.GetComponentsInChildren<Transform>(true))
        {
            if (!string.IsNullOrEmpty(child.name))
                result[child.name] = child;
        }
        return result;
    }

    private static Transform FindExact(Dictionary<string, Transform> transforms, string name)
    {
        return transforms.TryGetValue(name, out Transform value) ? value : null;
    }

    private static Transform FindContains(Dictionary<string, Transform> transforms, string namePart)
    {
        foreach (KeyValuePair<string, Transform> pair in transforms)
        {
            if (pair.Key.Contains(namePart))
                return pair.Value;
        }
        return null;
    }
}
