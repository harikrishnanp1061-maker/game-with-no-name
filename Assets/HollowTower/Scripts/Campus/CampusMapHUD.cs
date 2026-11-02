using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// TEMPORARY dev aid for the final level, like the tower's map: a blueprint of the campus drawn at load from the
    /// level's own geometry (roads, paths, lawns, hedges, buildings, the admin building's rooms), the whole objective
    /// route marked in stages (the current one bright), every objective marked, the keypad's code, all four players.
    /// [M] full map, [H] hide, click the map to warp there (Shift-click: the admin building's upper floor), [1]-[6]
    /// warp to the objectives, [O] open the main entrance doors, [P] open the head office, [F7] revive teammates beside you.
    /// </summary>
    public class CampusMapHUD : MonoBehaviour
    {
        // the campus in world x/z, and the blueprint's resolution
        static readonly Rect World = Rect.MinMaxRect(-182f, -58f, 178f, 274f);
        const int TexW = 1080;


        Texture2D blueprint, white;
        /// <summary>The campus blueprint drawn at load (the control room's map uses it too), and the world x/z it covers.</summary>
        public static Texture2D Blueprint { get; private set; }
        public static Rect WorldRect { get { return World; } }
        bool hide, full;
        GUIStyle head, small, badge, btn;
        string feedback; float feedbackUntil;

        // ------------------------------------------------------------------ the route, stage by stage (world x, z)
        struct Leg { public CampusObjective.Stage stage; public string name; public Color color; public Vector2[] pts; }

        static readonly Leg[] Route =
        {
            new Leg { stage = CampusObjective.Stage.FindKey, name = "1  key (upstairs, head's office)", color = new Color(1f, 0.55f, 0.1f), pts = new[] {
                new Vector2(0f, -22.4f), new Vector2(0f, -10f), new Vector2(9.2f, -4f), new Vector2(9.2f, 17f),   // to the foot of the stairs
                new Vector2(9.2f, 4f), new Vector2(9.2f, -10.5f), new Vector2(12.5f, -10.5f), new Vector2(12.5f, 16.8f),   // up, round the gallery
                new Vector2(18f, 16.8f), new Vector2(18f, 12.8f), new Vector2(24f, 12.8f), new Vector2(31.2f, 17.4f) } },
            new Leg { stage = CampusObjective.Stage.UnlockDoor, name = "2  main entrance doors", color = new Color(1f, 0.92f, 0.2f), pts = new[] {
                new Vector2(31.2f, 17.4f), new Vector2(24f, 12.8f), new Vector2(18f, 12.8f), new Vector2(18f, 16.8f), new Vector2(12.5f, 16.8f),
                new Vector2(12.5f, -10.5f), new Vector2(9.2f, -10.5f), new Vector2(9.2f, 4f), new Vector2(9.2f, 17f), new Vector2(0f, 20f), new Vector2(0f, 24f) } },
            new Leg { stage = CampusObjective.Stage.RestorePower, name = "3  generator (maintenance yard, SW)", color = new Color(0.3f, 1f, 0.4f), pts = new[] {
                new Vector2(0f, 24f), new Vector2(0f, 30f), new Vector2(-104f, 30f), new Vector2(-138f, 30f), new Vector2(-138f, -10f),
                new Vector2(-138f, -20f), new Vector2(-151f, -26f), new Vector2(-155.4f, -31f) } },
            new Leg { stage = CampusObjective.Stage.OpenGate, name = "4  gate lever (security booth, N)", color = new Color(0.25f, 0.85f, 1f), pts = new[] {
                new Vector2(-155.4f, -31f), new Vector2(-151f, -26f), new Vector2(-138f, -20f), new Vector2(-138f, 30f), new Vector2(-108f, 30f),
                new Vector2(-108f, 148f), new Vector2(0f, 148f), new Vector2(0f, 229.8f), new Vector2(15.6f, 229.8f), new Vector2(16.8f, 232.4f) } },
            new Leg { stage = CampusObjective.Stage.FindServiceGate, name = "5  main gate welded: service gate (SE)", color = new Color(1f, 0.45f, 0.3f), pts = new[] {
                new Vector2(16.8f, 232.4f), new Vector2(15.6f, 229.8f), new Vector2(11f, 215f), new Vector2(11f, 152f), new Vector2(114f, 148f),
                new Vector2(114f, -45f), new Vector2(152f, -46f), new Vector2(152f, -50f) } },
            new Leg { stage = CampusObjective.Stage.Escape, name = "6  through the service gate", color = new Color(1f, 0.3f, 0.85f), pts = new[] {
                new Vector2(152f, -50f), new Vector2(152f, -62f) } },
        };

        struct Spot { public string label; public Vector3 pos; public float yaw; public Color color; }
        static readonly Spot[] Warps =
        {
            new Spot { label = "Spawn lift", pos = new Vector3(0f, 0.2f, -19f), yaw = 0f, color = new Color(0.2f, 0.95f, 0.45f) },
            new Spot { label = "Head's office", pos = new Vector3(28f, 4.4f, 15f), yaw = 60f, color = new Color(1f, 0.55f, 0.1f) },
            new Spot { label = "Main doors", pos = new Vector3(0f, 0.2f, 20f), yaw = 0f, color = new Color(1f, 0.92f, 0.2f) },
            new Spot { label = "Generator", pos = new Vector3(-152f, 0.2f, -28f), yaw = -130f, color = new Color(0.3f, 1f, 0.4f) },
            new Spot { label = "Security booth", pos = new Vector3(13f, 0.2f, 229.8f), yaw = 90f, color = new Color(0.25f, 0.85f, 1f) },
            new Spot { label = "Main gate", pos = new Vector3(0f, 0.2f, 232f), yaw = 0f, color = new Color(1f, 0.3f, 0.85f) },
            new Spot { label = "Control room", pos = new Vector3(-28.6f, 4.4f, -16.3f), yaw = 90f, color = new Color(0.5f, 1f, 0.7f) },
            new Spot { label = "Service gate", pos = new Vector3(150f, 0.2f, -44f), yaw = 180f, color = new Color(1f, 0.45f, 0.3f) },
        };

        // ------------------------------------------------------------------ blueprint
        void Start()
        {
            white = Texture2D.whiteTexture;
            BuildBlueprint();
        }

        int TexH { get { return Mathf.RoundToInt(TexW * World.height / World.width); } }

        void BuildBlueprint()
        {
            int w = TexW, h = TexH;
            var px = new Color32[w * h];
            var bg = new Color32(16, 26, 22, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            var env = GameObject.Find("Campus_Final_Environment");
            if (env != null)
            {
                // draw in layers: ground surfaces, then hedges and water, then buildings, then the admin building's walls
                var layers = new List<KeyValuePair<int, KeyValuePair<Bounds, Color32>>>();
                foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int layer; Color32 c;
                    if (!Classify(r, out layer, out c)) continue;
                    layers.Add(new KeyValuePair<int, KeyValuePair<Bounds, Color32>>(layer, new KeyValuePair<Bounds, Color32>(r.bounds, c)));
                }
                layers.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var l in layers) Fill(px, w, h, l.Value.Key, l.Value.Value);
            }
            blueprint = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            blueprint.SetPixels32(px);
            blueprint.Apply();
            Blueprint = blueprint;
        }

        static bool Classify(MeshRenderer r, out int layer, out Color32 c)
        {
            layer = 0; c = default(Color32);
            string n = r.name;
            var p = r.transform.parent != null ? r.transform.parent.name : "";
            var b = r.bounds;
            if (n.StartsWith("Ground_Grass") || n == "Mud") return false;
            if (n.StartsWith("Road") || n == "Avenue" || n.StartsWith("CarPark") || n == "Gate_Plaza" || n == "Yard" || n.StartsWith("Yard_")) { layer = 1; c = new Color32(52, 56, 60, 255); return true; }
            if (n.StartsWith("Path") || n.StartsWith("Quad_") || n == "Plaza" || n.StartsWith("Garden_") || n.StartsWith("Cafe_") || n.Contains("Forecourt") || n.StartsWith("Library_Reading") || n == "Arch_Passage" || n.StartsWith("Avenue_Pavement") || n == "BusStop_Pad")
            { layer = 2; c = new Color32(118, 108, 92, 255); return true; }
            if (n == "Road_Dash" || n == "Stall_Line" || n == "Field_Line" || n == "Field_Circle") { layer = 3; c = new Color32(150, 150, 140, 255); return true; }
            if (n == "Hedge") { layer = 4; c = new Color32(40, 92, 46, 255); return true; }
            if (n.StartsWith("Pond_Water") || n == "Basin_Water") { layer = 4; c = new Color32(40, 70, 110, 255); return true; }
            if (n == "Shell" || n == "Tower" || n.StartsWith("Booth_Roof") || n == "Arch_Bridge" || n == "Bleacher" || n == "Wall" || n == "Gate_Pillar")
            { layer = 5; c = new Color32(70, 82, 112, 255); return true; }
            if (p == "Shell" && (n.EndsWith("_Floor") && b.max.y < 0.5f)) { layer = 5; c = new Color32(96, 104, 120, 255); return true; }   // the admin ground floor
            if ((p == "Shell" || p == "Rooms" || p == "Arrival_Elevator") && (n.StartsWith("Wall") || n.StartsWith("Elevator_Recess")) && b.min.y < 1f)
            { layer = 6; c = new Color32(20, 22, 30, 255); return true; }
            if (n == "Step") { layer = 6; c = new Color32(170, 160, 140, 255); return true; }
            return false;
        }

        static void Fill(Color32[] px, int w, int h, Bounds b, Color32 c)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - World.xMin) / World.width * w), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x - World.xMin) / World.width * w), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - World.yMin) / World.height * h), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z - World.yMin) / World.height * h), 0, h - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) px[y * w + x] = c;
        }

        // ------------------------------------------------------------------ input
        void Update()
        {
            if (!DevTools.Enabled) { if (RemasteredMapHUD.IsFullMapOpen) RemasteredMapHUD.IsFullMapOpen = false; return; }
            if (Puzzles.PuzzlePanel.IsOpen) return;
            if (InputWrap.Down(KeyCode.H)) hide = !hide;
            if (InputWrap.Down(KeyCode.M)) SetFull(!full);
            for (int i = 0; i < Warps.Length; i++)
                if (InputWrap.Down(KeyCode.Alpha1 + i)) WarpTo(Warps[i].pos, Warps[i].yaw, Warps[i].label);
            if (InputWrap.Down(KeyCode.O)) DevOpen(CampusObjective.Instance != null ? CampusObjective.Instance.mainDoor : null, "main entrance doors");
            if (InputWrap.Down(KeyCode.P)) DevOpen(HeadDoor(), "head office door");
            var giant = CampusGiant.Instance;
            if (giant != null && InputWrap.Down(KeyCode.J)) { giant.DevTogglePause(); feedback = giant.Paused ? "[DEV] Giant frozen - it sees nobody" : "[DEV] Giant awake"; feedbackUntil = Time.time + 2f; }
            if (giant != null && InputWrap.Down(KeyCode.U) && PlayerParty.Instance != null) { giant.DevEnrage(PlayerParty.Instance.CurrentIndex); feedback = "[DEV] Giant enraged at you"; feedbackUntil = Time.time + 2f; }
        }

        static CampusKeyDoor HeadDoor()
        {
            var g = GameObject.Find("Head_Office_Door");
            return g != null ? g.GetComponent<CampusKeyDoor>() : null;
        }

        /// <summary>DEV: opens a locked door as if its key or code were used, the same on every machine.</summary>
        void DevOpen(CampusKeyDoor door, string what)
        {
            if (door == null) return;
            if (door.Open) { feedback = "The " + what + " are already open"; feedbackUntil = Time.time + 2f; return; }
            var party = PlayerParty.Instance;
            Net.NetWorld.Act(door, 1, party != null ? party.CurrentIndex : 0);
            feedback = "[DEV] Opened the " + what; feedbackUntil = Time.time + 2f;
        }

        // the players' controllers free the mouse and stop walking while the (shared) full-map flag is up
        void SetFull(bool on) { full = on; RemasteredMapHUD.IsFullMapOpen = on; }

        void OnDisable() { if (full) RemasteredMapHUD.IsFullMapOpen = false; }

        void WarpTo(Vector3 pos, float yaw, string label)
        {
            var party = PlayerParty.Instance;
            var body = party != null ? party.Current : null;
            if (body == null) return;
            var cc = body.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            body.transform.position = pos;
            body.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = true;
            body.FaceTowards(pos + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            feedback = "Warped: " + label; feedbackUntil = Time.time + 2f;
        }

        void WarpToMap(Vector2 world, bool upper)
        {
            // inside the admin building: its ground floor (or the upper one with Shift); elsewhere the first thing below the sky
            bool inAdmin = world.x > -36f && world.x < 36f && world.y > -24f && world.y < 24f;
            float from = inAdmin ? (upper ? 7.6f : 3.2f) : 60f;
            Vector3 pos = new Vector3(world.x, 0.2f, world.y);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(world.x, from, world.y), Vector3.down, out hit, from + 5f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y + 0.1f;
            var body = PlayerParty.Instance != null ? PlayerParty.Instance.Current : null;
            WarpTo(pos, body != null ? body.transform.eulerAngles.y : 0f, "map (" + world.x.ToString("F0") + ", " + world.y.ToString("F0") + ")" + (inAdmin && upper ? " upper floor" : ""));
        }

        // ------------------------------------------------------------------ drawing
        void Styles()
        {
            if (head != null) return;
            head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold }; head.normal.textColor = new Color(0.4f, 1f, 0.75f);
            small = new GUIStyle(GUI.skin.label) { fontSize = 11 }; small.normal.textColor = new Color(0.8f, 0.85f, 0.85f);
            badge = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 11 };
        }

        void OnGUI()
        {
            if (!DevTools.Enabled) return;
            Styles();
            if (hide) { GUI.Label(new Rect(Screen.width - 170, Screen.height - 24, 160, 20), "[H] Show map & dev HUD", small); return; }
            ControlBar();
            if (full) FullMap(); else Minimap();
            if (Time.time < feedbackUntil)
            {
                var r = new Rect((Screen.width - 340f) * 0.5f, Screen.height - 52f, 340f, 28f);
                GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(r, white); GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 10, r.y + 5, r.width - 20, 20), feedback, head);
            }
        }

        void ControlBar()
        {
            // under the objective banner, three rows: doors and tools, warps, the giant
            var area = new Rect(12, 50, 500, 92);
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(area, white); GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(area.x + 6, area.y + 5, area.width - 12, area.height - 8));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(full ? "[M] Close" : "[M] Map", btn, GUILayout.Width(70))) SetFull(!full);
            if (GUILayout.Button("[H] Hide", btn, GUILayout.Width(62))) hide = true;
            if (GUILayout.Button("[F7] Revive", btn, GUILayout.Width(82)) && PlayerParty.Instance != null) PlayerParty.Instance.ReviveTeammatesHere();
            var md = CampusObjective.Instance != null ? CampusObjective.Instance.mainDoor : null;
            GUI.enabled = md != null && !md.Open;
            if (GUILayout.Button("[O] Main doors", btn, GUILayout.Width(110))) DevOpen(md, "main entrance doors");
            var hd = HeadDoor();
            GUI.enabled = hd != null && !hd.Open;
            if (GUILayout.Button("[P] Head office", btn, GUILayout.Width(110))) DevOpen(hd, "head office door");
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Warp:", small, GUILayout.Width(38));
            for (int i = 0; i < Warps.Length; i++)
                if (GUILayout.Button("[" + (i + 1) + "] " + ShortName(i), btn, GUILayout.Width(57))) WarpTo(Warps[i].pos, Warps[i].yaw, Warps[i].label);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var gi = CampusGiant.Instance;
            if (gi != null)
            {
                if (GUILayout.Button(gi.Paused ? "[J] Giant: OFF" : "[J] Giant: on", btn, GUILayout.Width(104))) gi.DevTogglePause();
                if (GUILayout.Button("[U] Enrage it", btn, GUILayout.Width(96)) && PlayerParty.Instance != null) gi.DevEnrage(PlayerParty.Instance.CurrentIndex);
                GUILayout.Label("giant: " + gi.Current.ToString().ToLower(), small, GUILayout.Width(96));
            }
            var obj = CampusObjective.Instance;
            GUILayout.Label(obj != null ? "stage: " + obj.Current : "", small);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string ShortName(int i) { return new[] { "Spawn", "Key", "Doors", "Generator", "Booth", "Gate", "Control", "Service" }[i]; }

        void Minimap()
        {
            float w = 300f, h = w * TexH / TexW;
            var r = new Rect(Screen.width - w - 12, 64, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(r.x - 6, r.y - 24, w + 12, h + 44), white); GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y - 22, w, 18), "LEVEL 3 - CAMPUS (dev map)", head);
            Map(r, false);
            GUI.Label(new Rect(r.x, r.yMax + 2, w, 16), "Click to warp (Shift: admin upper floor) | [M] full map", small);
        }

        void FullMap()
        {
            float h = Mathf.Min(Screen.height * 0.74f, 760f), w = h * TexW / TexH;
            if (w > Screen.width * 0.7f) { w = Screen.width * 0.7f; h = w * TexH / TexW; }
            var r = new Rect((Screen.width - w) * 0.5f - 110f, Mathf.Max(96f, (Screen.height - h) * 0.5f + 30f), w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.82f); GUI.DrawTexture(new Rect(r.x - 12, r.y - 34, w + 250, h + 46), white); GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y - 30, w, 20), "LEVEL 3 - CAMPUS BLUEPRINT  (click to warp, Shift-click: admin upper floor)", head);
            Map(r, true);
            // the legend: each leg of the route, and the keys
            float lx = r.xMax + 14, ly = r.y;
            GUI.Label(new Rect(lx, ly, 230, 18), "ROUTE (current stage bright)", head); ly += 22;
            var obj = CampusObjective.Instance;
            foreach (var leg in Route)
            {
                bool now = obj != null && obj.Current == leg.stage;
                GUI.color = leg.color; GUI.DrawTexture(new Rect(lx, ly + 6, 18, now ? 4 : 2), white); GUI.color = Color.white;
                var st = new GUIStyle(small); st.normal.textColor = now ? leg.color : new Color(0.75f, 0.78f, 0.78f); if (now) st.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(lx + 24, ly, 210, 18), leg.name, st); ly += 20;
            }
            ly += 10;
            GUI.Label(new Rect(lx, ly, 230, 18), "DEV KEYS", head); ly += 22;
            GUI.Label(new Rect(lx, ly, 240, 18), "KEY: head administrator's desk,", small); ly += 16;
            GUI.Label(new Rect(lx, ly, 240, 18), "upper floor, east wing (keypad door)", small); ly += 16;
            GUI.Label(new Rect(lx, ly, 240, 18), "glows only in P4's torch beam", small); ly += 24;
            foreach (var line in new[] { "[J] giant on/off  [U] enrage it at you", "[O] open main doors  [P] head office", "[1]-[8] warp (7 control room, 8 service gate)", "[F7] revive teammates here", "[F1]-[F4] switch player", "[M] map  [H] hide", "Keypad panel: [DEV] Skip", "Head office code: " + KeypadCode(), "Control room code: " + KeypadCode("Control_Room_Door") })
            { GUI.Label(new Rect(lx, ly, 230, 18), line, small); ly += 18; }
        }

        string KeypadCode(string doorName = "Head_Office_Door")
        {
            var door = GameObject.Find(doorName);
            var lockp = door != null ? door.GetComponent<Puzzles.PuzzleLock>() : null;
            if (lockp == null) return "-";
            if (lockp.Solved) return "solved";
            if (lockp.Seed == 0) return "rolled when first used";
            var c = lockp.Code.code;
            return c[0] + "" + c[1] + "" + c[2];
        }

        void Map(Rect r, bool labels)
        {
            if (blueprint != null) GUI.DrawTexture(r, blueprint, ScaleMode.StretchToFill);
            var obj = CampusObjective.Instance;
            foreach (var leg in Route)
            {
                bool now = obj != null && obj.Current == leg.stage;
                bool done = obj != null && obj.Current > leg.stage;
                var c = leg.color; c.a = now ? 1f : done ? 0.25f : 0.6f;
                Polyline(r, leg.pts, c, now ? 3f : 2f);
            }
            for (int i = 0; i < Warps.Length; i++)
                Badge(r, new Vector2(Warps[i].pos.x, Warps[i].pos.z), labels && i != 1 ? "[" + (i + 1) + "] " + Warps[i].label : "", Warps[i].color);   // [2] sits under the KEY marker
            if (labels)
            {
                Badge(r, new Vector2(24f, 12.8f), "", new Color(1f, 0.4f, 0.3f));
                Vector2 kp = MapPoint(r, new Vector2(24f, 12.8f));
                var kst = new GUIStyle(badge) { alignment = TextAnchor.MiddleLeft }; kst.normal.textColor = new Color(1f, 0.4f, 0.3f);
                if (Event.current.type == EventType.Repaint) GUI.Label(new Rect(kp.x + 34f, kp.y + 6f, 300, 16), "<- keypad door, code " + KeypadCode(), kst);
                Badge(r, new Vector2(0f, 252f), "ESCAPE", new Color(1f, 0.3f, 0.85f));
                foreach (var t in new[] { new KeyValuePair<string, Vector2>("ADMIN", new Vector2(-24f, 0f)), new KeyValuePair<string, Vector2>("QUAD", new Vector2(-30f, 100f)),
                                          new KeyValuePair<string, Vector2>("LIBRARY", new Vector2(-79f, 79f)), new KeyValuePair<string, Vector2>("LECTURE HALLS", new Vector2(81f, 81f)),
                                          new KeyValuePair<string, Vector2>("CLOCK TOWER", new Vector2(0f, 136f)), new KeyValuePair<string, Vector2>("CAR PARK", new Vector2(67f, 192f)),
                                          new KeyValuePair<string, Vector2>("SPORTS FIELD", new Vector2(-90f, 194f)), new KeyValuePair<string, Vector2>("GARDENS", new Vector2(-150f, 100f)),
                                          new KeyValuePair<string, Vector2>("YARD", new Vector2(-157f, -42f)), new KeyValuePair<string, Vector2>("CAFETERIA", new Vector2(70f, -30f)),
                                          new KeyValuePair<string, Vector2>("STUDENT CENTRE", new Vector2(148f, 0f)) })
                {
                    Vector2 p = MapPoint(r, t.Value);
                    var st = new GUIStyle(badge); st.normal.textColor = new Color(0.85f, 0.9f, 1f, 0.75f);
                    GUI.Label(new Rect(p.x - 60, p.y - 8, 120, 16), t.Key, st);
                }
            }
            foreach (var k in DevTasks.All)
            {
                // DEV: every task - puzzles purple, done ones green, the way out gold
                Color c = k.done ? new Color(0.35f, 1f, 0.45f) : k.exit ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 0.35f, 1f);
                string n = k.name.Contains(":") ? k.name.Substring(k.name.IndexOf(':') + 1).Trim() : k.name;
                Badge(r, new Vector2(k.Pos.x, k.Pos.z), labels ? (k.done ? "done: " : "") + n : "", c);
            }
            KeyMarker(r, labels);
            GiantMarker(r, labels);
            Players(r);
            // click to warp
            Event e = Event.current;
            if (r.Contains(e.mousePosition))
            {
                GUI.color = Color.yellow; GUI.DrawTexture(new Rect(e.mousePosition.x - 3, e.mousePosition.y - 3, 6, 6), white); GUI.color = Color.white;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    float wx = World.xMin + (e.mousePosition.x - r.x) / r.width * World.width;
                    float wz = World.yMin + (1f - (e.mousePosition.y - r.y) / r.height) * World.height;
                    WarpToMap(new Vector2(wx, wz), e.shift);
                    e.Use();
                }
            }
        }

        /// <summary>Where the main entrance key is right now: on the head administrator's desk, or with whoever carries it.</summary>
        void KeyMarker(Rect r, bool labels)
        {
            if (Event.current.type != EventType.Repaint) return;
            var obj = CampusObjective.Instance;
            var key = obj != null ? obj.key : null;
            if (key == null) return;
            Vector3 at = key.transform.position; string where = "KEY - head's desk (upstairs)";
            var party = PlayerParty.Instance;
            if (key.Holder != null)
            {
                at = key.Holder.transform.position;
                int i = party != null ? System.Array.IndexOf(party.players, key.Holder.GetComponent<RemasteredPlayerController>()) : -1;
                where = "KEY - carried by P" + (i + 1);
            }
            else if (key.EverTaken) where = "KEY - dropped here";
            Vector2 p = MapPoint(r, new Vector2(at.x, at.z));
            var gold = new Color(1f, 0.82f, 0.25f);
            // a pulsing ring round a solid diamond, so it can't be missed
            float pulse = 6f + Mathf.PingPong(Time.unscaledTime * 10f, 6f);
            GUI.color = new Color(gold.r, gold.g, gold.b, 0.45f);
            GUI.DrawTexture(new Rect(p.x - pulse, p.y - 1f, pulse * 2f, 2f), white);
            GUI.DrawTexture(new Rect(p.x - 1f, p.y - pulse, 2f, pulse * 2f), white);
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, p);
            GUI.color = Color.black; GUI.DrawTexture(new Rect(p.x - 6f, p.y - 6f, 12f, 12f), white);
            GUI.color = gold; GUI.DrawTexture(new Rect(p.x - 4.5f, p.y - 4.5f, 9f, 9f), white);
            GUI.matrix = saved;
            GUI.color = Color.white;
            var st = new GUIStyle(badge) { fontSize = labels ? 12 : 10 }; st.normal.textColor = gold;
            GUI.Label(new Rect(p.x - 110, p.y - 22, 220, 16), labels ? where : "KEY", st);
        }

        /// <summary>The giant: where it stands, which way it is looking, and what it is doing.</summary>
        void GiantMarker(Rect r, bool labels)
        {
            var g = CampusGiant.Instance;
            if (g == null || Event.current.type != EventType.Repaint) return;
            Vector2 p = MapPoint(r, new Vector2(g.transform.position.x, g.transform.position.z));
            var red = g.Current == CampusGiant.Mode.Roam ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.1f, 0.05f);
            Vector3 gd = g.GazeDirection; gd.y = 0f;
            if (gd.sqrMagnitude > 1e-4f)
            {
                gd.Normalize();
                Vector2 end = MapPoint(r, new Vector2(g.transform.position.x + gd.x * 90f, g.transform.position.z + gd.z * 90f));
                Polyline(r, new[] { new Vector2(g.transform.position.x, g.transform.position.z), new Vector2(g.transform.position.x + gd.x * 90f, g.transform.position.z + gd.z * 90f) }, new Color(red.r, red.g, red.b, 0.6f), 2f);
            }
            GUI.color = Color.black; GUI.DrawTexture(new Rect(p.x - 7, p.y - 7, 14, 14), white);
            GUI.color = red; GUI.DrawTexture(new Rect(p.x - 5.5f, p.y - 5.5f, 11, 11), white);
            GUI.color = Color.white;
            var st = new GUIStyle(badge) { fontSize = labels ? 11 : 9 }; st.normal.textColor = red;
            GUI.Label(new Rect(p.x - 90, p.y + 7, 180, 16), labels ? "GIANT - " + (g.Paused ? "frozen" : g.Current.ToString().ToLower()) : "GIANT", st);
        }

        void Players(Rect r)
        {
            var party = PlayerParty.Instance;
            if (party == null) return;
            for (int i = 0; i < party.players.Length; i++)
            {
                var p = party.players[i];
                if (p == null || PlayerParty.SitsOut(i)) continue;
                Vector2 m = MapPoint(r, new Vector2(p.transform.position.x, p.transform.position.z));
                bool me = p == party.Current;
                var c = p.IsDead ? new Color(0.5f, 0.5f, 0.5f) : me ? Color.white : new Color(0.4f, 0.8f, 1f);
                float s = me ? 9f : 7f;
                var saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(p.transform.eulerAngles.y + 45f, m);
                GUI.color = c; GUI.DrawTexture(new Rect(m.x - s * 0.5f, m.y - s * 0.5f, s, s), white);
                GUI.matrix = saved;
                var st = new GUIStyle(badge); st.normal.textColor = c;
                GUI.Label(new Rect(m.x - 20, m.y - 18, 40, 14), "P" + (i + 1), st);
                GUI.color = Color.white;
            }
        }

        Vector2 MapPoint(Rect r, Vector2 world)
        {
            return new Vector2(r.x + (world.x - World.xMin) / World.width * r.width, r.y + (1f - (world.y - World.yMin) / World.height) * r.height);
        }

        void Badge(Rect r, Vector2 world, string label, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 p = MapPoint(r, world);
            GUI.color = c; GUI.DrawTexture(new Rect(p.x - 4, p.y - 4, 8, 8), white); GUI.color = Color.white;
            if (string.IsNullOrEmpty(label)) return;
            var st = new GUIStyle(badge); st.normal.textColor = c;
            GUI.Label(new Rect(p.x - 70, p.y + 4, 140, 16), label, st);
        }

        void Polyline(Rect r, Vector2[] pts, Color c, float thick)
        {
            if (Event.current.type != EventType.Repaint) return;
            var saved = GUI.matrix;
            GUI.color = c;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = MapPoint(r, pts[i]), b = MapPoint(r, pts[i + 1]);
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(ang, a);
                GUI.DrawTexture(new Rect(a.x, a.y - thick * 0.5f, Vector2.Distance(a, b), thick), white);
                GUI.matrix = saved;
            }
            GUI.color = Color.white;
        }
    }
}
