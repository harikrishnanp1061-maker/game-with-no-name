using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower
{
    /// <summary>
    /// Skipping ahead from inside a level: hold K for two seconds in the tutorial to go up to the tower, or press 3
    /// (tutorial or tower) to go straight to level 3, the campus. In a session only the host can skip (it moves
    /// everyone); the others see who can. Put into those scenes when they load.
    /// </summary>
    public class TutorialSkip : MonoBehaviour
    {
        public KeyCode key = KeyCode.K;
        public float holdTime = 2f;

        float held;
        bool skipping;
        GUIStyle style;
        Texture2D white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (scene, mode) => { if (Next(scene.name) != null) new GameObject("TutorialSkip").AddComponent<TutorialSkip>(); };
            if (Next(SceneManager.GetActiveScene().name) != null && FindAnyObjectByType<TutorialSkip>() == null) new GameObject("TutorialSkip").AddComponent<TutorialSkip>();
        }

        /// <summary>Where skipping this level goes (null: no skip here).</summary>
        static string Next(string scene)
        {
            if (scene == Net.NetLobby.FirstLevel) return Net.NetLobby.TowerLevel;
            if (scene == Net.NetLobby.TowerLevel) return Net.NetLobby.CampusLevel;   // only by [3]: K does nothing in the tower
            return null;
        }

        public KeyCode levelThreeKey = KeyCode.Alpha3;

        void Skip(string to, string message)
        {
            skipping = true;
            var party = PlayerParty.Instance;
            if (party != null) { party.Flash(message, 3f); party.SetFade(1f); }
            Net.NetLobby.LoadLevel(to);
        }

        static bool InTower { get { return SceneManager.GetActiveScene().name == Net.NetLobby.TowerLevel; } }

        bool CanSkip { get { return Net.Net.IsAuthority; } }

        void Update()
        {
            if (skipping || !CanSkip || RemasteredMapHUD.IsFullMapOpen || Puzzles.PuzzlePanel.IsOpen) { held = 0f; return; }
            if (DevTools.Enabled && InputWrap.Down(levelThreeKey)) { Skip(Net.NetLobby.CampusLevel, "Going to level 3 - the campus..."); return; }
            if (InTower) return;
            if (InputWrap.Held(key)) held += Time.unscaledDeltaTime; else held = 0f;
            if (held >= holdTime) Skip(Net.NetLobby.TowerLevel, "Skipping the tutorial - going up to the tower...");
        }

        void OnGUI()
        {
            UiStyle.Use();
            if (skipping || (InTower && !DevTools.Enabled)) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleRight };
                style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                white = Texture2D.whiteTexture;
            }
            var r = new Rect(Screen.width - 330f, 12f, 318f, 22f);
            string text = InTower ? (CanSkip ? "[3] Go to level 3" : "The host can go to level 3 [3]")
                                  : (CanSkip ? "Hold [K] to skip the tutorial" + (DevTools.Enabled ? "  -  [3] level 3" : "") : "The host can skip the tutorial (hold K)");
            GUI.Label(r, text, style);
            if (held > 0f)
            {
                var bar = new Rect(Screen.width - 230f, 36f, 218f, 5f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(bar, white);
                GUI.color = new Color(1f, 0.85f, 0.3f, 0.9f); GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(held / holdTime), bar.height), white);
                GUI.color = Color.white;
            }
        }
    }
}
