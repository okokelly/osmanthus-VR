using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

// A world-space "canvas" that unfurls in front of the viewer, dims the surroundings, and plays a
// video (or a text placeholder when no clip is set), then folds away. Builds its own visuals so it
// needs no hand-authored prefab. Call PlayInFront(...) to trigger; onClosed fires when it finishes.
public class VideoScreen : MonoBehaviour
{
    [Header("Layout (metres)")]
    public float distance = 3.2f;   // in front of the head
    public float width = 4.4f;      // cinema-sized
    public float height = 2.5f;

    [Header("Timing")]
    public float unfoldTime = 1.1f;
    public float dimAlpha = 1.0f;   // fully black surroundings
    public float placeholderSeconds = 8f;

    public System.Action onClosed;

    // Overlay queue: dim first, then the panel, then the placeholder label over both.
    private const int DimQueue = 4000;
    private const int PanelQueue = 4001;
    private const int LabelQueue = 4002;

    private Transform _panel;
    private Renderer _dimRenderer;
    private Material _panelMat, _dimMat;
    private TextMeshPro _label;
    private VideoPlayer _player;
    private RenderTexture _rt;
    private bool _busy;
    private string _pendingLabel = "VIDEO";

    private void Awake()
    {
        BuildVisuals();
        gameObject.SetActive(false);
    }

    private void BuildVisuals()
    {
        // Dim overlay: large dark quad that sits just behind the panel and fades in.
        GameObject dim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dim.name = "DimOverlay";
        StripCollider(dim);
        dim.transform.SetParent(transform, false);
        dim.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        dim.transform.localScale = new Vector3(200f, 120f, 1f);
        _dimMat = MakeUnlit(new Color(0.02f, 0.02f, 0.03f, 0f), DimQueue);
        _dimRenderer = dim.GetComponent<Renderer>();
        _dimRenderer.sharedMaterial = _dimMat;

        // Panel: the video / placeholder surface that unfurls open.
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Panel";
        StripCollider(panel);
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = Vector3.zero;
        panel.transform.localScale = new Vector3(width, height, 1f);
        _panel = panel.transform;
        _panelMat = MakeUnlit(new Color(0.42f, 0.42f, 0.46f, 1f), PanelQueue);
        panel.GetComponent<Renderer>().sharedMaterial = _panelMat;

        // Optional placeholder label via TMP with a runtime OS font. Guarded so a missing font never
        // throws in Awake (an Awake exception would disable this component and stop the unfold).
        TMP_FontAsset fa = SafeRuntimeFont();
        if (fa != null)
        {
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            _label = labelGO.AddComponent<TextMeshPro>();
            _label.font = fa;
            _label.text = "VIDEO";
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableAutoSizing = false;
            _label.fontSize = 4f;
            _label.color = new Color(1f, 0.94f, 0.78f, 1f);
            _label.rectTransform.sizeDelta = new Vector2(width, height);
            // Match the panel: over the world, and above the panel it sits on.
            Material lm = _label.fontMaterial;
            if (lm != null)
            {
                if (lm.HasProperty("_ZTestMode"))
                    lm.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                lm.renderQueue = LabelQueue;
            }
        }
    }

    private static TMP_FontAsset _runtimeFont;
    private static bool _fontTried;
    private static TMP_FontAsset SafeRuntimeFont()
    {
        if (_fontTried) return _runtimeFont;
        _fontTried = true;
        try
        {
            if (TMP_Settings.defaultFontAsset != null) { _runtimeFont = TMP_Settings.defaultFontAsset; return _runtimeFont; }
            Font os = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica Neue", "Helvetica" }, 72);
            if (os != null) _runtimeFont = TMP_FontAsset.CreateFontAsset(os);
        }
        catch { _runtimeFont = null; }
        return _runtimeFont;
    }

    // Positions the screen in front of the given viewer (yaw only) and starts the sequence.
    public void PlayInFront(Transform viewer, VideoClip clip, string label)
    {
        if (_busy) return;
        gameObject.SetActive(true);

        // Head-lock the screen to the viewer so it's always a big screen straight ahead, regardless
        // of eye height or where the player is looking (a cinema screen on a black surround).
        if (viewer != null)
        {
            transform.SetParent(viewer, false);
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
        }

        _pendingLabel = string.IsNullOrEmpty(label) ? "VIDEO" : label;
        if (_label != null) _label.text = _pendingLabel;

        if (clip != null) SetupVideo(clip);
        else ShowPlaceholder();

        StartCoroutine(Run(clip));
    }

    private void ShowPlaceholder()
    {
        if (_player != null) _player.Stop();
        // Distinct tint per placeholder so the two videos read differently even without text.
        Color tint = _pendingLabel != null && _pendingLabel.Contains("2")
            ? new Color(0.34f, 0.48f, 0.72f, 1f)   // cool = video 2
            : new Color(0.72f, 0.52f, 0.30f, 1f);  // warm = video 1
        _panelMat.SetColor("_BaseColor", tint);
        if (_panelMat.HasProperty("_UseTexture")) _panelMat.SetFloat("_UseTexture", 0f);
        if (_panelMat.HasProperty("_BaseMap")) _panelMat.SetTexture("_BaseMap", null);
        if (_label != null) _label.gameObject.SetActive(true);
    }

    private void SetupVideo(VideoClip clip)
    {
        if (_label != null) _label.gameObject.SetActive(false);
        if (_rt == null)
        {
            _rt = new RenderTexture(1280, 720, 0);
            _rt.Create();
        }
        if (_player == null)
        {
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.isLooping = false;
        }
        _player.targetTexture = _rt;
        _player.clip = clip;
        _panelMat.SetColor("_BaseColor", Color.white);
        if (_panelMat.HasProperty("_BaseMap")) _panelMat.SetTexture("_BaseMap", _rt);
        if (_panelMat.HasProperty("_UseTexture")) _panelMat.SetFloat("_UseTexture", 1f);
    }

    private IEnumerator Run(VideoClip clip)
    {
        _busy = true;

        // Unfold: panel height 0 -> full, dim 0 -> dimAlpha.
        float t = 0f;
        while (t < unfoldTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / unfoldTime);
            _panel.localScale = new Vector3(width, Mathf.Max(0.001f, height * k), 1f);
            SetDim(dimAlpha * k);
            yield return null;
        }
        _panel.localScale = new Vector3(width, height, 1f);
        SetDim(dimAlpha);

        // Play the clip (or hold the placeholder for a fixed time).
        if (clip != null && _player != null)
        {
            bool done = false;
            void OnEnd(VideoPlayer vp) { done = true; }
            _player.loopPointReached += OnEnd;
            _player.Play();
            while (!done) yield return null;
            _player.loopPointReached -= OnEnd;
        }
        else
        {
            yield return new WaitForSeconds(placeholderSeconds);
        }

        // Fold away.
        t = 0f;
        while (t < unfoldTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / unfoldTime);
            _panel.localScale = new Vector3(width, Mathf.Max(0.001f, height * k), 1f);
            SetDim(dimAlpha * k);
            yield return null;
        }

        gameObject.SetActive(false);
        _busy = false;
        onClosed?.Invoke();
    }

    private void SetDim(float a)
    {
        Color c = _dimMat.GetColor("_BaseColor");
        c.a = a;
        _dimMat.SetColor("_BaseColor", c);
    }

    private static void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    // The takeover surfaces ignore depth entirely (see S_VideoScreenOverlay). A cinema-sized panel
    // cannot fit inside the corridor -- the clear span between the pillars is about 2 m and the
    // lattice tops are at y 2.28 -- so depth-testing it just means pillars and roof slice into the
    // picture. `queue` orders the dim behind the panel.
    private static Material MakeUnlit(Color color, int queue)
    {
        Shader sh = Shader.Find("Osmanthus/VideoScreenOverlay");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_UseTexture")) m.SetFloat("_UseTexture", 0f);

        // Fallback path only: the overlay shader already blends, ignores depth and sits in Overlay.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_ZTest")) m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = queue;
        return m;
    }
}
