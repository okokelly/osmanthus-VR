using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds a floating golden-osmanthus prefab from the Meshy model and replaces the orange
// placeholder markers scattered along the corridor with size/brightness-varied instances.
public static class OsmanthusBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string ModelPath = "Assets/Osmanthus/Art/Osmanthus/Osmanthus_Golden.fbx";
    private const string AlbedoPath = "Assets/Osmanthus/Art/Osmanthus/Osmanthus_Golden_Albedo.png";
    private const string NormalPath = "Assets/Osmanthus/Art/Osmanthus/Osmanthus_Golden_Normal.png";
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private const string PrefabPath = "Assets/Osmanthus/Prefabs/Osmanthus_Golden.prefab";
    private const string TrailRootName = "05 Osmanthus Trail";

    private const float TargetSize = 0.15f; // largest dimension of the flower, in metres
    private const float BloomTiltDeg = 50f;  // tilt so the bloom faces up-and-out instead of edge-on
    private const int RandomSeed = 12345;

    [MenuItem("Osmanthus/Setup/Build Osmanthus + Replace Placeholders")]
    public static void BuildAndReplace()
    {
        ConfigureNormalMap();
        Material[] variants = CreateMaterials();
        GameObject prefab = BuildPrefab(variants[1]);
        if (prefab == null) return;
        ReplacePlaceholders(prefab, variants);
    }

    private static void ConfigureNormalMap()
    {
        TextureImporter imp = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }
    }

    // Returns 3 brightness variants [dim, mid, bright] so a field of osmanthus reads richer.
    private static Material[] CreateMaterials()
    {
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        float[] baseB = { 0.70f, 1.0f, 1.35f };
        float[] emisB = { 0.20f, 0.55f, 1.1f };
        string[] names = { "M_Osmanthus_Golden_Dim", "M_Osmanthus_Golden_Mid", "M_Osmanthus_Golden_Bright" };
        Material[] result = new Material[3];

        Directory.CreateDirectory(MaterialFolder);
        for (int i = 0; i < 3; i++)
        {
            string path = MaterialFolder + names[i] + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit) { name = names[i] };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != lit)
            {
                m.shader = lit;
            }

            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(baseB[i], baseB[i] * 0.97f, baseB[i] * 0.88f, 1f));
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // double-sided so thin petals never show dark gaps

            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", new Color(1f, 0.74f, 0.34f) * emisB[i]);

            EditorUtility.SetDirty(m);
            result[i] = m;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static GameObject BuildPrefab(Material material)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("[Osmanthus] Model not found at " + ModelPath);
            return null;
        }

        GameObject src = PrefabUtility.InstantiatePrefab(model) as GameObject;
        src.transform.position = Vector3.zero;
        src.transform.rotation = Quaternion.identity;
        src.transform.localScale = Vector3.one;

        // Use mesh bounds (Renderer.bounds is stale right after editor instantiation and reads zero).
        MeshFilter[] filters = src.GetComponentsInChildren<MeshFilter>();
        Bounds b = new Bounds();
        bool init = false;
        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 world = mf.transform.TransformPoint(corner);
                if (!init) { b = new Bounds(world, Vector3.zero); init = true; }
                else b.Encapsulate(world);
            }
        }
        if (!init)
        {
            Debug.LogError("[Osmanthus] Model has no mesh to measure.");
            Object.DestroyImmediate(src);
            return null;
        }
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? TargetSize / maxDim : 1f;

        // root (animated) -> Tilt (constant bloom tilt) -> model (centred). Keeping the tilt on a
        // child means the placer can set the root's random yaw without wiping the upright tilt.
        GameObject root = new GameObject("Osmanthus_Golden");
        GameObject tilt = new GameObject("Tilt");
        tilt.transform.SetParent(root.transform, false);
        tilt.transform.localRotation = Quaternion.Euler(BloomTiltDeg, 0f, 0f);

        src.transform.SetParent(tilt.transform, false);
        src.transform.localScale = Vector3.one * scale;
        src.transform.localPosition = -b.center * scale; // centre the geometry on the pivot
        src.transform.localRotation = Quaternion.identity;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = material;

        root.AddComponent<FloatingOsmanthus>();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[Osmanthus] Built prefab. Raw model bounds size = " + b.size.ToString("0.00") +
                  " (max " + maxDim.ToString("0.00") + " -> " + TargetSize + "m) at " + PrefabPath);
        return prefab;
    }

    private static void ReplacePlaceholders(GameObject prefab, Material[] variants)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject oldRoot = GameObject.Find(TrailRootName);
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);

        GameObject trailRoot = new GameObject(TrailRootName);
        SceneManager.MoveGameObjectToScene(trailRoot, scene);

        Random.InitState(RandomSeed);
        int count = 0;
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            string n = t.gameObject.name;
            if (!n.StartsWith("Osmanthus") || !n.Contains("Guide")) continue;

            Vector3 p = t.position;
            // Hide the orange placeholder marker (kept, not destroyed, so this stays reversible).
            t.gameObject.SetActive(false);

            GameObject inst = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            inst.name = "Osmanthus_Flower " + count;
            inst.transform.SetParent(trailRoot.transform, true);
            float height = Random.Range(0.5f, 1.4f);
            inst.transform.position = new Vector3(p.x, p.y + height, p.z);
            // Random facing + a little extra tilt variety on top of the prefab's upright bloom tilt.
            inst.transform.rotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f));
            float s = Random.Range(0.6f, 1.3f);
            inst.transform.localScale = Vector3.one * s;

            Material variant = variants[Random.Range(0, variants.Length)];
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = variant;

            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Replaced " + count + " placeholder(s) with floating osmanthus under '" + TrailRootName + "'.");
    }
}
