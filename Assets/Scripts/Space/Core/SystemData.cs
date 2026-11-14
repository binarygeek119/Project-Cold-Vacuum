using System;
using UnityEngine;

namespace Space.Core
{
    /// <summary>
    /// Immutable data structures for a complete star system.
    /// All values in kilometers, seconds, and kilograms.
    /// </summary>
    
    [System.Serializable]
    public struct StarData
    {
        public readonly ulong seed;
        public readonly string name;
        public readonly double radiusKm;
        public readonly double massKg;
        public readonly float temperatureK;
        public readonly Vector3 positionKm;
        public readonly Vector3 spinAxis;
        public readonly double rotationPeriodS;
        public readonly bool isBinary;
        public readonly double companionOffsetKm;
        public readonly double companionRadiusKm;
        public readonly double companionMassKg;

        public StarData(ulong seed, string name, double radiusKm, double massKg, float temperatureK,
            Vector3 positionKm, Vector3 spinAxis, double rotationPeriodS, bool isBinary,
            double companionOffsetKm, double companionRadiusKm, double companionMassKg)
        {
            this.seed = seed;
            this.name = name;
            this.radiusKm = radiusKm;
            this.massKg = massKg;
            this.temperatureK = temperatureK;
            this.positionKm = positionKm;
            this.spinAxis = spinAxis;
            this.rotationPeriodS = rotationPeriodS;
            this.isBinary = isBinary;
            this.companionOffsetKm = companionOffsetKm;
            this.companionRadiusKm = companionRadiusKm;
            this.companionMassKg = companionMassKg;
        }
    }

    [System.Serializable]
    public struct MoonData
    {
        public readonly ulong seed;
        public readonly string name;
        public readonly double radiusKm;
        public readonly double massKg;
        public readonly double orbitRadiusKm;
        public readonly double orbitPeriodS;
        public readonly double eccentricity;
        public readonly double inclinationRad;
        public readonly double phase;
        public readonly Vector3 orbitPlaneX;
        public readonly Vector3 orbitPlaneY;

        public MoonData(ulong seed, string name, double radiusKm, double massKg,
            double orbitRadiusKm, double orbitPeriodS, double eccentricity,
            double inclinationRad, double phase, Vector3 orbitPlaneX, Vector3 orbitPlaneY)
        {
            this.seed = seed;
            this.name = name;
            this.radiusKm = radiusKm;
            this.massKg = massKg;
            this.orbitRadiusKm = orbitRadiusKm;
            this.orbitPeriodS = orbitPeriodS;
            this.eccentricity = eccentricity;
            this.inclinationRad = inclinationRad;
            this.phase = phase;
            this.orbitPlaneX = orbitPlaneX;
            this.orbitPlaneY = orbitPlaneY;
        }
    }

    [System.Serializable]
    public struct StationData
    {
        public readonly ulong seed;
        public readonly string name;
        public readonly double orbitRadiusKm;
        public readonly double orbitPeriodS;
        public readonly double phase;
        public readonly int stationType; // 0-3 for different visual types

        public StationData(ulong seed, string name, double orbitRadiusKm, double orbitPeriodS,
            double phase, int stationType)
        {
            this.seed = seed;
            this.name = name;
            this.orbitRadiusKm = orbitRadiusKm;
            this.orbitPeriodS = orbitPeriodS;
            this.phase = phase;
            this.stationType = stationType;
        }
    }

    [System.Serializable]
    public struct PlanetData
    {
        public readonly ulong seed;
        public readonly string name;
        public readonly double radiusKm;
        public readonly double massKg;
        public readonly double orbitRadiusKm;
        public readonly double orbitPeriodS;
        public readonly double eccentricity;
        public readonly double inclinationRad;
        public readonly double phase;
        public readonly Vector3 orbitPlaneX;
        public readonly Vector3 orbitPlaneY;
        public readonly bool isGasGiant;
        public readonly bool hasRings;
        public readonly MoonData[] moons;
        public readonly StationData? station;

        public PlanetData(ulong seed, string name, double radiusKm, double massKg,
            double orbitRadiusKm, double orbitPeriodS, double eccentricity,
            double inclinationRad, double phase, Vector3 orbitPlaneX, Vector3 orbitPlaneY,
            bool isGasGiant, bool hasRings, MoonData[] moons, StationData? station)
        {
            this.seed = seed;
            this.name = name;
            this.radiusKm = radiusKm;
            this.massKg = massKg;
            this.orbitRadiusKm = orbitRadiusKm;
            this.orbitPeriodS = orbitPeriodS;
            this.eccentricity = eccentricity;
            this.inclinationRad = inclinationRad;
            this.phase = phase;
            this.orbitPlaneX = orbitPlaneX;
            this.orbitPlaneY = orbitPlaneY;
            this.isGasGiant = isGasGiant;
            this.hasRings = hasRings;
            this.moons = moons;
            this.station = station;
        }
    }

    [System.Serializable]
    public struct BeltData
    {
        public readonly ulong seed;
        public readonly double innerRadiusKm;
        public readonly double outerRadiusKm;
        public readonly int rockCount;
        public readonly double widthKm;

        public BeltData(ulong seed, double innerRadiusKm, double outerRadiusKm, int rockCount, double widthKm)
        {
            this.seed = seed;
            this.innerRadiusKm = innerRadiusKm;
            this.outerRadiusKm = outerRadiusKm;
            this.rockCount = rockCount;
            this.widthKm = widthKm;
        }
    }

    /// <summary>
    /// Complete star system data - immutable, deterministic from seed.
    /// </summary>
    [System.Serializable]
    public struct SystemData
    {
        public readonly ulong systemSeed;
        public readonly StarData star;
        public readonly PlanetData[] planets;
        public readonly BeltData[] belts;

        public SystemData(ulong systemSeed, StarData star, PlanetData[] planets, BeltData[] belts)
        {
            this.systemSeed = systemSeed;
            this.star = star;
            this.planets = planets;
            this.belts = belts;
        }
    }
}
