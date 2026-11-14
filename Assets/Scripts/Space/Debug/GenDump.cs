#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Text;
using Space.Core;

namespace Space.Debug
{
    /// <summary>
    /// Editor tool to dump complete system data to text for debugging and verification.
    /// Access via Window > Space > Dump System
    /// </summary>
    public class GenDump : EditorWindow
    {
        private ulong seed = 12345;
        private string output = "";
        private Vector2 scrollPosition;

        [MenuItem("Window/Space/Dump System")]
        public static void ShowWindow()
        {
            GetWindow<GenDump>("System Dump");
        }

        void OnGUI()
        {
            GUILayout.Label("System Generator Debug", EditorStyles.boldLabel);
            
            seed = (ulong)EditorGUILayout.LongField("System Seed", (long)seed);
            
            if (GUILayout.Button("Generate & Dump"))
            {
                GenerateDump();
            }

            if (GUILayout.Button("Copy to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = output;
            }

            GUILayout.Space(10);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void GenerateDump()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            
            SystemData system = SystemGen.Generate(seed);
            
            sw.Stop();
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== SYSTEM DUMP ===");
            sb.AppendLine($"Seed: {seed}");
            sb.AppendLine($"Generation time: {sw.Elapsed.TotalMilliseconds:F3} ms");
            sb.AppendLine();
            
            // Star info
            sb.AppendLine("--- STAR ---");
            sb.AppendLine($"Name: {system.star.name}");
            sb.AppendLine($"Radius: {system.star.radiusKm:F0} km ({system.star.radiusKm / 696340.0:F2} solar radii)");
            sb.AppendLine($"Mass: {system.star.massKg:E3} kg ({system.star.massKg / 1.989e30:F2} solar masses)");
            sb.AppendLine($"Temperature: {system.star.temperatureK:F0} K");
            sb.AppendLine($"Rotation period: {system.star.rotationPeriodS / 86400.0:F2} days");
            sb.AppendLine($"Spin axis: {system.star.spinAxis}");
            sb.AppendLine($"Binary: {system.star.isBinary}");
            if (system.star.isBinary)
            {
                sb.AppendLine($"  Companion offset: {system.star.companionOffsetKm:F0} km");
                sb.AppendLine($"  Companion radius: {system.star.companionRadiusKm:F0} km");
                sb.AppendLine($"  Companion mass: {system.star.companionMassKg:E3} kg");
            }
            sb.AppendLine();
            
            // Planets
            sb.AppendLine($"--- PLANETS ({system.planets.Length}) ---");
            for (int i = 0; i < system.planets.Length; i++)
            {
                var planet = system.planets[i];
                sb.AppendLine($"\n[{i}] {planet.name}");
                sb.AppendLine($"  Type: {(planet.isGasGiant ? "Gas Giant" : "Rocky")}");
                sb.AppendLine($"  Radius: {planet.radiusKm:F0} km");
                sb.AppendLine($"  Mass: {planet.massKg:E3} kg");
                sb.AppendLine($"  Orbit radius: {planet.orbitRadiusKm:F0} km ({planet.orbitRadiusKm / 149597870.7:F3} AU)");
                sb.AppendLine($"  Orbit period: {planet.orbitPeriodS / 86400.0:F2} days");
                sb.AppendLine($"  Eccentricity: {planet.eccentricity:F3}");
                sb.AppendLine($"  Inclination: {planet.inclinationRad * 180.0 / Mathf.PI:F2}°");
                sb.AppendLine($"  Phase: {planet.phase:F3} rad");
                sb.AppendLine($"  Moons: {planet.moons.Length}");
                
                // Validate moon spacing
                double lastOrbit = planet.radiusKm * 2.0;
                for (int j = 0; j < planet.moons.Length; j++)
                {
                    var moon = planet.moons[j];
                    bool validOrbit = moon.orbitRadiusKm >= lastOrbit;
                    bool outsidePlanet = moon.orbitRadiusKm >= planet.radiusKm * 2.0;
                    
                    sb.AppendLine($"  Moon [{j}] {moon.name}");
                    sb.AppendLine($"    Radius: {moon.radiusKm:F1} km");
                    sb.AppendLine($"    Orbit: {moon.orbitRadiusKm:F0} km {(validOrbit ? "✓" : "✗ SPACING")} {(outsidePlanet ? "✓" : "✗ TOO CLOSE")}");
                    
                    lastOrbit = moon.orbitRadiusKm * 2.2;
                }
                
                if (planet.station.HasValue)
                {
                    var station = planet.station.Value;
                    sb.AppendLine($"  Station: {station.name} (orbit: {station.orbitRadiusKm:F0} km)");
                }
            }
            
            // Belts
            sb.AppendLine($"\n--- BELTS ({system.belts.Length}) ---");
            for (int i = 0; i < system.belts.Length; i++)
            {
                var belt = system.belts[i];
                sb.AppendLine($"[{i}] Belt");
                sb.AppendLine($"  Inner radius: {belt.innerRadiusKm:F0} km");
                sb.AppendLine($"  Outer radius: {belt.outerRadiusKm:F0} km");
                sb.AppendLine($"  Width: {belt.widthKm:F0} km");
                sb.AppendLine($"  Rock count: {belt.rockCount}");
            }
            
            // Validation summary
            sb.AppendLine("\n--- VALIDATION ---");
            bool allValid = true;
            
            // Check planet radii strictly increasing
            double lastPlanetOrbit = system.star.radiusKm * 5.0;
            for (int i = 0; i < system.planets.Length; i++)
            {
                if (system.planets[i].orbitRadiusKm <= lastPlanetOrbit)
                {
                    sb.AppendLine($"✗ Planet {i} orbit not strictly increasing");
                    allValid = false;
                }
                lastPlanetOrbit = system.planets[i].orbitRadiusKm;
            }
            
            // Check binary constraint
            if (system.star.isBinary)
            {
                for (int i = 0; i < system.planets.Length; i++)
                {
                    if (system.planets[i].orbitRadiusKm < system.star.companionOffsetKm * 9.0)
                    {
                        sb.AppendLine($"✗ Planet {i} inside binary exclusion zone (9 * {system.star.companionOffsetKm:F0} km)");
                        allValid = false;
                    }
                }
            }
            
            if (allValid)
            {
                sb.AppendLine("✓ All validations passed");
            }
            
            output = sb.ToString();
            
            UnityEngine.Debug.Log($"System {seed} generated in {sw.Elapsed.TotalMilliseconds:F3} ms");
        }

        [MenuItem("Window/Space/Test Seed Determinism")]
        public static void TestDeterminism()
        {
            ulong testSeed = 99999;
            var sys1 = SystemGen.Generate(testSeed);
            var sys2 = SystemGen.Generate(testSeed);
            
            bool identical = sys1.systemSeed == sys2.systemSeed &&
                            sys1.star.name == sys2.star.name &&
                            sys1.planets.Length == sys2.planets.Length;
            
            if (identical)
            {
                for (int i = 0; i < sys1.planets.Length; i++)
                {
                    if (sys1.planets[i].orbitRadiusKm != sys2.planets[i].orbitRadiusKm)
                    {
                        identical = false;
                        break;
                    }
                }
            }
            
            if (identical)
            {
                UnityEngine.Debug.Log($"✓ Seed {testSeed} produces identical systems");
            }
            else
            {
                UnityEngine.Debug.LogError($"✗ Seed {testSeed} produces DIFFERENT systems!");
            }
        }
    }
}
#endif
