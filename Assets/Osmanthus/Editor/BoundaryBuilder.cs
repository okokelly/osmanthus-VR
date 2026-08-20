using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds invisible collision boundaries so the continuous-locomotion player can't walk off the
// path and fall. A perimeter fence traces the walkable footprint (straight corridor + turn +
// terrace + jetty) and a thin "safety floor" underneath catches any gaps between floor pieces.
// Idempotent: re-running rebuilds the "06 Player Boundaries" root. Tune on-device as needed.
public static class BoundaryBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string RootName = "06 Player Boundaries";
    private const float WallHeight = 2.0f;
    private const float WallThick = 0.3f;

    // (name, centerX, centerZ, sizeX, sizeZ) — walls are WallHeight tall, centred at y = WallHeight/2.
    private static readonly (string, float, float, float, float)[] Walls =
    {
        // Straight corridor sides + entrance cap
        ("Wall_Corridor_Right", 1.85f, -5.5f, WallThick, 51.5f),
        ("Wall_Corridor_Left",  -1.85f, -6.35f, WallThick, 49.4f),
        ("Wall_Entrance",        0.0f, -31.3f, 4.0f, WallThick),
        ("Wall_Corridor_CornerGap", 0.9f, 20.15f, 2.0f, WallThick),

        // Turn + terrace outer perimeter (north / far side)
        ("Wall_Turn_North",   -5.0f, 21.9f, 10.3f, WallThick),
        ("Wall_Terrace_North", -14.3f, 24.15f, 8.9f, WallThick),

        // Turn + terrace outer perimeter (south / near side); corridor entry gap left open
        ("Wall_Turn_South",   -5.9f, 18.1f, 8.5f, WallThick),
        ("Wall_Terrace_South", -14.3f, 15.85f, 8.9f, WallThick),

        // Lake-facing (west) edge, split around the jetty finger
        ("Wall_Terrace_West_N", -18.75f, 22.65f, WallThick, 2.9f),
        ("Wall_Terrace_West_S", -18.75f, 17.35f, WallThick, 2.9f),
        ("Wall_Jetty_Tip",      -23.75f, 20.0f, WallThick, 3.0f),
        ("Wall_Jetty_North",    -21.1f, 21.45f, 5.5f, WallThick),
        ("Wall_Jetty_South",    -21.1f, 18.55f, 5.5f, WallThick),
    };

    // (name, centerX, centerZ, sizeX, sizeZ) — 1 m thick catch floors, top just below the real floor.
    private static readonly (string, float, float, float, float)[] SafetyFloors =
    {
        ("SafetyFloor_Corridor", 0.0f, -5.5f, 4.0f, 52.5f),
        ("SafetyFloor_LakeZone", -12.0f, 20.0f, 26.0f, 11.0f),
    };

    [MenuItem("Osmanthus/Setup/Build Player Boundaries")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        foreach (var w in Walls)
            CreateBox(root.transform, w.Item1,
                new Vector3(w.Item2, WallHeight * 0.5f, w.Item3),
                new Vector3(w.Item4, WallHeight, w.Item5));

        foreach (var f in SafetyFloors)
            CreateBox(root.transform, f.Item1,
                new Vector3(f.Item2, -0.55f, f.Item3), // 1 m thick, top at y = -0.05
                new Vector3(f.Item4, 1.0f, f.Item5));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Built " + (Walls.Length + SafetyFloors.Length) + " boundary collider(s) under '" + RootName + "'.");
    }

    private static void CreateBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size; // object scale is 1, so size is in world metres
    }
}
