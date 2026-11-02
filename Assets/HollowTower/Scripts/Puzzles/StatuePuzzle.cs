using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The watchers (campus quad): four stone gargoyles on plinths round the fountain, each turned a different way. A
    /// bronze plaque on the fountain says, in a riddle, what they should all be watching. Turn every statue (E, an
    /// eighth of a turn, stone grinding on stone - out in the open, where the giant walks) until all four face that
    /// place, and a hatch in the fountain's column drops open: the groundskeeper's key to the service gate's padlock.
    /// </summary>
    public class StatuePuzzle : WorldPuzzle
    {
        public Transform[] statues;
        public Vector3[] targets;
        public string[] riddles;
        public TextMesh plaque;

        int target;
        int[] rot;
        float[] baseYaw;

        protected override void Init()
        {
            base.Init();
            baseYaw = new float[statues.Length];
            for (int i = 0; i < statues.Length; i++) baseYaw[i] = statues[i].eulerAngles.y;
        }

        protected override void Setup(System.Random rnd)
        {
            target = rnd.Next(targets.Length);
            rot = new int[statues.Length];
            for (int i = 0; i < rot.Length; i++) { int guard = 0; do rot[i] = rnd.Next(8); while (Facing(i) && guard++ < 20); }
            if (plaque != null) plaque.text = "THE WATCHERS\n" + riddles[target];
        }

        bool Facing(int i)
        {
            Vector3 to = targets[target] - statues[i].position; to.y = 0f;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(baseYaw[i] + rot[i] * 45f, want)) <= 22.6f;
        }

        public override string PartPrompt(int part, int player) { return Solved ? null : "[E] Turn the statue"; }

        protected override void OnPress(int part, int player)
        {
            if (rot == null || part < 0 || part >= rot.Length) return;
            rot[part] = (rot[part] + 1) % 8;
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.wallGrind, statues[part].position + Vector3.up, 0.9f, 1.3f, 40f);
            Noise(25f);
            for (int i = 0; i < rot.Length; i++) if (!Facing(i)) return;
            Solve(player);
        }

        protected override void Update()
        {
            base.Update();
            if (rot == null || baseYaw == null) return;
            for (int i = 0; i < statues.Length; i++)
                statues[i].rotation = Quaternion.RotateTowards(statues[i].rotation, Quaternion.Euler(0f, baseYaw[i] + rot[i] * 45f, 0f), 90f * Time.deltaTime);
        }

        public override string Describe()
        {
            if (rot == null) return title + " (not rolled)";
            var s = title + ": watch " + target + " facing ";
            for (int i = 0; i < rot.Length; i++) s += (Facing(i) ? "Y" : "n");
            return s;
        }
    }
}
