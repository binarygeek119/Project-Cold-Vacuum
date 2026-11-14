using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Seeded random number generation and hashing for deterministic universe generation.
    /// All randomness flows from seeds - no global RNG allowed.
    /// </summary>
    public static class Seeds
    {
        /// <summary>
        /// Hash two 64-bit values using splitmix64-inspired algorithm
        /// </summary>
        public static ulong Hash64(ulong a, ulong b)
        {
            unchecked
            {
                ulong x = a ^ (b * 0x9E3779B97F4A7C15UL);
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        /// <summary>
        /// Hash three 64-bit values
        /// </summary>
        public static ulong Hash64(ulong a, ulong b, ulong c)
        {
            return Hash64(Hash64(a, b), c);
        }
    }

    /// <summary>
    /// Deterministic random number generator seeded from universe parameters.
    /// Uses splitmix64 algorithm - fast, no allocations, fully deterministic.
    /// </summary>
    public struct SeededRng
    {
        private ulong _state;

        public SeededRng(ulong seed)
        {
            _state = seed;
            // Ensure non-zero state
            if (_state == 0) _state = 1;
        }

        /// <summary>
        /// Generate next ulong value using splitmix64
        /// </summary>
        public ulong NextUlong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// Generate float in [0, 1) range
        /// </summary>
        public float NextFloat()
        {
            return (NextUlong() >> 11) * (1.0f / 9007199254740992.0f);
        }

        /// <summary>
        /// Generate double in [0, 1) range
        /// </summary>
        public double NextDouble()
        {
            return (NextUlong() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>
        /// Generate float in [min, max) range
        /// </summary>
        public float Range(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        /// <summary>
        /// Generate double in [min, max) range
        /// </summary>
        public double Range(double min, double max)
        {
            return min + NextDouble() * (max - min);
        }

        /// <summary>
        /// Generate int in [min, max] range (inclusive)
        /// </summary>
        public int Range(int min, int max)
        {
            return min + (int)(NextUlong() % (ulong)(max - min + 1));
        }

        /// <summary>
        /// Pick random element from array
        /// </summary>
        public T Pick<T>(T[] array)
        {
            if (array == null || array.Length == 0)
                return default;
            return array[NextUlong() % (ulong)array.Length];
        }

        /// <summary>
        /// Generate bool with given probability (0.0 = always false, 1.0 = always true)
        /// </summary>
        public bool Bool(float probability)
        {
            return NextFloat() < probability;
        }

        /// <summary>
        /// Generate unit vector on sphere surface
        /// </summary>
        public Vector3 OnUnitSphere()
        {
            float z = Range(-1f, 1f);
            float r = Mathf.Sqrt(1f - z * z);
            float theta = Range(0f, 2f * Mathf.PI);
            return new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);
        }
    }

    /// <summary>
    /// Tag constants for seed hashing - prevents collisions between different entity types
    /// </summary>
    public static class Tags
    {
        public const ulong Star = 0x1A2B3C4D5E6F7081UL;
        public const ulong Planet = 0x2B3C4D5E6F708192UL;
        public const ulong Moon = 0x3C4D5E6F708192A3UL;
        public const ulong Station = 0x4D5E6F708192A3B4UL;
        public const ulong Asteroid = 0x5E6F708192A3B4C5UL;
        public const ulong Belt = 0x6F708192A3B4C5D6UL;
        public const ulong Route = 0x708192A3B4C5D6E7UL;
        public const ulong Chunk = 0x8192A3B4C5D6E7F8UL;
        public const ulong Name = 0x92A3B4C5D6E7F809UL;
        public const ulong System = 0xA3B4C5D6E7F8091AUL;
    }
}
