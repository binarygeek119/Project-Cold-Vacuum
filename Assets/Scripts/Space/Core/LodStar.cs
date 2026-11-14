using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Handles LOD transitions for stars based on apparent size.
    /// Four tiers: Mesh (close) → Impostor → Glow → Point (far)
    /// </summary>
    public class LodStar : MonoBehaviour
    {
        [Header("LOD Thresholds (pixels)")]
        [SerializeField] private float meshThreshold = 100f;
        [SerializeField] private float impostorThreshold = 20f;
        [SerializeField] private float glowThreshold = 5f;
        
        [Header("References")]
        [SerializeField] private GameObject meshLod;
        [SerializeField] private GameObject impostorLod;
        [SerializeField] private GameObject glowLod;
        [SerializeField] private GameObject pointLod;
        
        [Header("Star Properties")]
        [SerializeField] private float starRadiusKm = 696340f; // Default: Sun radius
        
        private Camera mainCamera;
        private int currentLodTier = -1;
        private Renderer[] lodRenderers;
        
        void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("LodStar: No main camera found!");
                enabled = false;
                return;
            }
            
            // Cache renderers for each LOD
            lodRenderers = new Renderer[4];
            if (meshLod != null) lodRenderers[0] = meshLod.GetComponent<Renderer>();
            if (impostorLod != null) lodRenderers[1] = impostorLod.GetComponent<Renderer>();
            if (glowLod != null) lodRenderers[2] = glowLod.GetComponent<Renderer>();
            if (pointLod != null) lodRenderers[3] = pointLod.GetComponent<Renderer>();
            
            // Initialize with all LODs disabled
            SetLodTier(0);
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
            double angularSizeRad = AngularSize.CalculateAngularSize(starRadiusKm * 1000.0, distanceMeters);
            float pixelSize = AngularSize.CalculatePixelSize(angularSizeRad, mainCamera.fieldOfView, Screen.height);
            pixelSize = AngularSize.ClampToMinimumPixelSize(pixelSize);
            
            // Determine LOD tier
            int targetTier = AngularSize.DetermineLodTier(pixelSize, meshThreshold, impostorThreshold);
            
            // Apply glow/point thresholds
            if (pixelSize < glowThreshold)
            {
                targetTier = 3; // Point LOD
            }
            else if (pixelSize < impostorThreshold)
            {
                targetTier = 2; // Glow LOD
            }
            
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
        }
        
        /// <summary>
        /// Set star properties from SystemData
        /// </summary>
        public void InitializeFromStar(StarData star)
        {
            starRadiusKm = (float)star.radiusKm;
            
            // Update material colors based on star temperature
            if (meshLod != null)
            {
                var renderer = meshLod.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color starColor = GetStarColorFromTemperature(star.temperatureK);
                    renderer.material.SetColor("_Color", starColor);
                    renderer.material.SetColor("_EmissionColor", starColor * 2f);
                }
            }
        }
        
        /// <summary>
        /// Convert star temperature to color (approximation)
        /// </summary>
        private Color GetStarColorFromTemperature(float temperatureK)
        {
            // Simplified blackbody radiation approximation
            if (temperatureK < 3500f) return new Color(1.0f, 0.6f, 0.4f); // Red
            if (temperatureK < 5000f) return new Color(1.0f, 0.8f, 0.6f); // Orange
            if (temperatureK < 6000f) return new Color(1.0f, 0.95f, 0.8f); // Yellow
            if (temperatureK < 7500f) return new Color(1.0f, 1.0f, 0.95f); // White
            return new Color(0.8f, 0.9f, 1.0f); // Blue
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
        public void SetThresholds(float mesh, float impostor, float glow)
        {
            meshThreshold = mesh;
            impostorThreshold = impostor;
            glowThreshold = glow;
        }
        
        void OnDrawGizmosSelected()
        {
            if (mainCamera == null) return;
            
            // Visualize LOD thresholds
            Gizmos.color = Color.red;
            float meshDist = (float)AngularSize.CalculateDistanceForPixelSize(
                starRadiusKm * 1000.0, meshThreshold, mainCamera.fieldOfView, Screen.height);
            Gizmos.DrawWireSphere(transform.position, meshDist / 1000f);
            
            Gizmos.color = Color.yellow;
            float impostorDist = (float)AngularSize.CalculateDistanceForPixelSize(
                starRadiusKm * 1000.0, impostorThreshold, mainCamera.fieldOfView, Screen.height);
            Gizmos.DrawWireSphere(transform.position, impostorDist / 1000f);
            
            Gizmos.color = Color.green;
            float glowDist = (float)AngularSize.CalculateDistanceForPixelSize(
                starRadiusKm * 1000.0, glowThreshold, mainCamera.fieldOfView, Screen.height);
            Gizmos.DrawWireSphere(transform.position, glowDist / 1000f);
        }
    }
}
