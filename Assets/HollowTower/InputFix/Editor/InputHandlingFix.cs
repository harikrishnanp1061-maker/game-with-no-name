using System.IO;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Some Unity versions start new projects with only the *new* Input System switched on,
    /// but without the package installed, which stops scripts compiling. This lives in its
    /// own assembly so it still runs in that case, and switches the project back to the
    /// classic Input Manager (one editor restart).
    /// </summary>
    [InitializeOnLoad]
    static class InputHandlingFix
    {
        static InputHandlingFix()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 1) return; // 0 = classic, 1 = new only, 2 = both

            string manifest = "Packages/manifest.json";
            if (File.Exists(manifest) && File.ReadAllText(manifest).Contains("com.unity.inputsystem")) return;

            prop.intValue = 0;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Hollow Tower: switched Active Input Handling to the classic Input Manager.");
            if (EditorUtility.DisplayDialog("Hollow Tower",
                "Switched input handling to the classic Input Manager so the game's scripts compile.\nUnity needs to restart once.",
                "Restart now", "Later"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }
    }
}
