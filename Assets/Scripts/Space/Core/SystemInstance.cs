using UnityEngine;
using System;

namespace Space.Core
{
    /// <summary>
    /// Runtime instance of a generated star system. Manages bodies, time, and streaming.
    /// </summary>
    public class SystemInstance : MonoBehaviour
    {
        [Header("System Data")]
        [SerializeField] private ulong systemSeed;
        [SerializeField] private SystemData systemData;
        
        [Header("Time")]
        [SerializeField] private double systemTime;
        [SerializeField] private float timeScale = 1.0f;
        
        [Header("Streaming")]
        private float loadRadiusKm = 1000000f; // 1 million km
        private float unloadRadiusKm = 1500000f; // 1.5 million km
        
        // Runtime state
        private bool isInitialized;
        private GameObject starRoot;
        private OrbitalBody[] planetBodies;
        private OrbitalBody[][] moonBodies;
        private GameObject[] planetObjects;
        
        /// <summary>
        /// Initialize system from seed
        /// </summary>
        public void Initialize(ulong seed)
        {
            systemSeed = seed;
            systemData = SystemGen.Generate(seed);
            systemTime = 0.0;
            
            CreateSystemObjects();
            isInitialized = true;
        }
        
        /// <summary>
        /// Initialize from existing system data (for save/load)
        /// </summary>
        public void Initialize(SystemData data, double time)
        {
            systemData = data;
            systemSeed = data.systemSeed;
            systemTime = time;
            
            CreateSystemObjects();
            isInitialized = true;
        }
        
        private void CreateSystemObjects()
        {
            // Create star
            starRoot = new GameObject($"Star_{systemData.star.name}");
            starRoot.transform.SetParent(transform);
            starRoot.transform.localPosition = Vector3.zero;
            
            // Create orbital bodies for planets
            planetBodies = new OrbitalBody[systemData.planets.Length];
            moonBodies = new OrbitalBody[systemData.planets.Length][];
            planetObjects = new GameObject[systemData.planets.Length];
            
            for (int i = 0; i < systemData.planets.Length; i++)
            {
                var planet = systemData.planets[i];
                
                // Create planet gameobject
                planetObjects[i] = new GameObject($"Planet_{planet.name}");
                planetObjects[i].transform.SetParent(starRoot.transform);
                
                // Add orbital body component
                planetBodies[i] = planetObjects[i].AddComponent<OrbitalBody>();
                planetBodies[i].InitializeFromPlanet(planet);
                
                // Create moons
                moonBodies[i] = new OrbitalBody[planet.moons.Length];
                for (int j = 0; j < planet.moons.Length; j++)
                {
                    var moon = planet.moons[j];
                    GameObject moonObj = new GameObject($"Moon_{moon.name}");
                    moonObj.transform.SetParent(planetObjects[i].transform);
                    
                    moonBodies[i][j] = moonObj.AddComponent<OrbitalBody>();
                    moonBodies[i][j].InitializeFromMoon(moon);
                }
            }
            
            UpdatePositions(systemTime);
        }
        
        void Update()
        {
            if (!isInitialized) return;
            
            // Advance system time
            systemTime += Time.deltaTime * timeScale;
            
            // Update all body positions
            UpdatePositions(systemTime);
        }
        
        private void UpdatePositions(double time)
        {
            // Update planet positions
            for (int i = 0; i < planetBodies.Length; i++)
            {
                if (planetBodies[i] != null)
                {
                    double3 pos = planetBodies[i].PositionAt(time);
                    planetObjects[i].transform.localPosition = pos;
                    
                    // Update moon positions relative to planet
                    for (int j = 0; j < moonBodies[i].Length; j++)
                    {
                        if (moonBodies[i][j] != null)
                        {
                            double3 moonPos = moonBodies[i][j].PositionAt(time);
                            moonBodies[i][j].transform.localPosition = moonPos;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Set time scale (1.0 = real-time, 1000.0 = fast-forward)
        /// </summary>
        public void SetTimeScale(float scale)
        {
            timeScale = Mathf.Max(0.0f, scale);
        }
        
        /// <summary>
        /// Set absolute system time
        /// </summary>
        public void SetSystemTime(double time)
        {
            systemTime = time;
            UpdatePositions(systemTime);
        }
        
        /// <summary>
        /// Get current system time
        /// </summary>
        public double GetSystemTime()
        {
            return systemTime;
        }
        
        /// <summary>
        /// Get system data
        /// </summary>
        public SystemData GetSystemData()
        {
            return systemData;
        }
        
        /// <summary>
        /// Get system seed
        /// </summary>
        public ulong GetSystemSeed()
        {
            return systemSeed;
        }
        
        /// <summary>
        /// Get star transform
        /// </summary>
        public Transform GetStarTransform()
        {
            return starRoot != null ? starRoot.transform : null;
        }
        
        /// <summary>
        /// Get planet transforms
        /// </summary>
        public Transform[] GetPlanetTransforms()
        {
            Transform[] transforms = new Transform[planetObjects.Length];
            for (int i = 0; i < planetObjects.Length; i++)
            {
                transforms[i] = planetObjects[i] != null ? planetObjects[i].transform : null;
            }
            return transforms;
        }
        
        /// <summary>
        /// Find nearest planet to position
        /// </summary>
        public int FindNearestPlanet(Vector3 positionKm)
        {
            int nearest = -1;
            float minDist = float.MaxValue;
            
            for (int i = 0; i < planetObjects.Length; i++)
            {
                if (planetObjects[i] != null)
                {
                    float dist = Vector3.Distance(positionKm, planetObjects[i].transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = i;
                    }
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// Cleanup system
        /// </summary>
        public void Cleanup()
        {
            if (starRoot != null)
            {
                Destroy(starRoot);
            }
            
            isInitialized = false;
        }
        
        void OnDestroy()
        {
            Cleanup();
        }
    }
}
