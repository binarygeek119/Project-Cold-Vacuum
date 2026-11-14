using UnityEngine;
using System.Collections.Generic;

namespace Space.Core
{
    /// <summary>
    /// Manages object pooling for celestial bodies across different LOD tiers.
    /// Prevents runtime allocations and garbage collection spikes.
    /// </summary>
    public class PoolRegistry : MonoBehaviour
    {
        [System.Serializable]
        public class PoolConfig
        {
            public BodyType bodyType;
            public StreamTier tier;
            public GameObject prefab;
            public int initialPoolSize = 10;
            public int maxPoolSize = 50;
        }
        
        [Header("Pool Configurations")]
        [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();
        
        // Pool storage: key = (bodyType, tier)
        private Dictionary<System.Tuple<BodyType, StreamTier>, Queue<GameObject>> pools 
            = new Dictionary<System.Tuple<BodyType, StreamTier>, Queue<GameObject>>();
        
        // Track active instances
        private HashSet<GameObject> activeInstances = new HashSet<GameObject>();
        
        void Start()
        {
            InitializePools();
        }
        
        private void InitializePools()
        {
            foreach (var config in poolConfigs)
            {
                if (config.prefab == null)
                {
                    Debug.LogWarning($"PoolRegistry: No prefab assigned for {config.bodyType} at {config.tier}");
                    continue;
                }
                
                var key = System.Tuple.Create(config.bodyType, config.tier);
                
                if (!pools.ContainsKey(key))
                {
                    pools[key] = new Queue<GameObject>();
                }
                
                // Pre-populate pool
                for (int i = 0; i < config.initialPoolSize; i++)
                {
                    GameObject obj = Instantiate(config.prefab);
                    obj.SetActive(false);
                    obj.transform.SetParent(transform);
                    pools[key].Enqueue(obj);
                }
                
                Debug.Log($"Initialized pool for {config.bodyType} at {config.tier}: {config.initialPoolSize} objects");
            }
        }
        
        /// <summary>
        /// Get an object from the pool for the specified body type and tier
        /// </summary>
        public GameObject GetFromPool(BodyType bodyType, StreamTier tier)
        {
            var key = System.Tuple.Create(bodyType, tier);
            
            if (!pools.ContainsKey(key))
            {
                Debug.LogWarning($"PoolRegistry: No pool configured for {bodyType} at {tier}");
                return null;
            }
            
            Queue<GameObject> pool = pools[key];
            
            GameObject obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else
            {
                // Pool empty, create new instance
                var config = GetConfig(bodyType, tier);
                if (config == null || config.prefab == null)
                {
                    Debug.LogError($"PoolRegistry: Cannot instantiate - no config for {bodyType} at {tier}");
                    return null;
                }
                
                obj = Instantiate(config.prefab);
                obj.transform.SetParent(transform);
            }
            
            obj.SetActive(true);
            activeInstances.Add(obj);
            
            return obj;
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void ReturnToPool(BodyType bodyType, GameObject obj)
        {
            if (obj == null) return;
            
            if (!activeInstances.Contains(obj))
            {
                Debug.LogWarning("PoolRegistry: Attempting to return object that's not active");
                return;
            }
            
            // Deactivate and reset
            obj.SetActive(false);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            
            // Remove StreamableBody component data
            var streamable = obj.GetComponent<StreamableBody>();
            if (streamable != null)
            {
                Destroy(streamable);
            }
            
            // Find tier from the object's LOD component
            StreamTier tier = GetTierFromObject(obj);
            var key = System.Tuple.Create(bodyType, tier);
            
            if (!pools.ContainsKey(key))
            {
                Debug.LogWarning($"PoolRegistry: No pool for {bodyType} at {tier}, destroying object");
                Destroy(obj);
                return;
            }
            
            Queue<GameObject> pool = pools[key];
            var config = GetConfig(bodyType, tier);
            
            // Check max pool size
            if (config != null && pool.Count >= config.maxPoolSize)
            {
                Destroy(obj);
            }
            else
            {
                pool.Enqueue(obj);
            }
            
            activeInstances.Remove(obj);
        }
        
        private PoolConfig GetConfig(BodyType bodyType, StreamTier tier)
        {
            foreach (var config in poolConfigs)
            {
                if (config.bodyType == bodyType && config.tier == tier)
                {
                    return config;
                }
            }
            return null;
        }
        
        private StreamTier GetTierFromObject(GameObject obj)
        {
            // Determine tier based on LOD components
            if (obj.GetComponent<LodBody>() != null)
            {
                var lod = obj.GetComponent<LodBody>();
                if (lod.IsInMeshLod()) return StreamTier.NearLocal;
                if (lod.IsInImpostorLod()) return StreamTier.FarOrbital;
                if (lod.IsInPointLod()) return StreamTier.FarImpostor;
            }
            
            if (obj.GetComponent<LodStar>() != null)
            {
                var lod = obj.GetComponent<LodStar>();
                int tier = lod.GetCurrentLodTier();
                if (tier == 0) return StreamTier.NearLocal;
                if (tier == 1) return StreamTier.FarOrbital;
                return StreamTier.FarImpostor;
            }
            
            // Default to FarImpostor if no LOD component
            return StreamTier.FarImpostor;
        }
        
        /// <summary>
        /// Get pool statistics
        /// </summary>
        public void PrintPoolStats()
        {
            Debug.Log("=== Pool Registry Statistics ===");
            foreach (var kvp in pools)
            {
                var config = GetConfig(kvp.Key.Item1, kvp.Key.Item2);
                int maxSize = config != null ? config.maxPoolSize : 0;
                Debug.Log($"{kvp.Key.Item1} at {kvp.Key.Item2}: {kvp.Value.Count}/{maxSize} available");
            }
            Debug.Log($"Total active instances: {activeInstances.Count}");
        }
        
        /// <summary>
        /// Clear all pools (for scene transitions)
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in pools.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj != null)
                    {
                        Destroy(obj);
                    }
                }
            }
            
            pools.Clear();
            activeInstances.Clear();
            
            Debug.Log("All pools cleared");
        }
        
        void OnDestroy()
        {
            ClearAllPools();
        }
    }
}
