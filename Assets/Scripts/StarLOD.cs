using UnityEngine;

/// <summary>
/// Level of Detail for stars: shows a simple light+twinkle at distance,
/// swaps to the full star model when close.
/// </summary>
public class StarLOD : MonoBehaviour
{
    public GameObject farRepresentation;   // simple light + twinkle
    public GameObject nearRepresentation;   // full star model
    public float lodDistance = 400f;        // distance at which to switch to near

    private Transform _cam;

    void Start()
    {
        _cam = Camera.main != null ? Camera.main.transform : null;
        UpdateLOD();
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main != null ? Camera.main.transform : null;
        UpdateLOD();
    }

    private void UpdateLOD()
    {
        if (_cam == null) return;
        float dist = Vector3.Distance(transform.position, _cam.position);
        bool showNear = dist < lodDistance;

        if (farRepresentation != null) farRepresentation.SetActive(!showNear);
        if (nearRepresentation != null) nearRepresentation.SetActive(showNear);
    }
}