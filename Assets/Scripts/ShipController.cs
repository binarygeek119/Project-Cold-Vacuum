using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BF2(2005)-style starfighter flight. Thrust on right trigger, brake on left,
/// pitch/yaw steering, roll, momentum. Supports gamepad and keyboard+mouse.
/// </summary>
public class ShipController : MonoBehaviour
{
    [Header("Thrust")]
    public float thrustForce = 25f;
    public float brakeForce = 40f;
    public float maxSpeed = 120f;
    public float hyperCruiseSpeed = 900f;
    public float hyperSpoolTime = 2.5f;
    private float _targetSpeed = 0f;
    private bool _hyperDriveActive = false;
    public bool HyperDriveActive => _hyperDriveActive;

    [Header("Steering (torque)")]
    public float pitchTorque = 15f;
    public float yawTorque = 15f;
    public float rollTorque = 30f;
    public float angularDamping = 6f;
    public float pitchLimit = 80f;

    [Header("Banking")]
    public float maxBank = 80f;
    public float bankSpeed = 10f;

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
                yawInput += md.x * mouseSensitivity * 0.05f;
                pitchInput += -md.y * mouseSensitivity * 0.05f;
                if (mouse.leftButton.wasPressedThisFrame) fire = true;
            }
        }
        if (invertPitch) pitchInput = -pitchInput;

        // Steering works while thrusting or moving
        bool canSteer = thrust > triggerDeadzone || _rb.linearVelocity.sqrMagnitude > 25f;
        if (!canSteer)
        {
            pitchInput = 0f;
            yawInput = 0f;
            rollInput = 0f;
        }

        // --- hyper drive toggle (X button / H key) ---
        bool hyperToggle = false;
        if (gp != null) hyperToggle = gp.buttonWest.wasPressedThisFrame;
        else
        {
            var kb2 = Keyboard.current;
            if (kb2 != null && kb2.hKey.wasPressedThisFrame) hyperToggle = true;
        }
        if (hyperToggle) _hyperDriveActive = !_hyperDriveActive;

        // --- speed target (coast, don't snap to zero) ---
        float want;
        if (brake > triggerDeadzone) want = 0f;
        else if (thrust > triggerDeadzone)
            want = _hyperDriveActive ? hyperCruiseSpeed : maxSpeed;
        else
            want = _hyperDriveActive ? hyperCruiseSpeed * 0.75f : _targetSpeed; // coast

        float rate = (_hyperDriveActive ? hyperCruiseSpeed / hyperSpoolTime : maxSpeed / 1.5f) * Time.fixedDeltaTime;
        _targetSpeed = Mathf.MoveTowards(_targetSpeed, want, rate);

        Vector3 fwd = transform.forward;
        Vector3 v = _rb.linearVelocity;
        float fwdSpeed = Vector3.Dot(v, fwd);
        Vector3 lateral = v - fwd * fwdSpeed;
        _rb.linearVelocity = fwd * _targetSpeed + lateral * 0.9f;

        // --- steering torque ---
        float torqueScale = 1f + Mathf.Clamp01(_targetSpeed / Mathf.Max(hyperCruiseSpeed, 1f)) * 2f;
        _rb.AddTorque(transform.right * pitchInput * pitchTorque * torqueScale, ForceMode.Acceleration);
        _rb.AddTorque(transform.up * yawInput * yawTorque * torqueScale, ForceMode.Acceleration);
        float rollCmd = rollInput + (-yawInput * 0.5f);
        _rb.AddTorque(transform.forward * rollCmd * rollTorque * torqueScale, ForceMode.Acceleration);

        // --- banking visual (tilt model child) ---
        _bank = Mathf.Lerp(_bank, yawInput * maxBank * 0.6f + rollInput * maxBank, 5f * Time.fixedDeltaTime);
        _bank = Mathf.Clamp(_bank, -maxBank, maxBank);
        var model = transform.Find("Ship Model");
        if (model != null)
        {
            var baseRot = Quaternion.Euler(0f, -90f, 0f);
            model.localRotation = baseRot * Quaternion.Euler(_bank, 0f, 0f);
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

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null && collision.collider.GetComponent<AsteroidMotion>() != null)
        {
            _hyperDriveActive = false;
        }
    }

    void OnDisable()
    {
        if (Gamepad.current != null) Gamepad.current.ResetHaptics();
    }
}