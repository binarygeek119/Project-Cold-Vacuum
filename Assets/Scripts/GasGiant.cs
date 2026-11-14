using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A dynamic gas-giant planet: procedural banded texture, gentle orbit via rigidbody.
/// Orbit velocity is derived from the actual spawn radius so planets don't spiral in.
/// </summary>
public class GasGiant : MonoBehaviour
{
    [Header("Motion")]
    public Vector3 orbitCenter = Vector3.zero;
    public bool useOrbitCenter = false;
    public float spinSpeed = 5f;
    public float orbitSpeed = 0.05f;

    private Rigidbody _rb;
    private Vector3 _orbitCenter;
    private Vector3 _spinAxis;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        foreach (var c in GetComponents<Collider>()) c.isTrigger = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.isTrigger = true;
        _rb.linearDamping = 0.2f;
        _rb.angularDamping = 0.1f;
        _rb.mass = 10000f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (useOrbitCenter)
            _orbitCenter = orbitCenter;
        else
            _orbitCenter = transform.position;

        _spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(0.5f, 2f);
        orbitSpeed = Random.Range(0.001f, 0.005f);

        Vector3 toCenter = transform.position - _orbitCenter;
        float r = Mathf.Max(toCenter.magnitude, 1f);
        Vector3 tangent = Vector3.Cross(Vector3.up, toCenter).normalized;
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
        _rb.linearVelocity = tangent * (orbitSpeed * r);
        _rb.angularVelocity = _spinAxis * (spinSpeed * Mathf.Deg2Rad);

        if (Random.value < 0.6f)
        {
            AddRings();
        }

        GenerateTexture();
    }

    void FixedUpdate()
    {
        if (_rb == null) return;
        Vector3 toCenter = _orbitCenter - transform.position;
        float dist = toCenter.magnitude;
        if (dist > 0.01f)
        {
            float centripetal = _rb.mass * (orbitSpeed * orbitSpeed * dist);
            centripetal = Mathf.Min(centripetal, _rb.mass);
            _rb.AddForce(toCenter.normalized * centripetal, ForceMode.Force);
        }
    }

    private void AddRings()
    {
#if UNITY_EDITOR
        string ringMeshPath = "Assets/Mwester Studios/Simple Space Starter Kit/Models/Planet Rings.fbx";
        var ringAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ringMeshPath);
        if (ringAsset == null) return;

        var ring = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(ringAsset);
        if (ring == null) ring = UnityEngine.Object.Instantiate(ringAsset);
        ring.name = "Rings";
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        float ringScale = 1f / 64f * 1.8f;
        ring.transform.localScale = new Vector3(ringScale, ringScale, ringScale);

        string[] ringMats = new string[] {
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Rings 0.mat",
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Rings 1.mat",
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Rings 2.mat",
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Rings 3.mat",
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Rings 4.mat",
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Rings 5.mat",
            "Assets/Mwester Studios/Simple Space Starter Kit/Materials/Planet Materials/Dust Rings.mat",
        };
        var ringRend = ring.GetComponentInChildren<Renderer>();
        if (ringRend != null)
        {
            var matPath = ringMats[Random.Range(0, ringMats.Length)];
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null) ringRend.sharedMaterial = mat;
        }
#endif
    }

    private void GenerateTexture()
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;

        int w = 512, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var px = new Color[w * h];
        Color[][] palettes = new Color[][] {
            new Color[] { new Color(0.85f,0.7f,0.5f), new Color(0.7f,0.4f,0.2f), new Color(0.9f,0.85f,0.7f), new Color(0.5f,0.25f,0.15f), new Color(0.95f,0.6f,0.4f) },
            new Color[] { new Color(0.7f,0.85f,0.95f), new Color(0.4f,0.6f,0.8f), new Color(0.9f,0.95f,1f), new Color(0.2f,0.4f,0.6f), new Color(0.6f,0.8f,0.95f) },
            new Color[] { new Color(0.9f,0.75f,0.5f), new Color(0.75f,0.5f,0.3f), new Color(0.95f,0.9f,0.75f), new Color(0.6f,0.4f,0.25f), new Color(0.85f,0.65f,0.45f) },
            new Color[] { new Color(0.8f,0.3f,0.2f), new Color(0.6f,0.15f,0.1f), new Color(0.9f,0.5f,0.3f), new Color(0.4f,0.1f,0.05f), new Color(0.85f,0.4f,0.25f) },
        };
        var pal = palettes[Random.Range(0, palettes.Length)];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float v = (float)y / h;
            float bandNoise = Mathf.PerlinNoise(x * 0.01f, v * 4f);
            int bandIdx = Mathf.Clamp(Mathf.FloorToInt((v + bandNoise * 0.15f) * pal.Length), 0, pal.Length - 1);
            Color bandColor = pal[bandIdx];
            float turb = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            bandColor = Color.Lerp(bandColor, pal[(bandIdx + 1) % pal.Length], turb * 0.4f);
            float dx = (x / (float)w) - 0.3f;
            float dy = (y / (float)h) - 0.4f;
            float spot = Mathf.Exp(-(dx * dx * 20f + dy * dy * 40f));
            bandColor = Color.Lerp(bandColor, new Color(0.6f, 0.2f, 0.15f), spot * 0.6f);
            float streak = Mathf.PerlinNoise(x * 0.02f, v * 12f);
            bandColor = Color.Lerp(bandColor, bandColor * 1.1f, streak * 0.15f);
            px[y * w + x] = bandColor;
        }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var mat = new Material(sh);
        mat.color = Color.white;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.12f, 0.1f, 0.08f));
            if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
        }
        rend.material = mat;
    }
}
