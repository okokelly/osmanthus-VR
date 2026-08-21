using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Lays the base ground for Scene 4: a courtyard the corridor actually stands in, and a lake that
// stops at a shoreline instead of running underneath the building.
//
// The scene shipped with one 70 x 95 m water plane at y = 0.28 spanning x -65..+5, z -18.5..+76.5.
// The corridor footprint is x -10..+2.5, z -30..+22.5 and its walkway tops out at y = 0.10, so 41 m
// of the corridor was submerged: looking down you saw water, not paving, and looking out either side
// there was open water to the horizon and no garden at all.
//
// This replaces that plane with a shaped lake west of the corridor, a large mottled ground plane,
// a bank along the waterline and a few gravel paths. Deliberately plain: it is the base layer for
// hand-placed rockery / planting / osmanthus trees later.
public static class CourtyardGroundBuilder
{
    private const string ScenePath = "Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity";
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private const string RootName = "07 Courtyard Ground";
    private const string GroundShaderName = "Osmanthus/CourtyardGround";
    private const string LakeShaderName = "Osmanthus/Scene3LakeQuick";
    private const string ScreenshotPath = "Assets/Osmanthus/Screenshots/Scene4_Courtyard_Aerial.png";

    // Heights. The walkable stone tops out at 0.10, so everything here sits below it and the paving
    // reads as a raised walk.
    // Depth precision, not aesthetics, sets these gaps. At 2 cm the bank z-fought the water into a
    // dashed line, and at 6 cm the ground quad running under the lake z-fought it into a
    // checkerboard across the whole far half of the view. The ground is now cut at the shoreline so
    // it never overlaps the water at all, and the remaining layers are ~10 cm apart.
    private const float GroundY = -0.06f;
    private const float PathY = 0.04f;
    private const float BankY = 0.09f;
    private const float WaterY = 0.0f;

    private const float WaterFarX = -170f;   // out past the ground plane, under the video vista card

    // Lake edge, as (z, x) control points. The water lies west (-X) of this line. It closes right in
    // at the viewing terrace, whose west face is x = -18.6, so the jetty projects over open water,
    // and swings away south and north so the corridor looks out over garden, not lake.
    private static readonly Vector2[] ShoreControl =
    {
        new Vector2(-140f, -78f),
        new Vector2(-95f, -62f),
        new Vector2(-75f, -55f),
        new Vector2(-45f, -46f),
        new Vector2(-25f, -38f),
        new Vector2(-8f,  -31f),
        new Vector2(4f,   -26f),
        new Vector2(13f,  -21f),
        new Vector2(20f,  -18.6f),   // the terrace edge
        new Vector2(27f,  -21f),
        new Vector2(38f,  -27f),
        new Vector2(52f,  -37f),
        new Vector2(70f,  -48f),
        new Vector2(95f,  -60f),
        new Vector2(140f, -78f),
    };

    [MenuItem("Osmanthus/Scene 4/Build Courtyard Ground")]
    public static void Build()
    {
        Scene scene = OpenScene();
        RemovePrevious();

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        List<Vector3> shore = SampleShore(160);

        Material grass = GroundMaterial("M_Courtyard_Grass",
            new Color(0.21f, 0.25f, 0.14f, 1f), new Color(0.32f, 0.35f, 0.19f, 1f),
            0.055f, 1.5f, 3.0f, 0.18f, false);
        // Gravel and bank sit only a little above the grass in value. Read against a dark lawn, a
        // mid-grey ribbon reads as poured concrete, which is what the first pass looked like.
        Material path = GroundMaterial("M_Courtyard_Path",
            new Color(0.29f, 0.27f, 0.23f, 1f), new Color(0.37f, 0.34f, 0.29f, 1f),
            0.5f, 1.2f, 6.0f, 0.20f, true);
        Material bank = GroundMaterial("M_Courtyard_Bank",
            new Color(0.32f, 0.31f, 0.27f, 1f), new Color(0.40f, 0.38f, 0.33f, 1f),
            0.4f, 1.2f, 5.0f, 0.16f, true);

        // --- Ground -------------------------------------------------------------------------
        // Follows the shoreline on its west side rather than running under the lake, then caps off
        // beyond the shoreline's ends. Big enough east that its far edge lands under the painted
        // backdrop arc (radius 180) rather than in mid-air; the distance haze carries the last
        // stretch. The caps sit past the treeline ring, so their straight edges are never seen.
        AddMesh("Courtyard Ground", root.transform, ShoreMesh(shore, 150f, GroundY), grass, 0);
        AddMesh("Courtyard Ground - South Cap", root.transform,
            QuadMesh(-175f, 150f, -165f, shore[0].z + 0.05f, GroundY), grass, 0);
        AddMesh("Courtyard Ground - North Cap", root.transform,
            QuadMesh(-175f, 150f, shore[shore.Count - 1].z - 0.05f, 185f, GroundY), grass, 0);

        // --- Lake ---------------------------------------------------------------------------
        Material lakeMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "M_Scene3_LakeQuick.mat");
        if (lakeMat == null) lakeMat = grass;
        AddMesh("Courtyard Lake Surface", root.transform, ShoreMesh(shore, WaterFarX, WaterY), lakeMat, 0);
        RetuneLakeForShore(lakeMat);

        // The original rectangular plane is superseded. Kept in the scene, just switched off.
        SetActive("Quick Lake Surface", false);

        // --- Bank along the waterline --------------------------------------------------------
        AddMesh("Shore Bank", root.transform,
            RibbonMesh(OffsetShore(shore, -0.15f), OffsetShore(shore, 2.2f), BankY), bank, 1);

        // --- Distant planting ring -----------------------------------------------------------
        AddMesh("Distant Treeline", root.transform,
            ArcMesh(new Vector3(-6f, 0f, -4f), 96f, -1.5f, 13f, 200), TreelineMaterial(), 3);

        // --- Paths ---------------------------------------------------------------------------
        // A walk following the lake a few metres inland, and two spurs off the corridor so the
        // garden reads as somewhere you could go rather than open lawn.
        List<Vector3> shoreWalk = new List<Vector3>();
        foreach (Vector3 p in shore)
        {
            if (p.z < -28f || p.z > 48f) continue;
            shoreWalk.Add(new Vector3(p.x + 6.5f, 0f, p.z));
        }
        AddPath("Path - Shore Walk", root.transform, shoreWalk, 1.2f, path);

        AddPath("Path - East Garden", root.transform, Curve(new[]
        {
            new Vector3(3.0f, 0f, -4f), new Vector3(11f, 0f, -7f), new Vector3(19f, 0f, -3f),
            new Vector3(26f, 0f, 4f), new Vector3(31f, 0f, 13f), new Vector3(33f, 0f, 24f)
        }, 8), 1.1f, path);

        AddPath("Path - Entrance Spur", root.transform, Curve(new[]
        {
            new Vector3(-3f, 0f, -33f), new Vector3(-11f, 0f, -32f), new Vector3(-19f, 0f, -29f),
            new Vector3(-25f, 0f, -24f), new Vector3(-28f, 0f, -17f)
        }, 8), 1.1f, path);

        PaveCorridorFloor();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Osmanthus] Courtyard ground built. Lake now stops at the shoreline; corridor stands on land.");
    }

    [MenuItem("Osmanthus/Scene 4/Remove Courtyard Ground")]
    public static void Remove()
    {
        Scene scene = OpenScene();
        RemovePrevious();
        SetActive("Quick Lake Surface", true);
        RestoreCorridorFloor();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Osmanthus] Courtyard ground removed; original lake plane restored.");
    }

    [MenuItem("Osmanthus/Scene 4/Render Courtyard Aerial")]
    public static void RenderAerial()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath) OpenScene();

        GameObject camObject = new GameObject("~CourtyardAerialCam");
        Camera cam = camObject.AddComponent<Camera>();
        cam.transform.position = new Vector3(-24f, 130f, 6f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = 82f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.54f, 0.63f, 0.67f, 1f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 600f;

        const int W = 1100, H = 1100;
        RenderTexture target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        RenderTexture previous = RenderTexture.active;
        cam.targetTexture = target;
        RenderTexture.active = target;
        cam.Render();

        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        texture.Apply();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScreenshotPath));
        System.IO.File.WriteAllBytes(ScreenshotPath, texture.EncodeToPNG());

        RenderTexture.active = previous;
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(camObject);
        target.Release();
        Object.DestroyImmediate(target);

        AssetDatabase.ImportAsset(ScreenshotPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Osmanthus] Saved courtyard aerial: " + ScreenshotPath);
    }

    // --- shoreline ----------------------------------------------------------------------------

    // Catmull-Rom through the control points, so the lake edge curves instead of showing facets.
    private static List<Vector3> SampleShore(int samples)
    {
        List<Vector3> pts = new List<Vector3>();
        int n = ShoreControl.Length;
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
            pts.Add(new Vector3(v.y, 0f, v.x)); // control points are (z, x)
        }
        return pts;
    }

    // Shifts the shoreline inland (+) or into the water (-), along its own normal.
    private static List<Vector3> OffsetShore(List<Vector3> shore, float distance)
    {
        List<Vector3> result = new List<Vector3>(shore.Count);
        for (int i = 0; i < shore.Count; i++)
        {
            Vector3 a = shore[Mathf.Max(i - 1, 0)];
            Vector3 b = shore[Mathf.Min(i + 1, shore.Count - 1)];
            Vector3 tangent = (b - a).normalized;
            Vector3 normal = new Vector3(tangent.z, 0f, -tangent.x); // points inland (+X side)
            result.Add(shore[i] + normal * distance);
        }
        return result;
    }

    private static List<Vector3> Curve(Vector3[] control, int subdivisions)
    {
        List<Vector3> pts = new List<Vector3>();
        int n = control.Length;
        int samples = (n - 1) * subdivisions;
        for (int i = 0; i <= samples; i++)
        {
            float u = i / (float)samples * (n - 1);
            int k = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 2);
            float f = u - k;
            Vector3 p0 = control[Mathf.Max(k - 1, 0)];
            Vector3 p1 = control[k];
            Vector3 p2 = control[k + 1];
            Vector3 p3 = control[Mathf.Min(k + 2, n - 1)];
            pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * f
                  + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f
                  + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f));
        }
        return pts;
    }

    // --- meshes -------------------------------------------------------------------------------

    private static Mesh QuadMesh(float x0, float x1, float z0, float z1, float y)
    {
        Mesh m = new Mesh { name = "CourtyardQuad" };
        m.vertices = new[]
        {
            new Vector3(x0, y, z0), new Vector3(x1, y, z0),
            new Vector3(x0, y, z1), new Vector3(x1, y, z1)
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        return Finish(m);
    }

    // Water: the strip between the shoreline and a far edge out west.
    private static Mesh ShoreMesh(List<Vector3> shore, float farX, float y)
    {
        int n = shore.Count;
        Vector3[] verts = new Vector3[n * 2];
        Vector2[] uv = new Vector2[n * 2];
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            verts[i * 2] = new Vector3(shore[i].x, y, shore[i].z);
            verts[i * 2 + 1] = new Vector3(farX, y, shore[i].z);
            uv[i * 2] = new Vector2(f, 0f);
            uv[i * 2 + 1] = new Vector2(f, 1f);
        }
        Mesh m = new Mesh { name = "CourtyardLake" };
        m.vertices = verts;
        m.uv = uv;
        m.triangles = StripTriangles(n);
        return Finish(m);
    }

    private static Mesh RibbonMesh(List<Vector3> left, List<Vector3> right, float y)
    {
        int n = Mathf.Min(left.Count, right.Count);
        Vector3[] verts = new Vector3[n * 2];
        Vector2[] uv = new Vector2[n * 2];
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            verts[i * 2] = new Vector3(left[i].x, y, left[i].z);
            verts[i * 2 + 1] = new Vector3(right[i].x, y, right[i].z);
            uv[i * 2] = new Vector2(f, 0f);
            uv[i * 2 + 1] = new Vector2(f, 1f);
        }
        Mesh m = new Mesh { name = "CourtyardRibbon" };
        m.vertices = verts;
        m.uv = uv;
        m.triangles = StripTriangles(n);
        return Finish(m);
    }

    // A full ring standing on the ground, used for the distant planting band.
    private static Mesh ArcMesh(Vector3 centre, float radius, float yBottom, float yTop, int segments)
    {
        Vector3[] verts = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[(segments + 1) * 2];
        for (int i = 0; i <= segments; i++)
        {
            float f = i / (float)segments;
            float a = f * Mathf.PI * 2f;
            Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            verts[i * 2] = new Vector3(p.x, yBottom, p.z);
            verts[i * 2 + 1] = new Vector3(p.x, yTop, p.z);
            uv[i * 2] = new Vector2(f, 0f);
            uv[i * 2 + 1] = new Vector2(f, 1f);
        }
        Mesh m = new Mesh { name = "CourtyardTreeline" };
        m.vertices = verts;
        m.uv = uv;
        m.triangles = StripTriangles(segments + 1);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m; // vertical, so no upward-facing fix; the shader is double sided
    }

    private static int[] StripTriangles(int n)
    {
        int[] tris = new int[(n - 1) * 6];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
            tris[t++] = a; tris[t++] = b; tris[t++] = c;
            tris[t++] = c; tris[t++] = b; tris[t++] = d;
        }
        return tris;
    }

    // Winding depends on which way the shoreline runs, so rather than reason about it, check the
    // first triangle and flip the whole mesh if it ended up facing down.
    private static Mesh Finish(Mesh m)
    {
        Vector3[] v = m.vertices;
        int[] t = m.triangles;
        if (t.Length >= 3)
        {
            Vector3 normal = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            if (normal.y < 0f)
            {
                for (int i = 0; i < t.Length; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
                m.triangles = t;
            }
        }
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static void AddPath(string name, Transform parent, List<Vector3> centre, float halfWidth, Material material)
    {
        if (centre.Count < 2) return;
        List<Vector3> left = new List<Vector3>(centre.Count);
        List<Vector3> right = new List<Vector3>(centre.Count);
        for (int i = 0; i < centre.Count; i++)
        {
            Vector3 a = centre[Mathf.Max(i - 1, 0)];
            Vector3 b = centre[Mathf.Min(i + 1, centre.Count - 1)];
            Vector3 tangent = (b - a).normalized;
            Vector3 normal = new Vector3(tangent.z, 0f, -tangent.x);
            left.Add(centre[i] - normal * halfWidth);
            right.Add(centre[i] + normal * halfWidth);
        }
        AddMesh(name, parent, RibbonMesh(left, right, PathY), material, 2);
    }

    private static GameObject AddMesh(string name, Transform parent, Mesh mesh, Material material, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.sortingOrder = sortingOrder;
        return go;
    }

    // --- materials ----------------------------------------------------------------------------

    private static Material GroundMaterial(string name, Color a, Color b, float patchScale,
        float patchContrast, float detailScale, float detailStrength, bool featherEdges)
    {
        string path = MaterialFolder + name + ".mat";
        Shader shader = Shader.Find(GroundShaderName);
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }

        m.SetColor("_ColorA", a);
        m.SetColor("_ColorB", b);
        m.SetFloat("_PatchScale", patchScale);
        m.SetFloat("_PatchContrast", patchContrast);
        m.SetFloat("_DetailScale", detailScale);
        m.SetFloat("_DetailStrength", detailStrength);
        m.SetFloat("_DetailFade", 18f);
        m.SetFloat("_PatchFade", 130f);
        // Matches the measured tone at the base of the painted backdrop (~0.32) so the ground
        // reaches the horizon without a visible tonal step.
        m.SetColor("_HazeColor", new Color(0.32f, 0.33f, 0.31f, 1f));
        m.SetFloat("_HazeStart", 45f);
        m.SetFloat("_HazeEnd", 210f);
        m.SetFloat("_HazeMax", 0.9f);

        if (featherEdges)
        {
            m.SetFloat("_EdgeFadeStart", 0f);
            m.SetFloat("_EdgeFadeEnd", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            m.SetFloat("_EdgeFadeStart", 0f);
            m.SetFloat("_EdgeFadeEnd", 0f);
            m.renderQueue = (int)RenderQueue.Geometry;
        }

        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material TreelineMaterial()
    {
        string path = MaterialFolder + "M_Courtyard_Treeline.mat";
        Shader shader = Shader.Find("Osmanthus/DistantTreeline");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = "M_Courtyard_Treeline" };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;

        m.SetColor("_NearColor", new Color(0.19f, 0.23f, 0.17f, 1f));
        m.SetColor("_FarColor", new Color(0.33f, 0.35f, 0.31f, 1f));
        m.SetColor("_HazeColor", new Color(0.40f, 0.42f, 0.39f, 1f));
        m.SetFloat("_HazeAmount", 0.52f);
        m.SetFloat("_Ridge", 0.55f);
        m.SetFloat("_RidgeVariation", 0.45f);
        m.SetFloat("_RidgeScale", 30f);
        m.SetFloat("_Softness", 0.03f);
        m.SetFloat("_BaseFade", 0.10f);
        m.renderQueue = (int)RenderQueue.Transparent - 5; // behind the lake haze cards
        EditorUtility.SetDirty(m);
        return m;
    }

    // The corridor and pavilion floor is a single flat slab of M_Stone_Edge, a near-white stone that
    // the orange key light renders as mud. The scene's 55 joint strips that would have made it read
    // as paving sit at y 0.00-0.02, buried under this floor at y 0.18-0.21, so they never show.
    // Swapping in the procedural paving material gives the walk its slabs back.
    // Every walked surface, not just the inset. The inset is only x +/-0.91 while the floor slab
    // under it runs to +/-1.21, so leaving the border on M_Stone_WarmGrey put a tan mud stripe down
    // each side of the new paving. Same story for the pavilion plinth ring.
    private static bool IsWalkSurface(string name)
    {
        return name.Contains("FloorInset")
            || name == "Left_Floor" || name == "Right_Floor"
            || name == "Pavilion_StonePlinth"
            // The lake end: these are the whole foreground once you walk out to the water, and on
            // M_Complete_StoneLight / M_Complete_Stone under the orange key they read as brown mud.
            || name == "Lake Viewing Terrace" || name == "Stone Jetty"
            || name == "Lake Threshold" || name == "Entrance Threshold"
            || name == "Walkable Stone Foundation" || name == "Walkable Stone Foundation - Lake Turn";
    }

    // Which of the two FBX originals a given surface came from, for the restore path.
    private static bool WasStoneEdge(string name)
    {
        return name.Contains("FloorInset");
    }

    // The lake-end pieces are Prim() cubes built by CompleteCorridorLakeBuilder against project
    // materials, not FBX sub-assets, so they restore separately.
    private static readonly Dictionary<string, string> LakeEndOriginals = new Dictionary<string, string>
    {
        { "Lake Viewing Terrace", "M_Complete_StoneLight" },
        { "Stone Jetty", "M_Complete_Stone" },
        { "Lake Threshold", "M_Complete_StoneLight" },
        { "Entrance Threshold", "M_Complete_StoneLight" },
        { "Walkable Stone Foundation", "M_Complete_Stone" },
        { "Walkable Stone Foundation - Lake Turn", "M_Complete_Stone" },
    };

    private static void PaveCorridorFloor()
    {
        Material paving = PavingMaterial();
        int count = 0;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsWalkSurface(go.name)) continue;
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            if (r == null) continue;
            r.sharedMaterial = paving;
            count++;
        }
        Debug.Log("[Osmanthus] Paved " + count + " walk surface(s) with " + paving.name + ".");
    }

    // Both originals live inside the corridor FBX, so put them back from the model's sub-assets:
    // the insets were M_Stone_Edge, the surrounding floor slabs and plinth M_Stone_WarmGrey.
    private static void RestoreCorridorFloor()
    {
        const string fbx = "Assets/Art/Models/LongCorridor/LongCorridor_Pavilion_Stylized.fbx";
        Material edge = null, warmGrey = null;
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(fbx))
        {
            Material m = sub as Material;
            if (m == null) continue;
            if (m.name == "M_Stone_Edge") edge = m;
            else if (m.name == "M_Stone_WarmGrey") warmGrey = m;
        }
        if (edge == null || warmGrey == null)
        {
            Debug.LogWarning("[Osmanthus] Original floor materials not found in " + fbx);
            return;
        }

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsWalkSurface(go.name)) continue;
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            if (r == null) continue;

            string projectMaterial;
            if (LakeEndOriginals.TryGetValue(go.name, out projectMaterial))
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + projectMaterial + ".mat");
                if (m != null) r.sharedMaterial = m;
            }
            else
            {
                r.sharedMaterial = WasStoneEdge(go.name) ? edge : warmGrey;
            }
        }
    }

    private static Material PavingMaterial()
    {
        string path = MaterialFolder + "M_Corridor_Paving.mat";
        Shader shader = Shader.Find("Osmanthus/StonePaving");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = "M_Corridor_Paving" };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;

        m.SetColor("_StoneA", new Color(0.42f, 0.40f, 0.36f, 1f));
        m.SetColor("_StoneB", new Color(0.58f, 0.55f, 0.49f, 1f));
        m.SetColor("_GroutColor", new Color(0.24f, 0.23f, 0.21f, 1f));
        m.SetFloat("_SlabX", 0.60f);
        m.SetFloat("_SlabZ", 0.90f);
        m.SetFloat("_GroutWidth", 0.035f);
        m.SetFloat("_RunningBond", 1f);
        m.SetFloat("_SlabVariation", 0.55f);
        m.SetFloat("_GrainStrength", 0.10f);
        m.SetFloat("_GrainScale", 14f);
        m.SetFloat("_WearStrength", 0.16f);
        m.SetFloat("_DetailFade", 26f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // The lake gradient was keyed to a plane that started under the viewer's feet. It now starts at
    // the shoreline, so the near/far anchors move out with it.
    private static void RetuneLakeForShore(Material lake)
    {
        if (lake == null || lake.shader == null || lake.shader.name != LakeShaderName) return;
        lake.SetFloat("_GradFront", -19f);
        lake.SetFloat("_GradBack", -62f);

        // With the lake pushed back behind a shoreline it is a much smaller, further band than the
        // sheet that used to start at the viewer's feet. Measured off the terrace render, the
        // reflection column was washing the centre to 0.38 warm grey while the untouched sides sat
        // at 0.28 blue-grey, so it is pulled right back, and the sparkle with it.
        lake.SetColor("_NearColor", new Color(0.19f, 0.25f, 0.28f, 1f));
        lake.SetColor("_FarColor", new Color(0.30f, 0.33f, 0.33f, 1f));
        lake.SetFloat("_ReflectStrength", 0.15f);
        lake.SetFloat("_ReflectWidth", 10f);
        lake.SetFloat("_RippleStrength", 0.05f);
        lake.SetFloat("_GlitterStrength", 0.09f);
        lake.SetFloat("_GlitterScale", 0.8f);
        lake.SetFloat("_CamFadeStart", 16f);
        lake.SetFloat("_CamFadeEnd", 52f);
        EditorUtility.SetDirty(lake);
    }

    // --- scene helpers ------------------------------------------------------------------------

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

    private static void SetActive(string name, bool active)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.name == name) { go.SetActive(active); return; }
    }
}
