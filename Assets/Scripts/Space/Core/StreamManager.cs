using UnityEngine;
using System.Collections.Generic;

namespace Space.Core
{
    /// <summary>
    /// Manages distance-based streaming of celestial bodies with hysteresis and pooling.
    /// Implements tiered streaming: NearLocal (0-5km) → FarOrbital (5-1000km) → FarImpostor (1000km+)
    /// </summary>
    public class StreamManager : MonoBehaviour
    {
        public static StreamManager Instance { get; private set; }
        
        [Header("Streaming Configuration")]
        [SerializeField] private float nearLocalRadius = 5000f; // 5 km in meters
        [SerializeField] private float farOrbitalRadius = 1000000f; // 1000 km in meters
        [SerializeField] private float impostorRadius = 10000000f; // 10000 km in meters
        
        [Header("Hysteresis")]
        [SerializeField] private float loadHysteresis = 0.9f; // Load at 90% of threshold
        [SerializeField] private float unloadHysteresis = 1.1f; // Unload at 110% of threshold
        
        [Header("Budget")]
        [SerializeField] private int maxActiveNodes = 600;
        [SerializeField] private int maxMeshNodes = 100;
        [SerializeField] private int maxImpostorNodes = 200;
        
        [Header("Fade Settings")]
        [SerializeField] private float fadeDuration = 0.4f;
        
        // Streaming state
        private Camera mainCamera;
        private List<StreamableBody> activeBodies = new List<StreamableBody>();
        private Dictionary<int, StreamableBody> bodyLookup = new Dictionary<int, StreamableBody>();
        
        // Pooling
        private PoolRegistry poolRegistry;
        
        // Statistics
        private int meshCount;
        private int impostorCount;
        private int pointCount;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            mainCamera = Camera.main;
            poolRegistry = GetComponent<PoolRegistry>();
            
            if (poolRegistry == null)
            {
                poolRegistry = gameObject.AddComponent<PoolRegistry>();
            }
        }
        
        void Update()
        {
            if (mainCamera == null) return;
            
            UpdateStreaming();
            UpdateFadeTransitions();
            UpdateStats();
        }
        
        private void UpdateStreaming()
        {
            Vector3 cameraPos = mainCamera.transform.position;
            
            // Check existing bodies for unloading
            for (int i = activeBodies.Count - 1; i >= 0; i--)
            {
                StreamableBody body = activeBodies[i];
                if (body == null || body.BodyObject == null)
                {
                    activeBodies.RemoveAt(i);
                    continue;
                }
                
                float distance = Vector3.Distance(cameraPos, body.BodyObject.transform.position);
                StreamTier currentTier = body.CurrentTier;
                StreamTier targetTier = CalculateTier(distance);
                
                // Apply hysteresis
                if (ShouldUnload(currentTier, targetTier, distance))
                {
                    UnloadBody(body);
                    activeBodies.RemoveAt(i);
                }
                else if (currentTier != targetTier)
                {
                    // Transition to new tier
                    TransitionBody(body, targetTier);
                }
            }
            
            // Request new bodies from SystemInstanceManager
            RequestNewBodies(cameraPos);
        }
        
        private StreamTier CalculateTier(float distanceMeters)
        {
            if (distanceMeters <= nearLocalRadius) return StreamTier.NearLocal;
            if (distanceMeters <= farOrbitalRadius) return StreamTier.FarOrbital;
            if (distanceMeters <= impostorRadius) return StreamTier.FarImpostor;
            return StreamTier.None;
        }
        
        private bool ShouldUnload(StreamTier current, StreamTier target, float distance)
        {
            // Unload if target is None
            if (target == StreamTier.None) return true;
            
            // Apply hysteresis based on tier boundaries
            switch (current)
            {
                case StreamTier.NearLocal:
                    return distance > nearLocalRadius * unloadHysteresis && target != StreamTier.NearLocal;
                case StreamTier.FarOrbital:
                    return (distance > farOrbitalRadius * unloadHysteresis && target != StreamTier.FarOrbital) ||
                           (distance < nearLocalRadius * loadHysteresis && target == StreamTier.NearLocal);
                case StreamTier.FarImpostor:
                    return distance > impostorRadius * unloadHysteresis ||
                           (distance < farOrbitalRadius * loadHysteresis && target == StreamTier.FarOrbital);
            }
            
            return false;
        }
        
        private void TransitionBody(StreamableBody body, StreamTier newTier)
        {
            body.BeginFadeOut(fadeDuration);
            body.TargetTier = newTier;
            body.PendingTransition = true;
        }
        
        private void UnloadBody(StreamableBody body)
        {
            if (body.BodyObject != null)
            {
                poolRegistry.ReturnToPool(body.BodyType, body.BodyObject);
            }
            bodyLookup.Remove(body.BodyId);
        }
        
        private void RequestNewBodies(Vector3 cameraPos)
        {
            // Get nearby systems from SystemInstanceManager
            var systemManager = SystemInstanceManager.Instance;
            if (systemManager == null) return;
            
            var systems = systemManager.GetActiveSystems();
            foreach (var system in systems)
            {
                if (system == null) continue;
                
                var systemData = system.GetSystemData();
                Vector3 starPos = system.GetStarTransform().position;
                float starDist = Vector3.Distance(cameraPos, starPos);
                
                // Only process systems within impostor radius
                if (starDist > impostorRadius * 1.5f) continue;
                
                // Process planets
                var planets = system.GetPlanetTransforms();
                for (int i = 0; i < planets.Length && i < systemData.planets.Length; i++)
                {
                    if (planets[i] == null) continue;
                    
                    ProcessBody(planets[i].position, BodyType.Planet, systemData.planets[i], system);
                }
                
                // Process star itself
                ProcessBody(starPos, BodyType.Star, null, system);
            }
        }
        
        private void ProcessBody(Vector3 position, BodyType bodyType, object bodyData, SystemInstance system)
        {
            float distance = Vector3.Distance(mainCamera.transform.position, position);
            StreamTier tier = CalculateTier(distance);
            
            if (tier == StreamTier.None) return;
            
            // Check budget
            if (!CanSpawnInTier(tier)) return;
            
            // Check if already streaming
            int bodyId = position.GetHashCode();
            if (bodyLookup.ContainsKey(bodyId)) return;
            
            // Spawn new body
            SpawnBody(position, bodyType, tier, bodyData, system, bodyId);
        }
        
        private bool CanSpawnInTier(StreamTier tier)
        {
            switch (tier)
            {
                case StreamTier.NearLocal:
                    return meshCount < maxMeshNodes && activeBodies.Count < maxActiveNodes;
                case StreamTier.FarOrbital:
                    return impostorCount < maxImpostorNodes && activeBodies.Count < maxActiveNodes;
                case StreamTier.FarImpostor:
                    return activeBodies.Count < maxActiveNodes;
            }
            return false;
        }
        
        private void SpawnBody(Vector3 position, BodyType bodyType, StreamTier tier, 
            object bodyData, SystemInstance system, int bodyId)
        {
            GameObject bodyObj = poolRegistry.GetFromPool(bodyType, tier);
            if (bodyObj == null) return;
            
            bodyObj.transform.position = position;
            bodyObj.SetActive(true);
            
            // Initialize body components
            var streamable = bodyObj.GetComponent<StreamableBody>();
            if (streamable == null)
            {
                streamable = bodyObj.AddComponent<StreamableBody>();
            }
            
            streamable.Initialize(bodyId, bodyType, tier, bodyObj, bodyData, system);
            
            activeBodies.Add(streamable);
            bodyLookup[bodyId] = streamable;
        }
        
        private void UpdateFadeTransitions()
        {
            for (int i = activeBodies.Count - 1; i >= 0; i--)
            {
                StreamableBody body = activeBodies[i];
                if (body == null) continue;
                
                if (body.PendingTransition && !body.IsFading)
                {
                    // Fade complete, spawn new tier
                    SpawnBody(body.BodyObject.transform.position, body.BodyType, 
                        body.TargetTier, body.BodyData, body.System, body.BodyId);
                    UnloadBody(body);
                    activeBodies.RemoveAt(i);
                }
            }
        }
        
        private void UpdateStats()
        {
            meshCount = 0;
            impostorCount = 0;
            pointCount = 0;
            
            foreach (var body in activeBodies)
            {
                if (body == null) continue;
                
                switch (body.CurrentTier)
                {
                    case StreamTier.NearLocal:
                        meshCount++;
                        break;
                    case StreamTier.FarOrbital:
                        impostorCount++;
                        break;
                    case StreamTier.FarImpostor:
                        pointCount++;
                        break;
                }
            }
        }
        
        // Public API
        public int GetActiveBodyCount() => activeBodies.Count;
        public int GetMeshCount() => meshCount;
        public int GetImpostorCount() => impostorCount;
        public int GetPointCount() => pointCount;
        
        public StreamableBody GetBody(int bodyId)
        {
            bodyLookup.TryGetValue(bodyId, out StreamableBody body);
            return body;
        }
    }
    
    public enum StreamTier
    {
        None,
        NearLocal,
        FarOrbital,
        FarImpostor
    }
    
    public enum BodyType
    {
        Star,
        Planet,
        Moon,
        Station
    }
    
    /// <summary>
    /// Represents a streamable celestial body
    /// </summary>
    public class StreamableBody : MonoBehaviour
    {
        public int BodyId { get; private set; }
        public BodyType BodyType { get; private set; }
        public StreamTier CurrentTier { get; private set; }
        public StreamTier TargetTier { get; set; }
        public GameObject BodyObject { get; private set; }
        public object BodyData { get; private set; }
        public SystemInstance System { get; private set; }
        public bool PendingTransition { get; set; }
        public bool IsFading { get; private set; }
        
        private float fadeTimer;
        private float fadeDuration;
        private Renderer bodyRenderer;
        
        public void Initialize(int bodyId, BodyType bodyType, StreamTier tier, 
            GameObject bodyObj, object bodyData, SystemInstance system)
        {
            BodyId = bodyId;
            BodyType = bodyType;
            CurrentTier = tier;
            TargetTier = tier;
            BodyObject = bodyObj;
            BodyData = bodyData;
            System = system;
            PendingTransition = false;
            IsFading = false;
            
            bodyRenderer = bodyObj.GetComponent<Renderer>();
        }
        
        public void BeginFadeOut(float duration)
        {
            IsFading = true;
            fadeDuration = duration;
            fadeTimer = 0f;
        }
        
        void Update()
        {
            if (IsFading)
            {
                fadeTimer += Time.deltaTime;
                float alpha = 1f - (fadeTimer / fadeDuration);
                
                if (bodyRenderer != null)
                {
                    Color color = bodyRenderer.material.color;
                    color.a = Mathf.Clamp01(alpha);
                    bodyRenderer.material.color = color;
                }
                
                if (fadeTimer >= fadeDuration)
                {
                    IsFading = false;
                }
            }
        }
    }
}
