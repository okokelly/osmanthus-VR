using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Playtest fixups for Scene 04, from a headset session:
//   1. the video screen was still being sliced by corridor pillars ON DEVICE only
//   2. no way to tell which of the 59 osmanthus is the one you can touch
//   3. a long brown strip standing out of the lake
//   4. falling through the floor by the turn pavilion
public static class Scene4FixupBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private const string PatchRoot = "08 Floor Patch";

    private static readonly string[] RuntimeShaders =
    {
        "Osmanthus/VideoScreenOverlay",
        "Osmanthus/OsmanthusBeacon",
    };

    // Legacy dressing from the original blockout that the shaped lake left stranded above water.
    private static readonly string[] StrandedLakeProps = { "Shoreline Stone Lip" };

    // Everything the player is meant to be able to stand on.
    private static bool IsWalkSurface(string n)
    {
        return n.Contains("FloorInset") || n == "Left_Floor" || n == "Right_Floor"
            || n == "Pavilion_StonePlinth" || n.StartsWith("Walkable")
            || n == "Lake Viewing Terrace" || n == "Stone Jetty"
            || n == "Lake Threshold" || n == "Entrance Threshold";
    }

    [MenuItem("Osmanthus/Scene 4/Apply Playtest Fixups")]
    public static void Apply()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureShadersAlwaysIncluded();
        int stranded = HideStrandedProps();
        int patches = BuildFloorPatch(scene);
        bool beacon = WireBeacon();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[Osmanthus] Fixups applied — shaders pinned: {0}, stranded props hidden: {1}, floor patches: {2}, beacon wired: {3}",
            RuntimeShaders.Length, stranded, patches, beacon));
    }

    // --- 1. keep runtime-only shaders in the player build ------------------------------------
    //
    // VideoScreen builds its materials at runtime with Shader.Find. In the editor that always
    // resolves, but a player build strips any shader no material asset references, so on device it
    // fell back to URP/Unlit and lost ZTest Always — which is exactly why the pillars came back.
    private static void EnsureShadersAlwaysIncluded()
    {
        GraphicsSettings settings = AssetDatabase
            .LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        if (settings == null)
        {
            Debug.LogWarning("[Osmanthus] Could not open GraphicsSettings.asset; add the shaders to " +
                             "Always Included Shaders by hand.");
            return;
        }

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null) return;

        foreach (string name in RuntimeShaders)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) { Debug.LogWarning("[Osmanthus] Shader not found: " + name); continue; }

            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
            if (present) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    // --- 3. hide props the reshaped lake left standing in open water --------------------------
    private static int HideStrandedProps()
    {
        int n = 0;
        foreach (string name in StrandedLakeProps)
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go.name == name && go.activeSelf) { go.SetActive(false); n++; }
        return n;
    }

    // --- 4. close the holes in the standing surface -------------------------------------------
    //
    // The pavilion plinths reach x +/-2.29 but SafetyFloor_Corridor is only +/-2.0, and at the turn
    // SafetyFloor_Turn stops at x 0 while the corridor floor stops at z 20.8. A sweep found visible
    // stone with no collider under it at all three pavilions, the worst being x 0.5..2.0, z 21..22 —
    // reachable by walking north past the west end of Wall_Corridor_CornerGap and drifting east.
    // Rather than nudge individual walls, every visible walk surface gets a collider box under it,
    // grown by a capsule radius so the character controller can never find an edge.
    private const float PatchTopY = -0.05f;   // matches the existing SafetyFloor tops
    private const float PatchMargin = 0.6f;   // controller radius 0.5 + slack
    private const float PatchThickness = 0.6f;

    private static int BuildFloorPatch(Scene scene)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go.name == PatchRoot) Object.DestroyImmediate(go);

        GameObject root = new GameObject(PatchRoot);
        SceneManager.MoveGameObjectToScene(root, scene);

        List<Bounds> boxes = new List<Bounds>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsWalkSurface(r.gameObject.name)) continue;
            Bounds b = r.bounds;
            b.Expand(new Vector3(PatchMargin * 2f, 0f, PatchMargin * 2f));
            boxes.Add(b);
        }

        int made = 0;
        foreach (Bounds b in boxes)
        {
            GameObject go = new GameObject("Patch " + made.ToString("00"));
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(b.center.x, PatchTopY - PatchThickness * 0.5f, b.center.z);
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(b.size.x, PatchThickness, b.size.z);
            made++;
        }
        return made;
    }

    // --- 2. mark the touchable flower ----------------------------------------------------------
    private static bool WireBeacon()
    {
        OsmanthusTouchTrigger trigger = Object.FindFirstObjectByType<OsmanthusTouchTrigger>();
        if (trigger == null) { Debug.LogWarning("[Osmanthus] No OsmanthusTouchTrigger in scene."); return false; }

        OsmanthusBeacon beacon = trigger.GetComponent<OsmanthusBeacon>();
        if (beacon == null) beacon = trigger.gameObject.AddComponent<OsmanthusBeacon>();

        beacon.trigger = trigger;
        beacon.glowMaterial = BeaconMaterial("M_Osmanthus_Beacon_Glow", 0f);
        beacon.ringMaterial = BeaconMaterial("M_Osmanthus_Beacon_Ring", 1f);
        beacon.beaconColor = new Color(1f, 0.74f, 0.32f, 1f);
        EditorUtility.SetDirty(beacon);
        return true;
    }

    private static Material BeaconMaterial(string name, float mode)
    {
        string path = MaterialFolder + name + ".mat";
        Shader shader = Shader.Find("Osmanthus/OsmanthusBeacon");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;

        m.SetColor("_BaseColor", new Color(1f, 0.74f, 0.32f, 1f));
        m.SetFloat("_Mode", mode);
        m.SetFloat("_Alpha", 1f);
        m.SetFloat("_Falloff", 2.2f);
        m.SetFloat("_RingRadius", 0.38f);
        m.SetFloat("_RingWidth", 0.05f);
        m.renderQueue = (int)RenderQueue.Transparent + 10;
        EditorUtility.SetDirty(m);
        return m;
    }
}
