using UnityEditor;
using UnityEditor.SceneManagement;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// DEV: pressing Play in the editor starts in level 3 (the campus, in the arrival elevator) whatever scene is open,
    /// to save walking through the tutorial and the tower. Toggle with Hollow Tower > Dev > Play Starts In Level 3.
    /// </summary>
    [InitializeOnLoad]
    public static class DevPlayStart
    {
        const string Pref = "HollowTower.DevPlayStartsInCampus";
        const string Menu = "Hollow Tower/Dev/Play Starts In Level 3";

        static DevPlayStart() { EditorApplication.delayCall += Apply; }

        static bool On { get { return EditorPrefs.GetBool(Pref, false); } }

        static void Apply()
        {
            // off: Play runs the whole game from the Lobby (the intro), like the built game
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(On ? CampusBuilder.ScenePath : "Assets/HollowTower/Scenes/Lobby.unity");
            Menu_Validate();
        }

        [MenuItem(Menu, priority = 95)]
        static void Toggle()
        {
            EditorPrefs.SetBool(Pref, !On);
            Apply();
        }

        [MenuItem(Menu, true)]
        static bool Menu_Validate()
        {
            UnityEditor.Menu.SetChecked(Menu, On);
            return true;
        }
    }
}
