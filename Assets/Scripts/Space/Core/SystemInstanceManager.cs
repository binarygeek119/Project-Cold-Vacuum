using UnityEngine;
using System.Collections.Generic;

namespace Space.Core
{
    /// <summary>
    /// Singleton manager for all active star system instances.
    /// Handles system loading/unloading and provides access to active systems.
    /// </summary>
    public class SystemInstanceManager : MonoBehaviour
    {
        public static SystemInstanceManager Instance { get; private set; }
        
        [Header("Configuration")]
        [SerializeField] private int maxActiveSystems = 5;
        [SerializeField] private float systemLoadDistanceKm = 100000000000f; // 100 billion km (~0.01 light-years)
        private float systemUnloadDistanceKm = 150000000000f; // 150 billion km (~0.016 light-years)
        
        // Active systems
        private Dictionary<ulong, SystemInstance> activeSystems = new Dictionary<ulong, SystemInstance>();
        private FloatingOrigin floatingOrigin;
        private Transform shipTransform;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            floatingOrigin = FindAnyObjectByType<FloatingOrigin>();
            shipTransform = floatingOrigin != null ? floatingOrigin.ShipTransform : null;
        }
        
        void Update()
        {
            if (shipTransform == null) return;
            
            UpdateSystemStreaming();
        }
        
        private void UpdateSystemStreaming()
        {
            Vector3 shipPosKm = floatingOrigin.GetShipPositionKm();
            
            // Check for systems to load/unload
            // In a real implementation, this would check against a universe map
            // For now, we'll just manage currently active systems
        }
        
        /// <summary>
        /// Get or create a system instance
        /// </summary>
        public SystemInstance GetOrCreateSystem(ulong systemSeed)
        {
            if (activeSystems.TryGetValue(systemSeed, out SystemInstance existing))
            {
                return existing;
            }
            
            // Check if we need to unload an old system
            if (activeSystems.Count >= maxActiveSystems)
            {
                UnloadFarthestSystem();
            }
            
            // Create new system
            GameObject systemObj = new GameObject($"System_{systemSeed}");
            systemObj.transform.SetParent(transform);
            
            SystemInstance instance = systemObj.AddComponent<SystemInstance>();
            instance.Initialize(systemSeed);
            
            activeSystems[systemSeed] = instance;
            
            Debug.Log($"Loaded system {systemSeed} ({instance.GetSystemData().star.name})");
            
            return instance;
        }
        
        /// <summary>
        /// Get active system by seed
        /// </summary>
        public SystemInstance GetSystem(ulong systemSeed)
        {
            activeSystems.TryGetValue(systemSeed, out SystemInstance instance);
            return instance;
        }
        
        /// <summary>
        /// Unload a specific system
        /// </summary>
        public void UnloadSystem(ulong systemSeed)
        {
            if (activeSystems.TryGetValue(systemSeed, out SystemInstance instance))
            {
                instance.Cleanup();
                Destroy(instance.gameObject);
                activeSystems.Remove(systemSeed);
                
                Debug.Log($"Unloaded system {systemSeed}");
            }
        }
        
        /// <summary>
        /// Unload the farthest system from ship
        /// </summary>
        private void UnloadFarthestSystem()
        {
            if (activeSystems.Count == 0) return;
            
            Vector3 shipPosKm = floatingOrigin.GetShipPositionKm();
            float maxDist = 0f;
            ulong farthestSeed = 0;
            
            foreach (var kvp in activeSystems)
            {
                Vector3 systemPos = kvp.Value.GetStarTransform().position;
                float dist = Vector3.Distance(shipPosKm, systemPos);
                
                if (dist > maxDist)
                {
                    maxDist = dist;
                    farthestSeed = kvp.Key;
                }
            }
            
            UnloadSystem(farthestSeed);
        }
        
        /// <summary>
        /// Get all active systems
        /// </summary>
        public SystemInstance[] GetActiveSystems()
        {
            SystemInstance[] systems = new SystemInstance[activeSystems.Count];
            activeSystems.Values.CopyTo(systems, 0);
            return systems;
        }
        
        /// <summary>
        /// Get count of active systems
        /// </summary>
        public int GetActiveSystemCount()
        {
            return activeSystems.Count;
        }
        
        /// <summary>
        /// Find system containing position
        /// </summary>
        public SystemInstance FindSystemAtPosition(Vector3 positionKm)
        {
            foreach (var kvp in activeSystems)
            {
                SystemInstance system = kvp.Value;
                Vector3 starPos = system.GetStarTransform().position;
                float dist = Vector3.Distance(positionKm, starPos);
                
                // Check if within system bounds (simplified - real impl would use actual system radius)
                if (dist < systemLoadDistanceKm)
                {
                    return system;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Find nearest system to position
        /// </summary>
        public SystemInstance FindNearestSystem(Vector3 positionKm)
        {
            SystemInstance nearest = null;
            float minDist = float.MaxValue;
            
            foreach (var kvp in activeSystems)
            {
                SystemInstance system = kvp.Value;
                Vector3 starPos = system.GetStarTransform().position;
                float dist = Vector3.Distance(positionKm, starPos);
                
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = system;
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// Get current system (where ship is located)
        /// </summary>
        public SystemInstance GetCurrentSystem()
        {
            if (shipTransform == null) return null;
            return FindSystemAtPosition(floatingOrigin.GetShipPositionKm());
        }
        
        /// <summary>
        /// Check if ship is currently inside a star system
        /// </summary>
        public bool IsInStarSystem()
        {
            return GetCurrentSystem() != null;
        }
        
        /// <summary>
        /// Get the ship transform
        /// </summary>
        public Transform GetShip()
        {
            return shipTransform;
        }
        
        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
