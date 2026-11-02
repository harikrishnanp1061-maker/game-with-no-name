using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The security control room's console - the team's eyes for the whole level. One player sits here (E) while the
    /// others go out with walkie-talkies; the console has its own base-station radio ([B] to talk). From here:
    ///   - CCTV: every camera's feed (infrared, grainy). The building's cameras work from the start (battery); the
    ///     outdoor ones need the generator.
    ///   - The surveillance map: the campus, and the administration building floor by floor, with every walkie-talkie
    ///     on it, and any monster a camera can see right now (they fade a few seconds after they leave the picture).
    ///   - Distractions: ring a phone or throw on a bank of lights somewhere you are NOT going - a Nailed comes to look,
    ///     outdoors the giant turns its head. Car alarms are set off by hand, out in the car park.
    ///   - Markers: "go here" / "danger" on the map, shown on every walkie holder's screen.
    ///   - The gates: the service gate's magnetic lock is released only from here (after the main gate fails).
    /// While seated the body sits defenceless at the desk - the room's own camera shows the door.
    /// </summary>
    public class SurveillanceConsole : MonoBehaviour, IPartyInteractable
    {
        public static SurveillanceConsole Instance { get; private set; }
        /// <summary>This machine's player is sitting at the console.</summary>
        public static bool LocalSeated { get; private set; }

        public ServiceGate serviceGate;
        [Header("The video wall")]
        [Tooltip("The wall's monitors, left to right, top to bottom; each shows one camera, live.")]
        public Renderer[] wallScreens;
        public TextMesh[] wallLabels;
        [Tooltip("The big screen in the middle of the wall: the live surveillance map.")]
        public Renderer mapScreen;
        public Vector2 mapSize = new Vector2(3.2f, 2f);
        [Tooltip("Small monitors on the desk (the zoomed camera and two cycling ones).")]
        public Renderer[] deskScreens;
        [Tooltip("Where the view sits while at the desk, looking at the wall.")]
        public Transform viewPoint;
        public Bounds room = new Bounds(new Vector3(-30.5f, 6f, -18.5f), new Vector3(11f, 4.5f, 11f));
        [Tooltip("Camera renders per frame for the wall while someone is in the room.")]
        public int renderBudget = 3;
        public Rect adminWorld = Rect.MinMaxRect(-36.5f, -24.5f, 36.5f, 24.5f);
        public Transform adminRoot;

        int seatedPlayer = -1;
        float seatedAt;
        int selected;
        int mapTab;               // 0 campus, 1 admin ground, 2 admin upper
        int markerMode = -1;      // -1 none, 0 go here, 1 danger
        RenderTexture feed;
        RenderTexture[] screenRT, deskRT;
        Material[] screenMat;
        int[] screenCam;
        int renderNext; float nextFeed, nextSpot, nextLabels;
        bool zoom;                 // seated: false = looking at the wall in 3D, true = one camera full screen with the controls
        int hover = -1;            // the wall screen under the mouse (-2: the map)
        Camera viewCam; Camera playerCam;
        Texture2D staticTex; Material staticMat;
        Transform dotRoot; readonly List<Transform> dots = new List<Transform>(); Material dotMat;
        Texture2D plan0, plan1, white;
        string message; float messageUntil;
        readonly Dictionary<Object, KeyValuePair<Vector3, float>> seen = new Dictionary<Object, KeyValuePair<Vector3, float>>();
        GUIStyle title, small, label, btn, mono, big;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (LocalSeated) Stand(); }

        void Start()
        {
            white = Texture2D.whiteTexture;
            feed = new RenderTexture(960, 540, 16) { name = "CCTV_Feed" };
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            // static for a dead feed
            staticTex = new Texture2D(128, 72, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var sp = new Color[128 * 72]; var r = new System.Random(5);
            for (int i = 0; i < sp.Length; i++) { float v = (float)r.NextDouble() * 0.35f; sp[i] = new Color(v, v, v); }
            staticTex.SetPixels(sp); staticTex.Apply();
            staticMat = new Material(unlit); staticMat.SetTexture("_BaseMap", staticTex);
            int n = wallScreens != null ? wallScreens.Length : 0;
            screenRT = new RenderTexture[n]; screenMat = new Material[n]; screenCam = new int[n];
            for (int i = 0; i < n; i++)
            {
                screenRT[i] = new RenderTexture(384, 216, 16) { name = "Wall_" + i };
                screenMat[i] = new Material(unlit); screenMat[i].SetTexture("_BaseMap", screenRT[i]);
                screenMat[i].SetColor("_BaseColor", new Color(0.78f, 0.95f, 0.82f));
                screenCam[i] = i;
            }
            int dn = deskScreens != null ? deskScreens.Length : 0;
            deskRT = new RenderTexture[dn];
            for (int i = 0; i < dn; i++)
            {
                deskRT[i] = new RenderTexture(320, 180, 16);
                var m = new Material(unlit); m.SetTexture("_BaseMap", deskRT[i]); m.SetColor("_BaseColor", new Color(0.78f, 0.95f, 0.82f));
                deskScreens[i].sharedMaterial = m;
            }
            dotMat = new Material(unlit);
            if (adminRoot != null) { plan0 = Plan(1.2f); plan1 = Plan(5.4f); }
            // the camera list: the building's cameras first, then the grounds', each group by name
            CctvCamera.All.Sort((a, b) => a.outdoor != b.outdoor ? a.outdoor.CompareTo(b.outdoor) : string.CompareOrdinal(a.label, b.label));
        }

        // ------------------------------------------------------------------ sitting down
        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (LocalSeated) return null;
            return "[E] Sit at the surveillance console";
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (LocalSeated) return;
            LocalSeated = true; seatedPlayer = player; seatedAt = Time.time;
            Puzzles.PuzzlePanel.ExternalModal = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            SfxBank.Flat(CampusSynth.Relay, 0.5f, 1.3f);
            if (CctvCamera.All.Count > 0) selected = Mathf.Clamp(selected, 0, CctvCamera.All.Count - 1);
            zoom = false; hover = -1;
            // the view moves to the desk, looking at the wall: the screens themselves are the interface
            var party = PlayerParty.Instance;
            playerCam = party != null ? party.playerCamera : null;
            if (viewPoint != null)
            {
                if (viewCam == null)
                {
                    var go = new GameObject("Console_View"); go.transform.SetParent(transform, false);
                    viewCam = go.AddComponent<Camera>(); viewCam.fieldOfView = 62f; viewCam.nearClipPlane = 0.05f; viewCam.farClipPlane = 60f;
                    var d = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); d.renderPostProcessing = true;
                }
                viewCam.transform.SetPositionAndRotation(viewPoint.position, viewPoint.rotation);
                viewCam.enabled = true;
                if (playerCam != null) playerCam.enabled = false;
            }
        }

        void Stand()
        {
            LocalSeated = false; seatedPlayer = -1; markerMode = -1; zoom = false;
            if (viewCam != null) viewCam.enabled = false;
            if (playerCam != null) playerCam.enabled = true;
            Puzzles.PuzzlePanel.ExternalModal = false;
            CampusLightCuller.Focus = null;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        bool mapSet;
        void Update()
        {
            var party = PlayerParty.Instance;
            if (!mapSet && mapScreen != null && CampusMapHUD.Blueprint != null)
            {
                var mm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mm.SetTexture("_BaseMap", CampusMapHUD.Blueprint); mm.SetColor("_BaseColor", new Color(0.8f, 1f, 0.85f));
                mapScreen.sharedMaterial = mm; mapSet = true;
            }
            if (LocalSeated)
            {
                bool gone = party == null || party.CurrentIndex != seatedPlayer || party.Current == null || party.Current.IsDead || party.Spectating;
                if (gone) { Stand(); return; }
                if (Time.time > seatedAt + 0.4f && (InputWrap.Down(KeyCode.Escape) || InputWrap.Down(KeyCode.E)))
                {
                    if (zoom) zoom = false; else { Stand(); return; }
                }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (zoom)
                {
                    var cam = Selected;
                    if (cam != null)
                    {
                        CampusLightCuller.Focus = cam.view != null ? cam.view.transform.position : (Vector3?)null;
                        if (Time.time > nextFeed && cam.Live) { nextFeed = Time.time + 1f / 20f; cam.RenderTo(feed); }
                    }
                    if (InputWrap.Down(KeyCode.Tab)) mapTab = (mapTab + 1) % 3;
                    if (InputWrap.Down(KeyCode.LeftArrow) || InputWrap.Down(KeyCode.A)) { selected = (selected + CctvCamera.All.Count - 1) % CctvCamera.All.Count; nextFeed = 0f; }
                    if (InputWrap.Down(KeyCode.RightArrow) || InputWrap.Down(KeyCode.D)) { selected = (selected + 1) % CctvCamera.All.Count; nextFeed = 0f; }
                }
                else WallInput();
            }
            Wall(party);
            if (Time.time > nextSpot) { nextSpot = Time.time + 0.25f; Spot(); Dots(); }
        }

        bool Near(PlayerParty party) { return LocalSeated || (party != null && party.Current != null && room.Contains(party.Current.transform.position)); }

        /// <summary>The video wall: every screen a live camera (a few renders a frame, round the wall), the outdoor
        /// ones changing every few seconds on the last two screens, labels and timestamps, static on a dead feed.</summary>
        void Wall(PlayerParty party)
        {
            if (wallScreens == null || CctvCamera.All.Count == 0) return;
            int n = wallScreens.Length, cams = CctvCamera.All.Count;
            // which camera each screen shows: the first screens fixed, the last two cycling through the rest
            int fixedCount = Mathf.Min(n - 2, cams);
            for (int i = 0; i < n; i++)
            {
                if (i < fixedCount) screenCam[i] = i;
                else
                {
                    int rest = cams - fixedCount;
                    screenCam[i] = rest > 0 ? fixedCount + ((int)(Time.time / 7f) * 2 + (i - fixedCount)) % rest : i % cams;
                }
            }
            if (!Near(party)) return;
            for (int k = 0; k < renderBudget; k++)
            {
                int i = renderNext++ % (n + (deskRT != null ? deskRT.Length : 0));
                if (i < n)
                {
                    var c = CctvCamera.All[screenCam[i]];
                    wallScreens[i].sharedMaterial = c.Live ? screenMat[i] : staticMat;
                    if (c.Live) c.RenderTo(screenRT[i]);
                }
                else
                {
                    int d = i - n;
                    var c = CctvCamera.All[d == 0 ? selected : (selected + d * 5 + (int)(Time.time / 5f)) % cams];
                    if (c.Live) c.RenderTo(deskRT[d]);
                }
            }
            staticTex.wrapMode = TextureWrapMode.Repeat;
            staticMat.SetTextureOffset("_BaseMap", new Vector2(Random.value, Random.value));
            if (Time.time > nextLabels && wallLabels != null)
            {
                nextLabels = Time.time + 1f;
                string ts = new System.DateTime(1999, 10, 31, 3, 7, 0).AddSeconds(Time.timeSinceLevelLoad).ToString("HH:mm:ss");
                for (int i = 0; i < wallLabels.Length && i < n; i++)
                {
                    if (wallLabels[i] == null) continue;
                    var c = CctvCamera.All[screenCam[i]];
                    wallLabels[i].text = (screenCam[i] + 1).ToString("00") + " " + c.label.ToUpper() + (c.Live ? "   " + ts : "   NO SIGNAL");
                }
            }
        }

        /// <summary>Seated, looking at the wall: hover a screen to see what it is, click it to zoom in; click the map for
        /// the map desk.</summary>
        void WallInput()
        {
            hover = -1;
            if (viewCam == null) return;
            Vector2 mp = InputWrap.MousePosition;
            var ray = viewCam.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
            RaycastHit h;
            if (Physics.Raycast(ray, out h, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (mapScreen != null && h.collider.transform == mapScreen.transform) hover = -2;
                else if (wallScreens != null) for (int i = 0; i < wallScreens.Length; i++) if (h.collider.transform == wallScreens[i].transform) hover = i;
            }
            if (hover != -1 && InputWrap.Down(KeyCode.Mouse0))
            {
                if (hover >= 0) { selected = screenCam[hover]; mapTab = CctvCamera.All[selected].outdoor ? 0 : (CctvCamera.All[selected].Upper ? 2 : 1); }
                zoom = true; nextFeed = 0f;
                SfxBank.Flat(CampusSynth.Relay, 0.4f, 1.6f);
            }
        }

        /// <summary>The map screen's live dots: walkie holders, monsters a camera sees, the markers.</summary>
        void Dots()
        {
            if (mapScreen == null) return;
            if (dotRoot == null)
            {
                // beside the map quad (not under it: it is scaled), in its plane, just in front of it
                dotRoot = new GameObject("Map_Dots").transform; dotRoot.SetParent(mapScreen.transform.parent, false);
                dotRoot.SetPositionAndRotation(mapScreen.transform.position, mapScreen.transform.rotation);
            }
            var world = CampusMapHUD.WorldRect;
            var list = new List<KeyValuePair<Vector3, Color>>();
            var party = PlayerParty.Instance;
            if (party != null)
                for (int i = 0; i < party.players.Length; i++)
                    if (party.players[i] != null && !party.players[i].IsDead && Walkie.Has(i)) list.Add(new KeyValuePair<Vector3, Color>(party.players[i].transform.position, PlayerParty.Colors[i]));
            foreach (var kv in seen) if (kv.Key != null && Time.time - kv.Value.Value < 6f) list.Add(new KeyValuePair<Vector3, Color>(kv.Value.Key, kv.Key is CampusGiant ? new Color(1f, 0f, 0f) : new Color(1f, 0.3f, 0.1f)));
            foreach (var m in Walkie.Markers) list.Add(new KeyValuePair<Vector3, Color>(m.pos, Walkie.MarkerColors[Mathf.Clamp(m.kind, 0, 2)]));
            while (dots.Count < list.Count)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(d.GetComponent<Collider>());
                d.transform.SetParent(dotRoot, false); d.GetComponent<Renderer>().sharedMaterial = new Material(dotMat);
                dots.Add(d.transform);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                bool on = i < list.Count;
                dots[i].gameObject.SetActive(on);
                if (!on) continue;
                Vector3 w = list[i].Key;
                float u = (w.x - world.xMin) / world.width - 0.5f, v = (w.z - world.yMin) / world.height - 0.5f;
                dots[i].localPosition = new Vector3(u * mapSize.x, v * mapSize.y, -0.012f);   // quads face their local -z
                dots[i].localRotation = Quaternion.identity;
                dots[i].localScale = new Vector3(0.07f, 0.07f, 1f);
                dots[i].GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", list[i].Value * (Mathf.Repeat(Time.time * 2f, 1f) < 0.7f ? 1.6f : 0.6f));
            }
        }

        CctvCamera Selected { get { return selected >= 0 && selected < CctvCamera.All.Count ? CctvCamera.All[selected] : null; } }

        /// <summary>What the cameras can see right now (monsters only; the walkies show themselves).</summary>
        void Spot()
        {
            foreach (var m in NailedMonster.All)
            {
                if (m == null) continue;
                foreach (var c in CctvCamera.All)
                    if (c.Sees(m.transform.position, 1f, m.transform)) { seen[m] = new KeyValuePair<Vector3, float>(m.transform.position, Time.time); break; }
            }
            var g = CampusGiant.Instance;
            if (g != null)
                foreach (var c in CctvCamera.All)
                    if (c.outdoor && c.Live)
                    {
                        // it is 48 m tall: a camera sees it from far off if any of it is in the picture
                        Vector3 vp = c.view != null ? c.view.WorldToViewportPoint(g.transform.position + Vector3.up * 20f) : Vector3.back;
                        if (vp.z > 0f && vp.z < 260f && vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.5f && vp.y < 1.5f) { seen[g] = new KeyValuePair<Vector3, float>(g.transform.position, Time.time); break; }
                    }
        }

        // ------------------------------------------------------------------ the floor plans (from the building's own walls)
        Texture2D Plan(float y)
        {
            const float ppm = 4f;
            int w = Mathf.CeilToInt(adminWorld.width * ppm), h = Mathf.CeilToInt(adminWorld.height * ppm);
            var px = new Color32[w * h];
            var floorC = new Color32(28, 40, 36, 255); var wallC = new Color32(120, 190, 150, 255);
            for (int i = 0; i < px.Length; i++) px[i] = floorC;
            foreach (var col in adminRoot.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue;
                var b = col.bounds;
                if (b.min.y > y || b.max.y < y) continue;
                if (b.size.x > 20f && b.size.z > 20f) continue;   // a slab, not a wall
                int x0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - adminWorld.xMin) * ppm), 0, w - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x - adminWorld.xMin) * ppm), 0, w - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - adminWorld.yMin) * ppm), 0, h - 1), y1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z - adminWorld.yMin) * ppm), 0, h - 1);
                bool thin = b.size.x < 0.8f || b.size.z < 0.8f;
                var c = thin ? wallC : new Color32(60, 90, 75, 255);
                for (int yy = y0; yy <= y1; yy++) for (int xx = x0; xx <= x1; xx++) px[yy * w + xx] = c;
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px); t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ the screen
        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.55f, 1f, 0.7f);
            small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            small.normal.textColor = new Color(0.6f, 0.85f, 0.7f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            label.normal.textColor = new Color(0.8f, 0.95f, 0.85f);
            mono = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            mono.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(0.9f, 0.3f, 0.25f);
            btn = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = true };
        }

        void Box(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, white); GUI.color = Color.white; }
        void Say(string s) { message = s; messageUntil = Time.time + 3f; }

        void OnGUI()
        {
            UiStyle.Use();
            if (!LocalSeated) return;
            Styles();
            if (!zoom) { WallOverlay(); return; }
            GUI.depth = -20;
            var party = PlayerParty.Instance;
            float W = Screen.width, H = Screen.height;
            Box(new Rect(0, 0, W, H), new Color(0.01f, 0.03f, 0.02f, 1f));
            GUI.Label(new Rect(16, 8, 600, 26), "SECURITY CONTROL - HOLLOWAY COLLEGE", title);
            string grid = CampusPower.GridOn ? "CAMPUS GRID: ON" : "CAMPUS GRID: OFF (generator) - outdoor cameras, payphones and floodlights dead";
            GUI.Label(new Rect(W * 0.42f, 12, W * 0.58f - 16, 20), grid, small);

            // ---- left: the feed
            float fx = 16, fy = 40, fw = W * 0.56f, fh = fw * 9f / 16f;
            if (fh > H * 0.55f) { fh = H * 0.55f; fw = fh * 16f / 9f; }
            var feedRect = new Rect(fx, fy, fw, fh);
            Box(feedRect, Color.black);
            var cam = Selected;
            if (cam != null && cam.Live)
            {
                GUI.color = new Color(0.78f, 1f, 0.82f);   // monochrome-ish infrared
                GUI.DrawTexture(feedRect, feed, ScaleMode.StretchToFill, false);
                GUI.color = Color.white;
                for (float y = feedRect.y; y < feedRect.yMax; y += 3f) Box(new Rect(feedRect.x, y, feedRect.width, 1f), new Color(0f, 0f, 0f, 0.18f));
                float roll = Mathf.Repeat(Time.time * 60f, feedRect.height);
                Box(new Rect(feedRect.x, feedRect.y + roll, feedRect.width, 6f), new Color(1f, 1f, 1f, 0.04f));
            }
            else
            {
                for (int i = 0; i < 40; i++) Box(new Rect(feedRect.x + Random.value * fw, feedRect.y + Random.value * fh, Random.value * 60f, 2f), new Color(1, 1, 1, 0.15f));
                GUI.Label(feedRect, cam == null ? "NO CAMERAS" : "NO SIGNAL - CAMPUS GRID OFF", big);
            }
            if (cam != null)
            {
                GUI.Label(new Rect(feedRect.x + 10, feedRect.y + 6, 500, 20), (selected + 1).ToString("00") + "  " + cam.label.ToUpper(), mono);
                bool rec = Mathf.Repeat(Time.time, 1f) < 0.6f;
                System.DateTime t = new System.DateTime(1999, 10, 31, 3, 7, 0).AddSeconds(Time.timeSinceLevelLoad);
                GUI.Label(new Rect(feedRect.xMax - 210, feedRect.y + 6, 200, 20), (rec ? "REC " : "    ") + t.ToString("dd/MM/yyyy HH:mm:ss"), mono);
                foreach (var kv in seen)
                    if (Time.time - kv.Value.Value < 0.6f && kv.Key != null && cam.Live)
                    {
                        Vector3 p = kv.Key is CampusGiant ? kv.Value.Key + Vector3.up * 20f : kv.Value.Key + Vector3.up * 1.2f;
                        if (!(kv.Key is CampusGiant) && !cam.Sees(kv.Value.Key)) continue;
                        Vector3 vp = cam.view.WorldToViewportPoint(p);
                        if (vp.z <= 0f) continue;
                        var mr = new Rect(feedRect.x + vp.x * fw - 30, feedRect.y + (1f - vp.y) * fh - 30, 60, 60);
                        GUI.color = new Color(1f, 0.2f, 0.15f, 0.8f);
                        GUI.Box(mr, ""); GUI.color = Color.white;
                        GUI.Label(new Rect(mr.x - 20, mr.yMax, 100, 18), kv.Key is CampusGiant ? "GIANT" : "MOTION", small);
                    }
            }

            // ---- camera list
            float ly = feedRect.yMax + 8;
            float bw = (fw - 7 * 6) / 7f;
            for (int i = 0; i < CctvCamera.All.Count; i++)
            {
                var c = CctvCamera.All[i];
                var r = new Rect(fx + (i % 7) * (bw + 6), ly + (i / 7) * 34, bw, 30);
                GUI.backgroundColor = i == selected ? new Color(0.3f, 0.9f, 0.5f) : (c.Live ? new Color(0.35f, 0.5f, 0.42f) : new Color(0.3f, 0.2f, 0.2f));
                if (GUI.Button(r, (i + 1).ToString("00") + " " + c.label, btn)) { selected = i; nextFeed = 0f; if (!c.outdoor) mapTab = c.Upper ? 2 : 1; }
            }
            GUI.backgroundColor = Color.white;
            ly += Mathf.CeilToInt(CctvCamera.All.Count / 7f) * 34 + 6;

            // ---- what can be done from this camera's picture
            if (cam != null)
            {
                float ax = fx;
                System.Action next = () => { ax += 206; if (ax + 200 > fx + fw) { ax = fx; ly += 40; } };
                foreach (var ph in CampusPhone.All)
                {
                    if (!cam.Sees(ph.transform.position - Vector3.up * 0.9f, 1.2f)) continue;
                    GUI.enabled = ph.Ready && !ph.Ringing;
                    string t = ph.Ringing ? "RINGING..." : ph.Ready ? "RING " + ph.label.ToUpper() : (ph.outdoor && !CampusPower.GridOn ? ph.label + " - dead" : ph.label + " (" + Mathf.CeilToInt(ph.CooldownLeft) + " s)");
                    if (GUI.Button(new Rect(ax, ly, 200, 34), t, btn)) { ph.Dial(seatedPlayer); Say("Dialling " + ph.label + "..."); }
                    next();
                }
                foreach (var z in LightZone.All)
                {
                    Vector3 cp = cam.transform.position;
                    bool inside = !z.outdoor && !cam.outdoor && z.area.Contains(new Vector2(cp.x, cp.z)) && (cp.y > CampusAdmin_H) == (z.floorY > 1f);
                    bool inView = z.outdoor && cam.outdoor && cam.Sees(z.Centre - Vector3.up * 1.2f, 2.2f);
                    if (!inside && !inView) continue;
                    GUI.enabled = z.Ready;
                    if (GUI.Button(new Rect(ax, ly, 200, 34), (z.On ? "LIGHTS OFF: " : "LIGHTS ON: ") + z.label.ToUpper(), btn)) z.Toggle(seatedPlayer);
                    next();
                }
                GUI.enabled = true;
            }
            ly += 42;

            // ---- the service gate
            if (serviceGate != null) { GatePanel(new Rect(fx, ly, Mathf.Min(fw, 540), 86)); ly += 94; }
            GUI.Label(new Rect(fx, H - 26, fw, 20), "[Esc] back to the wall    [A]/[D] previous/next camera    hold [B] talk    [Tab] map floor    click a camera on the map to watch it", small);
            if (message != null && Time.time < messageUntil) GUI.Label(new Rect(fx, H - 48, fw, 20), message, label);

            // ---- right: the surveillance map
            float mx = fx + fw + 16, mw = W - mx - 16;
            GUI.Label(new Rect(mx, 40, 200, 22), "SURVEILLANCE MAP", title);
            string[] tabs = { "CAMPUS", "ADMIN - GROUND", "ADMIN - UPPER" };
            for (int i = 0; i < 3; i++)
            {
                GUI.backgroundColor = i == mapTab ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.3f, 0.42f, 0.36f);
                if (GUI.Button(new Rect(mx + i * (mw / 3f), 66, mw / 3f - 4, 26), tabs[i], btn)) mapTab = i;
            }
            GUI.backgroundColor = Color.white;
            Rect world = mapTab == 0 ? CampusMapHUD.WorldRect : adminWorld;
            Texture tex = mapTab == 0 ? (Texture)CampusMapHUD.Blueprint : mapTab == 1 ? plan0 : plan1;
            float aspect = world.width / world.height;
            float mh = Mathf.Min(H - 220, mw / aspect);
            float mww = mh * aspect;
            var map = new Rect(mx + (mw - mww) * 0.5f, 98, mww, mh);
            Box(new Rect(map.x - 2, map.y - 2, map.width + 4, map.height + 4), new Color(0.2f, 0.4f, 0.3f));
            if (tex != null) GUI.DrawTexture(map, tex, ScaleMode.StretchToFill, false); else Box(map, new Color(0.05f, 0.08f, 0.06f));
            if (mapTab == 0) { var a = W2M(new Vector3(adminWorld.center.x, 0, adminWorld.center.y), map, world); GUI.Label(new Rect(a.x - 30, a.y - 8, 60, 16), "ADMIN", small); }
            System.Func<Vector3, bool> onTab = p => mapTab == 0 ? !adminWorld.Contains(new Vector2(p.x, p.z)) : adminWorld.Contains(new Vector2(p.x, p.z)) && ((mapTab == 1) == (p.y < 3.5f));
            var e = Event.current;

            // light zones: an outline you can click
            foreach (var z in LightZone.All)
            {
                Vector3 c = z.Centre;
                if (z.outdoor ? mapTab != 0 : !onTab(c)) continue;
                var a = W2M(new Vector3(z.area.xMin, 0, z.area.yMax), map, world); var b = W2M(new Vector3(z.area.xMax, 0, z.area.yMin), map, world);
                var zr = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                Outline(zr, z.On ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.5f, 0.5f, 0.4f, 0.6f));
                if (z.On) Box(zr, new Color(1f, 0.85f, 0.3f, 0.08f));
                GUI.Label(new Rect(zr.x + 2, zr.y + 1, 160, 16), (z.On ? "* " : "") + z.label, small);
                if (markerMode < 0 && e.type == EventType.MouseDown && e.button == 1 && zr.Contains(e.mousePosition)) { e.Use(); if (z.Ready) z.Toggle(seatedPlayer); else Say(z.label + ": not ready"); }
            }
            // cameras
            for (int i = 0; i < CctvCamera.All.Count; i++)
            {
                var c = CctvCamera.All[i];
                if (c.outdoor ? mapTab != 0 : !onTab(c.transform.position)) continue;
                var p = W2M(c.transform.position, map, world);
                var r = new Rect(p.x - 7, p.y - 7, 14, 14);
                Box(r, i == selected ? new Color(0.3f, 1f, 0.5f) : c.Live ? new Color(0.6f, 0.8f, 1f) : new Color(0.4f, 0.3f, 0.3f));
                GUI.Label(new Rect(p.x + 8, p.y - 8, 40, 16), (i + 1).ToString("00"), small);
                if (markerMode < 0 && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { e.Use(); selected = i; nextFeed = 0f; }
            }
            // phones
            foreach (var ph in CampusPhone.All)
            {
                if (ph.outdoor ? mapTab != 0 : !onTab(ph.transform.position)) continue;
                var p = W2M(ph.transform.position, map, world);
                var r = new Rect(p.x - 6, p.y - 6, 12, 12);
                Box(r, ph.Ringing ? (Mathf.Repeat(Time.time, 0.4f) < 0.2f ? Color.yellow : Color.white) : ph.Ready ? new Color(1f, 0.75f, 0.2f) : new Color(0.4f, 0.35f, 0.25f));
                GUI.Label(new Rect(p.x - 2, p.y - 9, 20, 16), "T", small);
                if (markerMode < 0 && e.type == EventType.MouseDown && e.button == 1 && r.Contains(e.mousePosition)) { e.Use(); if (ph.Ready) { ph.Dial(seatedPlayer); Say("Dialling " + ph.label + "..."); } }
            }
            // the service gate
            if (serviceGate != null && mapTab == 0)
            {
                var p = W2M(serviceGate.Centre, map, world);
                Box(new Rect(p.x - 6, p.y - 3, 12, 6), serviceGate.IsOpen ? Color.green : new Color(1f, 0.3f, 0.2f));
                GUI.Label(new Rect(p.x - 40, p.y + 4, 100, 16), "SERVICE GATE", small);
            }
            // walkie-talkies
            if (party != null)
                for (int i = 0; i < party.players.Length; i++)
                {
                    var b = party.players[i];
                    if (b == null || b.IsDead || !Walkie.Has(i)) continue;
                    Vector3 bp = b.transform.position;
                    if (mapTab != 0 && !onTab(bp)) continue;
                    var p = W2M(bp, map, world);
                    var col = PlayerParty.Colors[i];
                    Box(new Rect(p.x - 5, p.y - 5, 10, 10), col);
                    var lc = small.normal.textColor; small.normal.textColor = col;
                    GUI.Label(new Rect(p.x + 7, p.y - 8, 60, 16), "P" + (i + 1) + (Walkie.OnAir[i] ? " ((o))" : ""), small);
                    small.normal.textColor = lc;
                }
            // monsters the cameras see
            foreach (var kv in seen)
            {
                float age = Time.time - kv.Value.Value;
                if (age > 8f || kv.Key == null) continue;
                Vector3 mp = kv.Value.Key;
                bool giant = kv.Key is CampusGiant;
                if (giant ? mapTab != 0 : (mapTab != 0 && !onTab(mp))) continue;
                var p = W2M(mp, map, world);
                float s = giant ? 18f : 10f;
                Box(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), new Color(1f, 0.1f, 0.05f, 1f - age / 8f));
                GUI.Label(new Rect(p.x + s * 0.5f + 2, p.y - 8, 120, 16), giant ? "GIANT" : (age < 0.5f ? "NAILED" : "NAILED " + age.ToString("F0") + "s ago"), small);
            }
            // markers
            foreach (var m in Walkie.Markers)
            {
                if (mapTab != 0 && !onTab(m.pos)) continue;
                var p = W2M(m.pos, map, world);
                var mc = Walkie.MarkerColors[Mathf.Clamp(m.kind, 0, 2)];
                Outline(new Rect(p.x - 7, p.y - 7, 14, 14), mc);
                GUI.Label(new Rect(p.x + 8, p.y + 2, 120, 16), Walkie.MarkerNames[Mathf.Clamp(m.kind, 0, 2)], small);
            }
            // placing a marker
            if (markerMode >= 0 && e.type == EventType.MouseDown && map.Contains(e.mousePosition))
            {
                e.Use();
                Vector3 wp = M2W(e.mousePosition, map, world);
                wp.y = mapTab == 2 ? CampusAdmin_H : 0.2f;
                Walkie.PlaceMarker(wp, markerMode, 4);
                Say("Marker sent to every walkie");
                markerMode = -1;
            }
            float by = map.yMax + 8;
            GUI.backgroundColor = markerMode == 0 ? Color.green : Color.white;
            if (GUI.Button(new Rect(mx, by, 140, 30), "MARK: GO HERE", btn)) markerMode = markerMode == 0 ? -1 : 0;
            GUI.backgroundColor = markerMode == 1 ? Color.red : Color.white;
            if (GUI.Button(new Rect(mx + 146, by, 140, 30), "MARK: DANGER", btn)) markerMode = markerMode == 1 ? -1 : 1;
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(mx + 292, by, 110, 30), "CLEAR MARKS", btn)) Walkie.Markers.RemoveAll(m => m.from == 4);
            GUI.Label(new Rect(mx, by + 34, mw, 64), (markerMode >= 0 ? "Click the map to place the marker. " : "") +
                "Left-click a camera to watch it. Right-click a phone (T) to ring it, or a light zone to switch it. Only players carrying a walkie-talkie show on this map; red = a monster a camera can see.", label);
        }

        const float CampusAdmin_H = 4.4f;

        /// <summary>Seated, looking at the wall: only a frame of help and the gate controls; the screens do the rest.</summary>
        void WallOverlay()
        {
            float W = Screen.width, H = Screen.height;
            var s = UiStyle.Bar(new Rect(20, H - 64, W - 40, 46));
            GUI.Label(new Rect(36, H - 58, W - 72, 36), "CLICK A SCREEN TO ZOOM IN   ·   CLICK THE MAP FOR THE MAP DESK   ·   HOLD [B] TO TALK   ·   [ESC] STAND UP", UiStyle.Text(15, TextAnchor.MiddleCenter));
            if (hover != -1)
            {
                string t = hover == -2 ? "SURVEILLANCE MAP" : (screenCam[hover] + 1).ToString("00") + "  " + CctvCamera.All[screenCam[hover]].label.ToUpper();
                Vector2 mp = Event.current.mousePosition;
                UiStyle.Slab(new Rect(mp.x + 16, mp.y + 10, 260, 34), -1.5f);
                GUI.Label(new Rect(mp.x + 16, mp.y + 10, 260, 34), t, UiStyle.Text(14, TextAnchor.MiddleCenter));
            }
            if (serviceGate != null) GatePanel(new Rect(20, 20, 520, 86));
        }

        void GatePanel(Rect sr)
        {
            var obj = CampusObjective.Instance;
            bool armed = obj != null && obj.gate != null && obj.gate.Jammed && CampusPower.GridOn;
            UiStyle.Slab(sr, -0.6f);
            string st = serviceGate.IsOpen ? "SERVICE GATE: OPEN - " + Mathf.CeilToInt(serviceGate.OpenLeft) + " S" + (serviceGate.Timed ? "  (SIREN)" : "")
                      : !CampusPower.GridOn ? "SERVICE GATE: NO POWER"
                      : !armed ? "SERVICE GATE: LOCKED OUT - MAIN GATE SYSTEM ACTIVE"
                      : serviceGate.padlocked ? "SERVICE GATE: MAGNET READY - PADLOCK STILL ON"
                      : "SERVICE GATE: LOCKED - RELEASE WHEN YOUR TEAM IS THERE";
            GUI.Label(new Rect(sr.x + 14, sr.y + 6, sr.width - 28, 24), st, UiStyle.Text(14, TextAnchor.MiddleLeft));
            GUI.enabled = armed && !serviceGate.IsOpen;
            if (UiStyle.Button(new Rect(sr.x + 12, sr.y + 38, 220, 38), "RELEASE " + serviceGate.holdSeconds.ToString("F0") + " S", 13)) serviceGate.Release(seatedPlayer, false);
            if (UiStyle.Button(new Rect(sr.x + 244, sr.y + 38, 262, 38), "TIMED " + serviceGate.timedSeconds.ToString("F0") + " S + SIREN", 13)) serviceGate.Release(seatedPlayer, true);
            GUI.enabled = true;
        }

        static Vector2 W2M(Vector3 w, Rect map, Rect world)
        {
            return new Vector2(map.x + (w.x - world.xMin) / world.width * map.width, map.yMax - (w.z - world.yMin) / world.height * map.height);
        }
        static Vector3 M2W(Vector2 m, Rect map, Rect world)
        {
            return new Vector3(world.xMin + (m.x - map.x) / map.width * world.width, 0f, world.yMin + (map.yMax - m.y) / map.height * world.height);
        }
        void Outline(Rect r, Color c)
        {
            Box(new Rect(r.x, r.y, r.width, 1.5f), c); Box(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), c);
            Box(new Rect(r.x, r.y, 1.5f, r.height), c); Box(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), c);
        }
    }
}
