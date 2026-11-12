using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal prototype ship controller driven by an Xbox/Gamepad.
/// Left stick  = move (strafe + forward/back)
/// Right stick = aim (yaw + pitch)
/// Triggers     = boost (right trigger)
/// Bumpers      = roll
/// A button     = (hook for fire) - logged for now
/// </summary>
public class ShipController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float turnSpeed = 150f;
    public float boostMultiplier = 2.5f;
    public float verticalSpeed = 6f;

    void Update()
    {
        var gp = Gamepad.current;
        if (gp == null) return;

        Vector2 move = gp.leftStick.ReadValue();
        Vector2 look = gp.rightStick.ReadValue();

        // Move relative to ship facing; left stick Y = forward/back, X = strafe
        Vector3 localMove = new Vector3(move.x, 0f, move.y);
        float speed = moveSpeed;
        if (gp.rightTrigger.ReadValue() > 0.1f) speed *= boostMultiplier;
        // Left trigger = descend, right trigger also boosts; left stick click = ascend
        float vert = 0f;
        if (gp.leftStickButton.isPressed) vert += 1f;
        if (gp.rightStickButton.isPressed) vert -= 1f;
        localMove.y = vert;
        transform.position += transform.TransformDirection(localMove) * speed * Time.deltaTime;

        // Aim: yaw (X) + pitch (Y)
        float yaw = look.x * turnSpeed * Time.deltaTime;
        float pitch = -look.y * turnSpeed * 0.6f * Time.deltaTime;
        transform.Rotate(pitch, yaw, 0f, Space.Self);

        // Roll with bumpers
        float roll = 0f;
        if (gp.leftShoulder.isPressed) roll += 1f;
        if (gp.rightShoulder.isPressed) roll -= 1f;
        transform.Rotate(0f, 0f, roll * turnSpeed * Time.deltaTime, Space.Self);

        // A button: fire hook (placeholder)
        if (gp.buttonSouth.wasPressedThisFrame)
        {
            Debug.Log("[Ship] Fire (A) pressed");
        }
    }
}
