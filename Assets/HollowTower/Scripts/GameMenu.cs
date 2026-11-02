using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower
{
    /// <summary>
    /// The in-game menu, [Esc] in any level (each player has their own): resume, volume, the developer options switch
    /// (off by default - the dev maps, warps, puzzle skips, passive monsters...), back to the lobby, quit.
    /// The world keeps going while it is open (it is a co-op game); the player stands still.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }
        static GameMenu instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (instance != null) return;
            var go = new GameObject("GameMenu"); DontDestroyOnLoad(go);
            instance = go.AddComponent<GameMenu>();
            SceneManager.activeSceneChanged += (a, b) => { if (Open) instance.Close(); };
        }

        static bool InGame { get { return PlayerParty.Instance != null && SceneManager.GetActiveScene().name != Net.NetLobby.LobbyScene; } }

        void Update()
        {
            if (!InGame || CinemaScreen.Showing) { if (Open) Close(); return; }
            if (!InputWrap.Down(KeyCode.Escape)) return;
            if (Open) { Close(); return; }
            // Esc first closes whatever else is up (a puzzle, the console, the full map)
            if (Puzzles.PuzzlePanel.IsOpen || RemasteredMapHUD.IsFullMapOpen) return;
            Open = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Close()
        {
            Open = false;
            if (InGame && !Puzzles.PuzzlePanel.IsOpen) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        void OnGUI()
        {
            if (!Open) return;
            UiStyle.Use();
            GUI.depth = -50;
            float W = Screen.width, H = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            float w = 520f, h = 404f, x = (W - w) * 0.5f, y = (H - h) * 0.5f;
            UiStyle.Panel(new Rect(x, y, w, h), -0.6f);
            UiStyle.Title(new Rect(x, y + 14, w, 70), 60, "[paused]");
            float by = y + 100;
            if (UiStyle.Button(new Rect(x + 60, by, w - 120, 56), "RESUME", 26)) Close();
            by += 72;
            UiStyle.Label(new Rect(x + 60, by, 120, 30), "VOLUME", 18, TextAnchor.MiddleLeft);
            float v = GUI.HorizontalSlider(new Rect(x + 190, by + 8, w - 250, 20), AudioListener.volume, 0f, 1f);
            if (Mathf.Abs(v - AudioListener.volume) > 0.001f) { AudioListener.volume = v; PlayerPrefs.SetFloat("LastBell_Volume", v); }
            by += 46;
            if (UiStyle.Button(new Rect(x + 60, by, w - 120, 50), "DEVELOPER OPTIONS: " + (DevTools.Enabled ? "ON" : "OFF"), 18)) DevTools.Enabled = !DevTools.Enabled;
            by += 66;
            if (UiStyle.Button(new Rect(x + 60, by, w - 120, 50), "LEAVE TO MAIN MENU", 20))
            {
                Close();
                if (Net.Net.Active && Net.NetBootstrap.Instance != null) Net.NetBootstrap.Instance.Leave();
                else { CinemaScreen.I.ShowLoading(); SceneManager.LoadScene(Net.NetLobby.LobbyScene); }
            }
        }
    }
}
