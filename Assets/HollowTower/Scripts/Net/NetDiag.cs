using System.IO;
using UnityEngine;

namespace HollowTower.Net
{
    /// <summary>
    /// Test aid for network sessions: launched with -netdiag &lt;folder&gt;, writes a status line (scene, session, which body
    /// this machine steers, where everyone is, the party floor's phase, how many monsters are hunting) to status.txt and
    /// a screenshot of its view every few seconds, so a test can see what a client sees. It also obeys cmd.txt in that
    /// folder (lines appended by the test): "tp x y z", "hold KEY seconds", "press KEY", "look yaw pitch", "shot".
    /// </summary>
    public class NetDiag : MonoBehaviour
    {
        static string folder;
        float next;
        int shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-netdiag") folder = args[i + 1];
            if (folder == null) return;
            Application.runInBackground = true;   // a test window keeps playing without focus
            Directory.CreateDirectory(folder);
            var go = new GameObject("NetDiag");
            DontDestroyOnLoad(go);
            go.AddComponent<NetDiag>();
        }

        long cmdRead;
        readonly System.Collections.Generic.Dictionary<KeyCode, float> held = new System.Collections.Generic.Dictionary<KeyCode, float>();
        bool clearDown;

        void Commands()
        {
            if (clearDown) { InputWrap.Bot.down.Clear(); clearDown = false; }
            var drop = new System.Collections.Generic.List<KeyCode>();
            foreach (var kv in held) if (Time.unscaledTime > kv.Value) drop.Add(kv.Key);
            foreach (var k in drop) { held.Remove(k); InputWrap.Bot.held.Remove(k); }
            InputWrap.Bot.Active = held.Count > 0 || InputWrap.Bot.down.Count > 0;
            string f = Path.Combine(folder, "cmd.txt");
            if (!File.Exists(f)) return;
            var text = File.ReadAllText(f);
            if (text.Length <= cmdRead) return;
            var lines = text.Substring((int)cmdRead).Split('\n');
            cmdRead = text.Length;
            var body = PlayerParty.Instance != null ? PlayerParty.Instance.Current : null;
            foreach (var raw in lines)
            {
                var a = raw.Trim().Split(' ');
                if (a.Length == 0 || a[0] == "") continue;
                try
                {
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    if (a[0] == "tp" && body != null)
                    {
                        var cc = body.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
                        body.transform.position = new Vector3(float.Parse(a[1], inv), float.Parse(a[2], inv), float.Parse(a[3], inv));
                        if (cc != null) cc.enabled = true;
                    }
                    else if (a[0] == "hold") { var k = (KeyCode)System.Enum.Parse(typeof(KeyCode), a[1]); held[k] = Time.unscaledTime + float.Parse(a[2], inv); InputWrap.Bot.held.Add(k); InputWrap.Bot.Active = true; }
                    else if (a[0] == "press") { InputWrap.Bot.down.Add((KeyCode)System.Enum.Parse(typeof(KeyCode), a[1])); InputWrap.Bot.Active = true; clearDown = true; }
                    else if (a[0] == "look" && body != null)
                    {
                        body.transform.rotation = Quaternion.Euler(0f, float.Parse(a[1], inv), 0f);
                        if (body.cameraHolder != null) body.cameraHolder.localRotation = Quaternion.Euler(float.Parse(a[2], inv), 0f, 0f);
                        typeof(RemasteredPlayerController).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(body, float.Parse(a[1], inv));
                        typeof(RemasteredPlayerController).GetField("pitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(body, float.Parse(a[2], inv));
                    }
                    else if (a[0] == "shot") next = 0f;
                }
                catch (System.Exception e) { File.AppendAllText(Path.Combine(folder, "status.txt"), "bad command '" + raw + "': " + e.Message + "\n"); }
            }
        }

        void Update()
        {
            Commands();
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 4f;
            var party = PlayerParty.Instance;
            var sb = new System.Text.StringBuilder();
            sb.Append(System.DateTime.Now.ToString("HH:mm:ss")).Append(" scene=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.Append(" net=").Append(Net.Active).Append(" server=").Append(Net.Active && Net.Manager.IsServer).Append(" id=").Append(Net.LocalId);
            if (party != null)
            {
                sb.Append(" steering=").Append(party.Current != null ? party.Current.name : "none");
                for (int i = 0; i < party.players.Length; i++)
                {
                    var b = party.players[i];
                    if (b == null) continue;
                    var nb = b.GetComponent<NetBody>();
                    sb.Append(" | P").Append(i + 1).Append(b.transform.position.ToString("0.0")).Append(b.IsDead ? " DEAD" : "").Append(nb != null && nb.IsSpawned ? (nb.IsOwner ? " mine" : " theirs") : "");
                }
            }
            if (PartyFloor.Instance != null) sb.Append(" | party=").Append(PartyFloor.Instance.Current).Append(" ").Append(PartyFloor.Instance.PhaseTimeLeft.ToString("0"));
            int hunting = 0; foreach (var m in PartyMonster.All) if (m != null && m.Hunting) hunting++;
            sb.Append(" huntingGuests=").Append(hunting);
            File.AppendAllText(Path.Combine(folder, "status.txt"), sb + "\n");
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "view_" + (shot++ % 50).ToString("00") + ".png"));
        }
    }
}
