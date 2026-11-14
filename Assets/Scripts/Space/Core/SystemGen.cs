using System;
using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Pure system generation - no Unity objects, no allocations beyond result.
    /// Generates complete SystemData from a single seed. Must complete in under 1ms.
    /// </summary>
    public static class SystemGen
    {
        // Reference constants from spec
        private const double AU_KM = 149_597_870.7;
        private const double G = 6.674e-20; // km³/kg/s²
        private const double StarRSolKm = 696_340.0;
        private const float BinaryChance = 0.045f;
        private const int MaxPlanets = 14;
        private const int MaxMoons = 31;
        private const float PlanetBias = 3.0f;
        private const float MoonBias = 2.6f;
        private const float BeltBias = 2.0f;
        private const float GasChance = 0.34f;
        private const float SpinClockwiseChance = 0.28f;

        /// <summary>
        /// Generate a complete star system from seed. Pure function, no side effects.
        /// </summary>
        public static SystemData Generate(ulong systemSeed)
        {
            SeededRng rng = new SeededRng(systemSeed);

            // Generate star
            StarData star = GenerateStar(systemSeed, ref rng);

            // Generate planets with strictly increasing orbital radii
            int planetCount = _weighted_count(ref rng, 0, MaxPlanets, PlanetBias);
            PlanetData[] planets = new PlanetData[planetCount];

            double lastOrbitRadius = star.radiusKm * 5.0; // Minimum 5 star radii
            for (int i = 0; i < planetCount; i++)
            {
                ulong planetSeed = Seeds.Hash64(systemSeed, Tags.Planet + (ulong)i);
                planets[i] = GeneratePlanet(planetSeed, ref rng, star, lastOrbitRadius, i);
                lastOrbitRadius = planets[i].orbitRadiusKm + planets[i].radiusKm * 10.0; // Space them out
            }

            // Generate asteroid belts
            int beltCount = _weighted_count(ref rng, 0, 3, BeltBias);
            BeltData[] belts = new BeltData[beltCount];
            for (int i = 0; i < beltCount; i++)
            {
                ulong beltSeed = Seeds.Hash64(systemSeed, Tags.Belt + (ulong)i);
                belts[i] = GenerateBelt(beltSeed, ref rng, star, planets);
            }

            return new SystemData(systemSeed, star, planets, belts);
        }

        private static StarData GenerateStar(ulong seed, ref SeededRng rng)
        {
            string name = NameGen.GenerateStarName(seed);
            
            // Star properties
            double radiusKm = StarRSolKm * rng.Range(0.5f, 2.0f);
            double massKg = 1.989e30 * rng.Range(0.5f, 2.0f); // Solar masses
            float temperatureK = rng.Range(3000f, 30000f);
            
            Vector3 positionKm = Vector3.zero; // Star at origin
            Vector3 spinAxis = rng.OnUnitSphere();
            double rotationPeriodS = rng.Range(10.0 * 86400.0, 100.0 * 86400.0); // 10-100 days

            // Binary star check
            bool isBinary = rng.Bool(BinaryChance);
            double companionOffsetKm = 0;
            double companionRadiusKm = 0;
            double companionMassKg = 0;

            if (isBinary)
            {
                companionOffsetKm = radiusKm * rng.Range(20.0f, 50.0f);
                companionRadiusKm = radiusKm * rng.Range(0.5f, 0.9f);
                companionMassKg = massKg * (companionRadiusKm / radiusKm);
            }

            return new StarData(seed, name, radiusKm, massKg, temperatureK, positionKm,
                spinAxis, rotationPeriodS, isBinary, companionOffsetKm, companionRadiusKm, companionMassKg);
        }

        private static PlanetData GeneratePlanet(ulong seed, ref SeededRng rng, StarData star, double minOrbitRadius, int index)
        {
            string name = NameGen.GeneratePlanetName(seed, index);
            
            // Planet type
            bool isGasGiant = rng.Bool(GasChance);
            
            // Size based on type
            double radiusKm = isGasGiant 
                ? rng.Range(30000.0, 70000.0)  // Gas giants: 30k-70k km
                : rng.Range(2000.0, 12000.0);  // Rocky: 2k-12k km

            double massKg = isGasGiant
                ? radiusKm * radiusKm * 1e10  // Rough mass scaling
                : radiusKm * radiusKm * 5e9;

            // Orbital radius - must be > minOrbitRadius and strictly increasing
            double orbitRadiusKm = minOrbitRadius + rng.Range(50000.0, 200000.0) * (index + 1);
            
            // Check binary constraint: no planet inside 9 * companion separation
            if (star.isBinary && orbitRadiusKm < star.companionOffsetKm * 9.0)
            {
                orbitRadiusKm = star.companionOffsetKm * 9.0 + rng.Range(50000.0, 100000.0);
            }

            // Orbital parameters
            double orbitPeriodS = CalculateOrbitalPeriod(orbitRadiusKm, star.massKg);
            double eccentricity = rng.Range(0.0, 0.3);
            double inclinationRad = rng.Range(0.0, 0.2); // Small inclinations
            double phase = rng.Range(0.0, Math.PI * 2.0);

            // Orbit plane
            Vector3 orbitPlaneX, orbitPlaneY;
            _orbit_plane(ref rng, inclinationRad, out orbitPlaneX, out orbitPlaneY);

            // Generate moons
            int moonCount = _weighted_count(ref rng, 0, MaxMoons, MoonBias);
            MoonData[] moons = new MoonData[moonCount];
            double lastMoonOrbit = radiusKm * 2.0; // Start at 2x planet radius

            for (int i = 0; i < moonCount; i++)
            {
                ulong moonSeed = Seeds.Hash64(seed, Tags.Moon + (ulong)i);
                moons[i] = GenerateMoon(moonSeed, ref rng, radiusKm, lastMoonOrbit, name, i);
                
                // Hill clamp - ensure moon stays within planet's gravitational influence
                _hill_clamp(ref moons[i], radiusKm, massKg, orbitRadiusKm, star.massKg);
                
                lastMoonOrbit = moons[i].orbitRadiusKm * 2.2; // Each moon at least 2.2x previous
            }

            // Station (30% chance for gas giants, 10% for rocky)
            StationData? station = null;
            float stationChance = isGasGiant ? 0.3f : 0.1f;
            if (rng.Bool(stationChance))
            {
                ulong stationSeed = Seeds.Hash64(seed, Tags.Station);
                station = GenerateStation(stationSeed, ref rng, radiusKm, orbitRadiusKm, name);
            }

            return new PlanetData(seed, name, radiusKm, massKg, orbitRadiusKm, orbitPeriodS,
                eccentricity, inclinationRad, phase, orbitPlaneX, orbitPlaneY, isGasGiant, false, moons, station);
        }

        private static MoonData GenerateMoon(ulong seed, ref SeededRng rng, double parentRadiusKm, double minOrbitKm, string planetName, int index)
        {
            string name = NameGen.GenerateMoonName(seed, planetName, index);
            
            double radiusKm = rng.Range(100.0, 3000.0);
            double massKg = radiusKm * radiusKm * 1e6;
            double orbitRadiusKm = minOrbitKm + rng.Range(5000.0, 20000.0);
            double orbitPeriodS = CalculateOrbitalPeriod(orbitRadiusKm, 5.972e24); // Assume Earth-like parent mass
            double eccentricity = rng.Range(0.0, 0.1);
            double inclinationRad = rng.Range(0.0, 0.1);
            double phase = rng.Range(0.0, Math.PI * 2.0);

            Vector3 orbitPlaneX, orbitPlaneY;
            _orbit_plane(ref rng, inclinationRad, out orbitPlaneX, out orbitPlaneY);

            return new MoonData(seed, name, radiusKm, massKg, orbitRadiusKm, orbitPeriodS,
                eccentricity, inclinationRad, phase, orbitPlaneX, orbitPlaneY);
        }

        private static StationData GenerateStation(ulong seed, ref SeededRng rng, double planetRadiusKm, double planetOrbitKm, string planetName)
        {
            string name = NameGen.GenerateStationName(seed);
            double orbitRadiusKm = planetRadiusKm * rng.Range(3.0, 8.0);
            double orbitPeriodS = rng.Range(3600.0, 86400.0); // 1 hour to 1 day
            double phase = rng.Range(0.0, Math.PI * 2.0);
            int stationType = rng.Range(0, 4);

            return new StationData(seed, name, orbitRadiusKm, orbitPeriodS, phase, stationType);
        }

        private static BeltData GenerateBelt(ulong seed, ref SeededRng rng, StarData star, PlanetData[] planets)
        {
            // Place belt between planets or beyond last planet
            double innerRadiusKm, outerRadiusKm;
            
            if (planets.Length == 0)
            {
                innerRadiusKm = star.radiusKm * 10.0;
                outerRadiusKm = innerRadiusKm + rng.Range(50000.0, 150000.0);
            }
            else
            {
                // Find a gap between planets
                int gapIndex = rng.Range(0, planets.Length);
                if (gapIndex < planets.Length - 1)
                {
                    innerRadiusKm = planets[gapIndex].orbitRadiusKm + planets[gapIndex].radiusKm * 5.0;
                    outerRadiusKm = planets[gapIndex + 1].orbitRadiusKm - planets[gapIndex + 1].radiusKm * 5.0;
                }
                else
                {
                    innerRadiusKm = planets[gapIndex].orbitRadiusKm + planets[gapIndex].radiusKm * 5.0;
                    outerRadiusKm = innerRadiusKm + rng.Range(50000.0, 150000.0);
                }
            }

            double widthKm = outerRadiusKm - innerRadiusKm;
            int rockCount = (int)(widthKm / 100.0 * rng.Range(0.5f, 2.0f)); // Density-based count

            return new BeltData(seed, innerRadiusKm, outerRadiusKm, rockCount, widthKm);
        }

        // Helper functions from spec

        private static int _weighted_count(ref SeededRng rng, int min, int max, float bias)
        {
            // Weighted random favoring lower counts
            float value = rng.NextFloat();
            float weighted = Mathf.Pow(value, bias);
            return min + (int)(weighted * (max - min + 1));
        }

        private static void _orbit_plane(ref SeededRng rng, double inclinationRad, out Vector3 planeX, out Vector3 planeY)
        {
            // Generate orbital plane vectors from inclination
            Vector3 normal = rng.OnUnitSphere();
            planeX = Vector3.Cross(normal, Vector3.up).normalized;
            if (planeX.sqrMagnitude < 0.01f)
                planeX = Vector3.Cross(normal, Vector3.forward).normalized;
            planeY = Vector3.Cross(normal, planeX).normalized;
        }

        private static void _hill_clamp(ref MoonData moon, double planetRadiusKm, double planetMassKg, double planetOrbitKm, double starMassKg)
        {
            // Calculate Hill sphere radius
            double hillRadius = planetOrbitKm * Math.Pow(planetMassKg / (3.0 * starMassKg), 1.0 / 3.0);
            
            // Clamp moon orbit to 1/3 of Hill sphere for stability
            double maxStableOrbit = hillRadius / 3.0;
            if (moon.orbitRadiusKm > maxStableOrbit)
            {
                // Recalculate with clamped orbit
                moon = new MoonData(moon.seed, moon.name, moon.radiusKm, moon.massKg,
                    maxStableOrbit, moon.orbitPeriodS, moon.eccentricity, moon.inclinationRad,
                    moon.phase, moon.orbitPlaneX, moon.orbitPlaneY);
            }
        }

        private static double CalculateOrbitalPeriod(double orbitRadiusKm, double centralMassKg)
        {
            // Kepler's third law: T = 2π√(r³/GM)
            return 2.0 * Math.PI * Math.Sqrt(orbitRadiusKm * orbitRadiusKm * orbitRadiusKm / (G * centralMassKg));
        }
    }
}
