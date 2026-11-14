using UnityEngine;
using System;

namespace Space.Core
{
    /// <summary>
    /// Pure orbital position calculator. Computes position as pure function of time.
    /// No integration, no accumulated state - just Kepler's equation solved iteratively.
    /// </summary>
    public class OrbitalBody : MonoBehaviour
    {
        [Header("Orbital Parameters")]
        [SerializeField] private double orbitRadiusKm;
        [SerializeField] private double eccentricity;
        [SerializeField] private double periodSeconds;
        [SerializeField] private double phaseRadians;
        [SerializeField] private double focusOffsetKm; // Y-offset for focus point
        
        [Header("Orbital Plane")]
        [SerializeField] private Vector3 planeX = Vector3.right;
        [SerializeField] private Vector3 planeY = Vector3.forward;

        // Cached values for performance
        private double semiMajorAxis;
        private double oneMinusESquared;
        private double sqrtOnePlusE;
        private double sqrtOneMinusE;

        void Awake()
        {
            CacheValues();
        }

        private void CacheValues()
        {
            semiMajorAxis = orbitRadiusKm;
            oneMinusESquared = 1.0 - eccentricity * eccentricity;
            sqrtOnePlusE = Math.Sqrt(1.0 + eccentricity);
            sqrtOneMinusE = Math.Sqrt(1.0 - eccentricity);
        }

        /// <summary>
        /// Calculate position at time t (pure function, no side effects)
        /// </summary>
        /// <param name="t">Time in seconds since epoch</param>
        /// <returns>Position in kilometers relative to focus point</returns>
        public double3 PositionAt(double t)
        {
            // Mean anomaly
            double M = phaseRadians + (Math.PI * 2.0 * t / periodSeconds);
            
            // Solve Kepler's equation iteratively (Newton-Raphson)
            double E = M;
            for (int i = 0; i < 6; i++)
            {
                E -= (E - eccentricity * Math.Sin(E) - M) / (1.0 - eccentricity * Math.Cos(E));
            }
            
            // True anomaly
            double nu = 2.0 * Math.Atan2(
                sqrtOnePlusE * Math.Sin(E * 0.5),
                sqrtOneMinusE * Math.Cos(E * 0.5)
            );
            
            // Radius
            double r = semiMajorAxis * oneMinusESquared / (1.0 + eccentricity * Math.Cos(nu));
            
            // Position in orbital plane
            double x = r * Math.Cos(nu);
            double y = r * Math.Sin(nu);
            
            // Transform to 3D space using orbital plane vectors
            double3 posInPlane = new double3(
                (double)planeX.x * x + (double)planeY.x * y,
                (double)planeX.y * x + (double)planeY.y * y,
                (double)planeX.z * x + (double)planeY.z * y
            );
            
            // Add focus offset
            return posInPlane + new double3(0, focusOffsetKm, 0);
        }

        /// <summary>
        /// Get current position based on system time
        /// </summary>
        public double3 GetCurrentPosition(double systemTime)
        {
            return PositionAt(systemTime);
        }

        /// <summary>
        /// Initialize orbital parameters
        /// </summary>
        public void Initialize(double orbitRadius, double eccentricity, double period, 
            double phase, double focusOffset, Vector3 planeX, Vector3 planeY)
        {
            this.orbitRadiusKm = orbitRadius;
            this.eccentricity = eccentricity;
            this.periodSeconds = period;
            this.phaseRadians = phase;
            this.focusOffsetKm = focusOffset;
            this.planeX = planeX;
            this.planeY = planeY;
            
            CacheValues();
        }

        /// <summary>
        /// Initialize from planet data
        /// </summary>
        public void InitializeFromPlanet(PlanetData planet)
        {
            Initialize(
                planet.orbitRadiusKm,
                planet.eccentricity,
                planet.orbitPeriodS,
                planet.phase,
                0.0, // Focus at star center
                planet.orbitPlaneX,
                planet.orbitPlaneY
            );
        }

        /// <summary>
        /// Initialize from moon data
        /// </summary>
        public void InitializeFromMoon(MoonData moon)
        {
            Initialize(
                moon.orbitRadiusKm,
                moon.eccentricity,
                moon.orbitPeriodS,
                moon.phase,
                0.0, // Focus at planet center
                moon.orbitPlaneX,
                moon.orbitPlaneY
            );
        }

        // Public getters for debugging
        public double OrbitRadius => orbitRadiusKm;
        public double Eccentricity => eccentricity;
        public double Period => periodSeconds;
        public double Phase => phaseRadians;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // Visualize orbit path in editor
            Gizmos.color = Color.yellow;
            
            int segments = 64;
            Vector3 lastPos = Vector3.zero;
            
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments * periodSeconds;
                double3 pos = PositionAt(t);
                Vector3 unityPos = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);
                
                if (i > 0)
                {
                    Gizmos.DrawLine(lastPos, unityPos);
                }
                lastPos = unityPos;
            }
        }
#endif
    }
}
