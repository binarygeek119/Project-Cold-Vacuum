using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 3D compass (Skyrim-style) with horizontal bar (top) and vertical bar (left).
/// Bars are fixed in place; pins rotate like gauge needles to point at targets.
/// Supports MULTIPLE pins (one per target) plus a dedicated yellow STAR pin.
/// Each pin is colored to match its target object.
/// </summary>
public class CompassHUD3D : MonoBehaviour
{
    public RawImage horizontalBar;
    public RawImage verticalBar;
    public RectTransform horizontalPin;   // template (hidden) for spawning pins
    public RectTransform verticalPin;
    public RectTransform starHorizontalPin; // dedicated yellow star pin (template)
    public RectTransform starVerticalPin;
    public Text targetLabel;

    public float maxAngle = 90f;
    public float barWidth = 400f;
    public float barHeight = 400f;

    private SpaceStreamer _streamer;
    private List<PinEntry> _pins = new List<PinEntry>();
    private Transform _nearestStar;

    private class PinEntry
    {
        public Transform target;
        public RectTransform hPin;
        public RectTransform vPin;
    }

    void Start()
    {
        _streamer = FindFirstObjectByType<SpaceStreamer>();
        // Hide templates
        if (horizontalPin != null) horizontalPin.gameObject.SetActive(false);
        if (verticalPin != null) verticalPin.gameObject.SetActive(false);
        if (starHorizontalPin != null) starHorizontalPin.gameObject.SetActive(false);
        if (starVerticalPin != null) starVerticalPin.gameObject.SetActive(false);
    }

    void Update()
    {
        if (horizontalBar == null || verticalBar == null) return;

        var ship = GameObject.Find("Player Ship");
        if (ship == null) return;

        // Get current system
        Vector3 systemCenter = Vector3.zero;
        float systemRadius = 0f;
        bool inSystem = false;
        if (_streamer != null)
        {
            systemCenter = _streamer.GetCurrentSystemCenter();
            systemRadius = _streamer.GetCurrentSystemRadius();
            inSystem = _streamer.IsInStarSystem();
        }

        // Find all targets in current system
        var targets = new List<Transform>();
        if (inSystem)
        {
            var allTargets = FindObjectsByType<CompassTarget>(FindObjectsSortMode.None);
            foreach (var ct in allTargets)
            {
                if (ct == null) continue;
                float distToSystem = Vector3.Distance(ct.transform.position, systemCenter);
                if (systemRadius > 0f && distToSystem > systemRadius) continue;
                targets.Add(ct.transform);
            }
        }

        // Find nearest star in current system
        _nearestStar = null;
        float minStarDist = float.MaxValue;
        foreach (var t in targets)
        {
            if (t == null) continue;
            bool isStar = t.name.Contains("Star") || t.GetComponent<StarLOD>() != null;
            if (!isStar) continue;
            float d = Vector3.Distance(ship.transform.position, t.position);
            if (d < minStarDist) { minStarDist = d; _nearestStar = t; }
        }

        // Sync pins: create new, remove stale
        SyncPins(targets);

        // Hide all pins when no targets
        if (targets.Count == 0)
        {
            foreach (var entry in _pins)
            {
                if (entry.hPin != null) entry.hPin.gameObject.SetActive(false);
                if (entry.vPin != null) entry.vPin.gameObject.SetActive(false);
            }
            if (starHorizontalPin != null) starHorizontalPin.gameObject.SetActive(false);
            if (starVerticalPin != null) starVerticalPin.gameObject.SetActive(false);
            if (targetLabel != null) targetLabel.gameObject.SetActive(false);
            return;
        }

        // Update each pin
        Vector3 forward = ship.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        foreach (var entry in _pins)
        {
            if (entry.target == null || entry.hPin == null || entry.vPin == null) continue;

            Vector3 toTarget = entry.target.position - ship.transform.position;
            if (toTarget.sqrMagnitude < 0.01f) continue;
            Vector3 dir = toTarget.normalized;

            Vector3 horizDir = new Vector3(dir.x, 0f, dir.z);
            if (horizDir.sqrMagnitude > 0.001f) horizDir.Normalize();
            float hAngle = Vector3.SignedAngle(forward, horizDir, Vector3.up);
            float vAngle = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            bool hVisible = Mathf.Abs(hAngle) <= maxAngle;
            entry.hPin.gameObject.SetActive(hVisible);
            if (hVisible)
            {
                entry.hPin.anchoredPosition = Vector2.zero;
                entry.hPin.localRotation = Quaternion.Euler(0f, 0f, -hAngle);
            }

            bool vVisible = Mathf.Abs(vAngle) <= maxAngle;
            entry.vPin.gameObject.SetActive(vVisible);
            if (vVisible)
            {
                entry.vPin.anchoredPosition = Vector2.zero;
                entry.vPin.localRotation = Quaternion.Euler(0f, 0f, vAngle);
            }

            var ct = entry.target.GetComponent<CompassTarget>();
            Color pinColor = ct != null ? ct.displayColor : Color.white;
            var hImg = entry.hPin.GetComponent<Image>();
            if (hImg != null) hImg.color = pinColor;
            var vImg = entry.vPin.GetComponent<Image>();
            if (vImg != null) vImg.color = pinColor;
        }

        // Update dedicated yellow STAR pins
        if (_nearestStar != null)
        {
            Vector3 toStar = _nearestStar.position - ship.transform.position;
            if (toStar.sqrMagnitude > 0.01f)
            {
                Vector3 starDir = toStar.normalized;
                Vector3 starHoriz = new Vector3(starDir.x, 0f, starDir.z);
                if (starHoriz.sqrMagnitude > 0.001f) starHoriz.Normalize();
                float starHAngle = Vector3.SignedAngle(forward, starHoriz, Vector3.up);
                float starVAngle = Mathf.Asin(Mathf.Clamp(starDir.y, -1f, 1f)) * Mathf.Rad2Deg;

                if (starHorizontalPin != null)
                {
                    bool vis = Mathf.Abs(starHAngle) <= maxAngle;
                    starHorizontalPin.gameObject.SetActive(vis);
                    if (vis)
                    {
                        starHorizontalPin.anchoredPosition = Vector2.zero;
                        starHorizontalPin.localRotation = Quaternion.Euler(0f, 0f, -starHAngle);
                        var img = starHorizontalPin.GetComponent<Image>();
                        if (img != null) img.color = Color.yellow;
                    }
                }
                if (starVerticalPin != null)
                {
                    bool vis = Mathf.Abs(starVAngle) <= maxAngle;
                    starVerticalPin.gameObject.SetActive(vis);
                    if (vis)
                    {
                        starVerticalPin.anchoredPosition = Vector2.zero;
                        starVerticalPin.localRotation = Quaternion.Euler(0f, 0f, starVAngle);
                        var img = starVerticalPin.GetComponent<Image>();
                        if (img != null) img.color = Color.yellow;
                    }
                }
            }
        }
        else
        {
            if (starHorizontalPin != null) starHorizontalPin.gameObject.SetActive(false);
            if (starVerticalPin != null) starVerticalPin.gameObject.SetActive(false);
        }

        // Label: show nearest target (non-star preferred, or star)
        if (targetLabel != null)
        {
            if (_pins.Count > 0 && _pins[0].target != null)
            {
                targetLabel.gameObject.SetActive(true);
                var ct = _pins[0].target.GetComponent<CompassTarget>();
                string name = ct != null && !string.IsNullOrEmpty(ct.displayName) ? ct.displayName : _pins[0].target.name;
                float dist = Vector3.Distance(ship.transform.position, _pins[0].target.position);
                targetLabel.text = name + "  " + Mathf.RoundToInt(dist) + "m";
            }
            else if (_nearestStar != null)
            {
                targetLabel.gameObject.SetActive(true);
                float dist = Vector3.Distance(ship.transform.position, _nearestStar.position);
                targetLabel.text = "Star  " + Mathf.RoundToInt(dist) + "m";
            }
            else
            {
                targetLabel.gameObject.SetActive(false);
            }
        }
    }

    private void SyncPins(List<Transform> targets)
    {
        for (int i = _pins.Count - 1; i >= 0; i--)
        {
            if (_pins[i].target == null || !targets.Contains(_pins[i].target))
            {
                if (_pins[i].hPin != null) Destroy(_pins[i].hPin.gameObject);
                if (_pins[i].vPin != null) Destroy(_pins[i].vPin.gameObject);
                _pins.RemoveAt(i);
            }
        }

        foreach (var t in targets)
        {
            bool exists = false;
            foreach (var entry in _pins)
            {
                if (entry.target == t) { exists = true; break; }
            }
            if (exists) continue;

            RectTransform hPin = null;
            if (horizontalPin != null)
            {
                var hGO = Instantiate(horizontalPin.gameObject, horizontalPin.parent);
                hGO.SetActive(true);
                hPin = hGO.GetComponent<RectTransform>();
            }

            RectTransform vPin = null;
            if (verticalPin != null)
            {
                var vGO = Instantiate(verticalPin.gameObject, verticalPin.parent);
                vGO.SetActive(true);
                vPin = vGO.GetComponent<RectTransform>();
            }

            _pins.Add(new PinEntry { target = t, hPin = hPin, vPin = vPin });
        }
    }
}
