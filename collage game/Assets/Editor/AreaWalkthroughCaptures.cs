using System.IO;
using UnityEngine;
using UnityEditor;

public static class AreaWalkthroughCaptures
{
    private const string OutDir = "Assets/Screenshots";

    private static (GameObject player, Camera cam) FindPlayerAndCamera()
    {
        Camera cam = Camera.main;
        GameObject player = GameObject.Find("Player_1");
        if (player == null) player = GameObject.Find("TutorialScene/Player_Cylinder");
        if (player == null) player = GameObject.Find("Player_FPS");
        if (player == null && cam != null) player = cam.gameObject;
        if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (allCams.Length > 0) cam = allCams[0];
        }
        return (player, cam);
    }

    [MenuItem("Tools/FLIP//FALL/Capture Entity 38 Creature Room")]
    public static void CaptureEntity38CreatureRoom()
    {
        // Area 5 center is (88, 0, 11). Entity 38 is at (88, 0.05, 11).
        // Capture dramatic full-body shot from (88, 2.1, 16.5) looking slightly down at (88, 1.8, 11)
        CaptureViewFromPoint(new Vector3(88.0f, 2.1f, 16.5f), new Vector3(88.0f, 1.8f, 11.0f), "entity38_creature_room.png");
        AssetDatabase.Refresh();
        Debug.Log("[AreaWalkthroughCaptures] Entity 38 Creature Room shot saved!");
    }

    [MenuItem("Tools/FLIP//FALL/Capture All 4 Players at Spawn")]
    public static void CaptureAll4PlayersAtSpawn()
    {
        // Spaced out around crumpled cab (0, 0.4, 50).
        // Wide shot from southwest corner looking across the crash site
        CaptureViewFromPoint(new Vector3(-8.2f, 2.4f, 41.5f), new Vector3(0.0f, 1.2f, 50.0f), "all_4_players_spawn.png");
        AssetDatabase.Refresh();
        Debug.Log("[AreaWalkthroughCaptures] All 4 Players at Spawn shot saved!");
    }

    [MenuItem("Tools/FLIP//FALL/Capture All Area Views")]
    public static void CaptureAll()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var (player, cam) = FindPlayerAndCamera();
        if (player == null || cam == null)
        {
            Debug.LogError("[AreaWalkthroughCaptures] Player or Camera not found!");
            return;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Area 1: Crash Room (showing totaled cab, crater, wall sconce & ceiling tube fixtures)
        SetPoseLookAt(player, cam, new Vector3(-6.5f, 0.0f, 45.0f), new Vector3(0.0f, 2.5f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "area1_crash_room.png"));

        // Area 2: Pit Corridor looking down with ceiling fluorescent troffers
        SetPoseLookAt(player, cam, new Vector3(15.0f, 0.0f, 50.0f), new Vector3(30.0f, 3.6f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "area2_pit_corridor.png"));

        // Area 3: Pit Corridor Section C (Special Hidden Pit with Red Dev Marker & Overhead Hazard Beacon)
        SetPoseLookAt(player, cam, new Vector3(50.0f, 0.0f, 50.0f), new Vector3(57.75f, 3.4f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "area3_hidden_pit_red.png"));

        // Area 4: Hidden Door Chamber (Cyan Glowing Dev Marker & Overhead Fluorescent Troffer)
        SetPoseLookAt(player, cam, new Vector3(88.5f, 0.0f, 50.0f), new Vector3(95.0f, 3.8f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "area4_hidden_door_cyan.png"));

        // Area 4 -> Area 5 Secret Passage View looking South through the entrance portal into Creature Room
        SetPoseLookAt(player, cam, new Vector3(102.0f, 1.6f, 28.0f), new Vector3(102.0f, 1.6f, 11.0f));
        Capture(cam, Path.Combine(OutDir, "area4_to_area5_passage.png"));

        // Area 5: Creature Room / Waiting Area (Wide angle showing suspended caged hanging lamps)
        SetPoseLookAt(player, cam, new Vector3(76.0f, 0.0f, 4.0f), new Vector3(86.0f, 3.8f, 11.0f));
        Capture(cam, Path.Combine(OutDir, "area5_creature_room.png"));

        // Area 6: Connecting Hallway (Ceiling troffers with progressive dimming & wall sconce)
        SetPoseLookAt(player, cam, new Vector3(68.0f, 0.0f, 11.0f), new Vector3(56.0f, 3.6f, 11.0f));
        Capture(cam, Path.Combine(OutDir, "area6_connecting_hallway.png"));

        // Area 7: Dark Fuse Room (Focusing on central workbench with the green SM_FuseCell hero asset)
        SetPoseLookAt(player, cam, new Vector3(39.0f, 0.0f, 9.2f), new Vector3(36.0f, 1.1f, 11.0f));
        Capture(cam, Path.Combine(OutDir, "area7_dark_fuse_room_green.png"));

        // Area 7: Exact user screenshot angle (Image 2) facing workbench table with the prominent glowing fuse cell
        SetPoseLookAt(player, cam, new Vector3(36.0f, 0.0f, 13.8f), new Vector3(36.0f, 1.05f, 11.0f));
        Capture(cam, Path.Combine(OutDir, "area7_fuse_table_user_view.png"));

        // Area 8: Final Exit Elevator Room (Front view of the 3D working elevator hero asset)
        SetPoseLookAt(player, cam, new Vector3(-4.5f, 0.0f, 11.0f), new Vector3(-9.5f, 1.6f, 11.0f));
        Capture(cam, Path.Combine(OutDir, "area8_exit_elevator.png"));

        // Area 8: Close-up of elevator power module with preinstalled cells & empty socket 3
        SetPoseLookAt(player, cam, new Vector3(-7.5f, 0.0f, 12.2f), new Vector3(-9.0f, 1.4f, 11.9f));
        Capture(cam, Path.Combine(OutDir, "area8_elevator_sockets_closeup.png"));

        // Reset player back to spawn
        SetPose(player, cam, new Vector3(-6.5f, 0.5f, 45.0f), Quaternion.Euler(0f, 50f, 0f));
        if (cc != null) cc.enabled = true;

        AssetDatabase.Refresh();
        Debug.Log("[AreaWalkthroughCaptures] All 8 area views captured to Assets/Screenshots/");
    }

    [MenuItem("Tools/FLIP//FALL/Capture Crash Room Detailed")]
    public static void CaptureCrashRoomDetailed()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var (player, cam) = FindPlayerAndCamera();
        if (player == null || cam == null)
        {
            Debug.LogError("[AreaWalkthroughCaptures] Player or Camera not found!");
            return;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Temporarily disable the player's visual mesh so it never clips the camera
        Transform visual = player.transform.Find("VisualCylinder");
        bool wasVisualActive = visual != null && visual.gameObject.activeSelf;
        if (visual != null) visual.gameObject.SetActive(false);

        // 1. Wide view from player spawn showing room, totaled cab, cracked ceiling & exit doorway
        SetPoseLookAt(player, cam, new Vector3(-8.0f, 0.0f, 41.0f), new Vector3(0.0f, 1.8f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_wide.png"));

        // 2. Upward view looking directly into the fractured ceiling and enclosed shaft
        SetPoseLookAt(player, cam, new Vector3(-2.2f, 0.0f, 45.5f), new Vector3(0.0f, 11.0f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_ceiling_shaft.png"));

        // 3. Close-up view of the crumpled cab & jammed doors & flickering amber interior
        SetPoseLookAt(player, cam, new Vector3(-1.6f, 0.0f, 46.8f), new Vector3(0.1f, 1.5f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_cab_closeup.png"));

        // 4. View from elevator interior looking out
        SetPoseLookAt(player, cam, new Vector3(0.5f, 0.5f, 50.5f), new Vector3(-8.0f, 1.6f, 45.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_cab_interior.png"));

        // 5. Cinematic view from East exit doorway looking back at the whole crash room & West wall
        SetPoseLookAt(player, cam, new Vector3(10.0f, 0.0f, 50.0f), new Vector3(-4.0f, 1.8f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_from_exit.png"));

        // 6. Impact crater and displaced concrete slabs
        SetPoseLookAt(player, cam, new Vector3(-5.0f, 0.0f, 44.0f), new Vector3(0.0f, 0.4f, 50.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_floor_crater.png"));

        // 7. West Maintenance Alcove (Shelf, Cement Sacks, Mobile Tool Cart, Electrical Junction)
        SetPoseLookAt(player, cam, new Vector3(-6.5f, 0.0f, 45.0f), new Vector3(-12.2f, 1.2f, 45.0f));
        Capture(cam, Path.Combine(OutDir, "crashroom_west_alcove.png"));

        // 8. Risen End Concrete Support (view matching user's exact perspective)
        SetPoseLookAt(player, cam, new Vector3(-4.8f, 0.0f, 51.2f), new Vector3(-1.8f, 0.75f, 49.4f));
        Capture(cam, Path.Combine(OutDir, "area1_elevator_risen_support.png"));

        // 9. Hazard Sign and Electrical Power Box on West wall (close-up verification)
        SetPoseLookAt(player, cam, new Vector3(-11.2f, 0.9f, 48.5f), new Vector3(-12.8f, 2.55f, 48.5f));
        Capture(cam, Path.Combine(OutDir, "area1_hazard_sign_closeup.png"));

        // Restore visual cylinder & reset player
        if (visual != null) visual.gameObject.SetActive(wasVisualActive);
        SetPose(player, cam, new Vector3(-6.5f, 0.5f, 45.0f), Quaternion.Euler(0f, 50f, 0f));
        if (cc != null) cc.enabled = true;

        AssetDatabase.Refresh();
        Debug.Log("[AreaWalkthroughCaptures] Detailed Crash Room screenshots captured!");
    }

    private static void SetPoseLookAt(GameObject player, Camera cam, Vector3 playerPos, Vector3 lookTarget)
    {
        player.transform.position = playerPos;
        Physics.SyncTransforms();
        Vector3 camPos = playerPos + new Vector3(0f, 1.6f, 0f);
        Vector3 dir = (lookTarget - camPos).normalized;
        Quaternion rot = Quaternion.LookRotation(dir);

        cam.transform.position = camPos;
        cam.transform.rotation = rot;
    }

    private static void SetPose(GameObject player, Camera cam, Vector3 pos, Quaternion rot)
    {
        player.transform.position = pos;
        Physics.SyncTransforms();
        Vector3 camPos = pos + new Vector3(0f, 1.6f, 0f);
        cam.transform.position = camPos;
        cam.transform.rotation = rot;
    }

    public static void CaptureViewFromPoint(Vector3 pos, Vector3 lookAt, string filename)
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
        GameObject camObj = new GameObject("TempCaptureCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 150f;
        cam.fieldOfView = 75f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.012f, 0.012f, 0.015f);

        var camData = camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.renderShadows = true;

        camObj.transform.position = pos;
        camObj.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized);

        Capture(cam, Path.Combine(OutDir, filename));
        Object.DestroyImmediate(camObj);
    }

    private static void Capture(Camera cam, string path)
    {
        int w = 1920, h = 1080;
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prevTarget;

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        RenderTexture.active = prevActive;
        Object.DestroyImmediate(rt);

        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(path, bytes);
        Debug.Log("Saved screenshot: " + path);
    }
}
