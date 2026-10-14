using System.IO;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class AutoSceneRunner
{
    private const string LatchKey = "AutoRealisticSceneRun_Latch_v6";

    static AutoSceneRunner()
    {
        EditorApplication.delayCall += OnEditorReady;
    }

    private static void OnEditorReady()
    {
        if (SessionState.GetBool(LatchKey, false)) return;
        SessionState.SetBool(LatchKey, true);

        Debug.Log("[AutoSceneRunner] Executing full procedural PBR realistic pipeline...");

        // 1. Generate PBR Textures & Normal Maps & Materials
        PbrTextureGenerator.GenerateAll();

        // 2. Build the detailed Crashed Elevator Scene
        ElevatorSceneBuilder.BuildScene();

        // 3. Setup Player, Directional Sun, Fog, Ambient, and URP Post-Processing
        SetupPlayerAndLighting.Setup();

        // 4. Ensure MCP Bridge connects
        try
        {
            MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost.StartAutoConnect();
        }
        catch { }

        // 5. Capture Screenshots from both viewpoints
        EditorApplication.delayCall += () =>
        {
            CaptureRenderScreenshots();
        };
    }

    [MenuItem("Tools/Re-Run Realistic Pipeline & Screenshots")]
    public static void ManualRun()
    {
        PbrTextureGenerator.GenerateAll();
        ElevatorSceneBuilder.BuildScene();
        SetupPlayerAndLighting.Setup();
        CaptureRenderScreenshots();
    }

    public static void CaptureRenderScreenshots()
    {
        string capturesDir = Path.Combine(Directory.GetCurrentDirectory(), "Captures");
        if (!Directory.Exists(capturesDir)) Directory.CreateDirectory(capturesDir);

        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("[AutoSceneRunner] No Camera found for screenshot capture.");
            return;
        }

        // 1. Capture Player FPS view
        string fpsShotPath = Path.Combine(capturesDir, "realistic_fps_view.png");
        RenderCameraToDisk(cam, fpsShotPath, 1920, 1080);
        Debug.Log($"[AutoSceneRunner] Saved realistic FPS view: {fpsShotPath}");

        // 2. Capture Hero Overview angle
        Vector3 origPos = cam.transform.position;
        Quaternion origRot = cam.transform.rotation;
        float origFov = cam.fieldOfView;
        Transform origParent = cam.transform.parent;

        cam.transform.parent = null;
        cam.transform.position = new Vector3(-3.2f, 1.8f, -3.8f);
        cam.transform.LookAt(new Vector3(0.1f, 0.95f, 0f));
        cam.fieldOfView = 50f;

        string heroShotPath = Path.Combine(capturesDir, "realistic_hero_view.png");
        RenderCameraToDisk(cam, heroShotPath, 1920, 1080);
        Debug.Log($"[AutoSceneRunner] Saved realistic Hero view: {heroShotPath}");

        // Restore original camera transform
        cam.transform.parent = origParent;
        cam.transform.position = origPos;
        cam.transform.rotation = origRot;
        cam.fieldOfView = origFov;

        Debug.Log("[AutoSceneRunner] Pipeline & Screenshots complete!");
    }

    private static void RenderCameraToDisk(Camera camera, string filePath, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = camera.targetTexture;
        camera.targetTexture = rt;
        camera.Render();
        camera.targetTexture = prevTarget;

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D screenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenTex.Apply();

        RenderTexture.active = prevActive;
        Object.DestroyImmediate(rt);

        byte[] pngBytes = screenTex.EncodeToPNG();
        Object.DestroyImmediate(screenTex);

        File.WriteAllBytes(filePath, pngBytes);
    }
}
