using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// DEV: every task in the loaded level (puzzles, keypads, lockboxes, switches, doors, key items, the way out), found
    /// at runtime so it works in the tutorial, the tower and the campus alike. The dev maps mark them; the dev task panel
    /// (F9) warps you to any of them or to the end of the floor you are on.
    /// </summary>
    public static class DevTasks
    {
        public class Task
        {
            public string name;
            public Transform t;
            public bool done;
            public bool exit;     // the way off this floor / out of the level
            public Vector3 offset; // for roots left at the world origin: where the thing itself is
            public Vector3 Pos { get { return t != null ? t.position + offset : Vector3.zero; } }
        }

        static readonly List<Task> list = new List<Task>();
        static float builtAt = -10f;
        static string builtScene;

        static readonly Dictionary<string, string> KeyItems = new Dictionary<string, string>
        {
            { "fuse_cell", "Fuse cell" }, { "crank_handle", "Crank handle" }, { "gate_key", "Gate key" },
            { "security_keycard", "Security keycard" }, { "main_key", "Entrance key" },
        };

        /// <summary>The tasks right now (rebuilt at most twice a second).</summary>
        public static List<Task> All
        {
            get
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (Time.unscaledTime - builtAt < 0.5f && scene == builtScene) { Refresh(); return list; }
                builtAt = Time.unscaledTime; builtScene = scene;
                Build();
                return list;
            }
        }

        static void Add(Component c, string name, bool done, bool exit = false)
        {
            if (c == null || !c.gameObject.activeInHierarchy) return;
            list.Add(new Task { name = name, t = c.transform, done = done, exit = exit, offset = Anchor(c) - c.transform.position });
        }

        static void Build()
        {
            list.Clear();
            foreach (var w in Puzzles.WorldPuzzle.All) if (w != null) Add(w, "Puzzle: " + w.title, w.Solved);
            foreach (var p in Object.FindObjectsByType<Puzzles.PuzzleState>(FindObjectsSortMode.None))
                Add(p, (p is Puzzles.PuzzleBox ? "Lockbox: " : "Keypad: ") + Nice(p.name), p.Solved);
            foreach (var n in Object.FindObjectsByType<Puzzles.CodeNote>(FindObjectsSortMode.None)) Add(n, "Clue: " + Nice(n.name), false);
            foreach (var s in Object.FindObjectsByType<FuseSocket>(FindObjectsSortMode.None)) Add(s, "Fuse socket: " + Nice(s.name), s.Powered, true);
            foreach (var s in Object.FindObjectsByType<Campus.CampusSwitch>(FindObjectsSortMode.None)) Add(s, "Switch: " + Nice(s.name), s.On);
            foreach (var d in Object.FindObjectsByType<Campus.CampusKeyDoor>(FindObjectsSortMode.None)) Add(d, "Locked door: " + Nice(d.name), d.Open);
            foreach (var g in Object.FindObjectsByType<Campus.ServiceGate>(FindObjectsSortMode.None)) Add(g, "Service gate (way out)", false, true);
            foreach (var e in Object.FindObjectsByType<TowerExitElevator>(FindObjectsSortMode.None)) Add(e, "Exit elevator (way out)", false, true);
            foreach (var e in Object.FindObjectsByType<TutorialElevator>(FindObjectsSortMode.None)) Add(e, "Tutorial elevator (way out)", false, true);
            foreach (var i in Object.FindObjectsByType<PartyItem>(FindObjectsSortMode.None))
            {
                string label;
                if (i == null || !KeyItems.TryGetValue(i.itemId, out label)) continue;
                if (i.GetComponentInParent<RemasteredPlayerController>() != null) continue;   // someone is carrying it
                Add(i, "Item: " + label, false);
            }
            Refresh();
        }

        /// <summary>Where a task really is: its own position, unless it is a root at the origin with its pieces placed
        /// out in the level (the statues, a note, the service gate) - then the middle of what it draws, at floor level.</summary>
        static Vector3 Anchor(Component c)
        {
            var gate = c as Campus.ServiceGate;
            if (gate != null) return gate.Centre;
            Vector3 p = c.transform.position;
            if (new Vector2(p.x, p.z).sqrMagnitude > 1f) return p;
            Bounds b = new Bounds(); bool any = false;
            foreach (var r in c.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) foreach (var col in c.GetComponentsInChildren<Collider>(true)) { if (!any) { b = col.bounds; any = true; } else b.Encapsulate(col.bounds); }
            return any ? new Vector3(b.center.x, b.min.y, b.center.z) : p;
        }

        static void Refresh()
        {
            list.RemoveAll(k => k.t == null || !k.t.gameObject.activeInHierarchy);
            foreach (var k in list)
            {
                var w = k.t.GetComponent<Puzzles.WorldPuzzle>(); if (w != null) k.done = w.Solved;
                var p = k.t.GetComponent<Puzzles.PuzzleState>(); if (p != null) k.done = p.Solved;
                var f = k.t.GetComponent<FuseSocket>(); if (f != null) k.done = f.Powered;
                var s = k.t.GetComponent<Campus.CampusSwitch>(); if (s != null) k.done = s.On;
                var d = k.t.GetComponent<Campus.CampusKeyDoor>(); if (d != null) k.done = d.Open;
                var i = k.t.GetComponent<PartyItem>(); if (i != null && k.t.GetComponentInParent<RemasteredPlayerController>() != null) k.done = true;
            }
        }

        static string Nice(string n)
        {
            n = n.Replace('_', ' ');
            return n.Length > 34 ? n.Substring(0, 34) : n;
        }

        /// <summary>Which tower floor (0, 1) a height is on; the campus and tutorial count everything as floor 0.</summary>
        public static int FloorOf(float y) { return y > LevelBuilder.Story - 0.6f ? 1 : 0; }

        /// <summary>The way off the floor the player is standing on (the nearest exit task at that height).</summary>
        public static Task EndOfFloor(Vector3 from)
        {
            Task best = null; float bd = float.MaxValue;
            foreach (var k in All)
            {
                if (!k.exit) continue;
                float d = Mathf.Abs(k.Pos.y - from.y) * 50f + Vector3.Distance(k.Pos, from) * 0.01f;
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }

        /// <summary>Puts the current player on walkable ground beside the task, facing it.</summary>
        public static bool WarpTo(Task k)
        {
            var party = PlayerParty.Instance;
            var body = party != null ? party.Current : null;
            if (k == null || k.t == null || body == null) return false;
            Vector3 at = k.Pos, spot = at;
            bool found = false;
            // a clear, walkable spot a step or two from the task at its own height, so you don't land inside a cabinet
            for (float ring = 1.3f; ring <= 3.5f && !found; ring += 0.7f)
                for (int i = 0; i < 12 && !found; i++)
                {
                    Vector3 c = at + Quaternion.Euler(0f, i * 30f, 0f) * (k.t.forward * ring);
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(c, out hit, 0.6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - at.y) > 2.2f) continue;
                    Vector3 feet = hit.position;
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(feet + Vector3.up * 1.2f, at + Vector3.up * 0.3f, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore)
                        && block.transform != k.t && !block.transform.IsChildOf(k.t) && Vector3.Distance(block.point, at) > 0.8f) continue;   // not through a wall
                    spot = feet; found = true;
                }
            if (!found)
            {
                NavMeshHit hit;
                for (float r = 1.2f; r <= 6f && !found; r += 1.2f)
                    if (NavMesh.SamplePosition(at, out hit, r, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - at.y) < 2.2f) { spot = hit.position; found = true; }
            }
            if (!found) spot = at + k.t.forward * 1.2f;
            var cc = body.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            body.transform.position = spot + Vector3.up * 0.1f;
            if (cc != null) cc.enabled = true;
            body.FaceTowards(at);
            if (TowerFloorCulling.Instance != null) TowerFloorCulling.Instance.ForceFloorUpdate(FloorOf(spot.y));
            return true;
        }
    }
}
