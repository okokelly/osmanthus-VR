using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the Meta (Oculus) OVRPlayerController rig for continuous joystick locomotion on Quest.
// This replaces the XRI XR Origin rig. Idempotent: re-running swaps the rig in place.
public static class MetaPlayerRigSetup
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string RigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRPlayerController.prefab";
    private const string XrRootName = "--- XR SYSTEM ---";
    private const string RigName = "OVRPlayerController";
    private const string XriRigName = "XR Origin (XR Rig)";

    // Player start: just inside the corridor entrance (Entrance Threshold z = -31), facing +Z.
    private static readonly Vector3 StartPosition = new Vector3(0f, 0f, -30f);
    private static readonly Vector3 StartEuler = new Vector3(0f, 0f, 0f);

    [MenuItem("Osmanthus/Setup/Add Meta Player Rig OVR")]
    public static void AddRig()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Osmanthus] OVRPlayerController prefab not found at " + RigPrefabPath);
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

        // Remove the XRI rig and any previous OVR rig so this is a clean swap.
        GameObject xri = GameObject.Find(XriRigName);
        if (xri != null) Object.DestroyImmediate(xri);
        GameObject existing = GameObject.Find(RigName);
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject rig = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        rig.name = RigName;
        rig.transform.SetParent(xrRoot.transform, true);
        rig.transform.SetPositionAndRotation(StartPosition, Quaternion.Euler(StartEuler));

        // Fixed manual eye height (160 cm): EyeLevel tracking + profile data off + camera raised.
        // Done by reflection so this editor script needs no hard reference to the Oculus assembly.
        ConfigureFixedEyeHeight(rig);

        // Keep the rig's own cameras; demote every other camera so the headset camera is the
        // single Main Camera / audio listener at runtime.
        HashSet<Camera> rigCameras = new HashSet<Camera>(rig.GetComponentsInChildren<Camera>(true));
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
        Debug.Log("[Osmanthus] Meta OVRPlayerController rig added at " + StartPosition +
                  ", replaced XRI rig, demoted " + demoted + " camera(s). Set tracking origin to Floor.");
    }

    private const float EyeHeight = 1.6f;

    private static void ConfigureFixedEyeHeight(GameObject rig)
    {
        foreach (MonoBehaviour c in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null) continue;
            string tn = c.GetType().Name;
            if (tn == "OVRManager")
            {
                object eyeLevel = ParseEnumMember(c, "trackingOriginType", "EyeLevel");
                if (eyeLevel != null) SetMember(c, "trackingOriginType", eyeLevel);
            }
            else if (tn == "OVRPlayerController")
            {
                // Stop it from overriding the camera height with the device profile.
                SetMember(c, "useProfileData", false);
            }
        }

        Transform camRig = FindChildByName(rig.transform, "OVRCameraRig");
        if (camRig != null)
        {
            Vector3 p = camRig.localPosition;
            camRig.localPosition = new Vector3(p.x, EyeHeight, p.z);
        }
        Debug.Log("[Osmanthus] Fixed eye height set to " + EyeHeight + "m (EyeLevel tracking, profile data off).");
    }

    private static object ParseEnumMember(Component c, string member, string valueName)
    {
        System.Type t = c.GetType();
        System.Type mt = t.GetProperty(member)?.PropertyType ?? t.GetField(member)?.FieldType;
        if (mt == null || !mt.IsEnum) return null;
        try { return System.Enum.Parse(mt, valueName); }
        catch { return null; }
    }

    private static void SetMember(Component c, string member, object value)
    {
        System.Type t = c.GetType();
        var prop = t.GetProperty(member);
        if (prop != null && prop.CanWrite) { prop.SetValue(c, value); return; }
        var field = t.GetField(member);
        if (field != null) field.SetValue(c, value);
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
