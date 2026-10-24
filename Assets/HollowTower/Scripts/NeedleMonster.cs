using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// Entity 38, the Needlelimbs (the collage game tutorial's monster, its model and four clips: idle, walk, chase,
    /// attack). It walks the NavMesh baked for its floor:
    ///   Patrol      wanders between points in its own part of the floor, pausing now and then.
    ///   Investigate goes to a noise (sprinting, landing, a door) and looks around.
    ///   Alert       on first sight it rears up and screeches (procedural: spine and head, there is no clip for it), then
    ///   Chase       runs the player down: faster than a walk, slower than a sprint.
    ///   Attack      a lunge that kills whoever is still in reach when it lands.
    ///   Search      lost them: goes to where they were last seen and sweeps its head about before giving up.
    /// Sight needs line of sight inside its field of view (much shorter against a crouching player); anything right
    /// beside it is noticed regardless. Ordinary doors in its way are bashed open; jammed ones are walls to it.
    /// An invisible one (invisibleUntilAggro) can only be seen by P3 until it has noticed someone; while it hunts
    /// everyone can see it.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NeedleMonster : MonoBehaviour
    {
        public enum State { Patrol, Investigate, Alert, Chase, Attack, Search, Feed }
        public static readonly List<NeedleMonster> All = new List<NeedleMonster>();

        [Header("Kind")]
        public bool invisibleUntilAggro;
        public int seer = 2;   // P3

        [Header("Territory")]
        public Vector3[] patrolPoints;

        [Header("Senses")]
        public float sightRange = 13f;
        public float crouchSightRange = 6.5f;
        public float fieldOfView = 150f;
        public float senseRange = 2.6f;
        public float hearing = 1f;
        public float loseAfter = 4f;

        [Header("Moves")]
        public float walkSpeed = 1.7f;
        public float chaseSpeed = 5.3f;
        public float attackRange = 2.2f;

        [Header("Animation states (the Needlelimbs' clips by default; the Human Deer has its own)")]
        public string idleState = "Entity38_Idle";
        public string walkState = "Entity38_Walk";
        public string chaseState = "Entity38_Chase";
        public string attackState = "Entity38_Attack";
        [Tooltip("Played while it notices someone; empty: rear up procedurally instead.")]
        public string alertState = "";
        public float attackPlaySpeed = 1.4f;

        [Header("Parts")]
        public Animator animator;
        public Renderer[] renderers;

        public State Current { get { return state; } }
        static bool bashing;

        NavMeshAgent agent;
        State state = State.Patrol;
        float stateTime, lastSeenTime, pauseUntil, nextRepath, nextDoorCheck, travelled, aggroUntil;
        RemasteredPlayerController target;
        Vector3 lastKnown;
        bool struck, visible = true;
        string playing;
        Transform spine, head;
        // in a session the host thinks for it; a client's copy is a puppet (NetMonster)
        Net.NetMonster net;
        Vector3 lastPos;
        float moveSpeed;
        int puppetAnim;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLower();
                if (head == null && n.Contains("head")) head = t;
                if (n.Contains("spine") && (spine == null || n.Contains("1") || n.Contains("001"))) spine = t;
            }
            state = State.Patrol;
            if (agent.isOnNavMesh) Enter(State.Patrol);
        }

        // ------------------------------------------------------------------ noise
        /// <summary>A sound at pos that carries radius metres; monsters in range that aren't already hunting come to look.</summary>
        public static void Hear(Vector3 pos, float radius)
        {
            if (bashing || !Net.Net.IsAuthority) return;   // a door a monster is smashing through isn't news to it; monsters think on the host
            foreach (var m in All)
            {
                if (m == null || m.state == State.Chase || m.state == State.Attack || m.state == State.Alert || m.state == State.Feed) continue;
                if (Mathf.Abs(m.transform.position.y - pos.y) > 3f) continue;
                if (Vector3.Distance(m.transform.position, pos) <= radius * m.hearing)
                {
                    m.lastKnown = pos;
                    m.Enter(State.Investigate);
                }
            }
        }

        // ------------------------------------------------------------------ brain
        void Update()
        {
            moveSpeed = Mathf.Lerp(moveSpeed, Vector3.Distance(transform.position, lastPos) / Mathf.Max(Time.deltaTime, 0.001f), Time.deltaTime * 10f);
            lastPos = transform.position;
            if (net == null) net = GetComponent<Net.NetMonster>();
            if (net != null && net.Puppet) { Puppet(); return; }
            var party = PlayerParty.Instance;
            if (party == null || agent == null) return;
            if (!agent.isOnNavMesh)
            {
                // the floor's NavMesh may load after this monster: step onto it once it is there
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas)) { agent.Warp(hit.position); Enter(state); }
                return;
            }
            stateTime += Time.deltaTime;
            var seen = LookForPlayer();

            switch (state)
            {
                case State.Patrol:
                case State.Investigate:
                case State.Search:
                    if (seen != null) { target = seen; Enter(State.Alert); break; }
                    Wander();
                    break;
                case State.Alert:
                    Face(target != null ? target.transform.position : lastKnown);
                    if (stateTime > 1.1f) Enter(State.Chase);
                    break;
                case State.Chase:
                    if (target == null || target.IsDead) { target = seen; if (target == null) { Enter(State.Search); break; } }
                    if (seen == target) { lastSeenTime = Time.time; lastKnown = target.transform.position; }
                    else if (seen != null && Dist(seen) < Dist(target) - 2f) target = seen;
                    if (Time.time - lastSeenTime > loseAfter) { Enter(State.Search); break; }
                    if (Time.time > nextRepath) { nextRepath = Time.time + 0.2f; agent.SetDestination(Time.time - lastSeenTime < 0.5f ? target.transform.position : lastKnown); }
                    if (Dist(target) <= attackRange && Time.time - lastSeenTime < 0.5f) Enter(State.Attack);
                    break;
                case State.Attack:
                    if (target != null) Face(target.transform.position);
                    if (!struck && stateTime > 0.45f)
                    {
                        struck = true;
                        if (target != null && !target.IsDead && !target.IsFrozen && Dist(target) <= attackRange + 0.7f) party.KillPlayer(target, "was taken by the Needlelimbs");
                    }
                    if (stateTime > 1.25f)
                    {
                        if (target != null && target.IsDead) Enter(State.Feed);
                        else Enter(State.Chase);
                    }
                    break;
                case State.Feed:
                    if (stateTime > 3.5f) { target = null; Enter(State.Patrol); }
                    break;
            }

            UpdateVisibility();
            UpdateAnimation();
            Footsteps();
            BashDoors();
            if (net != null)
                net.Push((int)state, Animator.StringToHash(playing ?? idleState), animator != null ? animator.speed : 1f,
                         state == State.Alert || state == State.Chase || state == State.Attack || Time.time < aggroUntil);
        }

        // a client's copy: the host's state (with its sounds), animation and whether it has shown itself
        void Puppet()
        {
            if (agent != null && agent.enabled) agent.enabled = false;
            var s = (State)net.State;
            if (s != state)
            {
                state = s; stateTime = 0f;
                var bank = SfxBank.Instance;
                if (bank != null)
                {
                    if (s == State.Alert) SfxBank.At(bank.monsterScreech, transform.position + Vector3.up * 2f, 1f, Random.Range(0.92f, 1.05f), 40f);
                    else if (s == State.Chase) SfxBank.At(bank.monsterRoar, transform.position + Vector3.up * 2f, 0.85f, 1f, 35f);
                    else if (s == State.Attack) SfxBank.At(bank.monsterAttack, transform.position + Vector3.up * 1.5f, 1f, 1f, 20f);
                }
            }
            else stateTime += Time.deltaTime;
            bool show = !invisibleUntilAggro || net.Shown || PlayerParty.BodyHas(PlayerParty.ActiveIndex, seer);
            if (show != visible) { visible = show; foreach (var r in renderers) if (r != null) r.enabled = show; }
            if (animator != null)
            {
                if (net.Anim != puppetAnim) { puppetAnim = net.Anim; animator.CrossFade(net.Anim, 0.2f); }
                animator.speed = net.AnimSpeed;
            }
            Footsteps();
        }

        void Wander()
        {
            if (agent.pathPending) return;
            bool arrived = agent.remainingDistance <= 0.6f;
            if (!arrived) return;
            if (state == State.Investigate || state == State.Search)
            {
                // stand and look about for a while, then give up
                if (pauseUntil == 0f) pauseUntil = Time.time + (state == State.Search ? 4.5f : 3f);
                if (Time.time > pauseUntil) Enter(State.Patrol);
                return;
            }
            if (pauseUntil == 0f) pauseUntil = Time.time + Random.Range(1.2f, 4f);
            if (Time.time < pauseUntil) return;
            pauseUntil = 0f;
            if (patrolPoints != null && patrolPoints.Length > 0) agent.SetDestination(patrolPoints[Random.Range(0, patrolPoints.Length)]);
        }

        void Enter(State s)
        {
            state = s;
            stateTime = 0f;
            pauseUntil = 0f;
            struck = false;
            var bank = SfxBank.Instance;
            switch (s)
            {
                case State.Patrol:
                    agent.isStopped = false; agent.speed = walkSpeed;
                    if (patrolPoints != null && patrolPoints.Length > 0) agent.SetDestination(patrolPoints[Random.Range(0, patrolPoints.Length)]);
                    break;
                case State.Investigate:
                case State.Search:
                    agent.isStopped = false; agent.speed = walkSpeed * (s == State.Search ? 1.6f : 1.3f);
                    agent.SetDestination(lastKnown);
                    break;
                case State.Alert:
                    agent.isStopped = true; agent.ResetPath();
                    aggroUntil = Time.time + 6f;
                    if (bank != null) SfxBank.At(bank.monsterScreech, transform.position + Vector3.up * 2f, 1f, Random.Range(0.92f, 1.05f), 40f);
                    if (target != null) lastKnown = target.transform.position;
                    lastSeenTime = Time.time;
                    break;
                case State.Chase:
                    agent.isStopped = false; agent.speed = chaseSpeed;
                    if (bank != null) SfxBank.At(bank.monsterRoar, transform.position + Vector3.up * 2f, 0.85f, 1f, 35f);
                    break;
                case State.Attack:
                    agent.isStopped = true; agent.ResetPath();
                    if (bank != null) SfxBank.At(bank.monsterAttack, transform.position + Vector3.up * 1.5f, 1f, 1f, 20f);
                    break;
                case State.Feed:
                    agent.isStopped = true; agent.ResetPath();
                    break;
            }
        }

        RemasteredPlayerController LookForPlayer()
        {
            var party = PlayerParty.Instance;
            RemasteredPlayerController best = null;
            float bestD = float.MaxValue;
            Vector3 eye = transform.position + Vector3.up * 1.7f;
            foreach (var p in party.players)
            {
                if (p == null || p.IsDead || p.IsFrozen) continue;
                Vector3 chest = p.transform.position + Vector3.up * 1.2f;
                float d = Vector3.Distance(eye, chest);
                var cc = p.GetComponent<CharacterController>();
                bool crouch = cc != null && cc.height < 1.6f;
                float range = crouch ? crouchSightRange : sightRange;
                if (d > range) continue;
                if (d > senseRange && Vector3.Angle(transform.forward, chest - eye) > fieldOfView * 0.5f) continue;
                if (Blocked(eye, chest, p)) continue;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        static bool Blocked(Vector3 from, Vector3 to, RemasteredPlayerController p)
        {
            foreach (var h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            {
                var pc = h.collider.GetComponentInParent<RemasteredPlayerController>();
                if (pc != null) continue;   // teammates don't hide each other
                if (h.collider.transform.IsChildOf(p.transform)) continue;
                return true;
            }
            return false;
        }

        float Dist(RemasteredPlayerController p) { return Vector3.Distance(transform.position, p.transform.position); }

        void Face(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }

        // ------------------------------------------------------------------ body
        void UpdateVisibility()
        {
            bool hunting = state == State.Alert || state == State.Chase || state == State.Attack || Time.time < aggroUntil;
            if (state == State.Chase || state == State.Attack) aggroUntil = Time.time + 5f;
            bool show = !invisibleUntilAggro || hunting || PlayerParty.BodyHas(PlayerParty.ActiveIndex, seer);
            if (show == visible) return;
            visible = show;
            foreach (var r in renderers) if (r != null) r.enabled = show;
        }

        void UpdateAnimation()
        {
            if (animator == null) return;
            float v = agent.velocity.magnitude;
            string want;
            switch (state)
            {
                case State.Attack: want = attackState; break;
                case State.Chase: want = v > 0.5f ? chaseState : idleState; break;
                case State.Alert: want = string.IsNullOrEmpty(alertState) ? idleState : alertState; break;
                case State.Feed: want = idleState; break;
                default: want = v > 0.25f ? walkState : idleState; break;
            }
            if (want != playing)
            {
                animator.CrossFade(want, 0.2f);
                playing = want;
            }
            animator.speed = want == walkState ? Mathf.Clamp(v / walkSpeed, 0.6f, 1.6f)
                           : want == chaseState ? Mathf.Clamp(v / chaseSpeed * 1.2f, 0.8f, 1.5f)
                           : want == attackState ? attackPlaySpeed : 1f;
        }

        // The clips have no "notice" or "look around": rear up and shake the head on alert, sweep it while searching.
        void LateUpdate()
        {
            if (state == State.Alert && string.IsNullOrEmpty(alertState))
            {
                float k = Mathf.Sin(Mathf.Clamp01(stateTime / 1.1f) * Mathf.PI);
                if (spine != null) spine.localRotation *= Quaternion.Euler(-28f * k, 0f, 0f);
                if (head != null) head.localRotation *= Quaternion.Euler(-15f * k, Mathf.Sin(stateTime * 38f) * 14f * k, Mathf.Sin(stateTime * 23f) * 8f * k);
            }
            else if ((state == State.Search || state == State.Investigate) && pauseUntil > 0f && head != null)
            {
                head.localRotation *= Quaternion.Euler(0f, Mathf.Sin(stateTime * 1.6f) * 50f, 0f);
            }
            else if (state == State.Feed && head != null)
            {
                head.localRotation *= Quaternion.Euler(25f + Mathf.Sin(stateTime * 9f) * 6f, 0f, 0f);
            }
        }

        void Footsteps()
        {
            var bank = SfxBank.Instance;
            if (bank == null) return;
            float v = moveSpeed;
            travelled += v * Time.deltaTime;
            float stride = state == State.Chase ? 1.3f : 0.95f;
            if (travelled < stride) return;
            travelled = 0f;
            // an unseen one is still heard: that is how the others learn it is there
            SfxBank.At(SfxBank.Pick(bank.monsterSteps), transform.position, state == State.Chase ? 0.9f : 0.5f, Random.Range(0.85f, 1.15f), state == State.Chase ? 26f : 16f);
        }

        void BashDoors()
        {
            if (Time.time < nextDoorCheck || agent.velocity.sqrMagnitude < 0.2f) return;
            nextDoorCheck = Time.time + 0.25f;
            Vector3 ahead = transform.position + transform.forward * 1.4f + Vector3.up * 1.2f;
            foreach (var c in Physics.OverlapSphere(ahead, 1.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var door = c.GetComponentInParent<InteractiveDoor>();
                if (door != null && !door.isOpen && !door.isLocked) { bashing = true; door.SetOpen(true); bashing = false; }
            }
        }
    }
}
