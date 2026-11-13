using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skyrim-style compass that points toward the nearest point of interest.
/// </summary>
public class CompassHUD : MonoBehaviour
{
    public RectTransform compassBar;   // the horizontal compass strip
    public RectTransform marker;       // the marker that moves on the bar
    public Text markerLabel;           // optional label
    public float maxAngle = 90f;       // half-width of compass in degrees
    public float barWidth = 400f;      // width of compass bar in pixels

    private Transform _target; // the object to point to

    void Update()
    {
        if (compassBar == null || marker == null) return;

        // find nearest point of interest
        FindNearestTarget();

        if (_target == null)
        {
            marker.gameObject.SetActive(false);
            if (markerLabel != null) markerLabel.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        if (markerLabel != null) markerLabel.gameObject.SetActive(true);

        // direction from ship to target
        var ship = GameObject.Find("Player Ship");
        if (ship == null) return;
        Vector3 toTarget = _target.position - ship.transform.position;
        toTarget.y = 0f; // horizontal only
        if (toTarget.sqrMagnitude < 0.01f) return;
        Vector3 dir = toTarget.normalized;

        // ship forward (horizontal)
        Vector3 forward = ship.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        // angle between forward and direction to target
        float angle = Vector3.SignedAngle(forward, dir, Vector3.up);

        // clamp to compass range
        if (Mathf.Abs(angle) > maxAngle)
        {
            marker.gameObject.SetActive(false);
            if (markerLabel != null) markerLabel.gameObject.SetActive(false);
            return;
        }

        // map angle to bar position (-barWidth/2 .. +barWidth/2)
        float x = (angle / maxAngle) * (barWidth / 2f);
        marker.anchoredPosition = new Vector2(x, marker.anchoredPosition.y);

        // label
        if (markerLabel != null)
        {
            float dist = toTarget.magnitude;
            markerLabel.text = _target.name + "  " + Mathf.RoundToInt(dist) + "m";
        }
    }

    private void FindNearestTarget()
    {
        _target = null;
        float minDist = float.MaxValue;

        // candidate targets: stars, planets, stations, asteroids clusters
        var candidates = new System.Collections.Generic.List<Transform>();
        // stars in scene
        foreach (var go in GameObject.FindGameObjectsWithTag("Untagged"))
        {
            if (go.name.Contains("Star") || go.name.Contains("Planet") || go.name.Contains("Station") || go.name.Contains("Sphere"))
                candidates.Add(go.transform);
        }

        foreach (var t in candidates)
        {
            if (t == null || t == transform) continue;
            var ship = GameObject.Find("Player Ship");
            if (ship == null) continue;
            float d = Vector3.Distance(ship.transform.position, t.position);
            if (d < minDist) { minDist = d; _target = t; }
        }
    }
}
