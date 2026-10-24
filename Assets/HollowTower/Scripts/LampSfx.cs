using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Sounds of failing fluorescent lamps, made in code so no audio files are needed: an electric buzz-crackle for a
    /// stutter, a sharp pop when a lamp cuts out, and a quiet mains hum to loop near a failing lamp.
    /// </summary>
    public static class LampSfx
    {
        const int Rate = 44100;
        static AudioClip buzz, pop, hum;

        public static AudioClip Buzz { get { return buzz != null ? buzz : (buzz = Make("LampBuzz", 0.45f, BuzzSample)); } }
        public static AudioClip Pop { get { return pop != null ? pop : (pop = Make("LampPop", 0.18f, PopSample)); } }
        public static AudioClip Hum { get { return hum != null ? hum : (hum = Make("LampHum", 1f, HumSample)); } }

        static System.Random rnd = new System.Random(7);
        static float Noise() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

        // 100 Hz mains buzz (rich in harmonics) chopped by random crackle bursts, fading out
        static float BuzzSample(float t, float len)
        {
            float mains = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 100f * t)) * 0.35f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.25f;
            float gate = Mathf.PerlinNoise(t * 60f, 3.1f) > 0.45f ? 1f : 0.15f;
            float crackle = Noise() * (Mathf.PerlinNoise(t * 140f, 7.7f) > 0.7f ? 0.8f : 0.05f);
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((len - t) / 0.08f);
            return (mains * gate + crackle) * env * 0.6f;
        }

        // a click with a short resonant thunk behind it
        static float PopSample(float t, float len)
        {
            float click = Noise() * Mathf.Exp(-t * 180f);
            float thunk = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 30f) * 0.5f;
            return (click + thunk) * 0.8f;
        }

        // seamless 1 s loop: whole cycles of 100 Hz and harmonics, a little noise
        static float HumSample(float t, float len)
        {
            float h = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.12f;
            return (h + Noise() * 0.04f) * 0.35f;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, seconds), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
