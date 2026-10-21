using System.IO;
using UnityEngine;
using UnityEditor;

public static class CapturePitMonsterViews
{
    private const string OutDir = "Assets/Screenshots";
    private const string ArtifactsDir = "C:/Users/Harikrishnan/.gemini/antigravity-ide/brain/1c1ea72c-cd92-46f8-a15f-b7a6702fbc86";

    [MenuItem("FLIP-FALL/Capture Monster 8 Pit Views")]
    public static void CaptureAllViews()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        // Shot 1: Down the pit shaft from walkway looking down at Monster 8 at (58.8, -9.75, 50)
        // Camera positioned at edge of the pit (54.5, 0.5, 50) looking down into the pit chamber (58.5, -9.0, 50)
        CaptureShot(
            new Vector3(54.0f, 0.8f, 50.0f),
            new Vector3(58.5f, -8.5f, 50.0f),
            "pit_monster8_lookdown.png");

        // Shot 2: Fallen player perspective inside the pit chamber (56.0, -8.8, 50) looking directly at Monster 8 (58.8, -8.0, 50)
        CaptureShot(
            new Vector3(56.0f, -8.8f, 50.0f),
            new Vector3(58.8f, -8.0f, 50.0f),
            "pit_monster8_eyelevel.png");

        // Shot 3: Wider 3/4 angle inside the pit chamber showing full body and chamber walls (56.2, -7.5, 47.8) looking at (58.8, -8.0, 50.0)
        CaptureShot(
            new Vector3(56.2f, -7.5f, 47.8f),
            new Vector3(58.8f, -8.0f, 50.0f),
            "pit_monster8_fullbody.png");

        AssetDatabase.Refresh();
        Debug.Log("[CapturePitMonsterViews] All Monster 8 pit screenshots captured!");
    }

    private static void CaptureShot(Vector3 pos, Vector3 lookAt, string filename)
    {
        GameObject camObj = new GameObject("TempPitCaptureCam");
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
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(camObj);

        byte[] bytes = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        string localPath = Path.Combine(OutDir, filename);
        File.WriteAllBytes(localPath, bytes);

        if (Directory.Exists(ArtifactsDir))
        {
            string artifactPath = Path.Combine(ArtifactsDir, filename);
            File.WriteAllBytes(artifactPath, bytes);
        }

        Debug.Log($"Saved screenshot: {localPath}");
    }
}
