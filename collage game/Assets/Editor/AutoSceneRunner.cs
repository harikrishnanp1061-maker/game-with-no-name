using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AutoSceneRunner
{
    static AutoSceneRunner()
    {
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

        // 1. Capture Player FPS view (standing in entrance arch looking at the illuminated crash scene)
        string fpsShotPath = Path.Combine(capturesDir, "realistic_fps_view.png");
        RenderCameraToDisk(cam, fpsShotPath, 1920, 1080);
        Debug.Log($"[AutoSceneRunner] Saved realistic FPS view: {fpsShotPath}");

        // Save original camera transform
        Vector3 origPos = cam.transform.position;
        Quaternion origRot = cam.transform.rotation;
        float origFov = cam.fieldOfView;
        Transform origParent = cam.transform.parent;

        // 2. Capture Hero Overview angle (angled three-quarter view showing elevator, cables, lights, and room)
        cam.transform.parent = null;
        cam.transform.position = new Vector3(-3.8f, 2.2f, -3.2f);
        cam.transform.LookAt(new Vector3(0.2f, 1.4f, 1.0f));
        cam.fieldOfView = 58f;

        string heroShotPath = Path.Combine(capturesDir, "realistic_hero_view.png");
        RenderCameraToDisk(cam, heroShotPath, 1920, 1080);
        Debug.Log($"[AutoSceneRunner] Saved realistic Hero view: {heroShotPath}");

        // 3. Capture Room Wide Overhead angle (showing full room architecture, ceiling breach, columns, and lighting)
        cam.transform.position = new Vector3(5.6f, 4.8f, -5.5f);
        cam.transform.LookAt(new Vector3(0f, 1.6f, 1.2f));
        cam.fieldOfView = 65f;

        string roomShotPath = Path.Combine(capturesDir, "realistic_room_wide_view.png");
        RenderCameraToDisk(cam, roomShotPath, 1920, 1080);
        Debug.Log($"[AutoSceneRunner] Saved realistic Room Wide view: {roomShotPath}");

        // Restore original camera transform
        cam.transform.parent = origParent;
        cam.transform.position = origPos;
        cam.transform.rotation = origRot;
        cam.fieldOfView = origFov;

        Debug.Log("[AutoSceneRunner] Room Pipeline & Screenshots successfully complete!");
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
