using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class StoryLayoutBuilder
{
    private const string MaterialFolder = "Assets/Osmanthus/Materials/";
    private static readonly string[] ScenePaths =
    {
        "Assets/Osmanthus/Scenes/01_Courtyard.unity",
        "Assets/Osmanthus/Scenes/02_Gallery.unity",
        "Assets/Osmanthus/Scenes/03_Observatory.unity"
    };

    private static Material wall;
    private static Material ground;
    private static Material dark;
    private static Material gold;
    private static Material osmanthus;
    private static Material sky;
    private static Material red;
    private static Material glass;
    private static Material lake;
    
    private static Material particle;
private static Material city;

    [MenuItem("Osmanthus/Build Story Layout")]
    public static void BuildStoryLayout()
    {
        LoadMaterials();
        BuildEnteringMemory();
        BuildAwakeningCorridor();
        BuildBetweenWorlds();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(ScenePaths[0], OpenSceneMode.Single);
        Debug.Log("[Osmanthus] Story layouts rebuilt: Entering Memory, Awakening the Corridor, Between Worlds.");
    }

private static void LoadMaterials()
    {
        wall = Load("M_Greybox_Wall");
        ground = Load("M_Greybox_Ground");
        dark = Load("M_Greybox_Dark");
        gold = Load("M_Accent_Gold");
        osmanthus = Load("M_Accent_Osmanthus");
        sky = Load("M_Accent_Sky");
        red = StoryMaterial("M_Memory_Red", new Color(0.34f, 0.075f, 0.055f, 1f), false, 0.22f, 0f);
        glass = StoryMaterial("M_Memory_Glass", new Color(0.82f, 0.68f, 0.42f, 0.26f), true, 0.72f, 0.05f);
        lake = StoryMaterial("M_Lake_Reflection", new Color(0.13f, 0.32f, 0.42f, 0.78f), true, 0.92f, 0.15f);
        city = StoryMaterial("M_City_Silver", new Color(0.19f, 0.25f, 0.29f, 1f), false, 0.55f, 0.1f);
        particle = ParticleMaterial();
    }

    private static Material Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
    }

    private static Material StoryMaterial(string name, Color color, bool transparent, float smoothness, float metallic)
    {
        string path = MaterialFolder + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(wall.shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1f : 0f);

        if (transparent)
        {
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
        else
        {
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform BeginScene(string path, string narrativeName, Vector3 originPosition, Color ambient, Color sunColor, float sunIntensity, out Component teleportTemplate)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject greyObject = GameObject.Find("--- GREYBOX ---");
        if (greyObject == null)
        {
            greyObject = new GameObject("--- GREYBOX ---");
            SceneManager.MoveGameObjectToScene(greyObject, scene);
        }

        teleportTemplate = null;
        Component[] components = greyObject.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == "TeleportationArea")
            {
                teleportTemplate = component;
                break;
            }
        }

        Transform narrative = Group("__NEW_NARRATIVE__", greyObject.transform);
        narrative.name = narrativeName;

        GameObject origin = GameObject.Find("XR Origin (Quest 3)");
        if (origin != null)
        {
            origin.transform.position = originPosition;
            origin.transform.rotation = Quaternion.identity;
        }

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (Light lightComponent in lights)
        {
            if (lightComponent.type != LightType.Directional) continue;
            lightComponent.color = sunColor;
            lightComponent.intensity = sunIntensity;
            lightComponent.shadows = LightShadows.None;
            lightComponent.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.fog = false;
        return narrative;
    }

    private static void FinishScene(Transform narrative)
    {
        Transform grey = narrative.parent;
        List<GameObject> oldChildren = new List<GameObject>();
        for (int i = 0; i < grey.childCount; i++)
        {
            Transform child = grey.GetChild(i);
            if (child != narrative) oldChildren.Add(child.gameObject);
        }

        foreach (GameObject child in oldChildren) Object.DestroyImmediate(child);
        Scene scene = grey.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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

    private static void CopyTeleport(Component source, GameObject target)
    {
        if (source == null) return;
        Component destination = target.AddComponent(source.GetType());
        EditorUtility.CopySerialized(source, destination);
    }

private static void BuildEnteringMemory()
    {
        Component teleportTemplate;
        Transform root = BeginScene(
            ScenePaths[0],
            "01 NARRATIVE - Entering Memory",
            new Vector3(0f, 0f, -8.5f),
            new Color(0.24f, 0.19f, 0.14f),
            new Color(1f, 0.73f, 0.42f),
            1.15f,
            out teleportTemplate);

        Transform teleport = Group("Teleport Path", root);
        GameObject floor = Prim("Teleport Floor - Entering Memory", PrimitiveType.Cube, new Vector3(0f, -0.12f, 3f), new Vector3(9f, 0.24f, 25f), ground, teleport, true);
        CopyTeleport(teleportTemplate, floor);

        Transform threshold = Group("Threshold - Present to Memory", root);
        Prim("Dark Present Left", PrimitiveType.Cube, new Vector3(-3.7f, 1.8f, -7.5f), new Vector3(2.2f, 3.6f, 0.5f), dark, threshold, true);
        Prim("Dark Present Right", PrimitiveType.Cube, new Vector3(3.7f, 1.8f, -7.5f), new Vector3(2.2f, 3.6f, 0.5f), dark, threshold, true);
        Prim("Threshold Column L", PrimitiveType.Cylinder, new Vector3(-2.65f, 2f, -2.2f), new Vector3(0.42f, 2f, 0.42f), red, threshold, true);
        Prim("Threshold Column R", PrimitiveType.Cylinder, new Vector3(2.65f, 2f, -2.2f), new Vector3(0.42f, 2f, 0.42f), red, threshold, true);
        Prim("Threshold Lintel", PrimitiveType.Cube, new Vector3(0f, 4.05f, -2.2f), new Vector3(6.1f, 0.38f, 0.65f), red, threshold, true);
        Prim("Memory Veil", PrimitiveType.Cube, new Vector3(0f, 2f, -1.8f), new Vector3(5.1f, 3.5f, 0.08f), glass, threshold, false);

        Transform corridor = Group("Fragmented Long Corridor", root);
        float[] corridorZ = { 0f, 4f, 8f, 12f };
        for (int i = 0; i < corridorZ.Length; i++)
        {
            float z = corridorZ[i];
            Prim("Corridor Column L " + (i + 1), PrimitiveType.Cylinder, new Vector3(-2.7f, 1.9f, z), new Vector3(0.36f, 1.9f, 0.36f), i == 0 ? gold : red, corridor, true);
            if (i != 2) Prim("Corridor Column R " + (i + 1), PrimitiveType.Cylinder, new Vector3(2.7f, 1.9f, z), new Vector3(0.36f, 1.9f, 0.36f), red, corridor, true);
            if (i != 1) Prim("Broken Roof Beam " + (i + 1), PrimitiveType.Cube, new Vector3(i == 3 ? -0.5f : 0f, 3.82f, z), new Vector3(i == 3 ? 4.8f : 6.1f, 0.3f, 0.45f), red, corridor, true);
        }
        Prim("Long Rail Left", PrimitiveType.Cube, new Vector3(-3.05f, 0.62f, 6f), new Vector3(0.16f, 1.05f, 13f), wall, corridor, true);
        Prim("Broken Rail Right A", PrimitiveType.Cube, new Vector3(3.05f, 0.62f, 2f), new Vector3(0.16f, 1.05f, 5f), wall, corridor, true);
        Prim("Broken Rail Right B", PrimitiveType.Cube, new Vector3(3.05f, 0.62f, 10f), new Vector3(0.16f, 1.05f, 4f), wall, corridor, true);

        Transform fragments = Group("Floating Memory Fragments", root);
        GameObject panel = Prim("Landscape Fragment", PrimitiveType.Cube, new Vector3(-3.65f, 2.25f, 1.4f), new Vector3(1.9f, 2.4f, 0.07f), glass, fragments, false);
        panel.transform.localRotation = Quaternion.Euler(0f, 9f, 0f);
        panel = Prim("Family Image Fragment", PrimitiveType.Cube, new Vector3(3.55f, 2.05f, 4.8f), new Vector3(1.65f, 2.1f, 0.07f), sky, fragments, false);
        panel.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
        panel = Prim("Garden Fragment", PrimitiveType.Cube, new Vector3(-3.45f, 2.55f, 9.4f), new Vector3(2.15f, 2.7f, 0.07f), glass, fragments, false);
        panel.transform.localRotation = Quaternion.Euler(0f, 7f, 0f);
        Prim("Distant Memory Glow", PrimitiveType.Cube, new Vector3(0f, 2.2f, 14.1f), new Vector3(6.5f, 4.2f, 0.08f), glass, fragments, false);

        AddScentTrail(root, "Osmanthus Scent Trail", -7.5f, 15, 1.38f, 0.85f);
        CreateParticles("Scent Guide Particles", root, new Vector3(0f, 0.35f, 3f), true, 150, 16f, 0);
        CreateWorldText("Scene 1 Instruction", "FOLLOW THE OSMANTHUS SCENT", root, new Vector3(0f, 2.1f, -6.4f), 0.06f, new Color(1f, 0.72f, 0.2f));

        Transform tree = Group("Remembered Osmanthus", root);
        Prim("Tree Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 12.8f), new Vector3(0.28f, 1.2f, 0.28f), dark, tree, true);
        Prim("Canopy A", PrimitiveType.Sphere, new Vector3(0f, 3f, 12.8f), new Vector3(1.8f, 1.1f, 1.5f), osmanthus, tree, false);
        Prim("Canopy B", PrimitiveType.Sphere, new Vector3(-1.1f, 2.8f, 12.8f), new Vector3(1f, 0.75f, 1f), osmanthus, tree, false);
        Prim("Canopy C", PrimitiveType.Sphere, new Vector3(1.05f, 2.75f, 12.6f), new Vector3(1f, 0.75f, 1f), osmanthus, tree, false);

        CreatePortal("Scene 1 Exit - Enter Memory", root, new Vector3(0f, 0f, 14f), "02_Gallery", true);
        FinishScene(root);
    }

private static void BuildAwakeningCorridor()
    {
        Component teleportTemplate;
        Transform root = BeginScene(
            ScenePaths[1],
            "02 NARRATIVE - Awakening the Corridor",
            new Vector3(0f, 0f, -14.5f),
            new Color(0.28f, 0.21f, 0.14f),
            new Color(1f, 0.69f, 0.35f),
            1.25f,
            out teleportTemplate);

        Transform teleport = Group("Teleport Path", root);
        GameObject floor = Prim("Teleport Floor - Awakening Corridor", PrimitiveType.Cube, new Vector3(0f, -0.12f, 1f), new Vector3(8f, 0.24f, 36f), ground, teleport, true);
        CopyTeleport(teleportTemplate, floor);

        Transform corridor = Group("Living Long Corridor", root);
        for (int i = 0; i < 8; i++)
        {
            float z = -12f + i * 4f;
            Material columnMaterial = i == 2 || i == 5 ? gold : red;
            bool memoryColumnOnLeft = i == 1 || i == 5;
            bool memoryColumnOnRight = i == 3;
            if (!memoryColumnOnLeft)
            {
                Prim("Painted Column L " + (i + 1), PrimitiveType.Cylinder, new Vector3(-3f, 1.95f, z), new Vector3(0.38f, 1.95f, 0.38f), columnMaterial, corridor, true);
            }
            if (!memoryColumnOnRight)
            {
                Prim("Painted Column R " + (i + 1), PrimitiveType.Cylinder, new Vector3(3f, 1.95f, z), new Vector3(0.38f, 1.95f, 0.38f), columnMaterial, corridor, true);
            }
            Prim("Ceiling Beam " + (i + 1), PrimitiveType.Cube, new Vector3(0f, 3.92f, z), new Vector3(6.7f, 0.34f, 0.5f), red, corridor, true);
        }
        Prim("Corridor Rail Left", PrimitiveType.Cube, new Vector3(-3.45f, 0.6f, 1f), new Vector3(0.16f, 1f, 34f), wall, corridor, true);
        Prim("Corridor Rail Right", PrimitiveType.Cube, new Vector3(3.45f, 0.6f, 1f), new Vector3(0.16f, 1f, 34f), wall, corridor, true);
        Prim("Roof Ribbon Left", PrimitiveType.Cube, new Vector3(-2.05f, 4.05f, 1f), new Vector3(2.2f, 0.12f, 34f), dark, corridor, false);
        Prim("Roof Ribbon Right", PrimitiveType.Cube, new Vector3(2.05f, 4.05f, 1f), new Vector3(2.2f, 0.12f, 34f), dark, corridor, false);

        Transform interactionRoot = Group("DAY 2 - Three Memory Pillars", root);
        GameObject managerObject = new GameObject("MemoryManager");
        managerObject.transform.SetParent(interactionRoot, false);
        MemoryManager manager = managerObject.AddComponent<MemoryManager>();

        float[] pillarZ = { -8f, 0f, 8f };
        float[] pillarX = { -3f, 3f, -3f };
        string[] memoryNames = { "Place", "Everyday Life", "Distance" };
        string[] memoryDescriptions = { "GARDEN / CORRIDOR / LAKE", "FOOTSTEPS / VOICES / HOME", "AIRPORT / CITY / ELSEWHERE" };
        Color[] memoryColors =
        {
            new Color(1f, 0.58f, 0.12f),
            new Color(1f, 0.32f, 0.12f),
            new Color(0.24f, 0.58f, 1f)
        };

        MemoryPillar[] memoryPillars = new MemoryPillar[3];
        Transform feedbackRoot = Group("Memory Planes", interactionRoot);
        for (int i = 0; i < 3; i++)
        {
            float x = pillarX[i];
            float z = pillarZ[i];
            Prim("Memory Column Base " + (i + 1), PrimitiveType.Cylinder, new Vector3(x, 0.1f, z), new Vector3(0.58f, 0.1f, 0.58f), dark, interactionRoot, true);
            GameObject pillar = Prim("Memory Pillar " + (i + 1) + " - " + memoryNames[i], PrimitiveType.Cylinder, new Vector3(x, 1.95f, z), new Vector3(0.46f, 1.95f, 0.46f), red, interactionRoot, true);
            Prim("Memory Halo " + (i + 1), PrimitiveType.Cylinder, new Vector3(x, 0.23f, z), new Vector3(0.68f, 0.025f, 0.68f), gold, interactionRoot, false);

            float panelX = x < 0f ? -4.25f : 4.25f;
            GameObject memoryPlane = Prim(memoryNames[i] + " Memory Plane", PrimitiveType.Cube, new Vector3(panelX, 2.45f, z + 0.8f), new Vector3(2.1f, 2.5f, 0.07f), i == 2 ? sky : glass, feedbackRoot, false);
            memoryPlane.transform.localRotation = Quaternion.Euler(0f, panelX > 0f ? -12f : 12f, 0f);

            ParticleSystem burst = CreateParticles(memoryNames[i] + " Osmanthus Burst", interactionRoot, new Vector3(x, 1.95f, z), false, 90, 0f, 38);
            float plaqueX = x < 0f ? -2.32f : 2.32f;
            CreateWorldText(memoryNames[i] + " Label", (i + 1) + ". " + memoryNames[i].ToUpperInvariant(), interactionRoot, new Vector3(plaqueX, 3.25f, z), 0.048f, memoryColors[i]);
            CreateWorldText(memoryNames[i] + " Description", memoryDescriptions[i], interactionRoot, new Vector3(plaqueX, 2.92f, z), 0.027f, new Color(0.88f, 0.82f, 0.7f));
            TextMesh status = CreateWorldText(memoryNames[i] + " Status", i == 0 ? "SELECT" : "LOCKED", interactionRoot, new Vector3(plaqueX, 2.62f, z), 0.034f, i == 0 ? new Color(1f, 0.78f, 0.3f) : new Color(0.55f, 0.58f, 0.62f));

            MemoryPillar memoryPillar = pillar.AddComponent<MemoryPillar>();
            memoryPillar.Configure(i, memoryNames[i], memoryColors[i], pillar.GetComponent<Renderer>(), memoryPlane, burst, status);
            memoryPillars[i] = memoryPillar;
        }

        AddScentTrail(root, "Osmanthus Scent Trail", -14f, 20, 0.75f, 0.65f);
        CreateParticles("Scent Guide Particles", root, new Vector3(0f, 0.38f, 1f), true, 170, 17f, 0);
        TextMesh instruction = CreateWorldText("Memory Instruction", "SELECT PLACE TO BEGIN", root, new Vector3(0f, 2.25f, -13.25f), 0.06f, new Color(1f, 0.78f, 0.3f));

        Transform merging = Group("First Signs of the Present", root);
        Prim("City Echo 01", PrimitiveType.Cube, new Vector3(5.4f, 2.4f, 9f), new Vector3(1.4f, 4.8f, 1.4f), city, merging, false);
        Prim("City Echo 02", PrimitiveType.Cube, new Vector3(5.8f, 3.1f, 14f), new Vector3(1.7f, 6.2f, 1.7f), sky, merging, false);
        Prim("Garden Echo 01", PrimitiveType.Sphere, new Vector3(-5.2f, 1.4f, 11f), new Vector3(2.2f, 1.2f, 1.7f), osmanthus, merging, false);

        GameObject gate = Prim("Locked Path to the Lake", PrimitiveType.Cube, new Vector3(0f, 1.6f, 17.45f), new Vector3(6.7f, 3.2f, 0.32f), dark, root, true);
        GameObject exitPortal = CreatePortal("Scene 2 Exit - Lake Path", root, new Vector3(0f, 0f, 17.2f), "03_Observatory", false);
        ParticleSystem completion = CreateParticles("All Memories Completion", root, new Vector3(0f, 0.8f, 16.2f), false, 180, 0f, 90);
        manager.Configure(memoryPillars, gate, exitPortal, completion, instruction);

        FinishScene(root);
    }

    private static void BuildBetweenWorlds()
    {
        Component teleportTemplate;
        Transform root = BeginScene(
            ScenePaths[2],
            "03 NARRATIVE - Between Worlds",
            new Vector3(0f, 0f, -12f),
            new Color(0.2f, 0.23f, 0.24f),
            new Color(1f, 0.72f, 0.39f),
            1.2f,
            out teleportTemplate);

        Transform teleport = Group("Teleport Path", root);
        GameObject floor = Prim("Teleport Floor - Corridor Exit", PrimitiveType.Cube, new Vector3(0f, -0.1f, -7f), new Vector3(7f, 0.2f, 12f), ground, teleport, true);
        CopyTeleport(teleportTemplate, floor);
        floor = Prim("Teleport Bridge - Between Worlds", PrimitiveType.Cube, new Vector3(0f, -0.06f, 4.6f), new Vector3(2.5f, 0.18f, 11.5f), wall, teleport, true);
        CopyTeleport(teleportTemplate, floor);
        floor = Prim("Teleport Viewing Deck", PrimitiveType.Cube, new Vector3(0f, 0f, 11.2f), new Vector3(5.5f, 0.25f, 3.5f), ground, teleport, true);
        CopyTeleport(teleportTemplate, floor);

        Transform water = Group("Lake of Two Reflections", root);
        Prim("Kunming Lake Reflection", PrimitiveType.Cube, new Vector3(0f, -0.28f, 8f), new Vector3(24f, 0.12f, 22f), lake, water, false);
        Prim("Final Reflection Circle", PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 11.2f), new Vector3(1.45f, 0.05f, 1.45f), gold, water, false);

        Transform corridor = Group("Long Corridor Opens to Lake", root);
        float[] exitZ = { -11f, -7f, -3f };
        for (int i = 0; i < exitZ.Length; i++)
        {
            float z = exitZ[i];
            Prim("Exit Column L " + (i + 1), PrimitiveType.Cylinder, new Vector3(-2.75f, 1.95f, z), new Vector3(0.38f, 1.95f, 0.38f), red, corridor, true);
            Prim("Exit Column R " + (i + 1), PrimitiveType.Cylinder, new Vector3(2.75f, 1.95f, z), new Vector3(0.38f, 1.95f, 0.38f), red, corridor, true);
            Prim("Exit Beam " + (i + 1), PrimitiveType.Cube, new Vector3(0f, 3.92f, z), new Vector3(6.2f, 0.34f, 0.5f), red, corridor, true);
        }
        Prim("Corridor Rail L", PrimitiveType.Cube, new Vector3(-3.15f, 0.62f, -7f), new Vector3(0.16f, 1.05f, 12f), wall, corridor, true);
        Prim("Corridor Rail R", PrimitiveType.Cube, new Vector3(3.15f, 0.62f, -7f), new Vector3(0.16f, 1.05f, 12f), wall, corridor, true);

        Transform oldWorld = Group("Old Garden Reflection - Left", root);
        Prim("Garden Island", PrimitiveType.Cylinder, new Vector3(-6.4f, -0.05f, 8f), new Vector3(3.2f, 0.24f, 2.5f), osmanthus, oldWorld, false);
        float[] pavilionX = { -7.5f, -5.3f };
        float[] pavilionZ = { 7f, 9f };
        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                Prim("Pavilion Column " + x + z, PrimitiveType.Cylinder, new Vector3(pavilionX[x], 1.45f, pavilionZ[z]), new Vector3(0.22f, 1.45f, 0.22f), red, oldWorld, false);
            }
        }
        Prim("Pavilion Roof Lower", PrimitiveType.Cube, new Vector3(-6.4f, 2.95f, 8f), new Vector3(5f, 0.28f, 4.3f), red, oldWorld, false);
        Prim("Pavilion Roof Upper", PrimitiveType.Cube, new Vector3(-6.4f, 3.3f, 8f), new Vector3(3.2f, 0.22f, 2.6f), gold, oldWorld, false);
        Prim("Garden Tree", PrimitiveType.Sphere, new Vector3(-8.5f, 1.6f, 13f), new Vector3(2.8f, 1.7f, 2.2f), osmanthus, oldWorld, false);

        Transform present = Group("Contemporary City Reflection - Right", root);
        Vector3[] towers =
        {
            new Vector3(5.2f, 2.4f, 3f), new Vector3(7.2f, 3.6f, 5.5f), new Vector3(9.1f, 2.8f, 8f),
            new Vector3(5.7f, 4.5f, 10f), new Vector3(8.1f, 5.3f, 13f), new Vector3(10f, 3.5f, 15f)
        };
        for (int i = 0; i < towers.Length; i++)
        {
            Vector3 p = towers[i];
            Prim("City Tower " + (i + 1).ToString("00"), PrimitiveType.Cube, p, new Vector3(1.5f + (i % 2) * 0.5f, p.y * 2f, 1.5f), i % 3 == 0 ? sky : city, present, false);
        }

        Transform merge = Group("Past Present Merge Planes", root);
        Vector3[] panels =
        {
            new Vector3(-8f, 2.5f, 4f), new Vector3(-4f, 2.9f, 7f), new Vector3(0f, 2.4f, 9f),
            new Vector3(4f, 3.2f, 7f), new Vector3(8f, 2.7f, 10f)
        };
        for (int i = 0; i < panels.Length; i++)
        {
            GameObject panel = Prim("Reflection Fragment " + (i + 1), PrimitiveType.Cube, panels[i], new Vector3(2.2f, 4.6f + i % 2, 0.06f), glass, merge, false);
            panel.transform.localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 8f : -8f, 0f);
        }

        AddScentTrail(root, "Osmanthus Scent Across the Lake", -11f, 18, 1.32f, 0.48f);
        FinishScene(root);
    }

    private static void AddScentTrail(Transform root, string groupName, float startZ, int count, float spacing, float amplitude)
    {
        Transform trail = Group(groupName, root);
        for (int i = 0; i < count; i++)
        {
            float z = startZ + i * spacing;
            float x = Mathf.Sin(i * 0.62f) * amplitude;
            float y = 0.18f + i % 3 * 0.06f;
            Prim("Scent Blossom " + (i + 1).ToString("00"), PrimitiveType.Sphere, new Vector3(x, y, z), new Vector3(0.15f, 0.065f, 0.15f), gold, trail, false);
        }
    }


private static GameObject CreatePortal(string name, Transform parent, Vector3 position, string nextScene, bool activeAtStart)
    {
        Transform portalRoot = Group(name, parent);
        portalRoot.localPosition = position;

        SphereCollider trigger = portalRoot.gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.55f;

        Prim("Portal Halo", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(1.6f, 0.04f, 1.6f), gold, portalRoot, false);
        TextMesh prompt = CreateWorldText("Portal Prompt", "FOLLOW THE SCENT", portalRoot, new Vector3(0f, 2.25f, 0f), 0.055f, new Color(1f, 0.72f, 0.2f));
        SceneTransitionPortal portal = portalRoot.gameObject.AddComponent<SceneTransitionPortal>();
        portal.Configure(nextScene, 1.55f, prompt);
        portalRoot.gameObject.SetActive(activeAtStart);
        return portalRoot.gameObject;
    }


private static ParticleSystem CreateParticles(string name, Transform parent, Vector3 position, bool loop, int maxParticles, float rate, int burstCount)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.duration = 2f;
        main.loop = loop;
        main.playOnAwake = loop;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = loop ? new ParticleSystem.MinMaxCurve(2.5f, 4.5f) : new ParticleSystem.MinMaxCurve(1.1f, 2f);
        main.startSpeed = loop ? new ParticleSystem.MinMaxCurve(0.03f, 0.16f) : new ParticleSystem.MinMaxCurve(0.35f, 1.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.46f, 0.04f, 0.72f), new Color(1f, 0.82f, 0.24f, 1f));
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = loop ? rate : 0f;
        if (!loop && burstCount > 0)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });
        }

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = loop ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Sphere;
        shape.scale = loop ? new Vector3(1.6f, 0.45f, 18f) : new Vector3(0.8f, 1.2f, 0.8f);
        shape.radius = 0.55f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particle;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (!loop) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }


private static TextMesh CreateWorldText(string name, string value, Transform parent, Vector3 position, float size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.identity;
        TextMesh text = go.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 64;
        text.characterSize = size * 0.16f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        return text;
    }


private static Material ParticleMaterial()
    {
        string path = MaterialFolder + "M_Osmanthus_Particle.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (material == null)
        {
            material = new Material(shader != null ? shader : wall.shader) { name = "M_Osmanthus_Particle" };
            AssetDatabase.CreateAsset(material, path);
        }

        Color color = new Color(1f, 0.52f, 0.08f, 0.92f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }
}
