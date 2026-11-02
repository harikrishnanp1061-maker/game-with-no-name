using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The giant that walks the campus at night: the tutorial's pit monster (Monster8) blown up to some 48 m tall. It
    /// wanders the open grounds on its four stilt legs, sweeping its long neck round and round, a dim red gaze following
    /// where its head looks. Anyone outdoors in its sight fills an exposure bar (faster when close, in the gaze, or
    /// running; slower crouched; cover - buildings, roofs, tree crowns - breaks its line of sight). When someone's bar
    /// fills, it rears up and roars, turns, and strides after them; if it gets its head over them in the open it slams it
    /// down and kills them. Lose it indoors and it searches, then goes back to wandering.
    ///
    /// Everything it does is animated here, procedurally, on the model's own bones (the pit clips don't fit): planted
    /// feet with two-bone IK and a diagonal walking gait, slow heavy steps that shake the ground, a body that dips and
    /// sways with each footfall, a snaking neck driven to its look point by CCD IK, the rear-up and the head slam. It
    /// thinks on the host; GiantNet carries its mode, look and exposures, and every machine animates from those.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class CampusGiant : MonoBehaviour
    {
        public enum Mode { Roam = 0, Roar = 1, Chase = 2, Strike = 3, Search = 4 }
        public static CampusGiant Instance { get; private set; }

        [Header("Walking")]
        public Vector3[] waypoints;
        public float roamSpeed = 2.4f, chaseSpeed = 7.5f, roamTurn = 9f, chaseTurn = 26f;
        [Header("Seeing")]
        [Tooltip("Seconds of plain view (mid distance, not in the gaze) to fill the bar.")]
        public float fillSeconds = 7f;
        public float viewRange = 260f, viewHalfAngle = 62f;
        [Header("Parts")]
        public Light gaze;
        public Light eyeLight;
        [Tooltip("A pale light hung in front of its face, so the face reads from a distance at night.")]
        public Light faceLight;
        public Renderer[] eyes;
        public Material dustMaterial;

        public Mode Current { get; private set; }
        public int Target { get; private set; } = -1;
        public readonly float[] Exposure = new float[4];
        /// <summary>DEV: stands still and sees nobody.</summary>
        public bool Paused { get; private set; }
        public Vector3 GazeDirection { get; private set; }

        float S;                       // the giant's scale (model metres to world metres)
        float modeStart, lostFor, nextScan, pauseUntil, nextGrowl;
        int waypoint;
        Vector3 lastSeen, strikePoint;
        bool struck, killed, roared, slammed;
        float lookYaw, lookPitch = 50f, yawTarget, pitchTarget = 50f, yawVel, pitchVel;
        Vector3 lastPos, vel; float lastYaw, yawRate;
        GiantNet net;

        // ------------------------------------------------------------------ bones
        Transform root, hips, spineMid, neckBase, head;
        Transform[] neck;
        Quaternion[] neckRest;
        Vector3 faceLocal;                          // the face's forward, in the head bone's space
        float nYaw, nDip, nSwing, vYaw, vDip, vSwing, gDip, gSwing;
        Vector3 aimPoint, aimVel;
        /// <summary>The player it has noticed and is staring at (before the bar fills), -1 for none.</summary>
        public int Watching { get; private set; } = -1;
        float watchLost;
        readonly List<Transform> pose = new List<Transform>();
        Quaternion[] restRot; Vector3[] restPos;

        class Leg
        {
            public Transform upper, lower, end;
            public bool front; public float side;
            public Vector3 rest; public float ankle;
            public Vector3 planted, from, to, foot;
            public bool stepping; public float t, dur;
        }
        readonly Leg[] legs = new Leg[4];

        // ------------------------------------------------------------------ setup
        void Awake()
        {
            Instance = this;
            S = transform.lossyScale.x;
            net = GetComponent<GiantNet>();
            var anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;
            root = Bone("ROOT"); hips = Bone("Bone"); spineMid = Bone("Bone_002"); neckBase = Bone("Bone_004"); head = Bone("Bone_006");
            neck = new[] { Bone("Bone_003"), Bone("Bone_004"), Bone("Bone_005"), Bone("Bone_012"), Bone("Bone_011"), Bone("Bone_010"), Bone("Bone_009"), Bone("Bone_008") };
            foreach (var t in root.GetComponentsInChildren<Transform>()) pose.Add(t);
            neckRest = new Quaternion[neck.Length];
            for (int i = 0; i < neck.Length; i++) neckRest[i] = neck[i].localRotation;
            // the face looks from the head bone towards the eyes (model space: the eyes sit at z 2.5, the bone at 2.0)
            faceLocal = head.InverseTransformDirection((transform.TransformPoint(new Vector3(0.011f, 1.30f, 2.50f)) - head.position).normalized);
            aimPoint = head.position + transform.forward * S;
            restRot = new Quaternion[pose.Count]; restPos = new Vector3[pose.Count];
            for (int i = 0; i < pose.Count; i++) { restRot[i] = pose[i].localRotation; restPos[i] = pose[i].localPosition; }
            legs[0] = MakeLeg("Bone_008_l", "Bone_009_l", "foot_l", false, -1f);
            legs[1] = MakeLeg("Bone_008_r", "Bone_009_r", "foot_r", false, 1f);
            legs[2] = MakeLeg("Bone_011_l", "Bone_012_l", "foot2_l", true, -1f);
            legs[3] = MakeLeg("Bone_011_r", "Bone_012_r", "foot2_r", true, 1f);
            lastPos = transform.position; lastYaw = transform.eulerAngles.y;
            yawTarget = lookYaw = 0f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        Transform Bone(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            Debug.LogWarning("[Giant] no bone " + n);
            return transform;
        }

        Leg MakeLeg(string upper, string lower, string end, bool front, float side)
        {
            var l = new Leg { upper = Bone(upper), lower = Bone(lower), end = Bone(end), front = front, side = side };
            Vector3 local = transform.InverseTransformPoint(l.end.position);
            l.ankle = l.end.position.y - transform.position.y;
            local.y = 0f; local.x *= 1.3f;   // a wider stance than the pit pose: braced, spidery
            l.rest = local;
            l.planted = l.foot = Ground(transform.TransformPoint(local));
            return l;
        }

        bool Authority { get { return net == null || !net.IsSpawned || net.IsServer; } }

        // ------------------------------------------------------------------ distractions (host)
        bool lured; CarAlarm lureCar; float lureLook;
        /// <summary>True while it is walking to (or stomping) a distraction rather than hunting.</summary>
        public bool Lured { get { return lured; } }

        /// <summary>Something noisy or bright out on the campus (CampusLure): it walks over to look. A car alarm it
        /// stomps flat. Ignored while it is hunting someone - the alarm keeps calling, so it comes once it gives up.</summary>
        public void Lure(Vector3 p, CarAlarm car)
        {
            if (!Authority || Paused) return;
            if (Current == Mode.Roar || Current == Mode.Chase || Current == Mode.Strike) return;
            if (lured && lureCar != null && car == null) return;   // a screaming car beats a phone
            lured = true; lureCar = car; lastSeen = p; Target = -1; lureLook = 0f;
            Enter(Mode.Search);
            lured = true;
        }

        // ------------------------------------------------------------------ thinking (host)
        bool warned;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // the first time this machine's player steps outside: what walks out there, and how to live
            var me = PlayerParty.Instance;
            if (!warned && me != null && me.Current != null && !Paused && !Indoors(me.Current.transform.position))
            {
                warned = true;
                me.Flash("Something enormous walks the campus. Stay out of its sight - move while it looks away, hide indoors or under trees.", 7f);
            }
            if (!Authority) { FromNet(); return; }
            var party = PlayerParty.Instance;
            if (Paused || party == null) { Push(); return; }

            See(party, dt);
            ReleaseWatch(party);
            float tm = Time.time - modeStart;
            var body = Target >= 0 && Target < party.players.Length ? party.players[Target] : null;
            bool targetOk = body != null && !body.IsDead;
            switch (Current)
            {
                case Mode.Roam:
                    Scan(dt, false);
                    if (Time.time > pauseUntil)
                    {
                        Vector3 wp = waypoints != null && waypoints.Length > 0 ? waypoints[waypoint] : transform.position;
                        if (Steer(wp, roamSpeed, roamTurn, dt) < 14f)
                        {
                            waypoint = (waypoint + 1) % Mathf.Max(1, waypoints.Length);
                            if (Random.value < 0.6f) pauseUntil = Time.time + Random.Range(4f, 9f);   // stops to look round
                        }
                    }
                    Growl();
                    break;
                case Mode.Roar:
                    if (tm > 4.3f) Enter(targetOk && !killed ? Mode.Chase : Mode.Roam);
                    break;
                case Mode.Chase:
                    if (!targetOk) { Enter(Mode.Roam); break; }
                    Vector3 tp = body.transform.position;
                    bool sees = CanSee(body);
                    if (sees) { lastSeen = tp; lostFor = 0f; } else lostFor += dt;
                    if (lostFor > 7f) { Exposure[Target] = 0.55f; Enter(Mode.Search); break; }
                    LookAtPoint(lastSeen + Vector3.up, dt);
                    Vector3 flat = lastSeen - transform.position; flat.y = 0f;
                    float ang = Vector3.Angle(transform.forward, flat);
                    float reach = 1.85f * S;
                    if (flat.magnitude < reach && ang < 30f)
                    {
                        if (sees) { strikePoint = tp; Enter(Mode.Strike); }
                    }
                    else Steer(lastSeen, chaseSpeed, chaseTurn, dt, reach * 0.85f);
                    break;
                case Mode.Strike:
                    if (tm < 1.05f && targetOk && CanSee(body)) strikePoint = body.transform.position;   // tracks until the slam
                    if (!struck && tm > 1.28f)
                    {
                        struck = true;
                        if (targetOk && !Indoors(body.transform.position) && Flat(body.transform.position - strikePoint) < 0.5f * S)
                        {
                            var shout = UserSfx.Get("monster_shout");
                            if (shout != null) PlayAt(shout, head.position, 1f, 0.85f, 0.06f * S, 500f);
                            party.KillPlayer(body, "was crushed by the giant");
                            killed = true;
                        }
                        if (lured)
                        {
                            if (lureCar != null) lureCar.Crush();
                            foreach (var pl in party.players)
                                if (pl != null && !pl.IsDead && !Indoors(pl.transform.position) && Flat(pl.transform.position - strikePoint) < 0.45f * S)
                                    party.KillPlayer(pl, "was crushed with the car");
                        }
                    }
                    if (tm > 2.4f)
                    {
                        if (killed) { Exposure[Target] = 0f; Enter(Mode.Roar); }
                        else Enter(targetOk ? Mode.Chase : Mode.Roam);
                    }
                    break;
                case Mode.Search:
                    if (lured)
                    {
                        if (lureCar != null && (lureCar.Crushed || !lureCar.Sounding && tm > 40f)) { lured = false; lureCar = null; Enter(Mode.Roam); break; }
                        float left = Steer(lastSeen, roamSpeed * 1.9f, roamTurn * 2f, dt, lureCar != null ? 1.5f * S : 6f);
                        LookAtPoint(lastSeen + Vector3.up, dt);
                        Vector3 toLure = lastSeen - transform.position; toLure.y = 0f;
                        if (lureCar != null && left < 1.85f * S && Vector3.Angle(transform.forward, toLure) < 30f) { strikePoint = lastSeen; Enter(Mode.Strike); lured = true; break; }
                        if (lureCar == null && left < 10f) { lureLook += dt; if (lureLook > 9f) { lured = false; Enter(Mode.Roam); } }
                        if (tm > 70f) { lured = false; lureCar = null; Enter(Mode.Roam); }
                        break;
                    }
                    Scan(dt, true);
                    if (Steer(lastSeen, roamSpeed * 1.4f, roamTurn * 1.6f, dt) < 20f && tm > 14f) { Target = -1; Enter(Mode.Roam); }
                    if (tm > 26f) { Target = -1; Enter(Mode.Roam); }
                    break;
            }
            Push();
        }

        void Enter(Mode m)
        {
            lured = false; lureCar = m == Mode.Strike || m == Mode.Search ? lureCar : null;
            if (m == Mode.Roar && Current != Mode.Strike) killed = false;
            Current = m; modeStart = Time.time;
            struck = false; roared = false; slammed = false;
            if (m == Mode.Chase) lostFor = 0f;
            if (m == Mode.Roam) { Target = -1; killed = false; }
        }

        /// <summary>Who it can see, and how much each of them has been seen.</summary>
        void See(PlayerParty party, float dt)
        {
            if (DevTools.MonstersPassive)
            {
                for (int i = 0; i < 4; i++) Exposure[i] = 0f;
                Watching = -1;
                if (Current == Mode.Chase || Current == Mode.Roar) Enter(Mode.Roam);
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                var b = i < party.players.Length ? party.players[i] : null;
                if (b == null || b.IsDead || PlayerParty.SitsOut(i)) { Exposure[i] = 0f; continue; }
                float rate = SightRate(b);
                bool indoors = rate < 0f;
                if (rate > 0f) Exposure[i] = Mathf.Min(1f, Exposure[i] + rate * dt);
                else if (!(Current == Mode.Chase && i == Target)) Exposure[i] = Mathf.Max(0f, Exposure[i] - dt / (indoors ? 6f : 14f));
                if (Current == Mode.Roam || Current == Mode.Search)
                {
                    // it notices you before it decides: past a quarter of the bar, its head locks on and stares
                    if (i == Watching) { if (rate > 0f) watchLost = 0f; else watchLost += dt; }
                    else if (rate > 0f && Exposure[i] > 0.25f && (Watching < 0 || Exposure[i] > Exposure[Watching] + 0.15f)) { Watching = i; watchLost = 0f; }
                }
                if (Exposure[i] >= 1f && (Current == Mode.Roam || Current == Mode.Search))
                {
                    Target = i; lastSeen = b.transform.position;
                    Enter(Mode.Roar);
                    if (party != null) party.Flash("The giant has seen Player " + (i + 1) + "!", 3f);
                }
            }
        }

        void ReleaseWatch(PlayerParty party)
        {
            if (Watching < 0) return;
            var b = Watching < party.players.Length ? party.players[Watching] : null;
            if (Current != Mode.Roam && Current != Mode.Search || b == null || b.IsDead || Exposure[Watching] < 0.08f || watchLost > 2.5f) Watching = -1;
        }

        /// <summary>How fast this player's exposure fills (per second); 0 out of sight, -1 indoors.</summary>
        float SightRate(RemasteredPlayerController b)
        {
            Vector3 p = b.transform.position;
            if (Indoors(p)) return -1f;
            // close to its body - under its belly, between its legs - it feels you, wherever its head is looking:
            // the bar fills in a couple of seconds (slower crouched)
            float body = Flat(p - transform.position);
            if (body < 1.6f * S) return Mathf.Lerp(1f / 1.8f, 1f / 3.5f, body / (1.6f * S)) * (b.IsCrouching ? 0.6f : 1f);
            Vector3 eye = head.position;
            Vector3 to = p + Vector3.up * 1.2f - eye;
            float d = to.magnitude;
            if (d > viewRange) return 0f;
            Vector3 flatTo = new Vector3(to.x, 0f, to.z);
            float cone = Vector3.Angle(new Vector3(GazeDirection.x, 0f, GazeDirection.z), flatTo);
            if (cone > viewHalfAngle) return 0f;
            if (!Clear(eye, p + Vector3.up * 1.2f)) return 0f;
            float distF = Mathf.Lerp(1.8f, 0.35f, Mathf.InverseLerp(40f, viewRange, d));
            float beam = gaze != null && Vector3.Angle(GazeDirection, to) < gaze.spotAngle * 0.5f ? 2.4f : 1f;
            var cc = b.GetComponent<CharacterController>();
            float move = cc != null && cc.velocity.magnitude > 4.5f ? 1.35f : 1f;
            float crouch = b.IsCrouching ? 0.55f : 1f;
            return distF * beam * move * crouch / fillSeconds;
        }

        bool CanSee(RemasteredPlayerController b)
        {
            Vector3 p = b.transform.position;
            return !Indoors(p) && Vector3.Distance(head.position, p) < viewRange && Clear(head.position, p + Vector3.up * 1.2f);
        }

        // tree crowns are triggers on Ignore Raycast: they hide you from it, but nothing walks into them
        const int CrownMask = 1 << 2;

        /// <summary>A roof, a ceiling or a tree crown overhead.</summary>
        public static bool Indoors(Vector3 p)
        {
            Vector3 from = p + Vector3.up * 2.2f;
            return Physics.Raycast(from, Vector3.up, 90f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(p + Vector3.up * 60f, Vector3.down, 57.8f, CrownMask, QueryTriggerInteraction.Collide);   // from above: a ray starting inside a crown misses it
        }

        static bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; float len = d.magnitude;
            if (Physics.Raycast(a, d / len, len - 0.5f, CrownMask, QueryTriggerInteraction.Collide)) return false;
            foreach (var h in Physics.RaycastAll(a, d / len, len - 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() == null && h.collider.GetComponentInParent<CampusGiant>() == null) return false;
            return true;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        /// <summary>Turns towards dest and walks; turns on the spot first when facing away. Returns the distance left.</summary>
        float Steer(Vector3 dest, float speed, float turn, float dt, float stopAt = 0f)
        {
            Vector3 to = dest - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.1f) return dist;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, want, turn * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float align = Mathf.Clamp01(Mathf.Cos(Mathf.DeltaAngle(yaw, want) * Mathf.Deg2Rad));
            if (dist > stopAt) transform.position += transform.forward * speed * align * align * dt;
            return dist;
        }

        /// <summary>The long head searching: a new direction every second or three, quick, with pauses between.</summary>
        void Scan(float dt, bool frantic)
        {
            if (Time.time > nextScan)
            {
                nextScan = Time.time + (frantic ? Random.Range(0.8f, 1.8f) : Random.Range(1.6f, 4f));
                yawTarget = Random.Range(frantic ? -130f : -105f, frantic ? 130f : 105f);
                pitchTarget = Random.Range(18f, 48f);
            }
            float smooth = frantic ? 0.45f : 0.9f;
            lookYaw = Mathf.SmoothDampAngle(lookYaw, yawTarget, ref yawVel, smooth);
            lookPitch = Mathf.SmoothDamp(lookPitch, pitchTarget, ref pitchVel, smooth);
        }

        void LookAtPoint(Vector3 p, float dt)
        {
            Vector3 to = p - neckBase.position;
            Vector3 local = Quaternion.Inverse(transform.rotation) * to;
            float y = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pt = Mathf.Atan2(-local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            lookYaw = Mathf.SmoothDampAngle(lookYaw, Mathf.Clamp(y, -140f, 140f), ref yawVel, 0.35f);
            lookPitch = Mathf.SmoothDamp(lookPitch, Mathf.Clamp(pt, 10f, 80f), ref pitchVel, 0.35f);
        }

        void Growl()
        {
            if (Time.time < nextGrowl) return;
            nextGrowl = Time.time + Random.Range(12f, 24f);
            var bank = SfxBank.Instance;
            if (bank != null && bank.monsterLurk != null) PlayAt(bank.monsterLurk, head.position, 1f, Random.Range(0.32f, 0.4f), 0.05f * S, 420f);
        }

        void Push()
        {
            int t = Current == Mode.Roam || Current == Mode.Search ? Watching : Target;
            if (net != null) net.Push((int)Current, t, new Vector2(lookYaw, lookPitch), new Vector4(Exposure[0], Exposure[1], Exposure[2], Exposure[3]), strikePoint);
        }

        void FromNet()
        {
            if (net == null) return;
            var m = (Mode)net.Mode;
            if (m != Current) { Current = m; modeStart = Time.time; struck = roared = slammed = false; }
            Target = net.Target;
            Watching = m == Mode.Roam || m == Mode.Search ? net.Target : -1;
            lookYaw = Mathf.SmoothDampAngle(lookYaw, net.Look.x, ref yawVel, 0.25f);
            lookPitch = Mathf.SmoothDamp(lookPitch, net.Look.y, ref pitchVel, 0.25f);
            var e = net.Exposure; Exposure[0] = e.x; Exposure[1] = e.y; Exposure[2] = e.z; Exposure[3] = e.w;
            strikePoint = net.Strike;
        }

        // ------------------------------------------------------------------ dev
        public void DevTogglePause()
        {
            Paused = !Paused;
            for (int i = 0; i < 4; i++) Exposure[i] = 0f;
            Enter(Mode.Roam);
        }

        public void DevEnrage(int player)
        {
            var party = PlayerParty.Instance;
            if (!Authority || party == null || player < 0 || player >= party.players.Length) return;
            Paused = false; Target = player; lastSeen = party.players[player].transform.position; Exposure[player] = 1f;
            Enter(Mode.Roar);
        }

        // ------------------------------------------------------------------ animation (every machine)
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            vel = Vector3.Lerp(vel, (transform.position - lastPos) / dt, 1f - Mathf.Exp(-dt * 6f));
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y) / dt, 1f - Mathf.Exp(-dt * 4f));
            lastPos = transform.position; lastYaw = transform.eulerAngles.y;

            for (int i = 0; i < pose.Count; i++) { pose[i].localRotation = restRot[i]; pose[i].localPosition = restPos[i]; }
            float tm = Time.time - modeStart;

            // the body: lowered (knees bent), pitched for the rear-up and the slam, dipping and swaying with the steps
            float pitch = 0f, drop = 0.18f, frontLift = 0f;
            if (Current == Mode.Roar)
            {
                float r = Smooth(0f, 1.5f, tm) * (1f - Smooth(3.0f, 3.45f, tm));
                pitch = -27f * r; frontLift = r; drop -= 0.04f * r;
                if (!roared && tm > 1.0f) { roared = true; Roar(); }
                if (!slammed && tm > 3.4f) { slammed = true; foreach (var l in legs) if (l.front) Footfall(l.planted, 1.7f); }
            }
            else if (Current == Mode.Strike)
            {
                float wind = Smooth(0f, 1.0f, tm), slam = Smooth(1.0f, 1.28f, tm), rec = Smooth(1.6f, 2.4f, tm);
                pitch = (-9f * wind * (1f - slam) + 24f * slam) * (1f - rec);
                drop += 0.34f * slam * (1f - rec);
                if (!slammed && tm > 1.28f) { slammed = true; Footfall(strikePoint, 2.2f); }
            }
            float bob = 0f, roll = 0f;
            foreach (var l in legs)
                if (l.stepping) { float s = Mathf.Sin(l.t * Mathf.PI); bob += s * 0.022f; roll += l.side * s * 1.4f; }
            roll += Mathf.Clamp(yawRate * 0.08f, -4f, 4f);
            BodyPose(pitch, roll, drop - bob);
            spineMid.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 0.55f) * 1.4f, transform.right) * spineMid.rotation;   // breathing

            // where the head goes, and what its face looks at
            Vector3 lookDir = transform.rotation * Quaternion.Euler(lookPitch, lookYaw, 0f) * Vector3.forward;
            Vector3 basePos = neck[0].position;
            Vector3 headGoal = basePos + lookDir * 2.0f * S, lookAt = basePos + lookDir * 6f * S;
            float settle = 0.55f;
            var party = PlayerParty.Instance;
            int lockOn = Current == Mode.Chase || Current == Mode.Roar ? Target : Watching;
            var locked = party != null && lockOn >= 0 && lockOn < party.players.Length ? party.players[lockOn] : null;
            if (locked != null && !locked.IsDead && Current != Mode.Strike)
            {
                // locked on: the head reaches towards them as far as the neck goes, the face stares straight at them
                Vector3 eye = locked.transform.position + Vector3.up * 1.4f;
                Vector3 to = eye - basePos;
                headGoal = basePos + to.normalized * Mathf.Min(to.magnitude, 1.9f * S);
                lookAt = eye; settle = 0.3f;
            }
            if (Current == Mode.Roar)
            {
                float r = Smooth(0f, 1.5f, tm) * (1f - Smooth(3.0f, 3.6f, tm));
                float thrash = Smooth(1.2f, 1.6f, tm) * (1f - Smooth(2.9f, 3.2f, tm));
                Vector3 high = basePos + (transform.forward * 0.9f + Vector3.up * 0.75f + transform.right * Mathf.Sin(tm * 4.2f) * 0.45f * thrash) * S;
                headGoal = Vector3.Lerp(headGoal, high, r);
                lookAt = Vector3.Lerp(lookAt, high + (transform.forward + Vector3.up * 1.4f) * S, r);   // roaring at the sky
                settle = 0.35f;
            }
            else if (Current == Mode.Strike)
            {
                float wind = Smooth(0f, 1.0f, tm), slam = Smooth(1.0f, 1.28f, tm), rec = Smooth(1.6f, 2.4f, tm);
                Vector3 back = basePos + (Vector3.up * 0.75f + transform.forward * 0.35f) * S;
                Vector3 goal = Vector3.Lerp(Vector3.Lerp(headGoal, back, wind), strikePoint + Vector3.up * 0.4f, slam);
                headGoal = Vector3.Lerp(goal, headGoal, rec);
                lookAt = strikePoint;
                settle = slam > 0f && rec < 1f ? 0.06f : 0.25f;
            }
            if (Current != Mode.Strike) headGoal.y = Mathf.Max(headGoal.y, transform.position.y + 0.55f * S);   // it hunts with its head up, not between its feet
            Neck(headGoal, settle, dt);
            aimPoint = Vector3.SmoothDamp(aimPoint, lookAt, ref aimVel, locked != null ? 0.12f : 0.35f);
            AimFace(aimPoint);
            GazeDirection = head.TransformDirection(faceLocal);

            Gait(dt, frontLift);
            foreach (var l in legs) LegIK(l);
            Glow(tm);
            Shake(dt);
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        void BodyPose(float pitch, float roll, float drop)
        {
            Vector3 pivot = hips.position;
            Quaternion r = Quaternion.AngleAxis(pitch, transform.right) * Quaternion.AngleAxis(roll, transform.forward);
            root.position = pivot + r * (root.position - pivot) - Vector3.up * drop * S;
            root.rotation = r * root.rotation;
        }

        /// <summary>
        /// The neck bends as one smooth curve from three numbers: a turn (spread over the base of the neck, so the
        /// hanging part swings round in an arc), a dip (the rising part bowing down) and a swing (the hanging part
        /// swinging forward and up). Each frame a few Newton steps find the dip and swing that put the head on its
        /// goal; the pose eases towards them, so it moves like something heavy, never kinks, and never snaps.
        /// </summary>
        void Neck(Vector3 goal, float settle, float dt)
        {
            Vector3 p0 = neck[0].position;
            Vector3 local = Quaternion.Inverse(transform.rotation) * (goal - p0);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -125f, 125f);
            Vector3 fwd = Quaternion.AngleAxis(transform.eulerAngles.y + yaw, Vector3.up) * Vector3.forward;
            float fT = Vector3.Dot(goal - p0, fwd), hT = goal.y - p0.y;
            for (int it = 0; it < 3; it++)
            {
                Vector2 e = Eval(yaw, gDip, gSwing, p0, fwd);
                Vector2 eD = Eval(yaw, gDip + 2f, gSwing, p0, fwd), eS = Eval(yaw, gDip, gSwing + 2f, p0, fwd);
                float a = (eD.x - e.x) / 2f, b = (eS.x - e.x) / 2f, c = (eD.y - e.y) / 2f, d = (eS.y - e.y) / 2f;
                float det = a * d - b * c;
                if (Mathf.Abs(det) < 1e-5f) break;
                float rf = fT - e.x, rh = hT - e.y;
                float dDip = (d * rf - b * rh) / det, dSwing = (-c * rf + a * rh) / det;
                gDip = Mathf.Clamp(gDip + Mathf.Clamp(dDip, -18f, 18f), -30f, 55f);
                gSwing = Mathf.Clamp(gSwing + Mathf.Clamp(dSwing, -18f, 18f), -35f, 85f);
            }
            nYaw = Mathf.SmoothDampAngle(nYaw, yaw, ref vYaw, settle * 1.15f);
            nDip = Mathf.SmoothDamp(nDip, gDip, ref vDip, settle);
            nSwing = Mathf.SmoothDamp(nSwing, gSwing, ref vSwing, settle);
            NeckPose(nYaw, nDip, nSwing, true);
        }

        Vector2 Eval(float yaw, float dip, float swing, Vector3 p0, Vector3 fwd)
        {
            NeckPose(yaw, dip, swing, false);
            Vector3 h = head.position - p0;
            return new Vector2(Vector3.Dot(h, fwd), h.y);
        }

        static readonly float[] YawShare = { 0.3f, 0.35f, 0.35f, 0f, 0f, 0f, 0f, 0f };
        static readonly float[] DipShare = { 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f };
        static readonly float[] SwingShare = { 0f, 0f, 0.2f, 0.3f, 0.3f, 0.2f, 0f, 0f };

        void NeckPose(float yaw, float dip, float swing, bool alive)
        {
            for (int i = 0; i < neck.Length; i++) neck[i].localRotation = neckRest[i];
            Vector3 up = Vector3.up;
            Vector3 side = Quaternion.AngleAxis(yaw, up) * transform.right;
            float t = Time.time;
            for (int i = 0; i < neck.Length; i++)
            {
                Quaternion q = Quaternion.AngleAxis(yaw * YawShare[i], up) * Quaternion.AngleAxis(dip * DipShare[i] - swing * SwingShare[i], side);
                // a slow, even S-curve sway down the hanging part (the same wave along it, so it never kinks)
                if (alive && i >= 3) q = Quaternion.AngleAxis(Mathf.Sin(t * 0.55f - i * 0.45f) * 1.6f, up) * q;
                neck[i].rotation = q * neck[i].rotation;
            }
        }

        /// <summary>The head turns its face towards the point: a little of it through the last neck bone, the rest
        /// through the head itself, within what a neck can do.</summary>
        void AimFace(Vector3 point)
        {
            var jaw = neck[neck.Length - 1];
            for (int pass = 0; pass < 2; pass++)
            {
                Transform b = pass == 0 ? jaw : head;
                Vector3 face = head.TransformDirection(faceLocal);
                Vector3 want = point - head.position;
                if (want.sqrMagnitude < 1e-4f) return;
                Quaternion q = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(face, want.normalized), pass == 0 ? 25f : 50f);
                b.rotation = Quaternion.Slerp(Quaternion.identity, q, pass == 0 ? 0.45f : 1f) * b.rotation;
            }
        }

        // ------------------------------------------------------------------ legs
        Vector3 Home(Leg l, float lead)
        {
            return Ground(transform.TransformPoint(l.rest) + vel * lead);
        }

        void Gait(float dt, float frontLift)
        {
            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            float dur = Mathf.Lerp(1.55f, 0.72f, Mathf.InverseLerp(roamSpeed, chaseSpeed, speed));
            bool walking = speed > 0.3f || Mathf.Abs(yawRate) > 2f;
            float threshold = walking ? 0.12f * S : 0.05f * S;
            bool lifted = frontLift > 0.02f;

            // the next pair may lift once every stepping foot is most of the way down
            bool canStart = true;
            foreach (var l in legs) if (l.stepping && l.t < 0.72f) canStart = false;
            if (canStart)
            {
                // diagonal pairs: rear-left with front-right, rear-right with front-left
                float e0 = Mathf.Max(Err(legs[0], dur), lifted ? 0f : Err(legs[3], dur));
                float e1 = Mathf.Max(Err(legs[1], dur), lifted ? 0f : Err(legs[2], dur));
                int g = e0 >= e1 ? 0 : 1;
                if (Mathf.Max(e0, e1) > threshold)
                {
                    var a = legs[g == 0 ? 0 : 1]; var b = legs[g == 0 ? 3 : 2];
                    foreach (var l in new[] { a, b })
                        if (!l.stepping && !(lifted && l.front) && Err(l, dur) > 0.02f * S) Begin(l, Home(l, dur * 0.6f), dur);
                }
            }
            foreach (var l in legs)
            {
                if (l.stepping)
                {
                    l.t += dt / l.dur;
                    float k = Mathf.Clamp01(l.t);
                    float e = k * k * (3f - 2f * k);
                    float arc = Mathf.Clamp(Flat(l.to - l.from) * 0.35f, 0.04f * S, 0.3f * S);
                    l.foot = Vector3.Lerp(l.from, l.to, e) + Vector3.up * arc * Mathf.Sin(k * Mathf.PI);
                    if (l.t >= 1f) { l.stepping = false; l.planted = l.foot = l.to; Footfall(l.to, Current == Mode.Chase ? 1.0f : 0.7f); }
                }
                else l.foot = l.planted;
                if (l.front && frontLift > 0f)
                {
                    // reared up: the front feet come off the ground and paw at the air
                    l.foot = l.planted + Vector3.up * frontLift * 0.6f * S + transform.forward * frontLift * 0.12f * S
                           + Vector3.up * Mathf.Sin(Time.time * 3.2f + l.side) * 0.05f * S * frontLift;
                }
            }
        }

        float Err(Leg l, float dur) { return l.stepping ? 0f : Flat(Home(l, dur * 0.6f) - l.planted); }

        void Begin(Leg l, Vector3 to, float dur)
        {
            l.stepping = true; l.t = 0f; l.dur = dur * Random.Range(0.94f, 1.06f);
            l.from = l.planted; l.to = to;
        }

        /// <summary>Two-bone IK: upper and lower turn so the foot lands on its target, the knee splayed out and up.</summary>
        void LegIK(Leg l)
        {
            Vector3 target = l.foot + Vector3.up * l.ankle;
            Vector3 a = l.upper.position, b = l.lower.position, c = l.end.position;
            float lab = (b - a).magnitude, lcb = (c - b).magnitude;
            float lat = Mathf.Clamp((target - a).magnitude, 0.01f, lab + lcb - 0.001f * S);
            Vector3 outward = transform.right * l.side;
            Vector3 pole = (a + target) * 0.5f + (outward * 0.7f + Vector3.up * 0.55f + transform.forward * (l.front ? 0.25f : -0.15f)) * S;

            float ac_ab0 = Angle(c - a, b - a), ba_bc0 = Angle(a - b, c - b), ac_at0 = Angle(c - a, target - a);
            float ac_ab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float ba_bc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));
            Vector3 axis0 = Vector3.Cross(c - a, pole - a).normalized;
            if (axis0.sqrMagnitude < 1e-6f) axis0 = transform.forward;
            Vector3 axis1 = Vector3.Cross(c - a, target - a);
            axis1 = axis1.sqrMagnitude < 1e-8f ? axis0 : axis1.normalized;

            l.lower.rotation = Quaternion.AngleAxis((ba_bc1 - ba_bc0) * Mathf.Rad2Deg, axis0) * l.lower.rotation;
            l.upper.rotation = Quaternion.AngleAxis(ac_at0 * Mathf.Rad2Deg, axis1) * Quaternion.AngleAxis((ac_ab1 - ac_ab0) * Mathf.Rad2Deg, axis0) * l.upper.rotation;
        }

        static float Angle(Vector3 x, Vector3 y) { return Mathf.Acos(Mathf.Clamp(Vector3.Dot(x.normalized, y.normalized), -1f, 1f)); }

        /// <summary>The ground under a point: the first big thing (roofs count; lamp posts and benches don't).</summary>
        Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, 70f, p.z), Vector3.down, 90f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float y = 0f;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                if (h.collider.bounds.size.magnitude < 7f) continue;
                if (h.point.y > y) y = h.point.y;
            }
            return new Vector3(p.x, y, p.z);
        }

        // ------------------------------------------------------------------ feel: footfalls, roar, shake, eyes
        static AudioClip boom;
        ParticleSystem dust;
        float shake;

        void Footfall(Vector3 at, float strength)
        {
            PlayAt(Boom(), at, Mathf.Clamp01(0.35f + strength * 0.2f), Random.Range(0.85f, 1.05f), 0.035f * S, 520f);
            // the user's steps: from far off a colossal boom, close by the heavy, near footfall
            var me = PlayerParty.Active;
            float d = me != null ? Flat(me.position - at) : 999f;
            var step = d > 70f ? UserSfx.Pick("giant_step_far", 2) : UserSfx.Pick("giant_step_near", 4);
            if (step != null) PlayAt(step, at, d > 70f ? 1f : 0.95f, Random.Range(0.92f, 1.04f), 0.05f * S, 600f);
            var bank = SfxBank.Instance;
            if (bank != null && bank.fallImpact != null) PlayAt(bank.fallImpact, at, 0.6f * strength, Random.Range(0.32f, 0.4f), 0.02f * S, 260f);
            AddShake(at, strength);
            Dust(at, strength);
            // underfoot is no place to be
            var party = PlayerParty.Instance;
            if (Authority && party != null)
                foreach (var b in party.players)
                    if (b != null && !b.IsDead && Flat(b.transform.position - at) < 0.25f * S && Mathf.Abs(b.transform.position.y - at.y) < 4f && !Indoors(b.transform.position))
                        party.KillPlayer(b, "was trampled by the giant");
        }

        void Roar()
        {
            var bank = SfxBank.Instance;
            var clip = bank != null && bank.monsterRoar != null ? bank.monsterRoar : null;
            if (clip != null)
            {
                PlayAt(clip, head.position, 1f, 0.42f, 0.06f * S, 700f);
                PlayAt(clip, head.position, 0.8f, 0.3f, 0.06f * S, 700f);   // an octave of sub-bass under it
            }
            var growl = UserSfx.Get("monster_growl");
            if (growl != null) PlayAt(growl, head.position, 1f, 0.8f, 0.06f * S, 700f);
            AddShake(head.position, 1.3f);
        }

        void AddShake(Vector3 at, float strength)
        {
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera.transform : null;
            if (cam == null) return;
            float d = Vector3.Distance(cam.position, at);
            float k = Mathf.Clamp01(1f - d / (11f * S));
            shake = Mathf.Max(shake, strength * k * k);
        }

        void Shake(float dt)
        {
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.3f);
            if (shake <= 0.001f) return;
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera.transform : null;
            if (cam == null) return;
            float t = Time.time * 23f, a = shake * shake;
            cam.localRotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 3.2f * a, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2.4f * a, (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 4f * a);
        }

        void Dust(Vector3 at, float strength)
        {
            if (dustMaterial == null) return;
            if (dust == null) dust = MakeDust();
            int n = Mathf.RoundToInt(10 * strength + 6);
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized;
                var ep = new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(r.x, 0.2f, r.y) * Random.Range(0.02f, 0.08f) * S,
                    velocity = new Vector3(r.x, Random.Range(0.05f, 0.25f), r.y) * Random.Range(3f, 9f) * strength,
                    startSize = Random.Range(0.25f, 0.55f) * S * 0.6f,
                    startLifetime = Random.Range(2.5f, 4.5f),
                    startColor = new Color(0.32f, 0.34f, 0.38f, Random.Range(0.25f, 0.45f)),
                    rotation = Random.Range(0f, 360f),
                };
                dust.Emit(ep, 1);
            }
        }

        ParticleSystem MakeDust()
        {
            var go = new GameObject("Giant_Dust");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600; main.gravityModifier = -0.01f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = 0.08f; limit.limit = 0.5f;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = dustMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        MaterialPropertyBlock block;

        void Glow(float tm)
        {
            bool angry = Current == Mode.Chase || Current == Mode.Strike || Current == Mode.Roar;
            float pulse = angry ? 0.75f + 0.25f * Mathf.Sin(Time.time * 9f) : 0.85f + 0.15f * Mathf.Sin(Time.time * 1.3f);
            if (eyes != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                foreach (var r in eyes)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(Gfx.EmissiveId, Gfx.ToNits(new Color(1f, 0.08f, 0.04f) * (angry ? 16f : 7f) * pulse));
                    r.SetPropertyBlock(block);
                }
            }
            if (eyeLight != null) eyeLight.intensity = (Paused ? 0f : angry ? 3200f : 1400f) * pulse;   // red on its own face
            if (faceLight != null) faceLight.intensity = angry ? 2600f : 1800f;
            if (gaze != null)
            {
                gaze.transform.position = head.position + Vector3.up * 0.05f * S;
                gaze.transform.rotation = Quaternion.LookRotation(GazeDirection);
                gaze.intensity = (Paused ? 0f : angry ? 4200f : 1700f) * pulse;   // 30-60 m away: inverse-square needs a lot
                gaze.spotAngle = Mathf.MoveTowards(gaze.spotAngle, angry ? 22f : 30f, Time.deltaTime * 10f);
                gaze.color = angry ? new Color(1f, 0.18f, 0.12f) : new Color(1f, 0.42f, 0.36f);
            }
        }

        static void PlayAt(AudioClip clip, Vector3 pos, float volume, float pitch, float minDist, float maxDist)
        {
            if (clip == null) return;
            var go = new GameObject("GiantSound");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = volume; s.pitch = pitch; s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = minDist; s.maxDistance = maxDist; s.dopplerLevel = 0f;
            s.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        /// <summary>A deep, rolling thump: sub-bass sine and low rumble, made once.</summary>
        static AudioClip Boom()
        {
            if (boom != null) return boom;
            const int rate = 44100;
            int n = (int)(rate * 2.4f);
            var s = new float[n];
            var rnd = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.02f;   // low-passed noise: the rumble
                float thump = Mathf.Sin(2f * Mathf.PI * (34f + 30f * Mathf.Exp(-t * 9f)) * t) * Mathf.Exp(-t * 2.4f);
                float knock = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 8f);
                s[i] = Mathf.Clamp(thump * 0.85f + knock * 0.35f + lp * 6f * Mathf.Exp(-t * 1.6f), -1f, 1f) * Mathf.Clamp01(t * 400f);
            }
            boom = AudioClip.Create("GiantFootfall", n, 1, rate, false);
            boom.SetData(s, 0);
            return boom;
        }

        // ------------------------------------------------------------------ the exposure bar
        GUIStyle barLabel, warn;

        void OnGUI()
        {
            UiStyle.Use();
            var party = PlayerParty.Instance;
            if (party == null || party.Current == null || party.Current.IsDead || Paused) return;
            int me = party.CurrentIndex;
            if (me < 0 || me > 3) return;
            if (barLabel == null)
            {
                barLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                warn = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            float e = Exposure[me];
            bool hunted = (Current == Mode.Chase || Current == Mode.Roar || Current == Mode.Strike) && Target == me;
            bool indoors = Indoors(party.Current.transform.position);
            if (e < 0.01f && !hunted)
            {
                if (!indoors) { barLabel.normal.textColor = new Color(1f, 1f, 1f, 0.35f); GUI.Label(new Rect((Screen.width - 300) * 0.5f, Screen.height - 128, 300, 18), "outdoors - keep out of the giant's sight", barLabel); }
                return;
            }
            float w = 360f, h = 14f, x = (Screen.width - w) * 0.5f, y = Screen.height - 120f;
            var white = Texture2D.whiteTexture;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), white);
            float flash = e > 0.75f || hunted ? 0.6f + 0.4f * Mathf.Sin(Time.time * 12f) : 1f;
            GUI.color = Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.1f, 0.05f), hunted ? 1f : e) * flash;
            GUI.DrawTexture(new Rect(x, y, w * (hunted ? 1f : e), h), white);
            GUI.color = Color.white;
            barLabel.normal.textColor = Color.white;
            string state = hunted ? "IT HAS SEEN YOU" : indoors ? "HIDDEN" : Watching == me ? "IT IS STARING AT YOU - GET OUT OF SIGHT" : e > 0.75f ? "EXPOSED - GET OUT OF SIGHT" : "SEEN";
            GUI.Label(new Rect(x, y - 20, w, 18), state, barLabel);
            if (hunted)
            {
                warn.normal.textColor = new Color(1f, 0.15f, 0.1f, flash);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 30), indoors ? "STAY INSIDE - IT IS SEARCHING" : "RUN - GET INDOORS", warn);
            }
        }
    }
}
