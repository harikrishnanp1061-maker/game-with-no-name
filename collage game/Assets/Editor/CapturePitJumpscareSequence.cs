using System.IO;
using UnityEngine;
using UnityEditor;

public static class CapturePitJumpscareSequence
{
    private const string OutDir = "Assets/Screenshots";
    private const string ArtifactsDir = "C:/Users/Harikrishnan/.gemini/antigravity-ide/brain/1c1ea72c-cd92-46f8-a15f-b7a6702fbc86";

    [MenuItem("FLIP-FALL/Capture Pit Jumpscare Sequence Shots")]
    public static void CaptureSequence()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        GameObject m8 = GameObject.Find("Monster8_PitStalker");
        if (m8 == null)
        {
            Debug.LogError("Monster8_PitStalker not found!");
            return;
        }

        Animator anim = m8.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("Animator not found on Monster8_PitStalker!");
            return;
        }

        // Camera inside the pit at fallen player perspective:
        // Pit floor is at -10.0m. Player head at -8.8m.
        Vector3 playerCamPos = new Vector3(56.2f, -8.8f, 50.0f);
        Vector3 m8HeadPos = new Vector3(58.8f, -8.0f, 50.0f);

        // Frame 1: Monster 8 on Idle breathing in pit
        anim.Play("Idle", 0, 0.2f);
        anim.Update(0f);
        CaptureShot(playerCamPos, m8HeadPos, "pit_01_idle_breathing.png", terrorWeight: 0.0f);

        // Frame 2: Player falls in, Monster 8 locks on & stares (2s stare delay begins, terror effect creeping in softly)
        Vector3 targetDir = (playerCamPos - m8.transform.position);
        targetDir.y = 0;
        m8.transform.rotation = Quaternion.LookRotation(targetDir.normalized);
        anim.Play("Idle", 0, 0.5f);
        anim.Update(0f);
        CaptureShot(playerCamPos, m8HeadPos, "pit_02_lockon_stare.png", terrorWeight: 0.35f);

        // Frame 3: Forward Jumpscare lunge (mid-lunge with roaring reach)
        anim.Play("Jumpscare", 0, 0.45f);
        anim.Update(0f);
        CaptureShot(playerCamPos, m8HeadPos, "pit_03_jumpscare_lunge.png", terrorWeight: 0.75f);

        // Frame 4: Peak Jumpscare hold frame (end frame held for 1s, terror effect at full peak reaching center)
        anim.Play("Jumpscare", 0, 0.95f);
        anim.Update(0f);
        CaptureShot(playerCamPos, m8HeadPos, "pit_04_peak_jumpscare_hold.png", terrorWeight: 1.0f);

        // Frame 5: Reverse animation back towards resting pose
        anim.Play("Jumpscare", 0, 0.30f);
        anim.Update(0f);
        CaptureShot(playerCamPos, m8HeadPos, "pit_05_reversing_animation.png", terrorWeight: 0.65f);

        // Frame 6: Settled back into Idle breathing
        anim.Play("Idle", 0, 0.1f);
        anim.Update(0f);
        CaptureShot(playerCamPos, m8HeadPos, "pit_06_return_to_idle.png", terrorWeight: 0.25f);

        AssetDatabase.Refresh();
        Debug.Log("[CapturePitJumpscareSequence] All sequence frames captured successfully!");
    }

    private static void CaptureShot(Vector3 pos, Vector3 lookAt, string filename, float terrorWeight)
    {
        // Temporarily configure terror screen effect volume weight for preview
        var terror = UnityEngine.Object.FindAnyObjectByType<MonsterTerrorScreenEffect>();
        UnityEngine.Rendering.Volume terrorVol = null;
        float prevWeight = 0f;
        if (terror != null)
        {
            terrorVol = terror.GetComponent<UnityEngine.Rendering.Volume>();
            if (terrorVol != null)
            {
                prevWeight = terrorVol.weight;
                // Use smoothstep visual curve matching MonsterTerrorScreenEffect
                terrorVol.weight = terrorWeight * terrorWeight * (3f - 2f * terrorWeight);
            }
        }

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

        // Restore terror volume weight
        if (terrorVol != null)
        {
            terrorVol.weight = prevWeight;
        }

        byte[] bytes = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        string localPath = Path.Combine(OutDir, filename);
        File.WriteAllBytes(localPath, bytes);

        if (Directory.Exists(ArtifactsDir))
        {
            string artifactPath = Path.Combine(ArtifactsDir, filename);
            File.WriteAllBytes(artifactPath, bytes);
        }

        Debug.Log($"Saved sequence frame: {localPath}");
    }
}
