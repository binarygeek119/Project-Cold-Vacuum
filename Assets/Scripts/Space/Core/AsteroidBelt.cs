using UnityEngine;
using System.Collections.Generic;

namespace Space.Core
{
    /// <summary>
    /// Renders asteroid belts using Graphics.RenderMeshInstanced for massive scale.
    /// Zero GameObjects per asteroid - pure GPU instancing.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class AsteroidBelt : MonoBehaviour
    {
        public const int MAX_INSTANCE_BATCH = 1023; // Unity's limit per call
        
        [Header("Belt Configuration")]
        [SerializeField] private BeltData beltData;
        [SerializeField] private Mesh asteroidMesh;
        [SerializeField] private Material asteroidMaterial;
        
        [Header("Visual Settings")]
        [SerializeField] private float minScale = 0.1f;
        [SerializeField] private float maxScale = 2.0f;
        [SerializeField] private float rotationSpeed = 0.1f;
        
        // Runtime data
        private Matrix4x4[] instanceMatrices;
        private MaterialPropertyBlock[] propertyBlocks;
        private int[] rockSeeds;
        private float[] rockRotations;
        private Vector3[] rockScales;
        private bool isInitialized = false;
        
        void Start()
        {
            InitializeBelt();
        }
        
        void Update()
        {
            if (!isInitialized) return;
            
            UpdateRotations();
            RenderBelt();
        }
        
        public void InitializeBelt()
        {
            if (asteroidMesh == null)
            {
                Debug.LogError("AsteroidBelt: No mesh assigned!");
                return;
            }
            
            if (asteroidMaterial == null)
            {
                Debug.LogError("AsteroidBelt: No material assigned!");
                return;
            }
            
            int rockCount = beltData.rockCount;
            
            // Allocate arrays
            instanceMatrices = new Matrix4x4[rockCount];
            rockSeeds = new int[rockCount];
            rockRotations = new float[rockCount];
            rockScales = new Vector3[rockCount];
            
            // Calculate number of property block batches
            int batchCount = (rockCount + MAX_INSTANCE_BATCH - 1) / MAX_INSTANCE_BATCH;
            propertyBlocks = new MaterialPropertyBlock[batchCount];
            for (int i = 0; i < batchCount; i++)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
            }
            
            // Generate asteroid positions deterministically
            SeededRng rng = new SeededRng(beltData.seed);
            GenerateAsteroids(ref rng);
            
            // Setup mesh renderer
            var meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = asteroidMesh;
            
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = asteroidMaterial;
            
            isInitialized = true;
            Debug.Log($"Initialized asteroid belt with {rockCount} rocks in {batchCount} batches");
        }
        
        private void GenerateAsteroids(ref SeededRng rng)
        {
            for (int i = 0; i < beltData.rockCount; i++)
            {
                // Generate random orbital parameters
                double angle = rng.NextDouble() * System.Math.PI * 2.0;
                double radius = beltData.innerRadiusKm + 
                               (rng.NextDouble() * (beltData.outerRadiusKm - beltData.innerRadiusKm));
                double height = (rng.NextDouble() - 0.5) * beltData.widthKm * 0.1; // Thin belt
                
                // Calculate position in belt plane
                double x = radius * System.Math.Cos(angle);
                double z = radius * System.Math.Sin(angle);
                double y = height;
                
                // Convert to local space (belt centered on transform)
                Vector3 localPos = new Vector3((float)x, (float)y, (float)z);
                
                // Random rotation
                rockRotations[i] = (float)(rng.NextDouble() * 360.0);
                
                // Random scale
                float scale = minScale + (float)(rng.NextDouble() * (maxScale - minScale));
                rockScales[i] = Vector3.one * scale;
                
                // Store seed for material variation
                rockSeeds[i] = rng.Range(0, 1000);
                
                // Create transformation matrix
                Matrix4x4 matrix = Matrix4x4.TRS(
                    localPos,
                    Quaternion.Euler(rockRotations[i], rockRotations[i] * 0.7f, rockRotations[i] * 1.3f),
                    rockScales[i]
                );
                
                instanceMatrices[i] = matrix;
            }
        }
        
        private void UpdateRotations()
        {
            float deltaTime = Time.deltaTime * rotationSpeed;
            
            for (int i = 0; i < beltData.rockCount; i++)
            {
                rockRotations[i] += deltaTime * (0.5f + (rockSeeds[i] % 100) * 0.01f);
                
                // Update matrix with new rotation
                Vector3 pos = instanceMatrices[i].GetColumn(3);
                Matrix4x4 matrix = Matrix4x4.TRS(
                    pos,
                    Quaternion.Euler(rockRotations[i], rockRotations[i] * 0.7f, rockRotations[i] * 1.3f),
                    rockScales[i]
                );
                
                instanceMatrices[i] = matrix;
            }
        }
        
        private void RenderBelt()
        {
            if (!isInitialized || asteroidMesh == null || asteroidMaterial == null) return;
            
            int rockCount = beltData.rockCount;
            
            // Use MaterialPropertyBlock for instanced rendering
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetColor("_Color", Color.gray);
            
            // Render each asteroid individually with Graphics.DrawMesh
            for (int i = 0; i < rockCount; i++)
            {
                Graphics.DrawMesh(
                    asteroidMesh,
                    instanceMatrices[i],
                    asteroidMaterial,
                    gameObject.layer,
                    null,
                    0,
                    props,
                    false,
                    false
                );
            }
        }
        
        /// <summary>
        /// Set belt data from generated system
        /// </summary>
        public void SetBeltData(BeltData data)
        {
            beltData = data;
            isInitialized = false; // Reset initialization flag
            InitializeBelt(); // Reinitialize with new data
        }
        
        /// <summary>
        /// Get rock count
        /// </summary>
        public int GetRockCount()
        {
            return isInitialized ? beltData.rockCount : 0;
        }
        
        /// <summary>
        /// Unload belt and free memory
        /// </summary>
        public void UnloadBelt()
        {
            instanceMatrices = null;
            propertyBlocks = null;
            rockSeeds = null;
            rockRotations = null;
            rockScales = null;
            isInitialized = false;
            
            Debug.Log("Asteroid belt unloaded");
        }
        
        void OnDestroy()
        {
            UnloadBelt();
        }
        
        #if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!isInitialized) return;
            
            // Visualize belt boundaries
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, (float)beltData.innerRadiusKm);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, (float)beltData.outerRadiusKm);
            
            // Show rock count
            UnityEditor.Handles.Label(transform.position, $"Rocks: {beltData.rockCount}");
        }
        #endif
    }
}
