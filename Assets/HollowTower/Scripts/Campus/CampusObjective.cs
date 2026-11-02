using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The final level's goals, worked out from the state of the world (so every machine agrees without sending
    /// anything): find the main entrance key in the administration building, unlock its main doors, start the campus
    /// generator in the west gardens' maintenance yard, open the main gate from the security booth, and get out through
    /// it. Shows the current goal along the top of the screen and the ending once someone walks out of the gate.
    /// </summary>
    public class CampusObjective : MonoBehaviour
    {
        public enum Stage { FindKey, UnlockDoor, RestorePower, OpenGate, FindServiceGate, Escape, Escaped }

        public static CampusObjective Instance { get; private set; }

        public PartyItem key;
        public CampusKeyDoor mainDoor;
        public CampusSwitch generator;
        public CampusSwitch gateControl;
        public CampusGate gate;
        public ServiceGate serviceGate;
        public Puzzles.ClockPuzzle clock;
        public Puzzles.StatuePuzzle statues;
        [Tooltip("The control room's console (the compass points there when the service gate needs releasing).")]
        public Transform controlRoom;
        [Tooltip("Beyond the gate: any living player standing in here gets the party out.")]
        public Bounds escapeZone = new Bounds(new Vector3(0f, 2f, 252f), new Vector3(30f, 8f, 18f));
        public Bounds serviceEscapeZone = new Bounds(new Vector3(152f, 2f, -62f), new Vector3(16f, 8f, 14f));

        [Header("Goal text")]
        public string findKey = "Find the main entrance key - the head administrator's office is upstairs";
        public string unlockDoor = "Unlock the main entrance doors (reception lobby, north side)";
        public string restorePower = "The main gate has no power - start the generator in the west gardens' maintenance yard";
        public string openGate = "Power is back - open the main gate from the security booth (north-east, by the car park)";
        public string escape = "The gate is open - get out of the campus!";
        public string findServiceGate = "The main gate is welded shut from outside. Find the old service gate in the far SOUTH-EAST corner - its lock is released from the security control room (admin building, upper floor, west)";
        public string releaseServiceGate = "Service gate found - someone at the control room console must release its lock while the others wait at the gate";
        public string escapeService = "The service gate is OPEN - get through it!";
        public string findCrank = "The generator's crank handle is locked in the clock tower's drive box (arch passage) - set the tower clock to the night shift's change-over time (security control room board)";
        public string findGateKey = "The service gate is padlocked too - the groundskeeper's key is 'with the watchers': turn the four statues round the quad fountain";
        public string controlRoomHint = "Security control room: admin building, upper floor, west wing - cameras, walkie-talkies, and the gate locks";

        public Stage Current { get; private set; }
        Stage shown = (Stage)(-1);
        float escapedAt;
        GUIStyle bannerStyle, titleStyle, bodyStyle, hintStyle;

        /// <summary>Where P4's compass points on the campus: the next thing to reach (CompassModel).</summary>
        public Vector3? CompassTarget
        {
            get
            {
                switch (Current)
                {
                    case Stage.FindKey: return key != null && key.Holder == null ? key.transform.position : (mainDoor != null ? mainDoor.transform.position : (Vector3?)null);
                    case Stage.UnlockDoor: return mainDoor != null ? mainDoor.transform.position : (Vector3?)null;
                    case Stage.RestorePower:
                        if (!Anyone("crank_handle") && clock != null) return clock.transform.position;
                        return generator != null ? generator.transform.position : (Vector3?)null;
                    case Stage.OpenGate: return gateControl != null ? gateControl.transform.position : (Vector3?)null;
                    case Stage.FindServiceGate:
                        // found it: the lock is released from the control room - unless you are already standing at it
                        var me = PlayerParty.Active;
                        if (serviceGate != null && serviceGate.Found && serviceGate.padlocked)
                            return Anyone("gate_key") || statues == null ? serviceGate.Centre : statues.transform.position + new Vector3(0f, 0f, 82f);
                        if (serviceGate != null && serviceGate.Found && controlRoom != null && me != null && Vector3.Distance(me.position, serviceGate.Centre) > 25f) return controlRoom.position;
                        return serviceGate != null ? serviceGate.Centre : (Vector3?)null;
                    case Stage.Escape: return serviceGate != null && serviceGate.IsOpen ? serviceGate.Centre + new Vector3(0f, 0f, -8f) : gate != null ? gate.transform.position : (Vector3?)null;
                }
                return null;
            }
        }

        // the campus is seen from far off (the giant is 48 m tall): shadows reach further and sharper here, and the
        // cameras use temporal anti-aliasing so thin distant shapes - its legs, its neck - stay clean instead of
        // breaking into jagged, crawling pixels. Put back as the level unloads (the pipeline asset is shared).
        float savedShadowDistance = -1f; int savedShadowRes;

        void Awake()
        {
            Instance = this; DistanceFade.KeepSky = true;
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null)
            {
                savedShadowDistance = urp.shadowDistance; savedShadowRes = urp.mainLightShadowmapResolution;
                urp.shadowDistance = 150f; urp.mainLightShadowmapResolution = 4096;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DistanceFade.KeepSky = false;
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null && savedShadowDistance >= 0f) { urp.shadowDistance = savedShadowDistance; urp.mainLightShadowmapResolution = savedShadowRes; }
        }

        void SharpCameras()
        {
            var party = PlayerParty.Instance;
            if (party == null) return;
            var cams = new System.Collections.Generic.List<Camera>();
            if (party.playerCamera != null) cams.Add(party.playerCamera);
            foreach (var p in party.players) if (p != null) cams.AddRange(p.GetComponentsInChildren<Camera>(true));
            foreach (var c in cams)
            {
                var d = c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (d == null || d.antialiasing == UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing) continue;
                d.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing;
                d.taaSettings.quality = UnityEngine.Rendering.Universal.TemporalAAQuality.High;
                d.taaSettings.contrastAdaptiveSharpening = 0.35f;   // TAA softens a little: sharpen it back
            }
        }

        bool droned;
        void Update()
        {
            if (!droned && mainDoor != null && mainDoor.Open && PlayerParty.Instance != null)
                foreach (var p in PlayerParty.Instance.players)
                    if (p != null && !p.IsDead && p.transform.position.z > 25f && !CampusGiant.Indoors(p.transform.position))
                    {
                        droned = true;
                        var c = UserSfx.Get("drone_doors");
                        if (c != null) SfxBank.Flat(c, 0.85f, 1f);
                        break;
                    }
            if (Time.frameCount % 30 == 0) SharpCameras();   // cheap; catches cameras a respawn or spectate re-creates
            if (Current != Stage.Escaped)
            {
                if ((gate != null && gate.IsOpen) || (serviceGate != null && serviceGate.IsOpen)) Current = Stage.Escape;
                else if (gate != null && gate.Jammed) Current = Stage.FindServiceGate;
                else if (generator != null && generator.On) Current = Stage.OpenGate;
                else if (mainDoor != null && mainDoor.Open) Current = Stage.RestorePower;
                else if (key != null && key.EverTaken) Current = Stage.UnlockDoor;
                else Current = Stage.FindKey;

                if (Current == Stage.Escape && SomeoneOutside()) { Current = Stage.Escaped; escapedAt = Time.time; }
            }
            if (Current != shown)
            {
                if ((int)shown >= 0 && PlayerParty.Instance != null && Current != Stage.Escaped) PlayerParty.Instance.Flash(Text(Current), 4f);
                shown = Current;
            }
            if (Current == Stage.Escaped && Time.time > escapedAt + 2f && InputWrap.Down(KeyCode.R))
            {
                if (Net.Net.Active) { if (Net.Net.IsAuthority) Net.NetLobby.LoadLevel(Net.NetLobby.LobbyScene); }
                else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }

        /// <summary>Some living player carries this item.</summary>
        static bool Anyone(string itemId)
        {
            var party = PlayerParty.Instance;
            if (party == null) return false;
            foreach (var p in party.players)
            {
                if (p == null || p.IsDead) continue;
                var inv = p.GetComponent<PlayerInventory>();
                if (inv != null && inv.Find(itemId) != null) return true;
            }
            return false;
        }

        bool SomeoneOutside()
        {
            var party = PlayerParty.Instance;
            if (party == null) return false;
            foreach (var p in party.players)
                if (p != null && !p.IsDead && (escapeZone.Contains(p.transform.position) || serviceEscapeZone.Contains(p.transform.position))) return true;
            return false;
        }

        string Text(Stage s)
        {
            switch (s)
            {
                case Stage.FindKey: return findKey;
                case Stage.UnlockDoor: return unlockDoor;
                case Stage.RestorePower: return Anyone("crank_handle") || clock == null ? restorePower : findCrank;
                case Stage.OpenGate: return openGate;
                case Stage.FindServiceGate:
                    if (serviceGate == null || !serviceGate.Found) return findServiceGate;
                    if (serviceGate.padlocked && !Anyone("gate_key")) return findGateKey;
                    if (serviceGate.padlocked) return "You have the groundskeeper's key - unlock the service gate's padlock";
                    return releaseServiceGate;
                case Stage.Escape: return serviceGate != null && serviceGate.IsOpen ? escapeService + "  (" + Mathf.CeilToInt(serviceGate.OpenLeft) + " s)" : escape;
            }
            return "";
        }

        void OnGUI()
        {
            UiStyle.Use();
            if (bannerStyle == null)
            {
                bannerStyle = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                bannerStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = new Color(0.85f, 0.95f, 1f);
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                bodyStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            }
            if (Current != Stage.Escaped)
            {
                string t = Text(Current);
                float w = Mathf.Min(860f, Screen.width - 40f);
                float h = t.Length > 110 ? 58f : 40f;
                var br = new Rect((Screen.width - w) * 0.5f, 10f, w, h);
                UiStyle.Slab(br, -0.4f);
                UiStyle.Label(new Rect(br.x + 18f, br.y, br.width - 36f, br.height), t, 15);
                // until someone has found the control room: where it is (it helps all the way to the end)
                if (!Walkie.AnyCollected && hintStyle != null)
                    GUI.Label(new Rect((Screen.width - w) * 0.5f, 14f + h, w, 20f), controlRoomHint, hintStyle);
                else if (hintStyle == null) { hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter }; hintStyle.normal.textColor = new Color(0.6f, 0.85f, 1f, 0.85f); }
                return;
            }
            // the ending: the campus behind them, the road ahead
            float k = Mathf.Clamp01((Time.time - escapedAt) / 2f);
            GUI.color = new Color(0f, 0f, 0f, 0.88f * k);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, k);
            UiStyle.Title(new Rect(0, Screen.height * 0.2f, Screen.width, 140f), 120, "[you got out]");
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.32f + 70f, Screen.width * 0.6f, 80f),
                "The gate grinds shut behind you. Whatever is left of the college stays inside it - for now.", bodyStyle);
            if (Time.time > escapedAt + 2f)
                GUI.Label(new Rect(0, Screen.height * 0.32f + 160f, Screen.width, 30f),
                    Net.Net.Active && !Net.Net.IsAuthority ? "Waiting for the host..." : "[R] " + (Net.Net.Active ? "Back to the lobby" : "Play again"), bodyStyle);
            GUI.color = Color.white;
        }
    }
}
