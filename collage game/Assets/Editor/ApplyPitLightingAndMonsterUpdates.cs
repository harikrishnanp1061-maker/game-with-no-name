using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ApplyPitLightingAndMonsterUpdates
{
    [MenuItem("FLIP//FALL/Apply Pit Lighting and Monster Updates")]
    public static void ApplyUpdates()
    {
        Debug.Log("[ApplyPitLighting] Starting updates to active scene...");

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[ApplyPitLighting] No valid active scene!");
            return;
        }

        Transform lightsParent = null;
        GameObject lightsObj = GameObject.Find("Lights");
        if (lightsObj != null) lightsParent = lightsObj.transform;

        Transform area2Parent = null;
        GameObject area2Obj = GameObject.Find("02_PitCorridor");
        if (area2Obj != null) area2Parent = area2Obj.transform;

        Material matMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_Master_Metal.mat");
        Material matBulbGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PBR_Master_BulbGlow.mat");

        // 1. Configure Monster 8 Pit Illumination Light
        GameObject monster8 = GameObject.Find("Monster8_PitStalker");
        if (monster8 != null)
        {
            Transform existingLight = monster8.transform.Find("MonsterPitIlluminationLight");
            GameObject lightObj = existingLight != null ? existingLight.gameObject : new GameObject("MonsterPitIlluminationLight");
            lightObj.transform.SetParent(monster8.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.2f, 1.6f);
            
            Light mLight = lightObj.GetComponent<Light>();
            if (mLight == null) mLight = lightObj.AddComponent<Light>();
            mLight.type = LightType.Point;
            mLight.range = 10.0f;
            mLight.intensity = 3.6f;
            mLight.color = new Color(1.0f, 0.94f, 0.82f);
            mLight.shadows = LightShadows.None;
            mLight.renderMode = LightRenderMode.ForcePixel;
            Debug.Log("[ApplyPitLighting] Monster 8 illumination light successfully configured.");
        }
        else
        {
            Debug.LogWarning("[ApplyPitLighting] Monster8_PitStalker not found in active scene.");
        }

        // 2. Add weak wall lights above each of the 4 pits
        float maxZ = 53.6f;
        float[] pitXCoords = new float[] { 22.5f, 34.5f, 46.5f, 57.75f };
        for (int i = 0; i < pitXCoords.Length; i++)
        {
            float px = pitXCoords[i];
            int pitNum = i + 1;
            string lightName = $"Pit{pitNum}_Wall_WarningLight";
            string bracketName = $"Pit{pitNum}_WallSconce_Bracket";
            string bulbName = $"Pit{pitNum}_WallSconce_Bulb";

            // Remove existing if present to avoid duplication
            GameObject oldLight = GameObject.Find(lightName);
            if (oldLight != null) Object.DestroyImmediate(oldLight);
            GameObject oldBracket = GameObject.Find(bracketName);
            if (oldBracket != null) Object.DestroyImmediate(oldBracket);
            GameObject oldBulb = GameObject.Find(bulbName);
            if (oldBulb != null) Object.DestroyImmediate(oldBulb);

            // Bracket
            GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bracket.name = bracketName;
            bracket.transform.position = new Vector3(px, 2.0f, maxZ - 0.15f);
            bracket.transform.localScale = new Vector3(0.24f, 0.16f, 0.18f);
            if (matMetal != null) bracket.GetComponent<Renderer>().sharedMaterial = matMetal;
            if (area2Parent != null) bracket.transform.SetParent(area2Parent, true);

            // Emissive Bulb
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulb.name = bulbName;
            bulb.transform.position = new Vector3(px, 1.95f, maxZ - 0.26f);
            bulb.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            if (matBulbGlow != null) bulb.GetComponent<Renderer>().sharedMaterial = matBulbGlow;
            if (area2Parent != null) bulb.transform.SetParent(area2Parent, true);

            // Weak Amber Light
            GameObject lightGo = new GameObject(lightName);
            lightGo.transform.position = new Vector3(px, 1.90f, maxZ - 0.40f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.82f, 0.55f);
            l.intensity = 1.6f;
            l.range = 5.5f;
            l.shadows = LightShadows.None;
            if (lightsParent != null) lightGo.transform.SetParent(lightsParent, true);
        }
        Debug.Log("[ApplyPitLighting] 4 pit wall warning lights created.");

        // 3. Add soft pit bottom fill lights
        string westFillName = "Pit_Depth_Fill_West";
        string eastFillName = "Pit_Depth_Fill_East";
        GameObject oldWest = GameObject.Find(westFillName);
        if (oldWest != null) Object.DestroyImmediate(oldWest);
        GameObject oldEast = GameObject.Find(eastFillName);
        if (oldEast != null) Object.DestroyImmediate(oldEast);

        GameObject westFill = new GameObject(westFillName);
        westFill.transform.position = new Vector3(28.0f, -8.5f, 50.0f);
        Light lw = westFill.AddComponent<Light>();
        lw.type = LightType.Point;
        lw.color = new Color(0.85f, 0.88f, 0.95f);
        lw.intensity = 2.5f;
        lw.range = 15.0f;
        lw.shadows = LightShadows.None;
        if (lightsParent != null) westFill.transform.SetParent(lightsParent, true);

        GameObject eastFill = new GameObject(eastFillName);
        eastFill.transform.position = new Vector3(52.0f, -8.5f, 50.0f);
        Light le = eastFill.AddComponent<Light>();
        le.type = LightType.Point;
        le.color = new Color(0.85f, 0.88f, 0.95f);
        le.intensity = 2.5f;
        le.range = 15.0f;
        le.shadows = LightShadows.None;
        if (lightsParent != null) eastFill.transform.SetParent(lightsParent, true);

        Debug.Log("[ApplyPitLighting] Pit depth fill lights created.");

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ApplyPitLighting] Saved scene " + scene.name);
    }
}
