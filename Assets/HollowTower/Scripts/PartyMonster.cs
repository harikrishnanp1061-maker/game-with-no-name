using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// A floor 2 party guest: a small pale imp in a party hat. It follows the floor's music (PartyFloor):
    ///   Play     while the music plays it stays at its spot doing something innocent: holding a balloon, scribbling on
    ///            a board, playing with a toy, sitting on the floor, or swaying to the tune.
    ///   Still    when the music dies it freezes mid-movement for a few seconds; only its head turns to follow you.
    ///   Hunt     then it comes for the players, in one of three ways (role): a Chaser runs down the nearest player it
    ///            knows of, the Swarm all pile onto one player together, an Interceptor heads for where its target is
    ///            going rather than where they are. They sense anyone out in the open on the floor (roughly, refreshed
    ///            every second or two; exactly when in sight) but not someone hidden, unless one of them saw the player
    ///            climb in: then it goes to that hiding spot and drags them out.
    ///   Attack   a swipe that kills whoever is still in reach when it lands.
    ///   Return   when the music comes back they walk home and pick up their game where they left it.
    /// The poses are bone offsets on top of the idle clip (there are no sit or hold clips).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PartyMonster : MonoBehaviour, Net.INetAction
    {
        public enum Role { Chaser, Swarm, Interceptor }
        public enum Activity { Balloon, Drawing, Toy, Sitting, Swaying }
        enum State { Play, Still, Hunt, Attack, Feed, Return }

        public static readonly List<PartyMonster> All = new List<PartyMonster>();

        public Role role;
        public Activity activity;
        public float height = 1.1f;

        [Header("Hunt")]
        public float stillTime = 3.5f;
        public float walkSpeed = 1.6f;
        public float runSpeed = 5.4f;
        public float sightRange = 16f;
        public float senseRadius = 45f;
        public float attackRange = 1.05f;

        [Header("Parts (set by the builder)")]
        public Animator animator;
        public Renderer[] renderers;
        public Transform balloon;          // Balloon: floats above the right hand
        public Transform balloonString;
        public Transform toy;              // Toy: on the floor in front of it

        public bool Hunting { get { return state == State.Hunt || state == State.Attack; } }

        NavMeshAgent agent;
        State state = State.Play;
        float stateTime, nextRepath, nextSense, nextLaugh, nextDoor, travelled, phase;
        Vector3 home;
        Quaternion homeRot;
        RemasteredPlayerController target;
        Vector3 knownPos, knownVel;
        PartyHideSpot witnessed;
        string playing;
        Transform hips, spine, head, armL, armR, foreL, foreR, thighL, thighR, shinL, shinR, handR;
        float headYaw;
        // the chase: swaying side to side and darting, the body leaning into it
        float swayFreq, dartUntil, nextDart, lean;
        Vector3 dartDir;
        Transform model;
        // in a session the host thinks for it; a client's copy is a puppet (NetMonster)
        Net.NetMonster net;
        Vector3 lastPos;
        float moveSpeed;
        int puppetAnim;
        Vector3 lurePos;
        float lureUntil;
        static RemasteredPlayerController swarmTarget;
        static float swarmPick;

        void OnEnable() { All.Add(this); PartyHideSpot.Entered += OnSomeoneHid; }
        void OnDisable() { All.Remove(this); PartyHideSpot.Entered -= OnSomeoneHid; }

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
            agent.updateRotation = true;
            home = transform.position;
            homeRot = transform.rotation;
            phase = Random.value * 10f;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            foreach (var t in GetComponentsInChildren<Transform>())
                switch (t.name)
                {
                    case "ROOT": hips = t; break;
                    case "spine2": spine = t; break;
                    case "head": head = t; break;
                    case "arm_L": armL = t; break;
                    case "arm_R": armR = t; break;
                    case "forarm_L": foreL = t; break;
                    case "forarm_R": foreR = t; break;
                    case "thigh_L": thighL = t; break;
                    case "thigh_R": thighR = t; break;
                    case "shin_L": shinL = t; break;
                    case "shin_R": shinR = t; break;
                    case "hand_R_end": handR = t; break;
                }
            // always animate: the poses are added on top of each frame's clip pose, and a culled animator wouldn't reset it
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            swayFreq = Random.Range(4.5f, 7.5f);
            net = GetComponent<Net.NetMonster>();
            lastPos = transform.position;
            if (animator != null) model = animator.transform;
            agent.enabled = false;   // standing still at its spot; the agent only drives it while it moves
            Play("Idle");
            if (animator != null) animator.Update(Random.value * 2f);   // not all in step
        }

        // ------------------------------------------------------------------ brain
        void Update()
        {
            // how fast it is actually going (the agent's velocity means nothing on a puppet)
            moveSpeed = Mathf.Lerp(moveSpeed, Vector3.Distance(transform.position, lastPos) / Mathf.Max(Time.deltaTime, 0.001f), Time.deltaTime * 10f);
            lastPos = transform.position;
            if (net != null && net.Puppet) { Puppet(); return; }
            var floor = PartyFloor.Instance;
            bool chaos = floor != null && floor.Current == PartyFloor.Phase.Chaos;
            stateTime += Time.deltaTime;

            switch (state)
            {
                case State.Play:
                    if (chaos) Enter(State.Still);
                    break;
                case State.Still:
                    if (!chaos) { Enter(Vector3.Distance(transform.position, home) > 0.4f ? State.Return : State.Play); break; }
                    if (stateTime > stillTime + PerMonsterDelay()) Enter(State.Hunt);
                    break;
                case State.Hunt:
                    if (!chaos) { Enter(State.Return); break; }
                    Hunt();
                    break;
                case State.Attack:
                    if (target != null) Face(target.transform.position, 720f);
                    if (stateTime > 0.38f && target != null)
                    {
                        var party = PlayerParty.Instance;
                        // pulling someone out of a hiding spot it saw them get into: it only has to be at the door
                        bool dragging = witnessed != null && PartyHideSpot.HiddenIn(target) == witnessed;
                        bool reach = dragging ? Vector3.Distance(transform.position, witnessed.ExitPosition) <= 1.6f
                                              : Vector3.Distance(transform.position, target.transform.position) <= attackRange + 0.55f;
                        if (party != null && !target.IsDead && reach && (!target.IsFrozen || dragging))
                        {
                            if (witnessed != null && PartyHideSpot.HiddenIn(target) == witnessed) witnessed.DragOut(target);
                            party.KillPlayer(target, "was caught by the party guests");
                            ChaosFx.Kick(1.2f);
                            Laugh(1.15f);
                            witnessed = null;
                            Enter(State.Feed);
                            break;
                        }
                    }
                    if (stateTime > 0.95f) Enter(State.Hunt);
                    break;
                case State.Feed:
                    if (!chaos) { Enter(State.Return); break; }
                    if (stateTime > 2.5f) { target = null; Enter(State.Hunt); }
                    break;
                case State.Return:
                    if (chaos) { Enter(State.Still); break; }   // caught on the way home: it freezes there, like the rest
                    if (!agent.enabled || (!agent.pathPending && agent.remainingDistance < 0.35f) || stateTime > 8f)   // stuck: it's simply back
                    {
                        if (agent.enabled) agent.Warp(home);
                        transform.SetPositionAndRotation(home, homeRot);
                        Enter(State.Play);
                    }
                    break;
            }

            Erratic();
            UpdateAnimation();
            Footsteps();
            if (Hunting) BashDoors();
            if (net != null) net.Push((int)state, Animator.StringToHash(playing ?? "Idle"), animator != null ? animator.speed : 1f, true);
        }

        // a client's copy: the host's state and animation, its own footsteps and weaving lean
        void Puppet()
        {
            if (agent.enabled) agent.enabled = false;
            var s = (State)net.State;
            if (s != state) { state = s; stateTime = 0f; }
            else stateTime += Time.deltaTime;
            if (animator != null)
            {
                if (net.Anim != puppetAnim) { puppetAnim = net.Anim; animator.CrossFade(net.Anim, 0.15f); }
                animator.speed = net.AnimSpeed;
            }
            Erratic();
            Footsteps();
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1) return false;
            PartyLaughs.At(transform.position + Vector3.up * height * 0.8f, b / 100f);
            return true;
        }

        float PerMonsterDelay() { return (Mathf.Abs(All.IndexOf(this)) % 8) * 0.12f; }   // they don't all wake on the same frame

        void Enter(State s)
        {
            state = s;
            stateTime = 0f;
            switch (s)
            {
                case State.Play:
                    if (agent.enabled) agent.enabled = false;
                    target = null; witnessed = null;
                    if (animator != null) animator.speed = 1f;
                    Play("Idle");
                    break;
                case State.Still:
                    if (animator != null) animator.speed = 0f;   // caught mid-game, like a photo
                    if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
                    break;
                case State.Hunt:
                    if (animator != null) animator.speed = 1f;
                    if (!agent.enabled) { agent.enabled = true; NavMeshHit hit; if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position); }
                    if (agent.isOnNavMesh) { agent.isStopped = false; agent.speed = runSpeed; }
                    nextSense = 0f;
                    nextLaugh = Time.time + Random.Range(0f, 1.2f);
                    break;
                case State.Attack:
                    if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
                    Play("Attack" + Random.Range(1, 4));
                    break;
                case State.Feed:
                    if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
                    Play("Idle");
                    break;
                case State.Return:
                    if (animator != null) animator.speed = 1f;
                    if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.speed = runSpeed * 1.15f; agent.SetDestination(home); }   // scurrying back before anyone sees
                    break;
            }
        }

        void Hunt()
        {
            if (!agent.isOnNavMesh) return;
            if (Time.time > nextLaugh) { Laugh(1f); nextLaugh = Time.time + Random.Range(3.5f, 8f); }

            // someone was seen climbing into a hiding spot: go and get them
            if (witnessed != null)
            {
                var inside = witnessed.Occupant;
                if (inside == null || inside.IsDead) witnessed = null;
                else
                {
                    if (Time.time > nextRepath) { nextRepath = Time.time + 0.3f; agent.SetDestination(witnessed.ExitPosition); }
                    if (Vector3.Distance(transform.position, witnessed.ExitPosition) < 0.9f) { target = inside; Face(witnessed.transform.position, 720f); Enter(State.Attack); }
                    return;
                }
            }

            // a noise draws it off, unless it has someone right in front of it
            if (Time.time < lureUntil && (target == null || !CanSee(target) || Vector3.Distance(transform.position, target.transform.position) > 6f))
            {
                if (Time.time > nextRepath) { nextRepath = Time.time + 0.4f; NavMeshHit lh; if (NavMesh.SamplePosition(lurePos, out lh, 3f, NavMesh.AllAreas)) agent.SetDestination(lh.position + Random.insideUnitSphere * 0.6f); }
                return;
            }
            if (Time.time > nextSense) { nextSense = Time.time + Random.Range(0.9f, 1.6f); Sense(); }
            if (target == null || target.IsDead || PartyHideSpot.HiddenIn(target) != null)
            {
                target = null;
                Roam();
                return;
            }
            if (CanSee(target)) { knownPos = target.transform.position; knownVel = target.GetComponent<CharacterController>() != null ? target.GetComponent<CharacterController>().velocity : Vector3.zero; }

            if (Time.time > nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                Vector3 dest = knownPos;
                float d = Vector3.Distance(transform.position, knownPos);
                if (role == Role.Interceptor && d > 4f)
                {
                    // cut them off: where they'll be by the time it gets there
                    Vector3 v = knownVel; v.y = 0f;
                    dest = knownPos + v * Mathf.Clamp(d / runSpeed, 0f, 2.5f);
                }
                else if (role == Role.Swarm && d > 5f)
                {
                    // the swarm spreads round its victim instead of queueing behind one another
                    int i = All.IndexOf(this);
                    float a = i * 2.39996f;
                    dest = knownPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f;
                }
                NavMeshHit hit;
                if (NavMesh.SamplePosition(dest, out hit, 3f, NavMesh.AllAreas)) dest = hit.position;
                agent.SetDestination(dest);
            }

            if (Vector3.Distance(transform.position, target.transform.position) <= attackRange && CanSee(target) && !target.IsFrozen) Enter(State.Attack);
        }

        // who it knows about: everyone out in the open on this floor, within its sense radius (hidden players don't count)
        void Sense()
        {
            var party = PlayerParty.Instance;
            if (party == null) return;
            if (role == Role.Swarm)
            {
                if (swarmTarget == null || swarmTarget.IsDead || PartyHideSpot.HiddenIn(swarmTarget) != null || Time.time > swarmPick)
                {
                    swarmPick = Time.time + 4f;
                    swarmTarget = Nearest(SwarmCentre(), party);
                }
                target = swarmTarget;
            }
            else
            {
                var n = Nearest(transform.position, party);
                if (target == null || n == null || (n != target && Dist(n) < Dist(target) - 3f)) target = n;
            }
            if (target != null)
            {
                // a rough fix: within a couple of metres of where they are, unless it can see them
                knownPos = target.transform.position;
                if (!CanSee(target)) knownPos += new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                var cc = target.GetComponent<CharacterController>();
                knownVel = cc != null ? cc.velocity : Vector3.zero;
            }
        }

        RemasteredPlayerController Nearest(Vector3 from, PlayerParty party)
        {
            RemasteredPlayerController best = null;
            float bestD = senseRadius;
            foreach (var p in party.players)
            {
                if (p == null || p.IsDead || PartyHideSpot.HiddenIn(p) != null) continue;
                if (Mathf.Abs(p.transform.position.y - transform.position.y) > 2.5f) continue;   // on another floor
                float d = Vector3.Distance(from, p.transform.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        static Vector3 SwarmCentre()
        {
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var m in All) if (m != null && m.role == Role.Swarm) { c += m.transform.position; n++; }
            return n > 0 ? c / n : Vector3.zero;
        }

        void Roam()
        {
            // nobody out in the open: prowl from hiding spot to hiding spot, sniffing at each
            if (agent.pathPending) return;
            if (agent.hasPath && agent.remainingDistance > 0.8f) return;
            if (stateTime < 0.5f) return;
            var spots = PartyHideSpot.All;
            Vector3 dest = transform.position + Random.insideUnitSphere * 10f;
            if (spots.Count > 0)
            {
                PartyHideSpot best = null; float bestD = float.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    var s = spots[Random.Range(0, spots.Count)];
                    float d = Vector3.Distance(s.ExitPosition, transform.position);
                    if (d < bestD && d > 2f && Mathf.Abs(s.transform.position.y - transform.position.y) < 2.5f) { bestD = d; best = s; }
                }
                if (best != null) dest = best.ExitPosition + Random.insideUnitSphere * 0.8f;
            }
            NavMeshHit hit;
            if (NavMesh.SamplePosition(dest, out hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            stateTime = 0f;
        }

        /// <summary>A noise (a party popper, someone panting or screaming): hunting guests in earshot that aren't
        /// already on someone they can see run to it and stay a few seconds.</summary>
        public static void HeardAt(Vector3 pos, float radius, float stay = 4f)
        {
            if (!Net.Net.IsAuthority) return;   // the guests think on the host
            foreach (var m in All)
            {
                if (m == null || !m.Hunting || Mathf.Abs(m.transform.position.y - pos.y) > 2.5f) continue;
                if (Vector3.Distance(m.transform.position, pos) > radius) continue;
                m.lurePos = pos;
                m.lureUntil = Time.time + stay + Vector3.Distance(m.transform.position, pos) / m.runSpeed;
            }
        }

        void OnSomeoneHid(PartyHideSpot spot, RemasteredPlayerController body)
        {
            if (!Hunting && state != State.Still) return;
            if (Vector3.Distance(transform.position, body.transform.position) > 10f) return;
            if (!CanSee(body, true)) return;
            witnessed = spot;
        }

        bool CanSee(RemasteredPlayerController p, bool anyDirection = false)
        {
            Vector3 eye = transform.position + Vector3.up * height * 0.85f;
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            float d = Vector3.Distance(eye, chest);
            if (d > sightRange) return false;
            if (!anyDirection && d > 3f && Vector3.Angle(transform.forward, chest - eye) > 80f) return false;
            foreach (var h in Physics.RaycastAll(eye, (chest - eye).normalized, d, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                if (h.collider.GetComponentInParent<PartyMonster>() != null) continue;
                return false;
            }
            return true;
        }

        float Dist(RemasteredPlayerController p) { return Vector3.Distance(transform.position, p.transform.position); }

        void Face(Vector3 p, float speed)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), speed * Time.deltaTime);
        }

        // Hunting, it doesn't run straight: it weaves left and right, now and then darts sideways, and its body leans
        // and lurches with it. Close in (the last couple of metres) it goes straight for you.
        void Erratic()
        {
            bool running = state == State.Hunt && moveSpeed > 2f;
            float lateral = 0f;
            if (running && (target == null || Vector3.Distance(transform.position, target.transform.position) > 2.5f))
            {
                Vector3 fwd = agent.enabled ? agent.velocity : transform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                float t = Time.time + phase;
                // the weave: a sideways velocity whose rhythm drifts, so no two run the same
                lateral = Mathf.Cos(t * swayFreq) * 2.2f + (Mathf.PerlinNoise(t * 1.3f, phase) - 0.5f) * 3f;
                if (Time.time > nextDart)
                {
                    nextDart = Time.time + Random.Range(1.2f, 3.2f);
                    dartUntil = Time.time + Random.Range(0.18f, 0.32f);
                    dartDir = side * (Random.value < 0.5f ? -1f : 1f);
                    swayFreq = Random.Range(4.5f, 8f);
                }
                Vector3 move = side * lateral * Time.deltaTime;
                if (Time.time < dartUntil) { move += dartDir * 4.5f * Time.deltaTime; lateral += Vector3.Dot(dartDir, side) * 4.5f; }
                if (agent.enabled && agent.isOnNavMesh) agent.Move(move);
            }
            lean = Mathf.Lerp(lean, lateral, Time.deltaTime * 8f);
            if (model != null)
                model.localRotation = running ? Quaternion.Euler(14f + Mathf.Sin(Time.time * 17f) * 4f, Mathf.Sin(Time.time * 9f + phase) * 12f, -lean * 7f)
                                              : Quaternion.Slerp(model.localRotation, Quaternion.identity, Time.deltaTime * 6f);
        }

        void Laugh(float pitchBias)
        {
            Net.NetWorld.Act(this, 1, 0, Mathf.RoundToInt(pitchBias * 100f));   // heard on every machine
        }

        // ------------------------------------------------------------------ body
        void Play(string s)
        {
            if (animator == null || s == playing) return;
            animator.CrossFade(s, 0.15f);
            playing = s;
        }

        void UpdateAnimation()
        {
            if (animator == null || state == State.Play || state == State.Still || state == State.Attack) return;
            float v = agent.enabled ? agent.velocity.magnitude : 0f;
            Play(v > 2.6f ? "Run" : v > 0.2f ? "Walk" : "Idle");
            // the run clip is a lazy jog at 1x: a scurrying child's legs go much faster
            if (state != State.Feed) animator.speed = playing == "Run" ? Mathf.Clamp(v / runSpeed * 2.1f, 1.6f, 2.5f) : playing == "Walk" ? Mathf.Clamp(v / walkSpeed * 1.2f, 0.8f, 2f) : 1f;
        }

        void LateUpdate()
        {
            float t = Time.time + phase;
            bool posed = state == State.Play || state == State.Still;
            Vector3 X = transform.right, Y = transform.up, Z = transform.forward;

            if (posed)
            {
                switch (activity)
                {
                    case Activity.Sitting:
                    case Activity.Toy:
                        // sat on the floor, legs straight out in front like a toddler
                        if (hips != null) hips.position -= Y * height * 0.27f;
                        Rot(thighL, X, -88f); Rot(thighR, X, -88f);
                        Rot(spine, X, activity == Activity.Toy ? 18f : 6f);
                        if (activity == Activity.Toy)
                        {
                            float k = state == State.Still ? 0f : Mathf.Sin(t * 3.1f);
                            Rot(armL, X, -55f + k * 12f); Rot(armR, X, -55f - k * 12f);
                            Rot(foreL, X, -20f); Rot(foreR, X, -20f);
                        }
                        break;
                    case Activity.Balloon:
                        Rot(armR, Z, 150f);
                        Rot(foreR, Z, 28f);
                        break;
                    case Activity.Drawing:
                    {
                        float a = state == State.Still ? 0f : t * 2.4f;
                        Rot(armR, X, -98f + Mathf.Sin(a) * 9f);
                        Rot(armR, Z, Mathf.Cos(a) * 10f);
                        Rot(foreR, X, -15f);
                        break;
                    }
                    case Activity.Swaying:
                    {
                        float s = state == State.Still ? 0f : Mathf.Sin(t * 2.2f);
                        Rot(spine, Z, s * 9f);
                        Rot(armL, Z, -25f - s * 15f); Rot(armR, Z, 25f - s * 15f);
                        break;
                    }
                }
            }

            // the head: a creepy tilt at play, tracking the nearest player once the music has died
            if (head != null)
            {
                float wantYaw = 0f;
                if (state == State.Still)
                {
                    var p = PlayerParty.Instance != null ? PlayerParty.Instance.Current : null;
                    if (p != null)
                    {
                        Vector3 d = transform.InverseTransformPoint(p.transform.position); d.y = 0f;
                        wantYaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -80f, 80f);
                    }
                }
                headYaw = Mathf.MoveTowards(headYaw, wantYaw, (state == State.Still ? 40f : 120f) * Time.deltaTime);   // slowly...
                Rot(head, Y, headYaw);
                if (posed) Rot(head, Z, (activity == Activity.Drawing ? 0f : 14f) + Mathf.Sin(t * 0.7f) * (state == State.Still ? 0f : 5f));
            }

            if (balloon != null && handR != null && balloonString != null)
            {
                Vector3 top = balloon.position - Vector3.up * 0.2f;
                if (posed)
                {
                    balloon.position = handR.position + Vector3.up * 0.75f + new Vector3(Mathf.Sin(t * 0.9f), 0f, Mathf.Cos(t * 0.7f)) * 0.06f;
                    top = balloon.position - Vector3.up * 0.2f;
                }
                else
                {
                    // let go: it drifts up to the ceiling and stays there until it is fetched back with the game
                    balloon.position = Vector3.MoveTowards(balloon.position, new Vector3(balloon.position.x, transform.position.y + 3.6f, balloon.position.z), Time.deltaTime * 0.8f);
                    top = balloon.position - Vector3.up * 0.2f;
                }
                Vector3 bottom = posed ? handR.position : top - Vector3.up * 0.9f;
                balloonString.position = (top + bottom) * 0.5f;
                balloonString.rotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
                balloonString.localScale = new Vector3(balloonString.localScale.x, Vector3.Distance(top, bottom), balloonString.localScale.z);
            }
        }

        static void Rot(Transform bone, Vector3 axis, float deg)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(deg, axis) * bone.rotation;
        }

        void Footsteps()
        {
            var bank = SfxBank.Instance;
            if (bank == null || bank.monsterSteps == null || bank.monsterSteps.Length == 0) return;
            float v = moveSpeed;
            travelled += v * Time.deltaTime;
            if (travelled < 0.55f) return;
            travelled = 0f;
            // little bare feet: the same steps as the big ones, pitched up and quiet
            SfxBank.At(SfxBank.Pick(bank.monsterSteps), transform.position, Hunting ? 0.45f : 0.25f, Random.Range(1.5f, 1.8f), 14f);
        }

        void BashDoors()
        {
            if (Time.time < nextDoor || agent.velocity.sqrMagnitude < 0.2f) return;
            nextDoor = Time.time + 0.3f;
            Vector3 ahead = transform.position + transform.forward * 0.9f + Vector3.up * 0.8f;
            foreach (var c in Physics.OverlapSphere(ahead, 0.7f, ~0, QueryTriggerInteraction.Ignore))
            {
                var door = c.GetComponentInParent<InteractiveDoor>();
                if (door != null && !door.isOpen && !door.isLocked) door.SetOpen(true);
            }
        }
    }
}
