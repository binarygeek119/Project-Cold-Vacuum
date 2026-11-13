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
        // fly-throughable: colliders are triggers (no solid collision)
        foreach (var c in GetComponents<Collider>()) c.isTrigger = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.isTrigger = true;
        _rb.linearDamping = 0.2f;
        _rb.angularDamping = 0.1f;
        _rb.mass = 10000f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _orbitCenter = transform.position;
        _spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(0.5f, 2f);
        orbitSpeed = Random.Range(0.005f, 0.02f);
        orbitRadius = Random.Range(80f, 200f);

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
            centripetal = Mathf.Min(centripetal, 200f);
            _rb.AddForce(toCenter.normalized * centripetal, ForceMode.Force);
        }
    }

    private void GenerateTexture()
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;

        // Try to use a PPS gas gradient texture
        string[] gradients = new string[] {
            "Assets/Procedural Planets and Stars/Demo/GeneratedGradients/Gas/PPS_gradient_gasBronze.png",
            "Assets/Procedural Planets and Stars/Demo/GeneratedGradients/Gas/PPS_gradient_gasDrip.png",
            "Assets/Procedural Planets and Stars/Demo/GeneratedGradients/Gas/PPS_gradient_gasHaven.png",
            "Assets/Procedural Planets and Stars/Demo/GeneratedGradients/Gas/PPS_gradient_gasVile.png",
            "Assets/Procedural Planets and Stars/Demo/GeneratedGradients/Gas/PPS_gradient_gasWish.png",
            "Assets/Procedural Planets and Stars/Demo/GeneratedGradients/Gas/PPS_gradient_orangeFire.png",
        };
        string gradPath = gradients[Random.Range(0, gradients.Length)];
        var gradTex = Resources.Load<Texture2D>(gradPath); // won't work, use AssetDatabase
        // Actually load via UnityEditor (editor-only, fine for this project)
        gradTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(gradPath);

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var mat = new Material(sh);
        mat.color = Color.white;
        if (gradTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", gradTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", gradTex);
        }
        else
        {
            // fallback: procedural banded texture
            GenerateProceduralTexture(mat);
        }
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.15f, 0.12f, 0.1f));
            if (gradTex != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", gradTex);
        }
        rend.material = mat;
    }

    private void GenerateProceduralTexture(Material mat)
    {
        int w = 512, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var px = new Color[w * h];
        Color[][] palettes = new Color[][] {
            new Color[] { new Color(0.85f,0.7f,0.5f), new Color(0.7f,0.4f,0.2f), new Color(0.9f,0.85f,0.7f), new Color(0.5f,0.25f,0.15f), new Color(0.95f,0.6f,0.4f) },
            new Color[] { new Color(0.7f,0.85f,0.95f), new Color(0.4f,0.6f,0.8f), new Color(0.9f,0.95f,1f), new Color(0.2f,0.4f,0.6f), new Color(0.6f,0.8f,0.95f) },
            new Color[] { new Color(0.9f,0.75f,0.5f), new Color(0.75f,0.5f,0.3f), new Color(0.95f,0.9f,0.75f), new Color(0.6f,0.4f,0.25f), new Color(0.85f,0.65f,0.45f) },
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
            px[y * w + x] = bandColor;
        }
        tex.SetPixels(px);
        tex.Apply();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
    }

}
