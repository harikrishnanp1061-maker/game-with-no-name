namespace HollowTower.Puzzles
{
    /// <summary>
    /// A keypad beside a door that also needs a fuse cell (the stairwell lock, the exit elevator): its owner asks
    /// TryOpen first, and only does its own thing (take the cell, close the doors) once the code is in. The code and
    /// its clues come from the seed (CodeLock), so every machine shows the same note.
    /// </summary>
    public class PuzzleLock : PuzzleState
    {
        public string title = "Keypad";
        public int tries = 2;
        [UnityEngine.Tooltip("No note of clues: the code itself is written somewhere close by (CodeNote), and stays the same after a wrong try.")]
        public bool hiddenCode;
        public string hiddenHint = "There is no note here. Whoever used this door must have kept the code somewhere close by.";

        protected override bool KeepSeedOnFail { get { return hiddenCode; } }
        int triesFails;

        CodeLock code;
        int codeSeed, triesSeed;
        int triesLeft;

        public CodeLock Code
        {
            get
            {
                if (code == null || codeSeed != Seed) { code = new CodeLock(Seed); codeSeed = Seed; }
                return code;
            }
        }

        /// <summary>Tries left on this seed for this machine's player (a re-scramble after the alarm gives them back).</summary>
        public int TriesLeft
        {
            get { if (triesSeed != Seed || triesFails != Fails) { triesSeed = Seed; triesFails = Fails; triesLeft = tries; } return triesLeft; }
            set { triesSeed = Seed; triesLeft = value; }
        }

        System.Action pending;

        /// <summary>The player pressed E on the owner: true if the code is already in (go ahead), otherwise the keypad
        /// opens and then() runs here once they get it right.</summary>
        public bool TryOpen(int player, System.Action then)
        {
            if (Solved) return true;
            pending = then;
            Begin(player);
            return false;
        }

        protected override void ShowPanel(int player)
        {
            var then = pending;
            pending = null;
            PuzzlePanel.OpenCodeLock(this, player, then);
        }

        public string Status(string needs)
        {
            if (Jammed) return title + ": jammed by the alarm - " + UnityEngine.Mathf.CeilToInt(JamLeft) + " s";
            return needs;
        }

        public override string Describe()
        {
            if (Seed == 0) return title + ": code not generated yet (rolled on first use)";
            var c = Code;
            return title + "  seed " + Seed + "  -  " + c.clues.Count + " clues, " + c.Solutions + " of 1000 codes fit: UNIQUE  (" + c.code[0] + c.code[1] + c.code[2] + ")";
        }
    }
}
