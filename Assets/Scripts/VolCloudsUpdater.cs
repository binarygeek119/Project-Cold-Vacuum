using UnityEngine;

/// <summary>
/// Drives the VolClouds shader animation (time + wind shift) and keeps the cloud layer around the camera.
/// </summary>
public class VolCloudsUpdater : MonoBehaviour
{
    public float timeScale = 0.01f;
    private Material _mat;
    private Transform _cam;

    void Start()
    {
        var rend = GetComponent<Renderer>();
        if (rend != null) _mat = rend.material;
        _cam = Camera.main != null ? Camera.main.transform : null;
    }

    void Update()
    {
        if (_mat == null) return;
        _mat.SetFloat("_CloudTime", Time.time * timeScale);
        _mat.SetFloat("_XShift", Mathf.Sin(Time.time * 0.05f) * 0.5f);

        // keep the cloud layer centered on the camera
        if (_cam != null)
        {
            transform.position = new Vector3(_cam.position.x, 0f, _cam.position.z);
        }
    }
}
