using UnityEngine;
using System.Collections.Generic;

namespace Space.Core
{
    /// <summary>
    /// Generates leap travel content (nebulae, asteroids, derelicts) between star systems.
    /// Uses seeded RNG based on spatial position for deterministic generation.
    /// </summary>
    public class LeapTravelGen : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private float chunkSizeKm = 100000000f; // 100 million km chunks
        [SerializeField] private int maxNebulaePerChunk = 4;
        [SerializeField] private int maxAsteroidsPerChunk = 20;
        [SerializeField] private int maxDerelictsPerChunk = 2;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject[] nebulaPrefabs;
        [SerializeField] private GameObject[] asteroidPrefabs;
        [SerializeField] private GameObject[] derelictPrefabs;
        
        [Header("Spawn Settings")]
        [SerializeField] private float nebulaMinScale = 10000f;
        [SerializeField] private float nebulaMaxScale = 50000f;
        [SerializeField] private float asteroidMinScale = 10f;
        [SerializeField] private float asteroidMaxScale = 500f;
        [SerializeField] private float derelictMinScale = 50f;
        [SerializeField] private float derelictMaxScale = 200f;
        
        // Tracking
        private Dictionary<Vector3Int, LeapTravelChunk> activeLeapTravelChunks = new Dictionary<Vector3Int, LeapTravelChunk>();
        private SystemInstanceManager systemManager;
        
        void Start()
        {
            systemManager = FindAnyObjectByType<SystemInstanceManager>();
        }
        
        void Update()
        {
            UpdateLeapTravelContent();
        }
        
        private void UpdateLeapTravelContent()
        {
            if (systemManager == null) return;
            
            // Only generate if not in a star system
            if (systemManager.IsInStarSystem()) return;
            
            Transform ship = systemManager.GetShip();
            if (ship == null) return;
            
            Vector3 shipPos = ship.position;
            
            // Calculate current chunk
            Vector3Int currentChunk = PositionToChunk(shipPos);
            
            // Generate chunks around ship
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int chunkCoord = currentChunk + new Vector3Int(x, y, z);
                        
                        if (!activeLeapTravelChunks.ContainsKey(chunkCoord))
                        {
                            GenerateChunk(chunkCoord);
                        }
                    }
                }
            }
            
            // Unload distant chunks
            UnloadDistantChunks(currentChunk);
        }
        
        private Vector3Int PositionToChunk(Vector3 position)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / chunkSizeKm),
                Mathf.FloorToInt(position.y / chunkSizeKm),
                Mathf.FloorToInt(position.z / chunkSizeKm)
            );
        }
        
        private Vector3 ChunkToWorld(Vector3Int chunkCoord)
        {
            return new Vector3(
                chunkCoord.x * chunkSizeKm,
                chunkCoord.y * chunkSizeKm,
                chunkCoord.z * chunkSizeKm
            );
        }
        
        private void GenerateChunk(Vector3Int chunkCoord)
        {
            // Generate seed from chunk coordinates
            ulong seed = (ulong)Seeds.Hash64(
                (ulong)chunkCoord.x,
                (ulong)chunkCoord.y,
                (ulong)chunkCoord.z
            );
            
            SeededRng rng = new SeededRng(seed);
            
            LeapTravelChunk chunk = new LeapTravelChunk();
            chunk.ChunkCoord = chunkCoord;
            chunk.WorldPos = ChunkToWorld(chunkCoord);
            
            Vector3 chunkCenter = chunk.WorldPos + Vector3.one * (chunkSizeKm * 0.5f);
            
            // Generate nebulae
            int nebulaCount = rng.Range(0, maxNebulaePerChunk + 1);
            for (int i = 0; i < nebulaCount; i++)
            {
                if (nebulaPrefabs == null || nebulaPrefabs.Length == 0) break;
                
                GameObject prefab = nebulaPrefabs[rng.Range(0, nebulaPrefabs.Length)];
                Vector3 pos = chunkCenter + RandomOffset(ref rng, chunkSizeKm * 0.4f);
                float scale = rng.Range(nebulaMinScale, nebulaMaxScale);
                
                GameObject nebula = Instantiate(prefab, pos, RandomRotation(ref rng));
                nebula.transform.localScale = Vector3.one * scale;
                nebula.transform.SetParent(transform);
                
                chunk.Nebulae.Add(nebula);
            }
            
            // Generate asteroids
            int asteroidCount = rng.Range(0, maxAsteroidsPerChunk + 1);
            for (int i = 0; i < asteroidCount; i++)
            {
                if (asteroidPrefabs == null || asteroidPrefabs.Length == 0) break;
                
                GameObject prefab = asteroidPrefabs[rng.Range(0, asteroidPrefabs.Length)];
                Vector3 pos = chunkCenter + RandomOffset(ref rng, chunkSizeKm * 0.45f);
                float scale = rng.Range(asteroidMinScale, asteroidMaxScale);
                
                GameObject asteroid = Instantiate(prefab, pos, RandomRotation(ref rng));
                asteroid.transform.localScale = Vector3.one * scale;
                asteroid.transform.SetParent(transform);
                
                // Add orbital motion
                var motion = asteroid.AddComponent<AsteroidMotion>();
                motion.InitializeOrbit(
                    chunkCenter,
                    rng.Range(0.01f, 0.1f), // Slow orbital speed
                    rng.OnUnitSphere(), // Random orbit axis
                    ref rng
                );
                
                chunk.Asteroids.Add(asteroid);
            }
            
            // Generate derelicts
            int derelictCount = rng.Range(0, maxDerelictsPerChunk + 1);
            for (int i = 0; i < derelictCount; i++)
            {
                if (derelictPrefabs == null || derelictPrefabs.Length == 0) break;
                
                GameObject prefab = derelictPrefabs[rng.Range(0, derelictPrefabs.Length)];
                Vector3 pos = chunkCenter + RandomOffset(ref rng, chunkSizeKm * 0.4f);
                float scale = rng.Range(derelictMinScale, derelictMaxScale);
                
                GameObject derelict = Instantiate(prefab, pos, RandomRotation(ref rng));
                derelict.transform.localScale = Vector3.one * scale;
                derelict.transform.SetParent(transform);
                
                chunk.Derelicts.Add(derelict);
            }
            
            activeLeapTravelChunks[chunkCoord] = chunk;
        }
        
        private Vector3 RandomOffset(ref SeededRng rng, float maxOffset)
        {
            return new Vector3(
                rng.Range(-maxOffset, maxOffset),
                rng.Range(-maxOffset, maxOffset),
                rng.Range(-maxOffset, maxOffset)
            );
        }
        
        private Quaternion RandomRotation(ref SeededRng rng)
        {
            return Quaternion.Euler(
                rng.Range(0f, 360f),
                rng.Range(0f, 360f),
                rng.Range(0f, 360f)
            );
        }
        
        private void UnloadDistantChunks(Vector3Int currentChunk)
        {
            List<Vector3Int> toRemove = new List<Vector3Int>();
            
            foreach (var kvp in activeLeapTravelChunks)
            {
                Vector3Int chunkCoord = kvp.Key;
                int distance = Mathf.Max(
                    Mathf.Abs(chunkCoord.x - currentChunk.x),
                    Mathf.Abs(chunkCoord.y - currentChunk.y),
                    Mathf.Abs(chunkCoord.z - currentChunk.z)
                );
                
                if (distance > 2) // Unload if more than 2 chunks away
                {
                    toRemove.Add(chunkCoord);
                }
            }
            
            foreach (Vector3Int chunkCoord in toRemove)
            {
                UnloadChunk(chunkCoord);
            }
        }
        
        private void UnloadChunk(Vector3Int chunkCoord)
        {
            if (!activeLeapTravelChunks.ContainsKey(chunkCoord)) return;
            
            LeapTravelChunk chunk = activeLeapTravelChunks[chunkCoord];
            
            // Destroy all objects in chunk
            foreach (var obj in chunk.Nebulae)
            {
                if (obj != null) Destroy(obj);
            }
            
            foreach (var obj in chunk.Asteroids)
            {
                if (obj != null) Destroy(obj);
            }
            
            foreach (var obj in chunk.Derelicts)
            {
                if (obj != null) Destroy(obj);
            }
            
            activeLeapTravelChunks.Remove(chunkCoord);
        }
        
        public void UnloadAllChunks()
        {
            List<Vector3Int> allChunks = new List<Vector3Int>(activeLeapTravelChunks.Keys);
            foreach (var chunkCoord in allChunks)
            {
                UnloadChunk(chunkCoord);
            }
        }
    }
    
    /// <summary>
    /// Represents a chunk of leap travel content
    /// </summary>
    public class LeapTravelChunk
    {
        public Vector3Int ChunkCoord;
        public Vector3 WorldPos;
        public List<GameObject> Nebulae = new List<GameObject>();
        public List<GameObject> Asteroids = new List<GameObject>();
        public List<GameObject> Derelicts = new List<GameObject>();
    }
}
