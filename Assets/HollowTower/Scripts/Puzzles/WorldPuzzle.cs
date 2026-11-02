using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// A puzzle played in the world itself, no panel: books pulled from a shelf, mirrors turned on their stands, pads
    /// stepped on, handwheels turned, statues turned on their plinths. Every press goes through NetWorld and runs on
    /// every machine in the host's order, from the same seed (rolled by the host when the session is up), so the
    /// puzzle's state is the same everywhere without sending it. Each type appears once in the whole game.
    /// </summary>
    public abstract class WorldPuzzle : MonoBehaviour, Net.INetAction
    {
        public static readonly List<WorldPuzzle> All = new List<WorldPuzzle>();

        public string title = "Puzzle";
        [Tooltip("Shown to everyone when it is solved.")]
        public string solvedMessage = "Something clicks open";

        [Header("What it guards")]
        [Tooltip("An item sealed away until the puzzle is solved (a fuse cell, a crank handle, a key).")]
        public PartyItem reward;
        [Tooltip("A hatch, lid or door that swings open when solved.")]
        public Transform rewardDoor;
        public Vector3 rewardDoorOpen = new Vector3(0f, -100f, 0f);

        public int Seed { get; private set; }
        Quaternion doorClosed; float doorT;

        bool inited;
        void Start() { EnsureInit(); }
        /// <summary>Lay out the rest state once - from Start, or earlier if the host's seed arrives first (a client
        /// that gets the RPC while the scene is still spawning, before Start has run).</summary>
        void EnsureInit() { if (inited) return; inited = true; Init(); }

        protected virtual void Init()
        {
            if (rewardDoor != null) doorClosed = rewardDoor.localRotation;
            Seal(true);
        }

        void Seal(bool shut)
        {
            if (reward == null) return;
            foreach (var c in reward.GetComponentsInChildren<Collider>(true)) c.enabled = !shut;
            reward.enabled = !shut;
            if (shut && reward.glowLight != null) reward.glowLight.enabled = false;
        }

        protected void AnimateDoor()
        {
            if (rewardDoor == null || !Solved || doorT >= 1f) return;
            doorT = Mathf.MoveTowards(doorT, 1f, Time.deltaTime / 1.6f);
            rewardDoor.localRotation = doorClosed * Quaternion.Euler(rewardDoorOpen * Mathf.SmoothStep(0f, 1f, doorT));
        }
        public bool Solved { get; private set; }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        protected virtual void Update()
        {
            if (Seed == 0 && Net.Net.IsAuthority && Net.NetWorld.Ready) Net.NetWorld.Act(this, 1, 0, Random.Range(1, int.MaxValue));
            AnimateDoor();
        }

        /// <summary>A piece was used (E, or stepped on) by this machine's player.</summary>
        public void Press(int part, int player)
        {
            if (Solved || Seed == 0) return;
            Net.NetWorld.Act(this, 2, part, player);
        }

        /// <summary>DEV: solve it outright.</summary>
        public void DevSolve(int player) { if (!Solved) Net.NetWorld.Act(this, 3, 0, player); }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            EnsureInit();
            switch (action)
            {
                case 1: if (Seed != 0) return false; Seed = b; Setup(new System.Random(b)); return true;
                case 2: if (Solved || Seed == 0) return false; OnPress(a, b); return true;
                case 3: if (Solved) return false; Solve(b); return true;
            }
            return false;
        }

        /// <summary>Every machine, once: lay the puzzle out from the seed (scramble it).</summary>
        protected abstract void Setup(System.Random rnd);
        /// <summary>Every machine, in the same order: a piece was used. Call Solve when it is done.</summary>
        protected abstract void OnPress(int part, int player);
        /// <summary>The prompt over a piece for this player (null: none).</summary>
        public abstract string PartPrompt(int part, int player);
        /// <summary>F10 proof overlay line.</summary>
        public virtual string Describe() { return title + (Seed == 0 ? " (not rolled)" : " seed " + Seed); }

        protected void Solve(int player)
        {
            if (Solved) return;
            Solved = true;
            Seal(false);
            OnSolved(player);
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.socketPower, transform.position + Vector3.up, 1f, 0.9f, 30f);
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash(solvedMessage, 5f);
        }

        protected virtual void OnSolved(int player) { }

        /// <summary>Something loud happened at the puzzle (a wrong move): every monster that can hear it comes.</summary>
        protected void Noise(float radius)
        {
            Net.NetWorld.Noise(transform.position, radius);
        }

        /// <summary>DEV: the nearest unsolved world puzzle to a point.</summary>
        public static WorldPuzzle Nearest(Vector3 p, float within)
        {
            WorldPuzzle best = null; float bd = within;
            foreach (var w in All) { if (w == null || w.Solved) continue; float d = Vector3.Distance(w.transform.position, p); if (d < bd) { bd = d; best = w; } }
            return best;
        }

        /// <summary>A simple tone (pads, gears): cached per pitch.</summary>
        static readonly Dictionary<int, AudioClip> tones = new Dictionary<int, AudioClip>();
        public static AudioClip Tone(float hz, float seconds = 0.5f, bool bell = true)
        {
            int key = Mathf.RoundToInt(hz * 10f) * 10 + (bell ? 1 : 0);
            AudioClip c;
            if (tones.TryGetValue(key, out c)) return c;
            const int rate = 44100; int n = (int)(rate * seconds);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float env = bell ? Mathf.Exp(-t * 5f) : Mathf.Clamp01(t * 40f) * Mathf.Clamp01((seconds - t) * 10f);
                d[i] = (Mathf.Sin(t * hz * 6.283f) * 0.5f + Mathf.Sin(t * hz * 2f * 6.283f) * 0.15f + Mathf.Sin(t * hz * 3f * 6.283f) * 0.05f) * env * 0.6f;
            }
            c = AudioClip.Create("Tone" + hz, n, 1, rate, false); c.SetData(d, 0);
            tones[key] = c;
            return c;
        }
    }
}
