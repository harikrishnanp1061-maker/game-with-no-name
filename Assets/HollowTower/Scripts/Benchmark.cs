using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Run the game with "-benchmark" on the command line: it tours every floor with enemies frozen,
    /// writes average / 1%-low FPS per spot to benchmark.txt next to the game, then quits.
    /// </summary>
    public class Benchmark : MonoBehaviour
    {
        struct Spot
        {
            public int floor, row, col; public float yaw; public string name;
            public Vector3 at;   // annex spots (outside the grid): a world position instead of a cell
            public Vector3 Pos { get { return at != Vector3.zero ? at : LevelBuilder.CellCenter(floor, row, col); } }
        }

        static readonly Spot[] Spots =
        {
            new Spot { floor = 3, row = 2, col = 12, yaw = 90f, name = "F4 open-plan office" },
            new Spot { floor = 3, row = 6, col = 12, yaw = 90f, name = "F4 corridor" },
            new Spot { floor = 3, row = 9, col = 33, yaw = -90f, name = "F4 server room" },
            new Spot { floor = 2, row = 2, col = 8, yaw = 90f, name = "F3 hotel wing" },
            new Spot { floor = 2, row = 12, col = 12, yaw = 0f, name = "F3 lounge" },
            new Spot { floor = 2, row = 2, col = 20, yaw = 90f, name = "F3 guest corridor" },
            new Spot { floor = 1, row = 3, col = 9, yaw = 180f, name = "F2 classroom" },
            new Spot { floor = 1, row = 12, col = 33, yaw = 0f, name = "F2 library" },
            new Spot { floor = 1, row = 6, col = 20, yaw = -90f, name = "F2 locker corridor" },
            new Spot { floor = 1, at = new Vector3(36f, 4f, 76.7f), yaw = 90f, name = "F2 north wing" },
            new Spot { floor = 1, at = new Vector3(87f, 4f, 80f), yaw = 0f, name = "F2 auditorium" },
            new Spot { floor = 0, row = 3, col = 14, yaw = 180f, name = "F1 lobby" },
            new Spot { floor = 0, row = 12, col = 12, yaw = 0f, name = "F1 cafeteria" },
            new Spot { floor = 0, row = 12, col = 26, yaw = 0f, name = "F1 boiler room" },
            new Spot { floor = 0, at = new Vector3(37.5f, 0f, -6f), yaw = 180f, name = "F1 mansion foyer" },
            new Spot { floor = 0, at = new Vector3(82.5f, 0f, -6f), yaw = 200f, name = "F1 chapel" },
            new Spot { floor = 0, at = new Vector3(103.5f, 0f, -12f), yaw = 180f, name = "F1 hospital corridor" },
            new Spot { floor = 0, at = new Vector3(97f, 0f, -36f), yaw = 225f, name = "F1 security hall" },
            new Spot { floor = 0, at = new Vector3(0f, 0f, -16.5f), yaw = 270f, name = "F1 shrine" },
        };

        const float Settle = 1.5f, Measure = 4f;
        int index = -1;
        float phaseStart;
        bool measuring;
        readonly List<float> frameTimes = new List<float>();
        readonly StringBuilder report = new StringBuilder();

        public static bool Requested { get { return HasArg("-benchmark") || Shots || GunShots || BossShots || DogShots; } }

        /// <summary>"-bossshots": pictures of each boss model (running, winding up, smashing) to Shots/, then quit.</summary>
        public static bool BossShots { get { return HasArg("-bossshots"); } }

        /// <summary>"-dogshots": a contact sheet of the zombie dog's raw clip, then its poses, to Shots/, then quit.</summary>
        public static bool DogShots { get { return HasArg("-dogshots"); } }

        /// <summary>"-gunshots": first-person pictures of the rifle (hip, aiming, reloading) to Shots/, then quit.</summary>
        public static bool GunShots { get { return HasArg("-gunshots"); } }
        /// <summary>Set once -gunshots has saved its pictures (the editor batch runner waits for it).</summary>
        public static bool Done { get; private set; }

        /// <summary>"-shots": same tour, but saves screenshots (two frames apart, both ways) to Shots/ instead of timing.</summary>
        public static bool Shots { get { return HasArg("-shots"); } }

        static bool HasArg(string arg)
        {
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == arg) return true;
            return false;
        }

        int shotStep;
        string shotDir;

        void Start()
        {
            Application.runInBackground = true;
            phaseStart = Time.realtimeSinceStartup + 4f;   // let shaders and the first frames settle
            report.AppendLine("Hollow Tower benchmark  " + System.DateTime.Now);
            report.AppendLine("GPU " + SystemInfo.graphicsDeviceName + "  " + SystemInfo.graphicsDeviceType + "  " + Screen.width + "x" + Screen.height +
                              "  ray tracing " + HDRPLook.RayTracing + "  DLSS " + HDRPLook.DLSS + "  render scale " + HDRPLook.RenderScale + "%");
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var p = PlayerController.Instance;
            if (gm == null || p == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < phaseStart) return;

            if (index < 0)
            {
                gm.StartPlaying();
                // Freeze (not hide) enemies: they still render, they just don't move or attack.
                foreach (var z in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                {
                    var agent = z.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.enabled = false;
                    z.enabled = false;
                }
                foreach (var g in Object.FindObjectsByType<Ghost>(FindObjectsSortMode.None)) g.enabled = false;
                Next(p, now);
                return;
            }
            if (GunShots) { TakeGunShots(p, now); return; }
            if (BossShots) { TakeBossShots(p, now); return; }
            if (DogShots) { TakeDogShots(p, now); return; }
            if (Shots) { TakeShots(p, now); return; }
            if (!measuring)
            {
                if (now - phaseStart < Settle) return;
                measuring = true;
                frameTimes.Clear();
                phaseStart = now;
                return;
            }
            frameTimes.Add(Time.unscaledDeltaTime);
            if (now - phaseStart < Measure) return;

            float sum = 0f;
            foreach (var t in frameTimes) sum += t;
            frameTimes.Sort();
            int n = Mathf.Max(1, frameTimes.Count / 100);
            float worst = 0f;
            for (int i = frameTimes.Count - n; i < frameTimes.Count; i++) worst += frameTimes[i];
            report.AppendLine(string.Format("{0,-22} avg {1,5:0.0} fps   1% low {2,5:0.0} fps", Spots[index].name, frameTimes.Count / sum, n / worst));
            Next(p, now);
        }

        void TakeShots(PlayerController p, float now)
        {
            if (now - phaseStart < Settle) return;
            if (shotDir == null) { shotDir = Path.Combine(Application.dataPath, "..", "Shots"); Directory.CreateDirectory(shotDir); }
            // Two frames 6 apart (flicker shows as a difference), then turn round and repeat.
            var s = Spots[index];
            string name = s.name.Replace(' ', '_').Replace("'", "");
            int k = shotStep++;
            if (k == 0 || k == 6 || k == 40 || k == 46)
                ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + (k < 40 ? "_fwd" : "_back") + (k % 40 == 0 ? "_a" : "_b") + ".png"));
            else if (k == 8) p.Teleport(s.Pos, s.yaw + 180f, 0f);
            else if (k > 50) { shotStep = 0; Next(p, now); }
        }

        RenderTexture gunRT;

        void TakeGunShots(PlayerController p, float now)
        {
            if (now - phaseStart < Settle) return;
            if (shotDir == null) { shotDir = Path.Combine(Application.dataPath, "..", "Shots"); Directory.CreateDirectory(shotDir); }
            int k = shotStep++;
            if (k == 0)
            {
                gunRT = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                p.Cam.targetTexture = gunRT;
            }
            else if (k == 40) { SaveGunShot("gun_hip"); MP5.ForceAim = true; }
            else if (k == 90) { SaveGunShot("gun_aim"); MP5.ForceAim = false; p.Gun.SetAmmo(10, p.Gun.Reserve); MP5.ForceReload = true; }
            else if (k == 112) SaveGunShot("gun_reload");
            else if (k > 115)
            {
                MP5.ForceReload = false;
                p.Cam.targetTexture = null;
                Done = true;
                enabled = false;
                Application.Quit();
            }
        }

        Zombie boss;
        // "-shotfrom N": start the model pictures at BossRig variant N.
        static int ShotFrom { get { var a = System.Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < a.Length; i++) if (a[i] == "-shotfrom") return int.Parse(a[i + 1]); return 0; } }

        void TakeBossShots(PlayerController p, float now)
        {
            if (now - phaseStart < Settle) return;
            if (shotDir == null) { shotDir = Path.Combine(Application.dataPath, "..", "Shots"); Directory.CreateDirectory(shotDir); }
            int k = shotStep++;
            int v = k / 100 + ShotFrom, f = k % 100;
            if (k == 0)
            {
                gunRT = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                p.Cam.targetTexture = gunRT;
                p.Teleport(LevelBuilder.CellCenter(0, 3, 14), 0f, 0f); // F1 lobby, facing away from the desk
            }
            if (v >= BossRig.Variants)
            {
                p.Cam.targetTexture = null;
                Done = true;
                enabled = false;
                Application.Quit();
                return;
            }
            if (f < 5) return; // let the camera catch up with the teleport
            Vector3 fwd = Vector3.ProjectOnPlane(p.Cam.transform.forward, Vector3.up).normalized;
            // Bosses, then the chasers (walkers: scary zombie, parasite; runner: the human deer).
            string label = v < BossRig.BossVariants ? "boss" + (v + 1) : v == BossRig.Scary ? "scary" : v == BossRig.Parasite ? "parasite" : "deer";
            float run = v < BossRig.BossVariants ? 6.6f : v == BossRig.Deer ? 7.5f : 5f;
            if (f == 5)
            {
                if (boss != null) Destroy(boss.gameObject);
                Zombie.ForceVariant = v;
                boss = Zombie.Spawn(v < BossRig.BossVariants ? EnemyKind.Brute : v == BossRig.Deer ? EnemyKind.Runner : EnemyKind.Walker, p.transform.position + fwd * (v < BossRig.BossVariants ? 5.5f : 4f), null);
                Zombie.ForceVariant = -1;
                boss.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                boss.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, fwd)); // side-on while running
            }
            if (f < 30) boss.DebugPose(run, 0f, 0f);
            else if (f == 30) SaveGunShot(label + "_run");
            else if (f < 36) boss.DebugPose(run, 0f, 0f);
            else if (f == 36) { SaveGunShot(label + "_run2"); boss.transform.rotation = Quaternion.LookRotation(-fwd); }
            else if (f < 46) boss.DebugPose(run, 0f, 0f);
            else if (f == 46) SaveGunShot(label + "_front");
            else if (f < 70) boss.DebugPose(0.5f, Mathf.Clamp01((f - 50) / 12f), 0f);
            else if (f == 70) SaveGunShot(label + "_windup");
            else if (f < 90) boss.DebugPose(0.5f, 0f, 1f - (f - 80) / 30f);
            else if (f == 90) SaveGunShot(label + "_smash");
        }

        RenderTexture cellRT;
        Texture2D sheet;
        const int SheetCols = 6, SheetCells = 34, CellW = 480, CellH = 270;

        void TakeDogShots(PlayerController p, float now)
        {
            if (now - phaseStart < Settle) return;
            if (shotDir == null) { shotDir = Path.Combine(Application.dataPath, "..", "Shots"); Directory.CreateDirectory(shotDir); }
            int k = shotStep++;
            if (k == 0)
            {
                gunRT = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                cellRT = new RenderTexture(CellW, CellH, 24, RenderTextureFormat.ARGB32);
                sheet = new Texture2D(CellW * SheetCols, CellH * ((SheetCells + SheetCols - 1) / SheetCols), TextureFormat.RGB24, false);
                p.Cam.targetTexture = cellRT;
                p.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 12f); // F4 corridor, looking a little down
            }
            if (k < 5) return;
            Vector3 fwd = Vector3.ProjectOnPlane(p.Cam.transform.forward, Vector3.up).normalized;
            if (k == 5)
            {
                boss = Zombie.Spawn(EnemyKind.Dog, p.transform.position + fwd * 2.8f, null);
                boss.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                boss.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, fwd)); // side-on
                return;
            }
            // The raw clip, every half second.
            int i = (k - 6) / 4, f = (k - 6) % 4;
            if (i < SheetCells)
            {
                boss.DebugRaw(i * 0.5f);
                if (f == 3)
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = cellRT;
                    sheet.ReadPixels(new Rect(0, 0, CellW, CellH), (i % SheetCols) * CellW, sheet.height - CellH - (i / SheetCols) * CellH);
                    RenderTexture.active = prev;
                }
                return;
            }
            int g = k - 6 - SheetCells * 4;
            if (g == 0)
            {
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(shotDir, "dog_clip.png"), sheet.EncodeToPNG());
                p.Cam.targetTexture = gunRT;
            }
            if (g < 30) boss.DebugPose(7f, 0f, 0f);
            else if (g == 30) SaveGunShot("dog_run");
            else if (g < 34) boss.DebugPose(7f, 0f, 0f);
            else if (g == 34) SaveGunShot("dog_run2");
            else if (g < 44) boss.DebugPose(0f, 0f, 0f);
            else if (g == 44) SaveGunShot("dog_idle");
            else if (g < 50) boss.DebugPose(0f, 0.3f, 0f);
            else if (g == 50) SaveGunShot("dog_bite_windup");
            else if (g < 54) boss.DebugPose(0f, 0.8f, 0f);
            else if (g == 54) SaveGunShot("dog_bite");
            else if (g < 58) boss.DebugPose(0f, 0f, 0.5f);
            else if (g == 58) { SaveGunShot("dog_howl"); boss.transform.rotation = Quaternion.LookRotation(-fwd); }
            else if (g < 70) boss.DebugPose(7f, 0f, 0f);
            else if (g == 70) SaveGunShot("dog_front");
            else
            {
                p.Cam.targetTexture = null;
                Done = true;
                enabled = false;
                Application.Quit();
            }
        }

        void SaveGunShot(string name)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = gunRT;
            var tex = new Texture2D(gunRT.width, gunRT.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, gunRT.width, gunRT.height), 0, 0);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        void Next(PlayerController p, float now)
        {
            index++;
            measuring = false;
            phaseStart = now;
            if (index >= Spots.Length)
            {
                string path = Path.Combine(Application.dataPath, "..", "benchmark.txt");
                if (!Shots) File.WriteAllText(path, report.ToString());
                Debug.Log("Hollow Tower benchmark written to " + path + "\n" + report);
                Application.Quit();
                enabled = false;
                return;
            }
            var s = Spots[index];
            p.Teleport(s.Pos, s.yaw, 0f);
        }
    }
}
