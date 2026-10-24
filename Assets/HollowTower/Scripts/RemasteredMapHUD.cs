using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower
{
    /// <summary>
    /// Tactical On-Screen Minimap, Floor Teleporter & Scene Switcher for HollowTower Remastered.
    /// Features:
    /// - Interactive Floor Teleportation: Click Floor 1-4 buttons or press 1-4 to instantly warp between floors.
    /// - Map Click-to-Teleport: Click anywhere on the minimap or full tactical map to teleport directly to that room/spot!
    /// - Complete Map Hide/Show: Press 'H' (or click [H] Hide Map) to completely toggle/hide the map and all HUD for total immersion.
    /// - Fullscreen Blueprint Overview ('M' key): Displays both floors with room hover detection and click-to-warp.
    /// - Scene Switcher ('F9' key): Seamlessly toggles between the Remastered Map and the Original GitHub Game.
    /// </summary>
    public class RemasteredMapHUD : MonoBehaviour
    {
        [Header("Settings")]
        public bool showMinimap = true;
        public bool fullMapOpen = false;
        public bool hideHUD = false;

        public static bool IsFullMapOpen = false;

        [Header("Keybindings")]
        public KeyCode toggleMapKey = KeyCode.M;
        public KeyCode hideHUDKey = KeyCode.H;
        public KeyCode switchSceneKey = KeyCode.F9;   // F1-F4 switch between the four players
        public KeyCode floor1Key = KeyCode.Alpha1;
        public KeyCode floor2Key = KeyCode.Alpha2;

        const int FloorCount = 2;   // floors 3 and 4 were removed: the tower is the ground floor and the party floor

        private Texture2D[] floorTextures;
        private Texture2D playerIcon;
        private Texture2D bgTexture;
        private Texture2D hoverBoxTexture;

        private GUIStyle headerStyle;
        private GUIStyle roomStyle;
        private GUIStyle hintStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeFloorBtnStyle;
        private GUIStyle boxStyle;
        private GUIStyle tooltipStyle;

        private int currentFloor = 0;
        private string currentRoomName = "Main Lobby";
        public const float StoryHeight = 4.2f;
        public const float CellSize = 4.0f;

        // Feedback banner when teleporting
        private string feedbackMessage = "";
        private float feedbackTimer = 0f;

        void Start()
        {
            GenerateTextures();
        }

        void GenerateTextures()
        {
            // Dark semi-transparent background
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.10f, 0.90f));
            bgTexture.Apply();

            // Hover indicator texture
            hoverBoxTexture = new Texture2D(1, 1);
            hoverBoxTexture.SetPixel(0, 0, new Color(0.2f, 0.8f, 1.0f, 0.35f));
            hoverBoxTexture.Apply();

            // Player arrow icon
            playerIcon = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float halfW = (15 - y) * 0.45f;
                    if (Mathf.Abs(x - 7.5f) <= halfW && y >= 2)
                        playerIcon.SetPixel(x, y, new Color(1f, 0.25f, 0.2f, 1f));
                    else
                        playerIcon.SetPixel(x, y, Color.clear);
                }
            }
            playerIcon.Apply();

            // Generate blueprint textures for all 4 floors
            var plans = LevelLayout.Plans;
            floorTextures = new Texture2D[FloorCount];
            int cols = LevelLayout.Cols; // 36
            int rows = LevelLayout.Rows; // 24
            int px = 4; // pixels per cell

            int tw = cols * px;
            int th = rows * px;

            for (int f = 0; f < FloorCount; f++)
            {
                var p = plans[f];
                var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;

                Color[] pixels = new Color[tw * th];
                Color solidCol = new Color(0.04f, 0.05f, 0.07f, 0.95f);
                Color roomCol = new Color(0.18f, 0.24f, 0.32f, 0.9f);
                Color corridorCol = new Color(0.24f, 0.32f, 0.42f, 0.9f);
                Color wallCol = new Color(0.65f, 0.72f, 0.82f, 1.0f);
                Color doorCol = new Color(0.95f, 0.75f, 0.25f, 1.0f);

                for (int i = 0; i < pixels.Length; i++) pixels[i] = solidCol;

                // 1. Fill cells
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int z = p.zone[r, c];
                        if (z < 0) continue;

                        Color cCol = (p.corridor != null && p.corridor.Contains(c, r)) ? corridorCol : roomCol;
                        if (p.shaft[r, c]) cCol = new Color(0.02f, 0.02f, 0.03f, 1.0f); // open shaft

                        int basePx = c * px;
                        int basePy = (rows - 1 - r) * px;

                        for (int py = 0; py < px; py++)
                        {
                            for (int pxi = 0; pxi < px; pxi++)
                            {
                                pixels[(basePy + py) * tw + (basePx + pxi)] = cCol;
                            }
                        }
                    }
                }

                // 2. Draw walls on edges
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int z = p.zone[r, c];
                        int ze = c + 1 < cols ? p.zone[r, c + 1] : -1;
                        int zs = r + 1 < rows ? p.zone[r + 1, c] : -1;

                        int basePx = c * px;
                        int basePy = (rows - 1 - r) * px;

                        // Vertical wall to the east
                        if (z != ze)
                        {
                            int edgeKey = LevelLayout.EdgeE(r, c);
                            bool isDoor = p.doors.Contains(edgeKey);
                            Color lCol = isDoor ? doorCol : wallCol;

                            int lineX = basePx + px - 1;
                            for (int py = 0; py < px; py++)
                            {
                                pixels[(basePy + py) * tw + lineX] = lCol;
                            }
                        }

                        // Horizontal wall to the south
                        if (z != zs)
                        {
                            int edgeKey = LevelLayout.EdgeS(r, c);
                            bool isDoor = p.doors.Contains(edgeKey);
                            Color lCol = isDoor ? doorCol : wallCol;

                            int lineY = basePy;
                            for (int pxi = 0; pxi < px; pxi++)
                            {
                                pixels[lineY * tw + (basePx + pxi)] = lCol;
                            }
                        }
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();
                floorTextures[f] = tex;
            }
        }

        void Update()
        {
            // Toggle complete HUD visibility
            if (InputWrap.Down(hideHUDKey))
            {
                hideHUD = !hideHUD;
                ShowFeedback(hideHUD ? "HUD Hidden (Press 'H' to Show)" : "HUD Visible");
            }

            // If HUD is hidden, only process 'H' to bring it back
            if (hideHUD) return;

            // Toggle Fullscreen Tactical Blueprint
            if (InputWrap.Down(toggleMapKey))
            {
                fullMapOpen = !fullMapOpen;
                IsFullMapOpen = fullMapOpen;
            }

            // Switch between Remastered and Original GitHub Game
            if (InputWrap.Down(switchSceneKey))
            {
                ToggleScene();
            }

            // Quick Floor Teleportation Hotkeys (1-4)
            if (InputWrap.Down(floor1Key)) TeleportToFloor(0);
            else if (InputWrap.Down(floor2Key)) TeleportToFloor(1);

            UpdatePlayerLocation();

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
            }
        }

        /// <summary>The body the map follows: the player being steered when there is a party, else this object.</summary>
        Transform Body { get { return PlayerParty.Active != null ? PlayerParty.Active : transform; } }

        void UpdatePlayerLocation()
        {
            Vector3 pos = Body.position;

            // Determine floor by height
            if (pos.y < StoryHeight * 0.85f) currentFloor = 0;
            else if (pos.y < StoryHeight * 1.85f) currentFloor = 1;
            else currentFloor = 1;

            // Determine room by grid cell
            int c = Mathf.FloorToInt(pos.x / CellSize);
            int r = LevelLayout.Rows - 1 - Mathf.FloorToInt(pos.z / CellSize);

            var plans = LevelLayout.Plans;
            if (currentFloor >= 0 && currentFloor < 4)
            {
                var p = plans[currentFloor];
                var room = p.RoomAt(c, r);
                if (room != null)
                {
                    currentRoomName = room.Label;
                }
                else if (p.corridor != null && p.corridor.Contains(c, r))
                {
                    currentRoomName = "Corridor Network";
                }
                else
                {
                    currentRoomName = "Service Area";
                }
            }
        }

        public void TeleportToFloor(int targetFloor)
        {
            targetFloor = Mathf.Clamp(targetFloor, 0, FloorCount - 1);
            currentFloor = targetFloor;

            // Central safe teleport spot (Elevator lobby / main corridor)
            float targetX = 72.0f; // Central X (Column 18)
            float targetZ = 48.0f; // Central Z (Row 12)
            float targetY = targetFloor * StoryHeight + 0.15f;

            TeleportPlayerTo(new Vector3(targetX, targetY, targetZ));
            if (TowerFloorCulling.Instance != null) TowerFloorCulling.Instance.ForceFloorUpdate(targetFloor);
            ShowFeedback($"Teleported to {GetFloorShortName(targetFloor)} Lobby");
        }

        public void TeleportToWorldPosition(float worldX, float worldZ, int targetFloor)
        {
            targetFloor = Mathf.Clamp(targetFloor, 0, FloorCount - 1);
            currentFloor = targetFloor;

            // Clamp inside building bounds with safe 1m margin
            float clampedX = Mathf.Clamp(worldX, 1.5f, (LevelLayout.Cols * CellSize) - 1.5f);
            float clampedZ = Mathf.Clamp(worldZ, 1.5f, (LevelLayout.Rows * CellSize) - 1.5f);
            float targetY = targetFloor * StoryHeight + 0.15f;

            TeleportPlayerTo(new Vector3(clampedX, targetY, clampedZ));
            if (TowerFloorCulling.Instance != null) TowerFloorCulling.Instance.ForceFloorUpdate(targetFloor);

            // Identify room clicked
            int c = Mathf.FloorToInt(clampedX / CellSize);
            int r = LevelLayout.Rows - 1 - Mathf.FloorToInt(clampedZ / CellSize);
            var room = LevelLayout.Plans[targetFloor].RoomAt(c, r);
            string locName = room != null ? room.Label : "Corridor";

            ShowFeedback($"Teleported to {GetFloorShortName(targetFloor)}: {locName}");
        }

        private void TeleportPlayerTo(Vector3 targetPos)
        {
            var cc = Body.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Body.position = targetPos;

            if (cc != null) cc.enabled = true;
            Debug.Log($"<color=cyan>[Teleport]</color> Player moved to: {targetPos}");
        }

        public void ShowFeedback(string msg)
        {
            feedbackMessage = msg;
            feedbackTimer = 3.0f;
        }

        public void ToggleScene()
        {
            string current = SceneManager.GetActiveScene().name;
            string target = current.Contains("Remastered") ? "HollowTower" : "HollowTower_Remastered";
            Debug.Log($"<color=cyan>[SceneSwitcher]</color> Switching scene to: {target}");
            SceneManager.LoadScene(target);
        }

        void InitStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 13;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);

            roomStyle = new GUIStyle(GUI.skin.label);
            roomStyle.fontSize = 11;
            roomStyle.fontStyle = FontStyle.Normal;
            roomStyle.normal.textColor = Color.white;

            hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 11;
            hintStyle.normal.textColor = new Color(0.75f, 0.85f, 0.95f);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 11;
            buttonStyle.fontStyle = FontStyle.Bold;

            activeFloorBtnStyle = new GUIStyle(GUI.skin.button);
            activeFloorBtnStyle.fontSize = 11;
            activeFloorBtnStyle.fontStyle = FontStyle.Bold;
            activeFloorBtnStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);

            boxStyle = new GUIStyle(GUI.skin.box);

            tooltipStyle = new GUIStyle(GUI.skin.box);
            tooltipStyle.fontSize = 11;
            tooltipStyle.normal.textColor = Color.yellow;
        }

        void OnGUI()
        {
            InitStyles();

            // When map is completely hidden by 'H', display a tiny discreet prompt in bottom right
            if (hideHUD)
            {
                GUI.Label(new Rect(Screen.width - 170, Screen.height - 25, 160, 20), "[H] Show Map & HUD", hintStyle);
                return;
            }

            // Top-Left Control Bar (Scene Switcher, Floor Teleporters, Hide HUD)
            DrawControlBar();

            // Fullscreen Blueprint or Top-Right Minimap
            if (fullMapOpen)
            {
                DrawFullMap();
            }
            else if (showMinimap)
            {
                DrawMinimap();
            }

            // Feedback banner when teleporting
            if (feedbackTimer > 0f && !string.IsNullOrEmpty(feedbackMessage))
            {
                float bannerW = 340f;
                float bannerH = 30f;
                float bx = (Screen.width - bannerW) * 0.5f;
                float by = Screen.height - 50f;
                GUI.DrawTexture(new Rect(bx, by, bannerW, bannerH), bgTexture);
                GUI.Box(new Rect(bx, by, bannerW, bannerH), GUIContent.none);
                GUI.Label(new Rect(bx + 10, by + 6, bannerW - 20, 20), "⚡ " + feedbackMessage, headerStyle);
            }
        }

        void DrawControlBar()
        {
            string activeScene = SceneManager.GetActiveScene().name;
            bool isRemaster = activeScene.Contains("Remastered");

            float barW = 280f;
            float barH = 66f;
            GUILayout.BeginArea(new Rect(12, 12, barW, barH), bgTexture != null ? boxStyle : GUIStyle.none);
            
            // Top row: Scene switch, Map Toggle, Hide Map
            GUILayout.BeginHorizontal();
            string targetLabel = isRemaster ? "[" + switchSceneKey + "] GitHub" : "[" + switchSceneKey + "] Remaster";
            if (GUILayout.Button(targetLabel, buttonStyle, GUILayout.Width(96), GUILayout.Height(24)))
            {
                ToggleScene();
            }

            string mapLabel = fullMapOpen ? "[M] Close" : "[M] Map";
            if (GUILayout.Button(mapLabel, buttonStyle, GUILayout.Width(76), GUILayout.Height(24)))
            {
                fullMapOpen = !fullMapOpen;
                IsFullMapOpen = fullMapOpen;
            }

            if (GUILayout.Button("[H] Hide", buttonStyle, GUILayout.Width(66), GUILayout.Height(24)))
            {
                hideHUD = true;
                ShowFeedback("HUD Hidden (Press 'H' to Show)");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Bottom row: Inter-floor teleport buttons
            GUILayout.BeginHorizontal();
            GUILayout.Label("Floor:", hintStyle, GUILayout.Width(42));
            for (int f = 0; f < FloorCount; f++)
            {
                bool isCurrent = (f == currentFloor);
                string btnLabel = $"[{f + 1}]";
                var st = isCurrent ? activeFloorBtnStyle : buttonStyle;
                if (GUILayout.Button(btnLabel, st, GUILayout.Width(45), GUILayout.Height(22)))
                {
                    TeleportToFloor(f);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void DrawMinimap()
        {
            if (floorTextures == null || currentFloor < 0 || currentFloor >= floorTextures.Length) return;

            float mapW = 250f;
            float mapH = 165f;
            float pad = 12f;
            float x = Screen.width - mapW - pad;
            float y = pad;

            // Background box
            GUI.DrawTexture(new Rect(x - 6, y - 6, mapW + 12, mapH + 68), bgTexture);

            // Floor & Room Title
            string floorTitle = GetFloorName(currentFloor);
            GUI.Label(new Rect(x, y, mapW, 18), floorTitle, headerStyle);
            GUI.Label(new Rect(x, y + 17, mapW, 16), "Room: " + currentRoomName, roomStyle);

            // Blueprint Map Rect
            Rect mapRect = new Rect(x, y + 36, mapW, mapH);
            GUI.DrawTexture(mapRect, floorTextures[currentFloor], ScaleMode.StretchToFill);

            // Click on Minimap to teleport
            HandleMapClick(mapRect, currentFloor);

            if (currentFloor == 0) DrawDevRoute(mapRect);

            // Player Marker
            DrawPlayerMarker(mapRect);

            // Helpful hints
            GUI.Label(new Rect(x, y + mapH + 40, mapW, 16), "Click map to warp | [H] Hide", hintStyle);
        }

        void DrawFullMap()
        {
            float totalW = Mathf.Min(Screen.width * 0.90f, 820f);
            float totalH = Mathf.Min(Screen.height * 0.85f, 580f);
            float x = (Screen.width - totalW) * 0.5f;
            float y = (Screen.height - totalH) * 0.5f;

            GUI.DrawTexture(new Rect(x - 12, y - 12, totalW + 24, totalH + 24), bgTexture);

            GUILayout.BeginArea(new Rect(x, y, totalW, totalH));
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("TACTICAL FACILITY BLUEPRINT (CLICK ANYWHERE TO TELEPORT)", headerStyle);
            if (GUILayout.Button("✕ Close [M]", buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
            {
                fullMapOpen = false;
                IsFullMapOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Current Location: {GetFloorName(currentFloor)} — {currentRoomName}", roomStyle);
            GUILayout.Label("Click on any room on either floor to instantly warp there. Press [1-2] or [H] to hide map.", hintStyle);
            GUILayout.Space(6);

            // the two floors side by side
            float subW = (totalW - 20) * 0.5f;
            float subH = Mathf.Min(totalH - 100, subW * LevelLayout.Rows / (float)LevelLayout.Cols);

            for (int f = 0; f < FloorCount; f++)
            {
                int gx = f;
                int gy = 0;
                float sx = gx * (subW + 10);
                float sy = 70 + gy * (subH + 12);
                Rect subRect = new Rect(sx, sy, subW, subH);

                // Sub-floor header & quick warp button
                GUI.Box(new Rect(subRect.x - 2, subRect.y - 20, subW + 4, subH + 24), GUIContent.none);
                GUI.Label(new Rect(subRect.x + 4, subRect.y - 19, subW - 100, 18), GetFloorName(f), f == currentFloor ? headerStyle : roomStyle);
                
                if (GUI.Button(new Rect(subRect.x + subW - 85, subRect.y - 20, 85, 18), $"Warp F{f + 1}", buttonStyle))
                {
                    TeleportToFloor(f);
                }

                if (floorTextures != null && floorTextures[f] != null)
                {
                    GUI.DrawTexture(subRect, floorTextures[f], ScaleMode.StretchToFill);

                    // Click to teleport on this floor
                    HandleMapClick(subRect, f);

                    // Draw Elevator indicators
                    DrawElevatorMarkers(subRect, f);
                    if (f == 0) DrawDevRoute(subRect);

                    if (f == currentFloor)
                    {
                        DrawPlayerMarker(subRect);
                    }
                }
            }

            GUILayout.EndArea();
        }

        void DrawElevatorMarkers(Rect mapRect, int floorIndex)
        {
            float totalW = LevelLayout.Cols * CellSize;
            float totalH = LevelLayout.Rows * CellSize;

            if (floorIndex == 0)
            {
                // Floor 1 Spawn Elevator in Parking Access (141.2m, 12.0m)
                float nx = 141.2f / totalW;
                float ny = 12.0f / totalH;
                float px = mapRect.x + nx * mapRect.width;
                float py = mapRect.y + (1f - ny) * mapRect.height;
                DrawLiftBadge(px, py, "[SPAWN LIFT]", new Color(0.2f, 0.95f, 0.45f, 0.95f));
            }
            else if (floorIndex == 1)
            {
                // the exit elevator, in floor 2's locker room (141.6m, 6.0m)
                float nx = 141.2f / totalW;
                float ny = 6.0f / totalH;
                float px = mapRect.x + nx * mapRect.width;
                float py = mapRect.y + (1f - ny) * mapRect.height;
                DrawLiftBadge(px, py, "[EXIT LIFT]", new Color(0.95f, 0.8f, 0.25f, 0.95f));
            }
        }

        // TEMPORARY dev aid: the floor 1 route (world x, z). Orange: spawn lift to the fuse cell (safe minefield lanes,
        // the chasm girder, Food Prep's false wall). Cyan: fuse cell to the stairwell (ledges, the locker room false wall).
        static readonly Vector2[] RouteToFuse =
        {
            new Vector2(141.2f, 12f), new Vector2(130f, 20f), new Vector2(130f, 26f), new Vector2(108f, 26f), new Vector2(105.8f, 26f),
            new Vector2(105.8f, 24.7f), new Vector2(100f, 24.7f), new Vector2(100f, 26f), new Vector2(57f, 26.9f), new Vector2(47f, 26.9f),
            new Vector2(38f, 26.9f), new Vector2(38f, 38f), new Vector2(44f, 38f), new Vector2(52f, 38f), new Vector2(65.4f, 45.5f),
        };
        static readonly Vector2[] RouteToStairs =
        {
            new Vector2(65.4f, 45.5f), new Vector2(52f, 38f), new Vector2(44f, 38f), new Vector2(38f, 34f), new Vector2(38f, 26.5f),
            new Vector2(25f, 26.5f), new Vector2(25f, 37f), new Vector2(26f, 42f), new Vector2(18f, 42f), new Vector2(10f, 47f),
            new Vector2(10f, 57f), new Vector2(18f, 62f), new Vector2(26f, 62f), new Vector2(27.3f, 70f), new Vector2(27.3f, 78f),
            new Vector2(26f, 84f), new Vector2(22f, 86f),
        };

        void DrawDevRoute(Rect mapRect)
        {
            DrawPolyline(mapRect, RouteToFuse, new Color(1f, 0.55f, 0.1f, 0.95f));
            DrawPolyline(mapRect, RouteToStairs, new Color(0.2f, 0.9f, 1f, 0.95f));
            Vector2 f = MapPoint(mapRect, new Vector2(65.4f, 46.85f)), st = MapPoint(mapRect, new Vector2(20f, 86f));
            DrawLiftBadge(f.x, f.y, "[FUSE CELL]", new Color(1f, 0.6f, 0.15f, 1f));
            DrawLiftBadge(st.x, st.y, "[STAIRS]", new Color(0.3f, 0.9f, 1f, 1f));
            foreach (var w in new[] { new Vector2(48f, 38f), new Vector2(10f, 52f) })
            {
                Vector2 p = MapPoint(mapRect, w);
                DrawLiftBadge(p.x, p.y, "false wall", new Color(1f, 0.45f, 0.05f, 1f));
            }
        }

        Vector2 MapPoint(Rect mapRect, Vector2 world)
        {
            return new Vector2(mapRect.x + world.x / (LevelLayout.Cols * CellSize) * mapRect.width,
                               mapRect.y + (1f - world.y / (LevelLayout.Rows * CellSize)) * mapRect.height);
        }

        void DrawPolyline(Rect mapRect, Vector2[] pts, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var saved = GUI.matrix;
            GUI.color = color;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = MapPoint(mapRect, pts[i]), b = MapPoint(mapRect, pts[i + 1]);
                float len = Vector2.Distance(a, b);
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(ang, a);
                GUI.DrawTexture(new Rect(a.x, a.y - 1f, len, 2f), Texture2D.whiteTexture);
                GUI.matrix = saved;
            }
            GUI.color = Color.white;
        }

        void DrawLiftBadge(float px, float py, string label, Color color)
        {
            var badgeStyle = new GUIStyle(GUI.skin.label);
            badgeStyle.fontSize = 9;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.normal.textColor = color;
            badgeStyle.alignment = TextAnchor.MiddleCenter;

            GUI.color = color;
            GUI.DrawTexture(new Rect(px - 4, py - 4, 8, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(px - 45, py + 4, 90, 16), label, badgeStyle);
        }

        void HandleMapClick(Rect mapRect, int floorIndex)
        {
            Vector2 mousePos = Event.current.mousePosition;

            // Check if mouse is hovering over this map area
            if (mapRect.Contains(mousePos))
            {
                float localX = mousePos.x - mapRect.x;
                float localY = mousePos.y - mapRect.y;
                float normX = Mathf.Clamp01(localX / mapRect.width);
                float normZ = Mathf.Clamp01(1.0f - (localY / mapRect.height));

                float worldX = normX * (LevelLayout.Cols * CellSize);
                float worldZ = normZ * (LevelLayout.Rows * CellSize);

                // Highlight crosshair hover
                GUI.DrawTexture(new Rect(mousePos.x - 3, mousePos.y - 3, 6, 6), hoverBoxTexture);

                // On click, teleport immediately!
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    TeleportToWorldPosition(worldX, worldZ, floorIndex);
                    Event.current.Use();
                }
            }
        }

        void DrawPlayerMarker(Rect mapRect)
        {
            if (playerIcon == null) return;

            Vector3 pos = Body.position;
            float normX = Mathf.Clamp01(pos.x / (LevelLayout.Cols * CellSize));
            float normY = Mathf.Clamp01(pos.z / (LevelLayout.Rows * CellSize));

            // Grid coordinate origin is North (top)
            float screenPx = mapRect.x + normX * mapRect.width;
            float screenPy = mapRect.y + (1f - normY) * mapRect.height;

            // Rotate around marker center according to player rotation
            float angle = Body.eulerAngles.y;
            Vector2 pivot = new Vector2(screenPx, screenPy);

            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.DrawTexture(new Rect(screenPx - 7, screenPy - 7, 14, 14), playerIcon);
            GUIUtility.RotateAroundPivot(-angle, pivot);
        }

        string GetFloorName(int floor)
        {
            switch (floor)
            {
                case 0: return "FLOOR 1: GROUND (Lobby)";
                case 1: return "FLOOR 2: SCHOOL (Labs)";
                case 2: return "FLOOR 3: HOTEL (Rooms)";
                case 3: return "FLOOR 4: OFFICES (HQ)";
                default: return $"FLOOR {floor + 1}";
            }
        }

        string GetFloorShortName(int floor)
        {
            switch (floor)
            {
                case 0: return "F1 Ground";
                case 1: return "F2 School";
                case 2: return "F3 Hotel";
                case 3: return "F4 Offices";
                default: return $"Floor {floor + 1}";
            }
        }
    }
}
