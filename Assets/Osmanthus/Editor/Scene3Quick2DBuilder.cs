using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Scene3Quick2DBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string ArtFolder = "Assets/Osmanthus/Art/Scene3Quick2D/";
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private const string ScreenshotPath = "Assets/Osmanthus/Screenshots/Scene3_Quick2_5D_v1.png";

    private const string FarTexturePath = ArtFolder + "Scene3_FarCityGarden_v1.png";
    private const string MidTexturePath = ArtFolder + "Scene3_MidGarden_Source_v1.png";
    private const string FogTexturePath = ArtFolder + "T_Scene3_SoftFog.png";
    private const string RootName = "05 Scene3_2.5D QUICK";
    private const string LakeShaderName = "Osmanthus/Scene3LakeQuick";

    // Lake surface X extents (used by both the plane placement and the shader gradient).
    private const float LakeFrontX = -6f;   // near the viewpoint
    private const float LakeBackX = -52f;   // at the far horizon / islands

    [MenuItem("Osmanthus/Scene 3/Build Quick 2.5D")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new FileNotFoundException("Scene 3 source scene not found", ScenePath);

        ConfigureTexture(FarTexturePath, false);
        ConfigureTexture(MidTexturePath, true);
        Texture2D fogTexture = BakeSoftFog(FogTexturePath);
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemovePreviousBuild();

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        Material farMaterial = CreateOrUpdateMaterial(
            "M_Scene3_FarCityGarden_Quick",
            AssetDatabase.LoadAssetAtPath<Texture2D>(FarTexturePath),
            false);
        // Lift + warm the far city so it reads as a sunlit modern-city / garden hybrid that blends
        // with the islands, rather than a dark faded band that feels disconnected.
        if (farMaterial.HasProperty("_BaseColor")) farMaterial.SetColor("_BaseColor", new Color(1.38f, 1.26f, 1.08f, 1f));
        Material midMaterial = CreateOrUpdateMaterial(
            "M_Scene3_MidGarden_Quick",
            AssetDatabase.LoadAssetAtPath<Texture2D>(MidTexturePath),
            true);
        Material lakeMaterial = CreateLakeMaterial();

        // Fog / haze tints (rgb + alpha).
        // Thinner and warmer fog so the city shows through and the layers blend instead of greying out.
        Material depthFogFar = CreateFogMaterial("M_Scene3_DepthFogFar_Quick", fogTexture, new Color(0.80f, 0.74f, 0.64f, 0.16f));
        Material depthFogNear = CreateFogMaterial("M_Scene3_DepthFogNear_Quick", fogTexture, new Color(0.82f, 0.76f, 0.65f, 0.11f));
        Material waterlineHaze = CreateFogMaterial("M_Scene3_WaterlineHaze_Quick", fogTexture, new Color(0.76f, 0.75f, 0.70f, 0.55f));
        Material nearShoreMist = CreateFogMaterial("M_Scene3_NearShoreMist_Quick", fogTexture, new Color(0.67f, 0.71f, 0.74f, 0.42f));

        // --- Lake surface (replaces the old flat colour slab; also covers the brown foreground) ---
        CreateLakePlane(root.transform, lakeMaterial);

        // Curved (cylindrical-arc) backdrop wrapped around the lake viewpoint so it fills the FOV
        // instead of reading as a flat screen. Centre = the Scene 3 viewpoint XZ; 180 deg faces -X.
        // Bottoms sit near the waterline (y ~ 0) so the horizon lands at eye level (no looking up).
        Vector3 arcCenter = new Vector3(-9.2f, 0f, 20f);

        // Far city: far enough for depth (radius 180) but present enough to read as a hybrid skyline.
        CreateArcCard("Far City + Garden Backdrop", root.transform, arcCenter, 180f, -8f, 150f, 180f, 110f, 64, farMaterial, 0);

        // Atmospheric depth fog bands between far and mid (thin, so they blend rather than separate).
        CreateArcCard("Depth Fog - Far Band", root.transform, arcCenter, 130f, -4f, 105f, 180f, 106f, 52, depthFogFar, 1);
        CreateArcCard("Depth Fog - Near Band", root.transform, arcCenter, 90f, -2f, 74f, 180f, 98f, 44, depthFogNear, 2);

        // Mid garden islands, kept near their original distance so they stay the same apparent size.
        CreateArcCard("Mid Garden Islands Cutout", root.transform, arcCenter, 46f, -2f, 42f, 180f, 88f, 44, midMaterial, 3);

        // --- Waterline haze: dissolves the hard bottom edge of the mid cutout into the lake ---
        CreateCard("Waterline Haze", root.transform, new Vector3(-46f, 1.8f, 20f), new Vector2(160f, 14f), waterlineHaze, 4);

        // --- Near-shore mist: soft veil over the near lake / foreground transition ---
        CreateCard("Near Shore Mist", root.transform, new Vector3(-24f, 1.6f, 20f), new Vector2(72f, 7f), nearShoreMist, 5);

        // NOTE: the scattered osmanthus placeholders in the foreground are intentional
        // (on-concept, and the anchor for the touch-to-video interaction) and are left visible.

        GameObject viewpoint = new GameObject("Scene3_Viewpoint_Quick");
        viewpoint.transform.SetParent(root.transform, false);
        viewpoint.transform.position = new Vector3(-9.2f, 1.65f, 20f);
        viewpoint.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

        CreatePreviewCamera(root.transform, viewpoint.transform.position);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RenderPreview();
        Debug.Log("[Osmanthus] Scene 3 quick 2.5D built in " + ScenePath);
    }

    [MenuItem("Osmanthus/Scene 3/Render Quick 2.5D Preview")]
    public static void RenderPreview()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject cameraObject = GameObject.Find("Scene3 Quick Preview Camera");
        if (cameraObject == null)
        {
            Debug.LogWarning("[Osmanthus] Scene 3 quick preview camera was not found.");
            return;
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));

        RenderTexture target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2
        };
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        bool previousEnabled = camera.enabled;

        camera.enabled = true;
        camera.targetTexture = target;
        RenderTexture.active = target;
        camera.Render();

        Texture2D texture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        texture.Apply();
        File.WriteAllBytes(ScreenshotPath, texture.EncodeToPNG());

        camera.targetTexture = previousTarget;
        camera.enabled = previousEnabled;
        RenderTexture.active = previousActive;
        Object.DestroyImmediate(texture);
        target.Release();
        Object.DestroyImmediate(target);

        AssetDatabase.ImportAsset(ScreenshotPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Osmanthus] Saved Scene 3 quick preview: " + ScreenshotPath);
    }

    private static void ConfigureTexture(string path, bool alpha)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = alpha;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    // Bakes a soft, edge-faded alpha band texture used for all fog / haze cards.
    private static Texture2D BakeSoftFog(string assetPath)
    {
        const int W = 128;
        const int H = 128;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false) { name = "T_Scene3_SoftFog" };
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)(H - 1);
            // Vertical soft band: 0 at top/bottom, peak in the middle.
            float band = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.3f);
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1);
                float edge = Mathf.SmoothStep(0f, 0.14f, u) * Mathf.SmoothStep(0f, 0.14f, 1f - u);
                float a = Mathf.Clamp01(band * edge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        string fullPath = Application.dataPath + assetPath.Substring("Assets".Length);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static Material CreateOrUpdateMaterial(string name, Texture2D texture, bool transparent)
    {
        string path = MaterialFolder + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);

        if (transparent)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // Transparent fog / haze material: reuses the URP Unlit transparent setup and tints it.
    private static Material CreateFogMaterial(string name, Texture2D texture, Color tint)
    {
        Material material = CreateOrUpdateMaterial(name, texture, true);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateLakeMaterial()
    {
        string path = MaterialFolder + "M_Scene3_LakeQuick.mat";
        Shader shader = Shader.Find(LakeShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[Osmanthus] Lake shader not found, using flat colour fallback.");
            return CreateOrUpdateColorMaterial("M_Scene3_LakeBlend_Quick", new Color(0.18f, 0.28f, 0.31f, 1f));
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "M_Scene3_LakeQuick" };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_GradFront", LakeFrontX);
        material.SetFloat("_GradBack", LakeBackX);
        // Palette tuned to sit with the painterly backdrop water (light, low-contrast),
        // with only a subtle animated shimmer and a faint warm reflection band.
        material.SetColor("_NearColor", new Color(0.30f, 0.37f, 0.39f, 1f));
        material.SetColor("_FarColor", new Color(0.52f, 0.57f, 0.57f, 1f));
        material.SetColor("_HorizonColor", new Color(0.80f, 0.72f, 0.58f, 1f));
        material.SetColor("_ReflectColor", new Color(0.85f, 0.62f, 0.42f, 1f));
        material.SetFloat("_ReflectStrength", 0.12f);
        material.SetColor("_RippleColor", new Color(0.86f, 0.90f, 0.95f, 1f));
        material.SetFloat("_RippleStrength", 0.025f);
        material.SetFloat("_RippleScale", 2.2f);
        material.SetFloat("_RippleSpeed", 0.4f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateColorMaterial(string name, Color color)
    {
        string path = MaterialFolder + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Flat lake surface plane. Extends from the horizon up to under the viewpoint so it
    // reads as the whole lake and covers the brown stone foreground.
    private static void CreateLakePlane(Transform parent, Material material)
    {
        GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
        lake.name = "Quick Lake Surface";
        lake.transform.SetParent(parent, false);
        // Plane primitive is 10x10 units at scale 1, facing +Y.
        lake.transform.position = new Vector3(-30f, 0.28f, 29f); // sits just above the shore stone so it reads as one water sheet
        lake.transform.localScale = new Vector3(7f, 1f, 9.5f); // ~70 x 95 units, X in [-65,5], Z in [-18,76]

        Collider collider = lake.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        MeshRenderer renderer = lake.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // Builds a curved billboard: a vertical cylindrical arc centred on `center`, facing inward.
    // The backdrop materials use double-sided culling, so inward visibility needs no winding care.
    private static GameObject CreateArcCard(string name, Transform parent, Vector3 center, float radius,
        float yBottom, float yTop, float angleCenterDeg, float angleSpanDeg, int segments, Material material, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildArcMesh(radius, yBottom, yTop, angleCenterDeg, angleSpanDeg, Mathf.Max(2, segments));

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static Mesh BuildArcMesh(float radius, float yBottom, float yTop, float angleCenterDeg, float angleSpanDeg, int segments)
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
            uv[i * 2] = new Vector2(f, 0f);
            uv[i * 2 + 1] = new Vector2(f, 1f);
        }
        int[] tris = new int[segments * 6];
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
            tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
            tris[ti++] = c; tris[ti++] = b; tris[ti++] = d;
        }
        Mesh m = new Mesh { name = "Scene3Arc" };
        m.vertices = verts;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static GameObject CreateCard(
        string name,
        Transform parent,
        Vector3 position,
        Vector2 size,
        Material material,
        int sortingOrder)
    {
        GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = name;
        card.transform.SetParent(parent, false);
        card.transform.position = position;
        card.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        card.transform.localScale = new Vector3(size.x, size.y, 1f);

        Collider collider = card.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        MeshRenderer renderer = card.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
        return card;
    }

    private static void CreatePreviewCamera(Transform parent, Vector3 viewpoint)
    {
        GameObject cameraObject = new GameObject("Scene3 Quick Preview Camera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = viewpoint;
        cameraObject.transform.LookAt(new Vector3(-52f, 2.2f, 20f)); // near-level gaze so the horizon sits at eye height

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 72f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 420f; // reach the far city arc (~220 units out)
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.54f, 0.63f, 0.67f);
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.enabled = false;
    }

    private static void RemovePreviousBuild()
    {
        GameObject previous = GameObject.Find(RootName);
        if (previous != null) Object.DestroyImmediate(previous);
    }
}
