using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// Zombie AI with sight + hearing.
    ///  Walker - slow, common, poor eyesight.
    ///  Runner - fragile but sprints as fast as you do.
    ///  Brute  - huge, soaks a full magazine, hits like a truck and hears everything.
    ///
    /// States: Wander -> Suspicious (it glimpsed something) -> Chase (it's seen you)
    ///         Investigate (it heard something) -> Search (lost you) -> back to Wander.
    /// </summary>
    public class Zombie : MonoBehaviour, NoiseSystem.IListener
    {
        public static readonly List<Zombie> All = new List<Zombie>();

        enum State { Wander, Suspicious, Investigate, Chase, Search, Dead }

        class Stats
        {
            public float hp, walk, chase, damage, sight, fov, hearing, cooldown, scale, reach;
            public Color skin, shirt, eyes;
            public float pitch;
        }

        /// <summary>Boss health by floor (0-based): a full magazine-and-a-half upstairs, a little less where they come in crowds.</summary>
        public static float BossHealth(int floor) { return floor >= 2 ? 520f : floor == 1 ? 460f : 420f; }

        static Stats For(EnemyKind k)
        {
            switch (k)
            {
                case EnemyKind.Runner:
                    // The human deer: the fastest thing in the tower. 7.5 m/s - only a sprint (8.2) pulls away, and it doesn't give up.
                    return new Stats { hp = 75f, walk = 2.0f, chase = 7.5f, damage = 12f, sight = 22f, fov = 150f, hearing = 1.3f, cooldown = 0.75f, scale = 1f, reach = 1.9f,
                        skin = new Color(0.55f, 0.55f, 0.5f), shirt = new Color(0.25f, 0.06f, 0.05f), eyes = new Color(3f, 3f, 2.5f), pitch = 1.35f };
                case EnemyKind.Dog:
                    // The zombie dog: noses you out even when you hide, howls the others onto you, bites fast.
                    // 7.0 m/s - quicker than a walk (4.8), but a sprint (8.2) still pulls away and it's easy to kill.
                    return new Stats { hp = 55f, walk = 1.8f, chase = 7.0f, damage = 9f, sight = 18f, fov = 160f, hearing = 1.5f, cooldown = 0.8f, scale = 1f, reach = 1.5f,
                        skin = new Color(0.4f, 0.32f, 0.28f), shirt = new Color(0.2f, 0.1f, 0.08f), eyes = new Color(3f, 0.4f, 0.2f), pitch = 1.7f };
                case EnemyKind.Brute:
                    // A boss: charges at 6.6-7.3 m/s (you only get away by sprinting, 8.2) and smashes every ~1.1 s.
                    // There are a lot of them now (5 / 11 / 20 on floors 3 / 2 / 1), so each is a little softer (see BossHealth).
                    return new Stats { hp = 700f, walk = 1.5f, chase = 6.6f, damage = 32f, sight = 18f, fov = 130f, hearing = 2.0f, cooldown = 1.1f, scale = 1.55f, reach = 2.5f,
                        skin = new Color(0.32f, 0.36f, 0.28f), shirt = new Color(0.1f, 0.1f, 0.12f), eyes = new Color(5f, 0.2f, 0.1f), pitch = 0.6f };
                default:
                    // Walkers (scary zombie / parasite): shamble when idle, but run you down at 5 m/s - faster than you walk.
                    return new Stats { hp = 100f, walk = 1.3f, chase = 5.0f, damage = 15f, sight = 16f, fov = 120f, hearing = 1f, cooldown = 1.2f, scale = 1f, reach = 1.7f,
                        skin = new Color(0.42f, 0.48f, 0.38f), shirt = new Color(0.22f, 0.25f, 0.3f), eyes = new Color(2.5f, 2.2f, 0.4f), pitch = 1f };
            }
        }

        public EnemyKind Kind { get; private set; }
        public bool IsDead { get { return state == State.Dead; } }
        public bool IsChasing { get { return state == State.Chase; } }
        public float Awareness { get { return awareness; } }
        public float Health { get { return hp; } }
        public bool CanSeePlayer { get { return canSee; } }
        /// <summary>The BossRig variant this one wears (-1 = the old block body).</summary>
        public int ModelVariant { get { return rig != null ? rig.Variant : -1; } }

        Stats s;
        State state = State.Wander;
        float hp;
        NavMeshAgent agent;
        Vector3 home, lastKnown;
        float awareness, lastSeen, stateTime, nextThink, nextWander, nextAttack, attackWindup = -1f, staggerEnd, nextGroan;
        float deathT;
        Vector3 fallAxis;
        bool canSee;
        bool bangingOnLocker;
        float lockerBangEnd;
        Transform torso, head, armL, armR, legL, legR, visual;
        AudioSource src;
        Renderer[] renderers;
        float animPhase;

        // Brutes wear a real boss model (BossRig); walkers the scary / parasite zombie, runners the human deer.
        public static int NextBossVariant, NextWalkerVariant;
        /// <summary>Screenshot helper: the BossRig variant the next spawn wears (-1 = by kind).</summary>
        public static int ForceVariant = -1;
        BossRig rig;
        Transform headCol, glow;
        float attackStart, slamTime = -10f, flinch;
        bool debugPose;
        float debugSpeed, debugRaise, debugSlam, debugRaw = -1f;

        // Zombie dogs (DogRig): smell, howl and run with a pack leader.
        DogRig dog;
        Zombie leader;
        float nextLeaderLook, nextHowl, howlStart = -10f, nextBark;
        bool smelling;
        /// <summary>How far a dog smells you - through doors and hiding spots, but not through floors.</summary>
        const float ScentRange = 9f;
        /// <summary>How far a dog's bark / howl carries to the other zombies.</summary>
        const float CallRange = 26f;
        public bool IsDog { get { return Kind == EnemyKind.Dog; } }
        public bool HasDogModel { get { return dog != null; } }
        /// <summary>How many times this dog has barked / howled for the pack.</summary>
        public int Calls { get; private set; }

        static bool SameBuilding(Vector3 a, Vector3 b) { return LevelBuilder.InAnnex(a) == LevelBuilder.InAnnex(b) && LevelBuilder.InWing(a) == LevelBuilder.InWing(b); }

        public static Zombie Spawn(EnemyKind kind, Vector3 position, Transform parent)
        {
            Vector3 requested = position;
            NavMeshHit hit;
            // Snap onto the navmesh - but never onto another storey (through a stairwell or a pit).
            // (and not across the tower wall onto the roof of an annex passage)
            if (NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - position.y) < 0.6f && SameBuilding(hit.position, position)) position = hit.position;
            else
            {
                // Nothing walkable right here on this storey (furniture, a pit): look around it, still on the same storey,
                // or the agent would drop itself onto the nearest navmesh - possibly the floor below.
                for (int i = 0; i < 24; i++)
                {
                    Vector2 r = Random.insideUnitCircle * (1.5f + i * 0.35f);
                    if (NavMesh.SamplePosition(position + new Vector3(r.x, 0f, r.y), out hit, 1.2f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - position.y) < 0.6f && SameBuilding(hit.position, position))
                    { position = hit.position; break; }
                }
            }
            var g = new GameObject(kind + " Zombie");
            g.transform.SetParent(parent, false);
            g.transform.position = position;
            g.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var z = g.AddComponent<Zombie>();
            z.Init(kind);
            // The agent places itself on the nearest navmesh when it's created; if that put it somewhere else
            // (another storey, an annex roof), put it back.
            if (Vector3.Distance(g.transform.position, position) > 0.8f)
            {
                Debug.LogWarning(string.Format("Hollow Tower: {0} spawn asked {3}, snapped {1}, agent moved it to {2}", kind, position.ToString("F1"), g.transform.position.ToString("F1"), requested.ToString("F1")));
                z.agent.Warp(position);
                if (Vector3.Distance(g.transform.position, position) > 0.8f) Debug.LogWarning("Hollow Tower: warp back failed, now at " + g.transform.position.ToString("F1"));
            }
            return z;
        }

        void Init(EnemyKind kind)
        {
            Kind = kind;
            s = For(kind);
            if (kind == EnemyKind.Brute) s.hp = BossHealth(LevelBuilder.FloorAt(transform.position));
            hp = s.hp;
            home = transform.position;
            transform.localScale = Vector3.one * s.scale;
            BuildBody();
            Gfx.SetLayerRecursive(gameObject, Gfx.EnemyLayer);

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = kind == EnemyKind.Dog ? 0.35f : 0.32f;
            agent.height = kind == EnemyKind.Dog ? 0.9f : 1.8f;
            agent.speed = s.walk;
            agent.angularSpeed = 320f;
            agent.acceleration = kind == EnemyKind.Brute ? 22f : 14f;
            agent.stoppingDistance = 0.6f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);

            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            // Louder again: full volume out to 5 m, and you hear a boss from 52 m, the rest from 36 m.
            src.minDistance = kind == EnemyKind.Brute ? 6f : 4.5f;
            src.maxDistance = kind == EnemyKind.Brute ? 52f : 36f;
            src.pitch = s.pitch * Random.Range(0.9f, 1.1f);
            nextGroan = Time.time + Random.Range(2f, 8f);
            nextWander = Time.time + Random.Range(0f, 3f);
            animPhase = Random.value * 10f;
        }

        void BuildBody()
        {
            if (BuildRigged()) return;

            var skin = Gfx.Lit(s.skin * Random.Range(0.85f, 1.1f), 0.25f);
            var shirt = Gfx.Lit(s.shirt * Random.Range(0.7f, 1.2f), 0.1f);
            var pants = Gfx.Lit(new Color(0.12f, 0.12f, 0.14f) * Random.Range(0.7f, 1.3f), 0.1f);
            var gore = Gfx.Lit(new Color(0.22f, 0.01f, 0.01f), 0.7f);
            var eyeMat = Gfx.Emissive(Color.white, s.eyes);

            visual = new GameObject("Body").transform;
            visual.SetParent(transform, false);

            torso = Gfx.Box("Torso", visual, new Vector3(0f, 1.18f, 0f), new Vector3(0.5f, 0.68f, 0.28f), shirt).transform;
            Gfx.Box("Gore", torso, new Vector3(0.15f, 0.1f, 0.51f), new Vector3(0.35f, 0.3f, 0.05f), gore, false);
            Gfx.Box("Gore", torso, new Vector3(-0.2f, -0.25f, 0.51f), new Vector3(0.25f, 0.2f, 0.05f), gore, false);

            head = Gfx.Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.68f, 0.03f), new Vector3(0.27f, 0.31f, 0.28f), skin).transform;
            Gfx.Prim(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.2f, 0.08f, 0.42f), Vector3.one * 0.16f, eyeMat, false);
            Gfx.Prim(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.2f, 0.08f, 0.42f), Vector3.one * 0.16f, eyeMat, false);
            Gfx.Box("Jaw", head, new Vector3(0f, -0.35f, 0.3f), new Vector3(0.5f, 0.15f, 0.3f), gore, false);

            armL = Limb("ArmL", new Vector3(-0.32f, 1.48f, 0f), new Vector3(0.12f, 0.64f, 0.12f), skin);
            armR = Limb("ArmR", new Vector3(0.32f, 1.48f, 0f), new Vector3(0.12f, 0.64f, 0.12f), skin);
            legL = Limb("LegL", new Vector3(-0.13f, 0.84f, 0f), new Vector3(0.17f, 0.84f, 0.17f), pants);
            legR = Limb("LegR", new Vector3(0.13f, 0.84f, 0f), new Vector3(0.17f, 0.84f, 0.17f), pants);

            if (Kind == EnemyKind.Brute)
            {
                // Hulking shoulders and a red glow you can spot down a corridor.
                Gfx.Box("Hump", visual, new Vector3(0f, 1.5f, -0.12f), new Vector3(0.7f, 0.3f, 0.35f), skin);
                Gfx.PointLight(head, new Vector3(0f, 0f, 0.8f), new Color(1f, 0.1f, 0.05f), 1.2f, 3f, LightShadows.None);
            }
            renderers = GetComponentsInChildren<Renderer>();
        }

        bool BuildDog()
        {
            dog = DogRig.Create(transform, 0.95f);
            if (dog == null) return false;
            head = dog.Head;
            var body = new GameObject("BodyCol");
            body.transform.SetParent(transform, false);
            var cap = body.AddComponent<CapsuleCollider>();
            cap.direction = 2; // lying along the dog
            cap.center = new Vector3(0f, 0.5f, 0f);
            cap.radius = 0.3f;
            cap.height = Mathf.Max(0.8f, dog.Length * 0.85f);
            var h = new GameObject("Head");
            h.transform.SetParent(transform, false);
            h.AddComponent<SphereCollider>().radius = 0.17f;
            headCol = h.transform;
            renderers = GetComponentsInChildren<Renderer>();
            TrackHead();
            return true;
        }

        bool BuildRigged()
        {
            if (Kind == EnemyKind.Dog) return BuildDog();
            int v;
            if (ForceVariant >= 0) v = ForceVariant;
            else if (Kind == EnemyKind.Brute) v = NextBossVariant++ % BossRig.BossVariants;
            else if (Kind == EnemyKind.Runner) v = BossRig.Deer;
            else v = (NextWalkerVariant++ % 2) == 0 ? BossRig.Scary : BossRig.Parasite;
            float height = v == BossRig.Deer ? 1.75f : v < BossRig.BossVariants ? 1.8f : 1.85f;
            rig = BossRig.Create(transform, v, height);
            if (rig == null) return false;
            head = rig.Head;

            // Hit boxes: the body, and a head sphere that tracks the head bone (headshots).
            var body = new GameObject("BodyCol");
            body.transform.SetParent(transform, false);
            var cap = body.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.9f, 0f);
            cap.height = 1.8f;
            cap.radius = rig.Variant == 0 ? 0.5f : 0.38f;
            var h = new GameObject("Head");
            h.transform.SetParent(transform, false);
            h.AddComponent<SphereCollider>().radius = 0.17f;
            headCol = h.transform;

            // The red glow you can spot down a corridor (bosses only: a floor of lit chasers would cost too much).
            if (Kind == EnemyKind.Brute)
            {
                glow = new GameObject("Glow").transform;
                glow.SetParent(transform, false);
                Gfx.PointLight(glow, Vector3.zero, new Color(1f, 0.1f, 0.05f), 0.7f, 2.5f, LightShadows.None);
            }
            renderers = GetComponentsInChildren<Renderer>();
            TrackHead();
            return true;
        }

        void TrackHead()
        {
            if (head == null) return;
            headCol.position = head.position + transform.up * (dog != null ? 0.02f : 0.1f) * s.scale;
            if (glow != null) glow.position = head.position + (transform.forward * 0.55f - transform.up * 0.25f) * s.scale;
        }

        /// <summary>Screenshot helper: hold a pose (speed m/s, smash raise / slam 0..1) regardless of AI.</summary>
        public void DebugPose(float speed, float raise, float slam)
        {
            debugPose = true;
            debugSpeed = speed; debugRaise = raise; debugSlam = slam; debugRaw = -1f;
        }

        /// <summary>Screenshot helper: show a dog's raw clip at time t (seconds into its one long take).</summary>
        public void DebugRaw(float t) { debugPose = true; debugRaw = t; }

        void LateUpdate()
        {
            if (dog != null) { PoseDog(); return; }
            if (rig == null || state == State.Dead) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused && !debugPose) return;
            float dt = Time.deltaTime;
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);
            if (debugPose)
            {
                rig.Pose(1f / 30f, debugSpeed, true, debugRaise, debugSlam, 0f); // fixed step: batch frames can be very short
            }
            else
            {
                float speed = agent.enabled ? agent.velocity.magnitude : 0f;
                float raise = 0f;
                if (attackWindup > 0f) raise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(attackStart, attackWindup, Time.time) * 1.25f);
                float slam = Mathf.Clamp01(1f - (Time.time - slamTime) / 0.45f);
                rig.Pose(dt, speed, state == State.Chase, raise, slam, flinch);
            }
            TrackHead();
        }

        float howlLength = 1f;

        void PoseDog()
        {
            if (state == State.Dead) { dog.Dying(Time.deltaTime); return; }
            if (GameManager.Instance != null && GameManager.Instance.IsPaused && !debugPose) return;
            if (debugPose)
            {
                if (debugRaw >= 0f) dog.Raw(debugRaw);
                else dog.Pose(1f / 30f, debugSpeed, debugRaise, debugSlam);
            }
            else
            {
                float speed = agent.enabled ? agent.velocity.magnitude : 0f;
                // Bite: crouch through the wind-up, then the snap.
                float bite = 0f;
                if (attackWindup > 0f) bite = 0.55f * Mathf.InverseLerp(attackStart, attackWindup, Time.time);
                else if (Time.time - slamTime < 0.35f) bite = 0.55f + 0.45f * (Time.time - slamTime) / 0.35f;
                float howl = (Time.time - howlStart) / howlLength;
                dog.Pose(Time.deltaTime, speed, bite, howl > 0f && howl < 1f ? howl : 0f);
            }
            TrackHead();
        }

        /// <summary>
        /// A dog barks or howls: every zombie within earshot on this floor runs to where the player is
        /// (and if you're hiding, they know which door to tear open).
        /// </summary>
        void CallPack(bool barkAtSpot)
        {
            var p = PlayerController.Instance;
            howlStart = Time.time;
            Calls++;
            howlLength = barkAtSpot ? 0.6f : 1.2f;
            src.PlayOneShot(barkAtSpot ? Sfx.Bark : Sfx.Howl, 1f);
            if (barkAtSpot && p != null && p.IsHidden) p.Expose();
            foreach (var z in All)
            {
                if (z == this || z.IsDead || z.IsDog) continue;
                Vector3 d = z.transform.position - lastKnown;
                if (Mathf.Abs(d.y) > LevelBuilder.Story * 0.6f) continue;
                d.y = 0f;
                if (d.magnitude > CallRange) continue;
                z.Alert(lastKnown);
            }
        }

        /// <summary>Called by a dog: it knows exactly where you are.</summary>
        public void Alert(Vector3 position)
        {
            if (IsDead) return;
            lastKnown = position;
            lastSeen = Time.time;
            EnterChase();
        }

        /// <summary>Dogs smell the player within ScentRange - no line of sight needed, hiding doesn't help.</summary>
        void Sniff(PlayerController p, float dt)
        {
            smelling = false;
            if (p == null || p.IsDead || LevelBuilder.InSafeZone(p.ChestPosition)) return;
            if (p.IsHidden && p.CurrentSpot != null && p.CurrentSpot.Safe) return;
            Vector3 d = p.transform.position - transform.position;
            if (Mathf.Abs(d.y) > 1.5f) return;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > ScentRange) return;
            smelling = true;
            lastKnown = p.transform.position;
            lastSeen = Time.time;
            if (state != State.Chase) awareness += (dist < 4f ? 1.4f : 0.6f) * dt; // a second or two to pick up the trail
        }

        /// <summary>Dogs run with a pack: the nearest brute (or any zombie) close by.</summary>
        void FindLeader()
        {
            if (leader != null && !leader.IsDead) return;
            leader = null;
            if (Time.time < nextLeaderLook) return;
            nextLeaderLook = Time.time + 3f;
            float best = 16f;
            foreach (var z in All)
            {
                if (z == this || z.IsDead || z.IsDog) continue;
                Vector3 d = z.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                float score = d.magnitude - (z.Kind == EnemyKind.Brute ? 8f : 0f);
                if (score < best) { best = score; leader = z; }
            }
        }

        Transform Limb(string name, Vector3 pivot, Vector3 size, Material m)
        {
            var p = new GameObject(name).transform;
            p.SetParent(visual, false);
            p.localPosition = pivot;
            Gfx.Box(name + "Mesh", p, new Vector3(0f, -size.y * 0.5f, 0f), size, m);
            return p;
        }

        bool hiddenByFloor;

        /// <summary>
        /// Called every frame by GameManager: a tower full of skinned zombies is expensive to draw (shadows
        /// from every lamp), so those more than a storey above or below the player aren't drawn at all.
        /// </summary>
        public static void CullOtherFloors(float playerY) { var p = PlayerController.Instance; CullOtherFloors(p != null ? p.transform.position : new Vector3(0f, playerY, 0f)); }

        /// <summary>Also hides anything more than 60 m away (the ground floor and its annex hold a crowd).
        /// In the two-storey annex both storeys stay drawn: you can look down from the gallery.</summary>
        public static void CullOtherFloors(Vector3 player)
        {
            float limit = LevelBuilder.Story * 0.9f;
            bool playerInAnnex = LevelBuilder.InAnnex(player);
            for (int i = 0; i < All.Count; i++)
            {
                var z = All[i];
                if (z.renderers == null) continue;
                Vector3 zp = z.transform.position;
                bool sameStack = playerInAnnex && LevelBuilder.InAnnex(zp);
                float dx = zp.x - player.x, dz = zp.z - player.z;
                bool hide = (!sameStack && Mathf.Abs(zp.y - player.y) > limit) || dx * dx + dz * dz > 60f * 60f;
                if (hide == z.hiddenByFloor) continue;
                z.hiddenByFloor = hide;
                foreach (var r in z.renderers) if (r != null) r.enabled = !hide;
            }
        }

        void OnEnable() { All.Add(this); NoiseSystem.Register(this); }
        void OnDisable() { All.Remove(this); NoiseSystem.Unregister(this); }

        public Vector3 ListenPosition { get { return transform.position; } }
        Vector3 EyePos { get { return transform.position + Vector3.up * (dog != null ? 0.75f : 1.65f) * s.scale; } }

        // ---------------- senses ----------------

        public void HearNoise(Vector3 position, float loudness)
        {
            if (IsDead) return;
            float l = Mathf.Clamp01(loudness * s.hearing);
            if (state == State.Chase)
            {
                if (!canSee) lastKnown = position;
                return;
            }
            awareness = Mathf.Max(awareness, 0.35f + l * 0.6f);
            lastKnown = position;
            if (awareness >= 1f) EnterChase();
            else SetState(State.Investigate);
        }

        void Perceive(float dt)
        {
            var p = PlayerController.Instance;
            canSee = false;
            if (IsDog) Sniff(p, dt);
            if (p == null || p.IsDead || p.IsHidden || LevelBuilder.InSafeZone(p.ChestPosition)) return;

            Vector3 target = p.ChestPosition;
            Vector3 eye = EyePos;
            Vector3 to = target - eye;
            float dist = to.magnitude;
            float range = s.sight * Mathf.Max(0.05f, p.Visibility);
            bool close = dist < 2.2f && Mathf.Abs(to.y) < 1.5f; // you can't sneak right past their nose
            if (!close)
            {
                if (dist > range) return;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (Vector3.Angle(transform.forward, flat) > s.fov * 0.5f) return;
            }
            if (Physics.Linecast(eye, target, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) return;
            if (!close && GhostSmoke.BlocksSight(eye, target)) return;   // ghost smoke hides you (and them)

            canSee = true;
            lastKnown = p.transform.position;
            lastSeen = Time.time;
            float gain = close ? 4f : 1.6f * (1.25f - dist / Mathf.Max(0.1f, range));
            if (Kind == EnemyKind.Runner) gain *= 1.3f;
            awareness += Mathf.Max(0.2f, gain) * dt;
        }

        public static bool AnyWatching(PlayerController p)
        {
            foreach (var z in All)
                if (!z.IsDead && z.canSee && z.awareness > 0.5f) return true;
            return false;
        }

        // ---------------- state machine ----------------

        void SetState(State next)
        {
            if (state == next) return;
            state = next;
            stateTime = Time.time;
            bangingOnLocker = false;
        }

        void EnterChase()
        {
            awareness = 1f;
            if (state != State.Chase)
            {
                SetState(State.Chase);
                if (IsDog)
                {
                    // It howls the pack onto you.
                    if (Time.time >= nextHowl) { nextHowl = Time.time + 7f; CallPack(false); }
                    else src.PlayOneShot(Sfx.Bark, 1f);
                }
                else src.PlayOneShot(Kind == EnemyKind.Walker ? Sfx.Groan : Sfx.Roar, 1f); // the deer shrieks (high-pitched roar)
            }
        }

        void Update()
        {
            if (state == State.Dead) { UpdateDeath(); return; }
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            float now = Time.time;
            if (now >= nextThink)
            {
                float dt = 0.15f;
                nextThink = now + dt;
                Perceive(dt);
                Think();
            }

            if (now >= nextGroan)
            {
                nextGroan = now + Random.Range(state == State.Chase ? 2.5f : 5f, state == State.Chase ? 5f : 12f);
                src.PlayOneShot(IsDog && state == State.Chase ? Sfx.Bark : Sfx.Groan, state == State.Chase ? 1f : 0.85f); // dogs growl, and bark on the run
            }

            UpdateAttack();
            Animate();
        }

        void Think()
        {
            var p = PlayerController.Instance;
            if (!agent.isOnNavMesh) return;

            bool stagger = Time.time < staggerEnd;
            if (IsDog)
            {
                FindLeader();
                // Its pack leader is hunting you: it runs alongside to bite.
                if (state != State.Chase && leader != null && leader.state == State.Chase)
                {
                    lastKnown = leader.lastKnown;
                    lastSeen = Time.time;
                    EnterChase();
                }
                // It's picked up your scent: nose down, straight to it.
                else if (smelling && (state == State.Wander || state == State.Search || state == State.Suspicious)) SetState(State.Investigate);
            }
            if (state != State.Chase)
            {
                if (awareness >= 1f) EnterChase();
                else if (!canSee && !smelling) awareness = Mathf.Max(0f, awareness - 0.15f * 0.15f);
            }

            switch (state)
            {
                case State.Wander:
                    agent.speed = s.walk;
                    if (canSee && awareness > 0.3f) { SetState(State.Suspicious); break; }
                    if (IsDog && leader != null)
                    {
                        // Trot at its leader's heel.
                        Vector3 heel = leader.transform.position - leader.transform.forward * 1.2f + leader.transform.right * 0.9f;
                        float gap = Vector3.Distance(transform.position, heel);
                        home = leader.transform.position;
                        if (gap > 2f)
                        {
                            agent.speed = gap > 6f ? s.chase * 0.6f : Mathf.Max(s.walk, leader.agent.velocity.magnitude * 1.1f);
                            agent.SetDestination(heel);
                        }
                        break;
                    }
                    if (Time.time > nextWander && (!agent.hasPath || agent.remainingDistance < 0.8f))
                    {
                        nextWander = Time.time + Random.Range(3f, 8f);
                        Vector3 dest;
                        if (RandomPointNear(home, 12f, out dest)) agent.SetDestination(dest);
                    }
                    break;

                case State.Suspicious:
                    // Freeze, stare, then creep toward what it glimpsed.
                    agent.speed = s.walk * 0.8f;
                    if (Time.time - stateTime < 1.0f) { agent.ResetPath(); FaceTowards(lastKnown); }
                    else agent.SetDestination(lastKnown);
                    if (!canSee && Time.time - lastSeen > 3f) SetState(State.Search);
                    break;

                case State.Investigate:
                    agent.speed = Mathf.Lerp(s.walk, s.chase, 0.45f);
                    agent.SetDestination(lastKnown);
                    if (canSee && awareness > 0.3f) SetState(State.Suspicious);
                    else if (!agent.pathPending && agent.remainingDistance < 1.2f) SetState(State.Search);
                    else if (Time.time - stateTime > 20f) SetState(State.Search);
                    break;

                case State.Chase:
                    agent.speed = stagger ? s.chase * 0.2f : s.chase;
                    // Brutes put on a burst when they have you in sight and close.
                    if (Kind == EnemyKind.Brute && !stagger && canSee && p != null && Vector3.Distance(transform.position, p.transform.position) < 12f)
                        agent.speed = s.chase * 1.1f;
                    if (p != null && p.IsHidden)
                    {
                        if (IsDog && smelling && p.CurrentSpot != null)
                        {
                            // It can smell you in there: it stands at the door barking, calling the others over.
                            Vector3 spot = p.CurrentSpot.ExitPosition;
                            agent.SetDestination(spot);
                            if (Vector3.Distance(transform.position, spot) < 1.8f)
                            {
                                FaceTowards(p.transform.position);
                                if (Time.time >= nextBark) { nextBark = Time.time + 1.5f; CallPack(true); }
                            }
                            break;
                        }
                        if (!IsDog && p.SeenHiding && p.CurrentSpot != null)
                        {
                            // It watched you climb in. It's coming for the locker.
                            Vector3 door = p.CurrentSpot.ExitPosition;
                            agent.SetDestination(door);
                            if (Vector3.Distance(transform.position, door) < 1.4f) BangOnLocker(p);
                            break;
                        }
                        canSee = false;
                    }
                    // While it has you in sight or on the nose, it keeps howling the pack in after you.
                    if (IsDog && p != null && !p.IsHidden && (canSee || smelling) && Time.time >= nextHowl)
                    {
                        nextHowl = Time.time + 7f;
                        CallPack(false);
                    }
                    if (canSee && p != null) agent.SetDestination(p.transform.position);
                    else
                    {
                        agent.SetDestination(lastKnown);
                        float patience = Kind == EnemyKind.Runner ? 12f : 6f; // the deer keeps hunting the longest
                        if (Time.time - lastSeen > patience && (!agent.pathPending && agent.remainingDistance < 1.5f))
                        {
                            awareness = 0.6f;
                            SetState(State.Search);
                        }
                        else if (Time.time - lastSeen > (Kind == EnemyKind.Runner ? 40f : 25f))
                        {
                            awareness = 0.4f;
                            SetState(State.Search);
                        }
                    }
                    break;

                case State.Search:
                    agent.speed = s.walk * 1.2f;
                    if (canSee && awareness > 0.3f) { SetState(State.Suspicious); break; }
                    if (!agent.hasPath || agent.remainingDistance < 0.8f)
                    {
                        Vector3 dest;
                        if (RandomPointNear(lastKnown, 6f, out dest)) agent.SetDestination(dest);
                    }
                    if (Time.time - stateTime > (Kind == EnemyKind.Runner ? 16f : 9f)) { home = lastKnown; SetState(State.Wander); }
                    break;
            }
        }

        void BangOnLocker(PlayerController p)
        {
            if (!bangingOnLocker)
            {
                bangingOnLocker = true;
                lockerBangEnd = Time.time + 1.6f;
                src.PlayOneShot(Sfx.Roar, 0.9f);
                GameManager.Instance.Toast("It's tearing at the locker door!");
            }
            else if (Time.time > lockerBangEnd)
            {
                bangingOnLocker = false;
                p.Unhide();
                p.TakeDamage(s.damage * 0.8f, transform.position);
                nextAttack = Time.time + s.cooldown;
            }
        }

        void UpdateAttack()
        {
            if (state != State.Chase) { attackWindup = -1f; return; }
            var p = PlayerController.Instance;
            if (p == null || p.IsDead || p.IsHidden || LevelBuilder.InSafeZone(p.ChestPosition)) { attackWindup = -1f; return; }
            Vector3 d = p.transform.position - transform.position;
            float flatDist = new Vector2(d.x, d.z).magnitude;
            bool inReach = flatDist < s.reach && Mathf.Abs(d.y) < 1.4f;

            if (attackWindup < 0f)
            {
                if (inReach && Time.time >= nextAttack)
                {
                    attackStart = Time.time;
                    attackWindup = Time.time + (Kind == EnemyKind.Brute ? 0.45f : IsDog ? 0.3f : 0.4f);
                    if (Kind == EnemyKind.Brute) src.PlayOneShot(Sfx.Roar, 0.7f);
                }
            }
            else
            {
                FaceTowards(p.transform.position);
                if (Time.time >= attackWindup)
                {
                    attackWindup = -1f;
                    nextAttack = Time.time + s.cooldown;
                    slamTime = Time.time;
                    if (flatDist < s.reach + 0.5f && Mathf.Abs(d.y) < 1.4f) p.TakeDamage(s.damage, transform.position);
                }
            }
        }

        void FaceTowards(Vector3 pos)
        {
            Vector3 d = pos - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }

        bool RandomPointNear(Vector3 center, float radius, out Vector3 result)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                Vector3 c = center + new Vector3(r.x, 0f, r.y);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(c, out hit, 2f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - center.y) < 1f)
                {
                    result = hit.position;
                    return true;
                }
            }
            result = center;
            return false;
        }

        // ---------------- damage ----------------

        public void TakeDamage(float amount, Vector3 point, Vector3 dir, bool headshot)
        {
            if (IsDead) return;
            hp -= amount;
            staggerEnd = Time.time + (Kind == EnemyKind.Brute ? 0.05f : 0.18f);
            if (torso != null) torso.localRotation = Quaternion.Euler(-12f, Random.Range(-10f, 10f), 0f);
            flinch = 1f;
            // Getting shot tells it exactly where you are.
            var p = PlayerController.Instance;
            if (p != null) { lastKnown = p.transform.position; lastSeen = Time.time; }
            EnterChase();
            if (hp <= 0f) Die(dir);
        }

        void Die(Vector3 dir)
        {
            SetState(State.Dead);
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            fallAxis = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 0 ? flat : transform.forward);
            deathT = 0f;
            if (rig != null) rig.Stop();
            src.pitch *= 0.8f;
            src.PlayOneShot(Sfx.Groan, 1f);
            GameManager.Instance.OnZombieKilled(this);

            // Fallen soldiers' rounds: some of the dead drop a small ammo box (brutes always do).
            if (Kind == EnemyKind.Brute || Random.value < 0.4f)
            {
                var drop = Pickup.Create(null, transform.position + Vector3.up * 0.5f + flat * 0.6f, PickupKind.Ammo);
                drop.floor = LevelBuilder.FloorAt(transform.position);
                drop.rounds = Kind == EnemyKind.Brute ? 45 : 20;
            }
        }

        void UpdateDeath()
        {
            deathT += Time.deltaTime;
            if (dog != null) { if (deathT > 12f) { transform.position += Vector3.down * Time.deltaTime * 0.3f; if (deathT > 16f) Destroy(gameObject); } return; } // the dog plays its own collapse
            if (deathT < 0.6f)
            {
                transform.Rotate(fallAxis, 150f * Time.deltaTime, Space.World);
            }
            else if (deathT > 12f)
            {
                transform.position += Vector3.down * Time.deltaTime * 0.3f;
                if (deathT > 16f) Destroy(gameObject);
            }
        }

        // ---------------- animation ----------------

        void Animate()
        {
            if (rig != null || dog != null) return; // posed in LateUpdate
            float speed = agent.enabled ? agent.velocity.magnitude : 0f;
            animPhase += Time.deltaTime * (2f + speed * 2.2f);
            float swing = Mathf.Sin(animPhase) * Mathf.Clamp01(speed / 2f) * 35f;
            legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            bool running = Kind == EnemyKind.Runner && speed > 3f;
            float reachUp = state == State.Chase || attackWindup > 0f ? -95f : -70f;
            if (running) reachUp = -20f;
            float armSway = Mathf.Sin(animPhase * 0.5f) * 8f;
            float strike = attackWindup > 0f ? Mathf.Sin(Time.time * 25f) * 25f : 0f;
            armL.localRotation = Quaternion.Euler(reachUp + armSway + strike + (running ? swing : 0f), 0f, -6f);
            armR.localRotation = Quaternion.Euler(reachUp - armSway - strike - (running ? swing : 0f), 0f, 6f);

            float lean = state == State.Chase ? 18f : 6f;
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(lean, Mathf.Sin(animPhase * 0.5f) * 6f, 0f), Time.deltaTime * 6f);
            head.localRotation = Quaternion.Euler(Mathf.Sin(animPhase * 0.3f) * 10f, Mathf.Sin(animPhase * 0.21f) * 25f, Mathf.Sin(animPhase * 0.17f) * 15f);
            visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(animPhase)) * 0.04f * Mathf.Clamp01(speed), 0f);
        }
    }
}
