using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Registry for all space shaders - prevents runtime Shader.Find calls.
    /// Create via Assets > Create > Space > Shader Registry
    /// </summary>
    [CreateAssetMenu(fileName = "SpaceShaderRegistry", menuName = "Space/Shader Registry")]
    public class SpaceShaderRegistry : ScriptableObject
    {
        [Header("Star Shaders")]
        public Shader starPoint;
        public Shader starGlow;
        public Shader starImpostor;
        public Shader starMesh;

        [Header("Body Shaders")]
        public Shader planetImpostor;
        public Shader ringBillboard;

        [Header("Runtime Materials (cached)")]
        private Material _starPointMat;
        private Material _starGlowMat;
        private Material _starImpostorMat;
        private Material _starMeshMat;
        private Material _planetImpostorMat;
        private Material _ringBillboardMat;

        // Getters that cache materials
        public Material GetStarPointMaterial()
        {
            if (_starPointMat == null && starPoint != null)
                _starPointMat = new Material(starPoint);
            return _starPointMat;
        }

        public Material GetStarGlowMaterial()
        {
            if (_starGlowMat == null && starGlow != null)
                _starGlowMat = new Material(starGlow);
            return _starGlowMat;
        }

        public Material GetStarImpostorMaterial()
        {
            if (_starImpostorMat == null && starImpostor != null)
                _starImpostorMat = new Material(starImpostor);
            return _starImpostorMat;
        }

        public Material GetStarMeshMaterial()
        {
            if (_starMeshMat == null && starMesh != null)
                _starMeshMat = new Material(starMesh);
            return _starMeshMat;
        }

        public Material GetPlanetImpostorMaterial()
        {
            if (_planetImpostorMat == null && planetImpostor != null)
                _planetImpostorMat = new Material(planetImpostor);
            return _planetImpostorMat;
        }

        public Material GetRingBillboardMaterial()
        {
            if (_ringBillboardMat == null && ringBillboard != null)
                _ringBillboardMat = new Material(ringBillboard);
            return _ringBillboardMat;
        }

        void OnValidate()
        {
            // Clear cached materials when shaders change
            _starPointMat = null;
            _starGlowMat = null;
            _starImpostorMat = null;
            _starMeshMat = null;
            _planetImpostorMat = null;
            _ringBillboardMat = null;
        }
    }
}
