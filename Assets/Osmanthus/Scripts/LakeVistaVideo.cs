using UnityEngine;
using UnityEngine.Video;

// Plays one clip as the far vista across the lake, looping without a visible cut.
//
// The clip runs on two coincident cards. While card A plays out its tail, card B restarts from
// frame 0 underneath/over it and the alpha dissolves between them; then the roles swap. Only one
// player is decoding except during the crossfade, so this stays cheap on Quest.
//
// Card B is drawn on top of card A (higher render queue on its material), so the dissolve is
// always driven by B's alpha: fade B in to hand over to B, fade B out to hand back to A.
public class LakeVistaVideo : MonoBehaviour
{
    [Header("Source")]
    public VideoClip clip;
    public Renderer surfaceA;   // drawn first
    public Renderer surfaceB;   // drawn over A, its alpha is the dissolve

    [Header("Loop")]
    [Tooltip("Seconds of dissolve at the loop point. Clamped to a third of the clip length.")]
    public float crossfade = 1f;

    [Header("Playback")]
    public int textureWidth = 1280;
    public int textureHeight = 736;
    public bool playAudio = false;
    [Range(0f, 1f)] public float audioVolume = 0.3f;

    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private VideoPlayer _playerA, _playerB;
    private RenderTexture _rtA, _rtB;
    private MaterialPropertyBlock _blockA, _blockB;
    private bool _bIsFront;      // true while B holds the picture
    private bool _handoverArmed; // the incoming player for this loop has already been started
    private float _fade;         // current alpha of card B

    private void Start()
    {
        if (clip == null || surfaceA == null || surfaceB == null)
        {
            enabled = false;
            return;
        }

        _blockA = new MaterialPropertyBlock();
        _blockB = new MaterialPropertyBlock();

        _rtA = NewTarget("LakeVistaA");
        _rtB = NewTarget("LakeVistaB");
        _playerA = NewPlayer(_rtA);
        _playerB = NewPlayer(_rtB);

        SetCard(surfaceA, _blockA, _rtA, 1f);
        SetCard(surfaceB, _blockB, _rtB, 0f);

        _playerA.Play();
    }

    private void OnDestroy()
    {
        ReleaseTarget(ref _rtA);
        ReleaseTarget(ref _rtB);
    }

    private RenderTexture NewTarget(string label)
    {
        // sRGB so the clip lands correctly in this project's linear colour space.
        RenderTexture rt = new RenderTexture(textureWidth, textureHeight, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = label };
        // Mips matter here. The card is ~29 m tall and read at a grazing angle from the terrace, so
        // the lower part of the picture is squeezed into a few pixels -- and the clip is full of
        // glass towers, whose window grids alias into a moire checkerboard without them.
        rt.useMipMap = true;
        rt.autoGenerateMips = true;
        rt.filterMode = FilterMode.Trilinear;
        rt.anisoLevel = 8;
        rt.Create();
        return rt;
    }

    private static void ReleaseTarget(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        Destroy(rt);
        rt = null;
    }

    private VideoPlayer NewPlayer(RenderTexture target)
    {
        VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;          // the ping-pong is the loop
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = target;
        player.clip = clip;
        if (playAudio)
        {
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.SetDirectAudioVolume(0, audioVolume);
        }
        else
        {
            player.audioOutputMode = VideoAudioOutputMode.None;
        }
        player.Prepare();
        return player;
    }

    private static void SetCard(Renderer target, MaterialPropertyBlock block, Texture texture, float alpha)
    {
        target.GetPropertyBlock(block);
        block.SetTexture(BaseMapId, texture);
        block.SetFloat(AlphaId, alpha);
        target.SetPropertyBlock(block);
    }

    private void Update()
    {
        VideoPlayer front = _bIsFront ? _playerB : _playerA;
        VideoPlayer back = _bIsFront ? _playerA : _playerB;

        double length = front.frameCount > 0 && front.frameRate > 0
            ? front.frameCount / front.frameRate
            : clip.length;
        float fade = Mathf.Clamp(crossfade, 0.05f, (float)length / 3f);
        double remaining = length - front.time;

        // Start the incoming player early enough that it is decoding by the time the dissolve runs.
        if (!_handoverArmed && front.isPlaying && remaining <= fade)
        {
            back.time = 0d;
            back.Play();
            _handoverArmed = true;
        }

        if (_handoverArmed)
        {
            // Drive B's alpha toward whichever side is taking over.
            float target = _bIsFront ? 0f : 1f;
            _fade = Mathf.MoveTowards(_fade, target, Time.deltaTime / fade);
            _blockB.SetFloat(AlphaId, _fade);
            _blockB.SetTexture(BaseMapId, _rtB);
            surfaceB.SetPropertyBlock(_blockB);

            if (Mathf.Approximately(_fade, target))
            {
                front.Stop();
                _bIsFront = !_bIsFront;
                _handoverArmed = false;
            }
        }
    }
}
