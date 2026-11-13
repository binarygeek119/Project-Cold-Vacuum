using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gives an asteroid a slow spin and a gentle orbit around its spawn point.
/// Also attracts nearby asteroids (gravity) so clusters pull together.
/// Works with a non-kinematic Rigidbody so impacts still move it.
/// </summary>
public class AsteroidMotion : MonoBehaviour
{
    public float spinSpeed = 20f;
    public Vector3 spinAxis = Vector3.up;
    public float orbitSpeed = 0.3f;
    public float orbitRadius = 1.5f;

    [Header("Mutual attraction")]
    public float attractRadius = 8f;        // asteroids within this distance pull together
    public float attractStrength = 2f;      // force strength
    public float attractMinDistance = 1.5f; // avoid singularity

    private Rigidbody _rb;
    private Vector3 _orbitCenter;
    private static readonly List<AsteroidMotion> All = new List<AsteroidMotion>();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

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

        _rb.angularVelocity = spinAxis * (spinSpeed * Mathf.Deg2Rad);
        Vector3 toCenter = transform.position - _orbitCenter;
        if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.right;
        Vector3 tangent = Vector3.Cross(Vector3.up, toCenter).normalized;
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
        _rb.linearVelocity = tangent * (orbitSpeed * orbitRadius);
    }

    void FixedUpdate()
    {
        if (_rb == null) return;

        // gentle centripetal force to keep orbit
        Vector3 toCenter = _orbitCenter - transform.position;
        float dist = toCenter.magnitude;
        if (dist > 0.01f)
        {
            float centripetal = _rb.mass * (orbitSpeed * orbitSpeed * dist);
            centripetal = Mathf.Min(centripetal, 2f);
            _rb.AddForce(toCenter.normalized * centripetal, ForceMode.Force);
        }

        // mutual attraction between nearby asteroids
        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == null || other == this || other._rb == null) continue;
            Vector3 dir = other.transform.position - transform.position;
            float d = dir.magnitude;
            if (d < attractMinDistance || d > attractRadius) continue;
            float force = attractStrength / (d * d);
            force = Mathf.Min(force, 3f);
            _rb.AddForce(dir.normalized * force, ForceMode.Force);
        }
    }
}
