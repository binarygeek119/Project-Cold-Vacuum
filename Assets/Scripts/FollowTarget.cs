using UnityEngine;

/// <summary>
/// Keeps a transform (e.g. a VolClouds layer) centered on a target on the XZ plane.
/// </summary>
public class FollowTarget : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null) return;
        var p = target.position;
        p.y = 0f;
        transform.position = p;
    }
}
