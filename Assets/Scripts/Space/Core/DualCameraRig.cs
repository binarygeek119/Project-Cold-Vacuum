using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Dual camera rig for space rendering - near camera for local objects,
    /// far camera for distant orbital bodies. Both share position/rotation.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DualCameraRig : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private Camera nearCamera;
        [SerializeField] private Camera farCamera;

        [Header("Layer Culling")]
        [SerializeField] private LayerMask nearCameraCullMask;
        [SerializeField] private LayerMask farCameraCullMask;

        [Header("Follow Target")]
        [SerializeField] private Transform followTarget;

        void Awake()
        {
            if (nearCamera == null || farCamera == null)
            {
                Debug.LogError("DualCameraRig: Both cameras must be assigned!");
                enabled = false;
                return;
            }

            // Configure near camera
            nearCamera.nearClipPlane = 0.05f;
            nearCamera.farClipPlane = 2000f;
            nearCamera.cullingMask = nearCameraCullMask;
            nearCamera.clearFlags = CameraClearFlags.Skybox;

            // Configure far camera
            farCamera.nearClipPlane = 1.0f;
            farCamera.farClipPlane = 5e8f;
            farCamera.cullingMask = farCameraCullMask;
            farCamera.clearFlags = CameraClearFlags.Depth; // Depth-clear after near camera
            farCamera.depth = nearCamera.depth - 1; // Render after near camera
        }

        void LateUpdate()
        {
            if (followTarget != null)
            {
                // Both cameras follow the target with identical position/rotation
                transform.position = followTarget.position;
                transform.rotation = followTarget.rotation;
            }
        }

        /// <summary>
        /// Set the follow target (usually the ship)
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }

        /// <summary>
        /// Get the near camera (for UI, raycasts, etc.)
        /// </summary>
        public Camera GetNearCamera()
        {
            return nearCamera;
        }

        /// <summary>
        /// Get the far camera
        /// </summary>
        public Camera GetFarCamera()
        {
            return farCamera;
        }

        /// <summary>
        /// Configure layer masks from code
        /// </summary>
        public void SetLayerMasks(LayerMask nearMask, LayerMask farMask)
        {
            nearCameraCullMask = nearMask;
            farCameraCullMask = farMask;
            
            if (nearCamera != null)
                nearCamera.cullingMask = nearMask;
            if (farCamera != null)
                farCamera.cullingMask = farMask;
        }
    }
}
