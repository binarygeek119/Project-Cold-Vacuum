using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Streams whole star systems (not a 3D chunk grid). Systems are placed with
/// lateral/vertical scatter so the universe is 3D, and are loaded/unloaded by
/// radial distance. Generation is infinite and seeded for reproducibility.
/// </summary>
public class SpaceStreamer : MonoBehaviour
{
    [Header("Streaming")]
    public Transform target;
    public float loadDistance = 6000f;
    public float unloadDistance = 14000f;

    [Header("Content")]
    public Material asteroidMaterial;
    public List<GameObject> asteroidPrefabs;
    public GameObject planetPrefab;
    public GameObject stationPrefab;
    public GameObject enemyShipPrefab;
    public GameObject starPrefab;
    public Material nebulaMaterial;

    [Header("Nebula clouds")]
    public float nebulaMinScale = 12f;
    public float nebulaMaxScale = 25f;

    [Header("Star systems")]
    public int seed = 12345;
    public int systemsAhead = 8;
    public float firstSystemDistance = 4000f;
    public float systemSpacingMin = 20000f;
    public float systemSpacingMax = 45000f;
    public float systemRadiusMin = 1200f;
    public float systemRadiusMax = 2200f;
    public float spawnAheadDistance = 120000f;
    public float spawnConeDegrees = 65f;

    private Transform dome;

    private readonly List<StarSystem> systems = new List<StarSystem>();

    private class StarSystem
    {
        public Vector3 center;
        public float radius;
        public bool hasNebula;
        public int planetCount;
        public int asteroidCount;
        public bool hasStation;
        public bool hasEnemy;
        public GameObject root;
        public bool loaded;
    }

    private System.Random _rng;

    void Start()
    {
        if (target == null)
        {
            var ship = GameObject.Find("Player Ship");
            if (ship != null) target = ship.transform;
            else if (Camera.main != null) target = Camera.main.transform;
        }
        dome = GameObject.Find("Starfield Dome")?.transform;

        _rng = new System.Random(seed);
        GenerateSystems();

        if (Camera.main == null)
            Debug.LogWarning("SpaceStreamer: Camera.main is null - tag your camera 'MainCamera' or StarLOD/StarField may not work.");
    }

    private void GenerateSystems()
    {
        systems.Clear();
        Vector3 fwd = (target != null) ? target.forward : Vector3.forward;
        Vector3 cursor = Vector3.zero;
        for (int i = 0; i < systemsAhead; i++)
        {
            float spacing = (i == 0) ? firstSystemDistance : Lerp(systemSpacingMin, systemSpacingMax, (float)_rng.NextDouble());
            cursor += fwd * spacing;
            float radius = Lerp(systemRadiusMin, systemRadiusMax, (float)_rng.NextDouble());
            Vector3 right = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.right : Vector3.Cross(fwd, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;
            Vector3 lateral = right * ((float)(_rng.NextDouble() * 2 - 1) * radius)
                            + up * ((float)(_rng.NextDouble() * 2 - 1) * radius * 0.25f);
            var sys = new StarSystem
            {
                center = cursor + lateral,
                radius = radius,
                hasNebula = _rng.NextDouble() < 0.5,
                planetCount = _rng.Next(1, 5),
                asteroidCount = _rng.Next(20, 60),
                hasStation = _rng.NextDouble() < 0.4,
                hasEnemy = _rng.NextDouble() < 0.5,
            };
            systems.Add(sys);
        }
    }

    public Vector3 GetCurrentSystemCenter()
    {
        if (target == null) return Vector3.zero;
        var sys = GetNearestSystem(target.position);
        return sys != null ? sys.center : Vector3.zero;
    }

    public float GetCurrentSystemRadius()
    {
        if (target == null) return 0f;
        var sys = GetNearestSystem(target.position);
        return sys != null ? sys.radius : 0f;
    }

    public bool IsInStarSystem()
    {
        if (target == null) return false;
        var sys = GetNearestSystem(target.position);
        if (sys == null) return false;
        return Vector3.Distance(target.position, sys.center) <= sys.radius;
    }

    public bool TryGetNearestSystem(Vector3 pos, out Vector3 center, out float radius)
    {
        var sys = GetNearestSystem(pos);
        if (sys == null) { center = Vector3.zero; radius = 0f; return false; }
        center = sys.center;
        radius = sys.radius;
        return true;
    }

    private StarSystem GetNearestSystem(Vector3 pos)
    {
        StarSystem nearest = null;
        float best = float.MaxValue;
        foreach (var s in systems)
        {
            float d = Vector3.Distance(pos, s.center);
            if (d < best) { best = d; nearest = s; }
        }
        return nearest;
    }

    void Update()
    {
        if (target == null)
        {
            var ship = GameObject.Find("Player Ship");
            if (ship != null) target = ship.transform;
            else if (Camera.main != null) target = Camera.main.transform;
        }
        if (target == null) return;

        if (dome != null) dome.position = target.position;

        // Load / unload by radial distance
        foreach (var sys in systems)
        {
            float d = Vector3.Distance(target.position, sys.center);
            if (!sys.loaded && d < loadDistance)
            {
                BuildSystem(sys);
                sys.loaded = true;
            }
            else if (sys.loaded && d > unloadDistance)
            {
                if (sys.root != null) Destroy(sys.root);
                sys.root = null;
                sys.loaded = false;
            }
        }

        RemoveDistantSystems();
        EnsureSystemsAroundShip();
    }

    private void RemoveDistantSystems()
    {
        int behind = 0;
        for (int i = systems.Count - 1; i >= 0; i--)
        {
            Vector3 to = systems[i].center - target.position;
            float d = to.magnitude;
            bool isBehind = Vector3.Dot(to, target.forward) < 0f;
            if (d > spawnAheadDistance * 1.5f) { Cull(systems[i]); systems.RemoveAt(i); continue; }
            if (d > unloadDistance && isBehind)
            {
                if (behind >= 2) { Cull(systems[i]); systems.RemoveAt(i); }
                else behind++;
            }
        }
    }

    private void Cull(StarSystem sys)
    {
        if (sys.loaded && sys.root != null) Destroy(sys.root);
        sys.loaded = false;
        sys.root = null;
    }

    private void EnsureSystemsAroundShip()
    {
        int ahead = 0;
        foreach (var s in systems)
        {
            Vector3 to = s.center - target.position;
            if (Vector3.Dot(to, target.forward) > 0f && to.magnitude < spawnAheadDistance) ahead++;
        }

        if (_rng == null) _rng = new System.Random(seed);
        int safety = 0;
        while (ahead < systemsAhead && safety < systemsAhead * 6)
        {
            safety++;
            Vector3 dir = RandomConeDir(target.forward, spawnConeDegrees);
            float spacing = Lerp(systemSpacingMin, systemSpacingMax, (float)_rng.NextDouble());
            float radius = Lerp(systemRadiusMin, systemRadiusMax, (float)_rng.NextDouble());
            Vector3 center = target.position + dir * spacing;

            if (TooClose(center)) continue;

            var sys = new StarSystem
            {
                center = center,
                radius = radius,
                hasNebula = _rng.NextDouble() < 0.5,
                planetCount = _rng.Next(1, 5),
                asteroidCount = _rng.Next(20, 60),
                hasStation = _rng.NextDouble() < 0.4,
                hasEnemy = _rng.NextDouble() < 0.5,
            };
            systems.Add(sys);
            ahead++;
        }
    }

    private bool TooClose(Vector3 center)
    {
        foreach (var s in systems)
            if (Vector3.Distance(s.center, center) < systemSpacingMin * 0.35f) return true;
        return false;
    }

    private Vector3 RandomConeDir(Vector3 fwd, float halfAngleDeg)
    {
        if (_rng == null) _rng = new System.Random(seed);
        float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
        float cosMax = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        float cosT = Lerp(cosMax, 1f, (float)_rng.NextDouble());
        float sinT = Mathf.Sqrt(Mathf.Max(0f, 1f - cosT * cosT));
        Vector3 up = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 right = Vector3.Cross(up, fwd).normalized;
        Vector3 trueUp = Vector3.Cross(fwd, right);
        return (fwd * cosT + right * (Mathf.Cos(a) * sinT) + trueUp * (Mathf.Sin(a) * sinT)).normalized;
    }

    private void BuildSystem(StarSystem sys)
    {
        var rng = new System.Random(sys.center.GetHashCode() ^ seed);
        var root = new GameObject("System");
        root.transform.position = sys.center;

        // --- Star ---
        var starParent = new GameObject("Star");
        starParent.transform.SetParent(root.transform, false);

        float starScale = Lerp(40f, 80f, (float)rng.NextDouble());

        // Light (always active)
        var lightGO = new GameObject("Star Light");
        lightGO.transform.SetParent(starParent.transform, false);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.95f, 0.8f);
        light.intensity = 8f;
        light.range = starScale * 8f;

        // Glow (always active)
        var glowGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glowGO.name = "Star Glow";
        glowGO.transform.SetParent(starParent.transform, false);
        glowGO.transform.localScale = Vector3.one * starScale * 1.05f;
        var gCol = glowGO.GetComponent<Collider>();
        if (gCol != null) Destroy(gCol);
        var gRend = glowGO.GetComponent<Renderer>();
        if (gRend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            Color sc = new Color(1f, 0.95f, 0.8f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", sc * 0.45f);
            if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", sc * 0.8f); }
            gRend.material = mat;
            gRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Far representation (toggled) - emissive core
        var farGO = new GameObject("Star Far");
        farGO.transform.SetParent(starParent.transform, false);
        var farCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        farCore.name = "Star Far Core";
        farCore.transform.SetParent(farGO.transform, false);
        farCore.transform.localScale = Vector3.one * starScale * 0.9f;
        var fcCol = farCore.GetComponent<Collider>();
        if (fcCol != null) Destroy(fcCol);
        var fcRend = farCore.GetComponent<Renderer>();
        if (fcRend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            Color sc = new Color(1f, 0.95f, 0.8f);
            mat.color = sc;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sc);
            if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", sc * 3f); }
            fcRend.material = mat;
            fcRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Twinkle (always active, moved out of farGO)
        var twinkleGO = new GameObject("Star Twinkle");
        twinkleGO.transform.SetParent(starParent.transform, false);
        var twinkle = twinkleGO.AddComponent<ParticleSystem>();
        var tmain = twinkle.main;
        tmain.startLifetime = 1f;
        tmain.startSpeed = 0f;
        tmain.startSize = starScale * 0.5f;
        tmain.startColor = new Color(1f, 0.95f, 0.8f, 1f);
        tmain.maxParticles = 1;
        tmain.loop = true;
        tmain.playOnAwake = true;
        var tem = twinkle.emission;
        tem.rateOverTime = 2f;
        var tshape = twinkle.shape;
        tshape.shapeType = ParticleSystemShapeType.Sphere;
        tshape.radius = 0.1f;
        var tcol = twinkle.colorOverLifetime;
        tcol.enabled = true;
        var tgrad = new Gradient();
        tgrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f,0.95f,0.8f), 0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.2f, 0.5f), new GradientAlphaKey(0.6f, 1f) });
        tcol.color = tgrad;

        // Near representation
        GameObject nearGO;
        if (starPrefab != null)
        {
            nearGO = Instantiate(starPrefab);
            nearGO.name = "Star Near";
            nearGO.transform.SetParent(starParent.transform, false);
            nearGO.transform.localScale = Vector3.one * starScale;
        }
        else
        {
            nearGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nearGO.name = "Star Near";
            nearGO.transform.SetParent(starParent.transform, false);
            nearGO.transform.localScale = Vector3.one * starScale;
            var rend = nearGO.GetComponent<Renderer>();
            if (rend != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                if (sh == null) sh = Shader.Find("Standard");
                var mat = new Material(sh);
                Color sc = new Color(1f, 0.95f, 0.8f);
                mat.color = sc;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sc);
                if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", sc * 2f); }
                rend.material = mat;
            }
            var col = nearGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        var lod = starParent.AddComponent<StarLOD>();
        lod.farRepresentation = farGO;
        lod.nearRepresentation = nearGO;
        lod.lodDistance = starScale * 6f;
        lod.verbose = true;

        starParent.transform.localScale = Vector3.one;
        var starCT = starParent.AddComponent<CompassTarget>();
        starCT.displayColor = new Color(1f, 0.9f, 0.3f);
        starCT.displayName = "Star";

        // --- Planets ---
        for (int i = 0; i < sys.planetCount; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r = Lerp(sys.radius * 0.35f, sys.radius * 0.85f, (float)rng.NextDouble());
            Vector3 offset = new Vector3(Mathf.Cos(ang) * r, (float)(rng.NextDouble() * 120 - 60), Mathf.Sin(ang) * r);

            GameObject planet;
            if (planetPrefab != null)
            {
                planet = Instantiate(planetPrefab);
            }
            else
            {
                planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var col = planet.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var sc = planet.AddComponent<SphereCollider>();
                sc.radius = 0.5f;
                sc.isTrigger = true;
                planet.AddComponent<GasGiant>();
            }
            planet.transform.SetParent(root.transform, false);
            planet.transform.localPosition = offset;
            float pScale = Lerp(50f, 120f, (float)rng.NextDouble());
            planet.transform.localScale = Vector3.one * pScale;
            if (planet.GetComponent<CompassTarget>() == null)
            {
                var pCT = planet.AddComponent<CompassTarget>();
                pCT.displayColor = new Color(0.3f, 0.6f, 1f);
                pCT.displayName = "Planet";
            }
            var gg = planet.GetComponent<GasGiant>();
            if (gg == null) gg = planet.AddComponent<GasGiant>();
            if (gg != null)
            {
                gg.useOrbitCenter = true;
                gg.orbitCenter = sys.center;
            }
        }

        // --- Asteroids ---
        for (int i = 0; i < sys.asteroidCount; i++)
        {
            float arx = (float)(rng.NextDouble() * 2 - 1);
            float ary = (float)(rng.NextDouble() * 2 - 1);
            float arz = (float)(rng.NextDouble() * 2 - 1);
            Vector3 aoffset = new Vector3(arx, ary, arz).normalized * (float)(rng.NextDouble() * sys.radius * 0.8);
            aoffset.y *= 0.4f;
            Vector3 apos = aoffset;

            GameObject ast;
            if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
            {
                var prefab = asteroidPrefabs[rng.Next(asteroidPrefabs.Count)];
                ast = Instantiate(prefab);
                if (ast.GetComponent<Rigidbody>() == null)
                {
                    var prb = ast.AddComponent<Rigidbody>();
                    prb.useGravity = false;
                    prb.linearDamping = 1f;
                    prb.angularDamping = 0.5f;
                    ast.AddComponent<AsteroidMotion>();
                }
            }
            else
            {
                ast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var rend = ast.GetComponent<Renderer>();
                if (rend != null && asteroidMaterial != null) rend.sharedMaterial = asteroidMaterial;
                if (ast.GetComponent<Collider>() == null) ast.AddComponent<SphereCollider>();
                var prb = ast.AddComponent<Rigidbody>();
                prb.useGravity = false;
                prb.linearDamping = 1f;
                prb.angularDamping = 0.5f;
                ast.AddComponent<AsteroidMotion>();
            }
            ast.transform.SetParent(root.transform, false);
            ast.transform.localPosition = apos;
            float ascale = 1f + (float)rng.NextDouble() * 2f;
            ast.transform.localScale = Vector3.one * ascale;
            var aCT = ast.AddComponent<CompassTarget>();
            aCT.displayColor = new Color(0.6f, 0.6f, 0.6f);
            aCT.displayName = "Asteroid";
        }

        // --- Station ---
        if (sys.hasStation && stationPrefab != null)
        {
            var stationRng = new System.Random(sys.center.GetHashCode() ^ "station".GetHashCode());
            float srx = (float)(stationRng.NextDouble() * 2 - 1);
            float sry = (float)(stationRng.NextDouble() * 2 - 1);
            float srz = (float)(stationRng.NextDouble() * 2 - 1);
            Vector3 stOffset = new Vector3(srx, sry, srz).normalized * (float)(stationRng.NextDouble() * sys.radius * 0.5);
            stOffset.y *= 0.2f;

            var st = Instantiate(stationPrefab);
            st.transform.SetParent(root.transform, false);
            st.transform.localPosition = stOffset;
            st.transform.localRotation = Quaternion.Euler(0f, (float)stationRng.NextDouble() * 360f, 0f);
            if (st.GetComponent<CompassTarget>() == null)
            {
                var stCT = st.AddComponent<CompassTarget>();
                stCT.displayColor = Color.white;
                stCT.displayName = "Station";
            }
        }

        // --- Enemy ---
        if (sys.hasEnemy && enemyShipPrefab != null)
        {
            var enemyRng = new System.Random(sys.center.GetHashCode() ^ "enemy".GetHashCode());
            float erx = (float)(enemyRng.NextDouble() * 2 - 1);
            float ery = (float)(enemyRng.NextDouble() * 2 - 1);
            float erz = (float)(enemyRng.NextDouble() * 2 - 1);
            Vector3 eOffset = new Vector3(erx, ery, erz).normalized * (float)(enemyRng.NextDouble() * sys.radius * 0.7);
            eOffset.y *= 0.3f;

            var ev = Instantiate(enemyShipPrefab);
            ev.transform.SetParent(root.transform, false);
            ev.transform.localPosition = eOffset;
            ev.transform.localRotation = Quaternion.Euler(0f, (float)enemyRng.NextDouble() * 360f, 0f);
            if (ev.GetComponent<EnemyShip>() == null) ev.AddComponent<EnemyShip>();
            if (ev.GetComponent<CompassTarget>() == null)
            {
                var evCT = ev.AddComponent<CompassTarget>();
                evCT.displayColor = new Color(1f, 0.3f, 0.3f);
                evCT.displayName = "Enemy";
            }
        }

        // --- Nebula ---
        if (sys.hasNebula && rng.NextDouble() < 0.3)
        {
            var nebulaRng = new System.Random(sys.center.GetHashCode() ^ "nebula".GetHashCode());
            float nrx = (float)(nebulaRng.NextDouble() * 2 - 1);
            float nry = (float)(nebulaRng.NextDouble() * 2 - 1);
            float nrz = (float)(nebulaRng.NextDouble() * 2 - 1);
            Vector3 nOffset = new Vector3(nrx, nry, nrz).normalized * (float)(nebulaRng.NextDouble() * sys.radius * 0.6);
            nOffset.y *= 0.2f;

            var neb = BuildNebula(rng);
            neb.transform.SetParent(root.transform, false);
            neb.transform.localPosition = nOffset;
            float ns = nebulaMinScale + (float)nebulaRng.NextDouble() * (nebulaMaxScale - nebulaMinScale);
            neb.transform.localScale = Vector3.one * ns;
            var nCT = neb.AddComponent<CompassTarget>();
            nCT.displayColor = new Color(0.7f, 0.3f, 1f);
            nCT.displayName = "Nebula";
        }

        sys.root = root;
    }

    private GameObject BuildNebula(System.Random rng)
    {
        var parent = new GameObject("Nebula Cloud");
        var ps = parent.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 15f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 7f);
        Color[] palette = new Color[] {
            new Color(1f, 0.95f, 0.9f),
            new Color(0.9f, 0.95f, 1f),
            new Color(0.95f, 0.9f, 1f),
            new Color(0.9f, 1f, 0.95f),
        };
        Color c = palette[rng.Next(palette.Length)];
        main.startColor = c;
        main.maxParticles = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = true;
        var em = ps.emission;
        em.rateOverTime = 30f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1f;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0.5f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.3f));
        var rend = parent.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var tex = (nebulaMaterial != null && nebulaMaterial.HasProperty("_BaseMap")) ? nebulaMaterial.GetTexture("_BaseMap") : null;
            var sh = Shader.Find("Custom/CloudParticle");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(sh);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            mat.color = Color.white;
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        parent.AddComponent<NebulaCloud>();
        return parent;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
