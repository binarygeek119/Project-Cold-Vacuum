using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Streams discrete star systems separated by empty space.
/// Each system has a star, planets, asteroids, and (sometimes) nebula clouds.
/// Between systems there is blank space (no spawning).
/// </summary>
public class SpaceStreamer : MonoBehaviour
{
    [Header("Streaming")]
    public Transform target;
    public float chunkSize = 100f;
    public int loadRadius = 1;
    public int unloadRadius = 2;
    public int maxSpawnsPerFrame = 4;
    public int maxDestroysPerFrame = 4;

    [Header("Content")]
    public Material asteroidMaterial;
    public System.Collections.Generic.List<GameObject> asteroidPrefabs;
    public GameObject planetPrefab;
    public GameObject stationPrefab;
    public GameObject enemyShipPrefab;
    public GameObject starPrefab;
    public Material nebulaMaterial;

    [Header("Nebula clouds")]
    public float nebulaMinScale = 12f;
    public float nebulaMaxScale = 25f;

    [Header("Star systems")]
    public int minSystems = 3;
    public int maxSystems = 5;

    private readonly Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    private readonly Queue<Vector3Int> loadQueue = new Queue<Vector3Int>();
    private readonly Queue<Vector3Int> unloadQueue = new Queue<Vector3Int>();
    private readonly HashSet<Vector3Int> queued = new HashSet<Vector3Int>();

    private Transform dome;

    // Star systems
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
    }

    private class Chunk
    {
        public Vector3Int coord;
        public List<GameObject> instances = new List<GameObject>();
    }

    void Start()
    {
        if (target == null)
        {
            var ship = GameObject.Find("Player Ship");
            if (ship != null) target = ship.transform;
            else if (Camera.main != null) target = Camera.main.transform;
        }
        dome = GameObject.Find("Starfield Dome")?.transform;

        GenerateSystems();
    }

    private void GenerateSystems()
    {
        systems.Clear();
        int count = Random.Range(minSystems, maxSystems + 1);
        float z = 1000f; // start far away
        for (int i = 0; i < count; i++)
        {
            // increasing gaps between systems for "blank space"
            if (i > 0) z += Random.Range(600f, 1000f);
            float radius = Random.Range(150f, 280f);
            var sys = new StarSystem
            {
                center = new Vector3(0f, 0f, z),
                radius = radius,
                hasNebula = Random.value < 0.5f, // some systems have nebula clouds
                planetCount = Random.Range(0, 4),
                asteroidCount = Random.Range(8, 25),
                hasStation = Random.value < 0.4f,
                hasEnemy = Random.value < 0.5f,
            };
            systems.Add(sys);
            z += radius; // system extends to radius
        }
    }

    // Find which system (if any) a world position belongs to
    private StarSystem GetSystem(Vector3 pos)
    {
        foreach (var sys in systems)
        {
            if (Vector3.Distance(pos, sys.center) <= sys.radius)
                return sys;
        }
        return null;
    }

    void Update()
    {
        if (target == null) return;

        // Keep starfield dome centered on the ship
        if (dome != null) dome.position = target.position;

        Vector3Int center = WorldToChunk(target.position);

        // Desired chunk set
        var desired = new HashSet<Vector3Int>();
        for (int x = -loadRadius; x <= loadRadius; x++)
        for (int y = -loadRadius; y <= loadRadius; y++)
        for (int z = -loadRadius; z <= loadRadius; z++)
            desired.Add(new Vector3Int(center.x + x, center.y + y, center.z + z));

        // Enqueue loads
        foreach (var c in desired)
        {
            if (!chunks.ContainsKey(c) && !queued.Contains(c))
            {
                queued.Add(c);
                loadQueue.Enqueue(c);
            }
        }

        // Enqueue unloads
        foreach (var kv in chunks)
        {
            if (ChunkDistance(kv.Key, center) > unloadRadius && !queued.Contains(kv.Key))
            {
                queued.Add(kv.Key);
                unloadQueue.Enqueue(kv.Key);
            }
        }

        // Process loads
        int spawns = 0;
        while (loadQueue.Count > 0 && spawns < maxSpawnsPerFrame)
        {
            var coord = loadQueue.Dequeue();
            queued.Remove(coord);
            if (chunks.ContainsKey(coord)) continue;
            if (!desired.Contains(coord)) continue;
            LoadChunk(coord);
            spawns++;
        }

        // Process unloads
        int destroys = 0;
        while (unloadQueue.Count > 0 && destroys < maxDestroysPerFrame)
        {
            var coord = unloadQueue.Dequeue();
            queued.Remove(coord);
            if (!chunks.ContainsKey(coord)) continue;
            UnloadChunk(coord);
            destroys++;
        }
    }

    private void LoadChunk(Vector3Int coord)
    {
        var chunk = new Chunk { coord = coord };
        chunks[coord] = chunk;

        Vector3 origin = new Vector3(coord.x, coord.y, coord.z) * chunkSize;
        Vector3 chunkCenter = origin + new Vector3(chunkSize * 0.5f, chunkSize * 0.5f, chunkSize * 0.5f);

        // Check if this chunk is inside a star system
        var sys = GetSystem(chunkCenter);
        if (sys == null)
        {
            // Blank space — no spawning
            return;
        }

        // Seeded RNG for this chunk
        var rng = new System.Random(Hash(coord) ^ sys.center.GetHashCode());

        // Spawn star (at system center chunk)
        Vector3Int sysChunk = WorldToChunk(sys.center);
        if (coord == sysChunk)
        {
            // Star with LOD: far (light + twinkle) / near (full model)
            var starParent = new GameObject("Star");
            starParent.transform.position = sys.center;
            float starScale = Random.Range(40f, 80f);

            // Far representation: point light + small twinkling sprite
            var farGO = new GameObject("Star Far");
            farGO.transform.SetParent(starParent.transform, false);
            var light = farGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.95f, 0.8f);
            light.intensity = 3f;
            light.range = starScale * 3f;
            // twinkle particle
            var twinkle = farGO.AddComponent<ParticleSystem>();
            var tmain = twinkle.main;
            tmain.startLifetime = 1f;
            tmain.startSpeed = 0f;
            tmain.startSize = starScale * 0.3f;
            tmain.startColor = new Color(1f, 0.95f, 0.8f, 1f);
            tmain.maxParticles = 1;
            tmain.loop = true;
            tmain.playOnAwake = true;
            var tem = twinkle.emission;
            tem.rateOverTime = 1f;
            var tshape = twinkle.shape;
            tshape.shapeType = ParticleSystemShapeType.Sphere;
            tshape.radius = 0.1f;
            var tcol = twinkle.colorOverLifetime;
            tcol.enabled = true;
            var tgrad = new Gradient();
            tgrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f,0.95f,0.8f), 0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(0.8f, 1f) });
            tcol.color = tgrad;

            // Near representation: full star model
            GameObject nearGO = null;
            if (starPrefab != null)
            {
                nearGO = (GameObject)Instantiate(starPrefab);
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
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = new Color(1f, 0.95f, 0.8f);
                    if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.7f) * 2f); }
                    rend.material = mat;
                }
                var col = nearGO.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
            }

            // LOD component
            var lod = starParent.AddComponent<StarLOD>();
            lod.farRepresentation = farGO;
            lod.nearRepresentation = nearGO;
            lod.lodDistance = 120f;

            // scale the parent (visual only, children already scaled)
            starParent.transform.localScale = Vector3.one;

            chunk.instances.Add(starParent);
        }

        // Planets
        for (int i = 0; i < sys.planetCount; i++)
        {
            // Distribute planets around the system center
            Vector3 offset = Random.insideUnitSphere * (sys.radius * 0.6f);
            offset.y *= 0.3f; // flatten
            Vector3 pos = sys.center + offset;
            // only spawn if this planet is in this chunk
            if (WorldToChunk(pos) != coord) continue;

            GameObject planet;
            if (planetPrefab != null)
            {
                planet = (GameObject)Instantiate(planetPrefab);
            }
            else
            {
                planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var col = planet.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
                var sc = planet.AddComponent<SphereCollider>();
                sc.radius = 0.5f;
                sc.isTrigger = true;
                planet.AddComponent<GasGiant>();
            }
            planet.transform.position = pos;
            float pScale = Random.Range(50f, 120f);
            planet.transform.localScale = Vector3.one * pScale;
            // orbit the star (system center)
            var gg = planet.GetComponent<GasGiant>();
            if (gg != null)
            {
                gg.useOrbitCenter = true;
                gg.orbitCenter = sys.center;
            }
            chunk.instances.Add(planet);
        }

        // Asteroids
        for (int i = 0; i < sys.asteroidCount; i++)
        {
            Vector3 aoffset = Random.insideUnitSphere * (sys.radius * 0.8f);
            aoffset.y *= 0.4f;
            Vector3 apos = sys.center + aoffset;
            if (WorldToChunk(apos) != coord) continue;

            GameObject ast;
            if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
            {
                var prefab = asteroidPrefabs[rng.Next(asteroidPrefabs.Count)];
                ast = (GameObject)Instantiate(prefab);
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
            ast.transform.position = apos;
            float ascale = Random.Range(1f, 3f);
            ast.transform.localScale = Vector3.one * ascale;
            chunk.instances.Add(ast);
        }

        // Station (rare, per system)
        if (sys.hasStation && stationPrefab != null)
        {
            // place station near system edge
            Vector3 stOffset = Random.insideUnitSphere * (sys.radius * 0.5f);
            stOffset.y *= 0.2f;
            Vector3 stPos = sys.center + stOffset;
            if (WorldToChunk(stPos) == coord)
            {
                var st = (GameObject)Instantiate(stationPrefab);
                st.transform.position = stPos;
                st.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                chunk.instances.Add(st);
            }
        }

        // Enemy Viper (per system)
        if (sys.hasEnemy && enemyShipPrefab != null)
        {
            Vector3 eOffset = Random.insideUnitSphere * (sys.radius * 0.7f);
            eOffset.y *= 0.3f;
            Vector3 ePos = sys.center + eOffset;
            if (WorldToChunk(ePos) == coord)
            {
                var ev = (GameObject)Instantiate(enemyShipPrefab);
                ev.transform.position = ePos;
                ev.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                if (ev.GetComponent<EnemyShip>() == null) ev.AddComponent<EnemyShip>();
                chunk.instances.Add(ev);
            }
        }

        // Nebula cloud (only in systems with nebula, and rare)
        if (sys.hasNebula && rng.NextDouble() < 0.3f)
        {
            var neb = BuildNebula(rng);
            Vector3 nOffset = Random.insideUnitSphere * (sys.radius * 0.6f);
            nOffset.y *= 0.2f;
            Vector3 nPos = sys.center + nOffset;
            if (WorldToChunk(nPos) == coord)
            {
                neb.transform.position = nPos;
                float ns = nebulaMinScale + (float)rng.NextDouble() * (nebulaMaxScale - nebulaMinScale);
                neb.transform.localScale = Vector3.one * ns;
                chunk.instances.Add(neb);
            }
            else
            {
                UnityEngine.Object.Destroy(neb);
            }
        }
    }

    private void UnloadChunk(Vector3Int coord)
    {
        if (!chunks.TryGetValue(coord, out var chunk)) return;
        foreach (var go in chunk.instances)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }
        chunk.instances.Clear();
        chunks.Remove(coord);
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

    private Vector3Int WorldToChunk(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.y / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize));
    }

    private int ChunkDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z));
    }

    private int Hash(Vector3Int c)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + c.x;
            h = h * 31 + c.y;
            h = h * 31 + c.z;
            return h;
        }
    }
}