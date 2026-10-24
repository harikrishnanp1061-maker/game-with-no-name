using UnityEngine;

namespace HollowTower
{
    /// <summary>Little synthesised sounds for the usable items: a sweet wrapper's rustle, a can's hiss, a party
    /// popper's bang and a glow stick's snap.</summary>
    public static class ItemSfx
    {
        const int Rate = 44100;
        static AudioClip rustle, fizz, pop, crack;

        public static AudioClip Rustle { get { return rustle != null ? rustle : (rustle = Make("Rustle", 0.7f, RustleAt)); } }
        public static AudioClip Fizz { get { return fizz != null ? fizz : (fizz = Make("Fizz", 1.1f, FizzAt)); } }
        public static AudioClip Pop { get { return pop != null ? pop : (pop = Make("Pop", 0.9f, PopAt)); } }
        public static AudioClip Crack { get { return crack != null ? crack : (crack = Make("Crack", 0.35f, CrackAt)); } }

        static System.Random rnd = new System.Random(3);
        static float N() { return (float)(rnd.NextDouble() * 2 - 1); }
        static float lp;

        delegate float Gen(float t, float len);

        static AudioClip Make(string name, float len, Gen g)
        {
            int n = Mathf.RoundToInt(len * Rate);
            var d = new float[n];
            lp = 0f;
            for (int i = 0; i < n; i++) d[i] = g(i / (float)Rate, len);
            float peak = 0.001f;
            foreach (var x in d) peak = Mathf.Max(peak, Mathf.Abs(x));
            for (int i = 0; i < n; i++) d[i] = d[i] / peak * 0.8f;
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        // crinkles: noise in short random bursts
        static float RustleAt(float t, float len)
        {
            float burst = Mathf.PerlinNoise(t * 38f, 1.7f);
            burst = burst > 0.55f ? (burst - 0.55f) * 4f : 0f;
            lp += (N() - lp) * 0.6f;
            return (N() - lp) * burst * Mathf.Clamp01((len - t) / 0.1f);
        }

        // the tab's crack, then a hiss that dies away
        static float FizzAt(float t, float len)
        {
            float tab = t < 0.03f ? N() * (1f - t / 0.03f) : 0f;
            lp += (N() - lp) * 0.85f;
            float hiss = (N() - lp) * 0.35f * Mathf.Exp(-t * 2.2f) * Mathf.Clamp01((t - 0.02f) / 0.05f);
            float bubbles = Mathf.PerlinNoise(t * 90f, 4f) > 0.7f ? N() * 0.2f * Mathf.Exp(-t * 1.5f) : 0f;
            return tab + hiss + bubbles;
        }

        // a bang with a low thump under it and a paper-streamer patter after
        static float PopAt(float t, float len)
        {
            float bang = N() * Mathf.Exp(-t * 45f);
            float thump = Mathf.Sin(t * 2f * Mathf.PI * 70f) * Mathf.Exp(-t * 18f) * 0.8f;
            float patter = t > 0.08f && Mathf.PerlinNoise(t * 60f, 8f) > 0.72f ? N() * 0.15f * Mathf.Exp(-(t - 0.08f) * 3f) : 0f;
            return bang + thump + patter;
        }

        // a sharp plastic snap and a little rattle
        static float CrackAt(float t, float len)
        {
            float snap = N() * Mathf.Exp(-t * 120f);
            float rattle = t > 0.04f ? N() * 0.25f * Mathf.Exp(-(t - 0.04f) * 30f) * (Mathf.Sin(t * 900f) > 0f ? 1f : 0.3f) : 0f;
            return snap + rattle;
        }
    }
}
