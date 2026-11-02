using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// Puts the puzzles into the tower when a scene loads: a lockbox over every fuse cell lying in the tower (a 4x4
    /// wiring panel on floor 1, 5x5 on the party floor; the tutorial's cell is left alone), and a keypad on the stairwell lock
    /// and the exit elevator, so those need the fuse cells and the code. Everything is made in the same order with the
    /// same names on every machine, so NetWorld finds the same box everywhere. With F10 on, each puzzle shows its seed
    /// and the solver's verdict above it in the world.
    /// </summary>
    public class PuzzleDirector : MonoBehaviour
    {
        public static int Floor1Size = 4, Floor1MinMoves = 4;   // wiring panel size, route tiles scrambled
        public static int Floor2Size = 5, Floor2MinMoves = 6;

        readonly List<PuzzleState> all = new List<PuzzleState>();
        GUIStyle label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Spawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Spawn();
        }

        static void Spawn()
        {
            if (FindAnyObjectByType<PuzzleDirector>() != null) return;
            if (FindAnyObjectByType<FuseSocket>(FindObjectsInactive.Include) == null && FindAnyObjectByType<TowerExitElevator>(FindObjectsInactive.Include) == null) return;   // not the tower
            new GameObject("PuzzleDirector").AddComponent<PuzzleDirector>();
        }

        void Start()
        {
            var cells = new List<PartyItem>();
            foreach (var it in FindObjectsByType<PartyItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (it.itemId == "fuse_cell" && it.name.StartsWith("FuseCell") && !it.EverTaken) cells.Add(it);
            cells.Sort((a, b) => string.CompareOrdinal(a.name, b.name) != 0 ? string.CompareOrdinal(a.name, b.name) : a.transform.position.x.CompareTo(b.transform.position.x));

            var root = new GameObject("PuzzleBoxes").transform;   // a root found by name alone (NetWorld)
            // one puzzle of each kind in the whole game: only floor 1's cell sits in a wiring lockbox (floor 2's are behind
            // the library bookcase and in the darkroom's light-trap safe)
            cells.RemoveAll(c => c.transform.position.y > LevelBuilder.Story * 0.5f);
            for (int i = 0; i < cells.Count; i++)
            {
                bool upstairs = false;
                all.Add(PuzzleBox.Build(cells[i], root, "Lockbox_" + i + "_" + cells[i].name,
                    upstairs ? Floor2Size : Floor1Size, upstairs ? Floor2MinMoves : Floor1MinMoves));
            }
            foreach (var s in FindObjectsByType<FuseSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                all.Add(Keypad(s.gameObject, "Stairwell keypad"));
            Debug.Log("[Puzzles] " + cells.Count + " lockboxes, " + (all.Count - cells.Count) + " keypads");
        }

        static PuzzleLock Keypad(GameObject on, string title)
        {
            var k = on.GetComponent<PuzzleLock>();
            if (k == null) { k = on.AddComponent<PuzzleLock>(); k.title = title; }
            return k;
        }

        void Update() { if (DevTools.Enabled && InputWrap.Down(KeyCode.F10)) PuzzlePanel.ShowProof = !PuzzlePanel.ShowProof; }
        // (world puzzles' proof lines and the F9 dev solve live in DevTools)

        void OnGUI()
        {
            if (!PuzzlePanel.ShowProof) return;
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera : Camera.main;
            if (cam == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.box) { fontSize = 12, wordWrap = false, alignment = TextAnchor.MiddleCenter };
                label.normal.textColor = new Color(0.55f, 1f, 0.65f);
            }
            foreach (var p in all)
            {
                if (p == null) continue;
                Vector3 at = p.transform.position + Vector3.up * 1.1f;
                Vector3 s = cam.WorldToScreenPoint(at);
                if (s.z < 0f || s.z > 25f) continue;
                string text = (p.Solved ? "[SOLVED] " : "") + p.Describe();
                var size = label.CalcSize(new GUIContent(text));
                GUI.Box(new Rect(s.x - size.x * 0.5f - 6f, Screen.height - s.y - 12f, size.x + 12f, 24f), text, label);
            }
        }
    }
}
