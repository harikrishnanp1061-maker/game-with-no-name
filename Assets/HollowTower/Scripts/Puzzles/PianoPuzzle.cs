using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The party's giant floor piano (floor 2 music room): six coloured pads on the floor and a music box on the
    /// teacher's table. Wind the music box (E) and it plays a tune while the pads light up in time; walk the tune back
    /// on the pads, stepping on them in order. Three rounds, each longer. Get it wrong and the box shrieks out of tune -
    /// every party guest on the floor hears it - and the tune starts again from the first round. Played through, the
    /// box sings the whole birthday song and the exit lift's override, wired into the party's sound system, clicks free.
    /// </summary>
    public class PianoPuzzle : WorldPuzzle
    {
        public static PianoPuzzle Instance { get; private set; }
        public const int MusicBox = 100;

        public Renderer[] pads;
        public Color[] padColours;
        public float[] padHz = { 262f, 294f, 330f, 392f, 440f, 523f };
        public int[] roundLengths = { 4, 5, 6 };

        int[] tune;
        int round, input;
        bool wound;
        float demoAt;
        float[] glow;
        MaterialPropertyBlock block;

        protected override void OnEnable() { base.OnEnable(); Instance = this; }
        protected override void OnDisable() { base.OnDisable(); if (Instance == this) Instance = null; }

        protected override void Setup(System.Random rnd)
        {
            tune = new int[roundLengths[roundLengths.Length - 1]];
            for (int i = 0; i < tune.Length; i++) { do tune[i] = rnd.Next(pads.Length); while (i > 0 && tune[i] == tune[i - 1]); }
        }

        bool Demoing { get { return wound && Time.time < demoAt + roundLengths[Mathf.Min(round, roundLengths.Length - 1)] * 0.7f + 0.3f; } }

        public override string PartPrompt(int part, int player)
        {
            if (Solved) return null;
            if (part == MusicBox) return wound ? null : "[E] Wind the music box - then step the tune back on the pads";
            return null;
        }

        protected override void OnPress(int part, int player)
        {
            if (tune == null) return;
            if (part == MusicBox) { if (!wound) { wound = true; round = 0; input = 0; demoAt = Time.time + 0.6f; } return; }
            if (part < 0 || part >= pads.Length) return;
            Play(part);
            if (!wound || Demoing) return;
            if (tune[input] == part)
            {
                input++;
                if (input >= roundLengths[round])
                {
                    round++; input = 0;
                    if (round >= roundLengths.Length) { Solve(player); return; }
                    demoAt = Time.time + 1.4f;
                }
                return;
            }
            // wrong note: the box shrieks, the guests hear, back to the start
            SfxBank.At(Tone(185f, 1.2f, false), transform.position, 1f, 1f, 45f);
            SfxBank.At(Tone(196f, 1.2f, false), transform.position, 1f, 1f, 45f);
            Noise(40f);
            wound = false; round = 0; input = 0;
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("The music box shrieks out of tune - wind it and start again", 3f);
        }

        void Play(int pad)
        {
            if (glow == null) glow = new float[pads.Length];
            glow[pad] = 1f;
            SfxBank.At(Tone(padHz[pad % padHz.Length], 0.7f), pads[pad].transform.position + Vector3.up, 0.9f, 1f, 25f);
        }

        int lastDemoNote = -1;
        protected override void Update()
        {
            base.Update();
            if (pads == null) return;
            if (glow == null) glow = new float[pads.Length];
            // the music box's demonstration: one note every 0.7 s
            if (wound && tune != null && Time.time >= demoAt)
            {
                int n = Mathf.FloorToInt((Time.time - demoAt) / 0.7f);
                if (n < roundLengths[Mathf.Min(round, roundLengths.Length - 1)] && n != lastDemoNote) { lastDemoNote = n; Play(tune[n]); }
            }
            else lastDemoNote = -1;
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < pads.Length; i++)
            {
                glow[i] = Mathf.MoveTowards(glow[i], Solved ? 0.35f : 0f, Time.deltaTime * 2f);
                pads[i].GetPropertyBlock(block);
                block.SetColor(Gfx.EmissiveId, Gfx.ToNits(padColours[i] * (0.08f + glow[i] * 3f)));
                pads[i].SetPropertyBlock(block);
            }
        }

        public override string Describe()
        {
            if (tune == null) return title + " (not rolled)";
            var s = title + ": round " + (round + 1) + " tune ";
            foreach (var t in tune) s += t;
            return s;
        }
    }
}
