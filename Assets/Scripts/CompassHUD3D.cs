using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 3D compass (Skyrim-style) with horizontal bar (top) and vertical bar (left).
/// Auto-detects stars and shows yellow pins. Supports filters and controller input.
/// D-pad UP: cycle filters | D-pad DOWN: toggle compose on/off
/// </summary>
public class CompassHUD3D : MonoBehaviour
{
    public RawImage horizontalBar;
    public RawImage verticalBar;
    public RectTransform horizontalPin;   // template (hidden) for spawning pins
    public RectTransform verticalPin;
    public Text targetLabel;

    public float maxAngle = 90f;
    public float barWidth = 400f;
    public float barHeight = 400f;
    public float fadeZone = 0.2f;  // 20% of bar length for edge fade
    public int maxStarsToTrack = 5;  // Track up to N closest stars

    public enum CompassFilter { Stars }

    private SpaceStreamer _streamer;
    private List<PinEntry> _pins = new List<PinEntry>();
    private bool _composeEnabled = true;
    private CompassFilter _currentFilter = CompassFilter.Stars;
    private float _inputCooldown = 0f;
    private const float INPUT_COOLDOWN_TIME = 0.3f;
    private bool _lastShouldShow = true;

    private class PinEntry
    {
        public Transform target;
        public RectTransform hPin;
        public RectTransform vPin;
        public bool isStar;
        public Color pinColor;
        public string systemCode;
        public Text hLabel;
        public Text vLabel;
    }

    void Start()
    {
        _streamer = FindAnyObjectByType<SpaceStreamer>();
        // Hide templates
        if (horizontalPin != null) horizontalPin.gameObject.SetActive(false);
        if (verticalPin != null) verticalPin.gameObject.SetActive(false);
    }

    enum FadeDirection { Horizontal, Vertical }

    string GenerateStarCode()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string code = "";
        // 3 random letters
        for (int i = 0; i < 3; i++)
            code += letters[Random.Range(0, letters.Length)];
        // 3 random numbers
        for (int i = 0; i < 3; i++)
            code += Random.Range(0, 10).ToString();
        return code;
    }

    void AddBarEdgeFade(RawImage bar, FadeDirection direction)
    {
        if (bar == null) return;

        // Create gradient texture
        Texture2D gradTex;
        int fadePixels;
        
        if (direction == FadeDirection.Horizontal)
        {
            int width = 256;
            fadePixels = Mathf.RoundToInt(width * fadeZone);
            gradTex = new Texture2D(width, 1, TextureFormat.RGBA32, false);

            for (int x = 0; x < width; x++)
            {
                float alpha = 1f;
                if (x < fadePixels)
                    alpha = (float)x / fadePixels;
                else if (x > width - fadePixels)
                    alpha = (float)(width - x) / fadePixels;

                gradTex.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }
        }
        else
        {
            int height = 256;
            fadePixels = Mathf.RoundToInt(height * fadeZone);
            gradTex = new Texture2D(1, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float alpha = 1f;
                if (y < fadePixels)
                    alpha = (float)y / fadePixels;
                else if (y > height - fadePixels)
                    alpha = (float)(height - y) / fadePixels;

                gradTex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        gradTex.Apply();
        gradTex.wrapMode = TextureWrapMode.Clamp;

        // Create overlay as sibling of bar (same parent) to avoid layout issues
        var overlayGO = new GameObject("BarFadeOverlay");
        overlayGO.transform.SetParent(bar.transform.parent, false);

        var overlayRawImage = overlayGO.AddComponent<RawImage>();
        overlayRawImage.texture = gradTex;
        overlayRawImage.color = Color.white;
        overlayRawImage.raycastTarget = false;

        // Match overlay position and size to the bar
        var overlayRT = overlayGO.GetComponent<RectTransform>();
        var barRT = bar.GetComponent<RectTransform>();
        
        // Copy bar's anchors and position
        overlayRT.anchorMin = barRT.anchorMin;
        overlayRT.anchorMax = barRT.anchorMax;
        overlayRT.anchoredPosition = barRT.anchoredPosition;
        overlayRT.sizeDelta = barRT.sizeDelta;
        overlayRT.pivot = barRT.pivot;
    }

    void Update()
    {
        HandleControllerInput();

        // Determine if compass should be visible
        bool shouldShow = _composeEnabled;

        // Debug: log state changes
        if (_lastShouldShow != shouldShow)
        {
            Debug.Log($"[CompassHUD] Compass visibility changed: {shouldShow} (enabled={_composeEnabled}, filter={_currentFilter})");
            _lastShouldShow = shouldShow;
        }

        // Show/hide bars
        if (horizontalBar != null)
        {
            bool wasActive = horizontalBar.gameObject.activeSelf;
            horizontalBar.gameObject.SetActive(shouldShow);
            if (wasActive != shouldShow)
            {
                Debug.Log($"[CompassHUD] Horizontal bar {(shouldShow ? "shown" : "hidden")}");
            }
        }
        else
        {
            Debug.LogWarning("[CompassHUD] horizontalBar is null!");
        }
        if (verticalBar != null)
        {
            bool wasActive = verticalBar.gameObject.activeSelf;
            verticalBar.gameObject.SetActive(shouldShow);
            if (wasActive != shouldShow)
            {
                Debug.Log($"[CompassHUD] Vertical bar {(shouldShow ? "shown" : "hidden")}");
            }
        }
        else
        {
            Debug.LogWarning("[CompassHUD] verticalBar is null!");
        }

        if (!shouldShow)
        {
            HideAllPins();
            return;
        }

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

        // Find targets based on current filter
        var targets = new List<TargetInfo>();

        if (_currentFilter == CompassFilter.Stars)
        {
            // Auto-detect all stars near the ship
            var allStars = FindObjectsByType<StarLOD>();
            foreach (var star in allStars)
            {
                if (star == null || star.transform == null) continue;
                float distToShip = Vector3.Distance(ship.transform.position, star.transform.position);
                // Only track stars within reasonable distance
                if (distToShip > 100000f) continue;
                targets.Add(new TargetInfo { transform = star.transform, isStar = true, distance = distToShip });
            }

            // Also check for objects with "Star" in name (fallback)
            var allObjects = FindObjectsByType<Transform>();
            foreach (var obj in allObjects)
            {
                if (obj == null) continue;
                if (obj.GetComponent<StarLOD>() != null) continue; // Already added
                if (!obj.name.Contains("Star")) continue;

                float distToShip = Vector3.Distance(ship.transform.position, obj.position);
                if (distToShip > 100000f) continue;
                targets.Add(new TargetInfo { transform = obj, isStar = true, distance = distToShip });
            }

            // Sort by distance and take closest N stars
            targets.Sort((a, b) => a.distance.CompareTo(b.distance));
            if (targets.Count > maxStarsToTrack)
                targets = targets.GetRange(0, maxStarsToTrack);
        }

        // Convert to Transform list for SyncPins
        var targetTransforms = new List<Transform>();
        foreach (var t in targets)
            targetTransforms.Add(t.transform);

        // Sync pins: create new, remove stale
        SyncPins(targetTransforms, targets);

        // Hide all pins when no targets
        if (targets.Count == 0)
        {
            HideAllPins();
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

            Color pinColor = entry.isStar ? Color.yellow : entry.pinColor;

            // Horizontal pin: slide left/right based on hAngle
            bool hVisible = Mathf.Abs(hAngle) <= maxAngle;
            entry.hPin.gameObject.SetActive(hVisible);
            if (entry.hLabel != null) entry.hLabel.gameObject.SetActive(hVisible);
            if (hVisible)
            {
                float normalizedAngle = hAngle / maxAngle;
                float xPos = normalizedAngle * (barWidth * 0.5f);
                entry.hPin.anchoredPosition = new Vector2(xPos, 0f);
                entry.hPin.localRotation = Quaternion.identity;

                // Calculate edge fade once
                float edgeFade = CalculateEdgeFade(normalizedAngle);

                // Position label below the horizontal bar
                if (entry.hLabel != null)
                {
                    entry.hLabel.rectTransform.anchoredPosition = new Vector2(xPos, -50f);
                    entry.hLabel.color = new Color(1f, 1f, 1f, edgeFade);
                }

                var hImg = entry.hPin.GetComponent<Image>();
                if (hImg != null) hImg.color = new Color(pinColor.r, pinColor.g, pinColor.b, pinColor.a * edgeFade);
            }

            // Vertical pin: slide up/down based on vAngle
            bool vVisible = Mathf.Abs(vAngle) <= maxAngle;
            entry.vPin.gameObject.SetActive(vVisible);
            if (entry.vLabel != null) entry.vLabel.gameObject.SetActive(vVisible);
            if (vVisible)
            {
                float normalizedAngle = vAngle / maxAngle;
                float yPos = normalizedAngle * (barHeight * 0.5f);
                entry.vPin.anchoredPosition = new Vector2(0f, yPos);
                entry.vPin.localRotation = Quaternion.identity;

                // Calculate edge fade once
                float edgeFade = CalculateEdgeFade(normalizedAngle);

                // Position label to the right of the vertical bar
                if (entry.vLabel != null)
                {
                    entry.vLabel.rectTransform.anchoredPosition = new Vector2(140f, yPos);
                    entry.vLabel.color = new Color(1f, 1f, 1f, edgeFade);
                }

                var vImg = entry.vPin.GetComponent<Image>();
                if (vImg != null) vImg.color = new Color(pinColor.r, pinColor.g, pinColor.b, pinColor.a * edgeFade);
            }
        }

        // Label: show nearest target
        if (targetLabel != null)
        {
            if (_pins.Count > 0 && _pins[0].target != null)
            {
                targetLabel.gameObject.SetActive(true);
                string name = _pins[0].isStar ? "Star" : _pins[0].target.name;
                float dist = Vector3.Distance(ship.transform.position, _pins[0].target.position);
                targetLabel.text = name + "  " + Mathf.RoundToInt(dist) + "m";
            }
            else
            {
                targetLabel.gameObject.SetActive(false);
            }
        }
    }

    void HandleControllerInput()
    {
        if (_inputCooldown > 0f)
        {
            _inputCooldown -= Time.deltaTime;
            return;
        }

        // D-pad UP: cycle filters
        bool dpadUp = false;
        if (Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame)
            dpadUp = true;
        if (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame)
            dpadUp = true;
            
        if (dpadUp)
        {
            _currentFilter = (CompassFilter)(((int)_currentFilter + 1) % System.Enum.GetValues(typeof(CompassFilter)).Length);
            _inputCooldown = INPUT_COOLDOWN_TIME;
            Debug.Log($"[CompassHUD] Filter changed to: {_currentFilter}");
        }

        // D-pad DOWN: toggle compose on/off
        bool dpadDown = false;
        if (Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame)
            dpadDown = true;
        if (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame)
            dpadDown = true;
            
        if (dpadDown)
        {
            _composeEnabled = !_composeEnabled;
            _inputCooldown = INPUT_COOLDOWN_TIME;
            Debug.Log($"[CompassHUD] Compose {( _composeEnabled ? "enabled" : "disabled")}");
        }
    }

    void HideAllPins()
    {
        foreach (var entry in _pins)
        {
            if (entry.hPin != null) entry.hPin.gameObject.SetActive(false);
            if (entry.vPin != null) entry.vPin.gameObject.SetActive(false);
            if (entry.hLabel != null) entry.hLabel.gameObject.SetActive(false);
            if (entry.vLabel != null) entry.vLabel.gameObject.SetActive(false);
        }
        if (targetLabel != null) targetLabel.gameObject.SetActive(false);
    }

    float CalculateEdgeFade(float normalizedPosition)
    {
        float absPos = Mathf.Abs(normalizedPosition);
        float fadeStart = 1f - fadeZone;

        if (absPos <= fadeStart)
            return 1f;
        else
            return 1f - (absPos - fadeStart) / fadeZone;
    }

    private void SyncPins(List<Transform> targets, List<TargetInfo> targetInfos)
    {
        // Remove stale pins
        for (int i = _pins.Count - 1; i >= 0; i--)
        {
            if (_pins[i].target == null || !targets.Contains(_pins[i].target))
            {
                if (_pins[i].hPin != null) Destroy(_pins[i].hPin.gameObject);
                if (_pins[i].vPin != null) Destroy(_pins[i].vPin.gameObject);
                if (_pins[i].hLabel != null) Destroy(_pins[i].hLabel.gameObject);
                if (_pins[i].vLabel != null) Destroy(_pins[i].vLabel.gameObject);
                _pins.RemoveAt(i);
            }
        }

        // Add new pins
        foreach (var info in targetInfos)
        {
            bool exists = false;
            foreach (var entry in _pins)
            {
                if (entry.target == info.transform) { exists = true; break; }
            }
            if (exists) continue;

            RectTransform hPin = null;
            Text hLabel = null;
            if (horizontalPin != null)
            {
                var hGO = Instantiate(horizontalPin.gameObject, horizontalPin.parent);
                hGO.SetActive(true);
                hPin = hGO.GetComponent<RectTransform>();

                // Create label for horizontal pin
                hLabel = CreatePinLabel(horizontalPin.parent, info.transform.name);
            }

            RectTransform vPin = null;
            Text vLabel = null;
            if (verticalPin != null)
            {
                var vGO = Instantiate(verticalPin.gameObject, verticalPin.parent);
                vGO.SetActive(true);
                vPin = vGO.GetComponent<RectTransform>();

                // Create label for vertical pin
                vLabel = CreatePinLabel(verticalPin.parent, info.transform.name);
            }

            _pins.Add(new PinEntry
            {
                target = info.transform,
                hPin = hPin,
                vPin = vPin,
                isStar = info.isStar,
                pinColor = info.isStar ? Color.yellow : Color.white,
                systemCode = GenerateStarCode(),
                hLabel = hLabel,
                vLabel = vLabel
            });
        }
    }

    Text CreatePinLabel(Transform parent, string targetName)
    {
        var labelGO = new GameObject("PinLabel");
        labelGO.transform.SetParent(parent, false);

        var label = labelGO.AddComponent<Text>();
        label.text = GenerateStarCode();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 32;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(200, 50);
        labelRT.anchoredPosition = Vector2.zero;

        return label;
    } // end CreatePinLabel

    class TargetInfo
    {
        public Transform transform;
        public bool isStar;
        public float distance;
    }
}
