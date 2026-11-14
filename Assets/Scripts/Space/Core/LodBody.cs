using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Handles LOD transitions for planets, moons, and stations.
    /// Three tiers: Mesh (close) → Impostor → Point (far)
    /// Gas giants can have ring billboards in impostor tier.
    /// </summary>
    public class LodBody : MonoBehaviour
    {
        public enum BodyType
        {
            Planet,
            Moon,
            Station
        }
        
        [Header("Body Type")]
        [SerializeField] private BodyType bodyType = BodyType.Planet;
        
        [Header("LOD Thresholds (pixels)")]
        [SerializeField] private float meshThreshold = 80f;
        [SerializeField] private float impostorThreshold = 15f;
        
        [Header("References")]
        [SerializeField] private GameObject meshLod;
        [SerializeField] private GameObject impostorLod;
        [SerializeField] private GameObject pointLod;
        
        [Header("Ring (Gas Giants Only)")]
        [SerializeField] private GameObject ringBillboard;
        [SerializeField] private bool hasRings = false;
        
        [Header("Body Properties")]
        [SerializeField] private float bodyRadiusKm = 6371f; // Default: Earth radius
        
        private Camera mainCamera;
        private int currentLodTier = -1;
        private Renderer[] lodRenderers;
        
        void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("LodBody: No main camera found!");
                enabled = false;
                return;
            }
            
            // Cache renderers for each LOD
            lodRenderers = new Renderer[3];
            if (meshLod != null) lodRenderers[0] = meshLod.GetComponent<Renderer>();
            if (impostorLod != null) lodRenderers[1] = impostorLod.GetComponent<Renderer>();
            if (pointLod != null) lodRenderers[2] = pointLod.GetComponent<Renderer>();
            
            // Initialize with all LODs disabled
            SetLodTier(0);
            
            // Configure ring visibility
            if (ringBillboard != null)
            {
                ringBillboard.SetActive(hasRings);
            }
        }
        
        void LateUpdate()
        {
            if (mainCamera == null) return;
            
            UpdateLod();
        }
        
        private void UpdateLod()
        {
            // Calculate distance to camera
            float distanceMeters = Vector3.Distance(transform.position, mainCamera.transform.position) * 1000f;
            
            // Calculate angular size
            double angularSizeRad = AngularSize.CalculateAngularSize(bodyRadiusKm * 1000.0, distanceMeters);
            float pixelSize = AngularSize.CalculatePixelSize(angularSizeRad, mainCamera.fieldOfView, Screen.height);
            pixelSize = AngularSize.ClampToMinimumPixelSize(pixelSize);
            
            // Determine LOD tier
            int targetTier = AngularSize.DetermineLodTier(pixelSize, meshThreshold, impostorThreshold);
            
            // Clamp to valid range (0-2 for bodies)
            targetTier = Mathf.Clamp(targetTier, 0, 2);
            
            // Update LOD if changed
            if (targetTier != currentLodTier)
            {
                SetLodTier(targetTier);
            }
        }
        
        private void SetLodTier(int tier)
        {
            currentLodTier = tier;
            
            // Enable only the active LOD
            for (int i = 0; i < lodRenderers.Length; i++)
            {
                if (lodRenderers[i] != null)
                {
                    lodRenderers[i].enabled = (i == tier);
                    lodRenderers[i].gameObject.SetActive(i == tier);
                }
            }
            
            // Show rings only in impostor tier for gas giants
            if (ringBillboard != null && hasRings)
            {
                ringBillboard.SetActive(tier == 1); // Show in impostor tier
            }
        }
        
        /// <summary>
        /// Initialize from planet data
        /// </summary>
        public void InitializeFromPlanet(PlanetData planet)
        {
            bodyType = BodyType.Planet;
            bodyRadiusKm = (float)planet.radiusKm;
            hasRings = planet.isGasGiant && planet.hasRings;
            
            UpdateMaterialProperties();
        }
        
        /// <summary>
        /// Initialize from moon data
        /// </summary>
        public void InitializeFromMoon(MoonData moon)
        {
            bodyType = BodyType.Moon;
            bodyRadiusKm = (float)moon.radiusKm;
            hasRings = false;
            
            UpdateMaterialProperties();
        }
        
        /// <summary>
        /// Initialize from station data
        /// </summary>
        public void InitializeFromStation(StationData station)
        {
            bodyType = BodyType.Station;
            bodyRadiusKm = 0.1f; // Stations are small
            hasRings = false;
            
            UpdateMaterialProperties();
        }
        
        private void UpdateMaterialProperties()
        {
            // Adjust thresholds based on body type
            switch (bodyType)
            {
                case BodyType.Planet:
                    meshThreshold = 80f;
                    impostorThreshold = 15f;
                    break;
                case BodyType.Moon:
                    meshThreshold = 50f;
                    impostorThreshold = 10f;
                    break;
                case BodyType.Station:
                    meshThreshold = 30f;
                    impostorThreshold = 8f;
                    break;
            }
            
            // Update ring visibility
            if (ringBillboard != null)
            {
                ringBillboard.SetActive(hasRings);
            }
        }
        
        /// <summary>
        /// Get current LOD tier (for debugging)
        /// </summary>
        public int GetCurrentLodTier()
        {
            return currentLodTier;
        }
        
        /// <summary>
        /// Set LOD thresholds
        /// </summary>
        public void SetThresholds(float mesh, float impostor)
        {
            meshThreshold = mesh;
            impostorThreshold = impostor;
        }
        
        /// <summary>
        /// Check if currently in mesh LOD
        /// </summary>
        public bool IsInMeshLod()
        {
            return currentLodTier == 0;
        }
        
        /// <summary>
        /// Check if currently in impostor LOD
        /// </summary>
        public bool IsInImpostorLod()
        {
            return currentLodTier == 1;
        }
        
        /// <summary>
        /// Check if currently in point LOD
        /// </summary>
        public bool IsInPointLod()
        {
            return currentLodTier == 2;
        }
        
        void OnDrawGizmosSelected()
        {
            if (mainCamera == null) return;
            
            // Visualize LOD thresholds
            Gizmos.color = Color.red;
            float meshDist = (float)AngularSize.CalculateDistanceForPixelSize(
                bodyRadiusKm * 1000.0, meshThreshold, mainCamera.fieldOfView, Screen.height);
            Gizmos.DrawWireSphere(transform.position, meshDist / 1000f);
            
            Gizmos.color = Color.yellow;
            float impostorDist = (float)AngularSize.CalculateDistanceForPixelSize(
                bodyRadiusKm * 1000.0, impostorThreshold, mainCamera.fieldOfView, Screen.height);
            Gizmos.DrawWireSphere(transform.position, impostorDist / 1000f);
        }
    }
}
