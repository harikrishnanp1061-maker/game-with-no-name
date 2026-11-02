using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The walkie-talkies from the security control room's charging rack, and the radio network they make:
    ///   - Voice: hold [B] to talk. Your microphone is sent (8 kHz, mu-law, through the host) to everyone else holding a
    ///     walkie and to whoever sits at the control room console (its base station). It comes out of their radio with
    ///     static and the squelch beeps, and an incoming call crackles out loud where they stand - a Nailed close by can
    ///     hear it.
    ///   - Markers: the console drops a "go here" or "danger" marker on its map; [Z] with a walkie pings the spot you are
    ///     looking at. Walkie holders see the markers on screen with their distance; the console sees them on its map.
    ///   - Tracking: the console's map shows where every walkie is. Players without one are invisible to it.
    /// Offline (or when two bodies share a machine) the markers and HUD still work; voice simply has nobody to reach.
    /// </summary>
    public class Walkie : NetworkBehaviour, Net.INetAction
    {
        public const string ItemId = "walkie";
        public const KeyCode TalkKey = KeyCode.B, PingKey = KeyCode.Z;
        const int Rate = 8000, Chunk = 480;   // 60 ms per packet

        public static Walkie Instance { get; private set; }

        public struct Marker { public Vector3 pos; public int kind; public int from; public float until; }
        public static readonly List<Marker> Markers = new List<Marker>();
        public static readonly string[] MarkerNames = { "GO HERE", "DANGER", "PING" };
        public static readonly Color[] MarkerColors = { new Color(0.3f, 1f, 0.5f), new Color(1f, 0.25f, 0.2f), new Color(0.4f, 0.8f, 1f) };

        /// <summary>Someone has picked up a walkie (the control room has been found).</summary>
        public static bool AnyCollected { get; private set; }
        /// <summary>Who is transmitting right now (player index), per player.</summary>
        public static readonly bool[] OnAir = new bool[5];   // 0-3 players, 4 = the console's base station

        public static bool Has(int player)
        {
            var party = PlayerParty.Instance;
            if (party == null || player < 0 || player >= party.players.Length || party.players[player] == null) return false;
            var inv = party.players[player].GetComponent<PlayerInventory>();
            return inv != null && inv.Find(ItemId) != null;
        }

        static int LocalPlayer { get { var p = PlayerParty.Instance; return p != null ? p.CurrentIndex : -1; } }
        /// <summary>This machine can hear and use the radio: its player holds a walkie, or sits at the console.</summary>
        public static bool LocalOnRadio { get { return SurveillanceConsole.LocalSeated || (LocalPlayer >= 0 && Has(LocalPlayer) && !PlayerParty.Instance.Spectating); } }

        // ------------------------------------------------------------------ markers
        public static void PlaceMarker(Vector3 pos, int kind, int from)
        {
            if (Instance != null) Net.NetWorld.Act(Instance, 1, kind, from, pos);
            else AddMarker(pos, kind, from);
        }

        static void AddMarker(Vector3 pos, int kind, int from)
        {
            // one live marker per sender and kind: a new one replaces it
            Markers.RemoveAll(m => m.from == from && m.kind == kind);
            Markers.Add(new Marker { pos = pos, kind = kind, from = from, until = Time.time + (kind == 2 ? 20f : 45f) });
            if (LocalOnRadio) SfxBank.Flat(CampusSynth.SquelchOn, 0.5f, kind == 1 ? 0.7f : 1.2f);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action == 1) { AddMarker(v, a, b); return true; }
            return false;
        }

        // ------------------------------------------------------------------ voice
        AudioClip mic; int micPos; bool talking; string micDevice; bool noMicWarned;
        readonly List<float> outBuf = new List<float>();
        float hp, lastIn;   // the send side's high-pass state
        // playback
        AudioSource radio, crackle;
        readonly float[] ring = new float[Rate * 2];
        int rHead, rTail; readonly object rLock = new object();
        volatile bool receiving;
        float nextNoise;
        readonly System.Random noiseRnd = new System.Random(3);

        void Awake() { Instance = this; }
        public override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; Markers.Clear(); AnyCollected = false; for (int i = 0; i < OnAir.Length; i++) OnAir[i] = false; if (talking) Microphone.End(micDevice); }

        void Start()
        {
            radio = gameObject.AddComponent<AudioSource>();
            radio.clip = AudioClip.Create("WalkieRx", Rate, 1, Rate, true, OnRead);
            radio.loop = true; radio.spatialBlend = 0f; radio.volume = 1f; radio.Play();
            crackle = gameObject.AddComponent<AudioSource>();
            crackle.clip = CampusSynth.Static; crackle.loop = true; crackle.spatialBlend = 0f; crackle.volume = 0f; crackle.Play();
        }

        void OnRead(float[] data)
        {
            lock (rLock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (rHead != rTail) { data[i] = ring[rTail]; rTail = (rTail + 1) % ring.Length; }
                    else data[i] = 0f;
                }
            }
        }

        void Update()
        {
            if (!AnyCollected && PlayerParty.Instance != null)
                for (int i = 0; i < 4; i++) if (Has(i)) { AnyCollected = true; break; }
            Markers.RemoveAll(m => Time.time > m.until);

            bool on = LocalOnRadio;
            bool held = on && InputWrap.Held(TalkKey) && (!Puzzles.PuzzlePanel.IsOpen || SurveillanceConsole.LocalSeated);
            if (held && !talking) StartTalk();
            else if (!held && talking) StopTalk();
            if (talking) Capture();

            if (on && !SurveillanceConsole.LocalSeated && InputWrap.Down(PingKey) && !Puzzles.PuzzlePanel.IsOpen) Ping();

            bool anyAir = false;
            for (int i = 0; i < OnAir.Length; i++) if (OnAir[i] && i != Speaker) anyAir = true;
            crackle.volume = Mathf.MoveTowards(crackle.volume, on && anyAir ? 0.06f : 0f, Time.deltaTime * 2f);
            radio.mute = !on;
        }

        /// <summary>This machine's radio id: its player, or the base station when it is at the console.</summary>
        static int Speaker { get { return SurveillanceConsole.LocalSeated ? 4 : LocalPlayer; } }

        void Ping()
        {
            var party = PlayerParty.Instance;
            var cam = party != null ? party.playerCamera : null;
            if (cam == null) return;
            RaycastHit hit;
            Vector3 p = Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point : cam.transform.position + cam.transform.forward * 20f;
            PlaceMarker(p, 2, LocalPlayer);
        }

        void StartTalk()
        {
            talking = true;
            SfxBank.Flat(CampusSynth.SquelchOn, 0.6f, 1f);
            SetAir(Speaker, true);
            if (Microphone.devices.Length == 0)
            {
                if (!noMicWarned && PlayerParty.Instance != null) PlayerParty.Instance.Flash("No microphone found - the others only hear your radio click", 3f);
                noMicWarned = true; mic = null; return;
            }
            micDevice = null;
            mic = Microphone.Start(micDevice, true, 1, 16000);
            micPos = 0; outBuf.Clear();
        }

        void StopTalk()
        {
            talking = false;
            SfxBank.Flat(CampusSynth.SquelchOff, 0.6f, 1f);
            SetAir(Speaker, false);
            if (mic != null) { Microphone.End(micDevice); mic = null; }
        }

        void Capture()
        {
            if (mic == null) return;
            int pos = Microphone.GetPosition(micDevice);
            if (pos < 0 || pos == micPos) return;
            int n = (pos - micPos + mic.samples) % mic.samples;
            var tmp = new float[n];
            if (micPos + n <= mic.samples) mic.GetData(tmp, micPos);
            else
            {
                var a = new float[mic.samples - micPos]; mic.GetData(a, micPos);
                var b = new float[n - a.Length]; mic.GetData(b, 0);
                a.CopyTo(tmp, 0); b.CopyTo(tmp, a.Length);
            }
            micPos = pos;
            int ch = Mathf.Max(1, mic.channels);
            for (int i = 0; i + 2 * ch - 1 < tmp.Length; i += 2 * ch)
            {
                float s = (tmp[i] + tmp[i + ch]) * 0.5f;   // 16 kHz -> 8 kHz
                // radio band: cut the rumble, keep it from clipping
                hp = 0.92f * (hp + s - lastIn); lastIn = s;
                outBuf.Add(hp);
            }
            while (outBuf.Count >= Chunk)
            {
                var pkt = new byte[Chunk];
                for (int i = 0; i < Chunk; i++) pkt[i] = MuLaw(outBuf[i] * 1.6f);
                outBuf.RemoveRange(0, Chunk);
                Send(pkt);
            }
        }

        void Send(byte[] pkt)
        {
            if (!IsSpawned) return;
            int from = Speaker;
            if (IsServer) DownRpc(from, pkt, NetworkManager.LocalClientId);
            else UpRpc(from, pkt);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void UpRpc(int from, byte[] pkt, RpcParams rpc = default)
        {
            Receive(from, pkt);
            DownRpc(from, pkt, rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void DownRpc(int from, byte[] pkt, ulong origin)
        {
            if (origin == NetworkManager.LocalClientId) return;
            Receive(from, pkt);
        }

        void Receive(int from, byte[] pkt)
        {
            if (from == Speaker || !LocalOnRadio) return;
            lock (rLock)
            {
                for (int i = 0; i < pkt.Length; i++)
                {
                    float s = Mathf.Clamp(MuLawDecode(pkt[i]) * 1.4f, -0.9f, 0.9f);
                    s += ((float)noiseRnd.NextDouble() * 2f - 1f) * 0.03f;   // a little hiss under the voice
                    int next = (rHead + 1) % ring.Length;
                    if (next == rTail) rTail = (rTail + 1) % ring.Length;   // falling behind: drop the oldest
                    ring[rHead] = s; rHead = next;
                }
            }
            // the call crackles out loud where you stand
            if (Time.time > nextNoise && PlayerParty.Instance != null && PlayerParty.Instance.Current != null && !SurveillanceConsole.LocalSeated)
            {
                nextNoise = Time.time + 3f;
                Net.NetWorld.Noise(PlayerParty.Instance.Current.transform.position, 7f);
            }
        }

        void SetAir(int who, bool on)
        {
            OnAir[who] = on;
            if (IsSpawned) { if (IsServer) AirDownRpc(who, on); else AirUpRpc(who, on); }
        }
        [Rpc(SendTo.Server)] void AirUpRpc(int who, bool on) { OnAir[who] = on; AirDownRpc(who, on); }
        [Rpc(SendTo.NotServer)]
        void AirDownRpc(int who, bool on)
        {
            bool was = OnAir[who]; OnAir[who] = on;
            if (who == Speaker || !LocalOnRadio || was == on) return;
            SfxBank.Flat(on ? CampusSynth.SquelchOn : CampusSynth.SquelchOff, 0.55f, 0.9f);
        }

        // G.711 mu-law
        static byte MuLaw(float f)
        {
            int s = Mathf.Clamp((int)(f * 32767f), -32635, 32635);
            int sign = (s >> 8) & 0x80; if (sign != 0) s = -s;
            s += 0x84;
            int exp = 7; for (int m = 0x4000; (s & m) == 0 && exp > 0; m >>= 1) exp--;
            int man = (s >> (exp + 3)) & 0x0F;
            return (byte)~(sign | (exp << 4) | man);
        }
        static float MuLawDecode(byte b)
        {
            int u = ~b & 0xFF;
            int sign = u & 0x80, exp = (u >> 4) & 7, man = u & 0x0F;
            int s = ((man << 3) + 0x84) << exp; s -= 0x84;
            return (sign != 0 ? -s : s) / 32768f;
        }

        // ------------------------------------------------------------------ HUD (walkie holders)
        GUIStyle hud, tag;
        void OnGUI()
        {
            UiStyle.Use();
            var party = PlayerParty.Instance;
            if (party == null || !LocalOnRadio || SurveillanceConsole.LocalSeated || RemasteredMapHUD.IsFullMapOpen) return;
            if (hud == null)
            {
                hud = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                tag = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            // who is talking
            string air = "";
            for (int i = 0; i < OnAir.Length; i++) if (OnAir[i]) air += (air.Length > 0 ? "  " : "") + (i == 4 ? "CONTROL" : "P" + (i + 1));
            hud.normal.textColor = talking ? new Color(1f, 0.4f, 0.3f) : new Color(0.65f, 0.95f, 0.75f);
            GUI.Label(new Rect(16, Screen.height - 120, 520, 22), talking ? "TRANSMITTING..." : (air.Length > 0 ? "ON AIR: " + air : "WALKIE  hold [B] talk   [Z] ping where you look"), hud);
            // the markers: a diamond and the distance, pinned to the screen edge when off screen
            var cam = party.playerCamera;
            if (cam == null) return;
            foreach (var m in Markers)
            {
                Vector3 sp = cam.WorldToScreenPoint(m.pos + Vector3.up * 0.5f);
                bool behind = sp.z < 0f;
                if (behind) sp.x = Screen.width - sp.x;   // behind you: mirrored, and pinned to the bottom edge
                float x = Mathf.Clamp(sp.x, 30f, Screen.width - 30f), y = Mathf.Clamp(Screen.height - sp.y, 60f, Screen.height - 60f);
                if (behind) { y = Screen.height - 150f; x = sp.x < Screen.width * 0.5f ? 30f : Screen.width - 30f; }
                var c = MarkerColors[Mathf.Clamp(m.kind, 0, 2)];
                c.a = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
                GUI.color = c;
                var m2 = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
                GUI.DrawTexture(new Rect(x - 8f, y - 8f, 16f, 16f), Texture2D.whiteTexture);
                GUI.matrix = m2;
                tag.normal.textColor = c;
                float d = party.Current != null ? Vector3.Distance(party.Current.transform.position, m.pos) : 0f;
                GUI.Label(new Rect(x - 80f, y + 10f, 160f, 18f), MarkerNames[Mathf.Clamp(m.kind, 0, 2)] + "  " + d.ToString("F0") + " m" + (m.from == 4 ? "" : "  (P" + (m.from + 1) + ")"), tag);
                GUI.color = Color.white;
            }
        }
    }
}
