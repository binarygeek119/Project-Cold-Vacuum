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
    private Vector3 _currentCenter = new Vector3(float.MaxValue, 0f, 0f);
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
        if (Camera.main != null) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = Color.black; }
    }

    void Update()
    {
        if (Camera.main != null) transform.position = Camera.main.transform.position;

        if (_streamer == null || _isFading) return;
        if (TryGetNearestSystem(out Vector3 sysCenter, out float radius))
        {
            if (Vector3.Distance(sysCenter, _currentCenter) > 1f)
            {
                _currentCenter = sysCenter;
                StartCoroutine(FadeToNewTexture());
            }
        }
    }

    private bool TryGetNearestSystem(out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;
        if (_streamer == null) return false;
        var target = _streamer.target;
        if (target == null) return false;
        if (!_streamer.TryGetNearestSystem(target.position, out center, out radius)) return false;
        float minDist = Vector3.Distance(target.position, center);
        if (minDist > radius * 1.2f) return false;
        return true;
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
