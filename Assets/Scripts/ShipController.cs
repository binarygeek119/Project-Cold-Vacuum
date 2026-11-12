using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Flight-like ship controller. Supports Xbox/Gamepad (preferred) and
/// keyboard + mouse fallback so it can be tested without a controller.
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
    public float mouseSensitivity = 0.1f;

    [Header("Banking")]
    public float maxBank = 45f;
    public float bankSpeed = 6f;

    [Header("Xbox / Gamepad")]
    public float stickDeadzone = 0.15f;
    public float triggerDeadzone = 0.1f;
    public bool rumbleEnabled = true;

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
        Vector2 move = Vector2.zero;
        Vector2 look = Vector2.zero;
        float boost = 0f;
        bool ascend = false, descend = false;
        float rollInput = 0f;
        bool fire = false;

        var gp = Gamepad.current;
        if (gp != null)
        {
            move = ApplyDeadzone(gp.leftStick.ReadValue(), stickDeadzone);
            look = ApplyDeadzone(gp.rightStick.ReadValue(), stickDeadzone);
            boost = gp.rightTrigger.ReadValue();
            if (gp.leftStickButton.isPressed) ascend = true;
            if (gp.rightStickButton.isPressed) descend = true;
            if (gp.leftShoulder.isPressed) rollInput += 1f;
            if (gp.rightShoulder.isPressed) rollInput -= 1f;
            fire = gp.buttonSouth.wasPressedThisFrame;

            // rumble
            if (rumbleEnabled)
            {
                if (boost > triggerDeadzone)
                    gp.SetMotorSpeeds(0.25f, 0.5f * boost);
                else
                    gp.SetMotorSpeeds(0f, 0f);
                if (fire)
                {
                    gp.SetMotorSpeeds(0.6f, 0.9f);
                    Invoke(nameof(StopRumble), 0.12f);
                }
            }
        }
        else
        {
            // keyboard + mouse fallback
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.spaceKey.isPressed) ascend = true;
                if (kb.leftCtrlKey.isPressed) descend = true;
                if (kb.leftShiftKey.isPressed) boost = 1f;
                if (kb.qKey.isPressed) rollInput += 1f;
                if (kb.eKey.isPressed) rollInput -= 1f;
                fire = kb.spaceKey.wasPressedThisFrame; // space also fires when tapped
            }
            if (mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue();
                look = md * mouseSensitivity;
                if (mouse.leftButton.wasPressedThisFrame) fire = true;
            }
            move = Vector2.ClampMagnitude(move, 1f);
        }

        if (ascend) move.y += 1f; // reuse move.y for vertical? handled below
        // vertical stored separately
        float vert = 0f;
        if (ascend) vert += 1f;
        if (descend) vert -= 1f;

        // --- desired velocity (local -> world) ---
        Vector3 localDesired = new Vector3(move.x, vert, move.y);
        float speed = moveSpeed;
        if (boost > triggerDeadzone) speed *= boostMultiplier;

        Vector3 worldDesired = transform.TransformDirection(localDesired.normalized) * (localDesired.magnitude * speed);

        // accelerate toward desired, with drag when idle
        _velocity = Vector3.Lerp(_velocity, worldDesired, acceleration * Time.deltaTime);
        if (localDesired.sqrMagnitude < 0.01f && Mathf.Abs(vert) < 0.01f)
            _velocity = Vector3.Lerp(_velocity, Vector3.zero, drag * Time.deltaTime);

        transform.position += _velocity * Time.deltaTime;

        // --- rotation targets ---
        _yaw += look.x * turnSpeed * Time.deltaTime;
        _pitch += -look.y * turnSpeed * 0.5f * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -pitchLimit, pitchLimit);

        // banking
        float targetBank = -move.x * maxBank;
        if (Mathf.Abs(move.x) < 0.05f && gp != null) targetBank = -look.x * maxBank * 0.4f;
        if (rollInput > 0f) targetBank += maxBank;
        if (rollInput < 0f) targetBank -= maxBank;
        targetBank = Mathf.Clamp(targetBank, -maxBank, maxBank);
        _bank = Mathf.Lerp(_bank, targetBank, bankSpeed * Time.deltaTime);

        Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, _bank);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmooth * Time.deltaTime);

        if (fire)
            Debug.Log("[Ship] Fire pressed");
    }

    private static Vector2 ApplyDeadzone(Vector2 v, float dz)
    {
        if (v.magnitude < dz) return Vector2.zero;
        return v.normalized * ((v.magnitude - dz) / (1f - dz));
    }

    private void StopRumble()
    {
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
    }

    void OnDisable()
    {
        if (Gamepad.current != null) Gamepad.current.ResetHaptics();
    }

    void OnDestroy()
    {
        if (Gamepad.current != null) Gamepad.current.ResetHaptics();
    }
}
