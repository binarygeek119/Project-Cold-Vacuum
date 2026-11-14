using UnityEngine;

/// <summary>
/// Slowly drifts/rotates a stratus (flat layered) nebula cloud.
/// </summary>
public class NebulaCloud : MonoBehaviour
{
    public float rotateSpeed = 4f;
    public Vector3 rotateAxis = Vector3.up;

    void Update()
    {
        transform.Rotate(rotateAxis.normalized, rotateSpeed * Time.deltaTime, UnityEngine.Space.Self);
    }
}
