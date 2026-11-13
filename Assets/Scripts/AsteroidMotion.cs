using UnityEngine;

/// <summary>
/// Gives an asteroid a slow spin and a gentle orbit around its spawn point.
/// Works with a non-kinematic Rigidbody so impacts still move it.
/// </summary>
public class AsteroidMotion : MonoBehaviour
{
    public float spinSpeed = 20f;          // deg/s
    public Vector3 spinAxis = Vector3.up;
    public float orbitSpeed = 0.3f;       // rad/s around spawn point
    public float orbitRadius = 1.5f;

    private Rigidbody _rb;
    private Vector3 _orbitCenter;
    private Vector3 _orbitPhase;          // random offset for phase

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 1f;
        _rb.angularDamping = 0.5f;

        _orbitCenter = transform.position;
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(5f, 25f);
        orbitSpeed = Random.Range(0.1f, 0.5f);
        orbitRadius = Random.Range(0.5f, 2f);
        _orbitPhase = Random.onUnitSphere;

        // initial spin
        _rb.angularVelocity = spinAxis * (spinSpeed * Mathf.Deg2Rad);
        // initial tangential velocity for orbit
        Vector3 toCenter = transform.position - _orbitCenter;
        if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.right;
        Vector3 tangent = Vector3.Cross(Vector3.up, toCenter).normalized;
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
        _rb.linearVelocity = tangent * (orbitSpeed * orbitRadius);
    }

    void FixedUpdate()
    {
        if (_rb == null) return;
        // gentle centripetal force to keep orbit (weak, so impacts dominate)
        Vector3 toCenter = _orbitCenter - transform.position;
        float dist = toCenter.magnitude;
        if (dist > 0.01f)
        {
            float centripetal = _rb.mass * (orbitSpeed * orbitSpeed * dist);
            centripetal = Mathf.Min(centripetal, 2f);
            _rb.AddForce(toCenter.normalized * centripetal, ForceMode.Force);
        }
    }
}
