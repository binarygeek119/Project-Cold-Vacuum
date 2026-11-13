using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D compass (Skyrim-style) with horizontal bar (top) and vertical bar (left).
/// Bars scroll (animate) with ship movement; pins are static images that
/// move position to point at the nearest target in the current star system.
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
    public float scrollSpeed = 0.5f;

    private Transform _target;
    private SpaceStreamer _streamer;
    private float _hScroll;
    private float _vScroll;

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

        // Scroll bars based on ship rotation (yaw/pitch)
        float yaw = ship.transform.eulerAngles.y;
        float pitch = ship.transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        _hScroll = Mathf.Lerp(_hScroll, yaw * 0.01f * scrollSpeed, Time.deltaTime * 5f);
        _vScroll = Mathf.Lerp(_vScroll, pitch * 0.01f * scrollSpeed, Time.deltaTime * 5f);

        if (horizontalBar != null)
            horizontalBar.uvRect = new Rect(_hScroll, 0f, 1f, 1f);
        if (verticalBar != null)
            verticalBar.uvRect = new Rect(0f, _vScroll, 1f, 1f);

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

        // Update horizontal pin position
        if (horizontalPin != null)
        {
            if (Mathf.Abs(hAngle) > maxAngle)
                horizontalPin.gameObject.SetActive(false);
            else
            {
                horizontalPin.gameObject.SetActive(true);
                float hx = (hAngle / maxAngle) * (barWidth / 2f);
                horizontalPin.anchoredPosition = new Vector2(hx, horizontalPin.anchoredPosition.y);
            }
        }

        // Update vertical pin position
        if (verticalPin != null)
        {
            if (Mathf.Abs(vAngle) > maxAngle)
                verticalPin.gameObject.SetActive(false);
            else
            {
                verticalPin.gameObject.SetActive(true);
                float vy = (vAngle / maxAngle) * (barHeight / 2f);
                verticalPin.anchoredPosition = new Vector2(verticalPin.anchoredPosition.x, -vy);
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

        // Get current star system info
        Vector3 systemCenter = Vector3.zero;
        float systemRadius = 0f;
        bool inSystem = false;
        if (_streamer != null)
        {
            systemCenter = _streamer.GetCurrentSystemCenter();
            systemRadius = _streamer.GetCurrentSystemRadius();
            inSystem = _streamer.IsInStarSystem();
        }

        // Hide pins if not in a star system
        if (!inSystem)
        {
            _target = null;
            return;
        }

        // Find all CompassTarget components
        var allTargets = FindObjectsByType<CompassTarget>(FindObjectsSortMode.None);
        foreach (var ct in allTargets)
        {
            if (ct == null) continue;
            // Filter by current system radius
            float distToSystem = Vector3.Distance(ct.transform.position, systemCenter);
            if (distToSystem > systemRadius) continue;
            float d = Vector3.Distance(ship.transform.position, ct.transform.position);
            if (d < minDist) { minDist = d; _target = ct.transform; }
        }
    }
}