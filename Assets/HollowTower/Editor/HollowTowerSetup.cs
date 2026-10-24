using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// One-time project setup. The first time Unity opens this project it creates
    /// Assets/HollowTower/Scenes/HollowTower.unity, adds it to Build Settings and opens it.
    /// After that, just press Play.
    /// </summary>
    [InitializeOnLoad]
    public static class HollowTowerSetup
    {
        const string ScenePath = "Assets/HollowTowerRemastered/Scenes/HollowTower_Remastered.unity";
        const string DoneKey = "HollowTower.SetupDone";

        static HollowTowerSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Allow playing whichever scene is currently open in the editor
            EditorSceneManager.playModeStartScene = null;

            // The HDRP Wizard only needs running once; stop it popping up over the game on every launch.
            var hdUser = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.Rendering.HighDefinition.HDUserSettings", false)).FirstOrDefault(x => x != null);
            var popup = hdUser != null ? hdUser.GetProperty("wizardIsStartPopup", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;
            if (popup != null && popup.CanWrite) popup.SetValue(null, false);

            // Open our scene on launch if the editor is sitting on a new untitled scene (HDRP's default one counts).
            if (!SessionState.GetBool(DoneKey, false))
            {
                SessionState.SetBool(DoneKey, true);
                var active = SceneManager.GetActiveScene();
                if ((string.IsNullOrEmpty(active.path) && active.rootCount <= 4) && File.Exists(ScenePath))
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("Hollow Tower/Play Game", priority = 2)]
        public static void Play()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Hollow Tower/Graphics/Set Graphics API to DirectX 11", priority = 20)]
        public static void SetDirectX11()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[Graphics]</color> PlayerSettings Graphics API configured for DirectX 11.");
        }

        [MenuItem("Hollow Tower/Graphics/Restart Editor in DirectX 11", priority = 21)]
        public static void RestartInDx11()
        {
            SetDirectX11();
            if (EditorUtility.DisplayDialog("Restart in DirectX 11?",
                "Unity will now save open scenes and restart with '-force-d3d11' to initialize the Direct3D 11 backend.", "Restart", "Cancel"))
            {
                EditorSceneManager.SaveOpenScenes();
                EditorApplication.OpenProject(Directory.GetCurrentDirectory(), "-force-d3d11");
            }
        }

        [MenuItem("Hollow Tower/Open README", priority = 40)]
        public static void OpenReadme()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "README.md"));
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
        }
    }
}
