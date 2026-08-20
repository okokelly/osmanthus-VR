using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Assembles the Phase C "touch osmanthus -> unfold video" interaction:
//   - a guide osmanthus (float + touch trigger) parented to a mover for the drift waypoints,
//   - a VideoScreen (builds its own visuals at runtime),
//   - the OsmanthusVideoSequence controller,
//   - a ray + trigger-button poker on the right controller (aim + pull index trigger).
// Idempotent: rebuilds the "07 Osmanthus Interaction" root and re-adds the poker.
public static class PhaseCBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string PrefabPath = "Assets/Osmanthus/Prefabs/Osmanthus_Golden.prefab";
    private const string BrightMatPath = "Assets/Osmanthus/Materials/M_Osmanthus_Golden_Bright.mat";
    private const string RootName = "07 Osmanthus Interaction";
    private const string RigName = "OVRPlayerController";

    private static readonly Vector3 WaypointA = new Vector3(0f, 1.4f, -18f);

    [MenuItem("Osmanthus/Setup/Build Phase C Interaction")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Osmanthus] Osmanthus prefab missing; run Build Osmanthus first."); return; }

        // Clean previous build + any existing pokers.
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        foreach (ControllerRayPoker p in Object.FindObjectsByType<ControllerRayPoker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform aim = p.transform.Find("AimLine");
            if (aim != null) Object.DestroyImmediate(aim.gameObject);
            Object.DestroyImmediate(p);
        }

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        // Guide osmanthus on a mover (the sequence drifts the mover between waypoints).
        GameObject mover = new GameObject("GuideMover");
        mover.transform.SetParent(root.transform, false);
        mover.transform.position = WaypointA;

        GameObject guide = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        guide.name = "Guide Osmanthus";
        guide.transform.SetParent(mover.transform, false);
        guide.transform.localPosition = Vector3.zero;
        guide.transform.localScale = Vector3.one * 1.6f;

        Material bright = AssetDatabase.LoadAssetAtPath<Material>(BrightMatPath);
        if (bright != null)
            foreach (Renderer r in guide.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = bright;

        SphereCollider col = guide.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f; // x1.6 scale -> ~0.48 m, easy to aim at
        OsmanthusTouchTrigger trigger = guide.AddComponent<OsmanthusTouchTrigger>();

        // Video screen (builds its visuals at runtime in Awake).
        GameObject screenGO = new GameObject("VideoScreen");
        screenGO.transform.SetParent(root.transform, false);
        VideoScreen screen = screenGO.AddComponent<VideoScreen>();

        // Sequence controller.
        GameObject seqGO = new GameObject("Osmanthus Video Sequence");
        seqGO.transform.SetParent(root.transform, false);
        OsmanthusVideoSequence seq = seqGO.AddComponent<OsmanthusVideoSequence>();
        seq.guideMover = mover.transform;
        seq.trigger = trigger;
        seq.screen = screen;

        // Right-controller ray poker.
        GameObject rig = GameObject.Find(RigName);
        if (rig != null)
        {
            Transform anchor = FindChildByName(rig.transform, "RightHandAnchor");
            if (anchor != null) AddPoker(anchor);
            else Debug.LogWarning("[Osmanthus] RightHandAnchor not found on the OVR rig; add the ray poker manually.");
        }
        else Debug.LogWarning("[Osmanthus] " + RigName + " not found; run Add Meta Player Rig first.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Phase C interaction built. Aim the right controller at the guide osmanthus and pull the index trigger.");
    }

    private static void AddPoker(Transform anchor)
    {
        ControllerRayPoker poker = anchor.gameObject.AddComponent<ControllerRayPoker>();
        poker.maxDistance = 14f;

        GameObject lineGO = new GameObject("AimLine");
        lineGO.transform.SetParent(anchor, false);
        LineRenderer lr = lineGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = lr.endWidth = 0.006f;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        lr.material = new Material(sh);
        poker.line = lr;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
