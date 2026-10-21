using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ElevatorSceneBuilder
{
    private const string MaterialDir = "Assets/Materials";
    private const string TextureDir = "Assets/Textures";

    [MenuItem("Tools/Build Crashed Elevator Scene")]
    public static void BuildScene()
    {
        // Clean up existing scene elements if re-running
        GameObject existing = GameObject.Find("CrashedElevatorScene");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject("CrashedElevatorScene");
        Undo.RegisterCreatedObjectUndo(root, "Create Crashed Elevator Scene");

        // Load generated PBR Materials
        Material matCrackedConcrete = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_CrackedConcrete.mat");
        Material matConcreteWall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_ConcreteWall.mat");
        Material matElevatorSteel = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_RustedSteel.mat");
        Material matIndustrialMetal = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_IndustrialMetal.mat");
        Material matSteelCable = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_SteelCable.mat");
        Material matWarningStripe = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_HazardWarning.mat");
        Material matEmergencyLamp = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_EmergencyLamp.mat");
        Material matFluorescent = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_FluorescentLamp.mat");
        Material matBeacon = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_EmergencyBeacon.mat");
        Material matMoss = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_Moss.mat");

        // Fallbacks if not yet loaded
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        if (matCrackedConcrete == null) matCrackedConcrete = new Material(urpLit);
        if (matConcreteWall == null) matConcreteWall = new Material(urpLit);
        if (matElevatorSteel == null) matElevatorSteel = new Material(urpLit);
        if (matIndustrialMetal == null) matIndustrialMetal = new Material(urpLit);
        if (matSteelCable == null) matSteelCable = new Material(urpLit);
        if (matWarningStripe == null) matWarningStripe = new Material(urpLit);
        if (matEmergencyLamp == null) matEmergencyLamp = new Material(urpLit);
        if (matFluorescent == null) matFluorescent = new Material(urpLit);
        if (matBeacon == null) matBeacon = new Material(urpLit);
        if (matMoss == null) matMoss = new Material(urpLit);

        // Instance material for the concrete floor with balanced smoothness and subtle normal map
        Material matFloor = new Material(matCrackedConcrete);
        matFloor.name = "PBR_ConcreteFloor_Tiled";
        matFloor.color = new Color(0.40f, 0.42f, 0.44f);
        if (matFloor.HasProperty("_BaseColor")) matFloor.SetColor("_BaseColor", matFloor.color);
        matFloor.SetTextureScale("_BaseMap", new Vector2(10f, 11f));
        if (matFloor.HasProperty("_BumpMap"))
        {
            matFloor.SetTextureScale("_BumpMap", new Vector2(10f, 11f));
            matFloor.SetFloat("_BumpScale", 0.45f);
        }
        matFloor.SetFloat("_Smoothness", 0.38f);

        // Instance material for concrete walls (smoother industrial poured finish)
        Material matWallInst = new Material(matConcreteWall);
        matWallInst.name = "PBR_ConcreteWall_Instanced";
        matWallInst.color = new Color(0.46f, 0.48f, 0.50f);
        if (matWallInst.HasProperty("_BaseColor")) matWallInst.SetColor("_BaseColor", matWallInst.color);
        matWallInst.SetTextureScale("_BaseMap", new Vector2(6f, 3f));
        if (matWallInst.HasProperty("_BumpMap"))
        {
            matWallInst.SetTextureScale("_BumpMap", new Vector2(6f, 3f));
            matWallInst.SetFloat("_BumpScale", 0.40f);
        }
        matWallInst.SetFloat("_Smoothness", 0.22f);

        // =========================================================================
        // 1. INDUSTRIAL CHAMBER / BUNKER ROOM STRUCTURE
        // =========================================================================
        GameObject roomRoot = new GameObject("Room_Industrial_Chamber");
        roomRoot.transform.parent = root.transform;

        float roomWidth = 16f;     // X: -8 to +8
        float roomDepth = 18f;     // Z: -8.5 to +9.5
        float roomHeight = 6.8f;   // Y: 0 to 6.8
        float wallThick = 0.45f;

        // --- A. Floor Slab ---
        GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorObj.name = "Room_Concrete_Floor";
        floorObj.transform.parent = roomRoot.transform;
        floorObj.transform.position = new Vector3(0, -0.25f, 0.5f);
        floorObj.transform.localScale = new Vector3(roomWidth, 0.5f, roomDepth);
        floorObj.GetComponent<Renderer>().sharedMaterial = matFloor;

        // Floor perimeter safety curb
        CreatePerimeterCurbs(roomRoot.transform, roomWidth, roomDepth, matIndustrialMetal);

        // Hazard border stripes on floor around elevator zone
        GameObject hazardFloorStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazardFloorStrip.name = "Floor_Hazard_Stripe_Left";
        hazardFloorStrip.transform.parent = roomRoot.transform;
        hazardFloorStrip.transform.position = new Vector3(-3.2f, 0.015f, 1.0f);
        hazardFloorStrip.transform.localScale = new Vector3(0.35f, 0.02f, 6.0f);
        hazardFloorStrip.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        GameObject hazardFloorStripR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazardFloorStripR.name = "Floor_Hazard_Stripe_Right";
        hazardFloorStripR.transform.parent = roomRoot.transform;
        hazardFloorStripR.transform.position = new Vector3(3.2f, 0.015f, 1.0f);
        hazardFloorStripR.transform.localScale = new Vector3(0.35f, 0.02f, 6.0f);
        hazardFloorStripR.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        // Front threshold hazard line
        GameObject hazardFloorFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazardFloorFront.name = "Floor_Hazard_Stripe_Front";
        hazardFloorFront.transform.parent = roomRoot.transform;
        hazardFloorFront.transform.position = new Vector3(0, 0.015f, -2.0f);
        hazardFloorFront.transform.localScale = new Vector3(6.75f, 0.02f, 0.35f);
        hazardFloorFront.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        // --- B. Back Wall (Z = 9.5) with Structural Buttresses ---
        GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWall.name = "Wall_Back_Concrete";
        backWall.transform.parent = roomRoot.transform;
        backWall.transform.position = new Vector3(0, roomHeight * 0.5f, 9.5f);
        backWall.transform.localScale = new Vector3(roomWidth, roomHeight, wallThick);
        backWall.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Concrete structural columns along back wall
        float[] backColX = new float[] { -6f, -2.5f, 2.5f, 6f };
        foreach (float cx in backColX)
        {
            CreateColumn(roomRoot.transform, new Vector3(cx, roomHeight * 0.5f, 9.25f), roomHeight, matWallInst, matIndustrialMetal);
        }

        // Elevator Vertical Guide Rails on Back Wall
        CreateGuideRail(roomRoot.transform, new Vector3(-1.4f, roomHeight * 0.5f, 9.15f), roomHeight, matIndustrialMetal);
        CreateGuideRail(roomRoot.transform, new Vector3( 1.4f, roomHeight * 0.5f, 9.15f), roomHeight, matIndustrialMetal);

        // --- C. Left Wall (X = -8.0) with Electrical Breaker Panels & Conduits ---
        GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "Wall_Left_Concrete";
        leftWall.transform.parent = roomRoot.transform;
        leftWall.transform.position = new Vector3(-8.0f, roomHeight * 0.5f, 0.5f);
        leftWall.transform.localScale = new Vector3(wallThick, roomHeight, roomDepth);
        leftWall.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Columns on Left Wall
        float[] sideColZ = new float[] { -5f, -1f, 3f, 7f };
        foreach (float cz in sideColZ)
        {
            CreateColumn(roomRoot.transform, new Vector3(-7.75f, roomHeight * 0.5f, cz), roomHeight, matWallInst, matIndustrialMetal);
        }

        // Electrical Breaker Cabinet with Sparks
        GameObject breakerBox = CreateElectricalCabinet(roomRoot.transform, new Vector3(-7.65f, 2.2f, -1.8f), matIndustrialMetal, matWarningStripe);

        // Overhead Cable Tray along left wall
        CreateCableTray(roomRoot.transform, new Vector3(-7.4f, 5.4f, 0.5f), roomDepth - 2f, true, matIndustrialMetal, matSteelCable);

        // --- D. Right Wall (X = +8.0) with Heavy Blast Door & Pipe Manifold ---
        GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWall.name = "Wall_Right_Concrete";
        rightWall.transform.parent = roomRoot.transform;
        rightWall.transform.position = new Vector3(8.0f, roomHeight * 0.5f, 0.5f);
        rightWall.transform.localScale = new Vector3(wallThick, roomHeight, roomDepth);
        rightWall.GetComponent<Renderer>().sharedMaterial = matWallInst;

        foreach (float cz in sideColZ)
        {
            CreateColumn(roomRoot.transform, new Vector3(7.75f, roomHeight * 0.5f, cz), roomHeight, matWallInst, matIndustrialMetal);
        }

        // Heavy Blast Airlock Door on Right Wall
        CreateBlastDoor(roomRoot.transform, new Vector3(7.75f, 1.8f, 2.5f), matIndustrialMetal, matWarningStripe);

        // Industrial Overhead Pipe Runs along Right Wall
        CreatePipeRun(roomRoot.transform, new Vector3(7.35f, 5.0f, 0.5f), roomDepth - 2f, matIndustrialMetal);

        // --- E. Front Wall (Z = -8.5) with Entrance Gateway Arch ---
        GameObject frontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontLeft.name = "Wall_Front_Left";
        frontLeft.transform.parent = roomRoot.transform;
        frontLeft.transform.position = new Vector3(-5.5f, roomHeight * 0.5f, -8.5f);
        frontLeft.transform.localScale = new Vector3(5.0f, roomHeight, wallThick);
        frontLeft.GetComponent<Renderer>().sharedMaterial = matWallInst;

        GameObject frontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontRight.name = "Wall_Front_Right";
        frontRight.transform.parent = roomRoot.transform;
        frontRight.transform.position = new Vector3(5.5f, roomHeight * 0.5f, -8.5f);
        frontRight.transform.localScale = new Vector3(5.0f, roomHeight, wallThick);
        frontRight.GetComponent<Renderer>().sharedMaterial = matWallInst;

        GameObject frontHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontHeader.name = "Wall_Front_ArchHeader";
        frontHeader.transform.parent = roomRoot.transform;
        frontHeader.transform.position = new Vector3(0, 5.2f, -8.5f);
        frontHeader.transform.localScale = new Vector3(6.0f, roomHeight - 3.6f, wallThick);
        frontHeader.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Portal Frame with warning hazard stripes
        CreatePortalFrame(roomRoot.transform, new Vector3(0, 1.8f, -8.45f), 5.8f, 3.6f, matIndustrialMetal, matWarningStripe);

        // --- F. Concrete Ceiling with Elevator Shaft Breach Opening ---
        GameObject ceilingGroup = new GameObject("Room_Ceiling_Structure");
        ceilingGroup.transform.parent = roomRoot.transform;

        float breachMinX = -1.6f;
        float breachMaxX = 2.0f;
        float breachMinZ = -0.8f;
        float breachMaxZ = 2.8f;

        // Front ceiling slab
        GameObject ceilFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceilFront.name = "Ceiling_Slab_Front";
        ceilFront.transform.parent = ceilingGroup.transform;
        float frontCeilDepth = (breachMinZ - (-8.5f));
        ceilFront.transform.position = new Vector3(0, roomHeight, -8.5f + frontCeilDepth * 0.5f);
        ceilFront.transform.localScale = new Vector3(roomWidth, wallThick, frontCeilDepth);
        ceilFront.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Back ceiling slab
        GameObject ceilBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceilBack.name = "Ceiling_Slab_Back";
        ceilBack.transform.parent = ceilingGroup.transform;
        float backCeilDepth = (9.5f - breachMaxZ);
        ceilBack.transform.position = new Vector3(0, roomHeight, breachMaxZ + backCeilDepth * 0.5f);
        ceilBack.transform.localScale = new Vector3(roomWidth, wallThick, backCeilDepth);
        ceilBack.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Left ceiling slab next to breach
        GameObject ceilLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceilLeft.name = "Ceiling_Slab_Left";
        ceilLeft.transform.parent = ceilingGroup.transform;
        float leftWidth = (breachMinX - (-8.0f));
        float midDepth = (breachMaxZ - breachMinZ);
        ceilLeft.transform.position = new Vector3(-8.0f + leftWidth * 0.5f, roomHeight, breachMinZ + midDepth * 0.5f);
        ceilLeft.transform.localScale = new Vector3(leftWidth, wallThick, midDepth);
        ceilLeft.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Right ceiling slab next to breach
        GameObject ceilRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceilRight.name = "Ceiling_Slab_Right";
        ceilRight.transform.parent = ceilingGroup.transform;
        float rightWidth = (8.0f - breachMaxX);
        ceilRight.transform.position = new Vector3(breachMaxX + rightWidth * 0.5f, roomHeight, breachMinZ + midDepth * 0.5f);
        ceilRight.transform.localScale = new Vector3(rightWidth, wallThick, midDepth);
        ceilRight.GetComponent<Renderer>().sharedMaterial = matWallInst;

        // Shattered ragged concrete chunks framing the elevator shaft ceiling breach
        CreateShatteredBreachFrame(ceilingGroup.transform, new Vector3(0.2f, roomHeight, 1.0f), 3.6f, 3.6f, matCrackedConcrete, matIndustrialMetal);

        // --- G. Vertical Elevator Shaft Chute (Extends 7m upwards into darkness!) ---
        CreateElevatorShaftTower(roomRoot.transform, new Vector3(0.2f, roomHeight, 1.0f), 3.6f, 3.6f, 7.5f, matWallInst, matIndustrialMetal);

        // Steel I-Beams / Rafters spanning the ceiling
        float[] beamZ = new float[] { -5.5f, -2.5f, 4.5f, 7.5f };
        foreach (float bz in beamZ)
        {
            CreateSteelIBeam(ceilingGroup.transform, new Vector3(0, roomHeight - 0.28f, bz), roomWidth - 0.5f, matIndustrialMetal);
        }

        // Industrial HVAC Air Duct running across the ceiling
        CreateVentilationDuct(ceilingGroup.transform, new Vector3(-3.8f, roomHeight - 0.7f, 0.5f), roomDepth - 1.5f, matIndustrialMetal);

        // =========================================================================
        // 2. IMPACT CRATER & DEBRIS ON CONCRETE FLOOR
        // =========================================================================
        GameObject craterRoot = new GameObject("Elevator_Impact_Zone");
        craterRoot.transform.parent = root.transform;

        // Dark scorch mark on concrete
        GameObject scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        scorch.name = "Impact_ScorchMark";
        scorch.transform.parent = craterRoot.transform;
        scorch.transform.position = new Vector3(0.2f, 0.02f, 0.9f);
        scorch.transform.localScale = new Vector3(5.2f, 0.01f, 5.2f);
        Material matScorch = new Material(matCrackedConcrete);
        matScorch.color = new Color(0.10f, 0.10f, 0.12f);
        if (matScorch.HasProperty("_BaseColor")) matScorch.SetColor("_BaseColor", matScorch.color);
        matScorch.SetFloat("_Smoothness", 0.45f);
        scorch.GetComponent<Renderer>().sharedMaterial = matScorch;

        // Shattered concrete slabs thrown upward by impact
        int slabCount = 18;
        for (int i = 0; i < slabCount; i++)
        {
            float angle = (i / (float)slabCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            float dist = Random.Range(1.8f, 3.9f);
            Vector3 pos = new Vector3(0.2f + Mathf.Cos(angle) * dist, 0.06f, 0.9f + Mathf.Sin(angle) * dist);

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = $"CrackedConcreteSlab_{i}";
            slab.transform.parent = craterRoot.transform;
            slab.transform.position = pos;
            slab.transform.localScale = new Vector3(Random.Range(0.6f, 1.4f), Random.Range(0.12f, 0.28f), Random.Range(0.6f, 1.4f));

            Vector3 tiltDir = (pos - new Vector3(0.2f, 0, 0.9f)).normalized;
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, tiltDir);
            slab.transform.rotation = Quaternion.AngleAxis(Random.Range(15f, 35f), tiltAxis) * Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));

            Material slabMat = new Material(matCrackedConcrete);
            slabMat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            if (slabMat.HasProperty("_BumpMap")) slabMat.SetTextureScale("_BumpMap", new Vector2(2f, 2f));
            slab.GetComponent<Renderer>().sharedMaterial = slabMat;
        }

        // =========================================================================
        // 3. CRASHED TOTALED ELEVATOR CAB (Inside Room)
        // =========================================================================
        GameObject elevatorCar = new GameObject("Totaled_Elevator_Car");
        elevatorCar.transform.parent = root.transform;

        // Smashed directly under the ceiling shaft opening
        elevatorCar.transform.position = new Vector3(0.2f, 0.42f, 0.9f);
        elevatorCar.transform.rotation = Quaternion.Euler(20f, -25f, -14f);

        float cabWidth = 2.4f;
        float cabDepth = 2.4f;
        float cabHeight = 2.8f;
        float cabWallThick = 0.12f;

        // Elevator Floor
        GameObject cabFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabFloor.name = "Elevator_Floor";
        cabFloor.transform.parent = elevatorCar.transform;
        cabFloor.transform.localPosition = new Vector3(0, 0.08f, 0);
        cabFloor.transform.localScale = new Vector3(cabWidth, 0.18f, cabDepth);
        cabFloor.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Threshold warning plate
        GameObject threshold = GameObject.CreatePrimitive(PrimitiveType.Cube);
        threshold.name = "Floor_ThresholdWarning";
        threshold.transform.parent = elevatorCar.transform;
        threshold.transform.localPosition = new Vector3(0, 0.18f, -cabDepth * 0.5f + 0.15f);
        threshold.transform.localScale = new Vector3(cabWidth * 0.9f, 0.04f, 0.32f);
        threshold.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        // Corner Pillars (bent)
        Vector3[] cornerLocs = new Vector3[] {
            new Vector3(-cabWidth * 0.5f + 0.08f, cabHeight * 0.5f, -cabDepth * 0.5f + 0.08f),
            new Vector3( cabWidth * 0.5f - 0.08f, cabHeight * 0.5f, -cabDepth * 0.5f + 0.08f),
            new Vector3(-cabWidth * 0.5f + 0.08f, cabHeight * 0.5f,  cabDepth * 0.5f - 0.08f),
            new Vector3( cabWidth * 0.5f - 0.08f, cabHeight * 0.5f,  cabDepth * 0.5f - 0.08f)
        };

        for (int c = 0; c < cornerLocs.Length; c++)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"Pillar_Corner_{c}";
            pillar.transform.parent = elevatorCar.transform;
            pillar.transform.localPosition = cornerLocs[c];
            float buckleX = (c == 0 || c == 1) ? 3.5f : -2.5f;
            float buckleZ = (c == 0 || c == 2) ? -3.0f : 3.0f;
            pillar.transform.localRotation = Quaternion.Euler(buckleX, 0, buckleZ);
            pillar.transform.localScale = new Vector3(0.18f, cabHeight, 0.18f);
            pillar.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;
        }

        // Back Wall
        GameObject cabBackWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabBackWall.name = "Wall_Back";
        cabBackWall.transform.parent = elevatorCar.transform;
        cabBackWall.transform.localPosition = new Vector3(0, cabHeight * 0.5f, cabDepth * 0.5f - cabWallThick * 0.5f);
        cabBackWall.transform.localRotation = Quaternion.Euler(-2.0f, 0, 1.5f);
        cabBackWall.transform.localScale = new Vector3(cabWidth - 0.15f, cabHeight, cabWallThick);
        cabBackWall.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Left Wall
        GameObject cabLeftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabLeftWall.name = "Wall_Left_Bowed";
        cabLeftWall.transform.parent = elevatorCar.transform;
        cabLeftWall.transform.localPosition = new Vector3(-cabWidth * 0.5f + cabWallThick * 0.5f - 0.06f, cabHeight * 0.5f, 0);
        cabLeftWall.transform.localRotation = Quaternion.Euler(0, 0, 5.5f);
        cabLeftWall.transform.localScale = new Vector3(cabWallThick, cabHeight, cabDepth - 0.15f);
        cabLeftWall.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Right Wall
        GameObject cabRightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabRightWall.name = "Wall_Right";
        cabRightWall.transform.parent = elevatorCar.transform;
        cabRightWall.transform.localPosition = new Vector3(cabWidth * 0.5f - cabWallThick * 0.5f, cabHeight * 0.5f, 0);
        cabRightWall.transform.localRotation = Quaternion.Euler(0, 0, -2.5f);
        cabRightWall.transform.localScale = new Vector3(cabWallThick, cabHeight, cabDepth - 0.15f);
        cabRightWall.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Front Door Header
        GameObject doorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorHeader.name = "Door_Header_Bent";
        doorHeader.transform.parent = elevatorCar.transform;
        doorHeader.transform.localPosition = new Vector3(0, cabHeight - 0.15f, -cabDepth * 0.5f + cabWallThick * 0.5f);
        doorHeader.transform.localRotation = Quaternion.Euler(10f, 0, -4f);
        doorHeader.transform.localScale = new Vector3(cabWidth, 0.32f, 0.2f);
        doorHeader.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Left Elevator Door (derailed, hanging askew)
        GameObject doorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorLeft.name = "Door_Left_Derailed";
        doorLeft.transform.parent = elevatorCar.transform;
        doorLeft.transform.localPosition = new Vector3(-0.38f, cabHeight * 0.44f, -cabDepth * 0.5f + 0.10f);
        doorLeft.transform.localRotation = Quaternion.Euler(7.5f, 22f, -16f);
        doorLeft.transform.localScale = new Vector3(0.95f, cabHeight * 0.85f, 0.08f);
        doorLeft.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Right Elevator Door (crumpled into frame)
        GameObject doorRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorRight.name = "Door_Right_Retracted";
        doorRight.transform.parent = elevatorCar.transform;
        doorRight.transform.localPosition = new Vector3(cabWidth * 0.35f, cabHeight * 0.45f, -cabDepth * 0.5f + 0.05f);
        doorRight.transform.localRotation = Quaternion.Euler(0, -8f, 2.0f);
        doorRight.transform.localScale = new Vector3(0.68f, cabHeight * 0.88f, 0.08f);
        doorRight.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Buckled Roof
        GameObject roofLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofLeft.name = "Roof_Crushed_Left";
        roofLeft.transform.parent = elevatorCar.transform;
        roofLeft.transform.localPosition = new Vector3(-cabWidth * 0.25f, cabHeight + 0.04f, 0);
        roofLeft.transform.localRotation = Quaternion.Euler(5f, 0, -21f);
        roofLeft.transform.localScale = new Vector3(cabWidth * 0.58f, 0.14f, cabDepth);
        roofLeft.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        GameObject roofRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofRight.name = "Roof_Crushed_Right";
        roofRight.transform.parent = elevatorCar.transform;
        roofRight.transform.localPosition = new Vector3(cabWidth * 0.24f, cabHeight + 0.03f, 0);
        roofRight.transform.localRotation = Quaternion.Euler(-4f, 0, 19f);
        roofRight.transform.localScale = new Vector3(cabWidth * 0.58f, 0.14f, cabDepth);
        roofRight.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Smashed Pulley Wheel Wreckage
        GameObject pulleyGear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pulleyGear.name = "Pulley_Wheel_Wreckage";
        pulleyGear.transform.parent = elevatorCar.transform;
        pulleyGear.transform.localPosition = new Vector3(0.05f, cabHeight + 0.38f, -0.1f);
        pulleyGear.transform.localRotation = Quaternion.Euler(72f, 35f, 15f);
        pulleyGear.transform.localScale = new Vector3(0.95f, 0.18f, 0.95f);
        pulleyGear.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Smashed Control Panel & Dangling Wires
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "ControlPanel_Smashed";
        panel.transform.parent = elevatorCar.transform;
        panel.transform.localPosition = new Vector3(cabWidth * 0.5f - 0.18f, cabHeight * 0.45f, -0.4f);
        panel.transform.localRotation = Quaternion.Euler(0, 0, 14f);
        panel.transform.localScale = new Vector3(0.08f, 0.9f, 0.35f);
        panel.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        for (int w = 0; w < 4; w++)
        {
            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wire.name = $"ExposedWire_{w}";
            wire.transform.parent = elevatorCar.transform;
            wire.transform.localPosition = new Vector3(cabWidth * 0.5f - 0.22f, cabHeight * 0.35f - w * 0.1f, -0.4f + (w - 1.5f) * 0.06f);
            wire.transform.localRotation = Quaternion.Euler(15f + w * 12f, 0, 8f + w * 10f);
            wire.transform.localScale = new Vector3(0.018f, 0.22f, 0.018f);
            Material wMat = new Material(urpLit);
            Color[] wireCols = new Color[] { Color.red, Color.yellow, Color.cyan, Color.white };
            wMat.color = wireCols[w % wireCols.Length];
            if (wMat.HasProperty("_BaseColor")) wMat.SetColor("_BaseColor", wMat.color);
            wire.GetComponent<Renderer>().sharedMaterial = wMat;
        }

        // Snapped Braided Steel Hoist Cables rising up into the ceiling breach
        GameObject cableGroup = new GameObject("Snapped_Hoist_Cables");
        cableGroup.transform.parent = elevatorCar.transform;

        for (int i = 0; i < 4; i++)
        {
            float xOffset = (i - 1.5f) * 0.25f;
            Vector3 basePos = new Vector3(xOffset, cabHeight + 0.45f, 0.05f);
            int segments = 8;
            Vector3 prevPos = basePos;
            for (int s = 1; s <= segments; s++)
            {
                float t = s / (float)segments;
                float angleArc = t * Mathf.PI * 1.5f + i * 0.75f;
                float h = Mathf.Sin(t * Mathf.PI * 0.85f) * (2.4f + i * 0.4f);
                float xDrift = Mathf.Cos(angleArc) * (0.85f + t * 0.9f) + xOffset;
                float zDrift = Mathf.Sin(angleArc) * (0.95f + t * 0.8f);
                Vector3 currPos = new Vector3(xDrift, cabHeight + 0.4f + h, zDrift);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"Cable_{i}_Seg_{s}";
                seg.transform.parent = cableGroup.transform;

                Vector3 mid = (prevPos + currPos) * 0.5f;
                Vector3 dir = (currPos - prevPos);
                seg.transform.localPosition = mid;
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                seg.transform.localScale = new Vector3(0.045f, dir.magnitude * 0.5f, 0.045f);
                seg.GetComponent<Renderer>().sharedMaterial = matSteelCable;

                prevPos = currPos;
            }
        }

        // =========================================================================
        // 4. ROOM PROPS & CORNER DRESSING (Barrels, Crates, Rubble)
        // =========================================================================
        GameObject propsGroup = new GameObject("Room_Props_And_Dressing");
        propsGroup.transform.parent = root.transform;

        // Stacked Industrial Metal Barrels in corner (X = -6.2, Z = 7.0)
        CreateIndustrialBarrel(propsGroup.transform, new Vector3(-6.2f, 0.5f, 7.2f), matIndustrialMetal);
        CreateIndustrialBarrel(propsGroup.transform, new Vector3(-5.3f, 0.5f, 7.4f), matIndustrialMetal);
        CreateIndustrialBarrel(propsGroup.transform, new Vector3(-5.8f, 1.45f, 7.3f), matIndustrialMetal);
        
        GameObject tippedBarrel = CreateIndustrialBarrel(propsGroup.transform, new Vector3(-4.5f, 0.3f, 4.8f), matIndustrialMetal);
        tippedBarrel.transform.rotation = Quaternion.Euler(85f, 22f, 10f);

        // Heavy Equipment Crates along right wall
        CreateEquipmentCrate(propsGroup.transform, new Vector3(6.5f, 0.5f, -2.5f), new Vector3(1.2f, 1.0f, 1.4f), matIndustrialMetal);
        CreateEquipmentCrate(propsGroup.transform, new Vector3(6.5f, 0.4f, -4.2f), new Vector3(1.0f, 0.8f, 1.0f), matIndustrialMetal);

        // Rubble chunks on concrete floor
        int debrisCount = 32;
        for (int r = 0; r < debrisCount; r++)
        {
            GameObject scrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scrap.name = $"RubbleChunk_{r}";
            scrap.transform.parent = propsGroup.transform;

            float rAngle = Random.Range(0f, Mathf.PI * 2f);
            float rDist = Random.Range(1.2f, 4.8f);
            Vector3 rPos = new Vector3(0.2f + Mathf.Cos(rAngle) * rDist, Random.Range(0.04f, 0.18f), 0.9f + Mathf.Sin(rAngle) * rDist);

            scrap.transform.position = rPos;
            scrap.transform.localScale = new Vector3(Random.Range(0.12f, 0.45f), Random.Range(0.08f, 0.25f), Random.Range(0.12f, 0.42f));
            scrap.transform.rotation = Random.rotation;
            scrap.GetComponent<Renderer>().sharedMaterial = (r % 2 == 0) ? matCrackedConcrete : matElevatorSteel;
        }

        // Organic damp moss overgrowth in floor crevices
        GameObject mossParent = new GameObject("Moss_Overgrowth");
        mossParent.transform.parent = root.transform;
        for (int m = 0; m < 28; m++)
        {
            GameObject clump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clump.name = $"Moss_Clump_{m}";
            clump.transform.parent = mossParent.transform;
            float mAngle = Random.Range(0f, Mathf.PI * 2f);
            float mDist = Random.Range(0.9f, 2.8f);
            Vector3 mPos = new Vector3(0.2f + Mathf.Cos(mAngle) * mDist, 0.04f, 0.9f + Mathf.Sin(mAngle) * mDist);
            clump.transform.position = mPos;
            clump.transform.localScale = new Vector3(Random.Range(0.25f, 0.65f), Random.Range(0.06f, 0.18f), Random.Range(0.25f, 0.65f));
            clump.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            Material mossInst = new Material(matMoss);
            mossInst.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            clump.GetComponent<Renderer>().sharedMaterial = mossInst;
        }

        // =========================================================================
        // 5. DYNAMIC LIGHTING SYSTEMS ACROSS THE ROOM
        // =========================================================================
        GameObject lightsGroup = new GameObject("Room_Dynamic_Lighting");
        lightsGroup.transform.parent = root.transform;

        // --- Light 1: Overhead Shaft Breach Volumetric Spotlight (From high inside vertical shaft) ---
        GameObject shaftLightObj = new GameObject("Light_ShaftBreach_Downlight");
        shaftLightObj.transform.parent = lightsGroup.transform;
        shaftLightObj.transform.position = new Vector3(0.2f, roomHeight + 6.0f, 0.9f);
        shaftLightObj.transform.rotation = Quaternion.Euler(75f, 12f, 0f);
        Light shaftLight = shaftLightObj.AddComponent<Light>();
        shaftLight.type = LightType.Spot;
        shaftLight.spotAngle = 68f;
        shaftLight.innerSpotAngle = 36f;
        shaftLight.range = 22f;
        shaftLight.color = new Color(0.88f, 0.94f, 1.0f); // Cool upper level industrial shaft light
        shaftLight.intensity = 8.5f;
        shaftLight.shadows = LightShadows.Soft;
        shaftLight.shadowNearPlane = 0.2f;

        // --- Light 2: Worksite Halogen Construction Floodlight (Floor Key Light on Elevator) ---
        CreateWorksiteFloodlight(lightsGroup.transform, new Vector3(-3.2f, 0.05f, -3.8f), new Vector3(0.2f, 1.2f, 0.9f), matIndustrialMetal);

        // --- Light 3: Flickering Broken Fluorescent Fixture (Suspended from Ceiling) ---
        GameObject brokenFixture = CreateFluorescentFixture(lightsGroup.transform, new Vector3(-2.2f, 5.0f, -2.5f), Quaternion.Euler(18f, 12f, -24f), matIndustrialMetal, matFluorescent, out Renderer brokenTubeRend);
        GameObject brokenLightObj = new GameObject("Light_Fluorescent_Flickering");
        brokenLightObj.transform.parent = brokenFixture.transform;
        brokenLightObj.transform.localPosition = new Vector3(0, -0.2f, 0);
        Light brokenLight = brokenLightObj.AddComponent<Light>();
        brokenLight.type = LightType.Point;
        brokenLight.color = new Color(0.90f, 0.95f, 1.0f);
        brokenLight.range = 14f;
        brokenLight.intensity = 5.5f;
        brokenLight.shadows = LightShadows.Soft;

        DynamicLightEffects brokenFlicker = brokenLightObj.AddComponent<DynamicLightEffects>();
        brokenFlicker.CurrentEffectType = DynamicLightEffects.EffectType.Flicker;
        brokenFlicker.baseIntensity = 5.5f;
        brokenFlicker.minIntensity = 0.6f;
        brokenFlicker.maxIntensity = 6.8f;
        brokenFlicker.speed = 14f;
        brokenFlicker.flickerRoughness = 0.72f;
        brokenFlicker.blackoutChance = 0.08f;
        brokenFlicker.targetRenderer = brokenTubeRend;
        brokenFlicker.emissiveColor = new Color(0.9f, 0.95f, 1.0f);

        // --- Light 4: Steady Operational Fluorescent Fixture (Rear Chamber) ---
        GameObject steadyFixture = CreateFluorescentFixture(lightsGroup.transform, new Vector3(2.5f, 5.5f, 4.5f), Quaternion.Euler(0, 0, 0), matIndustrialMetal, matFluorescent, out _);
        GameObject steadyLightObj = new GameObject("Light_Fluorescent_Steady");
        steadyLightObj.transform.parent = steadyFixture.transform;
        steadyLightObj.transform.localPosition = new Vector3(0, -0.2f, 0);
        Light steadyLight = steadyLightObj.AddComponent<Light>();
        steadyLight.type = LightType.Point;
        steadyLight.color = new Color(0.95f, 0.96f, 0.92f);
        steadyLight.range = 13f;
        steadyLight.intensity = 4.5f;
        steadyLight.shadows = LightShadows.Soft;

        // --- Light 5: Rotating Emergency Alarm Beacon (Wall Mounted on Right) ---
        GameObject beaconObj = CreateEmergencyBeacon(lightsGroup.transform, new Vector3(4.2f, 3.4f, 0.5f), matIndustrialMetal, matBeacon, out Renderer beaconLens);
        GameObject beaconLightObj = new GameObject("Light_Emergency_RotatingBeam");
        beaconLightObj.transform.parent = beaconObj.transform;
        beaconLightObj.transform.localPosition = new Vector3(0, 0.15f, 0);
        beaconLightObj.transform.localRotation = Quaternion.Euler(28f, 0, 0);
        Light beaconLight = beaconLightObj.AddComponent<Light>();
        beaconLight.type = LightType.Spot;
        beaconLight.spotAngle = 65f;
        beaconLight.innerSpotAngle = 28f;
        beaconLight.range = 14f;
        beaconLight.color = new Color(1.0f, 0.42f, 0.10f); // Vivid alarm amber
        beaconLight.intensity = 6.0f;
        beaconLight.shadows = LightShadows.Soft;

        DynamicLightEffects beaconDynamics = beaconLightObj.AddComponent<DynamicLightEffects>();
        beaconDynamics.CurrentEffectType = DynamicLightEffects.EffectType.RotatingBeacon;
        beaconDynamics.rotationAxis = Vector3.up;
        beaconDynamics.rotationSpeed = 220f;
        beaconDynamics.baseIntensity = 6.0f;
        beaconDynamics.targetRenderer = beaconLens;
        beaconDynamics.emissiveColor = new Color(1.0f, 0.35f, 0.08f);

        // Beacon ambient glow point light
        GameObject beaconGlowObj = new GameObject("Light_Emergency_GlowHalo");
        beaconGlowObj.transform.parent = beaconObj.transform;
        beaconGlowObj.transform.localPosition = Vector3.zero;
        Light beaconGlow = beaconGlowObj.AddComponent<Light>();
        beaconGlow.type = LightType.Point;
        beaconGlow.color = new Color(1.0f, 0.40f, 0.10f);
        beaconGlow.range = 8.0f;
        beaconGlow.intensity = 2.8f;
        beaconGlow.shadows = LightShadows.None;

        DynamicLightEffects beaconPulse = beaconGlowObj.AddComponent<DynamicLightEffects>();
        beaconPulse.CurrentEffectType = DynamicLightEffects.EffectType.Pulse;
        beaconPulse.minIntensity = 1.0f;
        beaconPulse.maxIntensity = 3.6f;
        beaconPulse.speed = 4.5f;

        // --- Light 6: Electrical Breaker Short-Circuit Arc Spark Light ---
        GameObject sparkLightObj = new GameObject("Light_Electrical_ArcSparks");
        sparkLightObj.transform.parent = breakerBox.transform;
        sparkLightObj.transform.localPosition = new Vector3(0.2f, -0.2f, 0.15f);
        Light sparkLight = sparkLightObj.AddComponent<Light>();
        sparkLight.type = LightType.Point;
        sparkLight.color = new Color(0.65f, 0.88f, 1.0f); // Electric cyan-white
        sparkLight.range = 9.0f;
        sparkLight.intensity = 0.2f;
        sparkLight.shadows = LightShadows.Soft;

        DynamicLightEffects sparkDynamics = sparkLightObj.AddComponent<DynamicLightEffects>();
        sparkDynamics.CurrentEffectType = DynamicLightEffects.EffectType.ElectricalSpark;
        sparkDynamics.baseIntensity = 1.0f;
        sparkDynamics.minIntensity = 0.1f;
        sparkDynamics.maxIntensity = 9.0f;

        // --- Light 7: Crashed Elevator Interior Emergency Lamp ---
        GameObject interiorLampFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        interiorLampFixture.name = "Light_Fluorescent_Interior_Dangling";
        interiorLampFixture.transform.parent = elevatorCar.transform;
        interiorLampFixture.transform.localPosition = new Vector3(0.15f, cabHeight - 0.7f, 0.25f);
        interiorLampFixture.transform.localRotation = Quaternion.Euler(42f, 18f, -24f);
        interiorLampFixture.transform.localScale = new Vector3(1.4f, 0.06f, 0.15f);
        interiorLampFixture.GetComponent<Renderer>().sharedMaterial = matEmergencyLamp;

        GameObject intPointObj = new GameObject("Interior_Emergency_PointLight");
        intPointObj.transform.parent = elevatorCar.transform;
        intPointObj.transform.localPosition = new Vector3(0f, cabHeight * 0.55f, 0.1f);
        Light emergencyPoint = intPointObj.AddComponent<Light>();
        emergencyPoint.type = LightType.Point;
        emergencyPoint.color = new Color(1.0f, 0.68f, 0.28f); // Deep tungsten warm amber
        emergencyPoint.intensity = 4.8f;
        emergencyPoint.range = 9.0f;
        emergencyPoint.shadows = LightShadows.Soft;

        DynamicLightEffects interiorFlicker = intPointObj.AddComponent<DynamicLightEffects>();
        interiorFlicker.CurrentEffectType = DynamicLightEffects.EffectType.Flicker;
        interiorFlicker.baseIntensity = 4.8f;
        interiorFlicker.minIntensity = 2.4f;
        interiorFlicker.maxIntensity = 5.6f;
        interiorFlicker.speed = 8.0f;
        interiorFlicker.flickerRoughness = 0.45f;
        interiorFlicker.targetRenderer = interiorLampFixture.GetComponent<Renderer>();
        interiorFlicker.emissiveColor = new Color(1.0f, 0.65f, 0.2f);

        Debug.Log("[ElevatorSceneBuilder] Crashed elevator successfully moved into atmospheric room structure with complete dynamic lighting systems!");
    }

    // =========================================================================
    // HELPER BUILDER METHODS
    // =========================================================================

    private static void CreateElevatorShaftTower(Transform parent, Vector3 center, float sizeX, float sizeZ, float height, Material matWall, Material matMetal)
    {
        GameObject shaftRoot = new GameObject("Shaft_Upper_Chute_Tower");
        shaftRoot.transform.parent = parent;
        shaftRoot.transform.position = center;

        float thick = 0.35f;

        // Front shaft wall
        GameObject wF = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wF.name = "Shaft_Wall_Front";
        wF.transform.parent = shaftRoot.transform;
        wF.transform.localPosition = new Vector3(0, height * 0.5f, -sizeZ * 0.5f - thick * 0.5f);
        wF.transform.localScale = new Vector3(sizeX + thick * 2f, height, thick);
        wF.GetComponent<Renderer>().sharedMaterial = matWall;

        // Back shaft wall
        GameObject wB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wB.name = "Shaft_Wall_Back";
        wB.transform.parent = shaftRoot.transform;
        wB.transform.localPosition = new Vector3(0, height * 0.5f, sizeZ * 0.5f + thick * 0.5f);
        wB.transform.localScale = new Vector3(sizeX + thick * 2f, height, thick);
        wB.GetComponent<Renderer>().sharedMaterial = matWall;

        // Left shaft wall
        GameObject wL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wL.name = "Shaft_Wall_Left";
        wL.transform.parent = shaftRoot.transform;
        wL.transform.localPosition = new Vector3(-sizeX * 0.5f - thick * 0.5f, height * 0.5f, 0);
        wL.transform.localScale = new Vector3(thick, height, sizeZ);
        wL.GetComponent<Renderer>().sharedMaterial = matWall;

        // Right shaft wall
        GameObject wR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wR.name = "Shaft_Wall_Right";
        wR.transform.parent = shaftRoot.transform;
        wR.transform.localPosition = new Vector3(sizeX * 0.5f + thick * 0.5f, height * 0.5f, 0);
        wR.transform.localScale = new Vector3(thick, height, sizeZ);
        wR.GetComponent<Renderer>().sharedMaterial = matWall;

        // Roof cap on top of shaft
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "Shaft_Cap_Ceiling";
        cap.transform.parent = shaftRoot.transform;
        cap.transform.localPosition = new Vector3(0, height + 0.1f, 0);
        cap.transform.localScale = new Vector3(sizeX + thick * 2f, 0.3f, sizeZ + thick * 2f);
        cap.GetComponent<Renderer>().sharedMaterial = matWall;

        // Internal steel guide rails running up the shaft
        GameObject rL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rL.transform.parent = shaftRoot.transform;
        rL.transform.localPosition = new Vector3(-sizeX * 0.5f + 0.1f, height * 0.5f, 0);
        rL.transform.localScale = new Vector3(0.12f, height, 0.15f);
        rL.GetComponent<Renderer>().sharedMaterial = matMetal;

        GameObject rR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rR.transform.parent = shaftRoot.transform;
        rR.transform.localPosition = new Vector3(sizeX * 0.5f - 0.1f, height * 0.5f, 0);
        rR.transform.localScale = new Vector3(0.12f, height, 0.15f);
        rR.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static void CreatePerimeterCurbs(Transform parent, float width, float depth, Material mat)
    {
        GameObject curbRoot = new GameObject("Perimeter_Curbs");
        curbRoot.transform.parent = parent;

        float curbH = 0.18f;
        float curbW = 0.25f;

        // Left curb
        GameObject cL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cL.transform.parent = curbRoot.transform;
        cL.transform.position = new Vector3(-width * 0.5f + curbW * 0.5f, curbH * 0.5f, 0.5f);
        cL.transform.localScale = new Vector3(curbW, curbH, depth);
        cL.GetComponent<Renderer>().sharedMaterial = mat;

        // Right curb
        GameObject cR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cR.transform.parent = curbRoot.transform;
        cR.transform.position = new Vector3(width * 0.5f - curbW * 0.5f, curbH * 0.5f, 0.5f);
        cR.transform.localScale = new Vector3(curbW, curbH, depth);
        cR.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void CreateColumn(Transform parent, Vector3 pos, float height, Material matConcrete, Material matMetal)
    {
        GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cube);
        col.name = "Structural_Column";
        col.transform.parent = parent;
        col.transform.position = pos;
        col.transform.localScale = new Vector3(0.75f, height, 0.75f);
        col.GetComponent<Renderer>().sharedMaterial = matConcrete;

        // Metal base footing collar
        GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foot.name = "Column_BaseCollar";
        foot.transform.parent = col.transform;
        foot.transform.localPosition = new Vector3(0, -0.44f, 0);
        foot.transform.localScale = new Vector3(1.25f, 0.12f, 1.25f);
        foot.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static void CreateGuideRail(Transform parent, Vector3 pos, float height, Material matMetal)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = "Elevator_Vertical_GuideRail";
        rail.transform.parent = parent;
        rail.transform.position = pos;
        rail.transform.localScale = new Vector3(0.12f, height, 0.18f);
        rail.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static GameObject CreateElectricalCabinet(Transform parent, Vector3 pos, Material matMetal, Material matHazard)
    {
        GameObject cabinet = new GameObject("Electrical_Breaker_Cabinet");
        cabinet.transform.parent = parent;
        cabinet.transform.position = pos;

        // Main housing
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Cabinet_Body";
        box.transform.parent = cabinet.transform;
        box.transform.localPosition = Vector3.zero;
        box.transform.localScale = new Vector3(0.35f, 1.4f, 0.95f);
        box.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Ajar door hanging open
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Cabinet_Door_Ajar";
        door.transform.parent = cabinet.transform;
        door.transform.localPosition = new Vector3(0.24f, 0, 0.35f);
        door.transform.localRotation = Quaternion.Euler(0, 55f, 0);
        door.transform.localScale = new Vector3(0.04f, 1.35f, 0.85f);
        door.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Hazard plate
        GameObject hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazard.name = "HighVoltage_WarningSign";
        hazard.transform.parent = door.transform;
        hazard.transform.localPosition = new Vector3(0.025f, 0.2f, 0);
        hazard.transform.localScale = new Vector3(0.02f, 0.35f, 0.35f);
        hazard.GetComponent<Renderer>().sharedMaterial = matHazard;

        return cabinet;
    }

    private static void CreateCableTray(Transform parent, Vector3 pos, float length, bool alongZ, Material matMetal, Material matCable)
    {
        GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "Overhead_Cable_Tray";
        tray.transform.parent = parent;
        tray.transform.position = pos;
        tray.transform.localScale = alongZ ? new Vector3(0.6f, 0.1f, length) : new Vector3(length, 0.1f, 0.6f);
        tray.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Thick power cables inside tray
        for (int i = 0; i < 3; i++)
        {
            GameObject cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cable.name = $"HeavyCable_{i}";
            cable.transform.parent = tray.transform;
            cable.transform.localPosition = new Vector3((i - 1f) * 0.3f, 0.6f, 0);
            cable.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            cable.transform.localScale = new Vector3(0.18f, 0.5f, 0.18f);
            cable.GetComponent<Renderer>().sharedMaterial = matCable;
        }
    }

    private static void CreateBlastDoor(Transform parent, Vector3 pos, Material matMetal, Material matHazard)
    {
        GameObject doorGroup = new GameObject("Heavy_Blast_AirlockDoor");
        doorGroup.transform.parent = parent;
        doorGroup.transform.position = pos;

        // Outer heavy frame
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "BlastDoor_Frame";
        frame.transform.parent = doorGroup.transform;
        frame.transform.localPosition = Vector3.zero;
        frame.transform.localScale = new Vector3(0.2f, 3.2f, 2.2f);
        frame.GetComponent<Renderer>().sharedMaterial = matHazard;

        // Recessed armored steel door slab
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "BlastDoor_Panel";
        panel.transform.parent = doorGroup.transform;
        panel.transform.localPosition = new Vector3(-0.04f, 0, 0);
        panel.transform.localScale = new Vector3(0.16f, 2.8f, 1.8f);
        panel.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Locking Wheel
        GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheel.name = "LockingWheel";
        wheel.transform.parent = panel.transform;
        wheel.transform.localPosition = new Vector3(-0.55f, 0, 0);
        wheel.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        wheel.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
        wheel.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static void CreatePipeRun(Transform parent, Vector3 pos, float length, Material matMetal)
    {
        GameObject pipeGroup = new GameObject("Wall_Piping_Run");
        pipeGroup.transform.parent = parent;
        pipeGroup.transform.position = pos;

        for (int p = 0; p < 2; p++)
        {
            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = $"IndustrialPipe_{p}";
            pipe.transform.parent = pipeGroup.transform;
            pipe.transform.localPosition = new Vector3(0, p * 0.35f, 0);
            pipe.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            pipe.transform.localScale = new Vector3(0.18f, length * 0.5f, 0.18f);
            pipe.GetComponent<Renderer>().sharedMaterial = matMetal;
        }
    }

    private static void CreatePortalFrame(Transform parent, Vector3 pos, float width, float height, Material matMetal, Material matHazard)
    {
        GameObject portal = new GameObject("Entrance_Portal_Arch");
        portal.transform.parent = parent;
        portal.transform.position = pos;

        float thick = 0.35f;

        // Left post
        GameObject leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPost.transform.parent = portal.transform;
        leftPost.transform.localPosition = new Vector3(-width * 0.5f + thick * 0.5f, 0, 0);
        leftPost.transform.localScale = new Vector3(thick, height, thick);
        leftPost.GetComponent<Renderer>().sharedMaterial = matHazard;

        // Right post
        GameObject rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPost.transform.parent = portal.transform;
        rightPost.transform.localPosition = new Vector3(width * 0.5f - thick * 0.5f, 0, 0);
        rightPost.transform.localScale = new Vector3(thick, height, thick);
        rightPost.GetComponent<Renderer>().sharedMaterial = matHazard;

        // Top lintel
        GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.transform.parent = portal.transform;
        lintel.transform.localPosition = new Vector3(0, height * 0.5f - thick * 0.5f, 0);
        lintel.transform.localScale = new Vector3(width, thick, thick);
        lintel.GetComponent<Renderer>().sharedMaterial = matHazard;
    }

    private static void CreateShatteredBreachFrame(Transform parent, Vector3 center, float sizeX, float sizeZ, Material matConcrete, Material matMetal)
    {
        GameObject breachFrame = new GameObject("Shaft_Breach_JaggedEdges");
        breachFrame.transform.parent = parent;
        breachFrame.transform.position = center;

        // Jagged concrete chunks hanging around breach edges
        int edgeCount = 14;
        for (int e = 0; e < edgeCount; e++)
        {
            float angle = (e / (float)edgeCount) * Mathf.PI * 2f;
            float rx = Mathf.Cos(angle) * (sizeX * 0.5f);
            float rz = Mathf.Sin(angle) * (sizeZ * 0.5f);

            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = $"Breach_JaggedChunk_{e}";
            chunk.transform.parent = breachFrame.transform;
            chunk.transform.localPosition = new Vector3(rx, -0.15f, rz);
            chunk.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            chunk.transform.localScale = new Vector3(Random.Range(0.4f, 0.9f), Random.Range(0.18f, 0.45f), Random.Range(0.4f, 0.9f));
            chunk.GetComponent<Renderer>().sharedMaterial = matConcrete;

            // Bent rebar wires dangling from cracked ceiling
            if (e % 3 == 0)
            {
                GameObject rebar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rebar.name = $"Dangling_Rebar_{e}";
                rebar.transform.parent = breachFrame.transform;
                rebar.transform.localPosition = new Vector3(rx, -0.45f, rz);
                rebar.transform.localRotation = Quaternion.Euler(Random.Range(10f, 35f), Random.Range(0f, 360f), Random.Range(10f, 35f));
                rebar.transform.localScale = new Vector3(0.025f, 0.4f, 0.025f);
                rebar.GetComponent<Renderer>().sharedMaterial = matMetal;
            }
        }
    }

    private static void CreateSteelIBeam(Transform parent, Vector3 pos, float spanLength, Material matMetal)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Ceiling_Structural_IBeam";
        beam.transform.parent = parent;
        beam.transform.position = pos;
        beam.transform.localScale = new Vector3(spanLength, 0.38f, 0.22f);
        beam.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static void CreateVentilationDuct(Transform parent, Vector3 pos, float length, Material matMetal)
    {
        GameObject duct = GameObject.CreatePrimitive(PrimitiveType.Cube);
        duct.name = "Ceiling_HVAC_VentDuct";
        duct.transform.parent = parent;
        duct.transform.position = pos;
        duct.transform.localScale = new Vector3(0.95f, 0.55f, length);
        duct.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static GameObject CreateFluorescentFixture(Transform parent, Vector3 pos, Quaternion rot, Material matMetal, Material matEmissive, out Renderer tubeRend)
    {
        GameObject fixture = new GameObject("Fixture_Fluorescent_Housing");
        fixture.transform.parent = parent;
        fixture.transform.position = pos;
        fixture.transform.rotation = rot;

        // Metal casing
        GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        casing.name = "Fixture_Chassis";
        casing.transform.parent = fixture.transform;
        casing.transform.localPosition = Vector3.zero;
        casing.transform.localScale = new Vector3(0.35f, 0.12f, 2.2f);
        casing.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Emissive light tube
        GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Fluorescent_Tube";
        tube.transform.parent = fixture.transform;
        tube.transform.localPosition = new Vector3(0, -0.07f, 0);
        tube.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        tube.transform.localScale = new Vector3(0.08f, 1.0f, 0.08f);
        tubeRend = tube.GetComponent<Renderer>();
        tubeRend.sharedMaterial = matEmissive;

        return fixture;
    }

    private static GameObject CreateEmergencyBeacon(Transform parent, Vector3 pos, Material matMetal, Material matBeacon, out Renderer beaconLens)
    {
        GameObject beacon = new GameObject("Beacon_Emergency_Mount");
        beacon.transform.parent = parent;
        beacon.transform.position = pos;

        // Base collar
        GameObject baseCollar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseCollar.name = "Beacon_Base";
        baseCollar.transform.parent = beacon.transform;
        baseCollar.transform.localPosition = Vector3.zero;
        baseCollar.transform.localScale = new Vector3(0.32f, 0.08f, 0.32f);
        baseCollar.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Glowing red/amber lens
        GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lens.name = "Beacon_GlassLens";
        lens.transform.parent = beacon.transform;
        lens.transform.localPosition = new Vector3(0, 0.14f, 0);
        lens.transform.localScale = new Vector3(0.24f, 0.12f, 0.24f);
        beaconLens = lens.GetComponent<Renderer>();
        beaconLens.sharedMaterial = matBeacon;

        return beacon;
    }

    private static void CreateWorksiteFloodlight(Transform parent, Vector3 pos, Vector3 targetPoint, Material matMetal)
    {
        GameObject floodlight = new GameObject("Worksite_Halogen_Floodlight_Rig");
        floodlight.transform.parent = parent;
        floodlight.transform.position = pos;

        // Tripod / floor base
        GameObject basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePlate.name = "Tripod_Base";
        basePlate.transform.parent = floodlight.transform;
        basePlate.transform.localPosition = new Vector3(0, 0.08f, 0);
        basePlate.transform.localScale = new Vector3(0.7f, 0.04f, 0.7f);
        basePlate.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Stand pole
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Tripod_Pole";
        pole.transform.parent = floodlight.transform;
        pole.transform.localPosition = new Vector3(0, 0.45f, 0);
        pole.transform.localScale = new Vector3(0.06f, 0.4f, 0.06f);
        pole.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Lamp head
        GameObject lampHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lampHead.name = "Halogen_LampHead";
        lampHead.transform.parent = floodlight.transform;
        lampHead.transform.localPosition = new Vector3(0, 0.85f, 0);
        lampHead.transform.LookAt(targetPoint);
        lampHead.transform.localScale = new Vector3(0.45f, 0.35f, 0.35f);
        lampHead.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Spotlight component
        GameObject lightObj = new GameObject("Light_Worksite_Halogen");
        lightObj.transform.parent = lampHead.transform;
        lightObj.transform.localPosition = new Vector3(0, 0, 0.2f);
        lightObj.transform.localRotation = Quaternion.identity;
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 68f;
        light.innerSpotAngle = 32f;
        light.range = 16f;
        light.color = new Color(1.0f, 0.84f, 0.62f); // Warm halogen glow
        light.intensity = 7.5f;
        light.shadows = LightShadows.Soft;
    }

    private static GameObject CreateIndustrialBarrel(Transform parent, Vector3 pos, Material matMetal)
    {
        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Industrial_Steel_Barrel";
        barrel.transform.parent = parent;
        barrel.transform.position = pos;
        barrel.transform.localScale = new Vector3(0.65f, 0.48f, 0.65f);
        barrel.GetComponent<Renderer>().sharedMaterial = matMetal;
        return barrel;
    }

    private static void CreateEquipmentCrate(Transform parent, Vector3 pos, Vector3 size, Material matMetal)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Industrial_Equipment_Crate";
        crate.transform.parent = parent;
        crate.transform.position = pos;
        crate.transform.localScale = size;
        crate.GetComponent<Renderer>().sharedMaterial = matMetal;
    }
}
