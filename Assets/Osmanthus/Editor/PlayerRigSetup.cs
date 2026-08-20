using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the Meta Quest player rig (XR Interaction Toolkit Starter Assets "XR Origin (XR Rig)")
// to the main scene. Idempotent: re-running replaces the existing rig instance.
public static class PlayerRigSetup
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string RigPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string XrRootName = "--- XR SYSTEM ---";
    private const string RigName = "XR Origin (XR Rig)";

    // Player start: just inside the corridor entrance (Entrance Threshold is at z = -31),
    // facing +Z down the corridor toward the pillars and the lake.
    private static readonly Vector3 StartPosition = new Vector3(0f, 0f, -30f);
    private static readonly Vector3 StartEuler = new Vector3(0f, 0f, 0f);

    [MenuItem("Osmanthus/Setup/Add Quest Player Rig")]
    public static void AddRig()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Osmanthus] XR rig prefab not found at " + RigPrefabPath);
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject xrRoot = GameObject.Find(XrRootName);
        if (xrRoot == null)
        {
            xrRoot = new GameObject(XrRootName);
            SceneManager.MoveGameObjectToScene(xrRoot, scene);
        }

        // Remove a previous rig instance so this is repeatable.
        GameObject existing = GameObject.Find(RigName);
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject rig = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        rig.name = RigName;
        rig.transform.SetParent(xrRoot.transform, true);
        rig.transform.SetPositionAndRotation(StartPosition, Quaternion.Euler(StartEuler));

        // Collect the rig's own cameras so we don't demote them below.
        HashSet<Camera> rigCameras = new HashSet<Camera>(rig.GetComponentsInChildren<Camera>(true));

        // Demote every non-rig camera: disable it, drop the MainCamera tag, mute its AudioListener,
        // so the headset camera is the single Main Camera / audio listener at runtime.
        int demoted = 0;
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rigCameras.Contains(cam)) continue;
            cam.enabled = false;
            if (cam.CompareTag("MainCamera")) cam.tag = "Untagged";
            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            demoted++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Quest player rig added at " + StartPosition +
                  ". Demoted " + demoted + " preview camera(s). Teleport areas auto-bind to the rig's provider at runtime.");
    }
}
