using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ElevatorSceneBuilder
{
    private const string MaterialDir = "Assets/Materials";

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
        Material matGroundEarth = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_MudSoil.mat");
        Material matCrackedConcrete = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_CrackedConcrete.mat");
        Material matMoss = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_Moss.mat");
        Material matElevatorSteel = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_RustedSteel.mat");
        Material matSteelCable = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_SteelCable.mat");
        Material matWarningStripe = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_HazardWarning.mat");
        Material matFlickerLight = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PBR_EmergencyLamp.mat");

        // Fallbacks if not yet generated
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");
        if (matGroundEarth == null) matGroundEarth = new Material(urpLit);
        if (matCrackedConcrete == null) matCrackedConcrete = new Material(urpLit);
        if (matMoss == null) matMoss = new Material(urpLit);
        if (matElevatorSteel == null) matElevatorSteel = new Material(urpLit);
        if (matSteelCable == null) matSteelCable = new Material(urpLit);
        if (matWarningStripe == null) matWarningStripe = new Material(urpLit);
        if (matFlickerLight == null) matFlickerLight = new Material(urpLit);

        // Instance material for ground to set high tiling
        Material matGroundTiled = new Material(matGroundEarth);
        matGroundTiled.name = "PBR_MudSoil_Tiled";
        matGroundTiled.SetTextureScale("_BaseMap", new Vector2(16, 16));
        if (matGroundTiled.HasProperty("_BumpMap")) matGroundTiled.SetTextureScale("_BumpMap", new Vector2(16, 16));

        // 1. GROUND & CRATER TERRAIN
        GameObject groundObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundObj.name = "Terrain_EarthGround";
        groundObj.transform.parent = root.transform;
        groundObj.transform.position = new Vector3(0, 0, 0);
        groundObj.transform.localScale = new Vector3(3.0f, 1f, 3.0f);
        groundObj.GetComponent<Renderer>().sharedMaterial = matGroundTiled;

        // Dark impact scorch decal disk
        GameObject scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        scorch.name = "Impact_ScorchMark";
        scorch.transform.parent = root.transform;
        scorch.transform.position = new Vector3(0.2f, 0.015f, -0.1f);
        scorch.transform.localScale = new Vector3(4.8f, 0.01f, 4.8f);
        Material matScorch = new Material(matGroundEarth);
        matScorch.color = new Color(0.06f, 0.05f, 0.04f);
        if (matScorch.HasProperty("_BaseColor")) matScorch.SetColor("_BaseColor", matScorch.color);
        matScorch.SetFloat("_Smoothness", 0.45f); // Wet shiny mud reflection
        scorch.GetComponent<Renderer>().sharedMaterial = matScorch;

        // Impact Crater Slabs (Cracked concrete / shattered foundation thrown upward)
        GameObject craterParent = new GameObject("ImpactCrater_Debris");
        craterParent.transform.parent = root.transform;

        int slabCount = 18;
        for (int i = 0; i < slabCount; i++)
        {
            float angle = (i / (float)slabCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            float dist = Random.Range(1.6f, 3.8f);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0.04f, Mathf.Sin(angle) * dist);

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = $"CrackedConcreteSlab_{i}";
            slab.transform.parent = craterParent.transform;
            slab.transform.position = pos;
            slab.transform.localScale = new Vector3(Random.Range(0.6f, 1.6f), Random.Range(0.12f, 0.35f), Random.Range(0.6f, 1.5f));

            Vector3 tiltDir = (pos - new Vector3(0, 0, 0)).normalized;
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, tiltDir);
            slab.transform.rotation = Quaternion.AngleAxis(Random.Range(15f, 38f), tiltAxis) * Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            
            Material slabMat = new Material(matCrackedConcrete);
            slabMat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            if (slabMat.HasProperty("_BumpMap")) slabMat.SetTextureScale("_BumpMap", new Vector2(2f, 2f));
            slab.GetComponent<Renderer>().sharedMaterial = slabMat;
        }

        // 2. CRASHED TOTALED ELEVATOR CAB
        GameObject elevatorCar = new GameObject("Totaled_Elevator_Car");
        elevatorCar.transform.parent = root.transform;

        // Embedded crash position and dramatic tilt angles
        elevatorCar.transform.position = new Vector3(0.1f, 0.42f, -0.1f);
        elevatorCar.transform.rotation = Quaternion.Euler(21f, -28f, -15f);

        float cabWidth = 2.4f;
        float cabDepth = 2.4f;
        float cabHeight = 2.8f;
        float wallThickness = 0.12f;

        // Floor base
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Elevator_Floor";
        floor.transform.parent = elevatorCar.transform;
        floor.transform.localPosition = new Vector3(0, 0.08f, 0);
        floor.transform.localScale = new Vector3(cabWidth, 0.18f, cabDepth);
        floor.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Chequered warning threshold plate on front floor edge
        GameObject threshold = GameObject.CreatePrimitive(PrimitiveType.Cube);
        threshold.name = "Floor_ThresholdWarning";
        threshold.transform.parent = elevatorCar.transform;
        threshold.transform.localPosition = new Vector3(0, 0.18f, -cabDepth * 0.5f + 0.15f);
        threshold.transform.localScale = new Vector3(cabWidth * 0.9f, 0.04f, 0.32f);
        threshold.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        // 4 Corner Structural Steel H-Pillars (bent / warped by impact)
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
        GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWall.name = "Wall_Back";
        backWall.transform.parent = elevatorCar.transform;
        backWall.transform.localPosition = new Vector3(0, cabHeight * 0.5f, cabDepth * 0.5f - wallThickness * 0.5f);
        backWall.transform.localRotation = Quaternion.Euler(-2.0f, 0, 1.5f);
        backWall.transform.localScale = new Vector3(cabWidth - 0.15f, cabHeight, wallThickness);
        backWall.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Left Wall (bowed outward by impact)
        GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "Wall_Left_Bowed";
        leftWall.transform.parent = elevatorCar.transform;
        leftWall.transform.localPosition = new Vector3(-cabWidth * 0.5f + wallThickness * 0.5f - 0.06f, cabHeight * 0.5f, 0);
        leftWall.transform.localRotation = Quaternion.Euler(0, 0, 5.5f);
        leftWall.transform.localScale = new Vector3(wallThickness, cabHeight, cabDepth - 0.15f);
        leftWall.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Right Wall
        GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWall.name = "Wall_Right";
        rightWall.transform.parent = elevatorCar.transform;
        rightWall.transform.localPosition = new Vector3(cabWidth * 0.5f - wallThickness * 0.5f, cabHeight * 0.5f, 0);
        rightWall.transform.localRotation = Quaternion.Euler(0, 0, -2.5f);
        rightWall.transform.localScale = new Vector3(wallThickness, cabHeight, cabDepth - 0.15f);
        rightWall.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Front Door Header (Bent downwards)
        GameObject doorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorHeader.name = "Door_Header_Bent";
        doorHeader.transform.parent = elevatorCar.transform;
        doorHeader.transform.localPosition = new Vector3(0, cabHeight - 0.15f, -cabDepth * 0.5f + wallThickness * 0.5f);
        doorHeader.transform.localRotation = Quaternion.Euler(10f, 0, -4f);
        doorHeader.transform.localScale = new Vector3(cabWidth, 0.32f, 0.2f);
        doorHeader.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Left Elevator Door (Derailed, hanging askew, revealing interior)
        GameObject doorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorLeft.name = "Door_Left_Derailed";
        doorLeft.transform.parent = elevatorCar.transform;
        doorLeft.transform.localPosition = new Vector3(-0.38f, cabHeight * 0.44f, -cabDepth * 0.5f + 0.10f);
        doorLeft.transform.localRotation = Quaternion.Euler(7.5f, 22f, -16f);
        doorLeft.transform.localScale = new Vector3(0.95f, cabHeight * 0.85f, 0.08f);
        doorLeft.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Right Elevator Door (Crumpled into door frame)
        GameObject doorRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorRight.name = "Door_Right_Retracted";
        doorRight.transform.parent = elevatorCar.transform;
        doorRight.transform.localPosition = new Vector3(cabWidth * 0.35f, cabHeight * 0.45f, -cabDepth * 0.5f + 0.05f);
        doorRight.transform.localRotation = Quaternion.Euler(0, -8f, 2.0f);
        doorRight.transform.localScale = new Vector3(0.68f, cabHeight * 0.88f, 0.08f);
        doorRight.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Buckled Roof (inward V-buckle from pulley impact)
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

        // Escape Hatch blown ajar
        GameObject hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hatch.name = "Roof_EscapeHatch_Ajar";
        hatch.transform.parent = elevatorCar.transform;
        hatch.transform.localPosition = new Vector3(0.12f, cabHeight + 0.35f, 0.2f);
        hatch.transform.localRotation = Quaternion.Euler(-38f, 28f, 18f);
        hatch.transform.localScale = new Vector3(0.75f, 0.06f, 0.75f);
        hatch.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // Smashed Pulley Wheel Wreckage on roof
        GameObject pulleyGear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pulleyGear.name = "Pulley_Wheel_Wreckage";
        pulleyGear.transform.parent = elevatorCar.transform;
        pulleyGear.transform.localPosition = new Vector3(0.05f, cabHeight + 0.38f, -0.1f);
        pulleyGear.transform.localRotation = Quaternion.Euler(72f, 35f, 15f);
        pulleyGear.transform.localScale = new Vector3(0.95f, 0.18f, 0.95f);
        pulleyGear.GetComponent<Renderer>().sharedMaterial = matElevatorSteel;

        // INTERIOR WRECKAGE
        // Hanging Ceiling Light Fixture (dangling by electrical cord)
        GameObject lightBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightBar.name = "Light_Fluorescent_Dangling";
        lightBar.transform.parent = elevatorCar.transform;
        lightBar.transform.localPosition = new Vector3(0.15f, cabHeight - 0.7f, 0.25f);
        lightBar.transform.localRotation = Quaternion.Euler(42f, 18f, -24f);
        lightBar.transform.localScale = new Vector3(1.4f, 0.06f, 0.15f);
        lightBar.GetComponent<Renderer>().sharedMaterial = matFlickerLight;

        // Amber Point Light inside illuminating through door
        GameObject pointLightObj = new GameObject("Interior_Emergency_PointLight");
        pointLightObj.transform.parent = elevatorCar.transform;
        pointLightObj.transform.localPosition = new Vector3(0f, cabHeight * 0.55f, 0.1f);
        Light emergencyPoint = pointLightObj.AddComponent<Light>();
        emergencyPoint.type = LightType.Point;
        emergencyPoint.color = new Color(1.0f, 0.72f, 0.35f);
        emergencyPoint.intensity = 2.8f;
        emergencyPoint.range = 7.5f;
        emergencyPoint.shadows = LightShadows.Soft;

        // Smashed Control Panel on right wall
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "ControlPanel_Smashed";
        panel.transform.parent = elevatorCar.transform;
        panel.transform.localPosition = new Vector3(cabWidth * 0.5f - 0.18f, cabHeight * 0.45f, -0.4f);
        panel.transform.localRotation = Quaternion.Euler(0, 0, 14f);
        panel.transform.localScale = new Vector3(0.08f, 0.9f, 0.35f);
        panel.GetComponent<Renderer>().sharedMaterial = matWarningStripe;

        // Dangling colorful electrical wires
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

        // Snapped Braided Steel Hoist Cables with Realistic Catenary Arcs
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
                float h = Mathf.Sin(t * Mathf.PI * 0.85f) * (2.1f + i * 0.35f);
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

        // 3. 3D ORGANIC MOSS MOUNDS & OVERGROWTH
        GameObject mossParent = new GameObject("Moss_Overgrowth_Clusters");
        mossParent.transform.parent = root.transform;

        int mossCount = 42;
        for (int m = 0; m < mossCount; m++)
        {
            GameObject clump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clump.name = $"Moss_Clump_{m}";
            clump.transform.parent = mossParent.transform;

            float mAngle = Random.Range(0f, Mathf.PI * 2f);
            float mDist = (m < 24) ? Random.Range(0.8f, 2.5f) : Random.Range(2.2f, 4.6f);
            Vector3 mPos = new Vector3(Mathf.Cos(mAngle) * mDist, 0.06f, Mathf.Sin(mAngle) * mDist);

            // Tendency to cluster on the elevator car base
            if (m % 3 == 0)
            {
                mPos = elevatorCar.transform.position + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.2f, 0.6f), Random.Range(-1.2f, 1.2f));
            }

            clump.transform.position = mPos;
            clump.transform.localScale = new Vector3(Random.Range(0.25f, 0.75f), Random.Range(0.08f, 0.26f), Random.Range(0.25f, 0.75f));
            clump.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            
            Material mossInstance = new Material(matMoss);
            mossInstance.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            if (mossInstance.HasProperty("_BumpMap")) mossInstance.SetTextureScale("_BumpMap", new Vector2(3f, 3f));
            clump.GetComponent<Renderer>().sharedMaterial = mossInstance;
        }

        // 4. SCATTERED IMPACT DEBRIS & BROKEN CONCRETE CHUNKS
        GameObject debrisParent = new GameObject("Scattered_Rubble_Chunks");
        debrisParent.transform.parent = root.transform;

        int debrisCount = 36;
        for (int r = 0; r < debrisCount; r++)
        {
            GameObject scrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scrap.name = $"RubbleChunk_{r}";
            scrap.transform.parent = debrisParent.transform;

            float rAngle = Random.Range(0f, Mathf.PI * 2f);
            float rDist = Random.Range(1.2f, 5.0f);
            Vector3 rPos = new Vector3(Mathf.Cos(rAngle) * rDist, Random.Range(0.04f, 0.18f), Mathf.Sin(rAngle) * rDist);

            scrap.transform.position = rPos;
            scrap.transform.localScale = new Vector3(Random.Range(0.12f, 0.48f), Random.Range(0.08f, 0.28f), Random.Range(0.12f, 0.45f));
            scrap.transform.rotation = Random.rotation;

            scrap.GetComponent<Renderer>().sharedMaterial = (r % 2 == 0) ? matCrackedConcrete : matElevatorSteel;
        }

        Debug.Log("[ElevatorSceneBuilder] Realistic crashed totaled elevator scene successfully generated!");
    }
}
