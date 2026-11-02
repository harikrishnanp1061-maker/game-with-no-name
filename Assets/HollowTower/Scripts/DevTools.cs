using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// TEMPORARY dev aids that work in every level:
    ///   [F8] monsters passive - nothing sees, hunts or kills you (the giant, the Nailed, the party guests, Needlelimbs);
    ///        they keep walking their rounds. Set on the host in a session (the monsters think there).
    ///   [F6] solve the nearest in-world puzzle (bookshelf, mirrors, floor piano, clock, statues) within 15 m.
    ///   [F10] (with the puzzle director) also labels the in-world puzzles with their answers.
    ///   [F9] the task panel: warp to any puzzle, keypad, switch, key item or the end of the floor (DevTasks).
    /// </summary>
    public class DevTools : MonoBehaviour
    {
        static bool monstersPassive;
        /// <summary>DEV: monsters see and kill nobody (only while developer options are on).</summary>
        public static bool MonstersPassive { get { return monstersPassive && enabled; } set { monstersPassive = value; } }
        static bool enabled;
        /// <summary>The developer options (the game menu's switch): OFF by default, every launch.</summary>
        public static bool Enabled { get { return enabled; } set { enabled = value; if (!value) Puzzles.PuzzlePanel.ShowProof = false; } }
        static DevTools instance;
        string msg; float msgUntil;
        GUIStyle style, proof;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (instance != null) return;
            var go = new GameObject("DevTools");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevTools>();
        }

        /// <summary>A death a passive monster must not cause (PlayerParty.KillPlayer checks it).</summary>
        public static bool BlocksDeath(string how)
        {
            if (!MonstersPassive || how == null) return false;
            string h = how.ToLowerInvariant();
            return h.Contains("giant") || h.Contains("nailed") || h.Contains("party guests") || h.Contains("needle") || h.Contains("caught") || h.Contains("torn") || h.Contains("trampled") || h.Contains("crushed");
        }

        bool tasksOpen;
        Vector2 taskScroll;

        /// <summary>Open / close the task panel (the dev map's button).</summary>
        public static void ToggleTasks() { if (instance != null && Enabled) instance.SetTasks(!instance.tasksOpen); }

        void SetTasks(bool on)
        {
            if (tasksOpen == on) return;
            tasksOpen = on;
            Puzzles.PuzzlePanel.ExternalModal = on;   // the player stands still and the mouse is free while it is up
            Cursor.lockState = on ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = on;
        }

        void Update()
        {
            if (!Enabled || PlayerParty.Instance == null) { SetTasks(false); return; }
            if (InputWrap.Down(KeyCode.F9)) SetTasks(!tasksOpen);
            if (InputWrap.Down(KeyCode.F8))
            {
                MonstersPassive = !MonstersPassive;
                Say(MonstersPassive ? "[DEV] Monsters PASSIVE - they can't see or kill you (F8)" : "[DEV] Monsters active again (F8)");
            }
            if (InputWrap.Down(KeyCode.F6))
            {
                var party = PlayerParty.Instance;
                var p = party != null && party.Current != null ? party.Current.transform.position : Vector3.zero;
                var w = Puzzles.WorldPuzzle.Nearest(p, 15f);
                if (w != null) { w.DevSolve(party != null ? party.CurrentIndex : 0); Say("[DEV] Solved: " + w.title); }
                else Say("[DEV] No unsolved world puzzle within 15 m");
            }
        }

        void Say(string s) { msg = s; msgUntil = Time.time + 3f; }

        void TaskPanel()
        {
            var tasks = DevTasks.All;
            var party = PlayerParty.Instance;
            Vector3 me = party.Current != null ? party.Current.transform.position : Vector3.zero;
            float w = 360f, h = Mathf.Min(Screen.height - 130f, 90f + tasks.Count * 26f);
            var r = new Rect(12, 92, w, h);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 8, r.y + 4, w - 16, 20), "[DEV] TASKS - click to warp   (F9 closes)", style);
            var end = DevTasks.EndOfFloor(me);
            if (GUI.Button(new Rect(r.x + 8, r.y + 28, w - 16, 26), end != null ? ">> END OF THIS FLOOR: " + end.name : "(no way out found on this floor)") && end != null)
            { DevTasks.WarpTo(end); Say("[DEV] Warped to " + end.name); SetTasks(false); }
            var view = new Rect(r.x + 8, r.y + 60, w - 16, h - 66);
            taskScroll = GUI.BeginScrollView(view, taskScroll, new Rect(0, 0, w - 36, tasks.Count * 26f));
            for (int i = 0; i < tasks.Count; i++)
            {
                var k = tasks[i];
                int fl = DevTasks.FloorOf(k.Pos.y);
                string label = (k.done ? "[done] " : "") + k.name + (Net.NetLobby.TowerLevel == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ? "  (F" + (fl + 1) + ")" : "")
                               + "  " + Mathf.RoundToInt(Vector3.Distance(me, k.Pos)) + "m";
                var c = GUI.color; GUI.color = k.done ? new Color(0.6f, 1f, 0.6f) : k.exit ? new Color(1f, 0.85f, 0.4f) : Color.white;
                if (GUI.Button(new Rect(0, i * 26f, w - 36, 24), label)) { DevTasks.WarpTo(k); Say("[DEV] Warped to " + k.name); SetTasks(false); }
                GUI.color = c;
            }
            GUI.EndScrollView();
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(1f, 0.6f, 1f);
                proof = new GUIStyle(GUI.skin.box) { fontSize = 12 };
                proof.normal.textColor = new Color(0.55f, 1f, 0.65f);
            }
            if (!Enabled) return;
            if (MonstersPassive) GUI.Label(new Rect(Screen.width - 230, Screen.height - 60, 220, 24), "[DEV] MONSTERS PASSIVE (F8)", style);
            if (msg != null && Time.time < msgUntil) GUI.Label(new Rect(Screen.width * 0.5f - 220, Screen.height * 0.2f, 440, 26), msg, style);
            if (PlayerParty.Instance != null)
            {
                if (!tasksOpen) GUI.Label(new Rect(Screen.width - 230, Screen.height - 32, 220, 22), "[DEV] F9: tasks / warp", style);
                else TaskPanel();
            }
            if (!Puzzles.PuzzlePanel.ShowProof) return;
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera : Camera.main;
            if (cam == null) return;
            foreach (var w in Puzzles.WorldPuzzle.All)
            {
                if (w == null) continue;
                Vector3 s = cam.WorldToScreenPoint(w.transform.position + Vector3.up * 1.5f);
                if (s.z < 0f || s.z > 30f) continue;
                string t = (w.Solved ? "[SOLVED] " : "") + w.Describe();
                var size = proof.CalcSize(new GUIContent(t));
                GUI.Box(new Rect(s.x - size.x * 0.5f, Screen.height - s.y, size.x + 10, 22), t, proof);
            }
        }
    }
}
