using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 3D compass (Skyrim-style) with horizontal bar (top) and vertical bar (left).
/// Bars are fixed in place; pins rotate like gauge needles to point at targets.
/// Supports MULTIPLE pins (one per target in the current star system).
/// Each pin is colored to match its target object.
/// </summary>
public class CompassHUD3D : MonoBehaviour
{
    public RawImage horizontalBar;
    public RawImage verticalBar;
    public RectTransform horizontalPinTemplate;  // template (hidden) for spawning pins
    public RectTransform verticalPinTemplate;
    public Text targetLabel;

    public float maxAngle = 90f;
    public float barWidth = 400f;
    public float barHeight = 400f;

    private SpaceStreamer _streamer;
    private List<PinEntry> _pins = new List<PinEntry>();

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
        if (horizontalPinTemplate != null) horizontalPinTemplate.gameObject.SetActive(false);
        if (verticalPinTemplate != null) verticalPinTemplate.gameObject.SetActive(false);
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

        // Sync pins: create new, remove stale
        SyncPins(targets);

        // If no targets (blank space), hide all pins and label
        if (targets.Count == 0)
        {
            foreach (var entry in _pins)
            {
                if (entry.hPin != null) entry.hPin.gameObject.SetActive(false);
                if (entry.vPin != null) entry.vPin.gameObject.SetActive(false);
            }
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

            // Horizontal angle
            Vector3 horizDir = new Vector3(dir.x, 0f, dir.z);
            if (horizDir.sqrMagnitude > 0.001f) horizDir.Normalize();
            float hAngle = Vector3.SignedAngle(forward, horizDir, Vector3.up);

            // Vertical angle
            float vAngle = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            // Horizontal pin: rotate like gauge needle
            bool hVisible = Mathf.Abs(hAngle) <= maxAngle;
            entry.hPin.gameObject.SetActive(hVisible);
            if (hVisible)
            {
                entry.hPin.anchoredPosition = Vector2.zero;
                entry.hPin.localRotation = Quaternion.Euler(0f, 0f, -hAngle);
            }

            // Vertical pin
            bool vVisible = Mathf.Abs(vAngle) <= maxAngle;
            entry.vPin.gameObject.SetActive(vVisible);
            if (vVisible)
            {
                entry.vPin.anchoredPosition = Vector2.zero;
                entry.vPin.localRotation = Quaternion.Euler(0f, 0f, vAngle);
            }

            // Color
            var ct = entry.target.GetComponent<CompassTarget>();
            Color pinColor = ct != null ? ct.displayColor : Color.white;
            var hImg = entry.hPin.GetComponent<Image>();
            if (hImg != null) hImg.color = pinColor;
            var vImg = entry.vPin.GetComponent<Image>();
            if (vImg != null) vImg.color = pinColor;
        }

        // Label: show nearest target
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
            else
            {
                targetLabel.gameObject.SetActive(false);
            }
        }
    }

    private void SyncPins(List<Transform> targets)
    {
        // Remove pins for targets no longer in list
        for (int i = _pins.Count - 1; i >= 0; i--)
        {
            if (_pins[i].target == null || !targets.Contains(_pins[i].target))
            {
                if (_pins[i].hPin != null) Destroy(_pins[i].hPin.gameObject);
                if (_pins[i].vPin != null) Destroy(_pins[i].vPin.gameObject);
                _pins.RemoveAt(i);
            }
        }

        // Create pins for new targets
        foreach (var t in targets)
        {
            bool exists = false;
            foreach (var entry in _pins)
            {
                if (entry.target == t) { exists = true; break; }
            }
            if (exists) continue;

            // Create horizontal pin from template
            RectTransform hPin = null;
            if (horizontalPinTemplate != null)
            {
                var hGO = Instantiate(horizontalPinTemplate.gameObject, horizontalPinTemplate.parent);
                hGO.SetActive(true);
                hPin = hGO.GetComponent<RectTransform>();
            }

            // Create vertical pin from template
            RectTransform vPin = null;
            if (verticalPinTemplate != null)
            {
                var vGO = Instantiate(verticalPinTemplate.gameObject, verticalPinTemplate.parent);
                vGO.SetActive(true);
                vPin = vGO.GetComponent<RectTransform>();
            }

            _pins.Add(new PinEntry { target = t, hPin = hPin, vPin = vPin });
        }
    }
}