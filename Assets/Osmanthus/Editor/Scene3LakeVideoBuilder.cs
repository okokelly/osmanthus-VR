using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Swaps the painted far vista across the Scene 3 lake for a looping video card, and re-tunes the
// lake surface material that reads badly next to it.
//
// The video sits on a gently curved arc at the distance the painted mid-garden cutout used to
// occupy, with feathered borders so it dissolves into the far city backdrop that is kept behind it
// as peripheral filler. Two coincident cards let LakeVistaVideo crossfade the loop point.
public static class Scene3LakeVideoBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private const string ClipPath = "Assets/Osmanthus/Video/Osmanthus_Video3.mp4";
    private const string PosterPath = "Assets/Osmanthus/Art/Scene3Quick2D/Scene3_Vista_Poster.png";
    private const string ScreenshotPath = "Assets/Osmanthus/Screenshots/Scene3_LakeVista_Video.png";
    private const string RootName = "06 Lake Vista Video";
    private const string ShaderName = "Osmanthus/Scene3VistaVideo";

    // The painted layer the video stands in for. Kept in the scene, just hidden, so
    // "Restore Painted Backdrop" can put it back.
    private const string ReplacedLayerName = "Mid Garden Islands Cutout";

    // Arc placement. Centre matches the rest of the 2.5D backdrop stack; 180 deg faces -X, the
    // direction the lake viewpoint looks.
    private static readonly Vector3 ArcCenter = new Vector3(-9.2f, 0f, 20f);
    private const float ArcRadius = 46f;
    private const float ArcSpanDeg = 62f;
    // Just under the waterline. Sitting it at -3 pushed a strip of the clip's street-level detail up
    // against the horizon, where it compressed into a busy checkered band over the lake; an A/B
    // render with the cards hidden confirmed the band was the card, not the water.
    private const float ArcBottomY = -0.5f;
    private const int ArcSegments = 48;

    // Source crop, as a fraction trimmed off each edge. The generated clip carries a "PixVerse.ai"
    // watermark in the top-right corner, so the right and top edges lose a sliver.
    private const float CropLeft = 0f;
    private const float CropRight = 0.055f;
    private const float CropBottom = 0f;
    private const float CropTop = 0.045f;

    [MenuItem("Osmanthus/Scene 3/Build Lake Vista Video")]
    public static void Build()
    {
        Scene scene = OpenScene();

        VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);
        if (clip == null) throw new FileNotFoundException("Lake vista clip not found", ClipPath);

        Texture2D poster = ImportPoster();
        RemovePrevious();

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = Vector3.zero;

        // Undistorted card height: arc width divided by the aspect of the *cropped* source.
        float arcWidth = 2f * Mathf.PI * ArcRadius * (ArcSpanDeg / 360f);
        float sourceAspect = (clip.width * (1f - CropLeft - CropRight))
                           / (clip.height * (1f - CropBottom - CropTop));
        float arcHeight = arcWidth / sourceAspect;

        Material matA = CreateVistaMaterial("M_Scene3_VistaVideo_A", poster);
        Material matB = CreateVistaMaterial("M_Scene3_VistaVideo_B", poster);
        // B starts invisible: it only shows while the loop dissolve hands the picture over.
        matB.SetFloat("_Alpha", 0f);

        // B sits marginally nearer the viewer and one sorting step later so it always draws over A.
        GameObject cardA = CreateArcCard("Lake Vista Video A", root.transform, ArcRadius,
            ArcBottomY, ArcBottomY + arcHeight, matA, 3);
        GameObject cardB = CreateArcCard("Lake Vista Video B", root.transform, ArcRadius - 0.5f,
            ArcBottomY, ArcBottomY + arcHeight, matB, 4);

        // Keep the haze layers painting over the video rather than under it.
        SetSortingOrder("Waterline Haze", 5);
        SetSortingOrder("Near Shore Mist", 6);

        LakeVistaVideo driver = root.AddComponent<LakeVistaVideo>();
        driver.clip = clip;
        driver.surfaceA = cardA.GetComponent<Renderer>();
        driver.surfaceB = cardB.GetComponent<Renderer>();
        driver.crossfade = 1f;
        driver.textureWidth = 1280;
        driver.textureHeight = Mathf.RoundToInt(1280f / ((float)clip.width / clip.height));
        driver.playAudio = false;

        SetLayerActive(ReplacedLayerName, false);
        RetireLegacyLakeProps(false);
        RetuneLakeSurface();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[Osmanthus] Lake vista video built: arc {0:0.0} x {1:0.0} m at radius {2}, span {3} deg.",
            arcWidth, arcHeight, ArcRadius, ArcSpanDeg));
    }

    [MenuItem("Osmanthus/Scene 3/Restore Painted Backdrop")]
    public static void Restore()
    {
        Scene scene = OpenScene();
        RemovePrevious();
        SetLayerActive(ReplacedLayerName, true);
        SetSortingOrder("Waterline Haze", 4);
        SetSortingOrder("Near Shore Mist", 5);
        RetireLegacyLakeProps(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Painted lake backdrop restored; video vista removed.");
    }

    [MenuItem("Osmanthus/Scene 3/Render Lake Vista Preview")]
    public static void RenderPreview()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) OpenScene();

        GameObject cameraObject = new GameObject("~LakeVistaPreviewCam");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(-9.2f, 1.65f, 20f);
        camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        camera.fieldOfView = 70f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.54f, 0.63f, 0.67f, 1f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 900f;

        const int W = 1600, H = 900;
        RenderTexture target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = target;
        RenderTexture.active = target;
        camera.Render();

        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        texture.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
        File.WriteAllBytes(ScreenshotPath, texture.EncodeToPNG());

        RenderTexture.active = previousActive;
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(cameraObject);
        target.Release();
        Object.DestroyImmediate(target);

        AssetDatabase.ImportAsset(ScreenshotPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Osmanthus] Saved lake vista preview: " + ScreenshotPath);
    }

    // --- helpers ----------------------------------------------------------------------------

    private static Scene OpenScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new FileNotFoundException("Scene 3 source scene not found", ScenePath);
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return scene;
    }

    private static void RemovePrevious()
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.name == RootName) Object.DestroyImmediate(go);
        }
    }

    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.name == name) return go;
        }
        return null;
    }

    private static void SetLayerActive(string name, bool active)
    {
        GameObject go = FindIncludingInactive(name);
        if (go != null) go.SetActive(active);
    }

    private static void SetSortingOrder(string name, int order)
    {
        GameObject go = FindIncludingInactive(name);
        if (go == null) return;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sortingOrder = order;
    }

    private static Texture2D ImportPoster()
    {
        if (!File.Exists(PosterPath)) return null;
        AssetDatabase.ImportAsset(PosterPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(PosterPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Trilinear + aniso for the same reason the runtime RenderTexture needs mips: this card
            // is read at a grazing angle and the clip's window grids moire badly without them.
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(PosterPath);
    }

    private static Material CreateVistaMaterial(string name, Texture2D poster)
    {
        string path = MaterialFolder + name + ".mat";
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new MissingReferenceException("Shader not found: " + ShaderName);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        // Poster frame so the card is legible in the editor; LakeVistaVideo swaps in the live
        // RenderTexture through a property block at runtime.
        material.SetTexture("_BaseMap", poster);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Alpha", 1f);
        material.SetFloat("_Exposure", 1f);
        material.SetFloat("_Desaturate", 0.12f);
        material.SetColor("_HazeColor", new Color(0.80f, 0.76f, 0.68f, 1f));
        material.SetFloat("_HazeAmount", 0.14f);
        material.SetFloat("_CropLeft", CropLeft);
        material.SetFloat("_CropRight", CropRight);
        material.SetFloat("_CropBottom", CropBottom);
        material.SetFloat("_CropTop", CropTop);
        material.SetFloat("_FeatherX", 0.16f);
        material.SetFloat("_FeatherTop", 0.14f);
        // A deeper bottom feather: the band just above the waterline is the most compressed part of
        // the card, so dissolving it into the lake haze costs nothing and hides the worst of it.
        material.SetFloat("_FeatherBottom", 0.22f);
        // Only fully present once the viewer is out at the lake terrace; from the corridor it stays
        // a faint suggestion rather than a billboard over the garden. _MinAlpha = 1 turns this off.
        material.SetVector("_FocusPoint", new Vector4(-9.2f, 1.8f, 20f, 0f));
        material.SetFloat("_FocusNear", 10f);
        material.SetFloat("_FocusFar", 32f);
        material.SetFloat("_MinAlpha", 0.15f);
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateArcCard(string name, Transform parent, float radius,
        float yBottom, float yTop, Material material, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = ArcCenter;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildArcMesh(radius, yBottom, yTop, 180f, ArcSpanDeg, ArcSegments);

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static Mesh BuildArcMesh(float radius, float yBottom, float yTop,
        float angleCenterDeg, float angleSpanDeg, int segments)
    {
        Vector3[] verts = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[(segments + 1) * 2];
        float a0 = (angleCenterDeg - angleSpanDeg * 0.5f) * Mathf.Deg2Rad;
        float a1 = (angleCenterDeg + angleSpanDeg * 0.5f) * Mathf.Deg2Rad;
        for (int i = 0; i <= segments; i++)
        {
            float f = i / (float)segments;
            float a = Mathf.Lerp(a0, a1, f);
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            verts[i * 2] = p + Vector3.up * yBottom;
            verts[i * 2 + 1] = p + Vector3.up * yTop;
            // Angle increases counter-clockwise while the viewer faces -X, so flip U to keep the
            // clip the right way round rather than mirrored.
            uv[i * 2] = new Vector2(1f - f, 0f);
            uv[i * 2 + 1] = new Vector2(1f - f, 1f);
        }
        int[] tris = new int[segments * 6];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
            tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
            tris[ti++] = c; tris[ti++] = b; tris[ti++] = d;
        }
        Mesh m = new Mesh { name = "Scene3VistaArc" };
        m.vertices = verts;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Legacy lake dressing from the original blockout builder. The 2.5D backdrop replaced all of it,
    // and once the water plane drops to deck level these poke back through it as grey lumps and a
    // black brick on the horizon.
    private static readonly string[] LegacyLakeProps =
    {
        "Distant Island Left", "Distant Island Right", "Far Pavilion Base", "Far Pavilion Roof"
    };

    private static void RetireLegacyLakeProps(bool active)
    {
        foreach (string name in LegacyLakeProps) SetLayerActive(name, active);
    }

    // Re-tunes the lake surface for the rewritten shader.
    //
    // Two things were wrong. The ripple was two aligned sines at a fixed world scale, which reads as
    // grey corduroy rather than water; and the horizon tone was chosen against a backdrop the
    // mid-garden cutout used to cover, so with that layer gone the far edge of the plane sat as a
    // bright band (measured 0.76) against a dark shore (measured 0.32). Far water is now matched to
    // the backdrop, and the detail is pushed into the near field, which is where the perspective
    // puts almost all of the visible surface.
    //
    // The plane deliberately runs back under the viewpoint. Pulling it out to expose the stone
    // terrace was tried and is worse: the deck is untextured and the warm key light turns it to mud,
    // and it leaves a hole either side of the terrace where nothing is drawn.
    private static void RetuneLakeSurface()
    {
        GameObject plane = FindIncludingInactive("Quick Lake Surface");
        if (plane != null)
        {
            plane.transform.position = new Vector3(-30f, 0.28f, 29f);
            plane.transform.localScale = new Vector3(7f, 1f, 9.5f);
        }

        Material lake = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "M_Scene3_LakeQuick.mat");
        if (lake == null) return;

        lake.SetFloat("_GradFront", -6f);
        lake.SetFloat("_GradBack", -52f);
        lake.SetColor("_NearColor", new Color(0.21f, 0.27f, 0.30f, 1f));
        lake.SetColor("_FarColor", new Color(0.33f, 0.36f, 0.35f, 1f));
        lake.SetColor("_HorizonColor", new Color(0.40f, 0.40f, 0.37f, 1f));
        lake.SetColor("_ReflectColor", new Color(0.80f, 0.66f, 0.48f, 1f));
        lake.SetFloat("_ReflectStrength", 0.20f);
        lake.SetFloat("_ReflectWidth", 9f);
        lake.SetColor("_RippleColor", new Color(0.84f, 0.89f, 0.95f, 1f));
        lake.SetFloat("_RippleStrength", 0.06f);
        lake.SetFloat("_RippleScale", 1.3f);    // ~5 m swell, so it varies across the near field
        lake.SetFloat("_RippleSpeed", 0.45f);
        lake.SetFloat("_RippleFalloff", 2.4f);
        lake.SetFloat("_DetailStrength", 0.05f);
        lake.SetFloat("_DetailScale", 3.5f);    // ~1.8 m chop right in front of the viewer
        lake.SetFloat("_GlitterStrength", 0.22f);
        lake.SetFloat("_GlitterScale", 1.4f);
        lake.SetColor("_HazeColor", new Color(0.33f, 0.34f, 0.32f, 0.9f));
        lake.SetFloat("_HazeStart", 0.62f);
        EditorUtility.SetDirty(lake);
    }
}
