using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// What a puzzle box and a keypad lock share, kept the same on every machine through NetWorld: the seed the puzzle
    /// is generated from (rolled the first time anyone opens it, so each game gets new puzzles), whether it is solved,
    /// and the alarm when someone fails it: a siren at the puzzle, then the nearest monster on that floor comes for the
    /// player who failed, wherever they are, for huntSeconds (hiding still works on the party floor). The puzzle then
    /// re-scrambles to a new seed and stays jammed for jamSeconds, so it can't just be retried at once.
    /// </summary>
    public abstract class PuzzleState : MonoBehaviour, Net.INetAction
    {
        public float jamSeconds = 8f;
        public float huntSeconds = 25f;

        public int Seed { get; private set; }
        public bool Solved { get; private set; }
        public int Fails { get; private set; }
        public bool Jammed { get { return Time.time < jamUntil; } }
        public float JamLeft { get { return Mathf.Max(0f, jamUntil - Time.time); } }

        float jamUntil;
        int pendingPlayer = -1;
        static AudioClip siren;

        /// <summary>The panel for this puzzle on this machine, for this player (the seed is known by now).</summary>
        protected abstract void ShowPanel(int player);
        /// <summary>One line for the F10 proof overlay: what it is and why it is solvable.</summary>
        public abstract string Describe();
        protected virtual void OnSolvedHere(int player) { }

        /// <summary>This machine's player wants to try it: roll the seed first if nobody has yet.</summary>
        protected void Begin(int player)
        {
            if (Solved || Jammed || PuzzlePanel.IsOpen) return;
            if (Seed != 0) { ShowPanel(player); return; }
            pendingPlayer = player;
            Net.NetWorld.Act(this, 1, 0, NewSeed());
        }

        protected virtual void Update()
        {
            // a client's roll comes back from the host a moment later
            if (pendingPlayer >= 0 && Seed != 0) { int p = pendingPlayer; pendingPlayer = -1; ShowPanel(p); }
        }

        public void ReportSolved(int player) { Net.NetWorld.Act(this, 2, player); }
        public void ReportFailed(int player) { Net.NetWorld.Act(this, 3, player, KeepSeedOnFail ? Seed : NewSeed()); }

        /// <summary>A lock whose answer is written somewhere in the world (a hidden code) keeps it after a failure.</summary>
        protected virtual bool KeepSeedOnFail { get { return false; } }

        /// <summary>Rolls the seed now (host, once the session is up), for puzzles whose answer is shown in the world
        /// before anyone opens them.</summary>
        public void EnsureSeed()
        {
            if (Seed != 0 || !Net.Net.IsAuthority || !Net.NetWorld.Ready) return;
            Net.NetWorld.Act(this, 1, 0, NewSeed());
        }

        static int NewSeed() { return Random.Range(1, int.MaxValue); }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            var party = PlayerParty.Instance;
            switch (action)
            {
                case 1:   // first roll: whoever got there first decides
                    if (Seed != 0) return false;
                    Seed = b;
                    return true;
                case 2:
                    if (Solved) return false;
                    Solved = true;
                    if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.socketPower, transform.position, 0.9f, 1.15f, 20f);
                    OnSolvedHere(a);
                    return true;
                case 3:
                    if (Solved) return false;
                    if (b == 0) return false;
                    Seed = b;
                    Fails++;
                    jamUntil = Time.time + jamSeconds;
                    Alarm(party != null && a >= 0 && a < party.players.Length ? party.players[a] : null, a);
                    return true;
            }
            return Extra(action, a, b, v);
        }

        /// <summary>A subclass's own networked actions (4 and up).</summary>
        protected virtual bool Extra(int action, int a, int b, Vector3 v) { return false; }

        void Alarm(RemasteredPlayerController body, int player)
        {
            SfxBank.At(Siren(), transform.position + Vector3.up * 0.3f, 1f, 1f, 45f);
            // the monsters think on the host: these do nothing on a client
            NeedleMonster.Hear(transform.position, 30f);
            PartyMonster.HeardAt(transform.position, 30f);
            Campus.NailedMonster.HearAt(transform.position, 40f);
            bool coming = NeedleMonster.Summon(body, huntSeconds) | PartyMonster.Summon(body, huntSeconds) > 0 | Campus.NailedMonster.Summon(body, huntSeconds);
            if (PlayerParty.Instance != null)
                PlayerParty.Instance.Flash(body != null && (coming || !Net.Net.IsAuthority)
                    ? "The alarm screams - something is coming for Player " + (player + 1) + ". RUN."
                    : "The alarm screams through the floor", 4f);
            ChaosFx.Kick(0.8f);
        }

        /// <summary>A harsh two-tone klaxon, made once.</summary>
        static AudioClip Siren()
        {
            if (siren != null) return siren;
            const int rate = 44100;
            int n = (int)(rate * 2.4f);
            var data = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = (int)(t / 0.3f) % 2 == 0 ? 880f : 660f;
                ph += f / rate;
                float sq = (ph % 1f) < 0.5f ? 1f : -1f;
                float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((2.4f - t) * 4f);
                data[i] = (sq * 0.25f + Mathf.Sin(ph * 2f * Mathf.PI) * 0.2f) * env;
            }
            siren = AudioClip.Create("PuzzleSiren", n, 1, rate, false);
            siren.SetData(data, 0);
            return siren;
        }
    }
}
