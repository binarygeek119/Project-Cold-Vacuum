using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smoother, flight-like ship controller (gamepad).
/// Left stick  = move (strafe + forward/back + vertical via stick clicks)
/// Right stick = aim (yaw + pitch)
/// Right trigger = boost
/// Bumpers = roll
/// A = fire (placeholder)
/// </summary>
public class ShipController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 12f;
    public float acceleration = 6f;
    public float drag = 3f;
    public float boostMultiplier = 2.5f;

    [Header("Rotation")]
    public float turnSpeed = 150f;
    public float rotationSmooth = 8f;
    public float pitchLimit = 60f;

    [Header("Banking")]
    public float maxBank = 45f;
    public float bankSpeed = 6f;

    private Vector3 _velocity;
    private float _yaw;
    private float _pitch;
    private float _bank;

    void Start()
    {
        var e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
    }

    void Update()
    {
        var gp = Gamepad.current;
        if (gp == null) return;

        Vector2 move = gp.leftStick.ReadValue();
        Vector2 look = gp.rightStick.ReadValue();

        // --- desired velocity (local -> world) ---
        Vector3 localDesired = new Vector3(move.x, 0f, move.y);
        if (gp.leftStickButton.isPressed) localDesired.y += 1f;
        if (gp.rightStickButton.isPressed) localDesired.y -= 1f;

        float speed = moveSpeed;
        if (gp.rightTrigger.ReadValue() > 0.1f) speed *= boostMultiplier;

        Vector3 worldDesired = transform.TransformDirection(localDesired.normalized) * (localDesired.magnitude * speed);

        // accelerate toward desired, with drag when idle
        _velocity = Vector3.Lerp(_velocity, worldDesired, acceleration * Time.deltaTime);
        if (localDesired.sqrMagnitude < 0.01f)
            _velocity = Vector3.Lerp(_velocity, Vector3.zero, drag * Time.deltaTime);

        transform.position += _velocity * Time.deltaTime;

        // --- rotation targets ---
        _yaw += look.x * turnSpeed * Time.deltaTime;
        _pitch += -look.y * turnSpeed * 0.5f * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -pitchLimit, pitchLimit);

        // banking: roll into strafe/turn
        float targetBank = -move.x * maxBank;
        if (Mathf.Abs(move.x) < 0.05f) targetBank = -look.x * maxBank * 0.4f;
        // bumper roll
        if (gp.leftShoulder.isPressed) targetBank += maxBank;
        if (gp.rightShoulder.isPressed) targetBank -= maxBank;
        targetBank = Mathf.Clamp(targetBank, -maxBank, maxBank);
        _bank = Mathf.Lerp(_bank, targetBank, bankSpeed * Time.deltaTime);

        Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, _bank);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmooth * Time.deltaTime);

        if (gp.buttonSouth.wasPressedThisFrame)
            Debug.Log("[Ship] Fire (A) pressed");
    }
}
