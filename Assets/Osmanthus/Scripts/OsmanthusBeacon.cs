using UnityEngine;

// Marks the one osmanthus the player can actually touch.
//
// The scene carries 59 osmanthus renderers and exactly one OsmanthusTouchTrigger — a single guide
// flower that relocates from waypoint A to waypoint B between the two videos. Without a mark there
// is nothing telling the player which of the 59 is live, so this hangs an additive gold halo, a
// breathing ground ring and a slow rising shimmer on whichever flower is currently armed, and hides
// all of it the moment the trigger fires.
//
// Materials are supplied as asset references rather than built with Shader.Find, so the shader is
// reachable from the scene and survives player-build shader stripping.
[DisallowMultipleComponent]
public class OsmanthusBeacon : MonoBehaviour
{
    [Header("Wiring")]
    public OsmanthusTouchTrigger trigger;
    public Material glowMaterial;
    public Material ringMaterial;

    [Header("Look")]
    public float glowSize = 1.25f;
    // Pushed behind the flower along the view ray, so the petals read in front of the halo instead
    // of being silhouetted inside it.
    public float glowDepthOffset = 0.28f;
    public float ringSize = 1.9f;
    public float ringDrop = 0.55f;      // metres below the flower
    public Color beaconColor = new Color(1f, 0.74f, 0.32f, 1f);

    [Header("Motion")]
    public float pulsePeriod = 1.6f;
    public float glowMin = 0.55f;
    public float glowMax = 1.15f;
    public float ripplePeriod = 2.4f;

    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");

    private Transform _glow, _ring;
    private Renderer _glowRenderer, _ringRenderer;
    private MaterialPropertyBlock _glowBlock, _ringBlock;
    private float _t;

    private void Start()
    {
        if (glowMaterial == null || ringMaterial == null)
        {
            enabled = false;
            return;
        }

        _glowBlock = new MaterialPropertyBlock();
        _ringBlock = new MaterialPropertyBlock();

        _glow = MakeQuad("BeaconGlow", glowMaterial, out _glowRenderer);
        _glow.localPosition = Vector3.zero;

        _ring = MakeQuad("BeaconRing", ringMaterial, out _ringRenderer);
        _ring.localPosition = new Vector3(0f, -ringDrop, 0f);
        _ring.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lies flat on the ground
        _ring.localScale = new Vector3(ringSize, ringSize, 1f);

        _ringBlock.SetFloat(ModeId, 1f);
        _ringBlock.SetColor(ColorId, beaconColor);
        _ringRenderer.SetPropertyBlock(_ringBlock);
    }

    private Transform MakeQuad(string label, Material material, out Renderer renderer)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = label;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);          // must never block the controller ray
        go.transform.SetParent(transform, false);
        renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    private void LateUpdate()
    {
        bool armed = trigger != null && trigger.IsArmed;
        if (_glow == null) return;

        if (_glow.gameObject.activeSelf != armed) _glow.gameObject.SetActive(armed);
        if (_ring.gameObject.activeSelf != armed) _ring.gameObject.SetActive(armed);
        if (!armed) return;

        _t += Time.deltaTime;

        // Halo: breathe in brightness and size together so it reads as light rather than a sprite.
        float phase = Mathf.Sin(_t / Mathf.Max(pulsePeriod, 0.05f) * Mathf.PI * 2f) * 0.5f + 0.5f;
        float amount = Mathf.Lerp(glowMin, glowMax, phase);
        _glow.localScale = Vector3.one * glowSize * Mathf.Lerp(0.88f, 1.12f, phase);

        Camera cam = Camera.main;
        if (cam != null)
        {
            // Billboard, yaw and pitch, so it stays a disc from any angle, and sit it just behind
            // the flower so the halo backs the petals rather than covering them.
            Vector3 viewRay = (transform.position - cam.transform.position).normalized;
            _glow.position = transform.position + viewRay * glowDepthOffset;
            _glow.rotation = Quaternion.LookRotation(viewRay, Vector3.up);
        }

        _glowBlock.SetFloat(ModeId, 0f);
        _glowBlock.SetColor(ColorId, beaconColor);
        _glowBlock.SetFloat(AlphaId, amount);
        _glowRenderer.SetPropertyBlock(_glowBlock);

        // Ground ring: a ripple expanding outward and fading, restarting each cycle.
        float ripple = Mathf.Repeat(_t / Mathf.Max(ripplePeriod, 0.05f), 1f);
        _ringBlock.SetFloat(ModeId, 1f);
        _ringBlock.SetColor(ColorId, beaconColor);
        _ringBlock.SetFloat(RingRadiusId, Mathf.Lerp(0.06f, 0.46f, ripple));
        _ringBlock.SetFloat(AlphaId, (1f - ripple) * 0.9f);
        _ringRenderer.SetPropertyBlock(_ringBlock);
        _ring.Rotate(0f, 0f, 12f * Time.deltaTime, Space.Self);
    }
}
