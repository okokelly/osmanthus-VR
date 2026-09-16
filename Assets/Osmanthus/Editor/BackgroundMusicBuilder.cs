using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ambient background music for Scene 4.
//
// One 2D looping AudioSource, deliberately quiet - it sits under the piece rather than scoring it.
// Nothing about it is positional: spatialBlend is 0 so it does not pan or attenuate as the player
// turns their head or walks the corridor, which is what you want for a bed and emphatically not
// what you want if it were a diegetic source.
//
// The track runs 4:33 against a roughly 4 minute experience - close enough that anyone who pauses
// to look around will reach the loop point, so looping is doing real work here, not just insurance.
//
// Idempotent: rebuilds the "10 Background Music" root every run.
public static class BackgroundMusicBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string ClipPath = "Assets/Osmanthus/Audio/Embers.mp3";
    private const string RootName = "10 Background Music";

    // Quiet enough to sit under the two video segments, whose audio runs through the VideoPlayer's
    // Direct output and is not ducked. Raise here if it needs more presence on device.
    private const float Volume = 0.18f;

    [MenuItem("Osmanthus/Scene 4/Build Background Music")]
    public static void Build()
    {
        Scene scene = OpenScene();
        ConfigureClipImport();

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null)
            throw new System.IO.FileNotFoundException("Background music clip missing", ClipPath);

        RemovePrevious();

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        AudioSource source = root.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;            // seamless restart at the end of the track
        source.playOnAwake = true;
        source.volume = Volume;
        source.spatialBlend = 0f;      // fully 2D: no panning, no distance falloff
        source.dopplerLevel = 0f;
        source.bypassReverbZones = true;
        source.priority = 0;           // never voice-culled in favour of an effect
        source.mute = false;

        EnsureListener();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log(string.Format("[Osmanthus] Background music wired: {0} ({1:0.0}s), loop on, volume {2}.",
            clip.name, clip.length, Volume));
    }

    [MenuItem("Osmanthus/Scene 4/Remove Background Music")]
    public static void Remove()
    {
        Scene scene = OpenScene();
        RemovePrevious();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Background music removed.");
    }

    // Decoded into memory this clip would cost roughly 46 MB of PCM on a headset that has none to
    // spare. Streaming keeps it to a small ring buffer, which is the right trade for something that
    // plays start to finish and is never retriggered.
    private static void ConfigureClipImport()
    {
        AudioImporter importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
        if (importer == null) return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        // The source is already a 64 kbps MP3; re-encoding above that only inflates the file.
        settings.quality = 0.5f;
        settings.preloadAudioData = true;

        bool dirty = importer.defaultSampleSettings.loadType != settings.loadType
                  || importer.defaultSampleSettings.compressionFormat != settings.compressionFormat
                  || !Mathf.Approximately(importer.defaultSampleSettings.quality, settings.quality)
                  || importer.forceToMono
                  || !importer.loadInBackground;

        importer.defaultSampleSettings = settings;
        importer.SetOverrideSampleSettings("Android", settings);
        importer.forceToMono = false;      // keep the stereo image; streaming makes the cost moot
        importer.loadInBackground = true;  // no hitch on scene load
        importer.ambisonic = false;

        if (dirty) importer.SaveAndReimport();
    }

    // The OVR rig carries the live listener; the XR Origin's spare is disabled. If neither is
    // present the music would play to nothing, so say so rather than fail silently.
    private static void EnsureListener()
    {
        foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (listener.enabled && listener.gameObject.activeInHierarchy) return;

        Debug.LogWarning("[Osmanthus] No enabled AudioListener in the scene - the music will be inaudible. "
                       + "The live one normally sits on OVRCameraRig/TrackingSpace/CenterEyeAnchor.");
    }

    private static Scene OpenScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return scene;
    }

    private static void RemovePrevious()
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.name == RootName) Object.DestroyImmediate(go);
    }
}
