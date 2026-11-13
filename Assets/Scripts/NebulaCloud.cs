using UnityEngine;

/// <summary>
/// A soft, glowing nebula cloud that billboards toward the camera and slowly rotates.
/// </summary>
public class NebulaCloud : MonoBehaviour
{
    public float rotateSpeed = 8f;
    private Transform _cam;

    void Start()
    {
        _cam = Camera.main != null ? Camera.main.transform : null;
    }

    void Update()
    {
        if (_cam != null)
        {
            transform.LookAt(_cam.position);
            // billboard: keep upright-ish by rotating 180 on Y after lookat
        }
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime, Space.Self);
    }
}
