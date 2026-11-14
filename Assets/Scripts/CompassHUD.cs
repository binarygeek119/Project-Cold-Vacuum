using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Skyrim-style compass that points toward the nearest point of interest.
/// </summary>
public class CompassHUD : MonoBehaviour
{
    public RectTransform compassBar;
    public RectTransform marker;
    public Text markerLabel;
    public float maxAngle = 90f;
    public float barWidth = 400f;

    private Transform _target;

    void Update()
    {
        if (compassBar == null || marker == null) return;

        FindNearestTarget();

        if (_target == null)
        {
            marker.gameObject.SetActive(false);
            if (markerLabel != null) markerLabel.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        if (markerLabel != null) markerLabel.gameObject.SetActive(true);

        var ship = GameObject.Find("Player Ship");
        if (ship == null) return;
        Vector3 toTarget = _target.position - ship.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return;
        Vector3 dir = toTarget.normalized;

        Vector3 forward = ship.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        float angle = Vector3.SignedAngle(forward, dir, Vector3.up);

        if (Mathf.Abs(angle) > maxAngle)
        {
            marker.gameObject.SetActive(false);
            if (markerLabel != null) markerLabel.gameObject.SetActive(false);
            return;
        }

        float x = (angle / maxAngle) * (barWidth / 2f);
        marker.anchoredPosition = new Vector2(x, marker.anchoredPosition.y);

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

        var ship = GameObject.Find("Player Ship");
        if (ship == null) return;

        // Candidate targets: GasGiant, StarLOD, Station, EnemyShip, or anything with "Star"/"Planet" in name
        var candidates = new List<Transform>();
        var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>();
        foreach (var tr in allTransforms)
        {
            var go = tr.gameObject;
            if (go == ship) continue;
            bool isTarget = false;
            if (go.GetComponent<GasGiant>() != null) isTarget = true;
            if (go.GetComponent<StarLOD>() != null) isTarget = true;
            if (go.GetComponent<EnemyShip>() != null) isTarget = true;
            if (go.name.Contains("Star") || go.name.Contains("Planet") || go.name.Contains("Station")) isTarget = true;
            if (isTarget) candidates.Add(tr);
        }

        Debug.Log("[CompassHUD] candidates=" + candidates.Count);
        foreach (var t in candidates)
        {
            if (t == null) continue;
            float d = Vector3.Distance(ship.transform.position, t.position);
            if (d < minDist) { minDist = d; _target = t; }
        }
    }
}