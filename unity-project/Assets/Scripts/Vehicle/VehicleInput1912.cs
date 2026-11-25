using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class VehicleInput1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public CameraController1912 cameraController;

    private VehicleController1912.VehicleInput currentInput;

    public VehicleController1912.VehicleInput CurrentInput => currentInput;
    public float SteeringVisualAngle => ReadSteeringVisualAngle();

    private void Awake()
    {
        if (vehicle == null)
            vehicle = GetComponent<VehicleController1912>();
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController1912>();
    }

    private void OnEnable()
    {
        currentInput = new VehicleController1912.VehicleInput();
        vehicle?.SetInput(currentInput);
    }

    private void OnDisable()
    {
        currentInput = new VehicleController1912.VehicleInput();
        vehicle?.SetInput(currentInput);
        cameraController?.SetOrbitInput(Vector2.zero);
    }

    private void Update()
    {
        if (vehicle == null)
            return;
        if (FindAnyObjectByType<PauseMenuUI>() != null)
        {
            currentInput = new VehicleController1912.VehicleInput();
            vehicle.SetInput(currentInput);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        float throttle = keyboard != null && keyboard.wKey.isPressed ? 1f : 0f;
        float brake = keyboard != null && keyboard.sKey.isPressed ? 1f : 0f;
        float steer = keyboard != null ? (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f) : 0f;
        float clutch = keyboard != null && keyboard.leftShiftKey.isPressed ? 1f : 0f;
        float handbrake = keyboard != null && keyboard.spaceKey.isPressed ? 1f : 0f;

        if (pad != null)
        {
            throttle = Mathf.Max(throttle, pad.rightTrigger.ReadValue());
            brake = Mathf.Max(brake, pad.leftTrigger.ReadValue());
            steer = Mathf.Clamp(steer + pad.leftStick.x.ReadValue(), -1f, 1f);
            clutch = Mathf.Max(clutch, pad.leftShoulder.isPressed ? 1f : 0f);
            handbrake = Mathf.Max(handbrake, pad.buttonSouth.isPressed ? 1f : 0f);
            cameraController?.SetOrbitInput(pad.rightStick.ReadValue());
        }
        else
        {
            cameraController?.SetOrbitInput(Vector2.zero);
        }

        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        float sensitivity = settings != null ? Mathf.Clamp(settings.steeringSensitivity, 0.5f, 1.5f) : 1f;
        currentInput = new VehicleController1912.VehicleInput
        {
            throttle = Mathf.Clamp01(throttle),
            brake = Mathf.Clamp01(brake),
            steer = Mathf.Clamp(steer * sensitivity, -1f, 1f),
            clutch = Mathf.Clamp01(clutch),
            handbrake = Mathf.Clamp01(handbrake)
        };
        vehicle.SetInput(currentInput);

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ShiftTo(1);
            if (keyboard.digit2Key.wasPressedThisFrame) ShiftTo(2);
            if (keyboard.digit3Key.wasPressedThisFrame) ShiftTo(3);
            if (keyboard.digit4Key.wasPressedThisFrame) ShiftTo(4);
            if (keyboard.rKey.wasPressedThisFrame) ShiftTo(-1);
            if (keyboard.fKey.wasPressedThisFrame) vehicle.TryStartEngine();
            if (keyboard.backspaceKey.wasPressedThisFrame) ResetVehicle();
            if (keyboard.cKey.wasPressedThisFrame) cameraController?.CycleCamera();
            if (keyboard.hKey.wasPressedThisFrame) GetComponent<EngineAudio1912>()?.PlayHorn();
            if ((keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)) PauseRace();
        }

        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) ShiftTo(vehicle.gear + 1);
            if (pad.dpad.down.wasPressedThisFrame) ShiftTo(vehicle.gear - 1);
            if (pad.leftShoulder.wasPressedThisFrame) ShiftTo(vehicle.gear + 1);
            if (pad.rightShoulder.wasPressedThisFrame) ShiftTo(vehicle.gear - 1);
            if (pad.buttonNorth.wasPressedThisFrame) vehicle.TryStartEngine();
            if (pad.buttonEast.wasPressedThisFrame) ResetVehicle();
            if (pad.rightStickButton.wasPressedThisFrame) cameraController?.CycleCamera();
            if (pad.leftStickButton.wasPressedThisFrame) GetComponent<EngineAudio1912>()?.PlayHorn();
            if (pad.startButton.wasPressedThisFrame) PauseRace();
        }
    }

    public void ShiftTo(int gear)
    {
        if (vehicle == null)
            return;
        int previousGear = vehicle.gear;
        vehicle.ShiftTo(gear);
        if (vehicle.gear == previousGear)
            return;
    }

    public void ResetVehicle()
    {
        vehicle?.ResetVehicle();
        cameraController?.SnapToVehicle();
    }

    private void PauseRace()
    {
        RaceManager race = RaceManager.Instance;
        if (race == null || race.State == RaceManager.RaceState.Finished || FindAnyObjectByType<PauseMenuUI>() != null)
            return;

        currentInput = new VehicleController1912.VehicleInput();
        vehicle?.SetInput(currentInput);
        race.Pause();
    }

    private float ReadSteeringVisualAngle()
    {
        return vehicle != null ? vehicle.SteeringVisualAngle : 0f;
    }
}
