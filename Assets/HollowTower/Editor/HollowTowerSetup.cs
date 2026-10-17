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
        const string ScenePath = "Assets/HollowTower/Scenes/HollowTower.unity";
        const string DoneKey = "HollowTower.SetupDone";

        static HollowTowerSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath))
            {
                CreateScene();
                return;
            }
            // Play always starts the game, whatever scene happens to be open in the editor.
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset != null) EditorSceneManager.playModeStartScene = sceneAsset;

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
                if (string.IsNullOrEmpty(active.path) && active.rootCount <= 4)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("Hollow Tower/Create or Reset Game Scene", priority = 1)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Hollow Tower (press Play)");
            go.AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
            SessionState.SetBool(DoneKey, true);
            Debug.Log("Hollow Tower: scene created at " + ScenePath + ". Press Play to start.");
        }

        [MenuItem("Hollow Tower/Play", priority = 2)]
        public static void Play()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Hollow Tower/Graphics/Install HDRP (step 1 of the ray tracing upgrade)", priority = 20)]
        public static void InstallHdrp()
        {
            if (!EditorUtility.DisplayDialog("Install HDRP?",
                "This adds Unity's High Definition Render Pipeline package, which is needed for ray tracing.\n\n" +
                "Afterwards open Window > Rendering > HDRP Wizard and press 'Fix All', then on the 'HDRP + DXR' tab press 'Fix All' again " +
                "to turn on ray tracing (needs a DirectX 12 GPU with ray tracing: NVIDIA RTX / AMD RX 6000 or newer).\n\n" +
                "The game's custom post effects switch off under HDRP; add a Volume for HDRP effects instead. See README.md.",
                "Install", "Cancel")) return;
            Client.Add("com.unity.render-pipelines.high-definition");
            Debug.Log("Hollow Tower: installing HDRP... watch the Package Manager progress bar.");
        }

        [MenuItem("Hollow Tower/Open README", priority = 40)]
        public static void OpenReadme()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "README.md"));
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
        }
    }
}
