using UnityEngine;

/// <summary>
/// Spawns lots of LOD stars scattered around the ship at random distances.
/// Far: glowing billboard + twinkle. Near: full star model.
/// The field follows the ship so it's always populated.
/// </summary>
public class StarField : MonoBehaviour
{
    public int starCount = 150;
    public float minDistance = 200f;
    public float maxDistance = 1500f;
    public float lodDistance = 400f;
    public GameObject starNearPrefab; // full star model (optional)

    private Transform _target;
    private GameObject[] _stars;
    private Vector3 _lastCenter;

    void Start()
    {
        _target = Camera.main != null ? Camera.main.transform : null;
        if (_target == null)
        {
            var ship = GameObject.Find("Player Ship");
            if (ship != null) _target = ship.transform;
        }
        SpawnStars();
    }

    void Update()
    {
        if (_target == null)
        {
            var ship = GameObject.Find("Player Ship");
            if (ship != null) _target = ship.transform;
            if (_target == null && Camera.main != null) _target = Camera.main.transform;
            if (_target == null) return;
        }

        // Reposition field if ship moved far
        if (Vector3.Distance(_target.position, _lastCenter) > 100f)
        {
            RepositionStars();
            _lastCenter = _target.position;
        }
    }

    private void SpawnStars()
    {
        _stars = new GameObject[starCount];
        _lastCenter = _target != null ? _target.position : Vector3.zero;

        for (int i = 0; i < starCount; i++)
        {
            _stars[i] = CreateStar(_lastCenter);
        }
    }

    private void RepositionStars()
    {
        if (_stars == null || _target == null) return;
        for (int i = 0; i < _stars.Length; i++)
        {
            if (_stars[i] == null) continue;
            // Move star to a new random position around the new center
            Vector3 dir = Random.onUnitSphere;
            float dist = Random.Range(minDistance, maxDistance);
            _stars[i].transform.position = _target.position + dir * dist;
        }
    }

    private GameObject CreateStar(Vector3 center)
    {
        Vector3 dir = Random.onUnitSphere;
        float dist = Random.Range(minDistance, maxDistance);
        Vector3 pos = center + dir * dist;

        var parent = new GameObject("Star");
        parent.transform.position = pos;

        // Far representation: glowing billboard
        var farGO = new GameObject("Star Far");
        farGO.transform.SetParent(parent.transform, false);

        // Billboard quad with glow
        var billboard = GameObject.CreatePrimitive(PrimitiveType.Quad);
        billboard.name = "Star Glow";
        billboard.transform.SetParent(farGO.transform, false);
        billboard.transform.localScale = Vector3.one * Random.Range(2f, 6f);
        var bcol = billboard.GetComponent<Collider>();
        if (bcol != null) Object.Destroy(bcol);
        var brend = billboard.GetComponent<Renderer>();
        if (brend != null)
        {
            var sh = Shader.Find("Custom/CloudParticle");
            if (sh == null) { Debug.LogError("StarField: CloudParticle shader not found"); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) { Debug.LogError("StarField: no shader found"); sh = Shader.Find("Standard"); }
            var mat = new Material(sh);
            Color starColor = RandomStarColor();
            mat.color = starColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", starColor);
            var tex = Resources.Load<Texture2D>("ParticleSoftCircle");
            // try asset path
            if (tex == null) tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ParticleSoftCircle.png");
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            brend.material = mat;
            brend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        billboard.AddComponent<BillboardToCamera>();

        // (Twinkle particle removed - was rendering as pink box)

        // Point light
        var light = farGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.95f, 0.8f);
        light.intensity = 2f;
        light.range = 50f;

        // Near representation: full star (only if prefab provided)
        GameObject nearGO = null;
        if (starNearPrefab != null)
        {
            nearGO = (GameObject)Instantiate(starNearPrefab);
            nearGO.name = "Star Near";
            nearGO.transform.SetParent(parent.transform, false);
            nearGO.transform.localScale = Vector3.one * Random.Range(8f, 15f);
        }
        else
        {
            // fallback: emissive sphere
            nearGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nearGO.name = "Star Near";
            nearGO.transform.SetParent(parent.transform, false);
            nearGO.transform.localScale = Vector3.one * Random.Range(8f, 15f);
            var rend = nearGO.GetComponent<Renderer>();
            if (rend != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                var mat = new Material(sh);
                Color sc = RandomStarColor();
                mat.color = sc;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sc);
                if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", sc * 2f); }
                rend.material = mat;
            }
            var col = nearGO.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        // LOD component
        var lod = parent.AddComponent<StarLOD>();
        lod.farRepresentation = farGO;
        lod.nearRepresentation = nearGO;
        lod.lodDistance = lodDistance;

        return parent;
    }

    private Color RandomStarColor()
    {
        float r = Random.value;
        if (r < 0.5f) return new Color(1f, 0.95f, 0.8f);       // white/yellow
        if (r < 0.7f) return new Color(0.8f, 0.9f, 1f);         // blue-white
        if (r < 0.85f) return new Color(1f, 0.6f, 0.3f);        // orange
        return new Color(1f, 0.4f, 0.3f);                       // red
    }
}