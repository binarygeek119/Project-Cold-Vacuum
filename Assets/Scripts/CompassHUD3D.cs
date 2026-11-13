using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D compass (Skyrim-style) with horizontal bar (top) and vertical bar (left).
/// Bars stay fixed in place; pins rotate like gauge needles to point at the
/// nearest target in the current star system. Bars and pins are separate images.
/// </summary>
public class CompassHUD3D : MonoBehaviour
{
    public RawImage horizontalBar;
    public RawImage verticalBar;
    public RectTransform horizontalPin;
    public RectTransform verticalPin;
    public Text targetLabel;

    public float maxAngle = 90f;
    public float barWidth = 400f;
    public float barHeight = 400f;

    private Transform _target;
    private SpaceStreamer _streamer;

    void Start()
    {
        _streamer = FindFirstObjectByType<SpaceStreamer>();
    }

    void Update()
    {
        if (horizontalBar == null || verticalBar == null) return;

        FindNearestTarget();

        var ship = GameObject.Find("Player Ship");
        if (ship == null) return;

        if (_target == null)
        {
            if (horizontalPin != null) horizontalPin.gameObject.SetActive(false);
            if (verticalPin != null) verticalPin.gameObject.SetActive(false);
            if (targetLabel != null) targetLabel.gameObject.SetActive(false);
            return;
        }

        if (horizontalPin != null) horizontalPin.gameObject.SetActive(true);
        if (verticalPin != null) verticalPin.gameObject.SetActive(true);
        if (targetLabel != null) targetLabel.gameObject.SetActive(true);

        Vector3 toTarget = _target.position - ship.transform.position;
        if (toTarget.sqrMagnitude < 0.01f) return;
        Vector3 dir = toTarget.normalized;

        // Horizontal angle (yaw bearing)
        Vector3 forward = ship.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 horizDir = new Vector3(dir.x, 0f, dir.z);
        if (horizDir.sqrMagnitude > 0.001f) horizDir.Normalize();
        float hAngle = Vector3.SignedAngle(forward, horizDir, Vector3.up);

        // Vertical angle (pitch bearing)
        float vAngle = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Rotate horizontal pin like a gauge needle (stays in place, rotates)
        if (horizontalPin != null)
        {
            if (Mathf.Abs(hAngle) > maxAngle)
                horizontalPin.gameObject.SetActive(false);
            else
            {
                horizontalPin.gameObject.SetActive(true);
                // Pin stays at center; rotates to point at target
                horizontalPin.anchoredPosition = Vector2.zero;
                horizontalPin.localRotation = Quaternion.Euler(0f, 0f, -hAngle);
            }
        }

        // Rotate vertical pin like a gauge needle
        if (verticalPin != null)
        {
            if (Mathf.Abs(vAngle) > maxAngle)
                verticalPin.gameObject.SetActive(false);
            else
            {
                verticalPin.gameObject.SetActive(true);
                verticalPin.anchoredPosition = Vector2.zero;
                verticalPin.localRotation = Quaternion.Euler(0f, 0f, vAngle);
            }
        }

        // Tint pins with target color
        var ct = _target.GetComponent<CompassTarget>();
        Color pinColor = ct != null ? ct.displayColor : Color.white;
        if (horizontalPin != null)
        {
            var img = horizontalPin.GetComponent<Image>();
            if (img != null) img.color = pinColor;
        }
        if (verticalPin != null)
        {
            var img = verticalPin.GetComponent<Image>();
            if (img != null) img.color = pinColor;
        }

        // Label
        if (targetLabel != null)
        {
            float dist = toTarget.magnitude;
            string name = ct != null && !string.IsNullOrEmpty(ct.displayName) ? ct.displayName : _target.name;
            targetLabel.text = name + "  " + Mathf.RoundToInt(dist) + "m";
        }
    }

    private void FindNearestTarget()
    {
        _target = null;
        float minDist = float.MaxValue;

        var ship = GameObject.Find("Player Ship");
        if (ship == null) return;

        // Check if in star system
        if (_streamer != null && !_streamer.IsInStarSystem())
        {
            return;
        }

        Vector3 systemCenter = _streamer != null ? _streamer.GetCurrentSystemCenter() : Vector3.zero;
        float systemRadius = _streamer != null ? _streamer.GetCurrentSystemRadius() : 0f;

        var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var tr in allTransforms)
        {
            var go = tr.gameObject;
            if (go == ship) continue;
            bool isTarget = false;
            if (go.GetComponent<CompassTarget>() != null) isTarget = true;
            if (go.name.Contains("Star") || go.name.Contains("Planet") || go.name.Contains("Station")) isTarget = true;
            if (!isTarget) continue;

            // Filter by current system
            if (systemRadius > 0f)
            {
                float distToSystem = Vector3.Distance(go.transform.position, systemCenter);
                if (distToSystem > systemRadius) continue;
            }

            float d = Vector3.Distance(ship.transform.position, go.transform.position);
            if (d < minDist) { minDist = d; _target = go.transform; }
        }
    }
}