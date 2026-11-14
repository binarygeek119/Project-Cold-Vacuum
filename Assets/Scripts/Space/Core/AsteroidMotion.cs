using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Simple orbital motion component for asteroids and other small bodies.
    /// Provides basic orbital movement around a center point.
    /// </summary>
    public class AsteroidMotion : MonoBehaviour
    {
        private Vector3 center;
        private float orbitalSpeed;
        private Vector3 orbitAxis;
        private float orbitalRadius;
        private float currentAngle;
        
        /// <summary>
        /// Initialize orbital parameters
        /// </summary>
        public void InitializeOrbit(Vector3 center, float speed, Vector3 axis, ref SeededRng rng)
        {
            this.center = center;
            this.orbitalSpeed = speed;
            this.orbitAxis = axis.normalized;
            this.orbitalRadius = Vector3.Distance(transform.position, center);
            this.currentAngle = rng.Range(0f, 360f);
            
            // Position on orbit
            UpdatePosition();
        }
        
        void Update()
        {
            if (orbitalSpeed <= 0f) return;
            
            currentAngle += orbitalSpeed * Time.deltaTime;
            if (currentAngle > 360f) currentAngle -= 360f;
            
            UpdatePosition();
        }
        
        private void UpdatePosition()
        {
            // Calculate position on circular orbit
            Vector3 offset = transform.position - center;
            
            // Rotate around axis
            Quaternion rotation = Quaternion.AngleAxis(currentAngle, orbitAxis);
            Vector3 newPosition = center + rotation * offset;
            
            transform.position = newPosition;
            
            // Rotate to face direction of motion
            transform.rotation = Quaternion.LookRotation(
                Vector3.Cross(orbitAxis, (transform.position - center).normalized),
                orbitAxis
            );
        }
    }
}
