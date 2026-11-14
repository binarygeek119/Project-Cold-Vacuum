using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Floating origin system that keeps the ship within 1km of world origin.
    /// Maintains double-precision ship position and shifts all world objects each frame.
    /// </summary>
    public class FloatingOrigin : MonoBehaviour
    {
        public const double SHIP_KILL_RADIUS_KM = 1.0;

        // Double-precision ship position in kilometers
        public double3 shipPositionKm { get; private set; }
        
        // Last ship position for computing delta
        private double3 _lastShipPositionKm;
        
        // Reference to ship transform
        [SerializeField] private Transform shipTransform;
        
        // All root transforms that need to be shifted (cached for performance)
        private Transform[] _rootsToShift;

        void Awake()
        {
            if (shipTransform == null)
            {
                Debug.LogError("FloatingOrigin: Ship transform not assigned!");
                enabled = false;
                return;
            }

            // Initialize ship position
            shipPositionKm = new double3(
                shipTransform.position.x / 1000.0,
                shipTransform.position.y / 1000.0,
                shipTransform.position.z / 1000.0
            );
            _lastShipPositionKm = shipPositionKm;

            // Cache all root transforms (exclude ship and this object)
            CacheRoots();
        }

        void CacheRoots()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            int count = 0;
            foreach (var root in rootObjects)
            {
                if (root != gameObject && root.transform != shipTransform)
                    count++;
            }

            _rootsToShift = new Transform[count];
            int index = 0;
            foreach (var root in rootObjects)
            {
                if (root != gameObject && root.transform != shipTransform)
                {
                    _rootsToShift[index++] = root.transform;
                }
            }
        }

        void LateUpdate()
        {
            // Update ship position from its movement (assumes ship controller updates position)
            shipPositionKm = new double3(
                shipTransform.position.x / 1000.0,
                shipTransform.position.y / 1000.0,
                shipTransform.position.z / 1000.0
            );

            // Compute delta movement
            double3 delta = shipPositionKm - _lastShipPositionKm;
            
            // If ship moved, shift all world objects in opposite direction
            if (delta.LengthSquared() > 0)
            {
                Vector3 shift = new Vector3(
                    (float)(-delta.x * 1000.0),
                    (float)(-delta.y * 1000.0),
                    (float)(-delta.z * 1000.0)
                );

                // Shift all roots
                for (int i = 0; i < _rootsToShift.Length; i++)
                {
                    if (_rootsToShift[i] != null)
                    {
                        _rootsToShift[i].position += shift;
                    }
                }

                // Snap ship back to origin
                shipTransform.position = Vector3.zero;
                
                // Update last position
                _lastShipPositionKm = new double3(0, 0, 0);
                shipPositionKm = new double3(0, 0, 0);
            }
            else
            {
                _lastShipPositionKm = shipPositionKm;
            }
        }

        /// <summary>
        /// Set ship position directly (used by streaming system, lock-on arrival, etc.)
        /// </summary>
        public void SetShipPosition(double3 positionKm)
        {
            shipPositionKm = positionKm;
            _lastShipPositionKm = positionKm;
            shipTransform.position = new Vector3(
                (float)(positionKm.x * 1000.0),
                (float)(positionKm.y * 1000.0),
                (float)(positionKm.z * 1000.0)
            );
        }

        /// <summary>
        /// Get current ship position in double-precision kilometers
        /// </summary>
        public double3 GetShipPosition()
        {
            return shipPositionKm;
        }

        /// <summary>
        /// Convert world position (km) to local position relative to ship
        /// </summary>
        public Vector3 WorldToLocalKm(double3 worldPosKm)
        {
            double3 local = worldPosKm - shipPositionKm;
            return new Vector3(
                (float)local.x * 1000.0f,
                (float)local.y * 1000.0f,
                (float)local.z * 1000.0f
            );
        }

        /// <summary>
        /// Get ship transform (public accessor)
        /// </summary>
        public Transform ShipTransform => shipTransform;

        /// <summary>
        /// Get ship position in kilometers (alias for GetShipPosition)
        /// </summary>
        public double3 GetShipPositionKm()
        {
            return shipPositionKm;
        }

        /// <summary>
        /// Get ship transform (convenience method for other managers)
        /// </summary>
        public Transform GetShip()
        {
            return shipTransform;
        }

        /// <summary>
        /// Get current origin offset in kilometers
        /// </summary>
        public Vector3 GetOriginOffset()
        {
            return shipPositionKm;
        }

        /// <summary>
        /// Check if ship is currently inside a star system
        /// </summary>
        public bool IsInStarSystem()
        {
            var systemManager = FindAnyObjectByType<SystemInstanceManager>();
            return systemManager != null && systemManager.IsInStarSystem();
        }
    }

    /// <summary>
    /// Simple double-precision 3D vector for orbital math
    /// </summary>
    public struct double3
    {
        public double x, y, z;

        public double3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public double LengthSquared()
        {
            return x * x + y * y + z * z;
        }

        public double Length()
        {
            return System.Math.Sqrt(LengthSquared());
        }

        public static double3 operator -(double3 a, double3 b)
        {
            return new double3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static double3 operator +(double3 a, double3 b)
        {
            return new double3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static double3 operator *(double3 a, double s)
        {
            return new double3(a.x * s, a.y * s, a.z * s);
        }

        public static implicit operator Vector3(double3 d)
        {
            return new Vector3((float)d.x, (float)d.y, (float)d.z);
        }
    }
}
