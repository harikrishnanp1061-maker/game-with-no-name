using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class PlayModePitJumpscareTest
{
    private const string OutDir = "Assets/Screenshots";
    private const string ArtifactsDir = "C:/Users/Harikrishnan/.gemini/antigravity-ide/brain/1c1ea72c-cd92-46f8-a15f-b7a6702fbc86";

    [MenuItem("FLIP-FALL/Test Pit Jumpscare In PlayMode")]
    public static void RunTest()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        else
        {
            ExecuteSequence();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += () =>
            {
                ExecuteSequence();
            };
        }
    }

    private static void ExecuteSequence()
    {
        var runner = new GameObject("PitTestRunner").AddComponent<PitTestRunnerMono>();
        Object.DontDestroyOnLoad(runner.gameObject);
    }

    public class PitTestRunnerMono : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(SequenceCoroutine());
        }

        private IEnumerator SequenceCoroutine()
        {
            Debug.Log("[PitJumpscareTest] Starting sequence in Play Mode...");
            yield return new WaitForSeconds(1.5f);

            var m8 = GameObject.Find("Monster8_PitStalker");
            var pitComp = m8 != null ? m8.GetComponent<PitMonsterJumpscare>() : null;
            var terror = Object.FindAnyObjectByType<MonsterTerrorScreenEffect>();

            // Find player or camera
            Camera cam = Camera.main;
            Transform playerTrans = null;
            if (cam != null) playerTrans = cam.transform;

            // Teleport player/camera into the pit chamber at (56.0, -8.8, 50.0) looking at Monster 8
            if (cam != null)
            {
                cam.transform.position = new Vector3(56.0f, -8.8f, 50.0f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(58.8f, -8.2f, 50.0f) - cam.transform.position);
            }

            // Capture initial Idle Stare before fall trigger
            CaptureFrame("jumpscare_01_idle.png");

            // Trigger Pit Fall
            Debug.Log("[PitJumpscareTest] Triggering Pit Fall...");
            if (pitComp != null)
            {
                pitComp.TriggerPitFall(cam != null ? cam.transform : transform);
            }
            if (terror != null)
            {
                terror.SetPitTerror(true, 1.0f);
            }

            // 1.0s into the 2.0s stare delay: Monster locking on, terror effect smoothly emerging
            yield return new WaitForSeconds(1.0f);
            CaptureFrame("jumpscare_02_lockon_stare.png");

            // Wait until 2.2s: Stare delay (2.0s) has elapsed, forward jumpscare lunge has triggered!
            yield return new WaitForSeconds(1.2f);
            CaptureFrame("jumpscare_03_forward_lunge.png");

            // Peak jumpscare hold frame (~3.8s into jumpscare)
            yield return new WaitForSeconds(2.0f);
            CaptureFrame("jumpscare_04_peak_jumpscare.png");

            // Reverse animation back (~1.5s into reverse)
            yield return new WaitForSeconds(1.8f);
            CaptureFrame("jumpscare_05_reversing_to_idle.png");

            // Back to idle breathing with full terror screen atmosphere
            yield return new WaitForSeconds(2.0f);
            CaptureFrame("jumpscare_06_idle_breathing_terror.png");

            Debug.Log("[PitJumpscareTest] All Play Mode test frames captured!");

            yield return new WaitForSeconds(1.0f);
            // End Play Mode
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
            Destroy(gameObject);
        }

        private void CaptureFrame(string filename)
        {
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

            int w = 1920, h = 1080;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;

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
            Destroy(rt);

            byte[] bytes = tex.EncodeToPNG();
            Destroy(tex);

            string localPath = Path.Combine(OutDir, filename);
            File.WriteAllBytes(localPath, bytes);

            if (Directory.Exists(ArtifactsDir))
            {
                string artifactPath = Path.Combine(ArtifactsDir, filename);
                File.WriteAllBytes(artifactPath, bytes);
            }

            Debug.Log($"[PitJumpscareTest] Saved: {localPath}");
        }
    }
}
