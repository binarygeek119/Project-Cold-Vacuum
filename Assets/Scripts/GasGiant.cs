using UnityEngine;

/// <summary>
/// A dynamic gas-giant planet: procedural banded texture, slow spin, gentle orbit.
/// Spawned into the endless space stream like asteroids.
/// </summary>
public class GasGiant : MonoBehaviour
{
    [Header("Motion")]
    public float spinSpeed = 5f;
    public float orbitSpeed = 0.05f;
    public float orbitRadius = 20f;

    private Rigidbody _rb;
    private Vector3 _orbitCenter;
    private Vector3 _spinAxis;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 0.2f;
        _rb.angularDamping = 0.1f;
        _rb.mass = 1000f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _orbitCenter = transform.position;
        _spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(2f, 8f);
        orbitSpeed = Random.Range(0.02f, 0.08f);
        orbitRadius = Random.Range(10f, 30f);

        _rb.angularVelocity = _spinAxis * (spinSpeed * Mathf.Deg2Rad);
        // initial tangential velocity
        Vector3 toCenter = transform.position - _orbitCenter;
        if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.right;
        Vector3 tangent = Vector3.Cross(Vector3.up, toCenter).normalized;
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
        _rb.linearVelocity = tangent * (orbitSpeed * orbitRadius);

        // generate procedural gas-giant texture
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
            centripetal = Mathf.Min(centripetal, 50f);
            _rb.AddForce(toCenter.normalized * centripetal, ForceMode.Force);
        }
    }

    private void GenerateTexture()
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;

        int w = 512, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var px = new Color[w * h];

        // pick a palette
        Color[][] palettes = new Color[][] {
            new Color[] { new Color(0.85f,0.7f,0.5f), new Color(0.7f,0.4f,0.2f), new Color(0.9f,0.85f,0.7f), new Color(0.5f,0.25f,0.15f), new Color(0.95f,0.6f,0.4f) }, // Jupiter
            new Color[] { new Color(0.7f,0.85f,0.95f), new Color(0.4f,0.6f,0.8f), new Color(0.9f,0.95f,1f), new Color(0.2f,0.4f,0.6f), new Color(0.6f,0.8f,0.95f) }, // Neptune
            new Color[] { new Color(0.9f,0.75f,0.5f), new Color(0.75f,0.5f,0.3f), new Color(0.95f,0.9f,0.75f), new Color(0.6f,0.4f,0.25f), new Color(0.85f,0.65f,0.45f) }, // Saturn
        };
        var pal = palettes[Random.Range(0, palettes.Length)];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float v = (float)y / h;
            // band index
            float bandNoise = Mathf.PerlinNoise(x * 0.01f, v * 4f);
            int bandIdx = Mathf.FloorToInt((v + bandNoise * 0.15f) * pal.Length);
            bandIdx = Mathf.Clamp(bandIdx, 0, pal.Length - 1);
            Color bandColor = pal[bandIdx];
            // add turbulence
            float turb = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            bandColor = Color.Lerp(bandColor, pal[(bandIdx + 1) % pal.Length], turb * 0.4f);
            // great spot / storm: darker oval
            float dx = (x / (float)w) - 0.3f;
            float dy = (y / (float)h) - 0.4f;
            float spot = Mathf.Exp(-(dx * dx * 20f + dy * dy * 40f));
            bandColor = Color.Lerp(bandColor, new Color(0.6f, 0.2f, 0.15f), spot * 0.6f);
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
        // subtle emission so dark side isn't pitch black
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.15f, 0.12f, 0.1f));
            if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
        }
        rend.material = mat;
    }
}
