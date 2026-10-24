using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Floor 2's carnival music and stingers, made in code (no audio files): a slightly out-of-tune calliope waltz in
    /// C minor that loops seamlessly, a deep boom for the moment the music dies, and a low warbling drone for the red
    /// "chaos" phase.
    /// </summary>
    public static class PartyMusic
    {
        const int Rate = 22050;
        const float Bpm = 168f;
        static AudioClip waltz, boom, drone;

        public static AudioClip Waltz { get { return waltz != null ? waltz : (waltz = MakeWaltz()); } }
        public static AudioClip Boom { get { return boom != null ? boom : (boom = MakeBoom()); } }
        public static AudioClip Drone { get { return drone != null ? drone : (drone = MakeDrone()); } }

        // chord roots per bar (MIDI) and whether the chord is minor; 16 bars of 3/4
        static readonly int[] Roots = { 48, 48, 43, 43, 48, 48, 41, 43, 44, 44, 41, 41, 48, 43, 48, 48 };
        static readonly bool[] Minor = { true, true, false, false, true, true, true, false, false, false, true, true, true, false, true, true };
        // melody: two eighths per beat, as offsets from the bar's chord (0 root, 3/4 third, 7 fifth, 12 octave, odd = chromatic slips)
        static readonly int[][] Riffs =
        {
            new[] { 12, 11, 12, 15, 19, 15 },
            new[] { 19, 18, 19, 22, 24, 19 },
            new[] { 15, 14, 15, 12, 7, 12 },
            new[] { 24, 23, 22, 19, 15, 12 },
        };

        static float Midi(float n) { return 440f * Mathf.Pow(2f, (n - 69f) / 12f); }

        // a reedy organ pipe: a few odd and even harmonics
        static float Pipe(float phase)
        {
            return Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.25f + Mathf.Sin(phase * 3f) * 0.18f + Mathf.Sin(phase * 4f) * 0.07f;
        }

        static AudioClip MakeWaltz()
        {
            float beat = 60f / Bpm, bar = beat * 3f;
            int bars = Roots.Length, n = Mathf.RoundToInt(bar * bars * Rate);
            var data = new float[n];
            var rnd = new System.Random(11);
            double melPhase = 0, bassPhase = 0, chordPhase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int b = Mathf.Min(bars - 1, (int)(t / bar));
                float inBar = t - b * bar;
                int beatIdx = Mathf.Min(2, (int)(inBar / beat));
                float inBeat = inBar - beatIdx * beat;
                int root = Roots[b];
                int third = Minor[b] ? 3 : 4;
                // the tape wobbles: a slow pitch drift shared by every voice, and the calliope is a little flat
                float wow = Mathf.Sin(t * 2f * Mathf.PI * 0.35f) * 0.18f + Mathf.Sin(t * 2f * Mathf.PI * 2.1f) * 0.04f - 0.12f;

                // melody: eighth notes picked from the riff for this bar, the third adjusted to the chord
                var riff = Riffs[(b + (b / 4)) % Riffs.Length];
                int e = Mathf.Min(5, (int)(inBar / (beat * 0.5f)));
                int off = riff[e];
                if (off % 12 == 3 || off % 12 == 4) off = off - (off % 12) + third;
                float mel = root + 12 + off + wow;
                melPhase += 2.0 * Mathf.PI * Midi(mel) / Rate;
                float inEighth = inBar - e * beat * 0.5f;
                float melEnv = Mathf.Clamp01(inEighth / 0.008f) * Mathf.Lerp(1f, 0.55f, inEighth / (beat * 0.5f));

                // oom (bass on 1) pah pah (chord on 2 and 3)
                float bassEnv = beatIdx == 0 ? Mathf.Exp(-inBeat * 5f) : 0f;
                bassPhase += 2.0 * Mathf.PI * Midi(root - 12 + wow) / Rate;
                float chordEnv = beatIdx > 0 ? Mathf.Exp(-inBeat * 9f) : 0f;
                chordPhase += 2.0 * Mathf.PI * Midi(root + wow) / Rate;
                float chord = Pipe((float)chordPhase) + Pipe((float)(chordPhase * Mathf.Pow(2f, third / 12f))) + Pipe((float)(chordPhase * 1.4983f));

                float s = Pipe((float)melPhase) * melEnv * 0.42f
                        + Mathf.Sin((float)bassPhase) * bassEnv * 0.5f
                        + chord * chordEnv * 0.11f
                        + (float)(rnd.NextDouble() * 2 - 1) * 0.012f;   // hiss of an old speaker
                data[i] = Mathf.Clamp(s * 0.8f, -1f, 1f);
            }
            // seamless loop: fade the last few ms into the first
            int x = Rate / 50;
            for (int i = 0; i < x; i++) { float a = i / (float)x; data[n - x + i] = data[n - x + i] * (1f - a) + data[i] * a; }
            var clip = AudioClip.Create("PartyWaltz", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeBoom()
        {
            int n = Mathf.RoundToInt(Rate * 2.2f);
            var data = new float[n];
            var rnd = new System.Random(3);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += 2.0 * Mathf.PI * Mathf.Lerp(70f, 32f, Mathf.Clamp01(t / 1.2f)) / Rate;
                float s = Mathf.Sin((float)ph) * Mathf.Exp(-t * 1.8f) + (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 14f) * 0.4f;
                data[i] = Mathf.Clamp(s * 0.9f, -1f, 1f);
            }
            var clip = AudioClip.Create("PartyBoom", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeDrone()
        {
            int n = Rate * 4;   // whole cycles of every partial over 4 s, so it loops cleanly
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = 1f + 0.35f * Mathf.Sin(t * 2f * Mathf.PI * 0.5f);
                float s = Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.45f + Mathf.Sin(t * 2f * Mathf.PI * 58.25f) * 0.3f * w + Mathf.Sin(t * 2f * Mathf.PI * 110.5f) * 0.12f;
                data[i] = s * 0.6f;
            }
            var clip = AudioClip.Create("PartyDrone", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
