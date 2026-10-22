using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class FlipFallLevelBuilder
{
    private const string ScenePath = "Assets/Scenes/TutorialScene.unity";
    private const string PropDir = "Assets/ThirdParty/PolyhavenProps/";

    // The 4 Master Materials for complete scene continuity (per user request)
    private static Material matWall;          // 1. ONE FOR THE WALL (Concrete042C)
    private static Material matRoomFloor;     // 2. ONE FOR ROOM FLOORS (Concrete044C)
    private static Material matPathwayFloor;  // 3. ONE FOR PATHWAY FLOORS (PavingStones115C)
    private static Material matMetal;         // 4. ONE METALLIC STYLE FOR METALLIC ELEMENTS (Metal062C)

    // Aliases routing directly to the 4 Master Materials
    private static Material matFloor => matRoomFloor;
    private static Material matCeiling => matWall;
    private static Material matShaftWall => matWall;
    private static Material matPillar => matWall;
    private static Material matOldConcrete => matWall;
    private static Material matCraterFloor => matRoomFloor;
    private static Material matCleanSteel => matMetal;
    private static Material matDuctwork => matMetal;

    // Dedicated PBR Materials for the Totaled Crashed Elevator Cab (rich authentic detail)
    private static Material matElevatorSteel;
    private static Material matElevatorInterior;
    private static Material matElevatorTreadPlate;
    private static Material matElevatorDoors;
    private static Material matElevatorCOP;
    private static Material matRebar;

    // Special functional elements & Emissive Fixture Materials
    private static Material matTubeGlow;
    private static Material matBulbGlow;
    private static Material matAmberGlow;
    private static Material matSparkGlow;
    private static Material matRedBeaconGlow;
    private static Material matHazardSign;
    private static Material matMarkerRed;
    private static Material matMarkerCyan;
    private static Material matMarkerGreen;
    private static Material matWarningStripe;
    private static Material matDarkPit;
    private static Material matWood;

    [MenuItem("Tools/FLIP//FALL/Rebuild Tutorial Map")]
    public static void Build()
    {
        Debug.Log("[FlipFall] Rebuilding FLIP//FALL Tutorial Map from scratch (clean environment, NO floating text)...");

        meshCache.Clear();
        InitMaterials();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = new GameObject("TutorialScene");
        Transform rootT = root.transform;

        GameObject envRoot = new GameObject("Environment");
        envRoot.transform.SetParent(rootT, false);

        GameObject ceilingsRoot = new GameObject("Ceilings");
        ceilingsRoot.transform.SetParent(envRoot.transform, false);

        GameObject propsRoot = new GameObject("Props");
        propsRoot.transform.SetParent(rootT, false);

        GameObject lightsRoot = new GameObject("Lighting");
        lightsRoot.transform.SetParent(rootT, false);

        GameObject markersRoot = new GameObject("DevelopmentMarkers");
        markersRoot.transform.SetParent(rootT, false);

        // Dark underground horror facility atmosphere: calibrated ambient toe and zero sky reflection
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.052f, 0.055f, 0.062f);
        RenderSettings.reflectionIntensity = 0.0f;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;

        // Global Post-Processing Volume with ACES tonemapping and horror grading
        SetupGlobalVolume(lightsRoot.transform);

        // Build all 8 areas in sequence (NO floating text)
        BuildArea1_CrashRoom(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform);
        BuildArea2_PitCorridor(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform, markersRoot.transform);
        BuildArea3_ApproachHallway(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform);
        BuildArea4_HiddenDoorChamber(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform, markersRoot.transform);
        BuildArea5_CreatureRoom(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform);
        BuildArea6_ConnectingHallway(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform);
        BuildArea7_DarkFuseRoom(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform, markersRoot.transform);
        BuildArea8_ExitElevatorRoom(envRoot.transform, ceilingsRoot.transform, propsRoot.transform, lightsRoot.transform);

        // Cylindrical Character Controller for map playtesting
        BuildPlayerCharacter(rootT);

        // Overview Cameras (Perspective and Orthographic top-down)
        BuildOverviewCameras(rootT);

        // Bind high-resolution PBR textures to all environmental props in the scene
        PropTextureBinder.BindAll();

        // Save generated box meshes with world-unit UVs as project asset
        SaveMeshesToAsset();

        // Save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log("[FlipFall] Tutorial Map rebuild complete! Saved -> " + ScenePath);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/FLIP//FALL/Toggle Ceilings")]
    public static void ToggleCeilings()
    {
        GameObject ceilings = GameObject.Find("TutorialScene/Environment/Ceilings");
        if (ceilings != null)
        {
            ceilings.SetActive(!ceilings.activeSelf);
            Debug.Log("[FlipFall] Ceilings active: " + ceilings.activeSelf);
            SceneView.RepaintAll();
        }
    }

    [MenuItem("FLIP//FALL/Fix All Overlapping Textures and Pit Edges")]
    public static void FixOverlappingTexturesAndPitEdges()
    {
        Debug.Log("[FlipFall] Adjusting all pit edges, drop walls, and area junction floors to eliminate overlapping textures and z-fighting...");

        InitMaterials();

        // 1. AREA 1 FLOOR (meets Area 2 at X = 13.0f with 0 overlap)
        UpdateBoxInScene("01_CrashRoom/Floor", new Vector3(-0.5f, -0.25f, 50f), new Vector3(27f, 0.5f, 28f), matRoomFloor);

        // 2. AREA 2 PIT WALKWAYS & DROP WALLS
        // Walkway 0: [13.0, 20.0]
        UpdateBoxInScene("Floor_Walkway_0", new Vector3(16.5f, -0.25f, 50f), new Vector3(7.0f, 0.5f, 8.2f), matPathwayFloor);
        // Pit 1: [20.0, 25.0], Drop tops at Y = -0.50m (flush under 50cm floor slab)
        UpdateBoxInScene("Pit_Drop_20W", new Vector3(19.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit_Drop_25E", new Vector3(25.2f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit1_GirderWalkway", new Vector3(22.5f, -0.1f, 52.6f), new Vector3(5.0f, 0.3f, 1.4f), matMetal);

        // Walkway 1: [25.0, 32.0]
        UpdateBoxInScene("Floor_Walkway_1", new Vector3(28.5f, -0.25f, 50f), new Vector3(7.0f, 0.5f, 8.2f), matPathwayFloor);
        // Pit 2: [32.0, 37.0], Drop tops at Y = -0.50m
        UpdateBoxInScene("Pit_Drop_32W", new Vector3(31.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit_Drop_37E", new Vector3(37.2f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit2_SteppingIsland", new Vector3(34.5f, -0.25f, 50f), new Vector3(2.2f, 0.5f, 2.2f), matPathwayFloor);

        // Walkway 2: [37.0, 44.0]
        UpdateBoxInScene("Floor_Walkway_2", new Vector3(40.5f, -0.25f, 50f), new Vector3(7.0f, 0.5f, 8.2f), matPathwayFloor);
        // Pit 3: [44.0, 49.0], Drop tops at Y = -0.50m
        UpdateBoxInScene("Pit_Drop_44W", new Vector3(43.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit_Drop_49E", new Vector3(49.2f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit3_LedgeWalkway", new Vector3(46.5f, -0.25f, 47.4f), new Vector3(5.0f, 0.5f, 1.5f), matPathwayFloor);

        // Walkway 3: [49.0, 55.0]
        UpdateBoxInScene("Floor_Walkway_3", new Vector3(52f, -0.25f, 50f), new Vector3(6.0f, 0.5f, 8.2f), matPathwayFloor);
        // Hidden Pit: [55.0, 60.5], Drop tops at Y = -0.50m
        UpdateBoxInScene("Pit_Drop_55W", new Vector3(54.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        UpdateBoxInScene("Pit_Drop_60E", new Vector3(60.7f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);

        // Final Walkway: [60.5, 67.0]
        UpdateBoxInScene("Floor_Walkway_Final", new Vector3(63.75f, -0.25f, 50f), new Vector3(6.5f, 0.5f, 8.2f), matPathwayFloor, topUvOffsetU: 5.725f);

        // 3. AREA 3 & AREA 4 FLOORS (exact flush junctions)
        UpdateBoxInScene("03_ApproachHallway/Floor", new Vector3(77f, -0.25f, 50f), new Vector3(20.0f, 0.5f, 7.2f), matPathwayFloor);
        UpdateBoxInScene("04_HiddenDoorSection/Floor", new Vector3(93.25f, -0.25f, 50f), new Vector3(12.5f, 0.5f, 13.5f), matRoomFloor);

        // Save generated box meshes with world-unit UVs as project asset
        SaveMeshesToAsset();

        // Mark active scene dirty and save
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log("[FlipFall] All pit edges, drop walls, and area junction floors updated with 0 overlapping textures and 0 z-fighting!");
        SceneView.RepaintAll();
    }

    private static void UpdateBoxInScene(string objectPathOrName, Vector3 worldPos, Vector3 size, Material mat, float uvScale = 0.5f, float topUvOffsetU = 0f)
    {
        GameObject go = GameObject.Find(objectPathOrName);
        if (go == null && objectPathOrName.Contains("/"))
        {
            string shortName = objectPathOrName.Substring(objectPathOrName.LastIndexOf('/') + 1);
            go = GameObject.Find(shortName);
        }
        if (go == null)
        {
            Debug.LogWarning($"[FlipFall] UpdateBoxInScene: Object not found: {objectPathOrName}");
            return;
        }

        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateBoxMesh(size, uvScale, topUvOffsetU);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        BoxCollider bc = go.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.size = size;
            bc.center = Vector3.zero;
        }

        EditorUtility.SetDirty(go);
    }

    private static void SetupGlobalVolume(Transform parent)
    {
        GameObject volGo = GameObject.Find("Global_PostProcessing_Volume");
        if (volGo == null)
        {
            volGo = new GameObject("Global_PostProcessing_Volume");
            volGo.transform.SetParent(parent, false);
            var v = volGo.AddComponent<UnityEngine.Rendering.Volume>();
            v.isGlobal = true;
            v.weight = 1.0f;
        }

        var volume = volGo.GetComponent<UnityEngine.Rendering.Volume>();
        string profPath = "Assets/Settings/Horror_VolumeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            if (!System.IO.Directory.Exists("Assets/Settings"))
            {
                System.IO.Directory.CreateDirectory("Assets/Settings");
            }
            AssetDatabase.CreateAsset(profile, profPath);

            var tonemapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.ACES;

            var colorAdj = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 16.0f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = -4.0f;
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = 0.10f;

            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.color.overrideState = true;
            vignette.color.value = Color.black;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.24f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;

            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.40f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.70f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        if (volume != null)
        {
            volume.sharedProfile = profile;
        }

        var terrorComp = volGo.GetComponent<MonsterTerrorScreenEffect>();
        if (terrorComp == null) terrorComp = volGo.AddComponent<MonsterTerrorScreenEffect>();
        terrorComp.maxTerrorRange = 18.0f;
        terrorComp.criticalTerrorRange = 5.0f;
        terrorComp.fadeInSpeed = 0.75f;
        terrorComp.fadeOutSpeed = 0.85f;
    }

    // =========================================================================
    // MATERIALS
    // =========================================================================
    private static void InitMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. ONE FOR THE WALL (Architectural concrete for all walls, columns, shaft, ceilings) - Dry matte finish
        matWall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_Master_Wall.mat");
        if (matWall == null) matWall = CreateMaterial(urpLit, "PBR_Master_Wall", new Color(0.60f, 0.60f, 0.60f), 0.08f, 0.0f);
        if (matWall != null)
        {
            matWall.SetFloat("_Smoothness", 0.08f);
            matWall.SetFloat("_EnvironmentReflections", 0.0f);
            EditorUtility.SetDirty(matWall);
        }
        ResetMaterialScale(matWall);

        // 2. ONE FOR ROOM FLOORS (Cast concrete slab floor for all room interiors) - Dry matte finish
        matRoomFloor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_Master_RoomFloor.mat");
        if (matRoomFloor == null) matRoomFloor = CreateMaterial(urpLit, "PBR_Master_RoomFloor", new Color(0.55f, 0.55f, 0.55f), 0.08f, 0.0f);
        if (matRoomFloor != null)
        {
            matRoomFloor.SetFloat("_Smoothness", 0.08f);
            matRoomFloor.SetFloat("_EnvironmentReflections", 0.0f);
            EditorUtility.SetDirty(matRoomFloor);
        }
        ResetMaterialScale(matRoomFloor);

        // 3. ONE FOR PATHWAY FLOORS (Paved stone slab walkway for all corridors, hallways and walkways) - Dry matte finish
        matPathwayFloor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_Master_PathwayFloor.mat");
        if (matPathwayFloor == null) matPathwayFloor = CreateMaterial(urpLit, "PBR_Master_PathwayFloor", new Color(0.50f, 0.50f, 0.50f), 0.08f, 0.0f);
        if (matPathwayFloor != null)
        {
            matPathwayFloor.SetFloat("_Smoothness", 0.08f);
            matPathwayFloor.SetFloat("_EnvironmentReflections", 0.0f);
            EditorUtility.SetDirty(matPathwayFloor);
        }
        ResetMaterialScale(matPathwayFloor);

        // 4. ONE METALLIC STYLE FOR METALLIC ELEMENTS (Structural steel for beams, troffers, rebar)
        matMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_Master_Metal.mat");
        if (matMetal == null) matMetal = CreateMaterial(urpLit, "PBR_Master_Metal", new Color(0.45f, 0.47f, 0.50f), 0.50f, 0.90f);
        ResetMaterialScale(matMetal);

        // Dedicated PBR Textured Materials for the Totaled Crashed Elevator Cab (rich authentic detail)
        matElevatorSteel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_ElevatorSteel.mat");
        if (matElevatorSteel == null) matElevatorSteel = CreateMaterial(urpLit, "Mat_ElevatorSteel", new Color(0.45f, 0.47f, 0.50f), 0.50f, 0.85f);

        matElevatorInterior = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_ElevatorInterior.mat");
        if (matElevatorInterior == null) matElevatorInterior = CreateMaterial(urpLit, "Mat_ElevatorInterior", new Color(0.30f, 0.32f, 0.35f), 0.40f, 0.70f);

        matElevatorTreadPlate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_ElevatorTreadPlate.mat");
        if (matElevatorTreadPlate == null) matElevatorTreadPlate = matElevatorSteel;

        matElevatorDoors = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_ElevatorDoors.mat");
        if (matElevatorDoors == null) matElevatorDoors = matElevatorSteel;

        matElevatorCOP = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_ElevatorCOP.mat");
        if (matElevatorCOP == null) matElevatorCOP = matElevatorSteel;

        matRebar = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_RebarRibbed.mat");
        if (matRebar == null) matRebar = matMetal;

        // Functional Lighting & Signage
        matTubeGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_FluorescentTube_Glow.mat");
        if (matTubeGlow == null)
        {
            matTubeGlow = CreateMaterial(urpLit, "PBR_FluorescentTube_Glow", new Color(0.92f, 0.96f, 1.0f), 0.95f, 0.0f, new Color(0.92f, 0.96f, 1.0f) * 3.8f);
        }

        matBulbGlow = CreateMaterial(urpLit, "Mat_BulbGlow", new Color(1.0f, 0.92f, 0.75f), 0.95f, 0.0f, new Color(1.0f, 0.90f, 0.72f) * 3.5f);
        matAmberGlow = CreateMaterial(urpLit, "Mat_AmberGlow", new Color(1.0f, 0.50f, 0.10f), 0.95f, 0.0f, new Color(1.0f, 0.45f, 0.08f) * 4.0f);
        matSparkGlow = CreateMaterial(urpLit, "Mat_SparkGlow", new Color(0.75f, 0.90f, 1.0f), 0.98f, 0.0f, new Color(0.70f, 0.88f, 1.0f) * 5.0f);
        matRedBeaconGlow = CreateMaterial(urpLit, "Mat_RedBeaconGlow", new Color(1.0f, 0.15f, 0.10f), 0.95f, 0.0f, new Color(1.0f, 0.12f, 0.08f) * 4.0f);

        matHazardSign = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_User_HazardSign.mat");

        // Emissive Markers for Development (Physical frames, NO text)
        matMarkerRed = CreateMaterial(urpLit, "Mat_MarkerRed", new Color(1.0f, 0.08f, 0.08f), 0.6f, 0.0f, new Color(1.0f, 0.1f, 0.1f) * 3.5f);
        matMarkerCyan = CreateMaterial(urpLit, "Mat_MarkerCyan", new Color(0.05f, 0.85f, 1.0f), 0.6f, 0.0f, new Color(0.05f, 0.85f, 1.0f) * 3.5f);
        matMarkerGreen = CreateMaterial(urpLit, "Mat_MarkerGreen", new Color(0.1f, 1.0f, 0.25f), 0.6f, 0.0f, new Color(0.1f, 1.0f, 0.25f) * 3.5f);

        matWarningStripe = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_HazardWarning.mat");
        if (matWarningStripe == null)
        {
            matWarningStripe = CreateMaterial(urpLit, "Mat_WarningStripe", new Color(0.95f, 0.75f, 0.1f), 0.3f, 0.0f);
        }

        matDarkPit = CreateMaterial(urpLit, "Mat_DarkPit", new Color(0.015f, 0.015f, 0.02f), 0.05f, 0.0f);
        matWood = CreateMaterial(urpLit, "Mat_InstitutionalWood", new Color(0.42f, 0.30f, 0.20f), 0.40f, 0.0f);
    }

    private static Material CreateMaterial(Shader shader, string name, Color color, float smoothness, float metallic, Color? emission = null)
    {
        string dir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        string path = dir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader && shader != null)
        {
            m.shader = shader;
        }
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void ResetMaterialScale(Material mat)
    {
        if (mat == null) return;
        mat.mainTextureScale = Vector2.one;
        mat.mainTextureOffset = Vector2.zero;
        if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", Vector2.one);
        if (mat.HasProperty("_BumpMap")) mat.SetTextureScale("_BumpMap", Vector2.one);
        if (mat.HasProperty("_MetallicGlossMap")) mat.SetTextureScale("_MetallicGlossMap", Vector2.one);
        if (mat.HasProperty("_OcclusionMap")) mat.SetTextureScale("_OcclusionMap", Vector2.one);
        EditorUtility.SetDirty(mat);
    }

    // =========================================================================
    // AREA 1: BROKEN ELEVATOR ROOM (TOTALED CAB, CRACKED CEILING, DYNAMIC LIGHTING)
    // =========================================================================
    private static void BuildArea1_CrashRoom(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent)
    {
        GameObject area = new GameObject("01_CrashRoom");
        area.transform.SetParent(envParent, false);

        float minX = -13f, maxX = 13f;
        float minZ = 37f, maxZ = 63f;
        float height = 5.5f;

        // 1. FLOOR (ONE FOR ROOM FLOORS: PBR_Master_RoomFloor) - aligns flush with Area 2 walkway at X = 13.0f
        CreateBox(area.transform, "Floor", new Vector3(-0.5f, -0.25f, 50f), new Vector3(27f, 0.5f, 28f), matRoomFloor);

        // Perimeter concrete plinths/curbs (ONE FOR THE WALL: PBR_Master_Wall) - cleanly framed between structural pilasters
        CreateBox(area.transform, "Curb_North_West", new Vector3(-10.2f, 0.175f, maxZ - 0.145f), new Vector3(5.0f, 0.35f, 0.28f), matWall);
        CreateBox(area.transform, "Curb_North_Mid", new Vector3(0f, 0.175f, maxZ - 0.145f), new Vector3(12.6f, 0.35f, 0.28f), matWall);
        CreateBox(area.transform, "Curb_North_East", new Vector3(9.5f, 0.175f, maxZ - 0.145f), new Vector3(3.6f, 0.35f, 0.28f), matWall);

        CreateBox(area.transform, "Curb_South_West", new Vector3(-10.2f, 0.175f, minZ + 0.145f), new Vector3(5.0f, 0.35f, 0.28f), matWall);
        CreateBox(area.transform, "Curb_South_Mid", new Vector3(0f, 0.175f, minZ + 0.145f), new Vector3(12.6f, 0.35f, 0.28f), matWall);
        CreateBox(area.transform, "Curb_South_East", new Vector3(9.5f, 0.175f, minZ + 0.145f), new Vector3(3.6f, 0.35f, 0.28f), matWall);

        CreateBox(area.transform, "Curb_West_A", new Vector3(minX + 0.145f, 0.175f, 40.2f), new Vector3(0.28f, 0.35f, 6.0f), matWall);
        CreateBox(area.transform, "Curb_West_Mid", new Vector3(minX + 0.145f, 0.175f, 50.0f), new Vector3(0.28f, 0.35f, 10.4f), matWall);
        CreateBox(area.transform, "Curb_West_B", new Vector3(minX + 0.145f, 0.175f, 59.8f), new Vector3(0.28f, 0.35f, 6.0f), matWall);

        // 2. WALLS & ARCHITECTURAL PILASTERS (ONE FOR THE WALL: PBR_Master_Wall) - overlapping corner joints
        CreateBox(area.transform, "Wall_West", new Vector3(minX - 0.25f, height * 0.5f, 50f), new Vector3(0.5f, height, 26f), matWall);
        CreateBox(area.transform, "Wall_North", new Vector3(0f, height * 0.5f, maxZ + 0.25f), new Vector3(28f, height, 0.5f), matWall);
        CreateBox(area.transform, "Wall_South", new Vector3(0f, height * 0.5f, minZ - 0.25f), new Vector3(28f, height, 0.5f), matWall);

        // Heavy Concrete Structural Columns / Buttresses
        CreateBox(area.transform, "Column_North_West", new Vector3(-7f, height * 0.5f, maxZ - 0.35f), new Vector3(1.4f, height, 0.7f), matWall);
        CreateBox(area.transform, "Column_North_East", new Vector3(7f, height * 0.5f, maxZ - 0.35f), new Vector3(1.4f, height, 0.7f), matWall);
        CreateBox(area.transform, "Column_South_West", new Vector3(-7f, height * 0.5f, minZ + 0.35f), new Vector3(1.4f, height, 0.7f), matWall);
        CreateBox(area.transform, "Column_South_East", new Vector3(7f, height * 0.5f, minZ + 0.35f), new Vector3(1.4f, height, 0.7f), matWall);
        CreateBox(area.transform, "Column_West_A", new Vector3(minX + 0.35f, height * 0.5f, 44f), new Vector3(0.7f, height, 1.4f), matWall);
        CreateBox(area.transform, "Column_West_B", new Vector3(minX + 0.35f, height * 0.5f, 56f), new Vector3(0.7f, height, 1.4f), matWall);

        // Ceiling Overhead Support Girders (ONE METALLIC STYLE: PBR_Master_Metal)
        CreateBox(ceilParent, "Girder_Shaft_North", new Vector3(0f, height - 0.25f, 53.0f), new Vector3(26f, 0.5f, 0.6f), matMetal);
        CreateBox(ceilParent, "Girder_Shaft_South", new Vector3(0f, height - 0.25f, 47.0f), new Vector3(26f, 0.5f, 0.6f), matMetal);

        // Upper Wall HVAC Ductwork (ONE METALLIC STYLE: PBR_Master_Metal)
        CreateBox(area.transform, "HVAC_Main_Duct", new Vector3(0f, 4.8f, maxZ - 0.5f), new Vector3(24f, 0.6f, 0.8f), matMetal);
        CreateBox(area.transform, "HVAC_Vent_Drop1", new Vector3(-4f, 4.3f, maxZ - 0.5f), new Vector3(0.5f, 0.6f, 0.5f), matMetal);
        CreateBox(area.transform, "HVAC_Vent_Drop2", new Vector3(4f, 4.3f, maxZ - 0.5f), new Vector3(0.5f, 0.6f, 0.5f), matMetal);

        // East wall Doorway to Pit Corridor: Doorway width 6.5m (Z: 46.75 to 53.25), height 4.2m
        CreateBox(area.transform, "Wall_East_South", new Vector3(maxX + 0.25f, height * 0.5f, 41.5f), new Vector3(0.5f, height, 10.5f), matWall);
        CreateBox(area.transform, "Wall_East_North", new Vector3(maxX + 0.25f, height * 0.5f, 58.5f), new Vector3(0.5f, height, 10.5f), matWall);
        CreateBox(area.transform, "Wall_East_Header", new Vector3(maxX + 0.25f, 4.2f + (height - 4.2f) * 0.5f, 50f), new Vector3(0.5f, height - 4.2f, 6.5f), matWall);

        // Reinforced Industrial Bulkhead Portal framing the exit doorway (ONE METALLIC STYLE: PBR_Master_Metal)
        CreateBox(area.transform, "Bulkhead_Left", new Vector3(maxX - 0.20f, 2.1f, 46.6f), new Vector3(0.4f, 4.2f, 0.4f), matMetal);
        CreateBox(area.transform, "Bulkhead_Right", new Vector3(maxX - 0.20f, 2.1f, 53.4f), new Vector3(0.4f, 4.2f, 0.4f), matMetal);
        CreateBox(area.transform, "Bulkhead_Lintel", new Vector3(maxX - 0.20f, 4.4f, 50f), new Vector3(0.4f, 0.4f, 7.2f), matMetal);

        // 3. ENCLOSED VERTICAL ELEVATOR SHAFT (ONE FOR THE WALL: PBR_Master_Wall)
        GameObject shaft = new GameObject("ElevatorShaft_Vertical");
        shaft.transform.SetParent(ceilParent, false);

        float shaftMinY = height;
        float shaftMaxY = 16.0f;
        float shaftH = shaftMaxY - shaftMinY;
        float shaftMidY = shaftMinY + shaftH * 0.5f;

        // Dark enclosed shaft walls with master wall concrete - overlapping corner joints
        CreateBox(shaft.transform, "Shaft_Wall_West", new Vector3(-2.8f, shaftMidY, 50f), new Vector3(0.4f, shaftH, 6.0f), matWall);
        CreateBox(shaft.transform, "Shaft_Wall_East", new Vector3(2.8f, shaftMidY, 50f), new Vector3(0.4f, shaftH, 6.0f), matWall);
        CreateBox(shaft.transform, "Shaft_Wall_North", new Vector3(0f, shaftMidY, 52.8f), new Vector3(6.0f, shaftH, 0.4f), matWall);
        CreateBox(shaft.transform, "Shaft_Wall_South", new Vector3(0f, shaftMidY, 47.2f), new Vector3(6.0f, shaftH, 0.4f), matWall);
        CreateBox(shaft.transform, "Shaft_Roof_Cap", new Vector3(0f, shaftMaxY + 0.2f, 50f), new Vector3(6.6f, 0.4f, 6.6f), matWall);

        // Structural I-Beam Ring Braces running around the shaft perimeter at 3 elevation tiers (ONE METALLIC STYLE)
        float[] ringY = new float[] { 8.5f, 11.5f, 14.5f };
        for (int r = 0; r < ringY.Length; r++)
        {
            float ry = ringY[r];
            CreateBox(shaft.transform, $"Shaft_RingBeam_N_{r}", new Vector3(0f, ry, 52.5f), new Vector3(5.2f, 0.25f, 0.25f), matMetal);
            CreateBox(shaft.transform, $"Shaft_RingBeam_S_{r}", new Vector3(0f, ry, 47.5f), new Vector3(5.2f, 0.25f, 0.25f), matMetal);
            CreateBox(shaft.transform, $"Shaft_RingBeam_W_{r}", new Vector3(-2.5f, ry, 50.0f), new Vector3(0.25f, 0.25f, 4.75f), matMetal);
            CreateBox(shaft.transform, $"Shaft_RingBeam_E_{r}", new Vector3(2.5f, ry, 50.0f), new Vector3(0.25f, 0.25f, 4.75f), matMetal);
        }

        // Shaft Top Machinery: Overhead Winch I-Beam Bed & Sheaves (ONE METALLIC STYLE)
        CreateBox(shaft.transform, "Shaft_Motor_Beam", new Vector3(0f, 15.2f, 50f), new Vector3(0.4f, 0.6f, 5.2f), matMetal);
        CreateCylinderRotated(shaft.transform, "Sheave_Left", new Vector3(0f, 14.8f, 49.3f), new Vector3(0.8f, 0.15f, 0.8f), Quaternion.Euler(0, 0, 90), matMetal);
        CreateCylinderRotated(shaft.transform, "Sheave_Right", new Vector3(0f, 14.8f, 50.7f), new Vector3(0.8f, 0.15f, 0.8f), Quaternion.Euler(0, 0, 90), matMetal);

        // Vertical Steel Guide T-Rails running down both sides of the shaft (ONE METALLIC STYLE)
        CreateBox(shaft.transform, "GuideRail_West", new Vector3(-2.55f, shaftMidY, 50f), new Vector3(0.12f, shaftH, 0.3f), matMetal);
        CreateBox(shaft.transform, "GuideRail_East", new Vector3(2.55f, shaftMidY, 50f), new Vector3(0.12f, shaftH, 0.3f), matMetal);

        // 4 Steel Hoist Cables descending from the overhead winch (ONE METALLIC STYLE)
        CreateCylinder(shaft.transform, "HoistCable_1", new Vector3(-0.4f, 9.8f, 49.6f), new Vector3(0.05f, 5.4f, 0.05f), matMetal);
        CreateCylinder(shaft.transform, "HoistCable_2", new Vector3(0.4f, 9.5f, 50.4f), new Vector3(0.05f, 5.7f, 0.05f), matMetal);
        // Snapped cables hanging frayed
        CreateCylinder(shaft.transform, "SnappedCable_A", new Vector3(-0.8f, 13.0f, 50.0f), new Vector3(0.04f, 2.2f, 0.04f), matMetal);
        CreateCylinder(shaft.transform, "SnappedCable_B", new Vector3(0.8f, 12.0f, 50.0f), new Vector3(0.04f, 3.2f, 0.04f), matMetal);

        // 4. CRACKED, FRACTURED CEILING SLABS & SHAFT OPENING BORDER
        GameObject crackedCeil = new GameObject("CrackedCeiling_Destruction");
        crackedCeil.transform.SetParent(ceilParent, false);

        // Surrounding solid ceiling panels framing the shaft opening precisely (ONE FOR THE WALL: PBR_Master_Wall) - overlapping exterior walls
        CreateBox(crackedCeil.transform, "Ceil_West", new Vector3(-8.25f, height + 0.15f, 50f), new Vector3(11.0f, 0.3f, 28f), matWall);
        CreateBox(crackedCeil.transform, "Ceil_East", new Vector3(8.25f, height + 0.15f, 50f), new Vector3(11.0f, 0.3f, 28f), matWall);
        CreateBox(crackedCeil.transform, "Ceil_North", new Vector3(0f, height + 0.15f, 58.5f), new Vector3(6.0f, 0.3f, 11.5f), matWall);
        CreateBox(crackedCeil.transform, "Ceil_South", new Vector3(0f, height + 0.15f, 41.5f), new Vector3(6.0f, 0.3f, 11.5f), matWall);

        // Ceiling Joists / Secondary Support Beams crossing the ceiling (ONE METALLIC STYLE: PBR_Master_Metal)
        CreateBox(crackedCeil.transform, "Ceil_Joist_West1", new Vector3(-7.84f, height - 0.14f, 43.0f), new Vector3(10.28f, 0.22f, 0.35f), matMetal);
        CreateBox(crackedCeil.transform, "Ceil_Joist_West2", new Vector3(-7.84f, height - 0.14f, 57.0f), new Vector3(10.28f, 0.22f, 0.35f), matMetal);
        CreateBox(crackedCeil.transform, "Ceil_Joist_East1", new Vector3(7.84f, height - 0.14f, 43.0f), new Vector3(10.28f, 0.22f, 0.35f), matMetal);
        CreateBox(crackedCeil.transform, "Ceil_Joist_East2", new Vector3(7.84f, height - 0.14f, 57.0f), new Vector3(10.28f, 0.22f, 0.35f), matMetal);

        // Heavy Reinforced Steel Border Channels framing the shaft opening perimeter (ONE METALLIC STYLE: PBR_Master_Metal)
        CreateBox(crackedCeil.transform, "Shaft_Border_North", new Vector3(0f, height + 0.05f, 52.85f), new Vector3(5.4f, 0.30f, 0.30f), matMetal);
        CreateBox(crackedCeil.transform, "Shaft_Border_South", new Vector3(0f, height + 0.05f, 47.15f), new Vector3(5.4f, 0.30f, 0.30f), matMetal);
        CreateBox(crackedCeil.transform, "Shaft_Border_West", new Vector3(-2.85f, height + 0.05f, 50.0f), new Vector3(0.30f, 0.30f, 5.4f), matMetal);
        CreateBox(crackedCeil.transform, "Shaft_Border_East", new Vector3(2.85f, height + 0.05f, 50.0f), new Vector3(0.30f, 0.30f, 5.4f), matMetal);

        // Jagged downward-tilted ceiling fracture slabs along the opening perimeter (ONE FOR THE WALL: PBR_Master_Wall)
        CreateBoxRotated(crackedCeil.transform, "FractureSlab_NW", new Vector3(-1.8f, height - 0.25f, 52.2f), new Vector3(1.6f, 0.25f, 1.2f), Quaternion.Euler(24f, -15f, -18f), matWall);
        CreateBoxRotated(crackedCeil.transform, "FractureSlab_NE", new Vector3(1.9f, height - 0.30f, 52.3f), new Vector3(1.5f, 0.25f, 1.3f), Quaternion.Euler(30f, 12f, 14f), matWall);
        CreateBoxRotated(crackedCeil.transform, "FractureSlab_SW", new Vector3(-1.9f, height - 0.28f, 47.7f), new Vector3(1.7f, 0.25f, 1.1f), Quaternion.Euler(-26f, 10f, -20f), matWall);
        CreateBoxRotated(crackedCeil.transform, "FractureSlab_SE", new Vector3(1.8f, height - 0.22f, 47.6f), new Vector3(1.4f, 0.25f, 1.2f), Quaternion.Euler(-22f, -12f, 16f), matWall);
        CreateBoxRotated(crackedCeil.transform, "FractureSlab_MidW", new Vector3(-2.4f, height - 0.35f, 50.0f), new Vector3(1.1f, 0.22f, 1.8f), Quaternion.Euler(6f, 0f, -32f), matWall);
        CreateBoxRotated(crackedCeil.transform, "FractureSlab_MidE", new Vector3(2.4f, height - 0.32f, 50.0f), new Vector3(1.0f, 0.22f, 1.9f), Quaternion.Euler(-4f, 0f, 28f), matWall);

        // Exposed bent reinforcement rebar rods protruding down from the jagged ceiling
        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f * Mathf.Deg2Rad;
            float r = 2.4f + (i % 3) * 0.25f;
            Vector3 rebarBase = new Vector3(Mathf.Cos(ang) * r, height - 0.1f, 50f + Mathf.Sin(ang) * r);
            Quaternion rebarRot = Quaternion.Euler(15f + (i % 4) * 8f, i * 30f, 25f + (i % 3) * 10f);
            CreateCylinderRotated(crackedCeil.transform, "Bent_Rebar_" + i, rebarBase + new Vector3(0, -0.4f, 0), new Vector3(0.035f, 0.8f, 0.035f), rebarRot, matRebar);
        }

        // Dangling electrical conduits & flex hoses (ONE METALLIC STYLE)
        CreateCylinderRotated(crackedCeil.transform, "Dangling_Conduit_1", new Vector3(-1.2f, height - 0.9f, 52.4f), new Vector3(0.04f, 1.8f, 0.04f), Quaternion.Euler(12f, 15f, -8f), matMetal);
        CreateCylinderRotated(crackedCeil.transform, "Dangling_Conduit_2", new Vector3(1.5f, height - 0.8f, 47.5f), new Vector3(0.04f, 1.6f, 0.04f), Quaternion.Euler(-14f, -20f, 10f), matMetal);

        // 5. TOTALED, CRUMPLED ELEVATOR CAB (Heavily deformed, bent framing, buckled roof & jammed doors)
        GameObject elevator = new GameObject("TotaledCrashedElevator");
        elevator.transform.SetParent(area.transform, false);
        elevator.transform.position = new Vector3(0f, 0.40f, 50f);
        // Heavy tilt & pitch from violent plunge
        elevator.transform.rotation = Quaternion.Euler(14.0f, 24.0f, -17.0f);

        // Smashed Base & Compressed Buffer Springs
        CreateBoxLocal(elevator.transform, "Cab_Base_Plate", new Vector3(0f, 0.12f, 0f), new Vector3(4.2f, 0.25f, 4.0f), matElevatorTreadPlate);
        CreateCylinderLocalRotated(elevator.transform, "BufferSpring_Left", new Vector3(-1.2f, -0.15f, 0f), new Vector3(0.4f, 0.3f, 0.4f), Quaternion.identity, matElevatorSteel);
        CreateCylinderLocalRotated(elevator.transform, "BufferSpring_Right", new Vector3(1.2f, -0.15f, 0f), new Vector3(0.4f, 0.3f, 0.4f), Quaternion.identity, matElevatorSteel);

        // Bent Structural Upright Steel Corner Columns
        CreateBoxLocalRotated(elevator.transform, "Upright_FL_BentOut", new Vector3(-1.95f, 1.65f, -1.85f), new Vector3(0.22f, 3.2f, 0.22f), Quaternion.Euler(6f, 0f, 20f), matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Upright_FR_Buckled", new Vector3(1.95f, 1.55f, -1.85f), new Vector3(0.22f, 3.0f, 0.22f), Quaternion.Euler(-10f, 0f, -16f), matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Upright_RL", new Vector3(-1.95f, 1.70f, 1.85f), new Vector3(0.22f, 3.2f, 0.22f), Quaternion.Euler(0f, 0f, 8f), matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Upright_RR", new Vector3(1.95f, 1.70f, 1.85f), new Vector3(0.22f, 3.2f, 0.22f), Quaternion.Euler(0f, 0f, -10f), matElevatorSteel);

        // Crumpled V-Shape Roof (Buckled down in center)
        CreateBoxLocalRotated(elevator.transform, "Roof_Buckled_Left", new Vector3(-1.0f, 3.20f, 0f), new Vector3(2.1f, 0.18f, 3.9f), Quaternion.Euler(0f, 0f, -12f), matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Roof_Buckled_Right", new Vector3(1.0f, 3.20f, 0f), new Vector3(2.1f, 0.18f, 3.9f), Quaternion.Euler(0f, 0f, 14f), matElevatorSteel);

        // Escape Hatch blown ajar
        CreateBoxLocalRotated(elevator.transform, "EscapeHatch_Door_Ajar", new Vector3(0.3f, 3.65f, -0.4f), new Vector3(0.9f, 0.08f, 0.9f), Quaternion.Euler(48f, 15f, -10f), matElevatorTreadPlate);

        // Hoist Crosshead Beam across roof with sheared cable eyelets
        CreateBoxLocalRotated(elevator.transform, "Roof_Crosshead_Beam", new Vector3(0f, 3.42f, 0f), new Vector3(4.2f, 0.35f, 0.4f), Quaternion.identity, matElevatorSteel);

        // Coiled tangled cables spilling over roof edge
        CreateCylinderLocalRotated(elevator.transform, "Roof_Cable_Coil_1", new Vector3(-0.6f, 3.55f, 0.2f), new Vector3(0.7f, 0.12f, 0.7f), Quaternion.Euler(15, 20, 5), matElevatorSteel);
        CreateCylinderLocalRotated(elevator.transform, "Roof_Cable_Spill_2", new Vector3(-1.8f, 2.7f, 0.4f), new Vector3(0.06f, 1.4f, 0.06f), Quaternion.Euler(10, 0, 18), matElevatorSteel);

        // Buckled Rear & Side Wall Enclosures (with dent creases & interior lining)
        CreateBoxLocalRotated(elevator.transform, "Wall_Back_Left", new Vector3(-0.95f, 1.70f, 1.85f), new Vector3(2.1f, 3.0f, 0.12f), Quaternion.Euler(0f, -8f, 0f), matElevatorInterior);
        CreateBoxLocalRotated(elevator.transform, "Wall_Back_Right", new Vector3(0.95f, 1.70f, 1.85f), new Vector3(2.1f, 3.0f, 0.12f), Quaternion.Euler(0f, 10f, 0f), matElevatorInterior);
        CreateBoxLocalRotated(elevator.transform, "Wall_Left_Deformed", new Vector3(-1.90f, 1.70f, 0f), new Vector3(0.12f, 3.0f, 3.8f), Quaternion.Euler(0f, 0f, 4f), matElevatorInterior);
        CreateBoxLocalRotated(elevator.transform, "Wall_Right_Panel", new Vector3(1.90f, 1.70f, 0f), new Vector3(0.12f, 3.0f, 3.8f), Quaternion.Euler(0f, 0f, -5f), matElevatorInterior);

        // Exterior vertical stiffener ribs (breaks plain flat sheet metal)
        CreateBoxLocalRotated(elevator.transform, "Rib_Left_1", new Vector3(-1.97f, 1.70f, -0.9f), new Vector3(0.06f, 2.9f, 0.10f), Quaternion.identity, matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Rib_Left_2", new Vector3(-1.97f, 1.70f, 0.9f), new Vector3(0.06f, 2.9f, 0.10f), Quaternion.identity, matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Rib_Right_1", new Vector3(1.97f, 1.70f, -0.9f), new Vector3(0.06f, 2.9f, 0.10f), Quaternion.identity, matElevatorSteel);
        CreateBoxLocalRotated(elevator.transform, "Rib_Right_2", new Vector3(1.97f, 1.70f, 0.9f), new Vector3(0.06f, 2.9f, 0.10f), Quaternion.identity, matElevatorSteel);

        // Trailing severed wires from blown-open hatch
        CreateCylinderLocalRotated(elevator.transform, "Hatch_Wire_1", new Vector3(0.4f, 3.3f, -0.3f), new Vector3(0.025f, 0.9f, 0.025f), Quaternion.Euler(20f, 35f, -20f), matWarningStripe);
        CreateCylinderLocalRotated(elevator.transform, "Hatch_Wire_2", new Vector3(0.1f, 3.4f, -0.5f), new Vector3(0.02f, 0.7f, 0.02f), Quaternion.Euler(-25f, 15f, 30f), matRebar);

        // Violent Jammed Accordion Doors (Left blown out, right jammed open)
        CreateBoxLocalRotated(elevator.transform, "Door_Left_PoppedOut", new Vector3(-1.25f, 1.35f, -2.15f), new Vector3(1.3f, 2.9f, 0.12f), Quaternion.Euler(-18f, -22f, 26f), matElevatorDoors);
        CreateBoxLocalRotated(elevator.transform, "Door_Right_JammedIn", new Vector3(0.95f, 1.65f, -1.75f), new Vector3(1.1f, 2.9f, 0.12f), Quaternion.Euler(8f, 16f, -6f), matElevatorDoors);
        CreateBoxLocalRotated(elevator.transform, "Door_Header_Track_Bent", new Vector3(0f, 3.05f, -1.85f), new Vector3(4.0f, 0.15f, 0.2f), Quaternion.Euler(0f, 0f, -8f), matElevatorSteel);

        // Elevator Interior Details (Visible through jagged opening)
        // 1. Car Operating Panel (COP) dangling by colorful wire bundle
        CreateBoxLocalRotated(elevator.transform, "Interior_COP_Panel", new Vector3(1.6f, 1.4f, -0.6f), new Vector3(0.08f, 1.1f, 0.35f), Quaternion.Euler(22f, 35f, 15f), matElevatorCOP);
        CreateCylinderLocalRotated(elevator.transform, "COP_Wire_Bundle", new Vector3(1.7f, 1.85f, -0.6f), new Vector3(0.04f, 0.6f, 0.04f), Quaternion.Euler(15f, 0f, -10f), matWarningStripe);
        // 2. Hanging smashed interior tube light
        CreateCylinderLocalRotated(elevator.transform, "Interior_Broken_Light", new Vector3(0f, 2.5f, 0.2f), new Vector3(0.06f, 1.2f, 0.06f), Quaternion.Euler(42f, 20f, -15f), matCleanSteel);
        // 3. Bent interior handrail
        CreateCylinderLocalRotated(elevator.transform, "Interior_Bent_Handrail", new Vector3(-1.65f, 1.0f, 0f), new Vector3(0.04f, 2.8f, 0.04f), Quaternion.Euler(90f, 14f, 0f), matElevatorSteel);

        // 6. DYNAMIC EMERGENCY LIGHTING & SPARKS WITH PHYSICAL FIXTURES
        // Physical Emergency Beacon mounted in roof of crumpled cab with amber dome & flicker
        CreateEmergencyBeacon(
            elevator.transform, "Emergency_Beacon_Cab",
            new Vector3(0.0f, 2.75f, 0.1f),
            Quaternion.Euler(180f, 0f, 0f),
            matElevatorSteel, matAmberGlow, lightsParent,
            new Color(1.0f, 0.46f, 0.10f), 3.8f, 11.5f);

        // Physical Sparking Junction Box mounted on elevator frame with severed wire & sparking node
        CreateSparkingJunction(
            elevator.transform, "Sparking_Junction_Cab",
            new Vector3(0.5f, 1.35f, -0.6f),
            Quaternion.Euler(15f, 40f, -10f),
            matElevatorSteel, matSparkGlow, lightsParent);

        // Gloomy Shaft Upper Floodlight fixture mounted on winch framing casting down shaft rails
        CreateIndustrialFloodlight(
            shaft.transform, "Shaft_Floodlight_Upper",
            new Vector3(0f, 14.8f, 48.2f),
            Quaternion.Euler(75f, 0f, 0f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.35f, 0.45f, 0.60f), 1.8f, 18f, 80f, castShadows: false);

        // 7. IMPACT CRATER & DISPLACED FLOOR SLABS
        GameObject crater = new GameObject("Floor_Impact_Crater");
        crater.transform.SetParent(area.transform, false);

        for (int i = 0; i < 10; i++)
        {
            float ang = i * 36f * Mathf.Deg2Rad;
            float r = 3.2f + (i % 3) * 0.5f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * r, 0.16f, 50f + Mathf.Sin(ang) * r);
            Quaternion slabRot = Quaternion.Euler((i % 2 == 0 ? 1 : -1) * (12f + (i % 3) * 5f), i * 36f, 14f + (i % 4) * 4f);
            CreateBoxRotated(crater.transform, "Crater_Rim_Slab_" + i, pos, new Vector3(1.5f + (i % 2) * 0.4f, 0.32f, 1.1f), slabRot, matRoomFloor);
        }

        // 4 Large Fallen Shattered Concrete Chunks from the ceiling (ONE FOR THE WALL: PBR_Master_Wall)
        CreateBoxRotated(area.transform, "Fallen_Ceiling_Chunk_1", new Vector3(-3.6f, 0.22f, 46.2f), new Vector3(1.8f, 0.38f, 1.2f), Quaternion.Euler(6f, 35f, -8f), matWall);
        CreateBoxRotated(area.transform, "Fallen_Ceiling_Chunk_2", new Vector3(3.8f, 0.20f, 46.8f), new Vector3(1.6f, 0.35f, 1.4f), Quaternion.Euler(-5f, -40f, 12f), matWall);
        CreateBoxRotated(area.transform, "Fallen_Ceiling_Chunk_3", new Vector3(-3.2f, 0.18f, 53.6f), new Vector3(1.7f, 0.32f, 1.1f), Quaternion.Euler(10f, 25f, 5f), matWall);
        CreateBoxRotated(area.transform, "Fallen_Ceiling_Chunk_4", new Vector3(2.8f, 0.22f, 54.0f), new Vector3(1.5f, 0.36f, 1.3f), Quaternion.Euler(-8f, 65f, -10f), matWall);

        // Fractured concrete slabs wedged directly under the elevator's risen end (physically supporting the tilted cab)
        CreateBoxRotated(area.transform, "Support_Concrete_Slab_RisenEnd", new Vector3(-1.85f, 0.50f, 49.70f), new Vector3(1.7f, 0.80f, 1.6f), Quaternion.Euler(14.0f, 24.0f, -17.0f), matWall);
        CreateBoxRotated(area.transform, "Support_Concrete_Chunk_Buffer", new Vector3(-1.12f, 0.22f, 50.50f), new Vector3(1.2f, 0.48f, 1.1f), Quaternion.Euler(18.0f, -15.0f, -12.0f), matRoomFloor);

        // 8. ENVIRONMENTAL PROPS & STORYTELLING ASSETS
        // Maintenance corner against West alcove
        // Shelf_01 standing upright against the West wall (-90 deg X rotation for upright vertical orientation)
        PlacePropSafe("Shelf_01", propsParent, new Vector3(-12.7f, 0f, 42.0f), Quaternion.Euler(-90, 90, 0), AlignTarget.Floor);
        PlacePropSafe("tool_cart", propsParent, new Vector3(-11.5f, 0f, 47.0f), Quaternion.Euler(0, 35, 0), AlignTarget.Floor);
        PlacePropSafe("metal_toolbox", propsParent, new Vector3(2.2f, 0f, 46.2f), Quaternion.Euler(0, -20, 0), AlignTarget.Floor);
        PlacePropSafe("crowbar_01", propsParent, new Vector3(1.6f, 0f, 46.0f), Quaternion.Euler(0, 75, 0), AlignTarget.Floor);
        PlacePropSafe("cement_bag", propsParent, new Vector3(-12.1f, 0f, 43.1f), Quaternion.Euler(0, 25, 0), AlignTarget.Floor);
        PlacePropSafe("cement_bag", propsParent, new Vector3(-11.9f, 0f, 43.8f), Quaternion.Euler(0, 105, 0), AlignTarget.Floor);

        // Industrial Barrels
        PlacePropSafe("Barrel_01", propsParent, new Vector3(-6.2f, 0f, 61.2f), Quaternion.Euler(90, 30, 0), AlignTarget.Floor); // knocked over
        PlacePropSafe("Barrel_02", propsParent, new Vector3(-5.2f, 0f, 61.8f), Quaternion.identity, AlignTarget.Floor); // upright

        // Wall Conduit & Junction Boxes
        PlacePropSafe("power_box_01", propsParent, new Vector3(-12.8f, 2.2f, 48.5f), Quaternion.Euler(0, 90, 0), AlignTarget.WallZ, 48.5f);
        PlacePropSafe("modular_industrial_pipes_01", propsParent, new Vector3(0f, 0f, maxZ - 0.2f), Quaternion.identity, AlignTarget.WallZ, maxZ);
        PlacePropSafe("modular_electric_cables", propsParent, new Vector3(-8f, 0f, minZ + 0.2f), Quaternion.Euler(0, 180, 0), AlignTarget.WallZ, minZ);

        // Stone rubble props embedded in crater
        PlacePropSafe("stone_01", propsParent, new Vector3(-2.8f, 0f, 47.5f), Quaternion.Euler(0, 45, 0), AlignTarget.Floor);
        PlacePropSafe("stone_01", propsParent, new Vector3(2.6f, 0f, 52.8f), Quaternion.Euler(15, 60, -20), AlignTarget.Floor);
        PlacePropSafe("stone_01", propsParent, new Vector3(-1.9f, 0f, 53.2f), Quaternion.Euler(-10, 110, 5), AlignTarget.Floor);
        PlacePropSafe("stone_01", propsParent, new Vector3(1.4f, 0f, 46.5f), Quaternion.Euler(0, -35, 12), AlignTarget.Floor);

        // Atmospheric industrial caged wall sconce on North column with emissive bulb
        CreateWallCagedSconce(
            area.transform, "Sconce_North_Column",
            new Vector3(-7f, 3.2f, maxZ - 0.05f),
            Quaternion.Euler(0f, 180f, 0f),
            matMetal, matBulbGlow, lightsParent,
            new Color(0.95f, 0.82f, 0.65f), 2.2f, 9.0f, castShadows: true);

        // Caged hanging light in the West maintenance alcove with subtle moody pulsation
        CreateCagedHangingLamp(
            area.transform, "Lamp_Alcove_Hanging",
            new Vector3(-11.5f, 3.8f, 46.0f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(0.98f, 0.85f, 0.65f), 1.8f, 9.0f, castShadows: false);

        GameObject alcoveLightObj = GameObject.Find("Lamp_Alcove_Hanging_Light");
        if (alcoveLightObj != null)
        {
            DynamicLightEffects dynAlcove = alcoveLightObj.AddComponent<DynamicLightEffects>();
            dynAlcove.CurrentEffectType = DynamicLightEffects.EffectType.Flicker;
            dynAlcove.baseIntensity = 1.8f;
            dynAlcove.minIntensity = 1.0f;
            dynAlcove.maxIntensity = 2.2f;
            dynAlcove.speed = 4f;
            dynAlcove.flickerRoughness = 0.25f;
            dynAlcove.blackoutChance = 0.015f;
        }

        // 9. PROFESSIONAL CEILING FLUORESCENT TUBE LIGHT SOURCES
        // Fixture 1: North Girder Tube Light (Steady industrial cool white downlight)
        CreateFluorescentTubeFixture(
            area.transform, "Ceil_TubeLight_North",
            new Vector3(-2.5f, height - 0.52f, 52.95f),
            Quaternion.Euler(0f, 0f, 0f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.88f, 0.93f, 1.0f), 8.2f, 18.0f, isDamaged: false, castShadows: false);

        // Fixture 2: South Girder Tube Light (Damaged / hanging tilted / erratic starter flicker)
        CreateFluorescentTubeFixture(
            area.transform, "Ceil_TubeLight_South_Damaged",
            new Vector3(2.6f, height - 0.58f, 47.15f),
            Quaternion.Euler(14f, 8f, -15f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.92f, 0.90f, 0.85f), 7.5f, 18.0f, isDamaged: true, castShadows: false);

        // Fixture 3: West Maintenance Alcove Ceiling Tube Light (Warm-neutral work light)
        CreateFluorescentTubeFixture(
            area.transform, "Ceil_TubeLight_WestAlcove",
            new Vector3(-10.5f, height - 0.15f, 45.0f),
            Quaternion.Euler(0f, 90f, 0f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.95f, 0.90f, 0.82f), 7.8f, 16.0f, isDamaged: false, castShadows: false);

        // Fixture 4: East Corridor Exit Bulkhead Tube Light
        CreateFluorescentTubeFixture(
            area.transform, "Ceil_TubeLight_EastExit",
            new Vector3(11.2f, 4.6f, 50.0f),
            Quaternion.Euler(0f, 90f, 0f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.90f, 0.94f, 1.0f), 8.5f, 16.0f, isDamaged: false, castShadows: false);

        // High-Angle Shaft Path Floodlight (Physical industrial floodlight fixture mounted at shaft top)
        CreateIndustrialFloodlight(
            shaft.transform, "Shaft_Inspection_Floodlight",
            new Vector3(0f, 15.0f, 50.0f),
            Quaternion.Euler(88f, 10f, 0f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.65f, 0.80f, 1.0f), 3.0f, 22.0f, 85f, castShadows: true);

        // Hazard & Electrical Warning Signs (dedicated sign plates with normalized [0, 1] UVs)
        if (matHazardSign != null)
        {
            CreateSignPlate(area.transform, "Sign_ExitCaution", new Vector3(maxX - 0.02f, 2.8f, 44.8f), new Vector3(0.02f, 0.5f, 0.5f), Vector3.left, matHazardSign);
            CreateSignPlate(area.transform, "Sign_HighVoltage", new Vector3(-12.78f, 2.9f, 48.5f), new Vector3(0.02f, 0.5f, 0.5f), Vector3.right, matHazardSign);
        }
    }

    // =========================================================================
    // AREA 2: LONG PIT CORRIDOR
    // =========================================================================
    private static void BuildArea2_PitCorridor(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent, Transform markersParent)
    {
        GameObject area = new GameObject("02_PitCorridor");
        area.transform.SetParent(envParent, false);

        float width = 7.2f;
        float height = 4.8f;
        float minZ = 46.4f, maxZ = 53.6f;

        // Pit bottom (deep dark floor at Y = -10m)
        CreateBox(area.transform, "Pit_Depth_Floor", new Vector3(40f, -10f, 50f), new Vector3(56f, 0.5f, 14f), matDarkPit);

        // Side Walls - plunge deep to Y = -11m to completely seal pit sides against the void
        CreateBox(area.transform, "Corridor_Wall_North", new Vector3(40.25f, -3.0f, maxZ + 0.25f), new Vector3(54.5f, 16.0f, 0.5f), matWall);
        CreateBox(area.transform, "Corridor_Wall_South", new Vector3(40.25f, -3.0f, minZ - 0.25f), new Vector3(54.5f, 16.0f, 0.5f), matWall);

        // Ceiling (under Ceilings parent) - extends over side walls
        CreateBox(ceilParent, "PitCorridor_Ceiling", new Vector3(40.25f, height + 0.25f, 50f), new Vector3(54.5f, 0.5f, 8.2f), matCeiling);

        // 1. Initial Walkway: X: 13 to 20 (7m solid) (ONE FOR PATHWAY FLOORS: PBR_Master_PathwayFloor) - exact boundary alignment
        CreateBox(area.transform, "Floor_Walkway_0", new Vector3(16.5f, -0.25f, 50f), new Vector3(7.0f, 0.5f, 8.2f), matPathwayFloor);

        // 2. NORMAL PIT 1: X: 20 to 25 (5m pit gap!) (ONE METALLIC STYLE: PBR_Master_Metal)
        // Tops lowered to Y = -5.25m (top at Y = -0.50m, flush with underside of 50cm floor slab) to eliminate coplanar z-fighting with walkway floors
        CreateBox(area.transform, "Pit_Drop_20W", new Vector3(19.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit_Drop_25E", new Vector3(25.2f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit1_GirderWalkway", new Vector3(22.5f, -0.1f, 52.6f), new Vector3(5.0f, 0.3f, 1.4f), matMetal);

        // 3. Walkway 1: X: 25 to 32 (7m solid)
        CreateBox(area.transform, "Floor_Walkway_1", new Vector3(28.5f, -0.25f, 50f), new Vector3(7.0f, 0.5f, 8.2f), matPathwayFloor);

        // 4. NORMAL PIT 2: X: 32 to 37 (5m pit gap!)
        CreateBox(area.transform, "Pit_Drop_32W", new Vector3(31.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit_Drop_37E", new Vector3(37.2f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit2_SteppingIsland", new Vector3(34.5f, -0.25f, 50f), new Vector3(2.2f, 0.5f, 2.2f), matPathwayFloor);

        // 5. Walkway 2: X: 37 to 44 (7m solid)
        CreateBox(area.transform, "Floor_Walkway_2", new Vector3(40.5f, -0.25f, 50f), new Vector3(7.0f, 0.5f, 8.2f), matPathwayFloor);

        // 6. NORMAL PIT 3: X: 44 to 49 (5m pit gap!)
        CreateBox(area.transform, "Pit_Drop_44W", new Vector3(43.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit_Drop_49E", new Vector3(49.2f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit3_LedgeWalkway", new Vector3(46.5f, -0.25f, 47.4f), new Vector3(5.0f, 0.5f, 1.5f), matPathwayFloor);

        // 7. Walkway 3: X: 49 to 55 (6m solid)
        CreateBox(area.transform, "Floor_Walkway_3", new Vector3(52f, -0.25f, 50f), new Vector3(6.0f, 0.5f, 8.2f), matPathwayFloor);

        // 8. SPECIAL HIDDEN PIT (Section C): X: 55 to 60.5 (5.5m pit gap!)
        // Tops lowered to Y = -5.25m (top at Y = -0.50m, flush with underside of 50cm floor slab) to prevent coplanar z-fighting with the walkway floor
        CreateBox(area.transform, "Pit_Drop_55W", new Vector3(54.8f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);
        CreateBox(area.transform, "Pit_Drop_60E", new Vector3(60.7f, -5.25f, 50f), new Vector3(0.4f, 9.5f, 8.2f), matWall);

        // Narrow crossing girder shifted to South side (Z = 47.0f), visible ONLY to Player 2, but solid for ALL 4 players!
        GameObject girder = CreateBox(area.transform, "HiddenPit_CrossingGirder", new Vector3(57.75f, -0.1f, 47.0f), new Vector3(5.5f, 0.25f, 1.2f), matMetal);
        PerceptionCover girderCover = girder.AddComponent<PerceptionCover>();
        girderCover.Configure(PerceptionCover.PerceptionRule.VisibleOnlyToTarget, PlayerIdentity.Player2, syncColliders: false);

        // Hidden floor cover spanning the pit gap, visible for Players 1, 3, 4 (hidden from Player 2)
        // Raised by 0.2mm (Y = 0.0002f) and UV-phase aligned to Walkway 3 (topUvOffsetU = 3.05f) for 100% seamless cobblestone continuation
        // Solid walkway floor for non-scout players from both directions across corridor
        GameObject floorCover = CreateQuad(area.transform, "FloorCover_HiddenPit", new Vector3(57.75f, 0.0002f, 50.0f), new Vector3(5.5f, 0f, 8.2f), Vector3.up, matPathwayFloor, topUvOffsetU: 3.05f, addCollider: false);
        PerceptionCover floorCoverComp = floorCover.AddComponent<PerceptionCover>();
        floorCoverComp.Configure(PerceptionCover.PerceptionRule.HiddenFromTarget, PlayerIdentity.Player2, syncColliders: false);

        // 9. Final Walkway: X: 60.5 to 67 (6.5m solid), continuous UVs (topUvOffsetU = 5.725f)
        CreateBox(area.transform, "Floor_Walkway_Final", new Vector3(63.75f, -0.25f, 50f), new Vector3(6.5f, 0.5f, 8.2f), matPathwayFloor, topUvOffsetU: 5.725f);

        // Corridor Lighting with physical industrial fixtures
        CreateFluorescentTubeFixture(
            area.transform, "PitCorridor_Tube_1",
            new Vector3(17f, height - 0.15f, 50f),
            Quaternion.identity,
            matMetal, matTubeGlow, lightsParent,
            new Color(0.92f, 0.90f, 0.85f), 8.5f, 14f, isDamaged: false);

        CreateFluorescentTubeFixture(
            area.transform, "PitCorridor_Tube_2_Damaged",
            new Vector3(29f, height - 0.22f, 50f),
            Quaternion.Euler(6f, 12f, -8f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.88f, 0.88f, 0.82f), 4.5f, 14f, isDamaged: true);

        CreateFluorescentTubeFixture(
            area.transform, "PitCorridor_Tube_3",
            new Vector3(41f, height - 0.15f, 50f),
            Quaternion.identity,
            matMetal, matTubeGlow, lightsParent,
            new Color(0.90f, 0.92f, 0.95f), 8.5f, 14f, isDamaged: false);

        CreateCagedHangingLamp(
            area.transform, "HiddenPit_HangingHazardLamp",
            new Vector3(57.75f, 3.6f, 50f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(1.0f, 0.88f, 0.72f), 10.0f, 16f, castShadows: false);

        // Downward cascading light illuminating pit shaft and bottom floor (so fallen player sees light coming down)
        CreateSpotLight(
            lightsParent, "Pit4_Shaft_Downlight",
            new Vector3(57.75f, -0.55f, 50.0f),
            Quaternion.Euler(90f, 0f, 0f),
            new Color(0.95f, 0.90f, 0.82f),
            9.5f, 18.0f, 110f, LightShadows.None);

        // Upward bounce fill illuminating underside of non-colliding FloorCover_HiddenPit (so player looking up sees the walkway texture)
        CreatePointLight(
            lightsParent, "Pit4_Underside_BounceLight",
            new Vector3(57.75f, -2.2f, 50.0f),
            new Color(0.92f, 0.88f, 0.80f),
            5.5f, 7.0f);

        // Wall-mounted warning lights above each pit along the corridor (weak amber lights making pits visible per user request)
        float[] pitXCoords = new float[] { 22.5f, 34.5f, 46.5f, 57.75f };
        for (int i = 0; i < pitXCoords.Length; i++)
        {
            float px = pitXCoords[i];
            int pitNum = i + 1;
            CreateBox(area.transform, $"Pit{pitNum}_WallSconce_Bracket", new Vector3(px, 2.0f, maxZ - 0.15f), new Vector3(0.24f, 0.16f, 0.18f), matMetal);
            CreateBox(area.transform, $"Pit{pitNum}_WallSconce_Bulb", new Vector3(px, 1.95f, maxZ - 0.26f), new Vector3(0.12f, 0.12f, 0.12f), matBulbGlow);
            CreatePointLight(lightsParent, $"Pit{pitNum}_Wall_WarningLight", new Vector3(px, 1.90f, maxZ - 0.40f), new Color(1.0f, 0.82f, 0.55f), 1.6f, 5.5f);
        }

        // Soft fill lights along the bottom of the pit trench so fallen players and Monster 8 are clearly visible
        CreatePointLight(lightsParent, "Pit_Depth_Fill_West", new Vector3(28.0f, -8.5f, 50.0f), new Color(0.85f, 0.88f, 0.95f), 2.5f, 15.0f);
        CreatePointLight(lightsParent, "Pit_Depth_Fill_East", new Vector3(52.0f, -8.5f, 50.0f), new Color(0.85f, 0.88f, 0.95f), 2.5f, 15.0f);

        // Props
        PlacePropSafe("trashbag", propsParent, new Vector3(18f, 0f, 49.5f), Quaternion.Euler(0, 15, 0), AlignTarget.Floor);
        PlacePropSafe("modular_industrial_pipes_01", propsParent, new Vector3(26f, 0f, maxZ - 0.15f), Quaternion.identity, AlignTarget.WallZ, maxZ);
        PlacePropSafe("modular_industrial_pipes_01", propsParent, new Vector3(46f, 0f, maxZ - 0.15f), Quaternion.identity, AlignTarget.WallZ, maxZ);
        PlacePropSafe("modular_electric_cables", propsParent, new Vector3(36f, 0f, minZ + 0.15f), Quaternion.Euler(0, 180, 0), AlignTarget.WallZ, minZ);
        PlacePropSafe("trashbag", propsParent, new Vector3(51f, 0f, minZ + 0.6f), Quaternion.identity, AlignTarget.Floor);

        // Monster 8: Pit Stalker lurking in the signature pit chamber
        SetupMonster8PitStalker.SetupPitMonster();
    }

    // =========================================================================
    // AREA 3: APPROACH HALLWAY
    // =========================================================================
    private static void BuildArea3_ApproachHallway(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent)
    {
        GameObject area = new GameObject("03_ApproachHallway");
        area.transform.SetParent(envParent, false);

        float minZ = 47f, maxZ = 53f;
        float height = 4.8f;

        // Floor & Ceiling extend under/over walls and align flush with Area 2 (X = 67) & Area 4 (X = 87)
        CreateBox(area.transform, "Floor", new Vector3(77f, -0.25f, 50f), new Vector3(20.0f, 0.5f, 7.2f), matPathwayFloor);
        CreateBox(ceilParent, "Approach_Ceiling", new Vector3(77f, height + 0.25f, 50f), new Vector3(20.5f, 0.5f, 7.2f), matCeiling);

        CreateBox(area.transform, "Wall_North", new Vector3(77f, height * 0.5f, maxZ + 0.25f), new Vector3(20.5f, height, 0.5f), matWall);
        CreateBox(area.transform, "Wall_South", new Vector3(77f, height * 0.5f, minZ - 0.25f), new Vector3(20.5f, height, 0.5f), matWall);

        // Classroom doors (architectural details, NO floating text) - recessed cleanly within frames
        CreateBox(area.transform, "Door_Room101", new Vector3(72f, 1.6f, maxZ - 0.03f), new Vector3(1.6f, 3.0f, 0.03f), matWood);
        CreateBox(area.transform, "Door_Room101_Frame", new Vector3(72f, 1.6f, maxZ - 0.05f), new Vector3(1.8f, 3.2f, 0.08f), matMetal);

        CreateBox(area.transform, "Door_Room102", new Vector3(81f, 1.6f, maxZ - 0.03f), new Vector3(1.6f, 3.0f, 0.03f), matWood);
        CreateBox(area.transform, "Door_Room102_Frame", new Vector3(81f, 1.6f, maxZ - 0.05f), new Vector3(1.8f, 3.2f, 0.08f), matMetal);

        // Lighting with physical ceiling fluorescent fixtures
        CreateFluorescentTubeFixture(
            area.transform, "Approach_Tube_1",
            new Vector3(72f, height - 0.15f, 50f),
            Quaternion.identity,
            matMetal, matTubeGlow, lightsParent,
            new Color(0.92f, 0.90f, 0.85f), 8.5f, 14f, isDamaged: false);

        CreateFluorescentTubeFixture(
            area.transform, "Approach_Tube_2",
            new Vector3(82f, height - 0.15f, 50f),
            Quaternion.identity,
            matMetal, matTubeGlow, lightsParent,
            new Color(0.92f, 0.90f, 0.85f), 8.5f, 14f, isDamaged: false);

        // Props
        PlacePropSafe("fire_alarm", propsParent, new Vector3(75f, 2.0f, minZ + 0.05f), Quaternion.identity, AlignTarget.WallZ, minZ);
        PlacePropSafe("wheelchair_01", propsParent, new Vector3(84f, 0f, minZ + 0.8f), Quaternion.Euler(0, -70, 0), AlignTarget.Floor);
    }

    // =========================================================================
    // AREA 4: HIDDEN DOOR / FALSE WALL CHAMBER
    // =========================================================================
    private static void BuildArea4_HiddenDoorChamber(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent, Transform markersParent)
    {
        GameObject area = new GameObject("04_HiddenDoorSection");
        area.transform.SetParent(envParent, false);

        float height = 5.0f;
        float minX = 87f, maxX = 99f;
        float minZ = 44f, maxZ = 56f;

        // Floor & Ceiling align flush with Area 3 (X = 87) and extend under walls
        CreateBox(area.transform, "Floor", new Vector3(93.25f, -0.25f, 50f), new Vector3(12.5f, 0.5f, 13.5f), matRoomFloor);
        CreateBox(ceilParent, "HiddenDoor_Ceiling", new Vector3(93f, height + 0.25f, 50f), new Vector3(13.5f, 0.5f, 13.5f), matCeiling);

        CreateBox(area.transform, "Wall_North", new Vector3(93f, height * 0.5f, maxZ + 0.25f), new Vector3(13.5f, height, 0.5f), matWall);
        CreateBox(area.transform, "Wall_South", new Vector3(93f, height * 0.5f, minZ - 0.25f), new Vector3(13.5f, height, 0.5f), matWall);

        // West Wall: opening from Approach Hallway (Z: 47 to 53)
        CreateBox(area.transform, "Wall_West_South", new Vector3(minX - 0.25f, height * 0.5f, 45.25f), new Vector3(0.5f, height, 3.5f), matWall);
        CreateBox(area.transform, "Wall_West_North", new Vector3(minX - 0.25f, height * 0.5f, 54.75f), new Vector3(0.5f, height, 3.5f), matWall);

        // EAST WALL: Dead-end false wall with HIDDEN DOORWAY opening
        // Opening: Width 3.2m (Z: 48.4 to 51.6), Height 3.6m
        CreateBox(area.transform, "Wall_East_South", new Vector3(maxX + 0.25f, height * 0.5f, 46.0f), new Vector3(0.5f, height, 4.8f), matWall);
        CreateBox(area.transform, "Wall_East_North", new Vector3(maxX + 0.25f, height * 0.5f, 54.0f), new Vector3(0.5f, height, 4.8f), matWall);
        CreateBox(area.transform, "Wall_East_Header", new Vector3(maxX + 0.25f, 3.6f + (height - 3.6f) * 0.5f, 50f), new Vector3(0.5f, height - 3.6f, 3.4f), matWall);

        // HIDDEN DOOR / FALSE WALL (Co-op Perception Secret Passage):
        // ONE-WAY MIRROR: Appears as a seamless solid wall from Area 4 approach side.
        // Transparent from the secret passage looking back!
        // Hidden from Player 1 who sees through from both sides.
        GameObject doorCover = CreateQuad(area.transform, "DoorCover_Area4_HiddenDoor", new Vector3(maxX - 0.001f, 1.8f, 50.0f), new Vector3(0f, 3.6f, 3.2f), Vector3.left, matWall, addCollider: false);
        PerceptionCover doorCoverComp = doorCover.AddComponent<PerceptionCover>();
        doorCoverComp.Configure(PerceptionCover.PerceptionRule.HiddenFromTarget, PlayerIdentity.Player1, syncColliders: false);
        doorCoverComp.ConfigureOneWay(PerceptionCover.OneWayAxis.PositiveX, 100.2f);

        // PASSAGE BEHIND HIDDEN DOOR (turns South into Creature Room)
        GameObject passage = new GameObject("Passage_To_CreatureRoom");
        passage.transform.SetParent(area.transform, false);

        // East extension: X: 98.75 to 105.0, Z: 46.9 to 53.1 (ONE FOR PATHWAY FLOORS)
        CreateBox(passage.transform, "Passage_Floor_East", new Vector3(101.875f, -0.25f, 50f), new Vector3(6.25f, 0.5f, 6.2f), matPathwayFloor);
        CreateBox(ceilParent, "Passage_Ceil_East", new Vector3(101.875f, height + 0.25f, 50f), new Vector3(6.25f, 0.5f, 6.2f), matCeiling);
        CreateBox(passage.transform, "Passage_Wall_EastCap", new Vector3(104.75f, height * 0.5f, 50f), new Vector3(0.5f, height, 6.2f), matWall);
        CreateBox(passage.transform, "Passage_Wall_North", new Vector3(101.75f, height * 0.5f, 52.75f), new Vector3(6.5f, height, 0.5f), matWall);

        // South run: Z: 47.5 down to 22.5 (Length: 25.5m), Center X = 102, Width: 5.5m (ONE FOR PATHWAY FLOORS)
        CreateBox(passage.transform, "Passage_Floor_South", new Vector3(102f, -0.25f, 35f), new Vector3(5.5f, 0.5f, 25.5f), matPathwayFloor);
        CreateBox(ceilParent, "Passage_Ceil_South", new Vector3(102f, height + 0.25f, 35f), new Vector3(5.5f, 0.5f, 25.5f), matCeiling);
        CreateBox(passage.transform, "Passage_Wall_East", new Vector3(104.75f, height * 0.5f, 35f), new Vector3(0.5f, height, 25.5f), matWall);
        CreateBox(passage.transform, "Passage_Wall_West", new Vector3(99.25f, height * 0.5f, 34f), new Vector3(0.5f, height, 23.5f), matWall);

        // Lighting with physical fluorescent and caged sconce fixtures
        CreateFluorescentTubeFixture(
            area.transform, "HiddenDoor_Chamber_Tube",
            new Vector3(93f, height - 0.15f, 50f),
            Quaternion.Euler(0, 90, 0),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.90f, 0.95f, 1.0f), 8.5f, 14f, isDamaged: false);

        CreateWallCagedSconce(
            passage.transform, "Passage_Sconce_1",
            new Vector3(104.5f, 3.4f, 42f),
            Quaternion.Euler(0f, -90f, 0f),
            matMetal, matBulbGlow, lightsParent,
            new Color(0.92f, 0.85f, 0.72f), 2.0f, 9.5f);

        CreateWallCagedSconce(
            passage.transform, "Passage_Sconce_2",
            new Vector3(104.5f, 3.4f, 30f),
            Quaternion.Euler(0f, -90f, 0f),
            matMetal, matBulbGlow, lightsParent,
            new Color(0.92f, 0.85f, 0.72f), 2.0f, 9.5f);

        // Narration Trigger: Situation 3 - At least 2 players cross first hidden passage
        GameObject triggerA4 = new GameObject("Trigger_Narration_FirstHiddenDoor");
        triggerA4.transform.SetParent(passage.transform, false);
        triggerA4.transform.position = new Vector3(101.5f, 2.0f, 50.0f);
        BoxCollider boxA4 = triggerA4.AddComponent<BoxCollider>();
        boxA4.isTrigger = true;
        boxA4.size = new Vector3(5.0f, 4.0f, 6.0f);
        NarrationMultiPlayerTrigger zoneA4 = triggerA4.AddComponent<NarrationMultiPlayerTrigger>();
        zoneA4.Configure("CROSS_FIRST_HIDDEN_DOOR", 2, true);
    }

    // =========================================================================
    // AREA 5: CREATURE ENCOUNTER ROOM (WAITING AREA - NO ENEMY)
    // =========================================================================
    private static void BuildArea5_CreatureRoom(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent)
    {
        GameObject area = new GameObject("05_CreatureRoom");
        area.transform.SetParent(envParent, false);

        float height = 5.5f;
        float minX = 72f, maxX = 104.5f;
        float minZ = -1f, maxZ = 23f;
        float roomWidth = maxX - minX; // 32.5m
        float midX = (minX + maxX) * 0.5f; // 88.25m

        // Floor & Ceiling overlap beyond exterior walls to seal void seams
        CreateBox(area.transform, "Floor", new Vector3(midX, -0.25f, 11f), new Vector3(roomWidth + 1.5f, 0.5f, 25.5f), matFloor);
        CreateBox(ceilParent, "CreatureRoom_Ceiling", new Vector3(midX, height + 0.25f, 11f), new Vector3(roomWidth + 1.5f, 0.5f, 25.5f), matCeiling);

        // North Wall: Seamless entrance connecting to passage from Area 4 (X: 99.5 to 104.5)
        // Solid wall west of the doorway (X: 71.5 to 99.5, width: 28.0m, center: 85.5m)
        CreateBox(area.transform, "Wall_North_West", new Vector3(85.5f, height * 0.5f, maxZ + 0.25f), new Vector3(28.0f, height, 0.5f), matWall);
        // Doorway Header over passage entrance (X: 99.5 to 104.5, width: 5.2m, center: 102.0m)
        CreateBox(area.transform, "Wall_North_Header", new Vector3(102.0f, 4.0f + (height - 4.0f) * 0.5f, maxZ + 0.25f), new Vector3(5.2f, height - 4.0f, 0.5f), matWall);

        // Industrial Portal Framing the passage entrance into Creature Room (ONE METALLIC STYLE: PBR_Master_Metal)
        CreateBox(area.transform, "Portal_Frame_Left", new Vector3(99.65f, 2.0f, maxZ + 0.15f), new Vector3(0.3f, 4.0f, 0.3f), matMetal);
        CreateBox(area.transform, "Portal_Frame_Right", new Vector3(104.35f, 2.0f, maxZ + 0.15f), new Vector3(0.3f, 4.0f, 0.3f), matMetal);
        CreateBox(area.transform, "Portal_Frame_Lintel", new Vector3(102.0f, 4.15f, maxZ + 0.15f), new Vector3(5.0f, 0.3f, 0.3f), matMetal);

        // South Wall (X: 71.25 to 105.25, width: 34.0m)
        CreateBox(area.transform, "Wall_South", new Vector3(midX, height * 0.5f, minZ - 0.25f), new Vector3(roomWidth + 1.5f, height, 0.5f), matWall);
        // East Wall: Perfectly continuous with Passage_Wall_East at X = 104.75m (Z: -1.5 to 23.5)
        CreateBox(area.transform, "Wall_East", new Vector3(maxX + 0.25f, height * 0.5f, 11f), new Vector3(0.5f, height, 25.0f), matWall);

        // West Wall: Separate Exit to Connecting Hallway at Z: 8 to 14 (Width: 6m, Height: 4.8m matching hallway)
        CreateBox(area.transform, "Wall_West_South", new Vector3(minX - 0.25f, height * 0.5f, 3.5f), new Vector3(0.5f, height, 9.5f), matWall);
        CreateBox(area.transform, "Wall_West_North", new Vector3(minX - 0.25f, height * 0.5f, 18.5f), new Vector3(0.5f, height, 9.5f), matWall);
        CreateBox(area.transform, "Wall_West_Header", new Vector3(minX - 0.25f, 4.8f + (height - 4.8f) * 0.5f, 11f), new Vector3(0.5f, height - 4.8f, 6.2f), matWall);

        // Hidden Exit Doorway False Wall:
        // ONE-WAY MIRROR: Visible as solid concrete wall from Creature Room approach side.
        // Transparent from the Connecting Hallway looking back!
        // Hidden from Player 1 who sees the doorway open from both sides.
        GameObject doorCover = CreateQuad(area.transform, "DoorCover_CreatureRoomExit", new Vector3(minX + 0.001f, 2.4f, 11.0f), new Vector3(0f, 4.8f, 5.50f), Vector3.right, matWall, topUvOffsetU: 4.125f, addCollider: false);
        PerceptionCover doorCoverComp = doorCover.AddComponent<PerceptionCover>();
        doorCoverComp.Configure(PerceptionCover.PerceptionRule.HiddenFromTarget, PlayerIdentity.Player1, syncColliders: false);
        doorCoverComp.ConfigureOneWay(PerceptionCover.OneWayAxis.NegativeX, 71.95f);

        // 4 Architectural Structural Pillars
        CreateBox(area.transform, "Pillar_NW", new Vector3(79f, height * 0.5f, 17f), new Vector3(1.4f, height, 1.4f), matPillar);
        CreateBox(area.transform, "Pillar_SW", new Vector3(79f, height * 0.5f, 5f), new Vector3(1.4f, height, 1.4f), matPillar);
        CreateBox(area.transform, "Pillar_NE", new Vector3(93f, height * 0.5f, 17f), new Vector3(1.4f, height, 1.4f), matPillar);
        CreateBox(area.transform, "Pillar_SE", new Vector3(93f, height * 0.5f, 5f), new Vector3(1.4f, height, 1.4f), matPillar);

        // Waiting Area Chairs & Desks along perimeter (keeps central chase arena open)
        for (int i = 0; i < 4; i++)
        {
            PlacePropSafe("SchoolChair_01", propsParent, new Vector3(82f + i * 2.4f, 0f, 1.5f), Quaternion.identity, AlignTarget.Floor);
        }
        PlacePropSafe("SchoolDesk_01", propsParent, new Vector3(maxX - 1.5f, 0f, 8f), Quaternion.Euler(0, -90, 0), AlignTarget.Floor);
        PlacePropSafe("SchoolDesk_01", propsParent, new Vector3(maxX - 1.5f, 0f, 12f), Quaternion.Euler(0, -90, 0), AlignTarget.Floor);
        PlacePropSafe("wooden_bookshelf_worn", propsParent, new Vector3(maxX - 0.6f, 0f, 16f), Quaternion.Euler(-90, -90, 0), AlignTarget.Floor);
        PlacePropSafe("wall_clock", propsParent, new Vector3(84f, 3.2f, maxZ - 0.05f), Quaternion.identity, AlignTarget.WallZ, maxZ);
        PlacePropSafe("standing_chalkboard_01", propsParent, new Vector3(95f, 0f, 20f), Quaternion.Euler(0, -25, 0), AlignTarget.Floor);

        // Lighting with physical caged hanging pendant fixtures
        CreateCagedHangingLamp(
            area.transform, "CreatureRoom_Lamp_Center",
            new Vector3(86f, 3.8f, 11f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(1.0f, 0.92f, 0.80f), 14.0f, 22f, castShadows: false);

        CreateCagedHangingLamp(
            area.transform, "CreatureRoom_Lamp_North",
            new Vector3(95f, 3.8f, 18f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(0.85f, 0.88f, 0.92f), 10.0f, 18f, castShadows: false);

        CreateCagedHangingLamp(
            area.transform, "CreatureRoom_Lamp_WestExit",
            new Vector3(75f, 3.8f, 11f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(0.88f, 0.90f, 0.95f), 12.0f, 18f, castShadows: false);

        // Narration Trigger: Situation 4 - At least 2 players reach the Fuse Room
        GameObject triggerA5 = new GameObject("Trigger_Narration_PastMonsterRoom");
        triggerA5.transform.SetParent(area.transform, false);
        triggerA5.transform.position = new Vector3(46.0f, 2.5f, 11.0f);
        BoxCollider boxA5 = triggerA5.AddComponent<BoxCollider>();
        boxA5.isTrigger = true;
        boxA5.size = new Vector3(6.0f, 4.5f, 7.5f);
        NarrationMultiPlayerTrigger zoneA5 = triggerA5.AddComponent<NarrationMultiPlayerTrigger>();
        zoneA5.Configure("PAST_MONSTER_ROOM", 2, true);
    }

    // =========================================================================
    // AREA 6: CONNECTING HALLWAY
    // =========================================================================
    private static void BuildArea6_ConnectingHallway(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent)
    {
        GameObject area = new GameObject("06_ConnectingHallway");
        area.transform.SetParent(envParent, false);

        float height = 4.8f;
        float minZ = 8f, maxZ = 14f;

        // Floor & Ceiling extend under/over walls and meet Area 5 & Area 7 junctions cleanly without protruding into rooms
        CreateBox(area.transform, "Floor", new Vector3(60f, -0.25f, 11f), new Vector3(23.0f, 0.5f, 7.2f), matPathwayFloor);
        CreateBox(ceilParent, "Connecting_Ceiling", new Vector3(60f, height + 0.25f, 11f), new Vector3(23.0f, 0.5f, 7.2f), matCeiling);

        CreateBox(area.transform, "Wall_North", new Vector3(60f, height * 0.5f, maxZ + 0.25f), new Vector3(23.0f, height, 0.5f), matWall);
        CreateBox(area.transform, "Wall_South", new Vector3(60f, height * 0.5f, minZ - 0.25f), new Vector3(23.0f, height, 0.5f), matWall);

        // Progressive Dimming Lighting with physical fixtures
        CreateFluorescentTubeFixture(
            area.transform, "ConnHall_Tube_East",
            new Vector3(68f, height - 0.15f, 11f),
            Quaternion.identity,
            matMetal, matTubeGlow, lightsParent,
            new Color(0.88f, 0.88f, 0.85f), 8.5f, 14f, isDamaged: false);

        CreateFluorescentTubeFixture(
            area.transform, "ConnHall_Tube_Mid_Damaged",
            new Vector3(60f, height - 0.22f, 11f),
            Quaternion.Euler(12f, 0f, 8f),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.75f, 0.75f, 0.70f), 4.5f, 14f, isDamaged: true);

        CreateWallCagedSconce(
            area.transform, "ConnHall_Sconce_West",
            new Vector3(51f, 3.2f, minZ + 0.05f),
            Quaternion.Euler(0f, 0f, 0f),
            matMetal, matBulbGlow, lightsParent,
            new Color(0.60f, 0.60f, 0.55f), 1.0f, 7.5f);

        // Props
        PlacePropSafe("modular_industrial_pipes_01", propsParent, new Vector3(62f, 0f, maxZ - 0.15f), Quaternion.identity, AlignTarget.WallZ, maxZ);
        PlacePropSafe("modular_electric_cables", propsParent, new Vector3(56f, 0f, minZ + 0.15f), Quaternion.Euler(0, 180, 0), AlignTarget.WallZ, minZ);
    }

    // =========================================================================
    // AREA 7: DARK FUSE ROOM
    // =========================================================================
    private static void BuildArea7_DarkFuseRoom(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent, Transform markersParent)
    {
        GameObject area = new GameObject("07_DarkFuseRoom");
        area.transform.SetParent(envParent, false);

        float height = 5.0f;
        float minX = 24f, maxX = 48f;
        float minZ = 1f, maxZ = 21f;

        // Floor & Ceiling overlap exterior walls to seal void seams
        CreateBox(area.transform, "Floor", new Vector3(36f, -0.25f, 11f), new Vector3(25.5f, 0.5f, 21.5f), matRoomFloor);
        CreateBox(ceilParent, "FuseRoom_Ceiling", new Vector3(36f, height + 0.25f, 11f), new Vector3(25.5f, 0.5f, 21.5f), matCeiling);

        CreateBox(area.transform, "Wall_North", new Vector3(36f, height * 0.5f, maxZ + 0.25f), new Vector3(25.5f, height, 0.5f), matWall);
        CreateBox(area.transform, "Wall_South", new Vector3(36f, height * 0.5f, minZ - 0.25f), new Vector3(25.5f, height, 0.5f), matWall);

        // East Wall: Entrance from Connecting Hallway at Z: 8 to 14
        CreateBox(area.transform, "Wall_East_South", new Vector3(maxX + 0.25f, height * 0.5f, 4.5f), new Vector3(0.5f, height, 7.5f), matWall);
        CreateBox(area.transform, "Wall_East_North", new Vector3(maxX + 0.25f, height * 0.5f, 17.5f), new Vector3(0.5f, height, 7.5f), matWall);
        CreateBox(area.transform, "Wall_East_Header", new Vector3(maxX + 0.25f, 4.2f + (height - 4.2f) * 0.5f, 11f), new Vector3(0.5f, height - 4.2f, 6.2f), matWall);

        // West Wall: Exit to Final Hallway at Z: 8 to 14
        CreateBox(area.transform, "Wall_West_South", new Vector3(minX - 0.25f, height * 0.5f, 4.5f), new Vector3(0.5f, height, 7.5f), matWall);
        CreateBox(area.transform, "Wall_West_North", new Vector3(minX - 0.25f, height * 0.5f, 17.5f), new Vector3(0.5f, height, 7.5f), matWall);
        CreateBox(area.transform, "Wall_West_Header", new Vector3(minX - 0.25f, 4.2f + (height - 4.2f) * 0.5f, 11f), new Vector3(0.5f, height - 4.2f, 6.2f), matWall);

        // Central Workbench with Fuse Mounting Fixture (NO floating text)
        CreateBox(area.transform, "Fuse_Workbench", new Vector3(36f, 0.45f, 11f), new Vector3(3.4f, 0.9f, 2.0f), matMetal);

        // Grounded physical industrial mounting socket for Fuse resting directly on workbench
        GameObject devFuseMarker = new GameObject("Marker_FuseLocation");
        devFuseMarker.transform.SetParent(area.transform, false);
        devFuseMarker.transform.position = new Vector3(36f, 0.45f, 11f);

        CreateBox(devFuseMarker.transform, "Fuse_Mounting_Plate", new Vector3(36f, 0.915f, 11f), new Vector3(0.9f, 0.025f, 0.9f), matMetal);

        // Green SM_FuseCell 3D Hero Asset (Prominently placed on workbench table)
        GameObject fusePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReadyMadeAssets/FuseCell/SM_FuseCell.fbx");
        if (fusePrefab != null)
        {
            GameObject fuseCell = Object.Instantiate(fusePrefab, devFuseMarker.transform);
            fuseCell.name = "SM_FuseCell_Pickup";
            fuseCell.transform.position = new Vector3(36f, 0.928f, 11f);
            fuseCell.transform.rotation = Quaternion.identity;
            fuseCell.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f); // Scaled for prominent visibility on 3.4m workbench

            // Remove legacy Animation component so it cannot overwrite position or scale at runtime
            Animation anim = fuseCell.GetComponent<Animation>();
            if (anim != null)
            {
                Object.DestroyImmediate(anim);
            }

            // Trigger collider for pickup interaction
            SphereCollider sc = fuseCell.AddComponent<SphereCollider>();
            sc.center = new Vector3(0f, 0.08f, 0f);
            sc.radius = 0.75f;
            sc.isTrigger = true;

            // Atmospheric emerald glow light radiating from the core (dormant until torch light shines)
            GameObject glowLightGo = new GameObject("Fuse_Green_GlowLight");
            glowLightGo.transform.SetParent(fuseCell.transform, false);
            glowLightGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            Light greenGlow = glowLightGo.AddComponent<Light>();
            greenGlow.type = LightType.Point;
            greenGlow.color = new Color(0.15f, 1.0f, 0.35f);
            greenGlow.intensity = 0.0f;
            greenGlow.range = 6.5f;
            greenGlow.enabled = false;
            greenGlow.shadows = LightShadows.None;

            // Interactive pickup component & reactive illumination (Player 4 torch ability)
            fuseCell.AddComponent<FuseCellIlluminationReceiver>();
            fuseCell.AddComponent<FuseCellPickup>();
        }

        // Electrical Panels on North Wall - cleanly offset to avoid coplanar Z-fighting with wall
        CreateBox(area.transform, "Breaker_Backplate", new Vector3(32f, 2.0f, maxZ - 0.055f), new Vector3(4.0f, 2.4f, 0.09f), matMetal);
        CreateBox(area.transform, "Hazard_Warning_Stripe", new Vector3(32f, 0.4f, maxZ - 0.03f), new Vector3(4.0f, 0.3f, 0.04f), matWarningStripe);
        PlacePropSafe("power_box_01", propsParent, new Vector3(31f, 1.8f, maxZ - 0.2f), Quaternion.identity, AlignTarget.WallZ, maxZ);
        PlacePropSafe("power_box_01", propsParent, new Vector3(33f, 1.8f, maxZ - 0.2f), Quaternion.identity, AlignTarget.WallZ, maxZ);

        // Storage Shelves and Barrels
        PlacePropSafe("Shelf_01", propsParent, new Vector3(40f, 0f, 3.0f), Quaternion.identity, AlignTarget.Floor);
        PlacePropSafe("Barrel_01", propsParent, new Vector3(44f, 0f, 3.5f), Quaternion.identity, AlignTarget.Floor);
        PlacePropSafe("Barrel_02", propsParent, new Vector3(45f, 0f, 3.2f), Quaternion.identity, AlignTarget.Floor);
        PlacePropSafe("tool_cart", propsParent, new Vector3(29f, 0f, 5.0f), Quaternion.Euler(0, 30, 0), AlignTarget.Floor);
        PlacePropSafe("metal_toolbox", propsParent, new Vector3(36.8f, 0.95f, 11f), Quaternion.identity, AlignTarget.Floor);

        // Atmospheric Dark Lighting with physical fixtures (Room is MAJORITLY DARK per user request)
        CreateCagedHangingLamp(
            area.transform, "Fuse_Workbench_HangingLamp",
            new Vector3(36f, 2.5f, 11f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(0.85f, 0.75f, 0.55f), 0.15f, 4.5f, castShadows: false);

        CreateWallCagedSconce(
            area.transform, "Breaker_WallSconce",
            new Vector3(32f, 3.1f, maxZ - 0.05f),
            Quaternion.Euler(0f, 180f, 0f),
            matMetal, matAmberGlow, lightsParent,
            new Color(1.0f, 0.50f, 0.10f), 0.30f, 4.0f);

        CreateCagedHangingLamp(
            area.transform, "FuseRoom_HangingFillLamp",
            new Vector3(36f, 3.6f, 6f),
            height,
            matMetal, matBulbGlow, lightsParent,
            new Color(0.35f, 0.40f, 0.50f), 0.04f, 4.0f, castShadows: false);
    }

    // =========================================================================
    // AREA 8: FINAL HALLWAY & EXIT ELEVATOR ROOM
    // =========================================================================
    private static void BuildArea8_ExitElevatorRoom(Transform envParent, Transform ceilParent, Transform propsParent, Transform lightsParent)
    {
        GameObject area = new GameObject("08_ExitElevatorRoom");
        area.transform.SetParent(envParent, false);

        float height = 5.5f;

        // FINAL HALLWAY: Runs West from X = 24 to X = 12 (ONE FOR PATHWAY FLOORS)
        GameObject finalHall = new GameObject("FinalHallway");
        finalHall.transform.SetParent(area.transform, false);

        // Floor & Ceiling overlap junctions with Area 7 and Area 8
        CreateBox(finalHall.transform, "Floor", new Vector3(18f, -0.25f, 11f), new Vector3(13.0f, 0.5f, 7.2f), matPathwayFloor);
        CreateBox(ceilParent, "FinalHall_Ceiling", new Vector3(18f, 4.8f + 0.25f, 11f), new Vector3(13.0f, 0.5f, 7.2f), matCeiling);
        CreateBox(finalHall.transform, "Wall_North", new Vector3(18f, 2.4f, 14.25f), new Vector3(13.0f, 4.8f, 0.5f), matWall);
        CreateBox(finalHall.transform, "Wall_South", new Vector3(18f, 2.4f, 7.75f), new Vector3(13.0f, 4.8f, 0.5f), matWall);

        // EXIT ELEVATOR ROOM: Center (0, 0, 11). Size: 24m wide (X: -12 to 12) × 22m deep (Z: 0 to 22), Height: 5.5m (ONE FOR ROOM FLOORS)
        float minX = -12f, maxX = 12f;
        float minZ = 0f, maxZ = 22f;

        // Floor & Ceiling overlap exterior walls to eliminate void seams
        CreateBox(area.transform, "Floor", new Vector3(0f, -0.25f, 11f), new Vector3(25.5f, 0.5f, 23.5f), matRoomFloor);
        CreateBox(ceilParent, "ExitRoom_Ceiling", new Vector3(0f, height + 0.25f, 11f), new Vector3(25.5f, 0.5f, 23.5f), matCeiling);

        // North Wall: (Separating wall between Exit Elevator Room and Crash Room - 15m solid separation!)
        CreateBox(area.transform, "Wall_North", new Vector3(0f, height * 0.5f, maxZ + 0.25f), new Vector3(25.5f, height, 0.5f), matWall);
        CreateBox(area.transform, "Wall_South", new Vector3(0f, height * 0.5f, minZ - 0.25f), new Vector3(25.5f, height, 0.5f), matWall);

        // East Wall: Has doorway from Final Hallway at Z: 8 to 14
        CreateBox(area.transform, "Wall_East_South", new Vector3(maxX + 0.25f, height * 0.5f, 4f), new Vector3(0.5f, height, 8.5f), matWall);
        CreateBox(area.transform, "Wall_East_North", new Vector3(maxX + 0.25f, height * 0.5f, 18f), new Vector3(0.5f, height, 8.5f), matWall);
        CreateBox(area.transform, "Wall_East_Header", new Vector3(maxX + 0.25f, 4.2f + (height - 4.2f) * 0.5f, 11f), new Vector3(0.5f, height - 4.2f, 6.2f), matWall);

        // West Wall: Framed around the enlarged EXIT ELEVATOR opening (1.35x scale)
        // Opening between Z: 9.40m and 12.60m (3.20m wide). Doorway header above Y: 3.40m
        CreateBox(area.transform, "Wall_West_South", new Vector3(minX - 0.25f, height * 0.5f, 4.70f), new Vector3(0.5f, height, 9.40f), matWall);
        CreateBox(area.transform, "Wall_West_North", new Vector3(minX - 0.25f, height * 0.5f, 17.30f), new Vector3(0.5f, height, 9.40f), matWall);
        CreateBox(area.transform, "Wall_West_Header", new Vector3(minX - 0.25f, 4.45f, 11f), new Vector3(0.5f, 2.10f, 3.20f), matWall);

        // Exterior Elevator Shaft Enclosure behind West wall (sealing the recessed elevator from exterior void)
        CreateBox(area.transform, "Shaft_Wall_West", new Vector3(-15.80f, height * 0.5f, 11f), new Vector3(0.5f, height, 4.60f), matWall);
        CreateBox(area.transform, "Shaft_Wall_South", new Vector3(-13.90f, height * 0.5f, 8.70f), new Vector3(3.80f, height, 0.5f), matWall);
        CreateBox(area.transform, "Shaft_Wall_North", new Vector3(-13.90f, height * 0.5f, 13.30f), new Vector3(3.80f, height, 0.5f), matWall);
        CreateBox(area.transform, "Shaft_Floor", new Vector3(-13.90f, -0.25f, 11f), new Vector3(3.80f, 0.5f, 4.60f), matRoomFloor);

        // STANDING EXIT ELEVATOR (User's Custom 3D Hero Asset)
        // Scaled to 1.35x for comfortable 4-player team boarding
        // Positioned at X = -13.43f so the sliding doors sit parallel and flush with the West wall at X = -12.0m!
        GameObject elevPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReadyMadeAssets/Elevator/Elevator.fbx");
        GameObject fusePrefabForSockets = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReadyMadeAssets/FuseCell/SM_FuseCell.fbx");
        if (elevPrefab != null)
        {
            GameObject exitElevator = Object.Instantiate(elevPrefab, area.transform);
            exitElevator.name = "Working_Elevator";
            exitElevator.transform.localScale = new Vector3(135f, 135f, 135f);
            exitElevator.transform.position = new Vector3(-13.43f, 0f, 11f);
            exitElevator.transform.rotation = Quaternion.Euler(-90f, 90f, 0f);

            // Bind authentic PBR materials matching Blender design identically
            ExitElevatorMaterialSetup.ApplyMaterialsToElevator(exitElevator);

            // Atmospheric fixture lighting matching the physical design
            GameObject elevLights = new GameObject("Elevator_Local_Lights");
            elevLights.transform.SetParent(exitElevator.transform, false);

            // Door flanking lights (warm-white fluorescent)
            GameObject leftLightGo = new GameObject("Light_Door_Left");
            leftLightGo.transform.SetParent(elevLights.transform, false);
            leftLightGo.transform.position = new Vector3(-12.15f, 1.70f, 11.95f);
            Light lDoorLeft = leftLightGo.AddComponent<Light>();
            lDoorLeft.type = LightType.Point;
            lDoorLeft.color = new Color(1.0f, 0.95f, 0.85f);
            lDoorLeft.intensity = 2.2f;
            lDoorLeft.range = 3.5f;

            GameObject rightLightGo = new GameObject("Light_Door_Right");
            rightLightGo.transform.SetParent(elevLights.transform, false);
            rightLightGo.transform.position = new Vector3(-12.15f, 1.70f, 10.05f);
            Light lDoorRight = rightLightGo.AddComponent<Light>();
            lDoorRight.type = LightType.Point;
            lDoorRight.color = new Color(1.0f, 0.95f, 0.85f);
            lDoorRight.intensity = 2.2f;
            lDoorRight.range = 3.5f;

            // Amber Marquee Chevrons Light
            GameObject marqueeLightGo = new GameObject("Light_Marquee_Amber");
            marqueeLightGo.transform.SetParent(elevLights.transform, false);
            marqueeLightGo.transform.position = new Vector3(-12.15f, 3.45f, 11.0f);
            Light lMarquee = marqueeLightGo.AddComponent<Light>();
            lMarquee.type = LightType.Point;
            lMarquee.color = new Color(1.0f, 0.75f, 0.20f);
            lMarquee.intensity = 2.5f;
            lMarquee.range = 3.0f;

            // Add colliders for walls, floor, roof, and sliding doors
            AddElevatorColliders(exitElevator);

            ElevatorPowerSocket powerSocket = exitElevator.AddComponent<ElevatorPowerSocket>();
            powerSocket.FuseCellPrefab = fusePrefabForSockets;

            // Remove legacy FBX Animation component so baked root tracks never teleport elevator to (0,0,0)
            Animation anim = exitElevator.GetComponent<Animation>();
            if (anim != null)
            {
                Object.DestroyImmediate(anim);
            }

            // Power socket setup: Socket 3 interaction trigger (scaled for 100x parent scale -> 0.5m world radius)
            Transform s3 = exitElevator.transform.Find("Exterior_Front_Assembly/Front_RightPillar_Base/PowerModule_MainHousing/Socket_03");
            if (s3 != null)
            {
                SphereCollider s3Col = s3.gameObject.AddComponent<SphereCollider>();
                s3Col.radius = 0.005f; // 0.005 * 100 = 0.5m in world space
                s3Col.isTrigger = true;
            }

            // Interior cabin finish trigger
            GameObject cabinTriggerGo = new GameObject("Cabin_Finish_Trigger");
            cabinTriggerGo.transform.SetParent(exitElevator.transform, false);
            cabinTriggerGo.transform.position = new Vector3(-13.0f, 1.2f, 11.0f);
            BoxCollider cabinCol = cabinTriggerGo.AddComponent<BoxCollider>();
            cabinCol.isTrigger = true;
            cabinCol.size = new Vector3(0.016f, 0.022f, 0.016f);
            cabinCol.enabled = false;
            powerSocket.CabinFinishTrigger = cabinCol;

            // Inside Elevator Close Button mounted on the interior console
            Transform console = exitElevator.transform.Find("Interior_Assembly/Interior_ControlPanel_Console");
            if (console != null)
            {
                GameObject btnObj = new GameObject("Elevator_Close_Button");
                btnObj.transform.SetParent(area.transform, false);
                btnObj.transform.rotation = Quaternion.identity;
                btnObj.transform.position = new Vector3(-12.70f, 1.05f, 11.84f);
                btnObj.transform.localScale = Vector3.one;

                // 3D Button Cap
                GameObject btnCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btnCap.name = "Button_Cap";
                btnCap.transform.SetParent(btnObj.transform, false);
                btnCap.transform.localRotation = Quaternion.identity;
                btnCap.transform.localScale = new Vector3(0.08f, 0.08f, 0.03f);
                Renderer capR = btnCap.GetComponent<Renderer>();
                Material btnMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                btnMat.SetColor("_BaseColor", new Color(0.2f, 1.0f, 0.4f));
                btnMat.EnableKeyword("_EMISSION");
                btnMat.SetColor("_EmissionColor", new Color(0.2f, 1.0f, 0.4f) * 3.5f);
                capR.material = btnMat;

                // Subtle button beacon light illuminating the console
                GameObject btnGlow = new GameObject("Button_Glow");
                btnGlow.transform.SetParent(btnObj.transform, false);
                Light bLight = btnGlow.AddComponent<Light>();
                bLight.type = LightType.Point;
                bLight.color = new Color(0.2f, 1.0f, 0.4f);
                bLight.intensity = 1.2f;
                bLight.range = 0.6f;

                // Sphere trigger collider for interaction (2.0m radius in world space)
                SphereCollider btnCol = btnObj.AddComponent<SphereCollider>();
                btnCol.radius = 2.0f;
                btnCol.isTrigger = true;

                // Close Button Logic
                ElevatorInteriorCloseButton closeBtn = btnObj.AddComponent<ElevatorInteriorCloseButton>();
                closeBtn.SetElevator(powerSocket);
                closeBtn.SetButtonCapRenderer(capR);
            }

            // Root trigger collider on exitElevator to satisfy [RequireComponent(typeof(Collider))]
            BoxCollider rootCol = exitElevator.AddComponent<BoxCollider>();
            rootCol.isTrigger = true;
            rootCol.center = new Vector3(0f, 0.011f, 0f);
            rootCol.size = new Vector3(0.022f, 0.024f, 0.022f);

            // ElevatorObjective for HUD tracking
            ElevatorObjective elevObj = exitElevator.AddComponent<ElevatorObjective>();
            if (elevObj != null)
            {
                elevObj.powered = false;
                elevObj.complete = false;
            }

            // Initialize sockets: pre-installs 2 fuse cells in Socket 1 & 2, leaving Socket 3 empty
            powerSocket.FindReferencesIfNeeded();
            powerSocket.InitializeSockets();
        }

        // 4-Player Gathering Mat on floor - offset cleanly above floor slab to prevent bottom-face Z-fighting
        CreateBox(area.transform, "Gathering_Floor_Mat", new Vector3(-9.5f, 0.015f, 11f), new Vector3(5.0f, 0.025f, 5.0f), matWarningStripe);

        // Waiting Benches
        PlacePropSafe("SchoolChair_01", propsParent, new Vector3(0f, 0f, 2.0f), Quaternion.identity, AlignTarget.Floor);
        PlacePropSafe("SchoolChair_01", propsParent, new Vector3(2.5f, 0f, 2.0f), Quaternion.identity, AlignTarget.Floor);
        PlacePropSafe("SchoolChair_01", propsParent, new Vector3(5.0f, 0f, 2.0f), Quaternion.identity, AlignTarget.Floor);

        // Broad, even illumination on the Exit Elevator with physical floodlight & fluorescent fixtures
        CreateIndustrialFloodlight(
            area.transform, "ExitElevator_Floodlight",
            new Vector3(-6.0f, 5.2f, 11f),
            Quaternion.Euler(45f, -90f, 0f),
            matMetal, matBulbGlow, lightsParent,
            new Color(1.0f, 0.95f, 0.85f), 3.2f, 18f, 95f, castShadows: true);

        // Balanced ambient room fill light so architecture and elevator cab body are evenly illuminated
        GameObject exitFillGo = new GameObject("Area8_Room_FillLight");
        exitFillGo.transform.SetParent(lightsParent, false);
        exitFillGo.transform.position = new Vector3(-4.0f, 3.2f, 11.0f);
        Light exitFillLight = exitFillGo.AddComponent<Light>();
        exitFillLight.type = LightType.Point;
        exitFillLight.color = new Color(0.88f, 0.92f, 0.96f);
        exitFillLight.intensity = 2.0f;
        exitFillLight.range = 16f;
        exitFillLight.shadows = LightShadows.None;

        CreateFluorescentTubeFixture(
            area.transform, "ExitRoom_Tube_Fill",
            new Vector3(2f, 5.35f, 11f),
            Quaternion.Euler(0, 90, 0),
            matMetal, matTubeGlow, lightsParent,
            new Color(0.88f, 0.92f, 0.96f), 8.5f, 14f, isDamaged: false);


        CreateFluorescentTubeFixture(
            finalHall.transform, "FinalHall_Tube",
            new Vector3(18f, 4.65f, 11f),
            Quaternion.identity,
            matMetal, matTubeGlow, lightsParent,
            new Color(0.85f, 0.88f, 0.92f), 8.5f, 14f, isDamaged: false);
    }

    private static void AddElevatorColliders(GameObject exitElevator)
    {
        if (exitElevator == null) return;

        string[] meshParts = new string[]
        {
            "Base_SkidChassis/Base_FloorThreshold",
            "Interior_Assembly/Interior_Floor_Treadplate",
            "Interior_Assembly/Interior_Wall_Left",
            "Interior_Assembly/Interior_Wall_Rear",
            "Interior_Assembly/Interior_Wall_Right",
            "Interior_Assembly/Interior_Ceiling_Panel",
            "Exterior_Left_Assembly",
            "Exterior_Rear_Assembly",
            "Exterior_Right_Assembly",
            "Top_RoofSlab"
        };

        for (int i = 0; i < meshParts.Length; i++)
        {
            Transform t = exitElevator.transform.Find(meshParts[i]);
            if (t != null)
            {
                MeshFilter[] mfs = t.GetComponentsInChildren<MeshFilter>();
                for (int m = 0; m < mfs.Length; m++)
                {
                    if (mfs[m].sharedMesh != null && mfs[m].GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mfs[m].gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mfs[m].sharedMesh;
                    }
                }
            }
        }

        // Sliding door convex mesh colliders
        Transform ld = exitElevator.transform.Find("Exterior_Front_Assembly/LeftSlidingDoor");
        if (ld != null)
        {
            MeshFilter mf = ld.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }

        Transform rd = exitElevator.transform.Find("Exterior_Front_Assembly/RightSlidingDoor");
        if (rd != null)
        {
            MeshFilter mf = rd.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }
    }

    // =========================================================================
    // CYLINDRICAL PLAYER CHARACTER
    // =========================================================================
    private static void BuildPlayerCharacter(Transform parent)
    {
        SetupFourPlayerCharacters.Build();
    }

    // =========================================================================
    // OVERVIEW CAMERAS
    // =========================================================================
    private static void BuildOverviewCameras(Transform parent)
    {
        GameObject existing = GameObject.Find("OverviewCamera");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        GameObject existingOrtho = GameObject.Find("OverviewCamera_Ortho");
        if (existingOrtho != null) Undo.DestroyObjectImmediate(existingOrtho);

        // 1. Perspective Overview (angled from South looking North)
        GameObject camGo = new GameObject("OverviewCamera");
        camGo.transform.SetParent(parent, false);
        camGo.transform.position = new Vector3(45f, 65f, 5f);
        camGo.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        Camera cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 1.0f;
        cam.farClipPlane = 250f;
        cam.fieldOfView = 65f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.012f, 0.012f, 0.015f);
        cam.enabled = false;

        // 2. Orthographic Top-Down (True Floorplan view matching reference map)
        GameObject camOrthoGo = new GameObject("OverviewCamera_Ortho");
        camOrthoGo.transform.SetParent(parent, false);
        camOrthoGo.transform.position = new Vector3(45f, 85f, 31f);
        camOrthoGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Camera camOrtho = camOrthoGo.AddComponent<Camera>();
        camOrtho.orthographic = true;
        camOrtho.orthographicSize = 40f;
        camOrtho.nearClipPlane = 1.0f;
        camOrtho.farClipPlane = 200f;
        camOrtho.clearFlags = CameraClearFlags.SolidColor;
        camOrtho.backgroundColor = new Color(0.012f, 0.012f, 0.015f);
        camOrtho.enabled = false;
    }

    // =========================================================================
    // GEOMETRY HELPERS (World-Unit UV Box Meshes for Zero Texture Stretching)
    // =========================================================================
    public static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    public static Mesh GetOrCreateBoxMesh(Vector3 size, float uvScale = 0.5f, float topUvOffsetU = 0f)
    {
        string key = $"Box_{size.x:F2}_{size.y:F2}_{size.z:F2}_{uvScale:F2}_{topUvOffsetU:F3}";
        if (meshCache.TryGetValue(key, out Mesh existing) && existing != null)
        {
            return existing;
        }

        Mesh mesh = new Mesh();
        mesh.name = key;

        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;

        Vector3[] vertices = new Vector3[24];
        Vector3[] normals = new Vector3[24];
        Vector2[] uvs = new Vector2[24];
        Vector4[] tangents = new Vector4[24];
        int[] triangles = new int[36];

        // 0: Top (+Y)
        BuildQuadFace(0, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx,  hy, -hz),
            new Vector3( hx,  hy, -hz),
            new Vector3( hx,  hy,  hz),
            new Vector3(-hx,  hy,  hz),
            Vector3.up, new Vector4(1f, 0f, 0f, 1f), size.x, size.z, uvScale, topUvOffsetU);

        // 1: Bottom (-Y)
        BuildQuadFace(1, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx, -hy,  hz),
            new Vector3( hx, -hy,  hz),
            new Vector3( hx, -hy, -hz),
            new Vector3(-hx, -hy, -hz),
            Vector3.down, new Vector4(1f, 0f, 0f, 1f), size.x, size.z, uvScale);

        // 2: North / Front (+Z)
        BuildQuadFace(2, vertices, normals, uvs, tangents, triangles,
            new Vector3( hx, -hy, hz),
            new Vector3(-hx, -hy, hz),
            new Vector3(-hx,  hy, hz),
            new Vector3( hx,  hy, hz),
            Vector3.forward, new Vector4(-1f, 0f, 0f, 1f), size.x, size.y, uvScale);

        // 3: South / Back (-Z)
        BuildQuadFace(3, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx, -hy, -hz),
            new Vector3( hx, -hy, -hz),
            new Vector3( hx,  hy, -hz),
            new Vector3(-hx,  hy, -hz),
            Vector3.back, new Vector4(1f, 0f, 0f, 1f), size.x, size.y, uvScale);

        // 4: East / Right (+X)
        BuildQuadFace(4, vertices, normals, uvs, tangents, triangles,
            new Vector3(hx, -hy, -hz),
            new Vector3(hx, -hy,  hz),
            new Vector3(hx,  hy,  hz),
            new Vector3(hx,  hy, -hz),
            Vector3.right, new Vector4(0f, 0f, 1f, 1f), size.z, size.y, uvScale);

        // 5: West / Left (-X)
        BuildQuadFace(5, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx, -hy,  hz),
            new Vector3(-hx, -hy, -hz),
            new Vector3(-hx,  hy, -hz),
            new Vector3(-hx,  hy,  hz),
            Vector3.left, new Vector4(0f, 0f, -1f, 1f), size.z, size.y, uvScale);

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        meshCache[key] = mesh;
        return mesh;
    }

    private static void BuildQuadFace(
        int quadIdx,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Vector4[] tangents,
        int[] triangles,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 norm, Vector4 tan,
        float uLen, float vLen,
        float uvScale,
        float uvOffsetU = 0f)
    {
        int vBase = quadIdx * 4;
        vertices[vBase + 0] = v0;
        vertices[vBase + 1] = v1;
        vertices[vBase + 2] = v2;
        vertices[vBase + 3] = v3;

        normals[vBase + 0] = norm;
        normals[vBase + 1] = norm;
        normals[vBase + 2] = norm;
        normals[vBase + 3] = norm;

        tangents[vBase + 0] = tan;
        tangents[vBase + 1] = tan;
        tangents[vBase + 2] = tan;
        tangents[vBase + 3] = tan;

        float uMax = uLen * uvScale;
        float vMax = vLen * uvScale;

        uvs[vBase + 0] = new Vector2(uvOffsetU, 0f);
        uvs[vBase + 1] = new Vector2(uvOffsetU + uMax, 0f);
        uvs[vBase + 2] = new Vector2(uvOffsetU + uMax, vMax);
        uvs[vBase + 3] = new Vector2(uvOffsetU, vMax);

        // Clockwise winding for outward-facing normals (Unity Cull Back convention)
        int tBase = quadIdx * 6;
        triangles[tBase + 0] = vBase + 0;
        triangles[tBase + 1] = vBase + 2;
        triangles[tBase + 2] = vBase + 1;
        triangles[tBase + 3] = vBase + 0;
        triangles[tBase + 4] = vBase + 3;
        triangles[tBase + 5] = vBase + 2;
    }

    public static void SaveMeshesToAsset()
    {
        string dir = "Assets/Meshes";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }
        string assetPath = "Assets/Meshes/LevelGeometryMeshes.asset";
        
        var existingMain = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (existingMain == null)
        {
            Mesh first = null;
            foreach (var kvp in meshCache)
            {
                if (kvp.Value == null) continue;
                if (first == null)
                {
                    first = kvp.Value;
                    AssetDatabase.CreateAsset(first, assetPath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(kvp.Value, assetPath);
                }
            }
        }
        else
        {
            foreach (var kvp in meshCache)
            {
                if (kvp.Value != null && !AssetDatabase.Contains(kvp.Value))
                {
                    AssetDatabase.AddObjectToAsset(kvp.Value, assetPath);
                }
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static Mesh GetOrCreateSignMesh(Vector3 size, Vector3 outwardNormal)
    {
        string key = $"Sign_{size.x:F3}_{size.y:F3}_{size.z:F3}_{outwardNormal.x:F0}_{outwardNormal.y:F0}_{outwardNormal.z:F0}";
        if (meshCache.TryGetValue(key, out Mesh existing) && existing != null)
        {
            return existing;
        }

        Mesh mesh = new Mesh();
        mesh.name = key;

        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;

        Vector3[] vertices = new Vector3[24];
        Vector3[] normals = new Vector3[24];
        Vector2[] uvs = new Vector2[24];
        Vector4[] tangents = new Vector4[24];
        int[] triangles = new int[36];

        // 0: Top (+Y)
        BuildSignQuadFace(0, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx,  hy, -hz),
            new Vector3( hx,  hy, -hz),
            new Vector3( hx,  hy,  hz),
            new Vector3(-hx,  hy,  hz),
            Vector3.up, new Vector4(1f, 0f, 0f, 1f), Vector3.Dot(Vector3.up, outwardNormal) > 0.9f);

        // 1: Bottom (-Y)
        BuildSignQuadFace(1, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx, -hy,  hz),
            new Vector3( hx, -hy,  hz),
            new Vector3( hx, -hy, -hz),
            new Vector3(-hx, -hy, -hz),
            Vector3.down, new Vector4(1f, 0f, 0f, 1f), Vector3.Dot(Vector3.down, outwardNormal) > 0.9f);

        // 2: North / Front (+Z)
        BuildSignQuadFace(2, vertices, normals, uvs, tangents, triangles,
            new Vector3( hx, -hy, hz),
            new Vector3(-hx, -hy, hz),
            new Vector3(-hx,  hy, hz),
            new Vector3( hx,  hy, hz),
            Vector3.forward, new Vector4(-1f, 0f, 0f, 1f), Vector3.Dot(Vector3.forward, outwardNormal) > 0.9f);

        // 3: South / Back (-Z)
        BuildSignQuadFace(3, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx, -hy, -hz),
            new Vector3( hx, -hy, -hz),
            new Vector3( hx,  hy, -hz),
            new Vector3(-hx,  hy, -hz),
            Vector3.back, new Vector4(1f, 0f, 0f, 1f), Vector3.Dot(Vector3.back, outwardNormal) > 0.9f);

        // 4: East / Right (+X)
        BuildSignQuadFace(4, vertices, normals, uvs, tangents, triangles,
            new Vector3(hx, -hy, -hz),
            new Vector3(hx, -hy,  hz),
            new Vector3(hx,  hy,  hz),
            new Vector3(hx,  hy, -hz),
            Vector3.right, new Vector4(0f, 0f, 1f, 1f), Vector3.Dot(Vector3.right, outwardNormal) > 0.9f);

        // 5: West / Left (-X)
        BuildSignQuadFace(5, vertices, normals, uvs, tangents, triangles,
            new Vector3(-hx, -hy,  hz),
            new Vector3(-hx, -hy, -hz),
            new Vector3(-hx,  hy, -hz),
            new Vector3(-hx,  hy,  hz),
            Vector3.left, new Vector4(0f, 0f, -1f, 1f), Vector3.Dot(Vector3.left, outwardNormal) > 0.9f);

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        meshCache[key] = mesh;
        return mesh;
    }

    public static Mesh GetOrCreateQuadMesh(Vector3 size, Vector3 normal, float uvScale = 0.5f, float uvOffsetU = 0f)
    {
        string key = $"Quad_{size.x:F3}_{size.y:F3}_{size.z:F3}_{normal.x:F0}_{normal.y:F0}_{normal.z:F0}_{uvScale:F2}_{uvOffsetU:F3}";
        if (meshCache.TryGetValue(key, out Mesh existing) && existing != null)
        {
            return existing;
        }

        Mesh mesh = new Mesh();
        mesh.name = key;

        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;

        Vector3[] vertices = new Vector3[4];
        Vector3[] normals = new Vector3[4];
        Vector2[] uvs = new Vector2[4];
        Vector4[] tangents = new Vector4[4];
        int[] triangles = new int[6];

        float uLen = 1f, vLen = 1f;

        if (normal.x < -0.5f) // West (-X): width along Z, height along Y
        {
            vertices[0] = new Vector3(0f, -hy,  hz);
            vertices[1] = new Vector3(0f, -hy, -hz);
            vertices[2] = new Vector3(0f,  hy, -hz);
            vertices[3] = new Vector3(0f,  hy,  hz);
            uLen = size.z; vLen = size.y;
            for (int i = 0; i < 4; i++)
            {
                normals[i] = Vector3.left;
                tangents[i] = new Vector4(0f, 0f, -1f, 1f);
            }
        }
        else if (normal.x > 0.5f) // East (+X): width along Z, height along Y
        {
            vertices[0] = new Vector3(0f, -hy, -hz);
            vertices[1] = new Vector3(0f, -hy,  hz);
            vertices[2] = new Vector3(0f,  hy,  hz);
            vertices[3] = new Vector3(0f,  hy, -hz);
            uLen = size.z; vLen = size.y;
            for (int i = 0; i < 4; i++)
            {
                normals[i] = Vector3.right;
                tangents[i] = new Vector4(0f, 0f, 1f, 1f);
            }
        }
        else // Top (+Y): width along X, length along Z
        {
            vertices[0] = new Vector3(-hx, 0f, -hz);
            vertices[1] = new Vector3( hx, 0f, -hz);
            vertices[2] = new Vector3( hx, 0f,  hz);
            vertices[3] = new Vector3(-hx, 0f,  hz);
            uLen = size.x; vLen = size.z;
            for (int i = 0; i < 4; i++)
            {
                normals[i] = Vector3.up;
                tangents[i] = new Vector4(1f, 0f, 0f, 1f);
            }
        }

        float uMax = uLen * uvScale;
        float vMax = vLen * uvScale;

        uvs[0] = new Vector2(uvOffsetU, 0f);
        uvs[1] = new Vector2(uvOffsetU + uMax, 0f);
        uvs[2] = new Vector2(uvOffsetU + uMax, vMax);
        uvs[3] = new Vector2(uvOffsetU, vMax);

        triangles[0] = 0;
        triangles[1] = 2;
        triangles[2] = 1;
        triangles[3] = 0;
        triangles[4] = 3;
        triangles[5] = 2;

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        meshCache[key] = mesh;
        return mesh;
    }

    private static void BuildSignQuadFace(
        int quadIdx,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Vector4[] tangents,
        int[] triangles,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 norm, Vector4 tan,
        bool isFront)
    {
        int vBase = quadIdx * 4;
        vertices[vBase + 0] = v0;
        vertices[vBase + 1] = v1;
        vertices[vBase + 2] = v2;
        vertices[vBase + 3] = v3;

        normals[vBase + 0] = norm;
        normals[vBase + 1] = norm;
        normals[vBase + 2] = norm;
        normals[vBase + 3] = norm;

        tangents[vBase + 0] = tan;
        tangents[vBase + 1] = tan;
        tangents[vBase + 2] = tan;
        tangents[vBase + 3] = tan;

        if (isFront)
        {
            uvs[vBase + 0] = new Vector2(0f, 0f);
            uvs[vBase + 1] = new Vector2(1f, 0f);
            uvs[vBase + 2] = new Vector2(1f, 1f);
            uvs[vBase + 3] = new Vector2(0f, 1f);
        }
        else
        {
            uvs[vBase + 0] = new Vector2(0.01f, 0.01f);
            uvs[vBase + 1] = new Vector2(0.02f, 0.01f);
            uvs[vBase + 2] = new Vector2(0.02f, 0.02f);
            uvs[vBase + 3] = new Vector2(0.01f, 0.02f);
        }

        int tBase = quadIdx * 6;
        triangles[tBase + 0] = vBase + 0;
        triangles[tBase + 1] = vBase + 2;
        triangles[tBase + 2] = vBase + 1;
        triangles[tBase + 3] = vBase + 0;
        triangles[tBase + 4] = vBase + 3;
        triangles[tBase + 5] = vBase + 2;
    }

    private static GameObject CreateSignPlate(
        Transform parent,
        string name,
        Vector3 worldPos,
        Vector3 size,
        Vector3 outwardNormal,
        Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateSignMesh(size, outwardNormal);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.center = Vector3.zero;

        return go;
    }

    private static GameObject CreateQuad(
        Transform parent,
        string name,
        Vector3 worldPos,
        Vector3 size,
        Vector3 normal,
        Material mat,
        float uvScale = 0.5f,
        float topUvOffsetU = 0f,
        bool addCollider = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateQuadMesh(size, normal, uvScale, topUvOffsetU);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        if (addCollider)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.center = Vector3.zero;
        }

        return go;
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat, float uvScale = 0.5f, float topUvOffsetU = 0f, bool addCollider = true)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateBoxMesh(size, uvScale, topUvOffsetU);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        if (addCollider)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.center = Vector3.zero;
        }

        return go;
    }

    private static GameObject CreateBoxLocal(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, float uvScale = 0.5f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateBoxMesh(size, uvScale);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.center = Vector3.zero;

        return go;
    }

    private static GameObject CreateCylinder(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject CreateBoxRotated(Transform parent, string name, Vector3 worldPos, Vector3 size, Quaternion rot, Material mat, float uvScale = 0.5f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.rotation = rot;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateBoxMesh(size, uvScale);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.center = Vector3.zero;

        return go;
    }

    private static GameObject CreateBoxLocalRotated(Transform parent, string name, Vector3 localPos, Vector3 size, Quaternion localRot, Material mat, float uvScale = 0.5f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateBoxMesh(size, uvScale);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.center = Vector3.zero;

        return go;
    }

    private static GameObject CreateCylinderRotated(Transform parent, string name, Vector3 worldPos, Vector3 size, Quaternion rot, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.rotation = rot;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject CreateCylinderLocalRotated(Transform parent, string name, Vector3 localPos, Vector3 size, Quaternion localRot, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject CreatePointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return go;
    }

    private static GameObject CreateSpotLight(Transform parent, string name, Vector3 pos, Quaternion rot, Color color, float intensity, float range, float spotAngle, LightShadows shadows = LightShadows.None)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.shadows = shadows;
        return go;
    }

    private static GameObject CreateFluorescentTubeFixture(
        Transform parent,
        string name,
        Vector3 pos,
        Quaternion rot,
        Material matHousing,
        Material matTubeGlow,
        Transform lightsParent,
        Color lightColor,
        float intensity,
        float range,
        bool isDamaged = false,
        bool castShadows = true)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = pos;
        fixture.transform.rotation = rot;

        // Metal casing troffer
        CreateBoxLocal(fixture.transform, "Housing", new Vector3(0f, 0.04f, 0f), new Vector3(1.8f, 0.08f, 0.22f), matHousing);

        // Twin fluorescent tubes with emissive glow
        CreateCylinderLocalRotated(fixture.transform, "Tube_1", new Vector3(0f, -0.01f, -0.05f), new Vector3(0.035f, 1.6f, 0.035f), Quaternion.Euler(0, 0, 90), matTubeGlow);
        CreateCylinderLocalRotated(fixture.transform, "Tube_2", new Vector3(0f, -0.01f, 0.05f), new Vector3(0.035f, 1.6f, 0.035f), Quaternion.Euler(0, 0, 90), matTubeGlow);

        // End caps
        CreateBoxLocal(fixture.transform, "EndCap_L", new Vector3(-0.88f, 0.02f, 0f), new Vector3(0.06f, 0.09f, 0.24f), matHousing);
        CreateBoxLocal(fixture.transform, "EndCap_R", new Vector3(0.88f, 0.02f, 0f), new Vector3(0.06f, 0.09f, 0.24f), matHousing);

        // Diffuse fluorescent tube light underneath (Point light gives realistic 360 wrap around corridor)
        GameObject lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(lightsParent, false);
        lightGo.transform.position = pos + rot * new Vector3(0f, -0.15f, 0f);

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;

        if (isDamaged)
        {
            DynamicLightEffects dyn = lightGo.AddComponent<DynamicLightEffects>();
            dyn.CurrentEffectType = DynamicLightEffects.EffectType.Flicker;
            dyn.baseIntensity = intensity;
            dyn.minIntensity = 0.2f;
            dyn.maxIntensity = intensity * 1.35f;
            dyn.speed = 12f;
            dyn.flickerRoughness = 0.6f;
            dyn.blackoutChance = 0.06f;
        }

        return fixture;
    }

    private static GameObject CreateSphereLocal(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        return go;
    }

    private static GameObject CreateSphere(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        return go;
    }

    private static GameObject CreateCagedHangingLamp(
        Transform parent,
        string name,
        Vector3 pos,
        float ceilingY,
        Material matMetal,
        Material matBulbGlow,
        Transform lightsParent,
        Color lightColor,
        float intensity,
        float range,
        bool castShadows = false)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = pos;

        // Ceiling junction mount & hanging conduit pipe
        float dropLen = Mathf.Max(0.2f, ceilingY - pos.y);
        CreateCylinderLocalRotated(fixture.transform, "Ceil_Mount", new Vector3(0f, dropLen, 0f), new Vector3(0.25f, 0.08f, 0.25f), Quaternion.identity, matMetal);
        CreateCylinderLocalRotated(fixture.transform, "Drop_Stem", new Vector3(0f, dropLen * 0.5f, 0f), new Vector3(0.035f, dropLen, 0.035f), Quaternion.identity, matMetal);

        // Industrial Bell Shade / Lamp Cap
        CreateCylinderLocalRotated(fixture.transform, "Lamp_Cap", new Vector3(0f, 0.15f, 0f), new Vector3(0.55f, 0.12f, 0.55f), Quaternion.identity, matMetal);
        CreateCylinderLocalRotated(fixture.transform, "Lamp_Socket", new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.12f, 0.22f), Quaternion.identity, matMetal);

        // Glowing Emissive Bulb
        CreateSphereLocal(fixture.transform, "Emissive_Bulb", new Vector3(0f, -0.05f, 0f), new Vector3(0.24f, 0.32f, 0.24f), matBulbGlow);

        // Protective Metal Wire Cage
        CreateCylinderLocalRotated(fixture.transform, "Cage_Ring", new Vector3(0f, -0.15f, 0f), new Vector3(0.38f, 0.025f, 0.38f), Quaternion.identity, matMetal);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f * Mathf.Deg2Rad;
            Vector3 barPos = new Vector3(Mathf.Cos(ang) * 0.18f, -0.08f, Mathf.Sin(ang) * 0.18f);
            CreateCylinderLocalRotated(fixture.transform, "Cage_Bar_" + i, barPos, new Vector3(0.02f, 0.30f, 0.02f), Quaternion.identity, matMetal);
        }

        // Point Light
        GameObject lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(lightsParent, false);
        lightGo.transform.position = pos + new Vector3(0f, -0.05f, 0f);

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = intensity;
        light.range = range;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

        return fixture;
    }

    private static GameObject CreateWallCagedSconce(
        Transform parent,
        string name,
        Vector3 pos,
        Quaternion rot,
        Material matMetal,
        Material matBulbGlow,
        Transform lightsParent,
        Color lightColor,
        float intensity,
        float range,
        bool castShadows = false)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = pos;
        fixture.transform.rotation = rot;

        // Wall mounting backplate
        CreateBoxLocal(fixture.transform, "Backplate", new Vector3(0f, 0f, 0.02f), new Vector3(0.35f, 0.45f, 0.04f), matMetal);
        // Forward bracket arm
        CreateBoxLocal(fixture.transform, "Bracket_Arm", new Vector3(0f, 0f, 0.18f), new Vector3(0.08f, 0.08f, 0.30f), matMetal);
        // Lamp hood / socket
        CreateCylinderLocalRotated(fixture.transform, "Lamp_Hood", new Vector3(0f, 0.08f, 0.32f), new Vector3(0.36f, 0.08f, 0.36f), Quaternion.identity, matMetal);

        // Glowing Emissive Bulb
        CreateSphereLocal(fixture.transform, "Emissive_Bulb", new Vector3(0f, -0.04f, 0.32f), new Vector3(0.20f, 0.26f, 0.20f), matBulbGlow);

        // Wire cage
        CreateCylinderLocalRotated(fixture.transform, "Cage_Ring", new Vector3(0f, -0.12f, 0.32f), new Vector3(0.30f, 0.02f, 0.30f), Quaternion.identity, matMetal);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f * Mathf.Deg2Rad;
            Vector3 barPos = new Vector3(Mathf.Cos(ang) * 0.14f, -0.06f, 0.32f + Mathf.Sin(ang) * 0.14f);
            CreateCylinderLocalRotated(fixture.transform, "Cage_Bar_" + i, barPos, new Vector3(0.018f, 0.24f, 0.018f), Quaternion.identity, matMetal);
        }

        // Point Light
        GameObject lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(lightsParent, false);
        lightGo.transform.position = pos + rot * new Vector3(0f, -0.04f, 0.32f);

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = intensity;
        light.range = range;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

        return fixture;
    }

    private static GameObject CreateIndustrialFloodlight(
        Transform parent,
        string name,
        Vector3 pos,
        Quaternion rot,
        Material matMetal,
        Material matLensGlow,
        Transform lightsParent,
        Color lightColor,
        float intensity,
        float range,
        float spotAngle,
        bool castShadows = false)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = pos;
        fixture.transform.rotation = rot;

        // Base mounting clamp / swivel bracket
        CreateBoxLocal(fixture.transform, "Mount_Clamp", new Vector3(0f, 0.15f, 0f), new Vector3(0.25f, 0.10f, 0.25f), matMetal);
        CreateCylinderLocalRotated(fixture.transform, "Swivel_Yoke", new Vector3(0f, 0.05f, 0f), new Vector3(0.06f, 0.12f, 0.06f), Quaternion.identity, matMetal);

        // Rectangular floodlight housing pointing along local +Z forward
        CreateBoxLocal(fixture.transform, "Housing_Body", new Vector3(0f, 0f, 0.15f), new Vector3(0.45f, 0.35f, 0.40f), matMetal);
        CreateBoxLocal(fixture.transform, "Housing_Visor", new Vector3(0f, 0.18f, 0.32f), new Vector3(0.47f, 0.05f, 0.15f), matMetal);

        // Glowing Emissive Lens Face
        CreateBoxLocal(fixture.transform, "Emissive_Lens", new Vector3(0f, 0f, 0.35f), new Vector3(0.40f, 0.30f, 0.02f), matLensGlow);

        // Spotlight
        GameObject lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(lightsParent, false);
        lightGo.transform.position = pos + rot * new Vector3(0f, 0f, 0.38f);
        lightGo.transform.rotation = rot;

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = lightColor;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.innerSpotAngle = spotAngle * 0.5f;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

        return fixture;
    }

    private static GameObject CreateEmergencyBeacon(
        Transform parent,
        string name,
        Vector3 localPos,
        Quaternion localRot,
        Material matBase,
        Material matAmberGlow,
        Transform lightsParent,
        Color lightColor,
        float intensity,
        float range)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.localPosition = localPos;
        fixture.transform.localRotation = localRot;

        // Base flange
        CreateCylinderLocalRotated(fixture.transform, "Beacon_Base", new Vector3(0f, 0.04f, 0f), new Vector3(0.38f, 0.08f, 0.38f), Quaternion.identity, matBase);
        // Emissive Amber Dome
        CreateCylinderLocalRotated(fixture.transform, "Beacon_Dome", new Vector3(0f, 0.16f, 0f), new Vector3(0.28f, 0.18f, 0.28f), Quaternion.identity, matAmberGlow);
        // Top cap & protective cage bars
        CreateCylinderLocalRotated(fixture.transform, "Beacon_TopCap", new Vector3(0f, 0.26f, 0f), new Vector3(0.30f, 0.03f, 0.30f), Quaternion.identity, matBase);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f * Mathf.Deg2Rad;
            Vector3 cagePos = new Vector3(Mathf.Cos(ang) * 0.15f, 0.16f, Mathf.Sin(ang) * 0.15f);
            CreateCylinderLocalRotated(fixture.transform, "Cage_Bar_" + i, cagePos, new Vector3(0.015f, 0.20f, 0.015f), Quaternion.identity, matBase);
        }

        // Point Light inside the beacon
        GameObject lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(lightsParent, false);
        lightGo.transform.position = fixture.transform.position + fixture.transform.up * 0.16f;

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;

        DynamicLightEffects dyn = lightGo.AddComponent<DynamicLightEffects>();
        dyn.CurrentEffectType = DynamicLightEffects.EffectType.Flicker;
        dyn.baseIntensity = intensity;
        dyn.minIntensity = 0.5f;
        dyn.maxIntensity = intensity * 1.3f;
        dyn.speed = 15f;
        dyn.flickerRoughness = 0.75f;
        dyn.blackoutChance = 0.08f;

        return fixture;
    }

    private static GameObject CreateSparkingJunction(
        Transform parent,
        string name,
        Vector3 localPos,
        Quaternion localRot,
        Material matMetal,
        Material matSparkGlow,
        Transform lightsParent)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.localPosition = localPos;
        fixture.transform.localRotation = localRot;

        // Junction box
        CreateBoxLocal(fixture.transform, "Box", Vector3.zero, new Vector3(0.24f, 0.24f, 0.12f), matMetal);
        // Broken cover hanging open
        CreateBoxLocalRotated(fixture.transform, "Cover_Blown", new Vector3(0.12f, -0.06f, 0.08f), new Vector3(0.24f, 0.24f, 0.02f), Quaternion.Euler(0, 35f, -20f), matMetal);
        // Protruding severed wire terminal
        CreateCylinderLocalRotated(fixture.transform, "Wire_Lead", new Vector3(0f, -0.16f, 0.06f), new Vector3(0.025f, 0.14f, 0.025f), Quaternion.Euler(15f, 0, 10f), matMetal);
        // Emissive spark node at wire tip
        CreateSphereLocal(fixture.transform, "Spark_Tip", new Vector3(0.02f, -0.24f, 0.08f), new Vector3(0.06f, 0.06f, 0.06f), matSparkGlow);

        // Point light for spark
        GameObject lightSparkGo = new GameObject(name + "_Light");
        lightSparkGo.transform.SetParent(lightsParent, false);
        lightSparkGo.transform.position = fixture.transform.TransformPoint(new Vector3(0.02f, -0.24f, 0.08f));

        Light lightSpark = lightSparkGo.AddComponent<Light>();
        lightSpark.type = LightType.Point;
        lightSpark.color = new Color(0.65f, 0.85f, 1.0f);
        lightSpark.intensity = 0.5f;
        lightSpark.range = 4.5f;
        lightSpark.shadows = LightShadows.None;

        DynamicLightEffects dynSpark = lightSparkGo.AddComponent<DynamicLightEffects>();
        dynSpark.CurrentEffectType = DynamicLightEffects.EffectType.ElectricalSpark;
        dynSpark.baseIntensity = 3.2f;
        dynSpark.speed = 18f;

        return fixture;
    }

    // =========================================================================
    // SAFE PROP PLACEMENT
    // =========================================================================
    private enum AlignTarget { Floor, WallZ, Ceiling }

    private static GameObject PlacePropSafe(string modelName, Transform parent, Vector3 targetPos, Quaternion rot, AlignTarget align, float surfaceRef = 0f)
    {
        string path = PropDir + modelName + ".fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = modelName;
        inst.transform.SetParent(parent, false);
        inst.transform.position = targetPos;
        inst.transform.rotation = rot;

        Bounds b = CalculateWorldBounds(inst);
        if (b.size.sqrMagnitude < 0.0001f) return inst;

        if (align == AlignTarget.Floor)
        {
            float floorY = targetPos.y;
            float diff = floorY - b.min.y;
            inst.transform.position += Vector3.up * diff;
        }
        else if (align == AlignTarget.Ceiling)
        {
            float ceilY = surfaceRef > 0.01f ? surfaceRef : 5.0f;
            float diff = ceilY - b.max.y;
            inst.transform.position += Vector3.up * diff;
        }
        else if (align == AlignTarget.WallZ)
        {
            float diff = (surfaceRef > targetPos.z) ? (surfaceRef - 0.02f) - b.max.z : (surfaceRef + 0.02f) - b.min.z;
            inst.transform.position += Vector3.forward * diff;
        }

        if (inst.GetComponentInChildren<Collider>() == null)
        {
            // Only add collider for solid floor furniture/props that should block the player
            bool needsCollision = (align == AlignTarget.Floor) &&
                !modelName.ToLower().Contains("bag") &&
                !modelName.ToLower().Contains("crowbar") &&
                !modelName.ToLower().Contains("toolbox") &&
                !modelName.ToLower().Contains("light") &&
                !modelName.ToLower().Contains("lamp") &&
                !modelName.ToLower().Contains("pipe") &&
                !modelName.ToLower().Contains("cable") &&
                !modelName.ToLower().Contains("alarm") &&
                !modelName.ToLower().Contains("clock");

            if (needsCollision)
            {
                BoxCollider bc = inst.AddComponent<BoxCollider>();
                Bounds localB = CalculateWorldBounds(inst);
                bc.center = inst.transform.InverseTransformPoint(localB.center);
                Vector3 ls = inst.transform.lossyScale;
                bc.size = new Vector3(
                    Mathf.Abs(ls.x) > 0.0001f ? localB.size.x / Mathf.Abs(ls.x) : localB.size.x,
                    Mathf.Abs(ls.y) > 0.0001f ? localB.size.y / Mathf.Abs(ls.y) : localB.size.y,
                    Mathf.Abs(ls.z) > 0.0001f ? localB.size.z / Mathf.Abs(ls.z) : localB.size.z
                );
            }
        }

        return inst;
    }

    private static Bounds CalculateWorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }
}
