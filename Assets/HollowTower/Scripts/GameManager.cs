using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HollowTower
{
    /// <summary>
    /// Runs the match: objectives, the HUD, the hint system, light culling,
    /// the final chase when the elevator powers up, and win / death screens.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        enum Phase { Intro, Playing, Won, Lost }

        public LevelInfo Level { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsOver { get { return phase == Phase.Won || phase == Phase.Lost; } }
        public bool IsPlaying { get { return phase == Phase.Playing; } }
        public bool AllFusesFound { get { return fusesCollected >= totalFuses; } }
        public int FusesCollected { get { return fusesCollected; } }
        public string ToastText { get { return Time.time < toastUntil ? toastText : ""; } }
        /// <summary>Floor (0-based) of the saved checkpoint, or -1 if none.</summary>
        public static int CheckpointFloor { get { return checkpoint != null ? checkpoint.floor : -1; } }
        public float ThreatLevel { get; private set; }

        Phase phase = Phase.Intro;
        Transform enemyRoot;
        int fusesCollected, totalFuses, kills, banished, hintsUsed;
        float introStart, startTime, endTime, overTime, nextLightCull, hintReadyAt, hintShownUntil, toastUntil, hitMarkerUntil;
        string hintText = "", toastText = "";
        bool hitHead, finalChase;
        readonly HashSet<string> tipsShown = new HashSet<string>();
        AudioSource ambience;
        GUIStyle big, mid, small, center, hintStyle;
        Texture2D white;

        const float HintCooldown = 12f;

        /// <summary>What you had when you first reached floor 3 or floor 2. Static so it survives the scene reload after death.</summary>
        class Checkpoint
        {
            public int floor, ammo, reserve, pistolAmmo, pistolReserve, kills, banished, hints;
            public Vector3 position;
            public float yaw, elapsed;
            public bool[] fuseTaken;
        }
        static Checkpoint checkpoint;
        static bool resumeFromCheckpoint;

        static readonly string[] IntroLines =
        {
            "NIGHT 41",
            "The Halvorsen Tower was the last safe zone in the city. Soldiers held it floor by floor.",
            "They lost.",
            "You wake on the 4th floor among the bodies, an M4A1 still in your hands.",
            "The street doors are welded shut. But the old maintenance logs mention a service elevator",
            "on the ground floor that was walled over years ago... and it still has its own power line.",
            "Find the 3 fuses. Find the hidden elevator. Get out.",
        };

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Setup(LevelInfo level, Transform enemies)
        {
            Level = level;
            enemyRoot = enemies;
            totalFuses = level.fuses.Count;
            startTime = Time.time;

            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = Sfx.Drone;
            ambience.loop = true;
            ambience.volume = 0.42f;
            ambience.spatialBlend = 0f;
            ambience.Play();
            gameObject.AddComponent<MapUI>();

            var p = PlayerController.Instance;
            int testFloor = TestStartFloor();
            if (!testStartUsed && testFloor >= 0 && p != null) { testStartUsed = true; checkpoint = TestCheckpoint(p, testFloor); resumeFromCheckpoint = true; }
            if (resumeFromCheckpoint && checkpoint != null) { resumeFromCheckpoint = false; RestoreCheckpoint(p); return; }
            resumeFromCheckpoint = false;
            checkpoint = null;
            if (p != null) p.InputLocked = true;
            SetCursor(false);
            introStart = Time.unscaledTime;
            Time.timeScale = 0f; // the building waits while you read
        }

        // ---------------- checkpoints ----------------

        void UpdateCheckpoint(PlayerController p)
        {
            if (p == null || p.IsDead || p.IsHidden) return;
            int f = p.Floor;
            if (f != 2 && f != 1) return;                          // floors 3 and 2 (0-based 2 and 1)
            if (checkpoint != null && checkpoint.floor <= f) return;
            var cp = new Checkpoint
            {
                floor = f, position = p.transform.position, yaw = p.Yaw,
                ammo = p.Rifle.Ammo, reserve = p.Rifle.Reserve, pistolAmmo = p.Pistol.Ammo, pistolReserve = p.Pistol.Reserve,
                kills = kills, banished = banished, hints = hintsUsed, elapsed = Time.time - startTime,
                fuseTaken = new bool[Level.fuses.Count],
            };
            for (int i = 0; i < Level.fuses.Count; i++) cp.fuseTaken[i] = Level.fuses[i] == null;
            checkpoint = cp;
            Toast("CHECKPOINT - FLOOR " + (f + 1));
        }

        static bool testStartUsed;

        /// <summary>0-based floor from a test flag (-startfloor3, -startfloor2, -startfloor1), or -1.</summary>
        static int TestStartFloor()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-startfloor3") return 2;
                if (a == "-startfloor2") return 1;
                if (a == "-startfloor1") return 0;
            }
            return -1;
        }

        /// <summary>Test start: a checkpoint at the foot of the stairs down onto floor f, with the fuses from every
        /// floor above taken (all 3 on floor 1) and starting ammo. Used once per launch; N starts a normal game.</summary>
        Checkpoint TestCheckpoint(PlayerController p, int f)
        {
            var s = LevelLayout.Stairs[f];
            var cp = new Checkpoint
            {
                floor = f, position = LevelBuilder.CellCenter(f, s.row0, s.LowCol), yaw = s.climbWest ? 90f : -90f,
                ammo = p.Rifle.Ammo, reserve = p.Rifle.Reserve, pistolAmmo = p.Pistol.Ammo, pistolReserve = p.Pistol.Reserve,
                fuseTaken = new bool[Level.fuses.Count],
            };
            for (int i = 0; i < Level.fuses.Count; i++) cp.fuseTaken[i] = Level.fuses[i] != null && Level.fuses[i].floor > f;
            return cp;
        }

        void RestoreCheckpoint(PlayerController p)
        {
            var cp = checkpoint;
            phase = Phase.Playing;
            Time.timeScale = 1f;
            startTime = Time.time - cp.elapsed;
            kills = cp.kills; banished = cp.banished; hintsUsed = cp.hints;
            if (p != null)
            {
                p.Teleport(cp.position, cp.yaw, 0f);
                p.Rifle.SetAmmo(cp.ammo, cp.reserve);
                p.Pistol.SetAmmo(cp.pistolAmmo, cp.pistolReserve);
                p.InputLocked = false;
            }
            // Give a moment to breathe: nothing waits right on top of the checkpoint.
            foreach (var z in Zombie.All.ToArray())
                if (Vector3.Distance(z.transform.position, cp.position) < 10f) Destroy(z.gameObject);
            for (int i = 0; i < Level.fuses.Count && i < cp.fuseTaken.Length; i++)
                if (cp.fuseTaken[i] && Level.fuses[i] != null) { Destroy(Level.fuses[i].gameObject); fusesCollected++; }
            if (totalFuses > 0 && fusesCollected >= totalFuses)
            {
                if (Level.elevator != null) Level.elevator.Open();
                StartFinalChase();
            }
            SetCursor(true);
            Toast("Back at the floor " + (cp.floor + 1) + " checkpoint");
        }

        // ---------------- game events ----------------

        public void CollectFuse(Pickup fuse)
        {
            fusesCollected++;
            NoiseSystem.Emit(fuse.transform.position, 24f);
            Sfx.At(Sfx.Roar, fuse.transform.position, 0.4f, 1.6f);
            if (fuse.floor == 1 && fusesCollected < totalFuses) SpawnWave(1, "Heavy footsteps on the stairs... more of the big ones are coming for you.");
            if (fusesCollected < totalFuses)
            {
                Toast(string.Format("FUSE {0}/{1} - sparks fly. That was loud... move!", fusesCollected, totalFuses));
                return;
            }
            Toast("ALL FUSES FOUND. Somewhere below, machinery groans to life...");
            if (Level.elevator != null) Level.elevator.Open();
            StartFinalChase();
        }

        void StartFinalChase()
        {
            finalChase = true;
            var p = PlayerController.Instance;
            int floor = p != null ? p.Floor : 0;

            // The building wakes up: relentless ghosts on your floor, a pack on the ground floor.
            for (int i = 0; i < 3; i++)
            {
                Vector3 spot = RandomFloorPoint(floor, p != null ? p.transform.position : Vector3.zero, 14f);
                Ghost.Spawn(spot + Vector3.up * 1.3f, enemyRoot, true);
            }
            // The rest of the ground floor's bosses pour in (20 in all), and a pack gathers round the tower's ground floor.
            SpawnWave(0, null);
            Zombie.Spawn(EnemyKind.Runner, LevelBuilder.CellCenter(0, 6, 12), enemyRoot);
            Zombie.Spawn(EnemyKind.Runner, LevelBuilder.CellCenter(0, 10, 29), enemyRoot);
            Zombie.Spawn(EnemyKind.Runner, LevelBuilder.CellCenter(0, 17, 20), enemyRoot);
            Zombie.Spawn(EnemyKind.Runner, LevelBuilder.CellCenter(0, 12, 26), enemyRoot);
            Zombie.Spawn(EnemyKind.Walker, LevelBuilder.CellCenter(0, 4, 29), enemyRoot);
            Zombie.Spawn(EnemyKind.Walker, LevelBuilder.CellCenter(0, 6, 6), enemyRoot);
            Zombie.Spawn(EnemyKind.Dog, LevelBuilder.CellCenter(0, 6, 21), enemyRoot);
            Zombie.Spawn(EnemyKind.Dog, LevelBuilder.CellCenter(0, 4, 15), enemyRoot);
            if (p != null) NoiseSystem.Emit(p.transform.position, 18f);
        }

        Vector3 RandomFloorPoint(int floor, Vector3 awayFrom, float minDist)
        {
            for (int i = 0; i < 40; i++)
            {
                int r = Random.Range(1, LevelBuilder.Rows - 1), c = Random.Range(1, LevelBuilder.Cols - 1);
                if (!LevelBuilder.Walkable(floor, r, c)) continue;
                Vector3 pos = LevelBuilder.CellCenter(floor, r, c);
                if (Vector3.Distance(pos, awayFrom) >= minDist) return pos;
            }
            return LevelBuilder.CellCenter(floor, 6, 9);
        }

        public int WaveBosses { get; private set; }
        readonly bool[] waveDone = new bool[4];

        /// <summary>
        /// The bosses that weren't there at the start: floor 2 gets 5 more when you take its fuse (11 in all),
        /// floor 1 gets 10 more when the lift powers up (20 in all). They come in away from you.
        /// </summary>
        public void SpawnWave(int floor, string message)
        {
            if (floor < 0 || floor >= waveDone.Length || waveDone[floor]) return;
            waveDone[floor] = true;
            var p = PlayerController.Instance;
            Vector3 at = p != null ? p.transform.position : Vector3.zero;
            var rnd = new System.Random(Mathf.RoundToInt(Time.time * 1000f));
            int n = LevelBuilder.BossWaves[floor];
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = LevelBuilder.SpawnSpot(floor, floor == 0 && i % 2 == 0, at, 30f, true, rnd);
                Zombie.Spawn(EnemyKind.Brute, pos, enemyRoot);
                WaveBosses++;
            }
            if (!string.IsNullOrEmpty(message)) Toast(message);
        }

        public void OnZombieKilled(Zombie z)
        {
            kills++;
            // Every kill pays a few rounds (a boss pays more).
            var p = PlayerController.Instance;
            if (p == null) return;
            bool boss = z != null && z.Kind == EnemyKind.Brute;
            int r = boss ? 15 : 6, pr = boss ? 6 : 3;
            p.AddKillAmmo(r, pr);
            Toast("Kill: +" + r + " M4A1, +" + pr + " 9mm rounds");
        }
        public void OnGhostBanished() { banished++; }

        public void Win()
        {
            if (IsOver) return;
            phase = Phase.Won;
            endTime = overTime = Time.time;
            var p = PlayerController.Instance;
            if (p != null) p.InputLocked = true;
            Level.elevator.CloseDoors();
        }

        public void Lose()
        {
            if (IsOver) return;
            phase = Phase.Lost;
            endTime = overTime = Time.time;
        }

        public void Toast(string msg)
        {
            toastText = msg;
            toastUntil = Time.time + 3.5f;
        }

        public void ShowHitMarker(bool head)
        {
            hitHead = head;
            hitMarkerUntil = Time.time + 0.12f;
        }

        // ---------------- update ----------------

        void Update()
        {
            var p = PlayerController.Instance;
            if (p != null) Zombie.CullOtherFloors(p.transform.position);

            if (phase == Phase.Intro)
            {
                if (InputWrap.Down(KeyCode.Return) || InputWrap.Down(KeyCode.Mouse0) || InputWrap.Down(KeyCode.Space)) StartPlaying();
                return;
            }

            if (IsOver)
            {
                if (Time.time - overTime > 2f)
                {
                    bool again = InputWrap.Down(KeyCode.Return) || InputWrap.Down(KeyCode.Space);
                    bool fresh = InputWrap.Down(KeyCode.N);
                    if (again || fresh)
                    {
                        // After a death, Enter carries on from the last checkpoint; N (or a win) starts over.
                        resumeFromCheckpoint = again && phase == Phase.Lost && checkpoint != null;
                        if (!resumeFromCheckpoint) checkpoint = null;
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    }
                }
                SetCursor(false);
                return;
            }

            if (InputWrap.Down(KeyCode.Escape))
            {
                IsPaused = !IsPaused;
                Time.timeScale = IsPaused ? 0f : 1f;
                AudioListener.pause = IsPaused;
                SetCursor(!IsPaused);
            }
            if (IsPaused) return;
            // Clicking back into the game view re-captures the mouse.
            if (Cursor.lockState != CursorLockMode.Locked && InputWrap.Down(KeyCode.Mouse0)) SetCursor(true);

            PowerGrid.Tick();
            if (Time.time >= nextLightCull) { nextLightCull = Time.time + 0.3f; CullLights(); }
            UpdateCheckpoint(p);
            ThreatLevel = ComputeThreat();
            if (InputWrap.Down(KeyCode.H)) RequestHint();
            ContextTips(p);
        }

        /// <summary>Leave the intro screen and start the game.</summary>
        public void StartPlaying()
        {
            if (phase != Phase.Intro) return;
            phase = Phase.Playing;
            Time.timeScale = 1f;
            startTime = Time.time;
            var p = PlayerController.Instance;
            if (p != null) p.InputLocked = false;
            SetCursor(true);
            Toast("Press H any time for a hint");
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        const int MaxShadowedLamps = 2, MaxRayTracedLamps = 0;
        readonly List<KeyValuePair<float, TowerLight>> shadowCandidates = new List<KeyValuePair<float, TowerLight>>();
        HashSet<TowerLight> shadowedLastTime = new HashSet<TowerLight>(), shadowedNow = new HashSet<TowerLight>();

        /// <summary>Re-evaluate which lamps are live now (power cuts call this).</summary>
        public void RefreshLights() { CullLights(); }

        void CullLights()
        {
            var p = PlayerController.Instance;
            if (p == null || Level == null) return;
            int floor = p.Floor;
            Vector3 pos = p.transform.position;
            // Only lamps around you are live; the nearest few cast shadows (ray-traced for the closest).
            shadowCandidates.Clear();
            var swap = shadowedLastTime; shadowedLastTime = shadowedNow; shadowedNow = swap; shadowedNow.Clear();
            foreach (var tl in Level.lights)
            {
                if (tl.light == null) continue;
                float d = Vector3.Distance(tl.light.transform.position, pos);
                int gap = Mathf.Abs(tl.floor - floor);
                bool on = ((gap == 0 && d < 44f) || (gap == 1 && d < 14f)) && PowerGrid.LampLive(tl);
                tl.light.enabled = on;
                tl.light.shadows = LightShadows.None;
                if (on && gap == 0 && tl.castsShadows && d < 26f) shadowCandidates.Add(new KeyValuePair<float, TowerLight>(d, tl));
            }
            shadowCandidates.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < shadowCandidates.Count && i < MaxShadowedLamps; i++)
            {
                var tl = shadowCandidates[i].Value;
                tl.light.shadows = LightShadows.Soft;
                if (tl.hd != null && !shadowedLastTime.Contains(tl)) tl.hd.RequestShadowMapRendering();
                shadowedNow.Add(tl);
                if (tl.hd != null) tl.hd.useRayTracedShadows = HDRPLook.RayTracing && i < MaxRayTracedLamps;
            }
        }

        float ComputeThreat()
        {
            var p = PlayerController.Instance;
            if (p == null) return 0f;
            float t = 0f;
            Vector3 pos = p.transform.position;
            foreach (var z in Zombie.All)
            {
                if (z.IsDead) continue;
                float d = Vector3.Distance(z.transform.position, pos);
                if (z.IsChasing) t = Mathf.Max(t, 1f - d / 22f);
                else if (z.Awareness > 0.3f) t = Mathf.Max(t, 0.4f * (1f - d / 18f));
            }
            foreach (var g in Ghost.All)
            {
                if (!g.IsActive) continue;
                float d = Vector3.Distance(g.transform.position, pos);
                t = Mathf.Max(t, (g.IsHunting ? 1f : 0.35f) * (1f - d / 15f));
            }
            return Mathf.Clamp01(t);
        }

        // ---------------- hints ----------------

        void RequestHint()
        {
            if (Time.time < hintReadyAt)
            {
                Toast(string.Format("Hint recharging... {0:0}s", hintReadyAt - Time.time));
                return;
            }
            hintReadyAt = Time.time + HintCooldown;
            hintsUsed++;
            var p = PlayerController.Instance;
            if (p == null) return;

            // Danger first: survival advice beats directions.
            Zombie chaser = null;
            foreach (var z in Zombie.All) if (!z.IsDead && z.IsChasing) { chaser = z; break; }
            bool ghostHunting = Ghost.AnyChasing();

            Vector3 target;
            string where = DescribeObjective(p, out target);

            var gen = PowerGrid.Generators[p.Floor];
            if (ghostHunting && !p.IsHidden)
                ShowHint("A ghost is hunting you. It drifts through walls and can vanish and reappear behind you - your flashlight flickers when it's close. Shoot it (about 5 hits), burn it with your flashlight (F), or get into a locker (E). Its smoke is a decoy: what you see and hear in it isn't real.");
            else if (chaser != null && !p.IsHidden)
            {
                var locker = NearestLocker(p.transform.position);
                string lockerDir = locker != null ? string.Format(" The nearest locker is {0:0}m to the {1}.", Vector3.Distance(locker.transform.position, p.transform.position), Compass(locker.transform.position - p.transform.position)) : "";
                ShowHint("You've been spotted! Break line of sight first, THEN hide - if they see you climb in, they'll drag you out." + lockerDir);
                if (locker != null) GuideOrb.Launch(p.transform.position, locker.ExitPosition);
            }
            else if (p.Health < 55f && !LevelBuilder.InSafeZone(p.transform.position) && Level.bookcase != null)
            {
                Vector3 bc = Level.bookcase.FrontPosition;
                string howFar = p.Floor == 1 ? string.Format("{0:0}m to the {1}", Vector3.Distance(bc, p.transform.position), Compass(bc - p.transform.position)) : "on floor 2";
                ShowHint("You're badly hurt. The library on floor 2 hides a vault behind a gold-trimmed bookcase (" + howFar + "). The dead can't follow you in - sit down there (E) to heal.");
                GuideOrb.Launch(p.transform.position, p.Floor == 1 ? bc : target);
            }
            else if (!PowerGrid.IsOn(p.Floor) && gen != null)
            {
                ShowHint(string.Format("The power is out on this floor. Its generator is {0:0}m to the {1} - press E beside it. Restarting it is loud. Or stay dark: the dead see you less, too.",
                    Vector3.Distance(gen.transform.position, p.transform.position), Compass(gen.transform.position - p.transform.position)));
                GuideOrb.Launch(p.transform.position, gen.UsePosition);
            }
            else
            {
                ShowHint(where + " Follow the light.");
                GuideOrb.Launch(p.transform.position, target);
            }
        }

        string DescribeObjective(PlayerController p, out Vector3 target)
        {
            int floor = p.Floor;
            if (fusesCollected >= totalFuses && Level.elevator != null)
            {
                target = Level.elevator.CarCenter;
                if (floor == 0) return string.Format("The lift is open - {0:0}m to the {1}, in the south-east corner of the security hall (through parking access and the hospital corridor).", Vector3.Distance(target, p.transform.position), Compass(target - p.transform.position));
                return "The lift is open on the GROUND floor: the security hall, south through parking access. Get downstairs.";
            }

            Pickup best = null;
            float bestScore = float.MaxValue;
            foreach (var f in Level.fuses)
            {
                if (f == null) continue;
                float score = Vector3.Distance(f.transform.position, p.transform.position) + Mathf.Abs(f.floor - floor) * 60f;
                if (score < bestScore) { bestScore = score; best = f; }
            }
            if (best == null)
            {
                target = p.transform.position;
                return "Find the hidden elevator.";
            }
            target = best.transform.position;
            if (best.floor == floor)
                return string.Format("A fuse is on this floor, about {0:0}m to the {1}.", Vector3.Distance(target, p.transform.position), Compass(target - p.transform.position));
            return string.Format("The next fuse is on floor {0}. Take the stairs {1}.", best.floor + 1, best.floor > floor ? "up" : "down");
        }

        static string Compass(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return names[Mathf.RoundToInt(a / 45f) % 8];
        }

        static HidingSpot NearestLocker(Vector3 pos)
        {
            HidingSpot best = null;
            float bd = float.MaxValue;
            foreach (var s in HidingSpot.All)
            {
                if (s.Occupied || Mathf.Abs(s.transform.position.y - pos.y) > 1.5f) continue;
                float d = Vector3.Distance(s.transform.position, pos);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        void ShowHint(string text)
        {
            hintText = text;
            hintShownUntil = Time.time + 9f;
            Sfx.At(Sfx.Whisper, PlayerController.Instance.transform.position, 0.3f, 1.4f);
        }

        void ContextTips(PlayerController p)
        {
            if (p == null || p.IsDead) return;
            if (!p.IsHidden && p.NearbyHidingSpot() != null) Tip("locker", "Press E to hide inside the locker.");
            if (p.Gun.Ammo == 0 && p.Gun.Reserve == 0) Tip("ammo", "Out of ammo. Press X for your other gun. Green boxes hold rounds for both, and every kill pays a few.");
            foreach (var pit in TrapPit.All)
                if (pit.Floor == p.Floor && Vector3.Distance(pit.Center, p.transform.position) < 7f)
                { Tip("pit", "A trap pit (hazard tape, red glow). Edge round it along the ledge - the dead can't follow you along it."); break; }
            if (p.Floor == 0 && LevelBuilder.InAnnex(p.transform.position)) Tip("annex", "The annex: the mansion has two storeys, the security hall a mezzanine. Its generator and the lift are in opposite corners.");
            foreach (var g in Ghost.All)
                if (g.IsActive && Vector3.Distance(g.transform.position, p.transform.position) < 12f)
                { Tip("ghost", "A ghost! Shoot it, or hold your flashlight on it until it burns away."); break; }
            foreach (var z in Zombie.All)
                if (!z.IsDead && z.Kind == EnemyKind.Brute && Vector3.Distance(z.transform.position, p.transform.position) < 15f)
                { Tip("brute", "A Brute. It takes a whole magazine and hears everything. Crouch (C) and avoid it."); break; }
            if (!PowerGrid.IsOn(p.Floor)) Tip("power", "Power cut! Every floor has a generator (red on the map, M). Press E beside it to bring the lights back.");
            if (GhostSmoke.All.Count > 0 && GhostSmoke.NearestDistance(p.transform.position) < 14f)
                Tip("smoke", "Ghost smoke. The shape and the sounds in it are a decoy that calls the dead - the real ghost has vanished. Watch your back.");
            if (p.LightLevel > 0.6f && !p.IsCrouching) Tip("light", "You're standing in the light. Stay in the dark and crouch (C) to stay unseen.");
        }

        static Generator NearGenerator(PlayerController p)
        {
            var g = PowerGrid.Generators[p.Floor];
            return g != null && g.PlayerNear ? g : null;
        }

        void Tip(string key, string text)
        {
            if (tipsShown.Contains(key)) return;
            tipsShown.Add(key);
            hintText = text;
            hintShownUntil = Time.time + 6f;
        }

        // ---------------- HUD ----------------

        void BuildStyles()
        {
            if (big != null) return;
            white = Texture2D.whiteTexture;
            big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            mid = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label);
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Italic };
        }

        void OnGUI()
        {
            BuildStyles();
            float s = Screen.height / 1080f;
            big.fontSize = Mathf.RoundToInt(64 * s);
            mid.fontSize = Mathf.RoundToInt(28 * s);
            small.fontSize = Mathf.RoundToInt(22 * s);
            center.fontSize = Mathf.RoundToInt(26 * s);
            hintStyle.fontSize = Mathf.RoundToInt(26 * s);
            var p = PlayerController.Instance;

            if (phase == Phase.Intro) { DrawIntro(s); return; }

            // Damage flash.
            if (p != null)
            {
                float hurt = Mathf.Clamp01(1f - (Time.time - p.LastHurtTime) / 0.6f);
                float low = p.Health < 35f ? (0.15f + 0.1f * Mathf.Sin(Time.time * 4f)) : 0f;
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.6f, 0f, 0f, Mathf.Max(hurt * 0.45f, low)));
            }

            // Inside ghost smoke you can't see a thing.
            if (p != null && p.Cam != null)
            {
                float haze = GhostSmoke.Density(p.Cam.transform.position);
                if (haze > 0.01f) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.32f, 0.36f, 0.37f, 0.86f * haze));
            }

            if (phase == Phase.Won) { DrawEnd(s, true); return; }
            if (phase == Phase.Lost) { DrawEnd(s, false); return; }
            if (p == null) return;

            // Crosshair + hit marker.
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            if (!p.Gun.IsAiming && !p.IsHidden)
            {
                float gap = (p.IsSprinting ? 18f : (p.IsCrouching ? 6f : 10f)) * s;
                Color cc = new Color(1f, 1f, 1f, 0.7f);
                Fill(new Rect(cx - gap - 8 * s, cy - 1, 8 * s, 2), cc);
                Fill(new Rect(cx + gap, cy - 1, 8 * s, 2), cc);
                Fill(new Rect(cx - 1, cy - gap - 8 * s, 2, 8 * s), cc);
                Fill(new Rect(cx - 1, cy + gap, 2, 8 * s), cc);
            }
            if (Time.time < hitMarkerUntil)
            {
                Color hc = hitHead ? new Color(1f, 0.2f, 0.1f) : Color.white;
                Fill(new Rect(cx - 12 * s, cy - 12 * s, 6 * s, 6 * s), hc);
                Fill(new Rect(cx + 6 * s, cy - 12 * s, 6 * s, 6 * s), hc);
                Fill(new Rect(cx - 12 * s, cy + 6 * s, 6 * s, 6 * s), hc);
                Fill(new Rect(cx + 6 * s, cy + 6 * s, 6 * s, 6 * s), hc);
            }

            float m = 30f * s;
            // Top-left: floor + objective.
            Shadowed(new Rect(m, m, 900 * s, 40 * s), "FLOOR " + (p.Floor + 1) + (finalChase ? "   -   RUN!" : ""), mid, new Color(0.85f, 0.85f, 0.8f));
            string objective = fusesCollected >= totalFuses
                ? "Escape: the lift in the security hall (ground floor, south annex)"
                : string.Format("Find the lift fuses  {0} / {1}", fusesCollected, totalFuses);
            Shadowed(new Rect(m, m + 38 * s, 900 * s, 34 * s), objective, small, new Color(0.95f, 0.8f, 0.35f));
            float hintCd = Mathf.Max(0f, hintReadyAt - Time.time);
            Shadowed(new Rect(m, m + 70 * s, 900 * s, 30 * s), hintCd > 0f ? string.Format("[H] Hint ({0:0}s)", hintCd) : "[H] Hint ready", small, new Color(0.6f, 0.75f, 0.9f, 0.8f));
            if (!PowerGrid.IsOn(p.Floor))
                Shadowed(new Rect(m, m + 100 * s, 900 * s, 30 * s), "POWER OUT on this floor - restart its generator (red on the map)", small,
                    new Color(1f, 0.35f, 0.25f, 0.7f + 0.3f * Mathf.Sin(Time.time * 4f)));

            // Top-centre: stealth state.
            string stealth; Color sc;
            StealthState(p, out stealth, out sc);
            Shadowed(new Rect(cx - 300 * s, m, 600 * s, 40 * s), stealth, new GUIStyle(mid) { alignment = TextAnchor.MiddleCenter }, sc);
            // Light / visibility meter.
            float vis = Mathf.Clamp01(p.Visibility / 1.4f);
            Fill(new Rect(cx - 100 * s, m + 44 * s, 200 * s, 6 * s), new Color(0f, 0f, 0f, 0.5f));
            Fill(new Rect(cx - 100 * s, m + 44 * s, 200 * s * vis, 6 * s), Color.Lerp(new Color(0.3f, 0.5f, 0.9f), new Color(1f, 0.9f, 0.4f), vis));
            Shadowed(new Rect(cx - 100 * s, m + 52 * s, 200 * s, 24 * s), "visibility", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(16 * s) }, new Color(1f, 1f, 1f, 0.5f));

            // Bottom-left: health + stamina.
            float by = Screen.height - m;
            Shadowed(new Rect(m, by - 70 * s, 300 * s, 30 * s), "HEALTH", small, new Color(1f, 1f, 1f, 0.7f));
            if (p.IsRegenerating) Shadowed(new Rect(m + 95 * s, by - 70 * s, 300 * s, 30 * s), "+ recovering", small, new Color(0.5f, 1f, 0.55f, 0.55f + 0.35f * Mathf.Sin(Time.time * 4f)));
            Fill(new Rect(m, by - 40 * s, 300 * s, 14 * s), new Color(0f, 0f, 0f, 0.55f));
            Fill(new Rect(m, by - 40 * s, 300 * s * Mathf.Clamp01(p.Health / p.MaxHealth), 14 * s), Color.Lerp(new Color(0.8f, 0.1f, 0.05f), new Color(0.35f, 0.75f, 0.3f), p.Health / p.MaxHealth));
            Fill(new Rect(m, by - 20 * s, 300 * s * p.Stamina, 5 * s), new Color(0.8f, 0.8f, 0.8f, 0.6f));

            // Bottom-right: ammo.
            var gun = p.Gun;
            var ammoStyle = new GUIStyle(big) { alignment = TextAnchor.LowerRight, fontSize = Mathf.RoundToInt(56 * s) };
            Shadowed(new Rect(Screen.width - m - 400 * s, by - 80 * s, 400 * s, 70 * s), gun.Ammo + " / " + gun.Reserve, ammoStyle,
                gun.Ammo == 0 ? new Color(1f, 0.3f, 0.2f) : Color.white);
            Shadowed(new Rect(Screen.width - m - 400 * s, by - 110 * s, 400 * s, 30 * s), gun.IsReloading ? "RELOADING..." : gun.DisplayName + "   [X] swap", new GUIStyle(small) { alignment = TextAnchor.LowerRight }, new Color(1f, 1f, 1f, 0.6f));
            Shadowed(new Rect(Screen.width - m - 400 * s, by - 140 * s, 400 * s, 30 * s), p.FlashlightOn ? "[F] Flashlight ON" : "[F] Flashlight off", new GUIStyle(small) { alignment = TextAnchor.LowerRight }, new Color(1f, 0.95f, 0.7f, 0.6f));

            // Locker prompt.
            if (p.IsHidden) Shadowed(new Rect(cx - 400 * s, Screen.height * 0.7f, 800 * s, 40 * s), "Hiding...  [E] to get out", center, new Color(0.7f, 0.85f, 1f));
            else if (p.InCover)
                Shadowed(new Rect(cx - 600 * s, Screen.height * 0.68f, 1200 * s, 40 * s),
                    (p.CoverIsLow ? "IN COVER  -  hold Right click to pop up & aim" : "IN COVER  -  A / D lean    Right click peek & aim") + "    [Q] leave",
                    center, p.IsPeeking ? new Color(1f, 0.75f, 0.4f) : new Color(0.7f, 0.9f, 0.7f));
            else if (p.NearbyHidingSpot() != null)
            {
                var spot = p.NearbyHidingSpot();
                string verb = "[E] " + spot.Verb + (spot.RequiresCrouch && !p.IsCrouching ? "  (crouch first: C)" : "");
                Shadowed(new Rect(cx - 400 * s, Screen.height * 0.62f, 800 * s, 40 * s), verb, center, Color.white);
            }
            else if (NearGenerator(p) != null)
            {
                var gen = NearGenerator(p);
                string g = gen.Starting ? "Starting the generator..." : PowerGrid.IsOn(gen.floor) ? "Generator running" : "[E] Restart the generator (loud!)";
                Shadowed(new Rect(cx - 400 * s, Screen.height * 0.62f, 800 * s, 40 * s), g, center, PowerGrid.IsOn(gen.floor) ? new Color(0.6f, 1f, 0.6f, 0.7f) : new Color(1f, 0.85f, 0.4f));
            }
            else if (SecretBookcase.Instance != null && SecretBookcase.Instance.PlayerNear)
                Shadowed(new Rect(cx - 400 * s, Screen.height * 0.62f, 800 * s, 40 * s), SecretBookcase.Instance.IsOpen ? "[E] Close the bookcase" : "[E] Pull the odd golden book...", center, new Color(1f, 0.85f, 0.4f));

            // Toast + hint.
            if (Time.time < toastUntil)
                Shadowed(new Rect(cx - 600 * s, Screen.height * 0.75f, 1200 * s, 40 * s), toastText, center, new Color(1f, 0.9f, 0.6f));
            if (Time.time < hintShownUntil)
            {
                Rect r = new Rect(cx - 520 * s, Screen.height * 0.18f, 1040 * s, 110 * s);
                Fill(r, new Color(0f, 0f, 0f, 0.55f));
                Shadowed(new Rect(r.x + 20 * s, r.y + 8 * s, r.width - 40 * s, r.height - 16 * s), hintText, hintStyle, new Color(0.75f, 0.9f, 1f));
            }

            if (IsPaused)
            {
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));
                Shadowed(new Rect(0, Screen.height * 0.3f, Screen.width, 80 * s), "PAUSED", big, Color.white);
                Shadowed(new Rect(cx - 500 * s, Screen.height * 0.45f, 1000 * s, 300 * s), ControlsText(), center, new Color(0.85f, 0.85f, 0.85f));
                DrawCredits(s);
            }
        }

        void StealthState(PlayerController p, out string text, out Color color)
        {
            if (p.IsHidden) { text = "HIDDEN"; color = new Color(0.4f, 0.7f, 1f); return; }
            bool spotted = Ghost.AnyChasing();
            bool suspicious = false;
            foreach (var z in Zombie.All)
            {
                if (z.IsDead) continue;
                if (z.IsChasing) spotted = true;
                else if (z.Awareness > 0.3f) suspicious = true;
            }
            if (spotted) { text = "SPOTTED!"; color = new Color(1f, 0.15f, 0.1f); }
            else if (suspicious) { text = "SOMETHING HEARD YOU..."; color = new Color(1f, 0.75f, 0.2f); }
            else { text = "UNSEEN"; color = new Color(0.6f, 0.8f, 0.6f, 0.8f); }
        }

        static string ControlsText()
        {
            return "WASD move    Mouse look    Shift sprint    C / Ctrl crouch    Space jump\n" +
                   "Left click fire    Right click aim    R reload (auto when empty)    X swap M4A1 / pistol\n" +
                   "Q take cover    in cover: A / D lean, Right click peek & aim, Q / S leave\n" +
                   "F flashlight (burns ghosts)    E hide / use    H hint    M / Tab map    Esc pause\n" +
                   "F8 ray tracing on/off (performance mode)    F9 ray-traced torch shadows";
        }

        void DrawIntro(float s)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.92f));
            Shadowed(new Rect(0, Screen.height * 0.12f, Screen.width, 90 * s), "HOLLOW TOWER", big, new Color(0.75f, 0.05f, 0.03f));
            float y = Screen.height * 0.26f;
            for (int i = 0; i < IntroLines.Length; i++)
            {
                float a = Mathf.Clamp01((Time.unscaledTime - introStart) * 0.8f - i * 0.5f);
                Shadowed(new Rect(Screen.width * 0.15f, y, Screen.width * 0.7f, 40 * s), IntroLines[i], center, new Color(0.85f, 0.85f, 0.8f, a));
                y += 44 * s;
            }
            Shadowed(new Rect(Screen.width * 0.1f, y + 30 * s, Screen.width * 0.8f, 165 * s), ControlsText(), center, new Color(0.6f, 0.7f, 0.8f));
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            Shadowed(new Rect(0, Screen.height * 0.88f, Screen.width, 40 * s), "Click or press Enter to begin", center, new Color(1f, 1f, 1f, blink));
            DrawCredits(s);
        }

        public const string Credits = "Models: \"office is old abandoned FREE\" by dasy444 (CC-BY 4.0) | \"St Bartholomew-the-Less Interior\" by artfletch (CC-BY 4.0) | \"The Smoking Room\" by The Hallwyl Museum (CC-BY 4.0) | " +
            "\"Apocalyptic Hospital Hallway Interior\" by ETBENO (CC-BY 4.0) | \"Shrine\" by MinZiin and \"9 mm\" by Slava Zemlyanik (Sketchfab Standard) | The Mansion Interiors, M4 Carbine, S.W.A.T. operator, Egypt chamber, monster juggernaut, Groaner (Sketchfab)";

        void DrawCredits(float s)
        {
            var st = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(10, Mathf.RoundToInt(15 * s)), wordWrap = true };
            Shadowed(new Rect(Screen.width * 0.05f, Screen.height - 50 * s, Screen.width * 0.9f, 46 * s), Credits, st, new Color(0.7f, 0.7f, 0.7f, 0.75f));
        }

        void DrawEnd(float s, bool won)
        {
            float t = Time.time - overTime;
            float fade = Mathf.Clamp01((t - (won ? 1.6f : 0.8f)) / 1.5f);
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, fade * 0.95f));
            if (fade < 0.6f) return;
            float time = endTime - startTime;
            Shadowed(new Rect(0, Screen.height * 0.3f, Screen.width, 90 * s), won ? "YOU ESCAPED" : "YOU DIED", big,
                won ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.8f, 0.05f, 0.03f));
            string stats = string.Format("Time {0}:{1:00}     Zombies killed {2}     Ghosts banished {3}     Fuses {4}/{5}     Hints used {6}",
                (int)(time / 60f), (int)(time % 60f), kills, banished, fusesCollected, totalFuses, hintsUsed);
            Shadowed(new Rect(0, Screen.height * 0.45f, Screen.width, 40 * s), stats, center, new Color(0.85f, 0.85f, 0.85f));
            if (won)
                Shadowed(new Rect(Screen.width * 0.15f, Screen.height * 0.52f, Screen.width * 0.7f, 80 * s),
                    "The lift shudders down past the ground floor, into the old service tunnels... and out under the river.", center, new Color(0.7f, 0.8f, 0.9f));
            if (t > 2f)
                Shadowed(new Rect(0, Screen.height * 0.7f, Screen.width, 40 * s),
                    !won && checkpoint != null ? "Press Enter to continue from the floor " + (checkpoint.floor + 1) + " checkpoint      N - new game"
                                               : "Press Enter to play again", center, Color.white);
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

    /// <summary>The hint "spirit": a small light that floats ahead of you along the real walking route.</summary>
    public class GuideOrb : MonoBehaviour
    {
        Vector3[] corners;
        int index;
        float life = 12f;
        Light lamp;

        public static void Launch(Vector3 from, Vector3 to)
        {
            NavMeshHit a, b;
            if (!NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas)) return;
            if (!NavMesh.SamplePosition(to, out b, 3f, NavMesh.AllAreas)) return;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.corners.Length == 0) return;

            var g = Gfx.Prim(PrimitiveType.Sphere, "GuideOrb", null, a.position + Vector3.up * 1.3f, Vector3.one * 0.18f,
                Gfx.Emissive(new Color(0.6f, 0.8f, 1f), new Color(1.5f, 2.5f, 4f)), false);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var orb = g.AddComponent<GuideOrb>();
            orb.corners = path.corners;
            orb.lamp = Gfx.PointLight(g.transform, Vector3.zero, new Color(0.5f, 0.75f, 1f), 1.6f, 6f, LightShadows.None);
        }

        void Update()
        {
            life -= Time.deltaTime;
            var p = PlayerController.Instance;
            if (life <= 0f || corners == null || index >= corners.Length)
            {
                lamp.intensity -= Time.deltaTime * 2f * Gfx.Lumens;
                transform.localScale *= 1f - Time.deltaTime * 2f;
                if (lamp.intensity <= 0f) Destroy(gameObject);
                return;
            }
            // Wait for the player if they fall behind.
            float speed = 5f;
            if (p != null && Vector3.Distance(p.transform.position, transform.position) > 9f) speed = 0f;
            Vector3 target = corners[index] + Vector3.up * 1.3f;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime)
                                 + Vector3.up * Mathf.Sin(Time.time * 6f) * 0.003f;
            if (Vector3.Distance(transform.position, target) < 0.15f) index++;
            lamp.intensity = (1.4f + Mathf.Sin(Time.time * 8f) * 0.3f) * Gfx.Lumens;
        }
    }
}
