using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupFootstepAudioTracks
{
    private const string WalkingTrackPath = "Assets/Audio/SFX/Player/freesound_community-concrete-footsteps-6752.mp3";
    private const string RunningTrackPath = "Assets/Audio/SFX/Player/freesound_community-running-sounds-6003.mp3";

    [InitializeOnLoadMethod]
    private static void OnProjectLoad()
    {
        EditorApplication.delayCall += () =>
        {
            ApplyTracksToScenePlayers();
        };
    }

    [MenuItem("FLIP-FALL/Apply Footstep Audio Tracks To Players")]
    public static void ApplyTracksToScenePlayers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        AudioClip walkClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WalkingTrackPath);
        AudioClip runClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RunningTrackPath);

        if (walkClip == null || runClip == null)
        {
            Debug.LogError($"[FootstepAudioSetup] Could not load audio tracks! Walk: {walkClip != null}, Run: {runClip != null}");
            return;
        }

        // Run SetupFourPlayerCharacters to ensure all 4 characters are fully synchronized
        SetupFourPlayerCharacters.Build();

        var allFps = UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (var fps in allFps)
        {
            fps.walkingAudioTrack = walkClip;
            fps.runningAudioTrack = runClip;
            EditorUtility.SetDirty(fps);
            Debug.Log($"[FootstepAudioSetup] Assigned walk & run tracks to {fps.gameObject.name}");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FootstepAudioSetup] Successfully saved scene {scene.name} with updated footstep tracks!");
        }
    }
}
