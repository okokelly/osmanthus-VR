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
    private const string RootName = "05 Scene3_2.5D QUICK";

    [MenuItem("Osmanthus/Scene 3/Build Quick 2.5D")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new FileNotFoundException("Scene 3 source scene not found", ScenePath);

        ConfigureTexture(FarTexturePath, false);
        ConfigureTexture(MidTexturePath, true);
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemovePreviousBuild();

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        Material farMaterial = CreateOrUpdateMaterial(
            "M_Scene3_FarCityGarden_Quick",
            AssetDatabase.LoadAssetAtPath<Texture2D>(FarTexturePath),
            false);
        Material midMaterial = CreateOrUpdateMaterial(
            "M_Scene3_MidGarden_Quick",
            AssetDatabase.LoadAssetAtPath<Texture2D>(MidTexturePath),
            true);
        Material lakeBlendMaterial = CreateOrUpdateColorMaterial(
            "M_Scene3_LakeBlend_Quick",
            new Color(0.22f, 0.31f, 0.34f, 1f));

        CreateLakeOverlay(root.transform, lakeBlendMaterial);

        // The terrace looks toward negative X. Unity's built-in Quad faces local -Z,
        // so -90 degrees around Y turns its front toward positive X and the viewer.
        CreateCard(
            "Far City + Garden Backdrop",
            root.transform,
            new Vector3(-78f, 47.7f, 20f),
            new Vector2(240f, 96f),
            farMaterial,
            0);

        CreateCard(
            "Mid Garden Islands Cutout",
            root.transform,
            new Vector3(-50f, 19.6f, 20f),
            new Vector2(70f, 39.4f),
            midMaterial,
            1);

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

    private static void CreateLakeOverlay(Transform parent, Material material)
    {
        GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        overlay.name = "Quick Lake Colour Blend";
        overlay.transform.SetParent(parent, false);
        overlay.transform.position = new Vector3(-35.1f, -0.135f, 29.5f);
        overlay.transform.localScale = new Vector3(33f, 0.018f, 59f);

        Collider collider = overlay.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        MeshRenderer renderer = overlay.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
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
        cameraObject.transform.LookAt(new Vector3(-52f, 5.3f, 20f));

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 72f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 160f;
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
