using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Tower map. A minimap of your current floor sits in the top-right corner;
    /// M or Tab opens the full map with all four floors, fuses, lockers and stairs.
    /// Enemies are never shown - you still have to watch and listen.
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        bool fullOpen;
        Texture2D[] floorTex;
        Texture2D white, arrow;
        bool builtWithElevator;
        GUIStyle label, title, legend, roomLabel;

        static readonly Color WallCol = new Color(0.55f, 0.58f, 0.62f, 0.95f);
        static readonly Color FloorCol = new Color(0.10f, 0.11f, 0.13f, 0.85f);
        static readonly Color StairCol = new Color(0.85f, 0.55f, 0.15f, 0.95f);
        static readonly Color ShaftCol = new Color(0.03f, 0.03f, 0.04f, 0.9f);
        static readonly Color ElevatorCol = new Color(0.3f, 0.9f, 0.5f, 0.95f);
        static readonly Color FuseCol = new Color(1f, 0.85f, 0.2f);
        static readonly Color LockerCol = new Color(0.35f, 0.65f, 1f);
        static readonly Color PlayerCol = new Color(1f, 0.25f, 0.2f);
        static readonly Color AmmoCol = new Color(0.45f, 0.85f, 0.3f);
        static readonly Color MedCol = new Color(1f, 1f, 1f);
        static readonly Color GenOnCol = new Color(0.2f, 0.75f, 0.65f);
        static readonly Color GenOffCol = new Color(1f, 0.2f, 0.12f);

        void Start()
        {
            white = Texture2D.whiteTexture;
            arrow = MakeArrow(32);
            BuildFloors(false);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || gm.IsPaused) { fullOpen = false; return; }
            if (InputWrap.Down(KeyCode.M) || InputWrap.Down(KeyCode.Tab)) fullOpen = !fullOpen;

            bool elevatorOpen = gm.Level != null && gm.Level.elevator != null && gm.Level.elevator.IsOpen;
            if (elevatorOpen != builtWithElevator) BuildFloors(elevatorOpen);
        }

        // ---------------- textures ----------------

        const int Px = 8;   // texture pixels per 3 m cell

        void BuildFloors(bool elevatorOpen)
        {
            builtWithElevator = elevatorOpen;
            if (floorTex == null) floorTex = new Texture2D[LevelBuilder.FloorCount];
            int W = LevelBuilder.Cols * Px, H = LevelBuilder.Rows * Px;
            var px = new Color[W * H];
            for (int f = 0; f < LevelBuilder.FloorCount; f++)
            {
                if (floorTex[f] == null)
                {
                    floorTex[f] = new Texture2D(W, H, TextureFormat.RGBA32, false);
                    floorTex[f].filterMode = FilterMode.Point;
                    floorTex[f].wrapMode = TextureWrapMode.Clamp;
                }
                var plan = LevelBuilder.Plan(f);
                for (int i = 0; i < px.Length; i++) px[i] = SolidCol;
                // Floor fill per cell.
                for (int r = 0; r < LevelBuilder.Rows; r++)
                    for (int c = 0; c < LevelBuilder.Cols; c++)
                    {
                        var room = plan.RoomAt(c, r);
                        Color col = room == null ? SolidCol : FillFor(room, elevatorOpen);
                        if (plan.shaft[r, c]) col = ShaftCol;
                        else if (plan.ramp[r, c]) col = StairCol;
                        FillCell(px, W, c, r, col);
                    }
                // Walls on cell edges wherever two different rooms meet without a door.
                for (int r = 0; r < LevelBuilder.Rows; r++)
                    for (int c = 0; c < LevelBuilder.Cols; c++)
                    {
                        int z = plan.zone[r, c];
                        int ze = c + 1 < LevelBuilder.Cols ? plan.zone[r, c + 1] : -2;
                        int zs = r + 1 < LevelBuilder.Rows ? plan.zone[r + 1, c] : -2;
                        if (z != ze && (z >= 0 || ze >= 0))
                        {
                            int key = c + 1 < LevelBuilder.Cols ? LevelLayout.EdgeE(r, c) : -1;
                            bool fake = key == plan.fakeWallEdge && key >= 0;
                            bool door = key >= 0 && plan.doors.Contains(key);
                            if (fake) VLine(px, W, (c + 1) * Px, r, elevatorOpen ? ElevatorCol : WallCol, false);
                            else VLine(px, W, (c + 1) * Px, r, WallCol, door);
                        }
                        if (z != zs && (z >= 0 || zs >= 0))
                        {
                            int key = r + 1 < LevelBuilder.Rows ? LevelLayout.EdgeS(r, c) : -1;
                            HLine(px, W, H, c, r + 1, WallCol, key >= 0 && plan.doors.Contains(key));
                        }
                        if (c == 0 && z >= 0) VLine(px, W, 0, r, WallCol, false);
                        if (r == 0 && z >= 0) HLine(px, W, H, c, 0, WallCol, plan.doors.Contains(LevelLayout.EdgeS(-1, c)));
                    }
                floorTex[f].SetPixels(px);
                floorTex[f].Apply();
            }
        }

        static readonly Color SolidCol = new Color(0.03f, 0.03f, 0.035f, 0.9f);
        static readonly Color CorridorCol = new Color(0.16f, 0.17f, 0.19f, 0.9f);
        static readonly Color WashCol = new Color(0.1f, 0.17f, 0.19f, 0.9f);

        static Color FillFor(Room room, bool elevatorOpen)
        {
            switch (room.type)
            {
                case RoomType.Corridor: case RoomType.StairLobby: return CorridorCol;
                case RoomType.Stair: return new Color(0.35f, 0.22f, 0.07f, 0.9f);
                case RoomType.Elevator: return elevatorOpen ? ElevatorCol : SolidCol;
                case RoomType.Washroom: return WashCol;
                case RoomType.Library: return new Color(0.2f, 0.14f, 0.08f, 0.9f);
                default: return FloorCol;
            }
        }

        static void FillCell(Color[] px, int W, int c, int r, Color col)
        {
            int y0 = (LevelBuilder.Rows - 1 - r) * Px;   // texture rows go bottom-up; map row 0 is north
            for (int y = 0; y < Px; y++)
                for (int x = 0; x < Px; x++)
                    px[(y0 + y) * W + c * Px + x] = col;
        }

        static void VLine(Color[] px, int W, int x, int r, Color col, bool door)
        {
            x = Mathf.Clamp(x, 0, W - 1);
            int y0 = (LevelBuilder.Rows - 1 - r) * Px;
            for (int y = 0; y < Px; y++)
            {
                if (door && y >= Px / 2 - 2 && y <= Px / 2 + 1) continue;
                px[(y0 + y) * W + x] = col;
                if (x > 0) px[(y0 + y) * W + x - 1] = col;
            }
        }

        static void HLine(Color[] px, int W, int H, int c, int rowLine, Color col, bool door)
        {
            int y = Mathf.Clamp((LevelBuilder.Rows - rowLine) * Px, 0, H - 1);
            for (int x = 0; x < Px; x++)
            {
                if (door && x >= Px / 2 - 2 && x <= Px / 2 + 1) continue;
                px[y * W + c * Px + x] = col;
                if (y > 0) px[(y - 1) * W + c * Px + x] = col;
            }
        }
        static Texture2D MakeArrow(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Arrow pointing up (+y in texture = top of the GUI rect).
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size;
                    float half = (1f - v) * 0.42f;
                    bool inside = v > 0.08f && v < 0.95f && Mathf.Abs(u) < half && !(v < 0.35f && Mathf.Abs(u) < (0.35f - v) * 0.9f);
                    t.SetPixel(x, y, inside ? Color.white : clear);
                }
            }
            t.Apply();
            return t;
        }

        // ---------------- drawing ----------------

        void BuildStyles(float s)
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                legend = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
                roomLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, clipping = TextClipping.Clip };
            }
            label.fontSize = Mathf.Max(10, Mathf.RoundToInt(18 * s));
            title.fontSize = Mathf.Max(14, Mathf.RoundToInt(40 * s));
            legend.fontSize = Mathf.Max(10, Mathf.RoundToInt(18 * s));
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            var p = PlayerController.Instance;
            if (gm == null || p == null || floorTex == null || !gm.IsPlaying || gm.IsPaused) return;
            GUI.depth = -10;
            float s = Screen.height / 1080f;
            BuildStyles(s);

            if (fullOpen) DrawFullMap(gm, p, s);
            else DrawMinimap(gm, p, s);
        }

        void DrawMinimap(GameManager gm, PlayerController p, float s)
        {
            float cellPx = 9.5f * s;
            float w = LevelBuilder.Cols * cellPx, h = LevelBuilder.Rows * cellPx;
            float m = 30f * s;
            var r = new Rect(Screen.width - m - w, m, w, h);
            Fill(new Rect(r.x - 6 * s, r.y - 6 * s, r.width + 12 * s, r.height + 34 * s), new Color(0f, 0f, 0f, 0.55f));
            DrawFloor(r, p.Floor, gm, p, s, false);
            var room = LevelBuilder.RoomAt(p.transform.position);
            string where = LevelBuilder.InSafeZone(p.transform.position) ? "Museum vault (safe)" : LevelBuilder.InAnnex(p.transform.position) || LevelBuilder.InWing(p.transform.position) ? LevelBuilder.AnnexAreaAt(p.transform.position) + (LevelBuilder.InAnnex(p.transform.position) && p.transform.position.y > 2.5f ? " (upper level)" : "") : room != null ? room.name : "";
            // Long room names would wrap onto the map: shrink the caption until it fits on one line.
            string caption = "FLOOR " + (p.Floor + 1) + "  -  " + where + "   [M] map";
            int size = label.fontSize;
            bool wrap = label.wordWrap;
            label.wordWrap = false;
            while (label.fontSize > 9 && label.CalcSize(new GUIContent(caption)).x > r.width + 10 * s) label.fontSize--;
            Shadowed(new Rect(r.x - 5 * s, r.yMax + 2 * s, r.width + 10 * s, 24 * s), caption, label, new Color(0.85f, 0.85f, 0.8f, 0.9f));
            label.fontSize = size;
            label.wordWrap = wrap;
        }

        void DrawFullMap(GameManager gm, PlayerController p, float s)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.78f));
            Shadowed(new Rect(0, 20 * s, Screen.width, 50 * s), "HALVORSEN TOWER", title, new Color(0.8f, 0.1f, 0.06f));

            // 2 x 2 grid: top row floors 4 and 3, bottom row floors 2 and 1 (like looking at the building).
            float top = 90 * s, bottom = Screen.height - 80 * s;
            float gap = 30 * s;
            float availH = (bottom - top - gap - 2 * 30 * s) / 2f;
            float availW = (Screen.width - 3 * gap) / 2f;
            float cell = Mathf.Min(availH / LevelBuilder.Rows, availW / LevelBuilder.Cols);
            float w = LevelBuilder.Cols * cell, h = LevelBuilder.Rows * cell;
            float x0 = Screen.width * 0.5f - gap * 0.5f - w, x1 = Screen.width * 0.5f + gap * 0.5f;

            int[] order = { 3, 2, 1, 0 };
            for (int i = 0; i < 4; i++)
            {
                int f = order[i];
                float x = (i % 2 == 0) ? x0 : x1;
                float y = top + (i / 2) * (h + 30 * s + gap);
                var r = new Rect(x, y + 30 * s, w, h);
                bool here = f == p.Floor;
                string name = "FLOOR " + (f + 1) + (f == 0 ? "  (ground)" : f == 3 ? "  (top)" : "") + (here ? "   - YOU ARE HERE" : "");
                Shadowed(new Rect(x, y, w, 28 * s), name, label, here ? PlayerCol : new Color(0.85f, 0.85f, 0.8f));
                if (here) Fill(new Rect(r.x - 4 * s, r.y - 4 * s, r.width + 8 * s, r.height + 8 * s), new Color(1f, 0.25f, 0.2f, 0.35f));
                DrawFloor(r, f, gm, p, s, true);
            }

            // Legend.
            float ly = Screen.height - 50 * s;
            float lx = Screen.width * 0.5f - 760 * s;
            lx = LegendItem(lx, ly, s, PlayerCol, "You");
            lx = LegendItem(lx, ly, s, FuseCol, "Fuse");
            lx = LegendItem(lx, ly, s, LockerCol, "Hiding spot");
            lx = LegendItem(lx, ly, s, AmmoCol, "Ammo");
            lx = LegendItem(lx, ly, s, MedCol, "Medkit");
            lx = LegendItem(lx, ly, s, PowerGrid.AnyOut ? GenOffCol : GenOnCol, "Generator");
            lx = LegendItem(lx, ly, s, StairCol, "Stairs");
            lx = LegendItem(lx, ly, s, ShaftCol + new Color(0.15f, 0.15f, 0.15f, 0f), "Stair shaft (drop)");
            lx = LegendItem(lx, ly, s, ElevatorCol, gm.AllFusesFound ? "Lift (open!)" : "Lift (needs 3 fuses)");
            lx = LegendItem(lx, ly, s, new Color(1f, 0.15f, 0.1f), "Trap pit");
            Shadowed(new Rect(lx + 20 * s, ly, 300 * s, 30 * s), "[M] close", legend, new Color(1f, 1f, 1f, 0.6f));
        }

        float LegendItem(float x, float y, float s, Color c, string text)
        {
            Fill(new Rect(x, y + 8 * s, 14 * s, 14 * s), c);
            Shadowed(new Rect(x + 20 * s, y, 220 * s, 30 * s), text, legend, new Color(0.9f, 0.9f, 0.9f));
            return x + 20 * s + legend.CalcSize(new GUIContent(text)).x + 26 * s;
        }

        /// <summary>The world area each floor's map shows: the ground floor takes in the annex to the south and west.</summary>
        static Rect WorldRect(int floor)
        {
            float W = LevelBuilder.Cols * LevelBuilder.Cell, H = LevelBuilder.Rows * LevelBuilder.Cell;
            return floor == 0 ? new Rect(-24f, -60f, W + 30f, H + 60f) : floor == 1 ? new Rect(0f, 0f, W, H + 28f) : new Rect(0f, 0f, W, H);
        }

        Rect mapRect, mapWorld;

        Vector2 ToMap(Rect unused, Vector3 world)
        {
            float u = (world.x - mapWorld.x) / mapWorld.width;
            float v = (world.z - mapWorld.y) / mapWorld.height;
            return new Vector2(mapRect.x + Mathf.Clamp01(u) * mapRect.width, mapRect.y + (1f - Mathf.Clamp01(v)) * mapRect.height);
        }

        Rect WorldToMapRect(Rect w)
        {
            Vector2 a = ToMap(mapRect, new Vector3(w.xMin, 0f, w.yMax)), b = ToMap(mapRect, new Vector3(w.xMax, 0f, w.yMin));
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
        }

        void DrawFloor(Rect box, int floor, GameManager gm, PlayerController p, float s, bool labels)
        {
            // Fit the floor's world area into the box, keeping its shape.
            mapWorld = WorldRect(floor);
            float k0 = Mathf.Min(box.width / mapWorld.width, box.height / mapWorld.height);
            mapRect = new Rect(box.x + (box.width - mapWorld.width * k0) * 0.5f, box.y + (box.height - mapWorld.height * k0) * 0.5f, mapWorld.width * k0, mapWorld.height * k0);
            var r = mapRect;
            if (floor <= 1)
            {
                // The annex buildings (ground floor) and the north wing (floor 2), under the tower's plan.
                foreach (var a in LevelBuilder.AnnexAreas)
                {
                    if (a.floor != floor) continue;
                    var ar = WorldToMapRect(a.xz);
                    Fill(new Rect(ar.x - 1, ar.y - 1, ar.width + 2, ar.height + 2), WallCol);
                    Fill(ar, a.twoStorey ? new Color(0.13f, 0.12f, 0.16f, 0.9f) : FloorCol);
                    if (labels || ar.width > 40f)
                    {
                        roomLabel.fontSize = Mathf.Max(8, Mathf.RoundToInt(13 * s));
                        Shadowed(ar, a.name + (a.twoStorey ? "\n(2 levels)" : ""), roomLabel, new Color(0.8f, 0.78f, 0.7f, 0.85f));
                    }
                }
            }
            GUI.DrawTexture(WorldToMapRect(new Rect(0f, 0f, LevelBuilder.Cols * LevelBuilder.Cell, LevelBuilder.Rows * LevelBuilder.Cell)), floorTex[floor]);
            float cell = mapRect.width * LevelBuilder.Cell / mapWorld.width;
            float dot = Mathf.Max(3f * s, cell * 0.45f);

            // Room names on the big map.
            if (labels)
            {
                roomLabel.fontSize = Mathf.Max(8, Mathf.RoundToInt(cell * 0.42f));
                foreach (var room in LevelBuilder.Plan(floor).rooms)
                {
                    if (room.type == RoomType.Corridor || room.type == RoomType.Elevator || room.cells.Count < 6) continue;
                    roomLabel.fontSize = Mathf.Max(8, Mathf.RoundToInt(cell * 0.42f));
                    var c = room.Center;
                    Vector2 mp = ToMap(r, c);
                    float w = room.rect.width * cell, h = room.rect.height * cell;
                    Shadowed(new Rect(mp.x - w * 0.5f, mp.y - h * 0.5f, w, h), room.name, roomLabel, new Color(0.75f, 0.75f, 0.72f, 0.75f));
                }
            }
            // The vault, once you know about it.
            var bc = SecretBookcase.Instance;
            if (floor == 1 && bc != null && bc.Discovered)
            {
                Vector2 vp = ToMap(r, bc.FrontPosition);
                Shadowed(new Rect(vp.x - 40 * s, vp.y - 30 * s, 60 * s, 20 * s), "VAULT >", label, new Color(1f, 0.8f, 0.3f));
            }
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);

            // Trap pits: a red cross.
            foreach (var pit in TrapPit.All)
            {
                if (pit == null || pit.Floor != floor) continue;
                Vector2 tp = ToMap(r, pit.Center);
                float k = Mathf.Max(5f * s, cell * 0.7f);
                Fill(new Rect(tp.x - k * 0.5f, tp.y - 1.5f, k, 3f), new Color(1f, 0.15f, 0.1f, 0.95f));
                Fill(new Rect(tp.x - 1.5f, tp.y - k * 0.5f, 3f, k), new Color(1f, 0.15f, 0.1f, 0.95f));
            }

            // Lockers.
            foreach (var spot in HidingSpot.All)
            {
                if (spot == null || LevelBuilder.FloorAt(spot.transform.position + Vector3.up * 0.2f) != floor) continue;
                Vector2 sp = ToMap(r, spot.transform.position);
                float k = dot * 0.8f;
                Fill(new Rect(sp.x - k * 0.5f, sp.y - k * 0.5f, k, k), spot.Safe ? FuseCol : LockerCol);
            }

            // Ammo and medkits.
            foreach (var pick in Pickup.All)
            {
                if (pick == null || pick.kind == PickupKind.Fuse || LevelBuilder.FloorAt(pick.transform.position + Vector3.down * 0.5f) != floor) continue;
                Vector2 pp = ToMap(r, pick.transform.position);
                bool ammo = pick.kind == PickupKind.Ammo;
                float k = dot * (ammo ? 1.25f : 0.8f);   // ammo stands out: it's what you go looking for
                Fill(new Rect(pp.x - k * 0.5f, pp.y - k * 0.5f, k, k), ammo ? AmmoCol : MedCol);
            }

            // Fuses still to find.
            if (gm.Level != null)
            {
                foreach (var fuse in gm.Level.fuses)
                {
                    if (fuse == null || fuse.floor != floor) continue;
                    Vector2 fp = ToMap(r, fuse.transform.position);
                    float k = dot * (1.4f + 0.5f * pulse);
                    Fill(new Rect(fp.x - k * 0.5f - 1, fp.y - k * 0.5f - 1, k + 2, k + 2), new Color(0, 0, 0, 0.8f));
                    Fill(new Rect(fp.x - k * 0.5f, fp.y - k * 0.5f, k, k), FuseCol);
                }

                // The floor's generator: green while the power is on, pulsing red when it needs restarting.
                var gen = PowerGrid.Generators[floor];
                if (gen != null)
                {
                    Vector2 gp = ToMap(r, gen.transform.position);
                    bool on = PowerGrid.IsOn(floor);
                    float k = dot * (on ? 1.1f : 1.4f + 0.6f * pulse);
                    Fill(new Rect(gp.x - k * 0.5f - 1, gp.y - k * 0.5f - 1, k + 2, k + 2), new Color(0, 0, 0, 0.8f));
                    Fill(new Rect(gp.x - k * 0.5f, gp.y - k * 0.5f, k, k), on ? GenOnCol : GenOffCol);
                    if (labels && !on) Shadowed(new Rect(gp.x + k, gp.y - 10 * s, 160 * s, 20 * s), "POWER", label, GenOffCol);
                }

                // The lift: marked once you've seen it or it's open.
                var lift = gm.Level.elevator;
                if (lift != null && floor == 0 && !lift.IsOpen && labels)
                {
                    Vector2 ep = ToMap(r, lift.CarCenter);
                    Shadowed(new Rect(ep.x - 40 * s, ep.y, 80 * s, 20 * s), "LIFT", label, new Color(0.6f, 0.8f, 0.65f, 0.8f));
                }
                if (lift != null && lift.IsOpen && floor == 0)
                {
                    Vector2 ep = ToMap(r, lift.CarCenter);
                    float k = cell * (0.8f + 0.4f * pulse);
                    Fill(new Rect(ep.x - k * 0.5f, ep.y - k * 0.5f, k, k), new Color(ElevatorCol.r, ElevatorCol.g, ElevatorCol.b, 0.6f));
                }
            }

            // Player arrow.
            if (p.Floor == floor)
            {
                Vector2 pp = ToMap(r, p.transform.position);
                float k = Mathf.Max(12f, cell * 1.1f);
                var old = GUI.matrix;
                var oldCol = GUI.color;
                GUIUtility.RotateAroundPivot(p.transform.eulerAngles.y, pp);
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.DrawTexture(new Rect(pp.x - k * 0.5f + 1, pp.y - k * 0.5f + 1, k, k), arrow);
                GUI.color = PlayerCol;
                GUI.DrawTexture(new Rect(pp.x - k * 0.5f, pp.y - k * 0.5f, k, k), arrow);
                GUI.matrix = old;
                GUI.color = oldCol;
            }

            // Compass tick.
            Shadowed(new Rect(r.x, r.y - 2 * s, 24 * s, 22 * s), "N", label, new Color(1f, 1f, 1f, 0.7f));
        }


        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }
    }
}
