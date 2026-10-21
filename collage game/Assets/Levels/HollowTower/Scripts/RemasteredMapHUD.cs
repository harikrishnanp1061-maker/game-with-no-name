using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower
{
    /// <summary>
    /// Tactical On-Screen Minimap & Scene Switcher for HollowTower Remastered.
    /// Features:
    /// - Persistent top-right Minimap with player icon and heading.
    /// - Real-time Floor and Room name detection.
    /// - 'M' key to toggle full-screen tactical blueprint overview.
    /// - 'F1' key (or on-screen button) to seamlessly switch between Remastered Map and Original GitHub Game.
    /// </summary>
    public class RemasteredMapHUD : MonoBehaviour
    {
        [Header("Settings")]
        public bool showMinimap = true;
        public bool fullMapOpen = false;
        public KeyCode toggleMapKey = KeyCode.M;
        public KeyCode switchSceneKey = KeyCode.F1;

        private Texture2D[] floorTextures;
        private Texture2D playerIcon;
        private Texture2D bgTexture;
        private GUIStyle headerStyle;
        private GUIStyle roomStyle;
        private GUIStyle hintStyle;
        private GUIStyle buttonStyle;
        private GUIStyle boxStyle;

        private int currentFloor = 0;
        private string currentRoomName = "Main Lobby";
        public const float StoryHeight = 4.2f;
        public const float CellSize = 4.0f;

        void Start()
        {
            GenerateTextures();
        }

        void GenerateTextures()
        {
            // Dark semi-transparent background
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.11f, 0.88f));
            bgTexture.Apply();

            // Player arrow icon
            playerIcon = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    // Triangle pointing up
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
            floorTextures = new Texture2D[4];
            int cols = LevelLayout.Cols; // 36
            int rows = LevelLayout.Rows; // 24
            int px = 4; // pixels per cell

            int tw = cols * px;
            int th = rows * px;

            for (int f = 0; f < 4; f++)
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
            if (Input.GetKeyDown(toggleMapKey))
            {
                fullMapOpen = !fullMapOpen;
            }

            if (Input.GetKeyDown(switchSceneKey))
            {
                ToggleScene();
            }

            UpdatePlayerLocation();
        }

        void UpdatePlayerLocation()
        {
            Vector3 pos = transform.position;

            // Determine floor by height
            if (pos.y < StoryHeight * 0.85f) currentFloor = 0;
            else if (pos.y < StoryHeight * 1.85f) currentFloor = 1;
            else if (pos.y < StoryHeight * 2.85f) currentFloor = 2;
            else currentFloor = 3;

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
                    currentRoomName = room.name;
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

            boxStyle = new GUIStyle(GUI.skin.box);
        }

        void OnGUI()
        {
            InitStyles();

            // Top-Left Scene Switcher Bar
            DrawSceneSwitcherBar();

            // Top-Right Minimap or Fullscreen Map
            if (fullMapOpen)
            {
                DrawFullMap();
            }
            else if (showMinimap)
            {
                DrawMinimap();
            }
        }

        void DrawSceneSwitcherBar()
        {
            string activeScene = SceneManager.GetActiveScene().name;
            bool isRemaster = activeScene.Contains("Remastered");

            GUILayout.BeginArea(new Rect(15, 15, 420, 70), bgTexture != null ? boxStyle : GUIStyle.none);
            GUILayout.BeginHorizontal();

            string targetLabel = isRemaster ? "[F1] Switch to GitHub Original" : "[F1] Switch to Remastered Map";
            if (GUILayout.Button(targetLabel, buttonStyle, GUILayout.Width(220), GUILayout.Height(28)))
            {
                ToggleScene();
            }

            string mapLabel = fullMapOpen ? "[M] Close Map" : "[M] Tactical Map";
            if (GUILayout.Button(mapLabel, buttonStyle, GUILayout.Width(110), GUILayout.Height(28)))
            {
                fullMapOpen = !fullMapOpen;
            }

            GUILayout.EndHorizontal();

            string sceneDesc = isRemaster ? "Active: Remastered High-Fidelity 3D Map" : "Active: Original GitHub Prototype Build";
            GUILayout.Label(sceneDesc, hintStyle);
            GUILayout.EndArea();
        }

        void DrawMinimap()
        {
            if (floorTextures == null || currentFloor < 0 || currentFloor >= floorTextures.Length) return;

            float mapW = 240f;
            float mapH = 160f;
            float pad = 12f;
            float x = Screen.width - mapW - pad;
            float y = pad;

            // Background box
            GUI.DrawTexture(new Rect(x - 4, y - 4, mapW + 8, mapH + 46), bgTexture);

            // Floor & Room Title
            string floorTitle = GetFloorName(currentFloor);
            GUI.Label(new Rect(x, y, mapW, 18), floorTitle, headerStyle);
            GUI.Label(new Rect(x, y + 16, mapW, 16), "Room: " + currentRoomName, roomStyle);

            // Blueprint Map
            Rect mapRect = new Rect(x, y + 36, mapW, mapH);
            GUI.DrawTexture(mapRect, floorTextures[currentFloor], ScaleMode.StretchToFill);

            // Player Marker
            DrawPlayerMarker(mapRect);
        }

        void DrawFullMap()
        {
            float totalW = Mathf.Min(Screen.width * 0.85f, 750f);
            float totalH = Mathf.Min(Screen.height * 0.80f, 520f);
            float x = (Screen.width - totalW) * 0.5f;
            float y = (Screen.height - totalH) * 0.5f;

            GUI.DrawTexture(new Rect(x - 10, y - 10, totalW + 20, totalH + 20), bgTexture);

            GUILayout.BeginArea(new Rect(x, y, totalW, totalH));
            GUILayout.Label("TACTICAL FACILITY BLUEPRINT (4 FLOORS)", headerStyle);
            GUILayout.Label($"Currently on: {GetFloorName(currentFloor)} — Room: {currentRoomName}", roomStyle);
            GUILayout.Space(6);

            // 2x2 grid of all 4 floors
            float subW = (totalW - 20) * 0.5f;
            float subH = (totalH - 80) * 0.5f;

            for (int f = 0; f < 4; f++)
            {
                int gx = f % 2;
                int gy = f / 2;
                Rect subRect = new Rect(gx * (subW + 10), 50 + gy * (subH + 10), subW, subH);

                GUI.Box(new Rect(subRect.x - 2, subRect.y - 18, subW + 4, subH + 22), GUIContent.none);
                GUI.Label(new Rect(subRect.x, subRect.y - 18, subW, 18), GetFloorName(f), f == currentFloor ? headerStyle : roomStyle);

                if (floorTextures != null && floorTextures[f] != null)
                {
                    GUI.DrawTexture(subRect, floorTextures[f], ScaleMode.StretchToFill);
                    if (f == currentFloor)
                    {
                        DrawPlayerMarker(subRect);
                    }
                }
            }

            GUILayout.EndArea();
        }

        void DrawPlayerMarker(Rect mapRect)
        {
            if (playerIcon == null) return;

            Vector3 pos = transform.position;
            float normX = Mathf.Clamp01(pos.x / (LevelLayout.Cols * CellSize));
            float normY = Mathf.Clamp01(pos.z / (LevelLayout.Rows * CellSize));

            // Grid coordinate origin is North (top)
            float screenPx = mapRect.x + normX * mapRect.width;
            float screenPy = mapRect.y + (1f - normY) * mapRect.height;

            // Rotate around marker center according to player rotation
            float angle = transform.eulerAngles.y;
            Vector2 pivot = new Vector2(screenPx, screenPy);

            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.DrawTexture(new Rect(screenPx - 7, screenPy - 7, 14, 14), playerIcon);
            GUIUtility.RotateAroundPivot(-angle, pivot);
        }

        string GetFloorName(int floor)
        {
            switch (floor)
            {
                case 0: return "FLOOR 1: GROUND (Lobby & Facilities)";
                case 1: return "FLOOR 2: SCHOOL (Classrooms & Labs)";
                case 2: return "FLOOR 3: HOTEL (Rooms & Suites)";
                case 3: return "FLOOR 4: OFFICES (Headquarters)";
                default: return $"FLOOR {floor + 1}";
            }
        }
    }
}
