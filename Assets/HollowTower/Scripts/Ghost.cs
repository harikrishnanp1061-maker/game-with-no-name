using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollowTower
{
    /// <summary>
    /// The pale ones: the "DOE Ghost" model (see <see cref="GhostBody"/>), walking with its feet on the floor -
    /// straight through walls - and sensing you nearby even without line of sight. While hunting they use three tricks:
    ///   - vanish: fade out, keep closing in unseen, and reappear behind you (your flashlight flickers when one is near),
    ///   - smoke: when close, they leave a cloud that lasts a minute; inside it a decoy ghost and fake noises
    ///     (which call the dead to it) are a false signal while the real ghost has vanished,
    ///   - power cut: one that starts hunting you can kill the lights on its floor until you restart that floor's generator.
    /// Three ways to survive: hide in a locker until they lose interest, hold your flashlight beam on them until
    /// they burn away, or shoot them (a handful of rounds disperses one). Either way, they come back later.
    /// </summary>
    public class Ghost : MonoBehaviour
    {
        public static readonly List<Ghost> All = new List<Ghost>();
        /// <summary>Vanishing and smoke (the play test turns them off to test bullets alone).</summary>
        public static bool Skills = true;

        enum State { Drift, Hunt, Recoil, Vanish, Banished }

        State state = State.Drift;
        Vector3 home, driftTarget, lastPos;
        float stateTime, banish, lostTime, alpha = 1f, respawnAt, nextWhisper;
        float huntStart, nextVanish, nextSmoke, vanishLength, fade = 1f;
        bool relentless, reappearNear;
        Light glow;
        Transform body;
        AudioSource src;
        Color baseEmission = new Color(0.35f, 0.55f, 0.8f);

        GhostBody model;
        Material[] primitiveMats;   // only if the model is missing
        bool shown = true;

        const float SenseRadius = 9f;
        const float SightRadius = 18f;
        const float MaxHealth = 110f;   // 5 MP5 body hits
        const float HitRadius = 0.5f;
        /// <summary>The ghost's transform sits this far above its feet (about chest height).</summary>
        public const float FeetBelow = 1.2f;
        public const float WalkSpeed = 1.2f, HuntSpeed = 3.2f, RelentlessSpeed = 3.8f;
        float health = MaxHealth, hitFlash;

        public bool IsHunting { get { return state == State.Hunt || (state == State.Vanish && reappearNear); } }
        public bool IsActive { get { return state != State.Banished; } }
        /// <summary>Faded out (vanished): bullets and the torch pass straight through.</summary>
        public bool IsInvisible { get { return fade < 0.5f; } }
        public GhostBody Body { get { return model; } }
        public static int Vanishes { get; private set; }
        public static int Reappears { get; private set; }
        /// <summary>How many power cuts were set off by shooting a ghost.</summary>
        public static int ShotCuts { get; private set; }

        public static Ghost Spawn(Vector3 position, Transform parent, bool relentless)
        {
            var g = new GameObject(relentless ? "Ghost (relentless)" : "Ghost");
            g.transform.SetParent(parent, false);
            g.transform.position = position;
            var ghost = g.AddComponent<Ghost>();
            ghost.relentless = relentless;
            ghost.home = position;
            ghost.driftTarget = position;
            ghost.lastPos = position;
            ghost.Build();
            if (relentless) ghost.SetState(State.Hunt);
            return ghost;
        }

        void Build()
        {
            body = new GameObject("Shape").transform;
            body.SetParent(transform, false);
            model = GhostBody.Create(body, transform, FeetBelow);
            if (model == null) BuildPrimitive();

            glow = Gfx.PointLight(transform, new Vector3(0f, 0.2f, 0.3f), new Color(0.5f, 0.7f, 1f), 0.9f, 6f, LightShadows.None);

            src = gameObject.AddComponent<AudioSource>();
            src.clip = Sfx.Whisper;
            src.loop = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1.5f;
            src.maxDistance = 14f;
            src.volume = 0.8f;
            src.pitch = Random.Range(0.8f, 1.2f);
            src.Play();
            nextSmoke = Time.time + Random.Range(0f, 10f);
        }

        /// <summary>The old robe-and-skull ghost, if the model is missing.</summary>
        void BuildPrimitive()
        {
            var mat = Gfx.Transparent(new Color(0.7f, 0.85f, 1f, 0.45f), baseEmission);
            var eyeMat = Gfx.Lit(new Color(0f, 0f, 0f, 1f), 0f);
            Gfx.Prim(PrimitiveType.Capsule, "Robe", body, new Vector3(0f, -0.35f, 0f), new Vector3(0.6f, 0.75f, 0.45f), mat, false);
            var head = Gfx.Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.36f, 0.44f, 0.36f), mat, false).transform;
            Gfx.Prim(PrimitiveType.Sphere, "EyeHoleL", head, new Vector3(-0.2f, 0.05f, 0.38f), new Vector3(0.22f, 0.3f, 0.15f), eyeMat, false);
            Gfx.Prim(PrimitiveType.Sphere, "EyeHoleR", head, new Vector3(0.2f, 0.05f, 0.38f), new Vector3(0.22f, 0.3f, 0.15f), eyeMat, false);
            primitiveMats = new[] { mat };
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static bool AnyChasing()
        {
            foreach (var g in All) if (g.IsHunting) return true;
            return false;
        }

        public static float NearestDistance(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var g in All)
            {
                if (!g.IsActive) continue;
                float d = Vector3.Distance(g.transform.position, p);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>The nearest visible ghost a bullet ray passes through within maxDistance (ghosts have no colliders).</summary>
        public static Ghost RayHit(Vector3 origin, Vector3 dir, float maxDistance, out float distance)
        {
            Ghost best = null;
            distance = maxDistance;
            foreach (var g in All)
            {
                if (!g.IsActive || g.IsInvisible) continue;
                // Upper body and legs: test a sphere on each.
                for (int i = 0; i < 2; i++)
                {
                    Vector3 c = g.body.position + Vector3.up * (i == 0 ? -0.45f : 0.25f);
                    float along = Vector3.Dot(c - origin, dir);
                    if (along < 0f || along > distance) continue;
                    if ((origin + dir * along - c).sqrMagnitude > HitRadius * HitRadius) continue;
                    distance = along;
                    best = g;
                }
            }
            return best;
        }

        /// <summary>Play test: smoke and vanishing are available straight away.</summary>
        public void ReadySkills() { nextSmoke = 0f; nextVanish = 0f; }

        public void TakeHit(float damage)
        {
            if (!IsActive || IsInvisible) return;
            health -= damage;
            hitFlash = 1f;
            // On floors 2 and 1, a ghost you shoot screams the lights out: the floor stays dark until you restart its generator.
            int f = LevelBuilder.FloorAt(transform.position + Vector3.down * FeetBelow);
            if (f <= 1 && PowerGrid.ShotCut(f, transform.position)) ShotCuts++;
            if (state == State.Drift) StartHunt();
            if (health <= 0f) Banish();
        }

        void SetState(State s)
        {
            state = s;
            stateTime = Time.time;
            if (s == State.Hunt && huntStart <= 0f) huntStart = Time.time;
            if (s == State.Drift || s == State.Banished) huntStart = 0f;
        }

        void StartHunt()
        {
            SetState(State.Hunt);
            nextVanish = Time.time + Random.Range(7f, 10f);
            Sfx.At(Sfx.Scream, transform.position, 0.8f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsPaused) return;
            var p = PlayerController.Instance;
            float dt = Time.deltaTime;

            if (state == State.Banished)
            {
                fade = 0f;
                if (Time.time >= respawnAt)
                {
                    transform.position = home;
                    lastPos = home;
                    SetState(State.Drift);
                    banish = 0f;
                    health = MaxHealth;
                    fade = 1f;
                    SetVisible(true);
                }
                return;
            }

            bool lit = !IsInvisible && p != null && p.IlluminatesPoint(transform.position);
            if (lit)
            {
                banish += dt;
                if (banish > 0.15f && Time.time > nextWhisper)
                {
                    nextWhisper = Time.time + 1.2f;
                    Sfx.At(Sfx.Scream, transform.position, 0.5f, Random.Range(0.9f, 1.2f));
                }
                if (banish >= 1.4f) { Banish(); return; }
            }
            else banish = Mathf.Max(0f, banish - dt * 0.5f);

            float speed = WalkSpeed;
            Vector3 target = driftTarget;
            float dist = p != null ? Vector3.Distance(transform.position, p.ChestPosition) : 99f;
            // Where the player stands, at the ghost's own "chest" height (its feet on the player's floor).
            Vector3 playerSpot = p != null ? p.transform.position + Vector3.up * FeetBelow : transform.position;
            switch (state)
            {
                case State.Drift:
                    if (Vector3.Distance(Flat(transform.position), Flat(driftTarget)) < 0.8f || Time.time - stateTime > 9f)
                    {
                        Vector2 r = Random.insideUnitCircle * 10f;
                        driftTarget = ClampToBuilding(home + new Vector3(r.x, 0f, r.y));
                        driftTarget.y = home.y;
                        stateTime = Time.time;
                    }
                    if (Senses(p))
                    {
                        StartHunt();
                        // It found you: the lights on this floor die.
                        int f = LevelBuilder.FloorAt(transform.position + Vector3.down * FeetBelow);
                        if (p.Floor == f) PowerGrid.TryCut(f, transform.position);
                    }
                    break;

                case State.Hunt:
                    if (p == null || p.IsDead) { SetState(State.Drift); break; }
                    speed = relentless ? RelentlessSpeed : HuntSpeed;
                    target = playerSpot;
                    if (p.IsHidden)
                    {
                        // They stand outside the locker for a moment, then lose interest - and fade away.
                        speed = 0.4f;
                        if (Time.time - lostTime > 3.5f && lostTime > 0f)
                        {
                            lostTime = 0f;
                            if (Skills) StartVanish(false, 2.5f);
                            else { SetState(State.Drift); driftTarget = ClampToBuilding(transform.position + Random.onUnitSphere * 10f); driftTarget.y = home.y; }
                            break;
                        }
                        if (lostTime <= 0f) lostTime = Time.time;
                    }
                    else if (!relentless && !Senses(p))
                    {
                        if (lostTime <= 0f) lostTime = Time.time;
                        if (Time.time - lostTime > 4f) { lostTime = 0f; SetState(State.Drift); }
                    }
                    else lostTime = 0f;

                    if (Skills && !p.IsHidden && !LevelBuilder.InSafeZone(p.ChestPosition))
                    {
                        float hunted = Time.time - huntStart;
                        if (hunted > 3f && Time.time > nextSmoke && dist < 7f && GhostSmoke.All.Count < 2)
                        {
                            // A cloud of smoke and a decoy where it stood; the real one slips away unseen.
                            Vector3 floorPos = transform.position + Vector3.down * FeetBelow;
                            GhostSmoke.Create(floorPos, transform.rotation);
                            nextSmoke = Time.time + Random.Range(70f, 90f);
                            StartVanish(true, Random.Range(4f, 5.5f));
                            break;
                        }
                        if (hunted > 7f && Time.time > nextVanish && dist > 3.5f) { StartVanish(true, Random.Range(2.5f, 4f)); break; }
                    }

                    Vector3 reach = p.ChestPosition - transform.position;
                    if (!p.IsHidden && Flat(reach).magnitude < 1.0f && Mathf.Abs(reach.y) < 1.5f && !IsInvisible)
                    {
                        p.TakeDamage(18f, transform.position);
                        Sfx.At(Sfx.Scream, transform.position, 1f, 0.8f);
                        SetState(State.Recoil);
                        driftTarget = transform.position + Flat(transform.position - p.ChestPosition).normalized * 6f;
                    }
                    break;

                case State.Recoil:
                    speed = 2.2f;
                    target = driftTarget;
                    if (Time.time - stateTime > 2.5f) SetState(State.Hunt);
                    break;

                case State.Vanish:
                    // Unseen, it closes in faster than it hunts (still slower than you sprint).
                    speed = reappearNear ? 5f : 2f;
                    target = reappearNear && p != null ? playerSpot : driftTarget;
                    if (Time.time - stateTime > vanishLength) Reappear(p);
                    break;
            }

            if (lit) speed *= 0.25f; // light pins them in place

            // Walk: across the floor at its pace; up or down (through the slabs - it's a ghost) at most 2 m/s.
            Vector3 to = target - transform.position;
            Vector3 flatStep = Vector3.ClampMagnitude(Flat(to), speed * dt);
            float rise = Mathf.Clamp(to.y, -2f * dt, 2f * dt);
            Vector3 next = transform.position + flatStep + Vector3.up * rise;
            // The vault's walls hold them back - they wait at the threshold.
            if (!LevelBuilder.InSafeZone(next) && (next.x < LevelBuilder.Cols * LevelBuilder.Cell - 0.3f || LevelBuilder.InAnnex(next))) transform.position = next;

            // Face where it's going; while hunting, square up to you when close.
            Vector3 face = Flat(to);
            if (p != null && IsHunting && (dist < 4f || flatStep.sqrMagnitude < 1e-6f)) face = Flat(p.ChestPosition - transform.position);
            if (state == State.Recoil && p != null) face = Flat(p.ChestPosition - transform.position);   // backs away, still watching
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), dt * (IsHunting ? 240f : 120f));

            fade = Mathf.MoveTowards(fade, state == State.Vanish ? 0f : 1f, dt * (state == State.Vanish ? 2.2f : 3.5f));
            Vector3 vel = (transform.position - lastPos) / Mathf.Max(1e-4f, dt);
            lastPos = transform.position;
            if (model != null)
            {
                model.ForwardSpeed = Vector3.Dot(Flat(vel), transform.forward);
                model.Hunting = state == State.Hunt || state == State.Recoil;
                model.LookAt = p != null && (IsHunting || dist < 10f) ? p.EyePosition : (Vector3?)null;
                model.FloorY = transform.position.y - FeetBelow;
            }
            UpdateLook(lit, p);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void StartVanish(bool closeIn, float length)
        {
            SetState(State.Vanish);
            reappearNear = closeIn;
            vanishLength = length;
            Vanishes++;
            Sfx.At(Sfx.Whisper, transform.position, 0.8f, 0.55f);
            if (!closeIn)
            {
                // Lost you: it wanders off somewhere else on this floor while unseen.
                for (int i = 0; i < 12; i++)
                {
                    Vector2 r = Random.insideUnitCircle.normalized * Random.Range(12f, 20f);
                    Vector3 d = ClampToBuilding(transform.position + new Vector3(r.x, 0f, r.y));
                    if (!LevelBuilder.InSafeZone(d)) { driftTarget = d; break; }
                }
                driftTarget.y = transform.position.y;
            }
        }

        void Reappear(PlayerController p)
        {
            if (reappearNear && p != null && !p.IsDead && !p.IsHidden)
            {
                // Out of the dark, just behind you, feet on your floor.
                Vector3 back = -Vector3.ProjectOnPlane(p.Cam != null ? p.Cam.transform.forward : p.transform.forward, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, back) * Random.Range(-1.5f, 1.5f);
                Vector3 spot = p.transform.position + back * Random.Range(2.6f, 3.6f) + side + Vector3.up * FeetBelow;
                if (!LevelBuilder.InSafeZone(spot) && !LevelBuilder.InSafeZone(p.ChestPosition)) transform.position = lastPos = ClampToBuilding(spot);
                transform.rotation = Quaternion.LookRotation(Flat(p.ChestPosition - transform.position).normalized + Vector3.forward * 1e-4f);
                Sfx.At(Sfx.Scream, transform.position, 0.9f, 1.15f);
                SetState(State.Hunt);
                nextVanish = Time.time + Random.Range(11f, 17f);
                lostTime = 0f;
                fade = 0.35f;
                Reappears++;
            }
            else
            {
                home = new Vector3(driftTarget.x, transform.position.y, driftTarget.z);
                SetState(State.Drift);
            }
        }

        bool Senses(PlayerController p)
        {
            if (p == null || p.IsDead || p.IsHidden || LevelBuilder.InSafeZone(p.ChestPosition)) return false;
            Vector3 d = p.ChestPosition - transform.position;
            float dist = d.magnitude;
            float sense = relentless ? 40f : SenseRadius;
            if (Mathf.Abs(d.y) < 2.5f && dist < sense * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(p.Visibility))) return true;
            if (dist < SightRadius * Mathf.Clamp01(p.Visibility) && !Physics.Linecast(transform.position, p.ChestPosition, Gfx.WorldMask, QueryTriggerInteraction.Ignore)
                && !GhostSmoke.BlocksSight(transform.position, p.ChestPosition))
                return true;
            return false;
        }

        static Vector3 ClampToBuilding(Vector3 p)
        {
            // Floor 2 ghosts roam its north wing too.
            if (p.y < LevelBuilder.Story * 1.75f + FeetBelow && p.y >= LevelBuilder.Story * 0.75f + FeetBelow)
            {
                p.x = Mathf.Clamp(p.x, LevelBuilder.Cell * 1.2f, LevelBuilder.Cols * LevelBuilder.Cell - LevelBuilder.Cell * 1.2f);
                p.z = Mathf.Clamp(p.z, LevelBuilder.Cell * 1.2f, LevelBuilder.WingZ1 - 1.5f);
                return p;
            }
            // Ground-floor ghosts roam the annex as well (south and west of the tower).
            if (p.y < LevelBuilder.Story * 0.75f + FeetBelow)
            {
                p.x = Mathf.Clamp(p.x, -20f, 110f);
                p.z = Mathf.Clamp(p.z, -50f, LevelBuilder.Rows * LevelBuilder.Cell - LevelBuilder.Cell * 1.2f);
                return p;
            }
            p.x = Mathf.Clamp(p.x, LevelBuilder.Cell * 1.2f, LevelBuilder.Cols * LevelBuilder.Cell - LevelBuilder.Cell * 1.2f);
            p.z = Mathf.Clamp(p.z, LevelBuilder.Cell * 1.2f, LevelBuilder.Rows * LevelBuilder.Cell - LevelBuilder.Cell * 1.2f);
            return p;
        }

        void Banish()
        {
            SetState(State.Banished);
            respawnAt = Time.time + 25f;
            Sfx.At(Sfx.Scream, transform.position, 1f, 0.6f);
            GameManager.Instance.Toast("The ghost scattered... for now.");
            GameManager.Instance.OnGhostBanished();
            SetVisible(false);
        }

        void SetVisible(bool on)
        {
            body.gameObject.SetActive(on);
            glow.enabled = on;
            if (on) src.Play(); else src.Stop();
        }

        void UpdateLook(bool lit, PlayerController p)
        {
            // Only ghosts near you (on your floor, or just above / below) are drawn and animated.
            bool near = p == null || (Mathf.Abs(transform.position.y - p.ChestPosition.y) < LevelBuilder.Story * 1.2f &&
                                      Vector3.Distance(transform.position, p.transform.position) < 45f);
            bool show = near && fade > 0.01f;
            if (show != shown)
            {
                shown = show;
                if (model != null) model.SetRenderersEnabled(show);
                else foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = show;
            }
            glow.enabled = show;
            src.volume = 0.8f * (0.3f + 0.7f * fade);
            if (!show) return;

            // Every so often it flickers half out of existence; it flares white when the torch is on it.
            float flicker = Random.value < 0.002f ? 0.35f : 1f;
            alpha = flicker < 1f ? flicker : Mathf.MoveTowards(alpha, 1f, Time.deltaTime * 4f);
            float a = alpha * fade * fade;
            bool hunting = state == State.Hunt || state == State.Recoil;
            Color e = lit ? Color.Lerp(baseEmission, Color.white * 2f, banish / 1.4f) : baseEmission * (hunting ? 0.12f : 0.07f);
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 6f);
            e = Color.Lerp(e, Color.white * 2.5f, hitFlash);
            if (model != null) model.SetLook(a, e);
            else
            {
                Color c = primitiveMats[0].GetColor(Gfx.BaseColorId); c.a = 0.45f * a;
                primitiveMats[0].SetColor(Gfx.BaseColorId, c);
                Gfx.SetEmission(primitiveMats[0], e * 3f * a);
            }
            glow.intensity = (hunting ? 0.12f : 0.08f) * a * Gfx.Lumens;
            glow.color = hunting ? new Color(0.6f, 0.65f, 1f) : new Color(0.5f, 0.7f, 1f);
            if (lit) body.localPosition = Random.insideUnitSphere * 0.04f; else body.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// Ghost smoke: a cold cloud that hangs for a minute where a ghost stood and fills the corridor (or room) wall to wall,
    /// up to ~9 m each way. Nobody sees through it - not you, not the dead, not the ghosts. It is also a false signal:
    /// a faint decoy ghost stands in it and fake footsteps and whispers come out of it, loud enough to call every
    /// zombie nearby, while the real ghost has vanished and is closing in from somewhere else. The flashlight
    /// shows the decoy up for what it is.
    /// </summary>
    public class GhostSmoke : MonoBehaviour
    {
        public static readonly List<GhostSmoke> All = new List<GhostSmoke>();
        public const float Life = 60f;
        const int PuffCount = 30;
        const float MaxReach = 9f, Height = 2.7f;

        public static int Calls { get; private set; }
        public float Age { get { return Time.time - born; } }
        public bool DecoyVisible { get { return decoy != null && decoyFade > 0.05f; } }
        public Vector3 DecoyPosition { get { return decoy != null ? decoy.position : transform.position; } }
        /// <summary>The cloud as a capsule along the corridor: A..B at chest height, with this radius.</summary>
        public Vector3 EndA { get { return endA; } }
        public Vector3 EndB { get { return endB; } }
        public float Radius { get { return radius; } }
        /// <summary>0..1: builds up over 2.5 s, thins out in its last 6 s.</summary>
        public float Strength { get { float a = Age; return Mathf.SmoothStep(0f, 1f, a / 2.5f) * Mathf.SmoothStep(0f, 1f, (Life - a) / 6f); } }

        static Texture2D puffTex;
        static Material puffMat;
        Transform[] puffs;
        Renderer[] puffR;
        Vector3[] puffHome;
        float[] puffSize, puffSeed;
        MaterialPropertyBlock block;
        float born, nextCall, decoyFade = 1f, lightOnDecoy, radius;
        Vector3 endA, endB;
        Transform decoy;
        GhostBody decoyBody;
        AudioSource hiss;
        bool exposed;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static float NearestDistance(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var s in All) best = Mathf.Min(best, s.DistanceTo(p));
            return best;
        }

        /// <summary>Distance from p to the cloud's core (0 inside it).</summary>
        public float DistanceTo(Vector3 p)
        {
            Vector3 q = ClosestOnSegment(endA, endB, p);
            return Mathf.Max(0f, Vector3.Distance(q, p) - radius);
        }

        /// <summary>How thick the smoke is at a point (the camera): 0 outside, up to 1 in the middle of a fresh cloud.</summary>
        public static float Density(Vector3 p)
        {
            float best = 0f;
            foreach (var s in All)
            {
                if (p.y < s.transform.position.y - 0.3f || p.y > s.transform.position.y + Height + 0.3f) continue;
                Vector3 q = ClosestOnSegment(s.endA, s.endB, new Vector3(p.x, s.endA.y, p.z));
                float d = Vector3.Distance(new Vector3(q.x, 0f, q.z), new Vector3(p.x, 0f, p.z));
                best = Mathf.Max(best, s.Strength * Mathf.Clamp01((s.radius + 0.4f - d) / 1.2f));
            }
            return best;
        }

        /// <summary>Does any smoke cloud cut the line of sight from a to b?</summary>
        public static bool BlocksSight(Vector3 a, Vector3 b)
        {
            foreach (var s in All)
            {
                if (s.Strength < 0.35f) continue;
                float y0 = s.transform.position.y - 0.2f, y1 = s.transform.position.y + Height;
                // Both ends above or below the cloud: the line clears it.
                if ((a.y < y0 && b.y < y0) || (a.y > y1 && b.y > y1)) continue;
                Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z);
                Vector2 p2 = new Vector2(s.endA.x, s.endA.z), q2 = new Vector2(s.endB.x, s.endB.z);
                if (SegmentDistance(a2, b2, p2, q2) < s.radius * 0.9f) return true;
            }
            return false;
        }

        static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        static float SegmentDistance(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            if (Intersects(p1, q1, p2, q2)) return 0f;
            return Mathf.Min(Mathf.Min(PointSeg(p1, p2, q2), PointSeg(q1, p2, q2)), Mathf.Min(PointSeg(p2, p1, q1), PointSeg(q2, p1, q1)));
        }

        static float PointSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }
            float d1 = Cross(b - a, c - a), d2 = Cross(b - a, d - a), d3 = Cross(d - c, a - c), d4 = Cross(d - c, b - c);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        public static GhostSmoke Create(Vector3 floorPos, Quaternion facing)
        {
            var g = new GameObject("Ghost smoke");
            g.transform.position = floorPos;
            var s = g.AddComponent<GhostSmoke>();
            s.Build(facing);
            return s;
        }

        static void MakeShared()
        {
            if (puffMat != null) return;
            const int n = 64;
            puffTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "SmokePuff" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float noise = Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.23f, y * 0.23f) * 0.4f;
                    float a = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * (0.45f + 0.75f * noise);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            puffTex.SetPixels32(px);
            puffTex.Apply(true, true);
            puffMat = Gfx.UnlitGlow(puffTex, new Color(0.42f, 0.5f, 0.5f), false);
            puffMat.name = "GhostSmoke";
        }

        /// <summary>How far the space is open from <paramref name="from"/> along <paramref name="dir"/> (walls stop the smoke).</summary>
        static float Reach(Vector3 from, Vector3 dir, float max)
        {
            RaycastHit h;
            return Physics.Raycast(from, dir, out h, max, Gfx.WorldMask, QueryTriggerInteraction.Ignore) ? Mathf.Max(0.3f, h.distance - 0.3f) : max;
        }

        void Build(Quaternion facing)
        {
            MakeShared();
            born = Time.time;
            nextCall = Time.time + 1.5f;
            block = new MaterialPropertyBlock();

            // Find the corridor: the cloud runs along the longer open axis and fills the shorter one wall to wall.
            Vector3 c = transform.position + Vector3.up * 1.2f;
            float e = Reach(c, Vector3.right, MaxReach), w = Reach(c, Vector3.left, MaxReach);
            float n = Reach(c, Vector3.forward, MaxReach), s = Reach(c, Vector3.back, MaxReach);
            bool alongX = e + w >= n + s;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            float plus = alongX ? e : n, minus = alongX ? w : s, half = Mathf.Clamp((alongX ? n + s : e + w) * 0.5f, 1.4f, 3.5f);
            Vector3 across = alongX ? Vector3.forward : Vector3.right;
            float acrossMid = alongX ? (n - s) * 0.5f : (e - w) * 0.5f;   // centre between the walls
            Vector3 centre = transform.position + across * Mathf.Clamp(acrossMid, -half, half);
            endA = centre + axis * plus + Vector3.up * 1.3f;
            endB = centre - axis * minus + Vector3.up * 1.3f;
            radius = half;

            puffs = new Transform[PuffCount];
            puffR = new Renderer[PuffCount];
            puffHome = new Vector3[PuffCount];
            puffSize = new float[PuffCount];
            puffSeed = new float[PuffCount];
            for (int i = 0; i < PuffCount; i++)
            {
                var q = Gfx.Prim(PrimitiveType.Quad, "Puff", transform, Vector3.zero, Vector3.one, puffMat, false);
                var r = q.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                puffs[i] = q.transform;
                puffR[i] = r;
                float t = (i + Random.value) / PuffCount;
                Vector3 along = Vector3.Lerp(endB, endA, t) - transform.position;
                puffHome[i] = new Vector3(along.x, Random.Range(0.4f, 2.3f), along.z) + across * Random.Range(-half * 0.7f, half * 0.7f);
                puffSize[i] = Random.Range(2.6f, 3.8f) * Mathf.Clamp(half / 1.5f, 1f, 1.6f);
                puffSeed[i] = Random.value * 100f;
            }

            // The decoy: a faint copy of the ghost standing in the smoke.
            var holder = new GameObject("Decoy").transform;
            holder.SetParent(transform, false);
            holder.localPosition = Vector3.up * Ghost.FeetBelow;
            holder.rotation = facing;
            decoyBody = GhostBody.Create(holder, holder, Ghost.FeetBelow);
            if (decoyBody != null)
            {
                decoy = holder;
                decoyBody.AlwaysSeeThrough = true;
                decoyBody.FloorY = transform.position.y;
            }
            else Destroy(holder.gameObject);

            hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = Sfx.Whisper;
            hiss.loop = true;
            hiss.spatialBlend = 1f;
            hiss.rolloffMode = AudioRolloffMode.Linear;
            hiss.minDistance = 2f;
            hiss.maxDistance = 16f;
            hiss.pitch = 0.6f;
            hiss.volume = 0.5f;
            hiss.Play();
            Sfx.At(Sfx.Drone, transform.position, 0.8f, 0.5f, 25f);
        }

        void Update()
        {
            float age = Age;
            if (age > Life) { Destroy(gameObject); return; }
            var gm = GameManager.Instance;
            if (gm != null && gm.IsPaused) return;
            float strength = Strength;
            var p = PlayerController.Instance;
            var cam = p != null && p.Cam != null ? p.Cam.transform : (Camera.main != null ? Camera.main.transform : null);

            // Puffs: camera-facing, slowly rolling and breathing. Thick enough to hide whatever is behind them.
            float spread = Mathf.Clamp01(age / 3f);
            for (int i = 0; i < puffs.Length; i++)
            {
                float t = Time.time * 0.25f + puffSeed[i];
                Vector3 home = Vector3.Lerp(new Vector3(0f, puffHome[i].y, 0f), puffHome[i], spread);   // it billows out from where the ghost stood
                Vector3 pos = transform.position + home + new Vector3(Mathf.Sin(t) * 0.6f, Mathf.Sin(t * 0.7f) * 0.2f, Mathf.Cos(t * 0.8f) * 0.6f);
                float size = puffSize[i] * (0.6f + 0.4f * spread) * (0.9f + 0.1f * Mathf.Sin(t * 1.3f));
                puffs[i].position = pos;
                puffs[i].localScale = Vector3.one * size;
                float near = 1f;
                if (cam != null)
                {
                    puffs[i].rotation = Quaternion.LookRotation(pos - cam.position, cam.up) * Quaternion.Euler(0f, 0f, puffSeed[i] * 40f + Time.time * 6f);
                    // Right in front of the lens a puff would just be a flat grey card: fade those, the screen haze takes over.
                    near = Mathf.Clamp01((Vector3.Distance(pos, cam.position) - 0.4f) / 1.2f);
                }
                block.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 0.62f * strength * near));
                puffR[i].SetPropertyBlock(block);
            }
            hiss.volume = 0.5f * strength;

            // False signal: fake footsteps and whispers that every zombie in earshot comes to check.
            if (Time.time >= nextCall && age < Life - 5f)
            {
                nextCall = Time.time + Random.Range(5f, 7.5f);
                Vector3 at = DecoyPosition;
                NoiseSystem.Emit(at, 20f);
                Calls++;
                if (Random.value < 0.5f) { Sfx.At(Sfx.Step, at, 0.9f, 0.8f, 22f); Sfx.At(Sfx.Step, at + Vector3.right * 0.3f, 0.8f, 0.75f, 22f); }
                else Sfx.At(Sfx.Whisper, at, 0.9f, Random.Range(0.8f, 1.1f), 22f);
            }

            if (decoy != null)
            {
                // It stands in the smoke and turns to watch you - until a torch beam shows there's nothing there.
                if (p != null)
                {
                    Vector3 f = Vector3.ProjectOnPlane(p.transform.position - decoy.position, Vector3.up);
                    if (f.sqrMagnitude > 0.01f) decoy.rotation = Quaternion.Slerp(decoy.rotation, Quaternion.LookRotation(f), Time.deltaTime * 1.5f);
                    decoyBody.LookAt = p.EyePosition;
                    if (!exposed && p.IlluminatesPoint(decoy.position)) lightOnDecoy += Time.deltaTime; else lightOnDecoy = Mathf.Max(0f, lightOnDecoy - Time.deltaTime);
                    if (!exposed && lightOnDecoy > 0.7f)
                    {
                        exposed = true;
                        if (gm != null) gm.Toast("Just smoke... the real one is somewhere else.");
                    }
                }
                decoyFade = Mathf.MoveTowards(decoyFade, exposed ? 0f : strength, Time.deltaTime * (exposed ? 1.5f : 1f));
                float a = decoyFade * (0.55f + 0.2f * Mathf.PerlinNoise(Time.time * 2f, 1.7f));
                decoyBody.SetLook(a, new Color(0.3f, 0.45f, 0.6f) * 0.8f);
                if (decoyFade <= 0.01f && exposed) { Destroy(decoy.gameObject); decoy = null; }
            }
        }
    }
}
