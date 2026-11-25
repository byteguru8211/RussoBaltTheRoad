using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleInput1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public CameraController1912 cameraController;

    private void Update()
    {
        if (vehicle == null)
            return;

        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        float throttle = keyboard != null ? (keyboard.wKey.isPressed ? 1f : 0f) : 0f;
        float brake = keyboard != null ? (keyboard.sKey.isPressed ? 1f : 0f) : 0f;
        float steer = keyboard != null ? ((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f)) : 0f;
        float clutch = keyboard != null && keyboard.leftShiftKey.isPressed ? 1f : 0f;
        float handbrake = keyboard != null && keyboard.spaceKey.isPressed ? 1f : 0f;

        if (pad != null)
        {
            throttle = Mathf.Max(throttle, pad.rightTrigger.ReadValue());
            brake = Mathf.Max(brake, pad.leftTrigger.ReadValue());
            steer = Mathf.Clamp(steer + pad.leftStick.x.ReadValue(), -1f, 1f);
            clutch = Mathf.Max(clutch, pad.leftShoulder.isPressed ? 1f : 0f);
            handbrake = Mathf.Max(handbrake, pad.buttonSouth.isPressed ? 1f : 0f);
        }

        VehicleController1912.VehicleInput input = new VehicleController1912.VehicleInput
        {
            throttle = throttle,
            brake = brake,
            steer = steer * (GameManager.Instance != null ? GameManager.Instance.Settings.steeringSensitivity : 1f),
            clutch = clutch,
            handbrake = handbrake
        };

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) vehicle.ShiftTo(1);
            if (keyboard.digit2Key.wasPressedThisFrame) vehicle.ShiftTo(2);
            if (keyboard.digit3Key.wasPressedThisFrame) vehicle.ShiftTo(3);
            if (keyboard.rKey.wasPressedThisFrame) vehicle.ShiftTo(-1);
            if (keyboard.fKey.wasPressedThisFrame) vehicle.TryStartEngine();
            if (keyboard.backspaceKey.wasPressedThisFrame) vehicle.ResetVehicle();
            if (keyboard.cKey.wasPressedThisFrame && cameraController != null) cameraController.CycleCamera();
        }

        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) vehicle.ShiftUp();
            if (pad.dpad.down.wasPressedThisFrame) vehicle.ShiftDown();
            if (pad.buttonNorth.wasPressedThisFrame) vehicle.TryStartEngine();
            if (pad.buttonEast.wasPressedThisFrame) vehicle.ResetVehicle();
            if (pad.rightStickButton.wasPressedThisFrame && cameraController != null) cameraController.CycleCamera();
        }

        vehicle.SetInput(input);
    }
}
