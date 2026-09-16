using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Dresses the corridor's surroundings with the seven Meshy garden assets.
//
// The composition is driven by one fact about the existing ground: the strip of land between the
// corridor and the waterline narrows from ~39 m at the entrance (z -31) to ~16 m at the viewing
// terrace (z 20). Walking north, the lake closes in on its own. The planting amplifies that in four
// beats — screen the water at the entrance, leak it in the middle, narrow, then hand it over at the
// turn.
//
// Two constraints shape every position:
//   1. The player never leaves the walkway. BoundaryBuilder locks them to the corridor, the turn,
//      the terrace and the jetty, so everything here is seen from a moving eye at 1.5 m through
//      pillar bays 2.5 m apart. The band that actually reads is 5-25 m out.
//   2. The lake vista video is an arc at radius 46 m due west of (-9.2, 20). The cone west of the
//      jetty is kept clear so the reveal is never sliced by a tree.
//
// Shore-relative planting (willows, reeds, water plants) is sampled off the same shoreline curve
// CourtyardGroundBuilder uses, so it follows the bank if that curve is ever retuned. Inland pieces
// are hand-placed against the pillar bays they are meant to be seen through.
//
// Idempotent: rebuilds the "09 Garden Planting" root every run.
public static class GardenPlantingBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string ModelFolder = "Assets/Osmanthus/Art/GardenPlanting/FBX/";
    private const string TextureFolder = "Assets/Osmanthus/Art/GardenPlanting/Textures/";
    private const string RootName = "09 Garden Planting";
    private const string ScreenshotPath = "Assets/Osmanthus/Screenshots/Scene4_Planting_Aerial.png";

    // ---------------------------------------------------------------------------------------
    // Species. The FBXs already import upright with their pivot on the ground and at true metric
    // scale, so `native` is the real height (or width, for the low spreading pieces) of the asset
    // as authored and the min/max are the range we want to see in the scene.
    // ---------------------------------------------------------------------------------------
    private enum Axis { Height, Width }

    private struct Species
    {
        public string File;
        public Axis Axis;
        public float Native;
        public float Min;
        public float Max;
        // Leaf-card plants need the wrap-lit foliage shader; the stones are solid volumes with
        // sound normals and look correct on URP Lit, so they are left alone.
        public bool Foliage;
        public Species(string file, Axis axis, float native, float min, float max, bool foliage)
        { File = file; Axis = axis; Native = native; Min = min; Max = max; Foliage = foliage; }
    }

    private const int Osmanthus = 0, Taihu = 1, Willow = 2, Rockery = 3, Shrub = 4, Bamboo = 5, WaterEdge = 6;

    private static readonly Species[] Kinds =
    {
        new Species("01_Osmanthus_Tree",        Axis.Height, 4.2f, 4.5f, 7.0f, true),
        new Species("02_Taihu_Standing_Stone",  Axis.Height, 2.8f, 3.0f, 3.8f, false),
        new Species("03_Weeping_Willow",        Axis.Height, 6.5f, 6.0f, 8.0f, true),
        new Species("04_Rockery_Cluster",       Axis.Width,  3.2f, 3.2f, 4.2f, false),
        // The shrub is only 1.6 m across as authored; anything past ~1.8x starts to read as a
        // stretched texture rather than a bigger plant, so the target range is capped there.
        new Species("05_Flowering_Shrub_Clump", Axis.Width,  1.6f, 2.0f, 2.9f, true),
        // Decimation turned the bamboo into fine blades - it reads as reed grass, not cane. Kept
        // near the water where that is exactly right, and held to 4 m so it never reads as
        // implausibly tall grass.
        new Species("06_Bamboo_Clump",          Axis.Height, 3.5f, 2.8f, 4.0f, true),
        // Lotus pads and reeds stand 0.8 m as authored. The original plan asked for 1.8-2.3 m,
        // which would have meant a ~2.5x stretch; a metre of lotus at the waterline is both
        // cheaper and closer to the real thing.
        new Species("07_Water_Edge_Clump",      Axis.Height, 0.8f, 1.0f, 1.4f, true),
    };

    // Per-species foliage tuning. Denser canopies want a little more wrap so their interiors do
    // not go flat; the open reed and lotus clumps want less, or they lose all form.
    private static readonly Dictionary<int, Vector2> FoliageTuning = new Dictionary<int, Vector2>
    {
        // kind -> (wrap, exposure)
        { Osmanthus, new Vector2(0.70f, 1.45f) },
        { Willow,    new Vector2(0.68f, 1.40f) },
        { Shrub,     new Vector2(0.62f, 1.30f) },
        { Bamboo,    new Vector2(0.55f, 1.25f) },
        { WaterEdge, new Vector2(0.55f, 1.20f) },
    };

    private static Material[] foliageMaterials;

    private static GameObject[] prefabs;

    // Measured off the corridor columns: they stand every 2.49 m, so the gaps the player can
    // actually see through are centred here. A taihu stone is only 1.1 m across - put one at a
    // round number like z = -13 and it spends the whole walk hidden behind a post. Wide things
    // (trees, clumps) span several bays and do not need this.
    private static readonly float[] BayCentres =
    {
        -28.48f, -25.99f, -23.50f, -21.47f, -20.00f, -18.54f, -16.51f, -14.02f, -11.53f, -10.00f,
        -8.48f, -5.99f, -3.50f, -1.47f, 0.00f, 1.47f, 3.50f, 5.99f, 8.48f, 10.00f, 11.53f,
        14.02f, 16.51f,
    };

    private static float SnapToBay(float z)
    {
        float best = z, d = float.MaxValue;
        foreach (float c in BayCentres)
        {
            float dd = Mathf.Abs(c - z);
            if (dd < d) { d = dd; best = c; }
        }
        // Only nudge; if the caller asked for somewhere no bay exists, leave it alone.
        return d <= 1.6f ? best : z;
    }

    // ---------------------------------------------------------------------------------------
    // Shoreline, same control points and Catmull-Rom as CourtyardGroundBuilder. Water lies west
    // (-X) of this line.
    // ---------------------------------------------------------------------------------------
    private static readonly Vector2[] ShoreControl =
    {
        new Vector2(-140f, -78f), new Vector2(-95f, -62f), new Vector2(-75f, -55f),
        new Vector2(-45f, -46f),  new Vector2(-25f, -38f), new Vector2(-8f,  -31f),
        new Vector2(4f,   -26f),  new Vector2(13f,  -21f), new Vector2(20f,  -18.6f),
        new Vector2(27f,  -21f),  new Vector2(38f,  -27f), new Vector2(52f,  -37f),
        new Vector2(70f,  -48f),  new Vector2(95f,  -60f), new Vector2(140f, -78f),
    };

    [MenuItem("Osmanthus/Scene 4/Build Garden Planting")]
    public static void Build()
    {
        Scene scene = OpenScene();
        ConfigureTextureImport();
        LoadPrefabs();
        RemovePrevious();

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        Transform entrance = Group("A Entrance Grove", root.transform);
        Transform middle = Group("B-C Corridor Sides", root.transform);
        Transform turn = Group("D Turn and Terrace", root.transform);
        Transform bank = Group("Shoreline", root.transform);
        Transform east = Group("East Lawn", root.transform);

        BuildEntranceGrove(entrance);
        BuildCorridorSides(middle);
        BuildTurnAndTerrace(turn);
        BuildShoreline(bank);
        BuildEastLawn(east);

        int count = root.GetComponentsInChildren<MeshRenderer>(true).Length;
        MarkStaticRecursively(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Osmanthus] Garden planting built: " + count + " instances under '" + RootName + "'.");
    }

    [MenuItem("Osmanthus/Scene 4/Remove Garden Planting")]
    public static void Remove()
    {
        Scene scene = OpenScene();
        RemovePrevious();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Garden planting removed.");
    }

    // =======================================================================================
    // Zones
    // =======================================================================================

    // A. The entrance (z -34 .. -21). The player spawns at (0, -30) facing north into what was
    // open lawn. An osmanthus grove wraps both sides so the first thing you feel is being inside a
    // garden, and the west arm is dense enough to hide the lake - which is 39 m away here and would
    // only read as grey mush. First water is saved for zone B.
    private static void BuildEntranceGrove(Transform parent)
    {
        // Weighted ahead of the spawn, not around it. The player starts at z -30 facing north, so
        // a grove centred on the spawn point puts most of its mass behind their head where it does
        // no work; the enclosure has to sit in the z -32..-21 arc they are actually looking into.
        float[,] westTrees = { { -6.5f, -31.5f, 5.0f }, { -10f, -29f, 6.0f }, { -14f, -26.5f, 6.6f },
                               { -9f, -24.5f, 5.4f },   { -15f, -22f, 6.0f },  { -5.5f, -27f, 4.8f } };
        float[,] eastTrees = { { 6.5f, -31.5f, 5.0f },  { 10f, -29f, 5.8f },   { 13.5f, -26.5f, 5.6f },
                               { 8f, -24f, 5.0f },      { 14f, -21f, 5.8f },   { 5.5f, -27.5f, 4.6f } };
        Place(parent, Osmanthus, westTrees);
        Place(parent, Osmanthus, eastTrees);

        float[,] shrubs = { { -4f, -33f, 0f }, { -8.5f, -33.5f, 0f }, { -17f, -29f, 0f },
                            { -18f, -24f, 0f }, { -12.5f, -20f, 0f }, { 4f, -33f, 0f },
                            { 8.5f, -33.5f, 0f }, { 16.5f, -29f, 0f }, { 5f, -22f, 0f } };
        Place(parent, Shrub, shrubs);

        // Following the entrance spur path out toward the water.
        float[,] rocks = { { -5f, -31f, 0f }, { -12f, -28f, 0f }, { -18f, -24f, 0f }, { -24f, -19f, 0f } };
        Place(parent, Rockery, rocks);
    }

    // B and C. The grove stops and the west band opens - this is the reveal window, so x -6..-24
    // between z -18 and -8 gets nothing tall. The taihu stone at (-7.5, -13) is the first real
    // focal point, silhouetted against the water and framed by a pillar bay. Video 1 triggers at
    // z -18, so what flanks the player there matters.
    private static void BuildCorridorSides(Transform parent)
    {
        float[,] trees = { { 6f, -16f, 5.0f }, { 9.5f, -9f, 5.6f },
                           { -6.5f, 0f, 5.6f }, { -9f, 7f, 5.0f }, { -6f, 14f, 5.8f },
                           { 7f, 2f, 5.0f }, { 11.5f, 12f, 5.6f } };
        Place(parent, Osmanthus, trees);

        // Four standing stones in the whole scene, each aimed at a bay. A stone is a full stop;
        // more than four and none of them land.
        // z values snapped to bay centres - see BayCentres. -13 sat squarely behind a column.
        float[,] stones = { { -7.5f, -14.02f, 3.2f }, { -11f, 3.5f, 3.6f }, { 8f, 8.48f, 3.0f } };
        Place(parent, Taihu, stones);

        float[,] rocks = { { 4f, -12f, 0f }, { 9f, -5f, 0f }, { -13f, -8f, 0f },
                           { -16f, 2f, 0f }, { -15f, 10f, 0f }, { 15f, -1f, 0f },
                           { 21f, 3f, 0f }, { 26f, 10f, 0f } };
        Place(parent, Rockery, rocks);

        float[,] shrubs = { { -11f, -18f, 0f }, { -14f, -3f, 0f }, { 12f, -6f, 0f }, { 18f, -2f, 0f },
                            { -12f, 6f, 0f }, { -13f, 17f, 0f }, { 23f, 1f, 0f }, { 28f, 7f, 0f },
                            { 31f, 15f, 0f } };
        Place(parent, Shrub, shrubs);
    }

    // D. The payoff. Coming out of the turn pavilion the player pivots west; the fourth stone at
    // (-6, 24) is the first thing in frame and two osmanthus canopy the walk out. Then everything
    // gets out of the way - nothing is placed inside the cone west of the jetty.
    private static void BuildTurnAndTerrace(Transform parent)
    {
        Place(parent, Taihu, new float[,] { { -6f, 24f, 3.8f } });
        Place(parent, Osmanthus, new float[,] { { -8f, 26f, 6.4f }, { -8f, 14f, 5.8f } });

        float[,] rocks = { { -19.5f, 15f, 0f }, { -19.5f, 25f, 0f }, { -17f, 13f, 0f },
                           { -17f, 27f, 0f }, { -11f, 30f, 0f }, { -5f, 31f, 0f } };
        Place(parent, Rockery, rocks);

        // Nothing between x -10 and the jetty on the z 16..24 band: two shrubs sat at z 19.5/20.5
        // in the first pass and blocked the entire lake reveal from the terrace.
        float[,] shrubs = { { -14f, 28f, 0f }, { -4f, 28f, 0f }, { -9f, 30f, 0f },
                            { 3f, 20f, 0f }, { 5f, 25f, 0f }, { -15.5f, 13f, 0f }, { -15.5f, 27f, 0f } };
        Place(parent, Shrub, shrubs);
    }

    // Everything that reads as "this is a real lake edge". Willows form one continuous line on the
    // bank, gapped between z 15 and 25 so they never cross the lake vista. Reeds and lotus sit at
    // the waterline. The two clumps closest to the jetty are placed off the west axis on purpose:
    // near-field parallax is the strongest depth cue in VR, but not if it blocks the view.
    private static void BuildShoreline(Transform parent)
    {
        float[] willowZ = { -30f, -22f, -14f, -6f, 1f, 8f, 13f, 27f, 33f, 39f, 45f, 52f };
        float[] willowH = { 6.4f, 7.0f, 7.4f, 7.0f, 7.8f, 7.4f, 6.6f, 6.6f, 7.4f, 7.0f, 6.4f, 6.2f };
        for (int i = 0; i < willowZ.Length; i++)
            PlaceAt(parent, Willow, OnShore(willowZ[i], 2.0f), willowH[i]);

        float[] lotusZ = { -24f, -19f, -11f, -3f, 3f, 9f, 14f, 16.5f, 24f, 26.5f, 30f, 35f, 41f, 47f };
        foreach (float z in lotusZ)
            PlaceAt(parent, WaterEdge, OnShore(z, -0.8f), 0f);

        // Deliberately off the jetty's west axis, flanking rather than blocking.
        PlaceAt(parent, WaterEdge, new Vector2(-24.5f, 15.5f), 1.35f);
        PlaceAt(parent, WaterEdge, new Vector2(-25f, 25f), 1.35f);

        // Reed grass a few metres up the bank, where the bamboo asset's blade silhouette belongs.
        float[] reedZ = { -20f, -12f, 0f, 6f, 12f, 30f, 36f, 42f };
        foreach (float z in reedZ)
            PlaceAt(parent, Bamboo, OnShore(z, 4.5f), 0f);
    }

    // The east side has no lake, just lawn out to the treeline ring at radius 96. Without a middle
    // ground it reads as an empty field the moment the player turns their head.
    private static void BuildEastLawn(Transform parent)
    {
        Place(parent, Osmanthus, new float[,] { { 22f, -6f, 6.6f }, { 27f, 16f, 7.0f } });
        float[,] reeds = { { 16f, -4f, 0f }, { 18f, 4f, 0f }, { 20f, 11f, 0f }, { 17f, 18f, 0f },
                           { 32f, -14f, 0f }, { 36f, -2f, 0f }, { 38f, 9f, 0f }, { 35f, 24f, 0f } };
        Place(parent, Bamboo, reeds);
    }

    // =======================================================================================
    // Placement helpers
    // =======================================================================================

    // rows are { x, z, size } - size 0 means "pick one from the species range".
    private static void Place(Transform parent, int kind, float[,] rows)
    {
        for (int i = 0; i < rows.GetLength(0); i++)
            PlaceAt(parent, kind, new Vector2(rows[i, 0], rows[i, 1]), rows[i, 2]);
    }

    private static void PlaceAt(Transform parent, int kind, Vector2 xz, float size)
    {
        Species s = Kinds[kind];
        // Derived from the coordinates so a rebuild reproduces the same garden, but no two
        // instances share a size or a facing.
        float r = Hash(xz.x, xz.y, kind);
        if (size <= 0f) size = Mathf.Lerp(s.Min, s.Max, r);
        float scale = size / s.Native;

        GameObject go = PrefabUtility.InstantiatePrefab(prefabs[kind], parent.gameObject.scene) as GameObject;
        if (go == null) go = Object.Instantiate(prefabs[kind]);
        go.name = s.File + " " + xz.x.ToString("0.0") + "_" + xz.y.ToString("0.0");
        go.transform.SetParent(parent, false);

        // These FBXs declare Y-up in the header but hold Z-up geometry, so Unity's importer puts a
        // -90 X correction on the prefab root rather than in the vertices (bakeAxisConversion is
        // off). Assigning a plain yaw here would wipe that and lay every asset on its side - which
        // reads as "the trees are a bit bushy" and only becomes obvious on the tall, narrow taihu
        // stone. Same treatment the corridor gets in CompleteCorridorLakeBuilder: keep the
        // correction, turn on top of it.
        Quaternion axisCorrection = go.transform.localRotation;
        go.transform.localPosition = new Vector3(xz.x, 0f, xz.y);
        go.transform.localRotation = Quaternion.Euler(0f, Hash(xz.y, xz.x, kind + 17) * 360f, 0f) * axisCorrection;
        go.transform.localScale = Vector3.one * scale;

        Material foliage = foliageMaterials != null ? foliageMaterials[kind] : null;
        foreach (Renderer r2 in go.GetComponentsInChildren<Renderer>(true))
        {
            // The scene runs without realtime shadows; keep these consistent with the corridor.
            r2.shadowCastingMode = ShadowCastingMode.Off;
            r2.receiveShadows = false;
            r2.lightProbeUsage = LightProbeUsage.Off;
            r2.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (foliage != null)
            {
                Material[] slots = r2.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = foliage;
                r2.sharedMaterials = slots;
            }
        }
        // Nothing here is reachable - the boundary colliders keep the player on the walkway - so
        // the colliders would only cost memory.
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
    }

    private static float Hash(float a, float b, int salt)
    {
        float v = Mathf.Sin(a * 12.9898f + b * 78.233f + salt * 37.719f) * 43758.5453f;
        return Mathf.Abs(v - Mathf.Floor(v));
    }

    // =======================================================================================
    // Shoreline sampling
    // =======================================================================================

    private static List<Vector3> shoreCache;

    private static List<Vector3> Shore()
    {
        if (shoreCache != null) return shoreCache;
        shoreCache = new List<Vector3>();
        int n = ShoreControl.Length;
        const int samples = 400;
        for (int i = 0; i < samples; i++)
        {
            float u = i / (float)(samples - 1) * (n - 1);
            int k = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 2);
            float f = u - k;
            Vector2 p0 = ShoreControl[Mathf.Max(k - 1, 0)];
            Vector2 p1 = ShoreControl[k];
            Vector2 p2 = ShoreControl[k + 1];
            Vector2 p3 = ShoreControl[Mathf.Min(k + 2, n - 1)];
            Vector2 v = 0.5f * ((2f * p1) + (-p0 + p2) * f
                       + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f
                       + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
            shoreCache.Add(new Vector3(v.y, 0f, v.x)); // control points are (z, x)
        }
        return shoreCache;
    }

    // A point `inland` metres east of the waterline at this z. Negative pushes into the water.
    private static Vector2 OnShore(float z, float inland)
    {
        List<Vector3> pts = Shore();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            float z0 = pts[i].z, z1 = pts[i + 1].z;
            if ((z0 - z) * (z1 - z) > 0f || Mathf.Approximately(z0, z1)) continue;
            float t = (z - z0) / (z1 - z0);
            float x = Mathf.Lerp(pts[i].x, pts[i + 1].x, t);
            return new Vector2(x + inland, z);
        }
        return new Vector2(-30f + inland, z);
    }

    // =======================================================================================
    // Assets and import settings
    // =======================================================================================

    private const string MaterialFolder = "Assets/Osmanthus/Materials/";

    private static void LoadPrefabs()
    {
        prefabs = new GameObject[Kinds.Length];
        for (int i = 0; i < Kinds.Length; i++)
        {
            string path = ModelFolder + Kinds[i].File + ".fbx";
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabs[i] == null)
                throw new System.IO.FileNotFoundException("Garden model missing", path);
        }
        BuildFoliageMaterials();
    }

    // One wrap-lit material per plant, reusing the albedo Unity already extracted from the FBX.
    // Shared per species so the SRP batcher can keep them in one batch.
    private static void BuildFoliageMaterials()
    {
        Shader shader = Shader.Find("Osmanthus/GardenFoliage");
        if (shader == null)
        {
            Debug.LogWarning("[Osmanthus] Osmanthus/GardenFoliage not found; plants stay on URP Lit.");
            foliageMaterials = new Material[Kinds.Length];
            return;
        }

        foliageMaterials = new Material[Kinds.Length];
        for (int i = 0; i < Kinds.Length; i++)
        {
            if (!Kinds[i].Foliage) continue;

            Texture albedo = null;
            MeshRenderer source = prefabs[i].GetComponentInChildren<MeshRenderer>();
            if (source != null && source.sharedMaterial != null && source.sharedMaterial.HasProperty("_BaseMap"))
                albedo = source.sharedMaterial.GetTexture("_BaseMap");
            if (albedo == null)
                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + Kinds[i].File + "_Albedo.png");

            string path = MaterialFolder + "M_Garden_" + Kinds[i].File + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "M_Garden_" + Kinds[i].File };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            Vector2 tune = FoliageTuning.ContainsKey(i) ? FoliageTuning[i] : new Vector2(0.65f, 1.35f);
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_AmbientColor", new Color(0.34f, 0.38f, 0.35f, 1f));
            m.SetFloat("_Wrap", tune.x);
            m.SetFloat("_Exposure", tune.y);
            m.SetFloat("_KeyStrength", 0.95f);
            m.SetFloat("_BaseShade", 0.25f);
            EditorUtility.SetDirty(m);
            foliageMaterials[i] = m;
        }
        AssetDatabase.SaveAssets();
    }

    // The albedos are fully opaque - checked per-pixel, every alpha is 255 - so the alpha channel
    // is dead weight and the meshes can stay on the opaque queue with no alpha test. On Android the
    // textures are pinned to 512 ASTC 6x6, which puts the whole set at roughly 1.7 MB.
    private static void ConfigureTextureImport()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            TextureImporterPlatformSettings android = ti.GetPlatformTextureSettings("Android");
            bool dirty = false;

            if (ti.alphaSource != TextureImporterAlphaSource.None)
            { ti.alphaSource = TextureImporterAlphaSource.None; ti.alphaIsTransparency = false; dirty = true; }

            if (!android.overridden || android.maxTextureSize != 512
                || android.format != TextureImporterFormat.ASTC_6x6)
            {
                android.overridden = true;
                android.maxTextureSize = 512;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.textureCompression = TextureImporterCompression.Compressed;
                ti.SetPlatformTextureSettings(android);
                dirty = true;
            }

            if (dirty) { ti.SaveAndReimport(); changed++; }
        }
        if (changed > 0)
            Debug.Log("[Osmanthus] Retuned " + changed + " planting texture(s) for Android: 512 ASTC 6x6, alpha dropped.");
    }

    // =======================================================================================
    // Scene helpers
    // =======================================================================================

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

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void MarkStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }

    // =======================================================================================
    // Preview
    // =======================================================================================

    [MenuItem("Osmanthus/Scene 4/Render Planting Aerial")]
    public static void RenderAerial()
    {
        OpenScene();
        GameObject camObject = new GameObject("~PlantingAerialCam");
        Camera cam = camObject.AddComponent<Camera>();
        cam.transform.position = new Vector3(-14f, 120f, 0f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = 52f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.54f, 0.63f, 0.67f, 1f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
        Capture(cam, ScreenshotPath, 1200, 1200);
        Object.DestroyImmediate(camObject);
    }

    private static void Capture(Camera cam, string path, int w, int h)
    {
        RenderTexture target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        RenderTexture previous = RenderTexture.active;
        cam.targetTexture = target;
        RenderTexture.active = target;
        cam.Render();

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderTexture.active = previous;
        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        target.Release();
        Object.DestroyImmediate(target);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Osmanthus] Saved " + path);
    }
}
