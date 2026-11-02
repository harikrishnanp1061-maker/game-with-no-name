using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower.Campus
{
    /// <summary>
    /// The Nailed: a gaunt, flayed figure with planks and nails driven through it, smaller than the giant and far
    /// quicker - one stalks the administration building's corridors, others the campus grounds. It does not see well in
    /// the dark, but it hears everything:
    ///   Patrol       walks its round, stopping now and then to listen, its head twitching.
    ///   Investigate  a phone ringing, a car alarm, lights thrown on, someone sprinting or a backfire: it stalks over
    ///                and searches there for a while. This is what the control room uses to pull it off the others.
    ///   Alert        sees someone: freezes, screams, then
    ///   Chase        runs them down (a little slower than a sprint - but sprinting runs out).
    ///   Attack       a lunging grab that kills whoever is still in reach.
    ///   Search       lost them: goes to where it last saw them and hunts round before going back to its round.
    /// Thinks on the host; a client's copy follows NetMonster (state, clip, speed) like the tower's monsters.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NailedMonster : MonoBehaviour
    {
        public enum State { Patrol = 0, Investigate = 1, Alert = 2, Chase = 3, Attack = 4, Search = 5, Feed = 6 }

        public static readonly List<NailedMonster> All = new List<NailedMonster>();

        [Header("Round")]
        public Vector3[] route;
        [Tooltip("Inside the administration building (a lure only reaches it from inside) or out on the grounds.")]
        public bool indoors;

        [Header("Senses")]
        public float sightRange = 20f;
        public float sightHalfAngle = 62f;
        [Tooltip("Scales how far it hears things.")]
        public float hearing = 1f;

        [Header("Moving")]
        public float walkSpeed = 1.45f, stalkSpeed = 2.6f, runSpeed = 6.2f;
        public float attackRange = 1.35f;
        [Tooltip("Ground speed (m/s) of the walk and run clips at animator speed 1 (from the Blender build).")]
        public float walkClipSpeed = 1.1f, runClipSpeed = 3.6f;

        [Header("Parts (set by the builder)")]
        public Animator animator;
        public Renderer[] renderers;

        public State Current { get { return state; } }
        public RemasteredPlayerController Target { get { return target; } }

        NavMeshAgent agent;
        Net.NetMonster net;
        State state = State.Patrol;
        float stateTime, nextRepath, pauseUntil, nextTwitch, lostFor, travelled, nextGrowl, moveSpeed, phase;
        int waypoint, puppetAnim;
        string playing;
        Vector3 interest, lastSeen, lastPos;
        RemasteredPlayerController target;
        Transform head, neck;
        Quaternion twitch = Quaternion.identity, twitchGoal = Quaternion.identity;
        float headYaw;
        MaterialPropertyBlock block;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        static readonly int IdleHash = Animator.StringToHash("Idle"), WalkHash = Animator.StringToHash("Walk"), RunHash = Animator.StringToHash("Run"),
                            AttackHash = Animator.StringToHash("Attack"), ScreamHash = Animator.StringToHash("Scream");

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            net = GetComponent<Net.NetMonster>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            if (animator != null) { animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; animator.applyRootMotion = false; }
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "head") head = t;
                else if (t.name == "neck_01") neck = t;
            }
            phase = Random.value * 10f;
            lastPos = transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
            // start at the waypoint nearest to where it stands
            if (route != null && route.Length > 0)
            {
                float best = float.MaxValue;
                for (int i = 0; i < route.Length; i++) { float d = (route[i] - transform.position).sqrMagnitude; if (d < best) { best = d; waypoint = i; } }
            }
            Enter(State.Patrol);
            if (animator != null) animator.Update(Random.value);
        }

        bool Authority { get { return net == null || !net.IsSpawned || net.IsServer; } }

        // ------------------------------------------------------------------ brain (host)
        void Update()
        {
            float dt = Time.deltaTime;
            moveSpeed = Mathf.Lerp(moveSpeed, Vector3.Distance(transform.position, lastPos) / Mathf.Max(dt, 0.001f), dt * 8f);
            lastPos = transform.position;
            Glow();
            Footsteps();
            if (!Authority) { Puppet(); return; }
            if (!agent.isOnNavMesh) { NavMeshHit h; if (NavMesh.SamplePosition(transform.position, out h, 4f, NavMesh.AllAreas)) agent.Warp(h.position); else return; }
            stateTime += dt;
            var party = PlayerParty.Instance;

            // eyes open all the time (except mid-grab): anyone it can see gets chased
            if (state != State.Attack && state != State.Alert && state != State.Feed && party != null)
            {
                var seen = Look(party);
                // just gave up a chase indoors: it only lunges again at someone right in front of it
                if (seen != null && Time.time < calmUntil && state != State.Chase && Dist(seen) > 5f) seen = null;
                if (seen != null && state != State.Chase && SafeInControlRoom(seen)) seen = null;
                if (seen != null)
                {
                    lastSeen = seen.transform.position; lostFor = 0f;
                    if (state != State.Chase) { target = seen; Enter(State.Alert); }
                    else if (seen != target && Dist(seen) < Dist(target) - 3f) target = seen;
                }
            }

            switch (state)
            {
                case State.Patrol:
                    if (route == null || route.Length == 0) break;
                    if (Time.time < pauseUntil) { Halt(); break; }
                    agent.speed = walkSpeed;
                    Go(route[waypoint]);
                    if (Arrived(1.2f))
                    {
                        waypoint = (waypoint + 1) % route.Length;
                        if (Random.value < 0.45f) pauseUntil = Time.time + Random.Range(2.5f, 6f);   // stops to listen
                    }
                    Growl(0.35f);
                    break;
                case State.Investigate:
                case State.Search:
                    agent.speed = state == State.Search ? stalkSpeed * 1.2f : stalkSpeed;
                    Go(interest);
                    if (Arrived(1.6f) || stateTime > 25f)
                    {
                        Halt();
                        // looks round the spot, sniffing, then picks a nearby corner to check
                        if (stateTime > 9f + (state == State.Search ? 6f : 0f)) { Enter(State.Patrol); break; }
                        if (Time.time > nextRepath + 2.5f)
                        {
                            NavMeshHit h;
                            Vector3 r = interest + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                            if (NavMesh.SamplePosition(r, out h, 3f, NavMesh.AllAreas)) { interest = h.position; nextRepath = Time.time; }
                        }
                    }
                    Growl(0.6f);
                    break;
                case State.Alert:
                    Halt();
                    if (target != null) Face(target.transform.position, 540f);
                    if (stateTime > 1.15f) Enter(target != null && !target.IsDead ? State.Chase : State.Search);
                    break;
                case State.Chase:
                    if (target == null || target.IsDead) { Enter(State.Search); break; }
                    bool sees = CanSee(target, true);
                    if (sees) { lastSeen = target.transform.position; lostFor = 0f; } else lostFor += dt;
                    if (lostFor > (indoors ? 2.5f : 5f)) { interest = lastSeen; Enter(State.Search); break; }
                    if (indoors && (stateTime > 14f || Vector3.Distance(transform.position, chaseFrom) > 26f || SafeInControlRoom(target)))
                    {
                        // inside it gives up a long chase (and never follows into the locked control room): the
                        // corridors are short and the team has to be able to reach the security room
                        calmUntil = Time.time + 8f; interest = SafeInControlRoom(target) ? chaseFrom : lastSeen; Enter(State.Search); break;
                    }
                    agent.speed = indoors ? runSpeed * 0.85f : runSpeed;
                    if (Time.time > nextRepath && !agent.pathPending) { nextRepath = Time.time + 0.2f; Go(sees ? target.transform.position : lastSeen, true); }
                    if (sees && Dist(target) <= attackRange) Enter(State.Attack);
                    BashDoors();
                    Growl(1.4f);
                    break;
                case State.Attack:
                    Halt();
                    if (target != null) Face(target.transform.position, 720f);
                    if (stateTime > 0.5f && target != null && party != null)
                    {
                        if (!target.IsDead && Dist(target) <= attackRange + 0.7f && CanSee(target, true))
                        {
                            party.KillPlayer(target, "was torn apart by the Nailed");
                            SfxBank.At(UserSfx.Get("monster_shout") ?? (SfxBank.Instance != null ? SfxBank.Instance.monsterAttack : null), transform.position + Vector3.up, 1f, 1f, 35f);
                            Enter(State.Feed);
                            break;
                        }
                    }
                    if (stateTime > 1.2f) Enter(target != null && !target.IsDead ? State.Chase : State.Search);
                    break;
                case State.Feed:
                    Halt();
                    if (stateTime > 3.5f) { target = null; Enter(State.Patrol); }
                    break;
            }

            Animate();
            if (net != null) net.Push((int)state, Animator.StringToHash(playing ?? "Idle"), animator != null ? animator.speed : 1f, true);
        }

        float calmUntil;
        Vector3 chaseFrom;

        /// <summary>The security control room is a refuge: its door locks behind the keycard, and the Nailed won't go in.</summary>
        static bool SafeInControlRoom(RemasteredPlayerController p)
        {
            var c = SurveillanceConsole.Instance;
            return c != null && p != null && c.room.Contains(p.transform.position);
        }

        void Enter(State s)
        {
            if (s == State.Chase && state != State.Chase) chaseFrom = transform.position;
            state = s; stateTime = 0f;
            switch (s)
            {
                case State.Alert:
                    Halt();
                    Play("Scream", 0.12f);
                    var bank = SfxBank.Instance;
                    if (bank != null) SfxBank.At(bank.monsterScreech, transform.position + Vector3.up * 1.6f, 1f, Random.Range(0.75f, 0.9f), 45f);
                    break;
                case State.Attack:
                    Play("Attack", 0.08f);
                    break;
                case State.Patrol:
                    target = null;
                    if (route != null && route.Length > 0)
                    {
                        // back to the nearest point of its round
                        float best = float.MaxValue;
                        for (int i = 0; i < route.Length; i++) { float d = (route[i] - transform.position).sqrMagnitude; if (d < best) { best = d; waypoint = i; } }
                    }
                    break;
            }
        }

        /// <summary>Heard (kind 1) or saw (kind 2: lights coming on) something at pos, within radius of it (host).</summary>
        public static void HearAt(Vector3 pos, float radius, int kind = 1)
        {
            if (!Net.Net.IsAuthority) return;
            foreach (var m in All)
            {
                if (m == null || !m.isActiveAndEnabled) continue;
                if (m.state == State.Chase || m.state == State.Attack || m.state == State.Alert || m.state == State.Feed) continue;
                float d = Vector3.Distance(m.transform.position, pos);
                // walls and floors muffle: a phone upstairs reaches less far than one down the corridor
                float reach = radius * m.hearing * (Mathf.Abs(m.transform.position.y - pos.y) > 3f ? 0.6f : 1f);
                if (kind == 1 && CampusGiant.Indoors(pos) != CampusGiant.Indoors(m.transform.position)) reach *= 0.5f;
                if (d > reach) continue;
                Vector3 dest;
                if (!m.ReachableNear(pos, out dest)) continue;
                m.interest = dest;
                m.Enter(State.Investigate);
                m.pauseUntil = 0f;
            }
        }

        static NavMeshPath probe;
        /// <summary>The nearest point to a noise it can actually walk to: not the top of the desk the phone sits on, not
        /// a roof - it tries the floor round the spot until a path gets there.</summary>
        bool ReachableNear(Vector3 pos, out Vector3 dest)
        {
            dest = pos;
            if (probe == null) probe = new NavMeshPath();
            Vector3 floor = pos; RaycastHit rh;
            if (Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out rh, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) floor = rh.point;
            for (int ring = 0; ring < 3; ring++)
                for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                {
                    float a = k * Mathf.PI / 4f, r = ring * 1.3f;
                    Vector3 c = floor + new Vector3(Mathf.Cos(a) * r, -0.6f, Mathf.Sin(a) * r);
                    NavMeshHit h;
                    if (!NavMesh.SamplePosition(c, out h, 2f, NavMesh.AllAreas)) continue;
                    if (h.position.y > floor.y + 0.5f) continue;
                    if (!agent.isOnNavMesh || !NavMesh.CalculatePath(agent.nextPosition, h.position, NavMesh.AllAreas, probe) || probe.status != NavMeshPathStatus.PathComplete) continue;
                    dest = h.position;
                    return true;
                }
            return false;
        }

        /// <summary>A failed lock's alarm: the nearest Nailed comes straight for that player for a while (host).</summary>
        public static bool Summon(RemasteredPlayerController body, float seconds)
        {
            if (!Net.Net.IsAuthority || body == null || DevTools.MonstersPassive) return false;
            NailedMonster best = null; float bd = 90f;
            foreach (var m in All)
            {
                if (m == null || m.state == State.Chase || m.state == State.Attack) continue;
                float d = Vector3.Distance(m.transform.position, body.transform.position);
                if (d < bd) { bd = d; best = m; }
            }
            if (best == null) return false;
            best.target = body; best.lastSeen = body.transform.position; best.lostFor = -seconds + 5f;
            best.Enter(State.Chase);
            return true;
        }

        // ------------------------------------------------------------------ senses
        RemasteredPlayerController Look(PlayerParty party)
        {
            if (DevTools.MonstersPassive) { if (state == State.Chase) Enter(State.Patrol); return null; }
            RemasteredPlayerController best = null; float bd = float.MaxValue;
            for (int i = 0; i < party.players.Length; i++)
            {
                var p = party.players[i];
                if (p == null || p.IsDead || PlayerParty.SitsOut(i)) continue;
                if (!CanSee(p, false)) continue;
                float d = Dist(p);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        bool CanSee(RemasteredPlayerController p, bool chasing)
        {
            Vector3 eye = head != null ? head.position : transform.position + Vector3.up * 1.7f;
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            Vector3 to = chest - eye;
            float d = to.magnitude;
            // it sees badly in the dark: a lit torch or a lit room gives you away from further off; crouching helps
            float range = sightRange * (chasing ? 1.4f : 1f);
            if (p.flashlightLight != null && p.flashlightLight.enabled) range *= 1.35f;
            if (p.IsCrouching && !chasing) range *= 0.55f;
            if (d > range) return false;
            if (!chasing && d > 2.2f && Vector3.Angle(transform.forward, to) > sightHalfAngle) return false;
            if (Mathf.Abs(to.y) > 3.2f && d < 12f && !chasing) return false;   // another floor
            foreach (var h in Physics.RaycastAll(eye, to / d, d, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        float Dist(RemasteredPlayerController p) { return p == null ? float.MaxValue : Vector3.Distance(transform.position, p.transform.position); }

        // ------------------------------------------------------------------ moving
        void Go(Vector3 dest, bool force = false)
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            if (!force && agent.hasPath && (agent.destination - dest).sqrMagnitude < 0.5f) return;
            NavMeshHit h;
            if (NavMesh.SamplePosition(dest, out h, 3f, NavMesh.AllAreas)) agent.SetDestination(h.position);
        }

        bool Arrived(float within) { return agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < within; }

        void Halt() { if (agent.isOnNavMesh && !agent.isStopped) { agent.isStopped = true; agent.ResetPath(); } }

        void Face(Vector3 p, float speed)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), speed * Time.deltaTime);
        }

        float nextDoor;
        void BashDoors()
        {
            if (Time.time < nextDoor) return;
            nextDoor = Time.time + 0.3f;
            Vector3 ahead = transform.position + transform.forward * 0.9f + Vector3.up * 1f;
            foreach (var c in Physics.OverlapSphere(ahead, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var door = c.GetComponentInParent<InteractiveDoor>();
                if (door != null && !door.isOpen && !door.isLocked) door.SetOpen(true);
            }
        }

        // ------------------------------------------------------------------ looks and sounds
        void Animate()
        {
            if (animator == null) return;
            if (state == State.Alert || state == State.Attack) return;   // a one-shot is playing
            float v = agent.velocity.magnitude;
            if (v > 3f) { Play("Run", 0.2f); animator.speed = Mathf.Clamp(v / runClipSpeed, 0.6f, 1.6f); }
            else if (v > 0.25f) { Play("Walk", 0.25f); animator.speed = Mathf.Clamp(v / walkClipSpeed, 0.5f, 2f); }
            else { Play("Idle", 0.3f); animator.speed = 1f; }
        }

        void Play(string clip, float fade)
        {
            if (animator == null || playing == clip) return;
            playing = clip;
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(clip, fade);
        }

        void Puppet()
        {
            if (agent.enabled) agent.enabled = false;
            var s = (State)net.State;
            if (s != state) { state = s; stateTime = 0f; } else stateTime += Time.deltaTime;
            if (animator != null)
            {
                if (net.Anim != puppetAnim) { puppetAnim = net.Anim; animator.CrossFadeInFixedTime(net.Anim, 0.15f); }
                animator.speed = net.AnimSpeed;
            }
        }

        void Growl(float rate)
        {
            if (Time.time < nextGrowl) return;
            nextGrowl = Time.time + Random.Range(9f, 20f) / rate;   // never continuous: a random gap between
            // still inside with the party (the main doors not yet open): now and then its drowned moan; otherwise the lurking breath
            var obj = CampusObjective.Instance;
            bool building = indoors && obj != null && obj.mainDoor != null && !obj.mainDoor.Open;
            var clip = building && Random.value < 0.5f ? UserSfx.Get("nailed_underwater") : UserSfx.Get("lurking");
            if (clip != null) SfxBank.At(clip, transform.position + Vector3.up * 1.6f, 0.8f, Random.Range(0.9f, 1.05f), 26f);
            else if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.monsterLurk, transform.position + Vector3.up * 1.6f, 0.75f, Random.Range(0.7f, 0.95f), 22f);
        }

        void Footsteps()
        {
            var bank = SfxBank.Instance;
            if (bank == null || bank.monsterSteps == null || bank.monsterSteps.Length == 0) return;
            travelled += moveSpeed * Time.deltaTime;
            float stride = moveSpeed > 3f ? 1.5f : 0.75f;
            if (travelled < stride) return;
            travelled = 0f;
            // bare clawed feet, and the planks nailed to it knocking together
            SfxBank.At(SfxBank.Pick(bank.monsterSteps), transform.position, moveSpeed > 3f ? 0.8f : 0.45f, Random.Range(0.85f, 1.05f), moveSpeed > 3f ? 26f : 16f);
        }

        /// <summary>The red pustules and eyes smoulder; they flare when it is after someone.</summary>
        void Glow()
        {
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            bool hunting = state == State.Alert || state == State.Chase || state == State.Attack;
            float k = (hunting ? 2.6f : 0.7f) * (0.85f + 0.15f * Mathf.Sin((Time.time + phase) * 6.1f));
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(Emission, new Color(1f, 0.12f, 0.06f) * k);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Head: twitching jerks while it prowls (it hears more than it sees), locked onto its prey when hunting.</summary>
        void LateUpdate()
        {
            if (head == null) return;
            if (Time.time > nextTwitch)
            {
                nextTwitch = Time.time + Random.Range(0.4f, 2.2f);
                twitchGoal = Random.value < 0.4f ? Quaternion.identity : Quaternion.Euler(Random.Range(-14f, 10f), Random.Range(-35f, 35f), Random.Range(-28f, 28f));
            }
            twitch = Quaternion.Slerp(twitch, twitchGoal, Time.deltaTime * 14f);   // snaps rather than turns
            bool hunting = state == State.Alert || state == State.Chase || state == State.Attack;
            if (hunting && target != null)
            {
                Vector3 to = target.transform.position + Vector3.up * 1.3f - head.position;
                Vector3 local = transform.InverseTransformDirection(to);
                float want = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                headYaw = Mathf.LerpAngle(headYaw, want, Time.deltaTime * 10f);
                head.rotation = Quaternion.AngleAxis(headYaw, transform.up) * head.rotation;
            }
            else
            {
                headYaw = Mathf.LerpAngle(headYaw, 0f, Time.deltaTime * 4f);
                head.rotation = transform.rotation * twitch * Quaternion.Inverse(transform.rotation) * head.rotation;
            }
        }
    }
}
