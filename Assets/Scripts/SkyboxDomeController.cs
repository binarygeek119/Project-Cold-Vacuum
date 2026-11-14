using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Dome-based skybox that cross-fades between nebula images when entering a new star system.
/// </summary>
public class SkyboxDomeController : MonoBehaviour
{
    public float fadeDuration = 2f;
    public float domeScale = 500f;

    private Material _mat;
    private Texture2D _texA;
    private int _currentSystem = -1;
    private bool _isFading = false;
    private SpaceStreamer _streamer;

    private string[] _nebulaPaths = new string[] {
        "Assets/DinV/Dynamic Space Background/Sprites/Nebula Blue.png",
        "Assets/DinV/Dynamic Space Background/Sprites/Nebula Aqua-Pink.png",
        "Assets/DinV/Dynamic Space Background/Sprites/Nebula Red.png",
    };

    private Texture2D[] _nebulaTexs;

    void Start()
    {
        _streamer = FindAnyObjectByType<SpaceStreamer>();
        var rend = GetComponent<Renderer>();
        if (rend != null) _mat = rend.material;
        transform.position = Vector3.zero;
        transform.localScale = new Vector3(-domeScale, -domeScale, -domeScale);

#if UNITY_EDITOR
        _nebulaTexs = new Texture2D[_nebulaPaths.Length];
        for (int i = 0; i < _nebulaPaths.Length; i++)
            _nebulaTexs[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(_nebulaPaths[i]);
#endif

        _texA = _nebulaTexs != null && _nebulaTexs.Length > 0 ? _nebulaTexs[0] : null;
        if (_mat != null && _texA != null)
        {
            _mat.SetTexture("_TexA", _texA);
            _mat.SetTexture("_TexB", _texA);
            _mat.SetFloat("_Blend", 0f);
        }

        // black background so dome renders
        if (Camera.main != null) Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = Color.black;
    }

    void Update()
    {
        if (Camera.main != null) transform.position = Camera.main.transform.position;

        if (_streamer == null || _isFading) return;
        int sysIndex = GetNearestSystemIndex();
        if (sysIndex >= 0 && sysIndex != _currentSystem)
        {
            _currentSystem = sysIndex;
            StartCoroutine(FadeToNewTexture());
        }
    }

    private int GetNearestSystemIndex()
    {
        if (_streamer == null) return -1;
        var field = _streamer.GetType().GetField("systems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return -1;
        var systems = field.GetValue(_streamer) as System.Collections.IList;
        if (systems == null || systems.Count == 0) return -1;
        var target = _streamer.target;
        if (target == null) return -1;
        float minDist = float.MaxValue;
        int idx = -1;
        for (int i = 0; i < systems.Count; i++)
        {
            var sys = systems[i];
            var centerField = sys.GetType().GetField("center");
            if (centerField == null) continue;
            Vector3 center = (Vector3)centerField.GetValue(sys);
            float d = Vector3.Distance(target.position, center);
            if (d < minDist) { minDist = d; idx = i; }
        }
        if (idx >= 0)
        {
            var sys = systems[idx];
            var radiusField = sys.GetType().GetField("radius");
            float radius = radiusField != null ? (float)radiusField.GetValue(sys) : 200f;
            if (minDist > radius * 2.5f) return -1;
        }
        return idx;
    }

    private System.Collections.IEnumerator FadeToNewTexture()
    {
        _isFading = true;
        Texture2D newTex = _nebulaTexs != null && _nebulaTexs.Length > 0
            ? _nebulaTexs[Random.Range(0, _nebulaTexs.Length)]
            : _texA;
        if (newTex == null) { _isFading = false; yield break; }

        if (_mat != null)
        {
            _mat.SetTexture("_TexB", newTex);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / fadeDuration;
                _mat.SetFloat("_Blend", Mathf.Clamp01(t));
                yield return null;
            }
            _texA = newTex;
            _mat.SetTexture("_TexA", _texA);
            _mat.SetFloat("_Blend", 0f);
        }
        _isFading = false;
    }
}
