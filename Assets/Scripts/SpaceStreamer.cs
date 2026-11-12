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

    [Header("Composite asteroids")]
    public System.Collections.Generic.List<GameObject> asteroidModels;
    public int minPieces = 2;
    public int maxPieces = 4;
    public float compositeRadius = 0.8f;

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
    }

    private void UnloadChunk(Vector3Int coord)
    {
        if (!chunks.TryGetValue(coord, out var chunk)) return;
        foreach (var go in chunk.instances)
        {
            if (go != null)
            {
                go.SetActive(false);
                pool.Enqueue(go);
            }
        }
        chunk.instances.Clear();
        chunks.Remove(coord);
    }

    private GameObject BuildAsteroid(System.Random rng)
    {
        // If no models provided, fall back to single primitive/prefab
        if (asteroidModels == null || asteroidModels.Count == 0)
        {
            return GetAsteroid();
        }

        var parent = new GameObject("Composite Asteroid");
        int pieces = Mathf.Clamp(rng.Next(minPieces, maxPieces + 1), 1, 10);

        for (int p = 0; p < pieces; p++)
        {
            var model = asteroidModels[rng.Next(asteroidModels.Count)];
            if (model == null) continue;
            var child = (GameObject)Instantiate(model);
            child.transform.SetParent(parent.transform, false);
            // random offset within composite radius
            Vector3 off = new Vector3(
                ((float)rng.NextDouble() * 2f - 1f) * compositeRadius,
                ((float)rng.NextDouble() * 2f - 1f) * compositeRadius * 0.6f,
                ((float)rng.NextDouble() * 2f - 1f) * compositeRadius);
            child.transform.localPosition = off;
            child.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);
            float sc = 0.5f + (float)rng.NextDouble() * 1.0f;
            child.transform.localScale = Vector3.one * sc;

            // ensure collider
            if (child.GetComponentInChildren<Collider>() == null)
            {
                var scol = child.AddComponent<SphereCollider>();
                scol.radius = 0.5f;
            }
        }

        // add a compound sphere collider on parent for physics (approx)
        var parentCol = parent.AddComponent<SphereCollider>();
        parentCol.radius = compositeRadius * 1.2f;

        return parent;
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
            // ensure at least one collider for physics (add sphere if missing)
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.5f;
            }
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && asteroidMaterial != null) rend.sharedMaterial = asteroidMaterial;
            // keep a sphere collider for physics
            if (go.GetComponent<Collider>() == null) go.AddComponent<SphereCollider>();
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
