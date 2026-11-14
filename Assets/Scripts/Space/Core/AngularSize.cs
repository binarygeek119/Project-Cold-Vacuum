using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Calculates apparent angular size for LOD transitions.
    /// Ensures continuous size across LOD tier changes.
    /// </summary>
    public static class AngularSize
    {
        /// <summary>
        /// Calculate apparent angular size in radians
        /// </summary>
        /// <param name="objectSizeMeters">Actual size of object in meters</param>
        /// <param name="distanceMeters">Distance from camera to object in meters</param>
        /// <returns>Angular size in radians</returns>
        public static double CalculateAngularSize(double objectSizeMeters, double distanceMeters)
        {
            if (distanceMeters <= 0) return Mathf.PI;
            return 2.0 * Mathf.Atan((float)(objectSizeMeters / (2.0 * distanceMeters)));
        }

        /// <summary>
        /// Calculate apparent size in pixels on screen
        /// </summary>
        /// <param name="angularSizeRad">Angular size in radians</param>
        /// <param name="cameraFOVDegrees">Camera field of view in degrees</param>
        /// <param name="screenHeightPixels">Screen height in pixels</param>
        /// <returns>Size in pixels</returns>
        public static float CalculatePixelSize(double angularSizeRad, float cameraFOVDegrees, int screenHeightPixels)
        {
            float fovRad = cameraFOVDegrees * Mathf.Deg2Rad;
            float pixelsPerRadian = screenHeightPixels / fovRad;
            return (float)(angularSizeRad * pixelsPerRadian);
        }

        /// <summary>
        /// Determine LOD tier based on angular size
        /// </summary>
        /// <param name="pixelSize">Apparent size in pixels</param>
        /// <param name="meshThreshold">Minimum pixels for mesh LOD</param>
        /// <param name="impostorThreshold">Minimum pixels for impostor LOD</param>
        /// <returns>LOD tier: 0 = mesh, 1 = impostor, 2 = point</returns>
        public static int DetermineLodTier(float pixelSize, float meshThreshold, float impostorThreshold)
        {
            if (pixelSize >= meshThreshold) return 0; // Mesh LOD
            if (pixelSize >= impostorThreshold) return 1; // Impostor LOD
            return 2; // Point LOD
        }

        /// <summary>
        /// Calculate scale factor to maintain consistent apparent size across LOD transitions
        /// </summary>
        /// <param name="currentPixelSize">Current apparent size in pixels</param>
        /// <param name="targetPixelSize">Target apparent size in pixels</param>
        /// <returns>Scale multiplier</returns>
        public static float CalculateLodScaleFactor(float currentPixelSize, float targetPixelSize)
        {
            if (currentPixelSize <= 0) return 1.0f;
            return targetPixelSize / currentPixelSize;
        }

        /// <summary>
        /// Clamp pixel size to minimum visible size (1 pixel)
        /// </summary>
        public static float ClampToMinimumPixelSize(float pixelSize)
        {
            return Mathf.Max(pixelSize, 1.0f);
        }

        /// <summary>
        /// Calculate distance at which object reaches a specific pixel size
        /// </summary>
        /// <param name="objectSizeMeters">Actual size in meters</param>
        /// <param name="targetPixelSize">Target pixel size</param>
        /// <param name="cameraFOVDegrees">Camera FOV in degrees</param>
        /// <param name="screenHeightPixels">Screen height in pixels</param>
        /// <returns>Distance in meters</returns>
        public static double CalculateDistanceForPixelSize(double objectSizeMeters, float targetPixelSize, 
            float cameraFOVDegrees, int screenHeightPixels)
        {
            float fovRad = cameraFOVDegrees * Mathf.Deg2Rad;
            float pixelsPerRadian = screenHeightPixels / fovRad;
            double targetAngularSize = targetPixelSize / pixelsPerRadian;
            
            if (targetAngularSize <= 0) return double.MaxValue;
            
            return objectSizeMeters / (2.0 * Mathf.Tan((float)(targetAngularSize / 2.0)));
        }
    }
}
