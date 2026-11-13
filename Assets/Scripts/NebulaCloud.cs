using UnityEngine;

/// <summary>
/// Slowly rotates a volumetric nebula cloud (3 crossed quads) so it reads as a 3D volume.
/// </summary>
public class NebulaCloud : MonoBehaviour
{
    public float rotateSpeed = 6f;
    public Vector3 rotateAxis = new Vector3(0.3f, 1f, 0.2f);

    void Update()
    {
        transform.Rotate(rotateAxis.normalized, rotateSpeed * Time.deltaTime, Space.Self);
    }
}
