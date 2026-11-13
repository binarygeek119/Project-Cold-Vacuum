using UnityEngine;

/// <summary>
/// Simple AI: flies toward the player, orbits at a distance, occasionally fires.
/// </summary>
public class EnemyShip : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 8f;
    public float turnSpeed = 60f;
    public float orbitDistance = 30f;
    public float fireInterval = 2f;

    private float _fireTimer;

    void Start()
    {
        if (target == null)
        {
            var ship = GameObject.Find("Player Ship");
            if (ship != null) target = ship.transform;
        }
        _fireTimer = fireInterval;
    }

    void Update()
    {
        if (target == null) return;
        Vector3 toTarget = target.position - transform.position;
        float dist = toTarget.magnitude;
        if (dist < 0.1f) return;

        // Direction to target
        Vector3 dir = toTarget.normalized;
        // Steer toward target
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime * Mathf.Deg2Rad);

        // Move toward target, but orbit at orbitDistance
        if (dist > orbitDistance)
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        else
            transform.position += transform.right * moveSpeed * 0.5f * Time.deltaTime; // orbit

        // Fire timer
        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f)
        {
            _fireTimer = fireInterval;
            Debug.Log("[Enemy] " + name + " fires at player");
        }
    }
}
