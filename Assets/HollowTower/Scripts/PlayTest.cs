using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// Run the game with "-playtest": a bot drives the player through virtual keys and mouse
    /// (InputWrap.Bot) and checks movement, the rifle, cover, ghosts, both bosses, the map, checkpoints
    /// and frame-to-frame flicker. Writes playtest.txt and PlayTest/*.png next to the game, then quits.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PlayTest : MonoBehaviour
    {
        public static bool Requested
        {
            get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-playtest") return true; return false; }
        }

        static PlayTest instance;
        readonly HashSet<KeyCode> pressNext = new HashSet<KeyCode>();
        readonly StringBuilder report = new StringBuilder();
        int passed, failed;
        string dir;

        public static void Ensure()
        {
            if (instance != null) return;
            var g = new GameObject("PlayTest");
            DontDestroyOnLoad(g);
            instance = g.AddComponent<PlayTest>();
        }

        void Start()
        {
            InputWrap.Bot.Active = true;
            Application.runInBackground = true;   // keep testing when the window loses focus
            dir = Path.Combine(Application.dataPath, "..", "PlayTest");
            Directory.CreateDirectory(dir);
            report.AppendLine("Hollow Tower play test  " + System.DateTime.Now + "  " + SystemInfo.graphicsDeviceName);
            StartCoroutine(Run());
        }

        void Update()
        {
            InputWrap.Bot.down.Clear();
            foreach (var k in pressNext) InputWrap.Bot.down.Add(k);
            pressNext.Clear();
        }

        // ---------------- helpers ----------------

        void Press(KeyCode k) { pressNext.Add(k); }
        void Hold(KeyCode k, bool on) { if (on) InputWrap.Bot.held.Add(k); else InputWrap.Bot.held.Remove(k); }
        void ReleaseAll() { InputWrap.Bot.held.Clear(); }

        void Check(string name, bool ok, string detail)
        {
            if (ok) passed++; else failed++;
            string line = (ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "  (" + detail + ")");
            report.AppendLine(line);
            Debug.Log("[PlayTest] " + line);
        }

        void Note(string text) { report.AppendLine("     " + text); Debug.Log("[PlayTest] " + text); }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
        }

        static PlayerController P { get { return PlayerController.Instance; } }
        static GameManager GM { get { return GameManager.Instance; } }

        void AimAt(Vector3 point)
        {
            Vector3 d = point - P.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            P.SetLook(yaw, pitch);
        }

        void FreezeEnemies()
        {
            foreach (var z in Zombie.All.ToArray()) Destroy(z.gameObject);
            foreach (var g in Ghost.All.ToArray()) Destroy(g.gameObject);
        }

        IEnumerator WaitForGame()
        {
            while (GM == null || P == null) yield return null;
            yield return Frames(30);
        }

        // ---------------- the run ----------------

        IEnumerator Run()
        {
            yield return WaitForGame();
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("00_intro");
            Press(KeyCode.Return);
            yield return Frames(5);
            PowerGrid.AutoCuts = false;   // only the power section cuts the lights
            Check("Intro: Enter starts the game", GM.IsPlaying, "");
            CountStartingEnemies();
            FreezeEnemies();
            yield return Frames(10);

            yield return Section("movement", Movement());
            yield return Section("rifle", Rifle());
            yield return Section("cover", Cover());
            yield return Section("ghost", GhostTest());
            yield return Section("ghost model", GhostModel());
            yield return Section("ghost vanish + smoke", GhostSkills());
            yield return Section("power cut", PowerCut());
            yield return Section("boss 1", Boss(0));
            yield return Section("boss 2", Boss(1));
            yield return Section("scary zombie", Chaser(EnemyKind.Walker, BossRig.Scary, "Scary zombie", 5f));
            yield return Section("parasite zombie", Chaser(EnemyKind.Walker, BossRig.Parasite, "Parasite zombie", 5f));
            yield return Section("human deer", Chaser(EnemyKind.Runner, BossRig.Deer, "Human deer", 7.5f));
            yield return Section("zombie dog", DogTest());
            yield return Section("point blank", PointBlank());
            yield return Section("map", MapTest());
            yield return Section("checkpoints", Checkpoints());
            // Checkpoints ends on a fresh intro screen.
            Press(KeyCode.Return);
            yield return Frames(5);
            FreezeEnemies();
            yield return Section("flicker", Flicker());
            // The new features run last (after the checkpoint test, which wants a fresh building).
            yield return Section("pistol", PistolTest());
            yield return Section("kill ammo", KillAmmo());
            yield return Section("boss waves", BossWaves());
            yield return Section("ghost shot power", GhostShotPower());
            yield return Section("smoke", SmokeTest());
            yield return Section("trap pits", PitTest());
            yield return Section("annex", AnnexTest());
            yield return Section("north wing", WingTest());
            yield return Section("walkabout", Walkabout());

            report.AppendLine();
            report.AppendLine(string.Format("{0} passed, {1} failed", passed, failed));
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "playtest.txt"), report.ToString());
            Debug.Log("[PlayTest] done\n" + report);
            InputWrap.Bot.Active = false;
            Application.Quit();
        }

        /// <summary>"-playtestonly ghost" runs just the sections whose name contains "ghost".</summary>
        static string OnlySections
        {
            get
            {
                var a = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < a.Length; i++) if (a[i] == "-playtestonly") return a[i + 1];
                return null;
            }
        }

        IEnumerator Section(string name, IEnumerator body)
        {
            if (OnlySections != null && !name.Contains(OnlySections)) yield break;
            report.AppendLine("== " + name);
            ReleaseAll();
            // Run the section, catching exceptions so one broken test doesn't stop the rest.
            while (true)
            {
                object cur;
                try
                {
                    if (!body.MoveNext()) break;
                    cur = body.Current;
                }
                catch (System.Exception e)
                {
                    Check(name + " threw", false, e.GetType().Name + ": " + e.Message);
                    Debug.LogException(e);
                    break;
                }
                yield return cur;
            }
            ReleaseAll();
            if (P != null && !P.IsDead) P.Heal(999f);
        }

        // ---------------- new: bosses, pistol, annex, pits, smoke ----------------

        void CountStartingEnemies()
        {
            int[] bosses = new int[4], ghosts = new int[4];
            foreach (var z in Zombie.All) if (!z.IsDead && z.Kind == EnemyKind.Brute) bosses[LevelBuilder.FloorAt(z.transform.position)]++;
            foreach (var g in Ghost.All) ghosts[LevelBuilder.FloorAt(g.transform.position + Vector3.down * Ghost.FeetBelow)]++;
            Check("Bosses at the start: floor 3 has 5, floor 2 has 6 (+5 wave), floor 1 has 10 (+10 wave)",
                bosses[2] == 5 && bosses[1] == 6 && bosses[0] == 10, "floors 1-4: " + bosses[0] + ", " + bosses[1] + ", " + bosses[2] + ", " + bosses[3]);
            Check("Ghosts: 3-5 on floor 3, more on floors 2 and 1", ghosts[2] >= 3 && ghosts[2] <= 5 && ghosts[1] > ghosts[2] && ghosts[0] > ghosts[2],
                "floors 1-4: " + ghosts[0] + ", " + ghosts[1] + ", " + ghosts[2] + ", " + ghosts[3]);
            foreach (var z in Zombie.All)
                if (z.Kind == EnemyKind.Brute && LevelBuilder.FloorAt(z.transform.position) == 0 && !LevelBuilder.InAnnex(z.transform.position))
                    Note("Ground-floor tower boss at " + z.transform.position.ToString("F1"));
            int annexBosses = 0, wingBosses = 0;
            foreach (var z in Zombie.All)
            {
                if (z.Kind != EnemyKind.Brute) continue;
                if (LevelBuilder.InAnnex(z.transform.position)) { annexBosses++; if (z.transform.position.y > 2f) Note("Annex boss up high at " + z.transform.position.ToString("F1")); }
                if (LevelBuilder.InWing(z.transform.position)) wingBosses++;
            }
            Note("Bosses in the annex: " + annexBosses + ", in the floor 2 wing: " + wingBosses);
            int annexEnemies = 0;
            foreach (var z in Zombie.All) if (LevelBuilder.InAnnex(z.transform.position)) annexEnemies++;
            Note("Enemies in the annex at the start: " + annexEnemies + "; boss health floors 1-3: " + Zombie.BossHealth(0) + ", " + Zombie.BossHealth(1) + ", " + Zombie.BossHealth(2));
        }

        IEnumerator PistolTest()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            yield return Frames(10);
            Check("Starts with the M4A1", !P.Gun.IsPistol, P.Gun.DisplayName);
            Press(KeyCode.X);
            yield return Frames(5);
            Check("X swaps to the pistol", P.Gun.IsPistol && P.Gun.Model.gameObject.activeInHierarchy && !P.Rifle.Model.gameObject.activeInHierarchy, P.Gun.DisplayName);
            Check("Pistol: 12-round magazine", P.Gun.Ammo == MP5.PistolMag, P.Gun.Ammo + " / " + P.Gun.Reserve);
            yield return Shot("20_pistol_hip");
            Hold(KeyCode.Mouse1, true);
            yield return Frames(25);
            yield return Shot("21_pistol_aim");
            Hold(KeyCode.Mouse1, false);
            int a0 = P.Gun.Ammo;
            Hold(KeyCode.Mouse0, true);
            yield return new WaitForSeconds(0.8f);
            Hold(KeyCode.Mouse0, false);
            Check("Pistol is semi-auto: holding the trigger fires one round", a0 - P.Gun.Ammo == 1, (a0 - P.Gun.Ammo) + " rounds");
            yield return Frames(3);   // let go first
            for (int i = 0; i < 4; i++) { Hold(KeyCode.Mouse0, true); yield return Frames(2); Hold(KeyCode.Mouse0, false); yield return new WaitForSeconds(0.2f); }
            Check("Each click fires again", a0 - P.Gun.Ammo == 5, (a0 - P.Gun.Ammo) + " rounds");
            P.Gun.SetAmmo(1, 24);
            Hold(KeyCode.Mouse0, true); yield return Frames(2); Hold(KeyCode.Mouse0, false);
            yield return new WaitForSeconds(1.2f);
            Check("Pistol reloads itself when empty", P.Gun.Ammo == MP5.PistolMag && P.Gun.Reserve == 12, P.Gun.Ammo + " / " + P.Gun.Reserve);
            Press(KeyCode.X);
            yield return Frames(5);
            Check("X swaps back to the M4A1", !P.Gun.IsPistol && P.Rifle.Model.gameObject.activeInHierarchy, P.Gun.DisplayName);
        }

        IEnumerator KillAmmo()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            var z = Zombie.Spawn(EnemyKind.Walker, LevelBuilder.CellCenter(3, 6, 10), null);
            yield return Frames(5);
            int r0 = P.Rifle.Reserve, p0 = P.Pistol.Reserve;
            z.TakeDamage(999f, z.transform.position, Vector3.right, false);
            yield return Frames(3);
            Check("A kill pays a small ammo reward (+6 rifle, +3 pistol)", P.Rifle.Reserve == r0 + 6 && P.Pistol.Reserve == p0 + 3, (P.Rifle.Reserve - r0) + " / " + (P.Pistol.Reserve - p0));
            Destroy(z.gameObject);
            foreach (var pk in Pickup.All.ToArray()) if (pk.kind == PickupKind.Ammo && Vector3.Distance(pk.transform.position, LevelBuilder.CellCenter(3, 6, 10)) < 3f) Destroy(pk.gameObject);
        }

        IEnumerator BossWaves()
        {
            int Count(int f) { int n = 0; foreach (var z in Zombie.All) if (!z.IsDead && z.Kind == EnemyKind.Brute && LevelBuilder.FloorAt(z.transform.position) == f) n++; return n; }
            P.Teleport(LevelBuilder.CellCenter(1, 6, 8), 90f, 0f);
            yield return Frames(10);
            GM.SpawnWave(1, null);
            yield return Frames(5);
            Check("Floor 2 wave: 5 more bosses (11 in all)", Count(1) == 5, Count(1) + " spawned");
            P.Teleport(LevelBuilder.CellCenter(0, 6, 8), 90f, 0f);
            yield return Frames(10);
            GM.SpawnWave(0, null);
            yield return Frames(5);
            int n0 = Count(0);
            Check("Floor 1 wave: 10 more bosses (20 in all)", n0 == 10, n0 + " spawned");
            float nearest = 999f;
            foreach (var z in Zombie.All) if (z.Kind == EnemyKind.Brute) nearest = Mathf.Min(nearest, Vector3.Distance(z.transform.position, P.transform.position));
            Check("Wave bosses arrive away from you (>= 25 m)", nearest >= 25f, nearest.ToString("0") + " m");
            yield return Frames(30);
            float fpsWindow = 0f; int frames = 0;
            for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime) { fpsWindow += Time.unscaledDeltaTime; frames++; yield return null; }
            Note("FPS with " + Zombie.All.Count + " live enemies in the tower: " + (frames / fpsWindow).ToString("0"));
            FreezeEnemies();
        }

        IEnumerator GhostShotPower()
        {
            PowerGrid.AutoCuts = true;
            Ghost.Skills = false;
            for (int f = 1; f >= 0; f--)
            {
                P.Teleport(LevelBuilder.CellCenter(f, 6, 6), 90f, 0f);
                yield return Frames(10);
                if (P.FlashlightOn) Press(KeyCode.F);
                yield return Frames(3);
                PowerGrid.Restore(f);
                yield return new WaitForSeconds(0.2f);
                var ghost = Ghost.Spawn(P.transform.position + Vector3.right * 8f + Vector3.up * 1.4f, null, false);
                yield return Frames(15);
                bool before = PowerGrid.IsOn(f);
                float t0 = Time.time;
                while (PowerGrid.IsOn(f) && ghost.IsActive && Time.time - t0 < 4f)
                {
                    AimAt(ghost.transform.position + Vector3.up * 0.1f);
                    Hold(KeyCode.Mouse0, true);
                    yield return null;
                    Hold(KeyCode.Mouse0, false);
                    yield return Frames(4);
                }
                Check("Floor " + (f + 1) + ": shooting a ghost cuts the power", before && !PowerGrid.IsOn(f), "power " + (PowerGrid.IsOn(f) ? "on" : "off"));
                Destroy(ghost.gameObject);
                var gen = PowerGrid.Generators[f];
                Check("Floor " + (f + 1) + " has a generator" + (f == 0 ? " (in the security hall)" : ""), gen != null && (f != 0 || LevelBuilder.SecurityHall.Contains(gen.transform.position + Vector3.up)), gen != null ? gen.transform.position.ToString("F0") : "none");
                if (gen != null)
                {
                    gen.Use();
                    yield return new WaitForSeconds(Generator.StartTime + 0.3f);
                    Check("Floor " + (f + 1) + ": restarting the generator brings it back", PowerGrid.IsOn(f), "");
                }
                Press(KeyCode.F);
                yield return Frames(3);
            }
            // Floor 3 ghosts don't do it.
            P.Teleport(LevelBuilder.CellCenter(2, 6, 6), 90f, 0f);
            yield return Frames(10);
            var g3 = Ghost.Spawn(P.transform.position + Vector3.right * 8f + Vector3.up * 1.4f, null, false);
            yield return Frames(10);
            g3.TakeHit(10f);
            Check("Floor 3: shooting a ghost leaves the lights on", PowerGrid.IsOn(2), "");
            Destroy(g3.gameObject);
            Ghost.Skills = true;
            PowerGrid.AutoCuts = false;
        }

        IEnumerator SmokeTest()
        {
            // A walker down the floor 4 corridor, smoke between you: it can't see you through it, and you can't see it.
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            var z = Zombie.Spawn(EnemyKind.Walker, LevelBuilder.CellCenter(3, 6, 10), null);
            z.transform.rotation = Quaternion.LookRotation(Vector3.left);
            z.GetComponent<NavMeshAgent>().enabled = false;
            yield return new WaitForSeconds(0.5f);
            bool sawBefore = z.CanSeePlayer;
            var smoke = GhostSmoke.Create(LevelBuilder.CellCenter(3, 6, 8), Quaternion.identity);
            yield return new WaitForSeconds(3.5f);
            Check("Smoke fills the corridor (runs along it)", Vector3.Distance(smoke.EndA, smoke.EndB) > 10f && smoke.Radius >= 1.4f,
                "length " + Vector3.Distance(smoke.EndA, smoke.EndB).ToString("0.0") + " m, radius " + smoke.Radius.ToString("0.0"));
            yield return new WaitForSeconds(0.4f);
            Check("Zombies can't see you through the smoke", sawBefore && !z.CanSeePlayer, "saw before " + sawBefore + ", sees now " + z.CanSeePlayer);
            Check("Smoke blocks the line of sight both ways", GhostSmoke.BlocksSight(P.EyePosition, z.transform.position + Vector3.up * 1.6f), "");
            yield return Shot("22_smoke_corridor");
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            yield return Frames(10);
            Check("Inside the smoke you see nothing (screen haze)", GhostSmoke.Density(P.Cam.transform.position) > 0.7f, GhostSmoke.Density(P.Cam.transform.position).ToString("0.00"));
            yield return Shot("23_inside_smoke");
            Destroy(smoke.gameObject);
            Destroy(z.gameObject);
        }

        IEnumerator PitTest()
        {
            Check("Trap pits: 4 on floor 2, 4 on floor 1", TrapPit.All.FindAll(t => t.Floor == 1).Count == 4 && TrapPit.All.FindAll(t => t.Floor == 0).Count == 4, TrapPit.All.Count + " pits");
            var pit = TrapPit.All.Find(t => t.Floor == 0);
            var up = TrapPit.All.Find(t => t.Floor == 1);
            if (pit == null || up == null) yield break;
            // Look at one from the corridor first: the tape and the glow.
            P.Teleport(pit.Center + Vector3.right * 4.5f, -90f, 25f);
            yield return Frames(30);
            yield return Shot("24_spike_pit");
            int falls = TrapPit.Falls;
            float hp = P.Health;
            P.Teleport(pit.Center + Vector3.up * 0.3f, 0f, 0f);
            yield return new WaitForSeconds(1.2f);
            Check("Ground floor: you fall into the spike pit and get hurt", TrapPit.Falls == falls + 1 && P.Health < hp && P.transform.position.y < -1.5f,
                "y " + P.transform.position.y.ToString("0.0") + ", health " + hp.ToString("0") + " -> " + P.Health.ToString("0"));
            Hold(KeyCode.Space, true);
            yield return new WaitForSeconds(1.2f);
            Hold(KeyCode.Space, false);
            Check("Holding Space climbs out", P.transform.position.y > -0.3f, "y " + P.transform.position.y.ToString("0.0"));
            // The ledge round it is walkable.
            P.Heal(999f);
            P.Teleport(up.Center + Vector3.right * 1.25f + Vector3.forward * 1.25f, 0f, 0f);
            yield return Frames(30);
            Check("Floor 2: the ledge round a pit holds you", P.Floor == 1 && P.transform.position.y > LevelBuilder.Story - 0.3f, "y " + P.transform.position.y.ToString("0.0"));
            P.Teleport(up.Center + Vector3.up * 0.3f, 0f, 0f);
            yield return new WaitForSeconds(2f);
            Check("Floor 2: the rotten floor drops you to floor 1", P.Floor == 0 && P.transform.position.y < 0.5f, "floor " + (P.Floor + 1) + ", y " + P.transform.position.y.ToString("0.0"));
            NavMeshHit nh;
            Check("The dead can't walk over a pit", !NavMesh.SamplePosition(pit.Center, out nh, 0.6f, NavMesh.AllAreas) || nh.position.y < -1f, "");
            P.Heal(999f);
        }

        IEnumerator AnnexTest()
        {
            Note("Annex spots: " + LevelBuilder.AnnexSpots.Count + " (upper storey: " + LevelBuilder.AnnexSpots.FindAll(v => v.y > 3f).Count + ")");
            foreach (var a in LevelBuilder.AnnexAreas) Note("  " + a.name + "  x " + a.xz.xMin.ToString("0") + ".." + a.xz.xMax.ToString("0") + "  z " + a.xz.yMin.ToString("0") + ".." + a.xz.yMax.ToString("0"));
            Check("Annex: the mansion, chapel, hospital, security hall, smoking room and shrine are all there", LevelBuilder.AnnexAreas.Count >= 6, LevelBuilder.AnnexAreas.Count + " areas");
            Check("The mansion has a second storey (spots above 3 m)", LevelBuilder.AnnexSpots.FindAll(v => v.y > 3f).Count >= 8, "");
            var lift = GM.Level.elevator;
            Check("The lift is in the security hall's south-east corner, the generator in the north-west",
                lift != null && LevelBuilder.SecurityHall.size.x > 0f && lift.CarCenter.x > LevelBuilder.SecurityHall.center.x && lift.CarCenter.z < LevelBuilder.SecurityHall.center.z
                && PowerGrid.Generators[0] != null && PowerGrid.Generators[0].transform.position.x < LevelBuilder.SecurityHall.center.x && PowerGrid.Generators[0].transform.position.z > LevelBuilder.SecurityHall.center.z, "");
            // Walkable routes (the navmesh the dead use) from the tower's ground floor to every part of the annex.
            Vector3 from = LevelBuilder.CellCenter(0, 17, 20);
            var targets = new List<KeyValuePair<string, Vector3>>
            {
                new KeyValuePair<string, Vector3>("the lift", lift != null ? lift.CarCenter + Vector3.forward * 2.6f : Vector3.zero),
                new KeyValuePair<string, Vector3>("the generator", PowerGrid.Generators[0] != null ? PowerGrid.Generators[0].UsePosition : Vector3.zero),
                new KeyValuePair<string, Vector3>("the mansion foyer", new Vector3(37.5f, 0f, -8f)),
                new KeyValuePair<string, Vector3>("the chapel", new Vector3(82.5f, 0f, -8f)),
                new KeyValuePair<string, Vector3>("the shrine", new Vector3(1.5f, 0f, -16.5f)),
                new KeyValuePair<string, Vector3>("the smoking room", new Vector3(110f, 0f, -42f)),
                new KeyValuePair<string, Vector3>("the mezzanine", new Vector3(86f, LevelBuilder.Story * 0.9f, -42f)),
            };
            var ups = LevelBuilder.AnnexSpots.FindAll(v => v.y > 3.5f && v.z < -3f && v.x < 70f);
            // The mansion's upper storey: most of its rooms reachable from the grand staircase.
            int upOk = 0;
            foreach (var u in ups.ToArray())
            {
                NavMeshHit ua, ub;
                var upath = new NavMeshPath();
                if (NavMesh.SamplePosition(from, out ua, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(u, out ub, 1.5f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(ua.position, ub.position, NavMesh.AllAreas, upath) && upath.status == NavMeshPathStatus.PathComplete) { upOk++; if (upOk == 1) ups[0] = u; }
            }
            Check("The mansion's upper storey is walkable from the tower (via its staircase)", upOk >= 8 && upOk == ups.Count, upOk + " of " + ups.Count + " spots");
            foreach (var t in targets)
            {
                NavMeshHit a, b;
                var path = new NavMeshPath();
                bool ok = NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(t.Value, out b, 2.5f, NavMesh.AllAreas)
                          && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                Check("Walkable from the tower to " + t.Key, ok, path.status + " " + t.Value.ToString("F0"));
            }
            // Pictures: the foyer, upstairs, the chapel, the shrine, the hospital, the hall and its mezzanine.
            PowerGrid.Restore(0);
            if (!P.FlashlightOn) Press(KeyCode.F);
            yield return ShotAt("30_mansion_foyer", new Vector3(37.5f, 0f, -5f), 180f, 8f);
            if (ups.Count > 0) yield return ShotAt("31_mansion_upstairs", ups[0], 180f, 0f);
            yield return ShotAt("32_chapel", new Vector3(82.5f, 0f, -5.5f), 230f, 5f);
            yield return ShotAt("33_shrine", new Vector3(2.5f, 0f, -16.5f), 270f, 5f);
            yield return ShotAt("34_hospital", new Vector3(103.5f, 0f, -4f), 180f, 0f);
            yield return ShotAt("35_security_hall", new Vector3(102f, 0f, -34.5f), 225f, 5f);
            yield return ShotAt("36_mezzanine", new Vector3(86f, LevelBuilder.Story * 0.9f + 0.05f, -38f), 160f, 15f);
            yield return ShotAt("37_smoking_room", new Vector3(109.2f, 0f, -42f), 90f, 10f);
            yield return ShotAt("38_lift_shutter", new Vector3(102f, 0f, -45f), 160f, 5f);
            // The map, with the annex on the ground floor.
            P.Teleport(new Vector3(96f, 0f, -40f), 180f, 0f);
            yield return Frames(10);
            Press(KeyCode.M);
            yield return Frames(10);
            yield return Shot("39_map_annex");
            Press(KeyCode.M);
            yield return Frames(5);
            Check("In the annex the HUD says floor 1", P.Floor == 0, "floor " + (P.Floor + 1));
        }

        IEnumerator WingTest()
        {
            Check("Floor 2 has a north wing (gym, classrooms, auditorium)", LevelBuilder.AnnexAreas.FindAll(a => a.floor == 1).Count >= 6 && LevelBuilder.WingSpots.Count >= 10,
                LevelBuilder.AnnexAreas.FindAll(a => a.floor == 1).Count + " rooms, " + LevelBuilder.WingSpots.Count + " spots");
            Vector3 from = new Vector3(37.5f, LevelBuilder.Story, 70.5f);   // classroom 2B (the corridor pits split the floor for the dead)
            var targets = new[] { new Vector3(18f, LevelBuilder.Story, 88f), new Vector3(48f, LevelBuilder.Story, 88f), new Vector3(87f, LevelBuilder.Story, 86f), new Vector3(57f, LevelBuilder.Story, 88f) };
            string[] names = { "gymnasium", "classroom 2M", "auditorium", "science store" };
            for (int i = 0; i < targets.Length; i++)
            {
                NavMeshHit a, b;
                var path = new NavMeshPath();
                bool ok = NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(targets[i], out b, 2f, NavMesh.AllAreas)
                          && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                Check("Floor 2: walkable from classroom 2B to the wing's " + names[i], ok, path.status.ToString());
            }
            foreach (float dx in new[] { 37.5f, 85.5f })
            {
                RaycastHit h;
                Vector3 a0 = new Vector3(dx, LevelBuilder.Story + 1f, 66f), a1 = new Vector3(dx, LevelBuilder.Story + 1f, 77f);
                bool blocked = Physics.Linecast(a0, a1, out h, Gfx.WorldMask, QueryTriggerInteraction.Ignore);
                NavMeshHit nh;
                bool navIn = NavMesh.SamplePosition(new Vector3(dx, LevelBuilder.Story, 70.5f), out nh, 0.6f, NavMesh.AllAreas);
                bool navDoor = NavMesh.SamplePosition(new Vector3(dx, LevelBuilder.Story, 72f), out nh, 0.6f, NavMesh.AllAreas);
                bool navPass = NavMesh.SamplePosition(new Vector3(dx, LevelBuilder.Story, 73.5f), out nh, 0.6f, NavMesh.AllAreas);
                Note("Wing doorway x " + dx + ": " + (blocked ? "blocked by " + h.collider.name + " at z " + h.point.z.ToString("F2") : "clear") + "; navmesh room/door/passage " + navIn + "/" + navDoor + "/" + navPass);
            }
            yield return ShotAt("44_wing_door_2B", new Vector3(37.5f, LevelBuilder.Story, 66.5f), 0f, 0f);
            int wingEnemies = 0;
            foreach (var z in Zombie.All) if (LevelBuilder.InWing(z.transform.position)) wingEnemies++;
            Note("Enemies in the wing right now: " + wingEnemies);
            if (!P.FlashlightOn) Press(KeyCode.F);
            yield return ShotAt("40_wing_corridor", new Vector3(36f, LevelBuilder.Story, 76.7f), 90f, 0f);
            yield return ShotAt("41_gymnasium", new Vector3(26f, LevelBuilder.Story, 80f), 300f, 5f);
            yield return ShotAt("42_auditorium", new Vector3(87f, LevelBuilder.Story, 80f), 0f, 5f);
            Check("In the wing the HUD says floor 2", P.Floor == 1, "floor " + (P.Floor + 1));
            P.Teleport(new Vector3(48f, LevelBuilder.Story, 88f), 0f, 0f);
            yield return Frames(10);
            Press(KeyCode.M);
            yield return Frames(10);
            yield return Shot("43_map_wing");
            Press(KeyCode.M);
            yield return Frames(5);
        }

        /// <summary>
        /// Runs around every new area like a player would (sprinting in random directions, bumping into walls) and checks
        /// nobody falls through a floor or walks out of the building - and that each area is lit.
        /// </summary>
        IEnumerator Walkabout()
        {
            var net = SafetyNet.Instance;
            Check("Safety net is running", net != null, "");
            if (net == null) yield break;
            PowerGrid.Restore(0); PowerGrid.Restore(1);
            if (P.FlashlightOn) Press(KeyCode.F);
            yield return Frames(3);
            int before = net.Rescues;
            var starts = new List<Vector3>();
            var rnd = new System.Random(5);
            var spots = new List<Vector3>(LevelBuilder.AnnexSpots);
            for (int i = 0; i < 24 && spots.Count > 0; i++) { int k = rnd.Next(spots.Count); starts.Add(spots[k]); spots.RemoveAt(k); }
            var wing = new List<Vector3>(LevelBuilder.WingSpots);
            for (int i = 0; i < 10 && wing.Count > 0; i++) { int k = rnd.Next(wing.Count); starts.Add(wing[k]); wing.RemoveAt(k); }
            float lowest = 999f; Vector3 lowestAt = Vector3.zero;
            int darkSpots = 0;
            var dark = new List<string>();
            foreach (var st in starts)
            {
                P.Heal(999f);
                P.Teleport(st, (float)rnd.NextDouble() * 360f, 0f);
                yield return new WaitForSeconds(0.6f);
                // How lit is it here (no torch)?
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                float sum = 0f; int n = 0;
                for (int y = tex.height / 4; y < tex.height * 3 / 4; y += 16) for (int x = tex.width / 8; x < tex.width * 7 / 8; x += 16) { sum += tex.GetPixel(x, y).grayscale; n++; }
                Destroy(tex);
                float lum = sum / Mathf.Max(1, n);
                if (lum < 0.03f) { darkSpots++; dark.Add(st.ToString("F0") + " " + lum.ToString("0.000")); }
                for (int leg = 0; leg < 4; leg++)
                {
                    P.SetLook((float)rnd.NextDouble() * 360f, 0f);
                    Hold(KeyCode.W, true); Hold(KeyCode.LeftShift, leg % 2 == 0);
                    for (float t = 0f; t < 1.4f; t += Time.deltaTime)
                    {
                        if (P.transform.position.y < lowest) { lowest = P.transform.position.y; lowestAt = P.transform.position; }
                        yield return null;
                    }
                    ReleaseAll();
                }
            }
            ReleaseAll();
            int rescues = net.Rescues - before;
            string where = "";
            for (int i = before; i < net.RescuedFrom.Count && i < before + 6; i++) where += " " + net.RescuedFrom[i].ToString("F0");
            Check("Running round the new areas: never left the walkable area (" + starts.Count + " starts)", rescues == 0, rescues + " rescues" + where);
            Check("Never fell below the ground floor", lowest > -1.5f || TrapPit.All.Exists(t => t.Contains(lowestAt)), "lowest y " + lowest.ToString("0.0") + " at " + lowestAt.ToString("F0"));
            Check("Every new area is lit without the torch (no pitch-black spots)", darkSpots <= 2, darkSpots + " dark of " + starts.Count + (dark.Count > 0 ? ": " + string.Join("; ", dark.GetRange(0, Mathf.Min(4, dark.Count))) : ""));
            // The net itself: step off into the void between the buildings and get put back.
            P.Teleport(new Vector3(60f, 0f, -30f), 0f, 0f);   // spot check
            Vector3 outside = new Vector3(-30f, 0f, -45f);     // bare ground slab, outside every building
            var r0 = net.Rescues;
            P.Teleport(outside, 0f, 0f);
            yield return new WaitForSeconds(1.5f);
            Check("Standing outside the buildings puts you back inside", net.Rescues == r0 + 1 && Vector3.Distance(P.transform.position, outside) > 5f, P.transform.position.ToString("F0"));
        }

        IEnumerator ShotAt(string name, Vector3 feet, float yaw, float pitch)
        {
            P.Teleport(feet, yaw, pitch);
            yield return new WaitForSeconds(1.2f);
            yield return Shot(name);
            Note(name + ": stood at " + P.transform.position.ToString("F1") + " (floor " + (P.Floor + 1) + ")");
        }

        // ---------------- movement ----------------

        IEnumerator Movement()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 5), 90f, 0f); // F4 corridor, facing east down its length
            yield return Frames(20);
            Hold(KeyCode.W, true);
            float best = 0f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { best = Mathf.Max(best, Flat(P.Velocity)); yield return null; }
            Check("Walk speed ~4.8 m/s", Mathf.Abs(best - 4.8f) < 0.4f, best.ToString("0.00") + " m/s");
            Hold(KeyCode.LeftShift, true);
            best = 0f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { best = Mathf.Max(best, Flat(P.Velocity)); yield return null; }
            Check("Sprint speed ~8.2 m/s", Mathf.Abs(best - 8.2f) < 0.5f && P.IsSprinting, best.ToString("0.00") + " m/s");
            ReleaseAll();
            Press(KeyCode.C);
            yield return Frames(30);
            Hold(KeyCode.S, true);
            best = 0f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { best = Mathf.Max(best, Flat(P.Velocity)); yield return null; }
            Check("Crouch walk ~2.6 m/s", P.IsCrouching && Mathf.Abs(best - 2.6f) < 0.4f, best.ToString("0.00") + " m/s");
            ReleaseAll();
            Press(KeyCode.C);
            yield return Frames(30);
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // ---------------- rifle ----------------

        IEnumerator Rifle()
        {
            var gun = P.Gun;
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            yield return Frames(10);
            gun.SetAmmo(MP5.MagSize, 150);
            Hold(KeyCode.Mouse0, true);
            float t0 = Time.time;
            while (gun.Ammo > 0 && Time.time - t0 < 5f) yield return null;
            float emptied = Time.time;
            Check("Full auto empties a 30-round mag in ~2.3 s", gun.Ammo == 0 && emptied - t0 < 2.8f, (emptied - t0).ToString("0.00") + " s");
            yield return null;
            Check("Auto reload starts when the mag runs dry", gun.IsReloading, "");
            Hold(KeyCode.Mouse0, false);
            while (gun.IsReloading && Time.time - emptied < 3f) yield return null;
            float fast = Time.time - emptied;
            Check("Fast auto reload ~0.7 s", gun.Ammo == MP5.MagSize && Mathf.Abs(fast - 0.7f) < 0.15f, fast.ToString("0.00") + " s, ammo " + gun.Ammo + "/" + gun.Reserve);
            Check("Reserve drops by 30", gun.Reserve == 120, "reserve " + gun.Reserve);

            Hold(KeyCode.Mouse0, true);
            yield return new WaitForSeconds(0.4f);
            Hold(KeyCode.Mouse0, false);
            int before = gun.Ammo;
            Press(KeyCode.R);
            yield return Frames(2);
            float r0 = Time.time;
            Check("R starts a manual reload", gun.IsReloading, "mag " + before);
            yield return Frames(20);
            yield return Shot("02_reload");
            while (gun.IsReloading && Time.time - r0 < 3f) yield return null;
            float manual = Time.time - r0;
            Check("Manual reload ~1.1 s", gun.Ammo == MP5.MagSize && Mathf.Abs(manual - 1.1f) < 0.15f, manual.ToString("0.00") + " s");

            gun.SetAmmo(0, 40);
            Hold(KeyCode.Mouse0, true);
            yield return Frames(3);
            Check("Pulling the trigger on an empty mag reloads", gun.IsReloading, "");
            Hold(KeyCode.Mouse0, false);
            while (gun.IsReloading) yield return null;
            Check("Partial reserve fills the mag", gun.Ammo == 30 && gun.Reserve == 10, gun.Ammo + "/" + gun.Reserve);
            gun.SetAmmo(30, 150);

            Hold(KeyCode.Mouse1, true);
            yield return new WaitForSeconds(0.5f);
            Check("Right click aims down sights", gun.IsAiming && P.Cam.fieldOfView < 53f, "fov " + P.Cam.fieldOfView.ToString("0.0"));
            yield return Shot("01_ads");
            Hold(KeyCode.Mouse1, false);
            yield return new WaitForSeconds(0.4f);
            Check("Releasing aim returns to hip", !gun.IsAiming && P.Cam.fieldOfView > 67f, "fov " + P.Cam.fieldOfView.ToString("0.0"));
            yield return Shot("01_hip");
        }

        // ---------------- cover ----------------

        IEnumerator Cover()
        {
            // Find a low and a tall cover spot by trying Q around walkable cells on floors 4 and 3.
            bool doneLow = false, doneTall = false;
            int tries = 0;
            for (int f = 3; f >= 3 && !(doneLow && doneTall); f--) // floor 4 only: lower floors would save a checkpoint
                for (int r = 1; r < LevelBuilder.Rows - 1 && !(doneLow && doneTall); r += 1)
                    for (int c = 1; c < LevelBuilder.Cols - 1 && !(doneLow && doneTall); c += 2)
                    {
                        if (!LevelBuilder.Walkable(f, r, c)) continue;
                        NavMeshHit hit;
                        if (!NavMesh.SamplePosition(LevelBuilder.CellCenter(f, r, c), out hit, 1f, NavMesh.AllAreas)) continue;
                        P.Teleport(hit.position, 0f, 0f);
                        yield return Frames(3);
                        Press(KeyCode.Q);
                        yield return Frames(2);
                        tries++;
                        if (!P.InCover) continue;
                        bool low = P.CoverIsLow;
                        if ((low && doneLow) || (!low && doneTall)) { Press(KeyCode.Q); yield return Frames(2); continue; }
                        if (low) doneLow = true; else doneTall = true;
                        yield return CoverChecks(low, f, r, c);
                    }
            Check("Found both low and tall cover", doneLow && doneTall, "low " + doneLow + ", tall " + doneTall + ", " + tries + " tries");

            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            yield return Frames(5);
        }

        IEnumerator CoverChecks(bool low, int f, int r, int c)
        {
            string kind = low ? "Low cover" : "Tall cover";
            Note(kind + " at floor " + (f + 1) + " row " + r + " col " + c);
            yield return new WaitForSeconds(0.6f);
            Check(kind + ": Q snaps into cover", P.InCover, "");
            if (low) Check(kind + ": crouched behind it", P.IsCrouching, "");
            yield return new WaitForSeconds(0.3f);
            yield return Shot(low ? "03_cover_low" : "03_cover_tall");

            Hold(KeyCode.Mouse1, true);
            yield return new WaitForSeconds(0.6f);
            Check(kind + ": right click peeks and aims", P.IsPeeking && P.Gun.IsAiming, "peeking " + P.IsPeeking + ", aiming " + P.Gun.IsAiming);
            if (low) Check(kind + ": pops up to aim", !P.IsCrouching, "");
            yield return Shot(low ? "03_cover_low_popup" : "03_cover_tall_peek");
            int ammo = P.Gun.Ammo;
            Hold(KeyCode.Mouse0, true);
            yield return new WaitForSeconds(0.3f);
            Hold(KeyCode.Mouse0, false);
            Check(kind + ": can fire from cover", P.Gun.Ammo < ammo, (ammo - P.Gun.Ammo) + " shots");
            Hold(KeyCode.Mouse1, false);
            yield return new WaitForSeconds(0.5f);
            Check(kind + ": back in cover after releasing", P.InCover && !P.IsPeeking && (!low || P.IsCrouching), "");

            if (!low)
            {
                Hold(KeyCode.A, true);
                yield return new WaitForSeconds(0.4f);
                float lx = P.Cam.transform.parent.localPosition.x;
                Hold(KeyCode.A, false);
                Hold(KeyCode.D, true);
                yield return new WaitForSeconds(0.4f);
                float rx = P.Cam.transform.parent.localPosition.x;
                Hold(KeyCode.D, false);
                Check(kind + ": A or D leans out of cover", lx < -0.1f || rx > 0.1f, "left " + lx.ToString("0.00") + ", right " + rx.ToString("0.00"));
                yield return new WaitForSeconds(0.4f);
            }

            Vector3 pos = P.transform.position;
            Hold(KeyCode.W, true);
            yield return new WaitForSeconds(0.3f);
            Hold(KeyCode.W, false);
            Check(kind + ": W does not walk you out of cover", P.InCover && Flat(P.transform.position - pos) < 0.2f, "");
            Press(KeyCode.Q);
            yield return Frames(3);
            Check(kind + ": Q leaves cover", !P.InCover, "");
            Hold(KeyCode.S, true);
            yield return new WaitForSeconds(0.3f);
            Hold(KeyCode.S, false);
            Check(kind + ": can walk again after leaving", Flat(P.transform.position - pos) > 0.3f, Flat(P.transform.position - pos).ToString("0.00") + " m");
            // Stand back up if the low cover left us crouched.
            yield return new WaitForSeconds(0.4f);
            Check(kind + ": standing again after leaving", !P.IsCrouching, "");
        }

        // ---------------- ghost ----------------

        IEnumerator GhostTest()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            if (P.FlashlightOn) Press(KeyCode.F); // the torch also burns ghosts; test bullets alone
            yield return Frames(3);
            P.Gun.SetAmmo(30, 150);
            Ghost.Skills = false;   // bullets alone: no vanishing mid-test
            var ghost = Ghost.Spawn(P.transform.position + Vector3.right * 9f + Vector3.up * 1.4f, null, false);
            yield return Frames(20);
            yield return Shot("04_ghost");
            int shots = 0;
            float t0 = Time.time;
            while (ghost.IsActive && Time.time - t0 < 6f && shots < 30)
            {
                AimAt(ghost.transform.position + Vector3.up * 0.1f);
                Hold(KeyCode.Mouse1, true);
                yield return Frames(2);
                AimAt(ghost.transform.position + Vector3.up * 0.1f);
                int a = P.Gun.Ammo;
                Hold(KeyCode.Mouse0, true);
                yield return null;
                Hold(KeyCode.Mouse0, false);
                yield return Frames(6);
                if (P.Gun.Ammo < a) shots++;
            }
            Hold(KeyCode.Mouse1, false);
            Check("Shooting scatters a ghost in about 5 hits", !ghost.IsActive && shots >= 5 && shots <= 7, shots + " shots fired");
            Check("Scatter toast shown", GM.ToastText.Contains("scattered"), "'" + GM.ToastText + "'");
            Destroy(ghost.gameObject);
            Ghost.Skills = true;
            Press(KeyCode.F);
            yield return Frames(3);
            Check("Flashlight back on", P.FlashlightOn, "");
        }

        IEnumerator GhostModel()
        {
            // A drifting ghost frozen 3.5 m in front of you, so the screenshot shows the model.
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            var ghost = Ghost.Spawn(P.transform.position + Vector3.right * 3.5f + Vector3.up * 1.25f, null, false);
            ghost.transform.rotation = Quaternion.LookRotation(Vector3.left);
            yield return Frames(30);
            ghost.enabled = false;
            var model = ghost.transform.Find("Shape/GhostModel");
            int skinned = model != null ? model.GetComponentsInChildren<SkinnedMeshRenderer>().Length : 0;
            Check("Ghost uses the DOE Ghost model", skinned == 4, skinned + " skinned meshes");
            var anim = model != null ? model.GetComponentInChildren<Animation>() : null;
            Check("Ghost plays its Stalking clip", anim != null && anim.isPlaying, anim != null ? anim.clip != null ? "clips ok" : "no default clip" : "no Animation");
            Bounds b = new Bounds();
            bool any = false;
            if (model != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            float floorY = P.transform.position.y;
            Check("Ghost model is about 1.7 m tall, standing on the floor", any && b.size.y > 1.4f && b.size.y < 2.2f && Mathf.Abs(b.min.y - floorY) < 0.1f,
                any ? "height " + b.size.y.ToString("F2") + " m, feet " + (b.min.y - floorY).ToString("F2") + " m up" : "no renderers");
            AimAt(ghost.transform.position + Vector3.down * 0.3f);
            yield return Frames(10);
            yield return Shot("04b_ghost_model");
            Destroy(ghost.gameObject);
            yield return Frames(3);

            // Now one walking at you down the corridor: legs swing, feet stay on the floor.
            if (P.FlashlightOn) Press(KeyCode.F);   // the torch would pin it in place
            Ghost.Skills = false;
            ghost = Ghost.Spawn(P.transform.position + Vector3.right * 9f + Vector3.up * Ghost.FeetBelow, null, false);
            yield return Frames(3);
            ghost.TakeHit(0f);   // it notices you
            float minThigh = 0f, maxThigh = 0f, worstFloor = 0f, t0 = Time.time;
            string worstInfo = "";
            bool shot = false;
            while (Time.time - t0 < 1.6f && ghost != null && ghost.Body != null)
            {
                yield return new WaitForEndOfFrame();   // the pose as drawn this frame
                if (ghost == null || Time.time - t0 < 0.3f) continue;   // getting into stride
                minThigh = Mathf.Min(minThigh, ghost.Body.LeftThighAngle);
                maxThigh = Mathf.Max(maxThigh, ghost.Body.LeftThighAngle);
                // The lowest point of the actual skinned feet (renderer bounds are only a loose box).
                float lowest = LowestVertex(ghost.Body, "Body") - floorY;
                if (Mathf.Abs(lowest) > Mathf.Abs(worstFloor)) { worstFloor = lowest; worstInfo = "phase " + ghost.Body.LeftThighAngle.ToString("F0") + " deg"; }
                if (!shot && Vector3.Distance(ghost.transform.position, P.transform.position) < 5.5f)
                {
                    // Three frames a step apart, to see the legs move.
                    shot = true;
                    AimAt(ghost.transform.position + Vector3.down * 0.35f);
                    yield return Shot("04d_ghost_walk_1");
                    yield return Frames(7);
                    AimAt(ghost.transform.position + Vector3.down * 0.35f);
                    yield return Shot("04d_ghost_walk_2");
                    yield return Frames(7);
                    AimAt(ghost.transform.position + Vector3.down * 0.35f);
                    yield return Shot("04d_ghost_walk_3");
                }
            }
            Check("Walking ghost swings its legs", maxThigh - minThigh > 40f, "left thigh " + minThigh.ToString("F0") + ".." + maxThigh.ToString("F0") + " deg");
            Check("Walking ghost keeps its feet on the floor", Mathf.Abs(worstFloor) < 0.06f, "worst " + (worstFloor * 100f).ToString("+0.0;-0.0") + " cm (+ above, - into the floor)");
            Note("worst frame: " + worstInfo);
            if (ghost != null) Destroy(ghost.gameObject);
            Ghost.Skills = true;
            Press(KeyCode.F);
            yield return Frames(3);
        }

        static Mesh baked;

        static float LowestVertex(GhostBody body, string meshName)
        {
            float low = float.MaxValue;
            if (baked == null) baked = new Mesh();
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.name != meshName) continue;
                smr.BakeMesh(baked, true);
                var t = smr.transform;
                foreach (var v in baked.vertices) low = Mathf.Min(low, (t.position + t.rotation * v).y);
            }
            return low;
        }

        IEnumerator GhostSkills()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            if (P.FlashlightOn) Press(KeyCode.F); // the torch would burn it
            yield return Frames(3);
            int vanishes = Ghost.Vanishes, reappears = Ghost.Reappears, calls = GhostSmoke.Calls;
            var ghost = Ghost.Spawn(P.transform.position + Vector3.right * 6f + Vector3.up * 1.4f, null, false);
            yield return Frames(5);
            ghost.ReadySkills();
            ghost.TakeHit(0f);   // it notices you
            float t0 = Time.time;
            bool sawInvisible = false;
            while (Time.time - t0 < 14f && Ghost.Vanishes == vanishes) { P.Heal(999f); yield return null; }
            Check("Hunting ghost vanishes", Ghost.Vanishes > vanishes, (Time.time - t0).ToString("F1") + " s into the hunt");
            Check("It left a smoke cloud when close", GhostSmoke.All.Count == 1, GhostSmoke.All.Count + " clouds");
            t0 = Time.time;
            while (Time.time - t0 < 9f && Ghost.Reappears == reappears)
            {
                if (ghost.IsInvisible) sawInvisible = true;
                P.Heal(999f);
                yield return null;
            }
            Check("Vanished ghost is invisible (no bullets, no torch)", sawInvisible, "");
            float d = Vector3.Distance(ghost.transform.position, P.ChestPosition);
            Check("It reappears close to you", Ghost.Reappears > reappears && d < 5f, d.ToString("F1") + " m away");
            Destroy(ghost.gameObject);

            if (GhostSmoke.All.Count > 0)
            {
                var smoke = GhostSmoke.All[0];
                P.Teleport(smoke.transform.position + Vector3.left * 6f, 90f, 0f);
                yield return Frames(5);
                AimAt(smoke.DecoyPosition + Vector3.down * 0.3f);
                yield return new WaitForSeconds(Mathf.Max(0f, 7.6f - smoke.Age));
                Check("Smoke sends out fake noises (false signal)", GhostSmoke.Calls > calls, (GhostSmoke.Calls - calls) + " calls in " + smoke.Age.ToString("F0") + " s");
                Check("A decoy ghost stands in the smoke", smoke.DecoyVisible, "");
                Check("Smoke lasts a minute", Mathf.Approximately(GhostSmoke.Life, 60f), GhostSmoke.Life + " s");
                yield return Shot("04c_ghost_smoke");
                Press(KeyCode.F);   // the torch shows it up
                AimAt(smoke.DecoyPosition);
                float t1 = Time.time;
                while (Time.time - t1 < 3f && smoke.DecoyVisible) { AimAt(smoke.DecoyPosition); yield return null; }
                Check("Flashlight exposes the decoy", !smoke.DecoyVisible && GM.ToastText.Contains("smoke"), "'" + GM.ToastText + "'");
                foreach (var s in GhostSmoke.All.ToArray()) Destroy(s.gameObject);
            }
            if (!P.FlashlightOn) Press(KeyCode.F);
            yield return Frames(3);
        }

        IEnumerator PowerCut()
        {
            int gens = 0;
            for (int f = 0; f < PowerGrid.Floors; f++) if (PowerGrid.Generators[f] != null) gens++;
            Check("A generator on every floor", gens == PowerGrid.Floors, gens + " generators");
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            yield return new WaitForSeconds(0.5f);
            int litBefore = LitLamps(3);
            PowerGrid.Cut(3, P.transform.position);
            yield return new WaitForSeconds(PowerGrid.Transition + 0.6f);
            int litAfter = LitLamps(3);
            Check("Power cut darkens the floor", !PowerGrid.IsOn(3) && litBefore > 0 && litAfter == 0, litBefore + " lamps lit -> " + litAfter);
            Check("Other floors keep their power", PowerGrid.IsOn(2) && PowerGrid.IsOn(0), "");
            yield return Shot("06_power_out");

            var gen = PowerGrid.Generators[3];
            if (gen == null) yield break;
            Vector3 toGen = gen.transform.position - gen.UsePosition;
            P.Teleport(gen.UsePosition, Mathf.Atan2(toGen.x, toGen.z) * Mathf.Rad2Deg, 15f);
            yield return new WaitForSeconds(0.3f);
            Check("Generator prompt in reach", gen.PlayerNear, Vector3.Distance(P.transform.position, gen.transform.position).ToString("F1") + " m");
            yield return Shot("07_generator");
            Press(KeyCode.E);
            yield return Frames(3);
            Check("E starts the generator", gen.Starting, "");
            yield return new WaitForSeconds(Generator.StartTime + PowerGrid.Transition + 0.6f);
            int litBack = LitLamps(3);
            Check("Generator restores the power", PowerGrid.IsOn(3) && litBack > 0, litBack + " lamps lit");
            yield return Shot("08_power_back");
        }

        int LitLamps(int floor)
        {
            int n = 0;
            foreach (var tl in GM.Level.lights) if (tl.floor == floor && tl.electric && tl.light != null && tl.light.enabled) n++;
            return n;
        }

        // ---------------- bosses ----------------

        IEnumerator Boss(int variant)
        {
            string name = "Boss " + (variant + 1);
            P.Heal(999f);
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            Zombie.NextBossVariant = variant;
            var boss = Zombie.Spawn(EnemyKind.Brute, LevelBuilder.CellCenter(3, 6, 12), null);
            yield return Frames(5);
            boss.TakeDamage(1f, boss.transform.position, Vector3.forward, false); // wake it
            float hp0 = P.Health;
            float topSpeed = 0f, t0 = Time.time;
            bool shot = false;
            var agent = boss.GetComponent<NavMeshAgent>();
            while (Time.time - t0 < 9f && P.Health >= hp0)
            {
                AimAt(boss.transform.position + Vector3.up * 1.6f);
                topSpeed = Mathf.Max(topSpeed, Flat(agent.velocity));
                if (!shot && Vector3.Distance(boss.transform.position, P.transform.position) < 8f) { shot = true; yield return Shot("05_boss" + (variant + 1) + "_charge"); }
                yield return null;
            }
            float reached = Time.time - t0;
            Check(name + ": charges at 6.6-7.3 m/s", topSpeed > 6.2f && topSpeed < 7.6f, topSpeed.ToString("0.00") + " m/s");
            Check(name + ": smash hits for 32", Mathf.Abs((hp0 - P.Health) - 32f) < 1f, "took " + (hp0 - P.Health).ToString("0") + " after " + reached.ToString("0.0") + " s");
            yield return Shot("05_boss" + (variant + 1) + "_smash");

            // Kill it: aimed bursts until it drops.
            P.Gun.SetAmmo(30, 300);
            int startRounds = P.Gun.Ammo + P.Gun.Reserve;
            t0 = Time.time;
            Hold(KeyCode.Mouse1, true);
            int diag = 0;
            while (!boss.IsDead && Time.time - t0 < 15f)
            {
                P.Heal(999f);
                AimAt(boss.transform.position + Vector3.up * 1.4f);
                RaycastHit h;
                var ray = new Ray(P.Cam.transform.position, P.Cam.transform.forward);
                bool hit = Physics.Raycast(ray, out h, 50f, Gfx.ShootMask, QueryTriggerInteraction.Ignore);
                bool onBoss = hit && h.collider.GetComponentInParent<Zombie>() == boss;
                if (diag++ % 60 == 0)
                {
                    var ba = boss.GetComponent<NavMeshAgent>();
                    Note(name + " aim ray hits " + (hit ? h.collider.name + " at " + h.distance.ToString("0.00") + " m" : "nothing") +
                         "; boss " + Flat(boss.transform.position - P.transform.position).ToString("0.00") + " m away, player cell " + LevelBuilder.CellOf(P.transform.position) +
                         " boss cell " + LevelBuilder.CellOf(boss.transform.position) + (ba.enabled ? " path " + ba.pathStatus + " remaining " + ba.remainingDistance.ToString("0.0") : ""));
                }
                // Like a player: only shoot when the boss is actually in the sights.
                Hold(KeyCode.Mouse0, onBoss || Physics.CheckSphere(P.Cam.transform.position, 0.05f, 1 << Gfx.EnemyLayer));
                yield return null;
            }
            ReleaseAll();
            int used = startRounds - (P.Gun.Ammo + P.Gun.Reserve) + (boss.IsDead ? 15 : 0);   // a boss kill pays 15 rounds back
            Check(name + ": dies to most of a magazine or more", boss.IsDead && used >= 16, used + " rounds, " + (Time.time - t0).ToString("0.0") + " s");
            yield return Frames(5);
            Pickup drop = null;
            foreach (var pk in Pickup.All) if (pk.kind == PickupKind.Ammo && pk.rounds == 45 && Vector3.Distance(pk.transform.position, boss.transform.position) < 3f) drop = pk;
            Check(name + ": drops a 45-round ammo box", drop != null, "");
            if (drop != null)
            {
                int res = P.Gun.Reserve;
                P.Teleport(drop.transform.position - Vector3.up * 0.9f, 90f, 0f);
                yield return Frames(10);
                Check(name + ": ammo box pickup adds 45", P.Gun.Reserve == res + 45, res + " -> " + P.Gun.Reserve);
            }
            if (boss != null) Destroy(boss.gameObject);
            P.Heal(999f);
        }

        // ---------------- chasers: scary zombie, parasite (walkers), the human deer (runner) ----------------

        IEnumerator Chaser(EnemyKind kind, int variant, string name, float speed)
        {
            P.Heal(999f);
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f); // F4 corridor
            yield return Frames(10);
            Zombie.ForceVariant = variant;
            var z = Zombie.Spawn(kind, LevelBuilder.CellCenter(3, 6, 13), null);
            Zombie.ForceVariant = -1;
            Check(name + ": wears its model", z.ModelVariant == variant, "variant " + z.ModelVariant);
            yield return Frames(5);
            z.TakeDamage(1f, z.transform.position, Vector3.forward, false); // wake it
            float hp0 = P.Health, topSpeed = 0f, t0 = Time.time;
            bool shot = false;
            var agent = z.GetComponent<NavMeshAgent>();
            while (Time.time - t0 < 9f && P.Health >= hp0)
            {
                AimAt(z.transform.position + Vector3.up * 1.2f);
                topSpeed = Mathf.Max(topSpeed, Flat(agent.velocity));
                if (!shot && Vector3.Distance(z.transform.position, P.transform.position) < 5f) { shot = true; yield return Shot("05_" + name.ToLower().Replace(' ', '_') + "_chase"); }
                yield return null;
            }
            Check(name + ": chases at ~" + speed.ToString("0.0") + " m/s", Mathf.Abs(topSpeed - speed) < 0.6f, topSpeed.ToString("0.00") + " m/s");
            Check(name + ": hits you when it catches you", P.Health < hp0, "took " + (hp0 - P.Health).ToString("0") + " after " + (Time.time - t0).ToString("0.0") + " s");

            // Sprinting (8.2 m/s) still pulls away from it.
            P.Heal(999f);
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            z.GetComponent<NavMeshAgent>().Warp(LevelBuilder.CellCenter(3, 6, 5));
            yield return Frames(3);
            z.TakeDamage(1f, z.transform.position, Vector3.forward, false);
            float d0 = Flat(z.transform.position - P.transform.position);
            Hold(KeyCode.W, true); Hold(KeyCode.LeftShift, true);
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) { P.Heal(999f); yield return null; }
            float d1 = Flat(z.transform.position - P.transform.position);
            ReleaseAll();
            Check(name + ": a sprint outruns it", d1 > d0 + 0.5f, d0.ToString("0.0") + " m -> " + d1.ToString("0.0") + " m");

            // Kill it.
            P.Heal(999f);
            P.Gun.SetAmmo(30, 300);
            t0 = Time.time;
            Hold(KeyCode.Mouse1, true);
            while (!z.IsDead && Time.time - t0 < 12f)
            {
                P.Heal(999f);
                AimAt(z.transform.position + Vector3.up * 0.9f);
                RaycastHit h;
                bool on = Physics.Raycast(new Ray(P.Cam.transform.position, P.Cam.transform.forward), out h, 50f, Gfx.ShootMask, QueryTriggerInteraction.Ignore) &&
                          h.collider.GetComponentInParent<Zombie>() == z;
                Hold(KeyCode.Mouse0, on || Physics.CheckSphere(P.Cam.transform.position, 0.05f, 1 << Gfx.EnemyLayer));
                yield return null;
            }
            ReleaseAll();
            Check(name + ": can be shot dead", z.IsDead, (Time.time - t0).ToString("0.0") + " s");
            yield return Frames(30);
            if (z != null) Destroy(z.gameObject);
            P.Heal(999f);
        }

        // ---------------- the zombie dog ----------------

        IEnumerator DogTest()
        {
            P.Heal(999f);
            // 1. Hide in a locker; a dog nearby smells you, barks, and the zombie it calls tears the door open.
            HidingSpot spot = null;
            foreach (var s in HidingSpot.All)
                if ((s.kind == HidingSpot.Kind.Locker || s.kind == HidingSpot.Kind.Wardrobe || s.kind == HidingSpot.Kind.Stall) && !s.Safe && !s.Occupied &&
                    NavMesh.SamplePosition(s.ExitPosition + s.transform.forward * 4f, out _, 0.6f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(s.ExitPosition + s.transform.forward * 2.5f, out _, 0.6f, NavMesh.AllAreas)) { spot = s; break; }
            if (spot != null) Note("Hiding in a " + spot.kind + " at " + spot.ExitPosition.ToString("F1"));
            Check("Dog: found a locker to hide in", spot != null, "");
            if (spot != null)
            {
                P.Teleport(spot.ExitPosition, Quaternion.LookRotation(-spot.transform.forward).eulerAngles.y, 0f);
                yield return Frames(5);
                Press(KeyCode.E);
                yield return Frames(10);
                Check("Dog: player is hidden", P.IsHidden && !P.SeenHiding, "");
                var walker = Zombie.Spawn(EnemyKind.Walker, spot.ExitPosition + spot.transform.forward * 2.5f, null);
                var dog = Zombie.Spawn(EnemyKind.Dog, spot.ExitPosition + spot.transform.forward * 4f, null);
                Check("Dog: wears the dog model", dog.HasDogModel, "");
                float t0 = Time.time;
                bool shot = false;
                while (Time.time - t0 < 10f && !P.SeenHiding) { P.Heal(999f); yield return null; }
                Check("Dog: smells you in the locker and barks at it", P.SeenHiding && dog.Calls > 0, (Time.time - t0).ToString("0.0") + " s, " + dog.Calls + " calls");
                Check("Dog: its howl sets the zombie on you", walker.IsChasing, "");
                while (Time.time - t0 < 20f && P.IsHidden)
                {
                    P.Heal(999f);
                    if (!shot && Time.time - t0 > 1.5f && Vector3.Distance(dog.transform.position, spot.ExitPosition) < 2f) { shot = true; yield return Shot("05_dog_barking"); }
                    yield return null;
                }
                Check("Dog: the zombie it called tears the door open", !P.IsHidden, (Time.time - t0).ToString("0.0") + " s");
                Destroy(walker.gameObject);
                Destroy(dog.gameObject);
                if (P.IsHidden) P.Unhide();
                yield return Frames(10);
            }

            // 2. It runs with its boss: wake the brute and the dog joins the hunt.
            P.Heal(999f);
            P.Teleport(LevelBuilder.CellCenter(3, 6, 4), 90f, 0f);
            yield return Frames(10);
            var brute = Zombie.Spawn(EnemyKind.Brute, LevelBuilder.CellCenter(3, 6, 16), null);
            var pup = Zombie.Spawn(EnemyKind.Dog, LevelBuilder.CellCenter(3, 6, 17), null);
            yield return new WaitForSeconds(3.5f); // it finds its leader
            brute.Alert(P.transform.position);
            float tb = Time.time;
            while (Time.time - tb < 2f && !pup.IsChasing) yield return null;
            Check("Dog: runs with the boss when it hunts you", pup.IsChasing, (Time.time - tb).ToString("0.0") + " s");
            Destroy(brute.gameObject);
            Destroy(pup.gameObject);
            yield return Frames(5);

            // 3. Chase, bite, outrun, kill - the same checks as the other chasers.
            P.Heal(999f);
            P.Teleport(LevelBuilder.CellCenter(3, 6, 6), 90f, 0f);
            yield return Frames(10);
            var z = Zombie.Spawn(EnemyKind.Dog, LevelBuilder.CellCenter(3, 6, 15), null);
            yield return Frames(5);
            z.TakeDamage(1f, z.transform.position, Vector3.forward, false); // wake it
            float hp0 = P.Health, topSpeed = 0f, tc = Time.time;
            bool chaseShot = false;
            var agent = z.GetComponent<NavMeshAgent>();
            while (Time.time - tc < 9f && P.Health >= hp0)
            {
                AimAt(z.transform.position + Vector3.up * 0.5f);
                topSpeed = Mathf.Max(topSpeed, Flat(agent.velocity));
                if (!chaseShot && Vector3.Distance(z.transform.position, P.transform.position) < 4.5f) { chaseShot = true; yield return Shot("05_dog_chase"); }
                yield return null;
            }
            Check("Dog: chases at ~7.0 m/s", Mathf.Abs(topSpeed - 7f) < 0.6f, topSpeed.ToString("0.00") + " m/s");
            Check("Dog: bites you when it catches you", P.Health < hp0, "took " + (hp0 - P.Health).ToString("0") + " after " + (Time.time - tc).ToString("0.0") + " s");

            P.Heal(999f);
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            agent.Warp(LevelBuilder.CellCenter(3, 6, 5));
            yield return Frames(3);
            z.TakeDamage(1f, z.transform.position, Vector3.forward, false);
            float d0 = Flat(z.transform.position - P.transform.position);
            Hold(KeyCode.W, true); Hold(KeyCode.LeftShift, true);
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) { P.Heal(999f); yield return null; }
            float d1 = Flat(z.transform.position - P.transform.position);
            ReleaseAll();
            Check("Dog: a sprint outruns it", d1 > d0 + 0.5f, d0.ToString("0.0") + " m -> " + d1.ToString("0.0") + " m");

            P.Heal(999f);
            P.Gun.SetAmmo(30, 300);
            Note("Dog health before the kill: " + z.Health.ToString("0") + (z.IsDead ? " (already dead)" : ""));
            tc = Time.time;
            Hold(KeyCode.Mouse1, true);
            while (!z.IsDead && Time.time - tc < 12f)
            {
                P.Heal(999f);
                AimAt(z.transform.position + Vector3.up * 0.5f);
                RaycastHit h;
                bool on = Physics.Raycast(new Ray(P.Cam.transform.position, P.Cam.transform.forward), out h, 50f, Gfx.ShootMask, QueryTriggerInteraction.Ignore) &&
                          h.collider.GetComponentInParent<Zombie>() == z;
                Hold(KeyCode.Mouse0, on || Physics.CheckSphere(P.Cam.transform.position, 0.05f, 1 << Gfx.EnemyLayer));
                yield return null;
            }
            ReleaseAll();
            Check("Dog: can be shot dead", z.IsDead, (Time.time - tc).ToString("0.0") + " s");
            yield return new WaitForSeconds(1.5f);
            if (z != null) { AimAt(z.transform.position); yield return Shot("05_dog_dead"); Destroy(z.gameObject); }
            P.Heal(999f);
        }

        IEnumerator PointBlank()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 4), 90f, 0f);
            yield return Frames(10);
            var boss = Zombie.Spawn(EnemyKind.Brute, P.transform.position + Vector3.right * 3f, null);
            yield return Frames(3);
            boss.GetComponent<NavMeshAgent>().enabled = false;
            boss.enabled = false;
            boss.transform.position = P.transform.position + Vector3.right * 0.25f; // camera ends up inside its hit box
            Physics.SyncTransforms();
            yield return Frames(2);
            P.SetLook(90f, 0f);
            float hp = boss.Health;
            Hold(KeyCode.Mouse0, true);
            yield return null;
            Hold(KeyCode.Mouse0, false);
            yield return Frames(3);
            Check("Point-blank shots hurt a brute standing on top of you", boss.Health < hp, hp + " -> " + boss.Health);
            Destroy(boss.gameObject);
        }

        // ---------------- map ----------------

        IEnumerator MapTest()
        {
            P.Teleport(LevelBuilder.CellCenter(3, 6, 8), 90f, 0f);
            yield return Frames(10);
            yield return Shot("06_minimap");
            Press(KeyCode.M);
            yield return Frames(10);
            yield return Shot("06_map_full");
            Press(KeyCode.M);
            yield return Frames(5);
            int ammo = 0, med = 0, fuses = 0;
            foreach (var pk in Pickup.All) { if (pk.kind == PickupKind.Ammo) ammo++; else if (pk.kind == PickupKind.Medkit) med++; else fuses++; }
            Note("Pickups in the tower: " + ammo + " ammo boxes, " + med + " medkits, " + fuses + " fuses");
            Check("Map toggles without errors", true, "");
        }

        // ---------------- flicker ----------------

        IEnumerator Flicker()
        {
            var spots = new[]
            {
                new Vector4(2, 2, 8, 90), new Vector4(2, 3, 10, 0), new Vector4(2, 4, 11, 180), new Vector4(2, 2, 12, -90),
                new Vector4(2, 12, 12, 0), new Vector4(3, 2, 12, 90), new Vector4(1, 6, 20, -90), new Vector4(0, 3, 14, 180),
            };
            // Designed lamp stutter is meant to change the picture; switch it off so only rendering flicker counts.
            foreach (var fl in FindObjectsByType<FlickerLight>(FindObjectsSortMode.None)) fl.enabled = false;
            foreach (var s in spots)
            {
                P.Teleport(LevelBuilder.CellCenter((int)s.x, (int)s.y, (int)s.z), s.w, 0f);
                yield return new WaitForSeconds(1.5f);
                yield return new WaitForEndOfFrame();
                var a = ScreenCapture.CaptureScreenshotAsTexture();
                yield return Frames(6);
                yield return new WaitForEndOfFrame();
                var b = ScreenCapture.CaptureScreenshotAsTexture();
                float diff = Diff(a, b);
                string tag = "F" + (s.x + 1) + "_r" + s.y + "c" + s.z + "_y" + s.w;
                File.WriteAllBytes(Path.Combine(dir, "07_" + tag + ".png"), a.EncodeToPNG());
                if (diff > 0.02f) File.WriteAllBytes(Path.Combine(dir, "07_" + tag + "_b.png"), b.EncodeToPNG());
                Check("No flicker at " + tag, diff < 0.02f, (diff * 100f).ToString("0.00") + "% of pixels changed with the camera still");
                float white = WhiteFraction(a);
                if (s.x == 2) Check("No white-box at " + tag, white < 0.25f, (white * 100f).ToString("0") + "% near-white pixels");
                Destroy(a); Destroy(b);
            }
        }

        static float Diff(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32(); var pb = b.GetPixels32();
            int n = Mathf.Min(pa.Length, pb.Length), changed = 0, sampled = 0;
            for (int i = 0; i < n; i += 7)
            {
                sampled++;
                int d = Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
                if (d > 60) changed++;
            }
            return sampled == 0 ? 0f : (float)changed / sampled;
        }

        static float WhiteFraction(Texture2D a)
        {
            var px = a.GetPixels32();
            int w = 0, n = 0;
            for (int i = 0; i < px.Length; i += 11) { n++; if (px[i].r > 225 && px[i].g > 225 && px[i].b > 225) w++; }
            return n == 0 ? 0f : (float)w / n;
        }

        // ---------------- checkpoints ----------------

        IEnumerator Checkpoints()
        {
            // Grab a fuse on floor 4 first so the checkpoint has one to remember.
            Pickup fuse = null;
            foreach (var f in GM.Level.fuses) if (f != null && f.floor == 3) fuse = f;
            if (fuse != null)
            {
                P.Teleport(fuse.transform.position - Vector3.up * 0.9f, 0f, 0f);
                yield return Frames(10);
                Note("Collected the floor 4 fuse: " + GM.FusesCollected + " fuses");
            }
            FreezeEnemies();
            Check("No checkpoint yet on floor 4", GameManager.CheckpointFloor == -1, "floor " + GameManager.CheckpointFloor);

            P.Teleport(LevelBuilder.CellCenter(2, 2, 8), 90f, 0f);
            yield return Frames(5);
            Check("Reaching floor 3 saves a checkpoint", GameManager.CheckpointFloor == 2 && GM.ToastText.Contains("CHECKPOINT"), "floor " + GameManager.CheckpointFloor + ", '" + GM.ToastText + "'");
            yield return Shot("08_checkpoint_f3");

            P.Gun.SetAmmo(17, 88);
            P.Teleport(LevelBuilder.CellCenter(1, 6, 20), -90f, 0f);
            yield return Frames(5);
            Vector3 cpPos = P.transform.position;
            Check("Reaching floor 2 updates the checkpoint", GameManager.CheckpointFloor == 1, "floor " + GameManager.CheckpointFloor);
            int fuses = GM.FusesCollected;

            P.Teleport(LevelBuilder.CellCenter(2, 2, 8), 90f, 0f);
            yield return Frames(5);
            Check("Going back up keeps the floor 2 checkpoint", GameManager.CheckpointFloor == 1, "floor " + GameManager.CheckpointFloor);

            P.TakeDamage(999f, P.transform.position + Vector3.forward);
            yield return new WaitForSeconds(3.5f);
            Check("Death shows the end screen", GM.IsOver, "");
            yield return Shot("08_death_screen");
            Press(KeyCode.Return);
            yield return Frames(3);
            yield return WaitForGame();
            FreezeEnemies();
            yield return Frames(5);
            Check("Enter continues from the checkpoint (game resumes)", GM.IsPlaying, "");
            float off = Vector3.Distance(P.transform.position, cpPos);
            Check("Respawned at the floor 2 checkpoint", off < 1.5f && P.Floor == 1, off.ToString("0.00") + " m away, floor " + (P.Floor + 1));
            Check("Ammo restored from the checkpoint", P.Gun.Ammo == 17 && P.Gun.Reserve == 88, P.Gun.Ammo + "/" + P.Gun.Reserve);
            Check("Fuses kept", GM.FusesCollected == fuses, GM.FusesCollected + " vs " + fuses);
            Check("Full health after respawn", P.Health >= P.MaxHealth - 0.1f, P.Health.ToString("0"));
            yield return Shot("08_respawned");

            P.TakeDamage(999f, P.transform.position + Vector3.forward);
            yield return new WaitForSeconds(3.5f);
            Press(KeyCode.N);
            yield return Frames(3);
            yield return WaitForGame();
            Check("N starts a new game (intro screen)", !GM.IsPlaying && !GM.IsOver, "");
            Check("New game clears the checkpoint", GameManager.CheckpointFloor == -1, "floor " + GameManager.CheckpointFloor);
            Check("New game starts on floor 4 with a full mag", P.Floor == 3 && P.Gun.Ammo == MP5.MagSize, "floor " + (P.Floor + 1) + ", " + P.Gun.Ammo + "/" + P.Gun.Reserve);
            Note("Starting ammo: " + P.Gun.Ammo + " / " + P.Gun.Reserve);
        }
    }
}
