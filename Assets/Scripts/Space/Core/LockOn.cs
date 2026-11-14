using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Handles target locking mechanics for leap travel.
    /// Implements cone-based targeting, sphere occlusion, and burn-to-arrival navigation.
    /// </summary>
    public class LockOn : MonoBehaviour
    {
        [Header("Lock Parameters")]
        [SerializeField] private float lockConeDeg = 8f;
        [SerializeField] private float lockChargeS = 1.5f;
        [SerializeField] private double lockRangeM = 2.0e13; // 20 billion km
        [SerializeField] private double arrivalRadii = 400.0;
        
        [Header("References")]
        [SerializeField] private Transform shipTransform;
        [SerializeField] private Rigidbody shipRigidbody;
        [SerializeField] private SystemInstanceManager systemManager;
        
        // Lock state
        private LockTarget currentTarget;
        private float lockProgress = 0f;
        private bool isLocking = false;
        private bool isBurning = false;
        
        void Start()
        {
            if (shipTransform == null)
            {
                shipTransform = transform;
            }
            
            if (shipRigidbody == null)
            {
                shipRigidbody = GetComponent<Rigidbody>();
            }
            
            if (systemManager == null)
            {
                systemManager = FindAnyObjectByType<SystemInstanceManager>();
            }
        }
        
        void Update()
        {
            if (isLocking)
            {
                UpdateLockProgress();
            }
            
            if (isBurning)
            {
                UpdateBurn();
            }
        }
        
        /// <summary>
        /// Attempt to lock onto a target
        /// </summary>
        public bool AttemptLock(Transform targetTransform, BodyType bodyType, object bodyData)
        {
            if (targetTransform == null) return false;
            
            // Check range
            double distanceM = Vector3.Distance(transform.position, targetTransform.position) * 1000.0;
            if (distanceM > lockRangeM)
            {
                Debug.Log($"LockOn: Target too far ({distanceM:E}m > {lockRangeM:E}m)");
                return false;
            }
            
            // Check cone
            Vector3 toTarget = (targetTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > lockConeDeg)
            {
                Debug.Log($"LockOn: Target outside lock cone ({angle:F1}° > {lockConeDeg}°)");
                return false;
            }
            
            // Check occlusion (sphere math against stars/planets)
            if (IsOccluded(targetTransform.position))
            {
                Debug.Log("LockOn: Target occluded by celestial body");
                return false;
            }
            
            // Start locking
            currentTarget = new LockTarget(targetTransform, bodyType, bodyData);
            isLocking = true;
            lockProgress = 0f;
            
            return true;
        }
        
        private void UpdateLockProgress()
        {
            if (currentTarget == null || currentTarget.TargetTransform == null)
            {
                CancelLock();
                return;
            }
            
            // Verify target still in cone
            Vector3 toTarget = (currentTarget.TargetTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toTarget);
            
            if (angle > lockConeDeg)
            {
                Debug.Log("LockOn: Target left lock cone");
                CancelLock();
                return;
            }
            
            // Progress lock
            lockProgress += Time.deltaTime / lockChargeS;
            
            if (lockProgress >= 1f)
            {
                CompleteLock();
            }
        }
        
        private void CompleteLock()
        {
            isLocking = false;
            lockProgress = 1f;
            
            Debug.Log($"LockOn: Locked onto {currentTarget.BodyType}");
            
            // Start burn to target
            StartBurn();
        }
        
        private void StartBurn()
        {
            if (currentTarget == null) return;
            
            isBurning = true;
            
            // Face the target and apply thrust
            if (shipTransform != null && currentTarget.TargetTransform != null)
            {
                Vector3 direction = (currentTarget.TargetTransform.position - shipTransform.position).normalized;
                shipTransform.rotation = Quaternion.LookRotation(direction);
                
                // Apply force if we have a rigidbody
                if (shipRigidbody != null)
                {
                    shipRigidbody.AddForce(direction * 50f, ForceMode.Acceleration);
                }
            }
        }
        
        private void UpdateBurn()
        {
            if (currentTarget == null || currentTarget.TargetTransform == null)
            {
                CancelBurn();
                return;
            }
            
            // Check arrival
            double distanceM = Vector3.Distance(transform.position, currentTarget.TargetTransform.position) * 1000.0;
            double arrivalDistanceM = GetArrivalDistance();
            
            if (distanceM <= arrivalDistanceM)
            {
                ArriveAtTarget();
            }
        }
        
        private double GetArrivalDistance()
        {
            if (currentTarget == null) return 0;
            
            // Calculate arrival distance based on target body size
            double bodyRadiusKm = 0;
            
            if (currentTarget.BodyData is PlanetData planet)
            {
                bodyRadiusKm = planet.radiusKm;
            }
            else if (currentTarget.BodyData is MoonData moon)
            {
                bodyRadiusKm = moon.radiusKm;
            }
            else if (currentTarget.BodyData is StarData star)
            {
                bodyRadiusKm = star.radiusKm;
            }
            else if (currentTarget.BodyData is StationData station)
            {
                bodyRadiusKm = 0.1; // Stations are small
            }
            
            return bodyRadiusKm * arrivalRadii * 1000.0; // Convert to meters
        }
        
        private void ArriveAtTarget()
        {
            isBurning = false;
            
            Debug.Log($"LockOn: Arrived at {currentTarget.BodyType}");
            
            // Kill velocity
            if (shipRigidbody != null)
            {
                shipRigidbody.linearVelocity = Vector3.zero;
                shipRigidbody.angularVelocity = Vector3.zero;
            }
            
            // Face target
            if (currentTarget.TargetTransform != null)
            {
                Vector3 direction = (currentTarget.TargetTransform.position - shipTransform.position).normalized;
                shipTransform.rotation = Quaternion.LookRotation(direction);
            }
            
            // Clear target
            currentTarget = null;
        }
        
        private bool IsOccluded(Vector3 targetPosition)
        {
            if (systemManager == null) return false;
            
            Vector3 shipPos = transform.position;
            Vector3 targetDir = (targetPosition - shipPos).normalized;
            float distanceToTarget = Vector3.Distance(shipPos, targetPosition);
            
            // Check all active systems for occluding bodies
            var systems = systemManager.GetActiveSystems();
            foreach (var system in systems)
            {
                if (system == null) continue;
                
                var systemData = system.GetSystemData();
                
                // Check star
                if (CheckSphereOcclusion(shipPos, targetDir, distanceToTarget, 
                    system.GetStarTransform().position, (float)systemData.star.radiusKm))
                {
                    return true;
                }
                
                // Check planets
                var planets = system.GetPlanetTransforms();
                for (int i = 0; i < planets.Length && i < systemData.planets.Length; i++)
                {
                    if (planets[i] == null) continue;
                    
                    if (CheckSphereOcclusion(shipPos, targetDir, distanceToTarget,
                        planets[i].position, (float)systemData.planets[i].radiusKm))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private bool CheckSphereOcclusion(Vector3 rayOrigin, Vector3 rayDir, float rayLength,
            Vector3 sphereCenter, float sphereRadius)
        {
            Vector3 oc = rayOrigin - sphereCenter;
            float b = Vector3.Dot(oc, rayDir);
            float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
            
            // Ray starts inside sphere
            if (c < 0) return false;
            
            float discriminant = b * b - c;
            
            // No intersection
            if (discriminant < 0) return false;
            
            float sqrtDisc = Mathf.Sqrt(discriminant);
            float t1 = -b - sqrtDisc;
            float t2 = -b + sqrtDisc;
            
            // Intersection behind ray origin
            if (t2 < 0) return false;
            
            // Intersection in front of target
            if (t1 > rayLength) return false;
            
            return true;
        }
        
        public void CancelLock()
        {
            isLocking = false;
            lockProgress = 0f;
            currentTarget = null;
        }
        
        public void CancelBurn()
        {
            isBurning = false;
            
            // Stop applying thrust
            if (shipRigidbody != null)
            {
                shipRigidbody.linearVelocity = Vector3.zero;
            }
        }
        
        public bool IsLocked()
        {
            return currentTarget != null && !isLocking && !isBurning;
        }
        
        public bool IsBurning()
        {
            return isBurning;
        }
        
        public float GetLockProgress()
        {
            return lockProgress;
        }
        
        public LockTarget GetCurrentTarget()
        {
            return currentTarget;
        }
    }
    
    /// <summary>
    /// Represents a lock target
    /// </summary>
    public class LockTarget
    {
        public Transform TargetTransform { get; private set; }
        public BodyType BodyType { get; private set; }
        public object BodyData { get; private set; }
        
        public LockTarget(Transform target, BodyType bodyType, object bodyData)
        {
            TargetTransform = target;
            BodyType = bodyType;
            BodyData = bodyData;
        }
    }
}
