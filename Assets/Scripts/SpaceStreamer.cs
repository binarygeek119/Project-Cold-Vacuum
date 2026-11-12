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
    public int asteroidsPerChunk = 5;
    public int maxSpawnsPerFrame = 4;
    public int maxDestroysPerFrame = 4;

    [Header("Content")]
    public GameObject asteroidPrefab;
    public Material asteroidMaterial;

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

            // skip if too close to chunk center? no, fine

            var go = GetAsteroid();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);
            float s = 1f + (float)rng.NextDouble() * 2f;
            go.transform.localScale = new Vector3(s * 0.8f, s * 1.1f, s * 0.9f);
            go.SetActive(true);
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
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && asteroidMaterial != null) rend.sharedMaterial = asteroidMaterial;
            // remove collider to keep physics light
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
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
