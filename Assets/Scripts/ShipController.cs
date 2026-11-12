using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Star Wars: Battlefront 2 (2005)-style starfighter flight.
///  - Right trigger = thrust (hold to accelerate; release to coast with drag)
///  - Left trigger  = brake
///  - Left stick    = pitch (Y) + yaw (X) steering
///  - Bumpers       = roll
///  - Right stick   = (unused / free-look trim)
///  - A button      = fire (placeholder)
/// Momentum + inertia + banking into turns.
/// Keyboard fallback: W/S pitch, A/D yaw, Space thrust, Shift brake, Q/E roll, mouse fine-aim.
/// </summary>
public class ShipController : MonoBehaviour
{
    [Header("Thrust")]
    public float thrustAcceleration = 40f;   // acceleration while thrusting
    public float brakeDeceleration = 40f;    // deceleration while braking
    public float passiveDrag = 2f;           // drag when neither thrust nor brake
    public float maxSpeed = 150f;
    public float minSpeed = 0f;              // allow full stop

    [Header("Steering")]
    public float pitchSpeed = 90f;           // deg/s at full stick
    public float yawSpeed = 90f;
    public float rollSpeed = 120f;
    public float rotationSmooth = 6f;
    public float pitchLimit = 80f;

    [Header("Banking")]
    public float maxBank = 60f;
    public float bankSpeed = 5f;

    [Header("Xbox / Gamepad")]
    public float stickDeadzone = 0.15f;
    public float triggerDeadzone = 0.1f;

    [Header("Thrusters (flames)")]
    public ParticleSystem[] thrusters;
    public float thrusterMinEmission = 8f;
    public float thrusterMaxEmission = 70f;

    [Header("Keyboard")]
    public float mouseSensitivity = 0.1f;

    private Vector3 _velocity;
    private float _yaw;
    private float _pitch;
    private float _bank;
    private float _rollInput;

    void Start()
    {
        var e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
    }

    void Update()
    {
        float pitchInput = 0f;
        float yawInput = 0f;
        float rollInput = 0f;
        float thrust = 0f;   // 0..1
        float brake = 0f;    // 0..1
        bool fire = false;

        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 steer = ApplyDeadzone(gp.leftStick.ReadValue(), stickDeadzone);
            pitchInput = -steer.y;     // up on stick = pitch up
            yawInput = steer.x;
            thrust = gp.rightTrigger.ReadValue();
            brake = gp.leftTrigger.ReadValue();
            if (gp.leftShoulder.isPressed) rollInput -= 1f;
            if (gp.rightShoulder.isPressed) rollInput += 1f;
            // right stick fine-trim
            Vector2 trim = ApplyDeadzone(gp.rightStick.ReadValue(), stickDeadzone);
            yawInput += trim.x * 0.4f;
            pitchInput += -trim.y * 0.4f;
            fire = gp.buttonSouth.wasPressedThisFrame;

        }
        else
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) pitchInput += 1f;
                if (kb.sKey.isPressed) pitchInput -= 1f;
                if (kb.aKey.isPressed) yawInput -= 1f;
                if (kb.dKey.isPressed) yawInput += 1f;
                if (kb.qKey.isPressed) rollInput -= 1f;
                if (kb.eKey.isPressed) rollInput += 1f;
                if (kb.spaceKey.isPressed) thrust = 1f;
                if (kb.leftShiftKey.isPressed) brake = 1f;
            }
            if (mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue();
                yawInput += md.x * mouseSensitivity * 0.05f;
                pitchInput += -md.y * mouseSensitivity * 0.05f;
                if (mouse.leftButton.wasPressedThisFrame) fire = true;
            }
        }

        // --- thrust / speed ---
        float currentSpeed = _velocity.magnitude;
        if (thrust > triggerDeadzone)
        {
            _velocity += transform.forward * (thrustAcceleration * thrust * Time.deltaTime);
        }
        if (brake > triggerDeadzone)
        {
            _velocity -= _velocity.normalized * (brakeDeceleration * brake * Time.deltaTime);
            if (_velocity.magnitude < 0.5f) _velocity = Vector3.zero;
        }
        // passive drag
        _velocity = Vector3.Lerp(_velocity, Vector3.zero, passiveDrag * Time.deltaTime);
        // clamp speed
        if (_velocity.magnitude > maxSpeed)
            _velocity = _velocity.normalized * maxSpeed;
        if (_velocity.magnitude < minSpeed && _velocity.magnitude > 0f && thrust <= triggerDeadzone && brake <= triggerDeadzone)
            _velocity = Vector3.zero; // allow stop when coasting to 0

        // --- thruster flames ---
        if (thrusters != null)
        {
            float t = Mathf.Clamp01(thrust);
            float emRate = Mathf.Lerp(thrusterMinEmission, thrusterMaxEmission, t);
            float spd = Mathf.Lerp(2f, 7f, t);
            float sz = Mathf.Lerp(0.25f, 0.45f, t);
            foreach (var ps in thrusters)
            {
                if (ps == null) continue;
                var em = ps.emission;
                em.rateOverTime = emRate;
                var m = ps.main;
                m.startSpeed = spd;
                m.startSize = sz;
                if (!ps.isPlaying) ps.Play();
            }
        }

        transform.position += _velocity * Time.deltaTime;

        // --- rotation targets ---
        _yaw += yawInput * yawSpeed * Time.deltaTime;
        _pitch += pitchInput * pitchSpeed * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -pitchLimit, pitchLimit);

        // banking: roll into yaw
        float targetBank = -yawInput * maxBank * 0.6f + rollInput * maxBank;
        targetBank = Mathf.Clamp(targetBank, -maxBank, maxBank);
        _bank = Mathf.Lerp(_bank, targetBank, bankSpeed * Time.deltaTime);

        float roll = rollInput * rollSpeed * Time.deltaTime;
        _yaw += 0f; // yaw already applied
        // apply roll to bank only (visual), not yaw

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

}
