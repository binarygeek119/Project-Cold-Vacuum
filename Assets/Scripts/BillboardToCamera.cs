using UnityEngine;

/// <summary>
/// Makes a GameObject always face the main camera.
/// </summary>
public class BillboardToCamera : MonoBehaviour
{
    void Update()
    {
        if (Camera.main != null)
            transform.LookAt(Camera.main.transform);
    }
}