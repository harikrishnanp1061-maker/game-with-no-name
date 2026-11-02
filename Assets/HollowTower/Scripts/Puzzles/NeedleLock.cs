using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// A timing lock: a gauge needle sweeps back and forth and has to be stopped inside the yellow band, several times
    /// in a row, with the band narrower and the needle faster each time (the campus generator: catch the ignition as
    /// the pressure peaks). The bands come from the seed, so every machine shows the same gauge. A miss makes a noise
    /// (the engine backfires: monsters come to look); too many misses set off the alarm like any failed puzzle.
    /// </summary>
    public class NeedleLock : PuzzleState
    {
        public string title = "GAUGE";
        public string instructions = "Stop the needle in the yellow band - [SPACE] or click.";
        public int hitsNeeded = 3;
        public int missesAllowed = 2;
        [Tooltip("Degrees per second at the first stage; each stage is faster.")]
        public float speed = 95f;
        public float speedGain = 0.4f;
        [Tooltip("Width of the yellow band (degrees of the 140 degree dial) at the first and last stage.")]
        public float bandStart = 30f, bandEnd = 16f;
        public string missMessage = "It coughs and dies.";

        /// <summary>Every machine: a miss happened (the owner plays the bang and calls the monsters).</summary>
        public event System.Action<int> Missed;

        System.Action pending;

        public bool TryOpen(int player, System.Action then)
        {
            if (Solved) return true;
            pending = then;
            Begin(player);
            return false;
        }

        protected override void ShowPanel(int player)
        {
            var then = pending; pending = null;
            PuzzlePanel.OpenNeedle(this, player, then);
        }

        /// <summary>Centre (degrees, -70..70 across the dial) and width of the band at a stage.</summary>
        public void Band(int stage, out float centre, out float width)
        {
            var rnd = new System.Random(Seed * 31 + stage * 7919);
            width = Mathf.Lerp(bandStart, bandEnd, hitsNeeded > 1 ? stage / (float)(hitsNeeded - 1) : 0f);
            float lim = 70f - width * 0.5f - 4f;
            centre = Mathf.Lerp(-lim, lim, (float)rnd.NextDouble());
        }

        public float SpeedAt(int stage) { return speed * (1f + speedGain * stage); }

        public void ReportMiss(int player) { Net.NetWorld.Act(this, 4, player); }

        protected override bool Extra(int action, int a, int b, Vector3 v)
        {
            if (action != 4 || Solved) return false;
            if (Missed != null) Missed(a);
            return true;
        }

        public override string Describe()
        {
            if (Seed == 0) return title + ": not rolled yet";
            var sb = new System.Text.StringBuilder(title + " seed " + Seed + ": bands ");
            for (int i = 0; i < hitsNeeded; i++) { float c, w; Band(i, out c, out w); sb.Append(c.ToString("F0") + "+-" + (w * 0.5f).ToString("F0") + " "); }
            sb.Append("(every band lies on the dial, so it can always be stopped in)");
            return sb.ToString();
        }
    }
}
