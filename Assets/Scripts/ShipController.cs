using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Physics-based starfighter flight (Rigidbody). BF2(2005)-style feel:
/// thrust on right trigger, brake on left, pitch/yaw steering, roll, momentum.
/// Uses forces/torque so collisions and inertia are physical.
/// </summary>
public class ShipController : MonoBehaviour
{
    [Header("Thrust")]
    public float thrustForce = 25f;          // acceleration while thrusting
    public float brakeForce = 40f;          // deceleration while braking
    public float maxSpeed = 50f;

    [Header("Steering (torque)")]
    public float pitchTorque = 15f;
    public float yawTorque = 15f;
    public float rollTorque = 30f;
    public float angularDamping = 6f;
    public float pitchLimit = 80f;

    [Header("Banking")]
    public float maxBank = 60f;

    [Header("Xbox / Gamepad")]
    public float stickDeadzone = 0.15f;
    public float triggerDeadzone = 0.1f;

    [Header("Thrusters (flames)")]
    public ParticleSystem[] thrusters;
    public float thrusterMinEmission = 8f;
    public float thrusterMaxEmission = 70f;

    [Header("Keyboard")]
    public float mouseSensitivity = 0.05f;

    [Header("Pitch")]
    public bool invertPitch = true;

    private Rigidbody _rb;
    private float _bank;
    private float _yawAccum;   // for banking visual only

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 0.5f;
        _rb.angularDamping = angularDamping;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        // banking visual (smooth tilt of the model child) — driven by yaw input
        // (handled in FixedUpdate via torque; here we just keep thrusters in sync)
    }

    void FixedUpdate()
    {
        float pitchInput = 0f;
        float yawInput = 0f;
        float rollInput = 0f;
        float thrust = 0f;
        float brake = 0f;
        bool fire = false;

        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 steer = ApplyDeadzone(gp.leftStick.ReadValue(), stickDeadzone);
            pitchInput = -steer.y;
            yawInput = steer.x;
            thrust = gp.rightTrigger.ReadValue();
            brake = gp.leftTrigger.ReadValue();
            if (gp.leftShoulder.isPressed) rollInput -= 1f;
            if (gp.rightShoulder.isPressed) rollInput += 1f;
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
                if (kb.fKey.wasPressedThisFrame) fire = true;
            }
            if (mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue();
                yawInput += md.x * mouseSensitivity * 0.02f;
                pitchInput += -md.y * mouseSensitivity * 0.02f;
                if (mouse.leftButton.wasPressedThisFrame) fire = true;
            }
        }
        // Steering only works while thrusting
        if (thrust <= triggerDeadzone)
        {
            pitchInput = 0f;
            yawInput = 0f;
            rollInput = 0f;
        }
        if (invertPitch) pitchInput = -pitchInput;

        // --- thrust ---
        if (thrust > triggerDeadzone)
        {
            _rb.AddForce(transform.forward * thrust * thrustForce, ForceMode.Acceleration);
        }
        else if (brake > triggerDeadzone && _rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            _rb.AddForce(-_rb.linearVelocity.normalized * brake * brakeForce, ForceMode.Acceleration);
        }
        else
        {
            // No thrust applied: stop (no coasting)
            _rb.linearVelocity = Vector3.zero;
        }

        // --- speed clamp ---
        if (_rb.linearVelocity.magnitude > maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;

        // --- steering torque ---
        _rb.AddTorque(transform.right * pitchInput * pitchTorque, ForceMode.Acceleration);
        _rb.AddTorque(transform.up * yawInput * yawTorque, ForceMode.Acceleration);
        // roll into yaw + manual roll
        float rollCmd = rollInput + (-yawInput * 0.5f);
        _rb.AddTorque(transform.forward * rollCmd * rollTorque, ForceMode.Acceleration);

        // --- banking visual (tilt model child) ---
        _bank = Mathf.Lerp(_bank, -yawInput * maxBank * 0.6f + rollInput * maxBank, 5f * Time.fixedDeltaTime);
        _bank = Mathf.Clamp(_bank, -maxBank, maxBank);
        // apply bank to the model child if present
        var model = transform.Find("Ship Model");
        if (model != null)
        {
            var baseRot = Quaternion.Euler(0f, -90f, 0f);
            model.localRotation = baseRot * Quaternion.Euler(0f, 0f, _bank);
        }

        // --- thrusters ---
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

        if (fire)
            Debug.Log("[Ship] Fire pressed");
    }

    private static Vector2 ApplyDeadzone(Vector2 v, float dz)
    {
        if (v.magnitude < dz) return Vector2.zero;
        return v.normalized * ((v.magnitude - dz) / (1f - dz));
    }

    void OnCollisionEnter(Collision c)
    {
        Debug.Log("[Ship] Collided with " + c.collider.name);
    }
}