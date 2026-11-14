using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace Space.Debug
{
    /// <summary>
    /// Debug HUD displaying real-time space system information.
    /// Shows system seed, time, ship position, active systems, and performance stats.
    /// </summary>
    public class SpaceHud : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text hudText;
        
        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 0.5f;
        
        private Space.Core.FloatingOrigin floatingOrigin;
        private Space.Core.SystemInstanceManager systemManager;
        private float updateTimer;
        private StringBuilder sb = new StringBuilder(1024);
        
        // Performance tracking
        private int frameCount;
        private float fpsTimer;
        private float currentFps;
        
        void Start()
        {
            floatingOrigin = FindAnyObjectByType<Space.Core.FloatingOrigin>();
            systemManager = Space.Core.SystemInstanceManager.Instance;
            
            if (hudText == null)
            {
                UnityEngine.Debug.LogWarning("SpaceHud: No Text component assigned!");
            }
        }
        
        void Update()
        {
            // FPS tracking
            frameCount++;
            fpsTimer += Time.deltaTime;
            if (fpsTimer >= 1.0f)
            {
                currentFps = frameCount / fpsTimer;
                frameCount = 0;
                fpsTimer = 0.0f;
            }
            
            // Update HUD text at interval
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                UpdateHud();
                updateTimer = 0.0f;
            }
        }
        
        private void UpdateHud()
        {
            if (hudText == null) return;
            
            sb.Clear();
            
            // Performance stats
            sb.AppendLine($"<b>Performance</b>");
            sb.AppendLine($"FPS: {currentFps:F1}");
            sb.AppendLine($"Alloc: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024:F1} MB");
            sb.AppendLine();
            
            // Ship info
            if (floatingOrigin != null)
            {
                Vector3 shipPosKm = floatingOrigin.GetShipPositionKm();
                Vector3 shipPosMeters = shipPosKm * 1000.0f;
                
                sb.AppendLine($"<b>Ship Position</b>");
                sb.AppendLine($"X: {shipPosMeters.x:E3} m");
                sb.AppendLine($"Y: {shipPosMeters.y:E3} m");
                sb.AppendLine($"Z: {shipPosMeters.z:E3} m");
                sb.AppendLine($"From origin: {shipPosKm.magnitude:F2} km");
                sb.AppendLine();
            }
            
            // System info
            if (systemManager != null)
            {
                int activeCount = systemManager.GetActiveSystemCount();
                var activeSystems = systemManager.GetActiveSystems();
                var currentSystem = systemManager.GetCurrentSystem();
                
                sb.AppendLine($"<b>Systems</b>");
                sb.AppendLine($"Active: {activeCount}");
                
                if (currentSystem != null)
                {
                    var systemData = currentSystem.GetSystemData();
                    sb.AppendLine($"Current: {systemData.star.name}");
                    sb.AppendLine($"Seed: {currentSystem.GetSystemSeed()}");
                    sb.AppendLine($"Time: {currentSystem.GetSystemTime():F1} s");
                    sb.AppendLine($"Planets: {systemData.planets.Length}");
                }
                else
                {
                    sb.AppendLine($"Current: Leap Travel");
                }
                
                sb.AppendLine();
                
                // List active systems
                if (activeSystems.Length > 0)
                {
                    sb.AppendLine($"<b>Active Systems</b>");
                    foreach (var system in activeSystems)
                    {
                        var data = system.GetSystemData();
                        Vector3 starPos = system.GetStarTransform().position;
                        float distFromShip = floatingOrigin != null ? 
                            Vector3.Distance(floatingOrigin.GetShipPositionKm(), starPos) : 0f;
                        
                        sb.AppendLine($"• {data.star.name} ({distFromShip:F0} km)");
                    }
                    sb.AppendLine();
                }
            }
            
            // Floating origin info
            if (floatingOrigin != null)
            {
                Vector3 originOffset = floatingOrigin.GetOriginOffset();
                sb.AppendLine($"<b>Floating Origin</b>");
                sb.AppendLine($"Offset: {originOffset.magnitude:F2} km");
                sb.AppendLine();
            }
            
            hudText.text = sb.ToString();
        }
        
        void OnGUI()
        {
            // Optional: Draw additional debug info directly
            if (Input.GetKeyDown(KeyCode.F2))
            {
                // Toggle HUD visibility could be added here
            }
        }
    }
}
