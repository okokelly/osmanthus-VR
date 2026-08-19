using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CompleteCorridorLakeBuilder
{
    private const string BaseScenePath = "Assets/Osmanthus/Scenes/02_Gallery.unity";
    private const string TargetScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string CorridorAssetPath = "Assets/Art/Models/LongCorridor/LongCorridor_Pavilion_Stylized.fbx";
    private const string CornerAssetPath = "Assets/Art/Models/LongCorridor/LongCorridor_PavilionCorner_Stylized.fbx";
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private const string ScreenshotPath = "Assets/Osmanthus/Screenshots/CompleteCorridorLake_Overview.png";

    private static Material stone;
    private static Material stoneLight;
    private static Material grout;
    private static Material lake;
    private static Material lakeGlow;
    private static Material island;
    private static Material gold;

    [MenuItem("Osmanthus/Build Complete Corridor + Lake")]
    public static void Build()
    {
        AssetDatabase.ImportAsset(CorridorAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(CornerAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BaseScenePath) == null)
            throw new FileNotFoundException("Base scene not found", BaseScenePath);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
            AssetDatabase.DeleteAsset(TargetScenePath);

        if (!AssetDatabase.CopyAsset(BaseScenePath, TargetScenePath))
            throw new IOException("Could not duplicate base scene to " + TargetScenePath);

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        Component teleportTemplate = FindComponentByTypeName("TeleportationArea");
        LoadMaterials();

        GameObject root = new GameObject("COMPLETE CORRIDOR + LAKE");
        SceneManager.MoveGameObjectToScene(root, scene);

        Transform architecture = Group("01 Architecture - Two Straight Sets and Pavilion Turn", root.transform);
        Transform paving = Group("02 Stone Paving", root.transform);
        Transform waterfront = Group("03 Waterfront and Lake", root.transform);
        Transform atmosphere = Group("04 Atmosphere and Memory Trail", root.transform);

        BuildCorridorSets(scene, architecture);
        BuildStoneWalkway(paving, teleportTemplate);
        BuildLakefront(waterfront, teleportTemplate);
        BuildAtmosphere(atmosphere);
        ConfigurePlayerAndLighting();
        CreatePreviewCamera(root.transform);

        // The duplicated base scene supplies XR/EventSystem/lighting configuration.
        // Its previous greybox is removed only after the teleport component has been copied.
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            if (sceneRoot == root) continue;
            if (sceneRoot.name == "--- GREYBOX ---" || sceneRoot.name.StartsWith("__NEW_NARRATIVE__"))
                Object.DestroyImmediate(sceneRoot);
        }

        MarkStaticRecursively(root);
        ConfigureRenderSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TargetScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RenderPreview();
        Debug.Log("[Osmanthus] Built complete corridor scene: " + TargetScenePath);
    }

    public static void RenderPreview()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != TargetScenePath)
            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        GameObject cameraObject = GameObject.Find("Complete Scene Preview Camera");
        if (cameraObject == null)
        {
            Debug.LogWarning("[Osmanthus] Preview camera was not found.");
            return;
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));

        RenderTexture target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        target.antiAliasing = 2;
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
        Debug.Log("[Osmanthus] Saved overview screenshot: " + ScreenshotPath);
    }

    private static void LoadMaterials()
    {
        Material reference = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "M_Greybox_Wall.mat");
        Shader shader = reference != null ? reference.shader : Shader.Find("Universal Render Pipeline/Lit");

        stone = MaterialAsset("M_Complete_Stone", shader, new Color(0.39f, 0.405f, 0.39f, 1f), 0.18f, 0f, false);
        stoneLight = MaterialAsset("M_Complete_StoneLight", shader, new Color(0.51f, 0.50f, 0.46f, 1f), 0.14f, 0f, false);
        grout = MaterialAsset("M_Complete_Grout", shader, new Color(0.115f, 0.14f, 0.145f, 1f), 0.05f, 0f, false);
        lake = MaterialAsset("M_Complete_Lake", shader, new Color(0.055f, 0.23f, 0.29f, 1f), 0.88f, 0.08f, false);
        lakeGlow = MaterialAsset("M_Complete_LakeGlow", shader, new Color(0.12f, 0.43f, 0.49f, 0.24f), 0.95f, 0.04f, true);
        island = MaterialAsset("M_Complete_Island", shader, new Color(0.075f, 0.16f, 0.14f, 1f), 0.06f, 0f, false);
        gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "M_Accent_Gold.mat");
        if (gold == null)
            gold = MaterialAsset("M_Accent_Gold", shader, new Color(1f, 0.54f, 0.08f, 1f), 0.3f, 0.05f, false);
    }

    private static Material MaterialAsset(string name, Shader shader, Color color, float smoothness, float metallic, bool transparent)
    {
        string path = MaterialFolder + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1f : 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

        if (transparent)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildCorridorSets(Scene scene, Transform parent)
    {
        GameObject corridorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CorridorAssetPath);
        GameObject cornerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CornerAssetPath);
        if (corridorAsset == null)
            throw new FileNotFoundException("Corridor FBX has not imported", CorridorAssetPath);
        if (cornerAsset == null)
            throw new FileNotFoundException("Corner pavilion FBX has not imported", CornerAssetPath);

        float[] centres = { -20.0f, 0f };
        for (int i = 0; i < centres.Length; i++)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(corridorAsset, scene) as GameObject;
            if (instance == null) instance = Object.Instantiate(corridorAsset);
            instance.name = "Corridor Set " + (i + 1).ToString("00") + " - Octagonal Pavilion";
            instance.transform.SetParent(parent, false);
            Quaternion importedAxisCorrection = instance.transform.localRotation;
            Vector3 importedScale = instance.transform.localScale;
            instance.transform.localPosition = new Vector3(0f, 0f, centres[i]);
            // Preserve the FBX root's Blender-Z-up to Unity-Y-up correction, then turn
            // the corridor's longitudinal X axis onto the scene's Z walking axis.
            instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * importedAxisCorrection;
            instance.transform.localScale = importedScale;
        }

        GameObject corner = PrefabUtility.InstantiatePrefab(cornerAsset, scene) as GameObject;
        if (corner == null) corner = Object.Instantiate(cornerAsset);
        corner.name = "Corridor Set 03 - Pavilion Turn to Lake";
        corner.transform.SetParent(parent, false);
        Quaternion cornerAxisCorrection = corner.transform.localRotation;
        Vector3 cornerImportedScale = corner.transform.localScale;
        corner.transform.localPosition = new Vector3(0f, 0f, 20f);
        // The incoming -X arm now lands on the previous set's +Z end; the other arm turns to the lake.
        corner.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * cornerAxisCorrection;
        corner.transform.localScale = cornerImportedScale;
    }

    private static void BuildStoneWalkway(Transform parent, Component teleportTemplate)
    {
        GameObject baseWalk = Prim("Walkable Stone Foundation", PrimitiveType.Cube,
            new Vector3(0f, -0.105f, -5.5f), new Vector3(3.45f, 0.20f, 51.0f), stone, parent, true);
        CopyComponent(teleportTemplate, baseWalk);

        GameObject turnWalk = Prim("Walkable Stone Foundation - Lake Turn", PrimitiveType.Cube,
            new Vector3(-5.5f, -0.105f, 20f), new Vector3(11.0f, 0.20f, 3.45f), stone, parent, true);
        CopyComponent(teleportTemplate, turnWalk);

        // Low geometry grout pattern follows the straight route into the pavilion.
        for (int lane = -1; lane <= 1; lane++)
        {
            float x = lane * 0.86f;
            Prim("Long Grout Seam " + lane, PrimitiveType.Cube,
                new Vector3(x, 0.007f, -5.5f), new Vector3(0.025f, 0.016f, 50.7f), grout, parent, false);
        }

        for (int i = 0; i <= 40; i++)
        {
            float z = -30f + i * 1.25f;
            Prim("Transverse Stone Joint " + i.ToString("00"), PrimitiveType.Cube,
                new Vector3(0f, 0.008f, z), new Vector3(3.25f, 0.017f, 0.025f), grout, parent, false);
        }

        // The same paving language turns 90 degrees inside the last pavilion.
        for (int lane = -1; lane <= 1; lane++)
        {
            float z = 20f + lane * 0.86f;
            Prim("Turn Grout Seam " + lane, PrimitiveType.Cube,
                new Vector3(-5.5f, 0.007f, z), new Vector3(10.7f, 0.016f, 0.025f), grout, parent, false);
        }

        for (int i = 1; i <= 8; i++)
        {
            float x = -i * 1.25f;
            Prim("Turn Stone Joint " + i.ToString("00"), PrimitiveType.Cube,
                new Vector3(x, 0.008f, 20f), new Vector3(0.025f, 0.017f, 3.25f), grout, parent, false);
        }

        Prim("Entrance Threshold", PrimitiveType.Cube,
            new Vector3(0f, 0.035f, -31.0f), new Vector3(4.7f, 0.13f, 1.1f), stoneLight, parent, true);
        Prim("Lake Threshold", PrimitiveType.Cube,
            new Vector3(-10.8f, 0.04f, 20f), new Vector3(1.0f, 0.14f, 5.2f), stoneLight, parent, true);
    }

    private static void BuildLakefront(Transform parent, Component teleportTemplate)
    {
        GameObject terrace = Prim("Lake Viewing Terrace", PrimitiveType.Cube,
            new Vector3(-14.3f, -0.07f, 20f), new Vector3(8.6f, 0.24f, 8.0f), stoneLight, parent, true);
        CopyComponent(teleportTemplate, terrace);

        GameObject jetty = Prim("Stone Jetty", PrimitiveType.Cube,
            new Vector3(-21.1f, -0.055f, 20f), new Vector3(5.0f, 0.21f, 2.6f), stone, parent, true);
        CopyComponent(teleportTemplate, jetty);

        Prim("Shoreline Stone Lip", PrimitiveType.Cube,
            new Vector3(-18.6f, 0.11f, 29.5f), new Vector3(0.36f, 0.32f, 59f), stone, parent, true);
        Prim("Lake Water", PrimitiveType.Cube,
            new Vector3(-35.1f, -0.22f, 29.5f), new Vector3(33f, 0.08f, 59f), lake, parent, false);
        Prim("Lake Memory Reflection", PrimitiveType.Cube,
            new Vector3(-30f, -0.155f, 28f), new Vector3(20f, 0.012f, 38f), lakeGlow, parent, false);

        // Distant garden silhouettes keep the lake from reading as an empty plane.
        Prim("Distant Island Left", PrimitiveType.Sphere,
            new Vector3(-36f, -0.4f, 7f), new Vector3(8.5f, 1.3f, 4.4f), island, parent, false);
        Prim("Distant Island Right", PrimitiveType.Sphere,
            new Vector3(-42f, -0.48f, 43f), new Vector3(7.0f, 1.1f, 3.6f), island, parent, false);
        Prim("Far Pavilion Base", PrimitiveType.Cube,
            new Vector3(-36f, 0.48f, 7f), new Vector3(3.2f, 0.9f, 2.2f), island, parent, false);
        Prim("Far Pavilion Roof", PrimitiveType.Cylinder,
            new Vector3(-36f, 1.35f, 7f), new Vector3(2.2f, 0.28f, 2.2f), gold, parent, false);
    }

    private static void BuildAtmosphere(Transform parent)
    {
        // A restrained osmanthus trail guides the eye from the entrance to the water.
        for (int i = 0; i < 22; i++)
        {
            float z = -29f + i * 2.35f;
            float x = Mathf.Sin(i * 0.67f) * 0.58f;
            float y = 0.22f + (i % 4) * 0.055f;
            Prim("Osmanthus Guide " + (i + 1).ToString("00"), PrimitiveType.Sphere,
                new Vector3(x, y, z), new Vector3(0.095f, 0.035f, 0.095f), gold, parent, false);
        }

        for (int i = 0; i < 7; i++)
        {
            float x = -1.1f - i * 2.15f;
            float z = 20f + Mathf.Sin(i * 0.8f) * 0.45f;
            Prim("Osmanthus Turn Guide " + (i + 1).ToString("00"), PrimitiveType.Sphere,
                new Vector3(x, 0.25f + (i % 3) * 0.05f, z), new Vector3(0.095f, 0.035f, 0.095f), gold, parent, false);
        }

        // Fog and the distant lake silhouettes provide the soft outer boundary.
    }

    private static void ConfigurePlayerAndLighting()
    {
        GameObject origin = GameObject.Find("XR Origin (Quest 3)");
        if (origin != null)
        {
            origin.transform.position = new Vector3(0f, 0f, -30.0f);
            origin.transform.rotation = Quaternion.identity;
        }

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        bool foundDirectional = false;
        foreach (Light lightComponent in lights)
        {
            if (lightComponent.type != LightType.Directional) continue;
            foundDirectional = true;
            lightComponent.color = new Color(1f, 0.72f, 0.43f);
            lightComponent.intensity = 1.05f;
            lightComponent.shadows = LightShadows.None;
            lightComponent.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        }

        if (!foundDirectional)
        {
            GameObject sunObject = new GameObject("Memory Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.43f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.None;
            sunObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        }
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.23f, 0.24f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.16f, 0.23f, 0.25f);
        RenderSettings.fogDensity = 0.009f;
    }

    private static void CreatePreviewCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Complete Scene Preview Camera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = new Vector3(-29f, 14f, -35f);
        cameraObject.transform.LookAt(new Vector3(-6f, 1.1f, 9f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 48f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 180f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.17f, 0.19f);
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.enabled = false;
    }

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Prim(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (!keepCollider)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }
        return go;
    }

    private static Component FindComponentByTypeName(string typeName)
    {
        Component[] components = Object.FindObjectsByType<Component>(FindObjectsInactive.Include);
        foreach (Component component in components)
            if (component != null && component.GetType().Name == typeName) return component;
        return null;
    }

    private static void CopyComponent(Component source, GameObject target)
    {
        if (source == null) return;
        Component destination = target.AddComponent(source.GetType());
        EditorUtility.CopySerialized(source, destination);
    }

    private static void MarkStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.GetComponent<Camera>() != null) continue;
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
        }
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene entry in scenes)
            if (entry.path == TargetScenePath) return;
        scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
