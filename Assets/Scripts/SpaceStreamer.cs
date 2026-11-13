using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Streams an endless asteroid field around a target (the ship).
/// Chunks load/unload seamlessly as the target moves; placement is
/// deterministic per chunk so revisiting an area looks identical.
/// </summary>
public class SpaceStreamer : MonoBehaviour
{
    [Header("Streaming")]
    public Transform target;
    public float chunkSize = 100f;
    public int loadRadius = 1;     // chunks (cube) loaded around target
    public int unloadRadius = 2;   // chunks beyond this are unloaded
    public int asteroidsPerChunk = 2;
    public int maxSpawnsPerFrame = 4;
    public int maxDestroysPerFrame = 4;

    [Header("Content")]
    public GameObject asteroidPrefab;
    public Material asteroidMaterial;

    [Header("Starter Kit prefabs")]
    public System.Collections.Generic.List<GameObject> asteroidPrefabs;
    public GameObject planetPrefab;

    [Header("Composite asteroids")]
    public System.Collections.Generic.List<GameObject> asteroidModels;
    public int minPieces = 2;
    public int maxPieces = 4;
    public float compositeRadius = 0.8f;

    [Header("Nebula clouds")]
    public float nebulaChance = 0.35f;          // probability per chunk
    public float planetChance = 0.03f;           // probability per chunk (gas giants)
    public float nebulaMinScale = 18f;
    public float nebulaMaxScale = 40f;
    public Material nebulaMaterial;

    private readonly Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    private readonly Queue<Vector3Int> loadQueue = new Queue<Vector3Int>();
    private readonly Queue<Vector3Int> unloadQueue = new Queue<Vector3Int>();
    private readonly HashSet<Vector3Int> queued = new HashSet<Vector3Int>();

    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private Transform dome;

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
        if (asteroidMaterial == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            asteroidMaterial = new Material(sh);
            asteroidMaterial.color = new Color(0.55f, 0.5f, 0.45f);
        }
    }

    void Update()
    {
        if (target == null) return;

        // Keep starfield dome centered on the ship (endless space feel)
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

        // Enqueue unloads (loaded chunks outside unload radius)
        foreach (var kv in chunks)
        {
            if (ChunkDistance(kv.Key, center) > unloadRadius && !queued.Contains(kv.Key))
            {
                queued.Add(kv.Key);
                unloadQueue.Enqueue(kv.Key);
            }
        }

        // Process loads (limited per frame)
        int spawns = 0;
        while (loadQueue.Count > 0 && spawns < maxSpawnsPerFrame)
        {
            var coord = loadQueue.Dequeue();
            queued.Remove(coord);
            if (chunks.ContainsKey(coord)) continue;
            // only load if still desired (avoid stale)
            if (!desired.Contains(coord)) continue;
            LoadChunk(coord);
            spawns++;
        }

        // Process unloads (limited per frame)
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

        var rng = new System.Random(Hash(coord));
        Vector3 origin = new Vector3(coord.x, coord.y, coord.z) * chunkSize;

        for (int i = 0; i < asteroidsPerChunk; i++)
        {
            Vector3 local = new Vector3(
                (float)rng.NextDouble() * chunkSize,
                (float)rng.NextDouble() * chunkSize,
                (float)rng.NextDouble() * chunkSize);
            Vector3 pos = origin + local;

            var go = BuildAsteroid(rng);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);
            float sc = 1f + (float)rng.NextDouble() * 1.5f;
            go.transform.localScale = Vector3.one * sc;
            chunk.instances.Add(go);
        }

        // Nebula cloud (random, colorful, glowing)
        if (rng.NextDouble() < nebulaChance)
        {
            var neb = BuildNebula(rng);
            Vector3 nlocal = new Vector3(
                (float)rng.NextDouble() * chunkSize,
                (float)rng.NextDouble() * chunkSize,
                (float)rng.NextDouble() * chunkSize);
            neb.transform.position = origin + nlocal;
            neb.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float ns = nebulaMinScale + (float)rng.NextDouble() * (nebulaMaxScale - nebulaMinScale);
            neb.transform.localScale = Vector3.one * ns;
            chunk.instances.Add(neb);
        }

        // Gas giant planet (rare)
        if (rng.NextDouble() < planetChance)
        {
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var pcol = planet.GetComponent<Collider>();
            if (pcol != null) UnityEngine.Object.Destroy(pcol);
            var psc = planet.AddComponent<SphereCollider>();
            psc.radius = 0.5f;
            psc.isTrigger = true;
            planet.AddComponent<GasGiant>();
            Vector3 plocal = new Vector3(
                (float)rng.NextDouble() * chunkSize,
                (float)rng.NextDouble() * chunkSize,
                (float)rng.NextDouble() * chunkSize);
            planet.transform.position = origin + plocal;
            float pscale = 50f + (float)rng.NextDouble() * 70f;
            planet.transform.localScale = Vector3.one * pscale;
            chunk.instances.Add(planet);
        }
    }

    private void UnloadChunk(Vector3Int coord)
    {
        if (!chunks.TryGetValue(coord, out var chunk)) return;
        foreach (var go in chunk.instances)
        {
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
        chunk.instances.Clear();
        chunks.Remove(coord);
    }

    private GameObject BuildAsteroid(System.Random rng)
    {
        if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
        {
            var prefab = asteroidPrefabs[rng.Next(asteroidPrefabs.Count)];
            if (prefab != null)
            {
                var go = (GameObject)Instantiate(prefab);
                // ensure collider
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = 0.5f;
                }
                // add rigidbody + motion if missing
                if (go.GetComponent<Rigidbody>() == null)
                {
                    var prb = go.AddComponent<Rigidbody>();
                    prb.useGravity = false;
                    prb.linearDamping = 1f;
                    prb.angularDamping = 0.5f;
                    go.AddComponent<AsteroidMotion>();
                }
                return go;
            }
        }
        // fallback: composite from asteroidModels
        if (asteroidModels != null && asteroidModels.Count > 0)
        {
            return BuildCompositeAsteroid(rng);
        }
        // fallback: primitive
        var prim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var rend = prim.GetComponent<Renderer>();
        if (rend != null && asteroidMaterial != null) rend.sharedMaterial = asteroidMaterial;
        if (prim.GetComponent<Collider>() == null) prim.AddComponent<SphereCollider>();
        var prb2 = prim.AddComponent<Rigidbody>();
        prb2.useGravity = false;
        prb2.linearDamping = 1f;
        prb2.angularDamping = 0.5f;
        prim.AddComponent<AsteroidMotion>();
        return prim;
    }

    private GameObject BuildCompositeAsteroid(System.Random rng)
    {
        // Fallback composite (unused when asteroidPrefabs is set)
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var rend = go.GetComponent<Renderer>();
        if (rend != null && asteroidMaterial != null) rend.sharedMaterial = asteroidMaterial;
        if (go.GetComponent<Collider>() == null) go.AddComponent<SphereCollider>();
        var prb = go.AddComponent<Rigidbody>();
        prb.useGravity = false;
        prb.linearDamping = 1f;
        prb.angularDamping = 0.5f;
        go.AddComponent<AsteroidMotion>();
        return go;
    }

    private GameObject BuildNebula(System.Random rng)
    {
        // Volumetric cloud using a particle system for a real 3D cloud look
        var parent = new GameObject("Nebula Cloud");

        var ps = parent.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 20f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 5f);
        Color[] palette = new Color[] {
            new Color(1f, 0.95f, 0.9f),
            new Color(0.9f, 0.95f, 1f),
            new Color(1f, 0.9f, 0.95f),
            new Color(0.95f, 1f, 0.95f),
        };
        Color c = palette[rng.Next(palette.Length)];
        main.startColor = c;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = true;

        // emission
        var em = ps.emission;
        em.rateOverTime = 20f;

        // shape: large sphere volume
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        // noise for organic movement
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;

        // color over lifetime: fade in/out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(c, 0f),
                new GradientColorKey(c, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.2f),
                new GradientAlphaKey(0.6f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = grad;

        // size over lifetime
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

        // renderer material
        var rend = parent.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var tex = (nebulaMaterial != null && nebulaMaterial.HasProperty("_BaseMap")) ? nebulaMaterial.GetTexture("_BaseMap") : null;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var mat = new Material(sh);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.color = Color.white;
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }

        parent.AddComponent<NebulaCloud>();
        return parent;
    }

    private GameObject BuildPlanet(System.Random rng)
    {
        GameObject go;
        if (planetPrefab != null)
        {
            go = (GameObject)Instantiate(planetPrefab);
        }
        else
        {
            string meshPath = "Assets/Procedural Planets and Stars/Assets/Meshes/PPS_Sphere7992.fbx";
            var meshAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
            if (meshAsset != null)
            {
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(meshAsset);
                if (go == null) go = UnityEngine.Object.Instantiate(meshAsset);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            }
        }
        foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
        var sc = go.AddComponent<SphereCollider>();
        sc.radius = 0.5f;
        sc.isTrigger = true;
        go.AddComponent<GasGiant>();
        return go;
    }

    private GameObject GetAsteroid()
    {
        if (pool.Count > 0)
        {
            var pooled = pool.Dequeue();
            return pooled;
        }
        GameObject go;
        if (asteroidPrefab != null)
        {
            go = Instantiate(asteroidPrefab);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.5f;
            }
            if (go.GetComponent<Rigidbody>() == null)
            {
                var prb = go.AddComponent<Rigidbody>();
                prb.useGravity = false;
                prb.linearDamping = 1f;
                prb.angularDamping = 0.5f;
                go.AddComponent<AsteroidMotion>();
            }
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && asteroidMaterial != null) rend.sharedMaterial = asteroidMaterial;
            if (go.GetComponent<Collider>() == null) go.AddComponent<SphereCollider>();
            var prb = go.AddComponent<Rigidbody>();
            prb.useGravity = false;
            prb.linearDamping = 1f;
            prb.angularDamping = 0.5f;
            go.AddComponent<AsteroidMotion>();
        }
        return go;
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