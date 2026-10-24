using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The party guests' laughter. If a clip is at Resources/MonsterLaugh, snippets of its first 25 seconds are used;
    /// otherwise a handful of children's giggles are synthesised (voiced "ha"s shaped by child vowel formants, with
    /// breathy onsets and a hall tail). Every laugh is layered: a second, slightly later and differently pitched voice
    /// plays with it, so a few guests laughing at once sound like a crowd of children. All 3D, at the guest.
    /// </summary>
    public class PartyLaughs : MonoBehaviour
    {
        public const float SourceWindow = 25f;   // only the first 25 s of a supplied recording

        static PartyLaughs instance;
        static AudioClip supplied;
        static bool looked;
        static AudioClip[] giggles;

        class Voice { public AudioSource src; public float stopAt, fadeFrom, delayUntil; public float volume; }
        readonly List<Voice> voices = new List<Voice>();

        static PartyLaughs Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("PartyLaughs");
            instance = go.AddComponent<PartyLaughs>();
            for (int i = 0; i < 18; i++)
            {
                var vgo = new GameObject("Voice" + i);
                vgo.transform.SetParent(go.transform, false);
                var s = vgo.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f; s.maxDistance = 30f; s.dopplerLevel = 0f;
                // an empty school: every giggle rings down the hall
                var verb = vgo.AddComponent<AudioReverbFilter>();
                verb.reverbPreset = AudioReverbPreset.Hallway;
                instance.voices.Add(new Voice { src = s });
            }
            return instance;
        }

        /// <summary>A guest laughs here (pitchBias above 1 for a shriller, nastier laugh).</summary>
        public static void At(Vector3 pos, float pitchBias = 1f)
        {
            var me = Get();
            if (!looked) { looked = true; supplied = Resources.Load<AudioClip>("MonsterLaugh"); }
            // each laugh: the giggle a little higher (a smaller child) with a second voice under it, sometimes dropped
            // low and slow - the thing wearing the child
            float basePitch = Random.Range(1.0f, 1.28f) * pitchBias;
            me.Start1(pos, basePitch, 0f, 1f);
            float under = Random.value < 0.35f ? Random.Range(0.62f, 0.75f) : basePitch * Random.Range(1.1f, 1.25f);
            me.Start1(pos + Random.insideUnitSphere * 0.3f, under, Random.Range(0.08f, 0.3f), under < 0.8f ? 0.75f : 0.55f);   // the overlap
        }

        void Start1(Vector3 pos, float pitch, float delay, float volume)
        {
            Voice v = null;
            foreach (var x in voices) if (!x.src.isPlaying && Time.time >= x.delayUntil) { v = x; break; }
            if (v == null) return;   // the crowd is loud enough already
            var s = v.src;
            s.transform.position = pos;
            if (supplied != null)
            {
                float window = Mathf.Min(SourceWindow, supplied.length);
                float dur = Mathf.Min(window, Random.Range(1.6f, 3.2f));
                s.clip = supplied;
                s.time = Random.Range(0f, Mathf.Max(0f, window - dur));
                v.stopAt = Time.time + delay + dur / pitch;
            }
            else
            {
                if (giggles == null) giggles = MakeGiggles(6);
                s.clip = giggles[Random.Range(0, giggles.Length)];
                s.time = 0f;
                v.stopAt = Time.time + delay + s.clip.length / pitch + 0.05f;
            }
            v.volume = volume;
            s.pitch = pitch;
            s.volume = volume;
            v.fadeFrom = v.stopAt - 0.35f;
            v.delayUntil = Time.time + delay;
            s.PlayDelayed(delay);
        }

        void Update()
        {
            foreach (var v in voices)
            {
                if (!v.src.isPlaying) continue;
                if (Time.time >= v.stopAt) { v.src.Stop(); continue; }
                v.src.volume = Time.time > v.fadeFrom ? v.volume * Mathf.Clamp01((v.stopAt - Time.time) / 0.35f) : v.volume;
            }
        }

        // ------------------------------------------------------------------ synthesis
        const int Rate = 44100;

        static AudioClip[] MakeGiggles(int n)
        {
            var list = new AudioClip[n];
            var rnd = new System.Random(77);
            for (int i = 0; i < n; i++) list[i] = Giggle(rnd, i);
            return list;
        }

        static float R(System.Random r, float a, float b) { return a + (b - a) * (float)r.NextDouble(); }

        static float Formant(float f, float centre, float bw) { float x = (f - centre) / bw; return 1f / (1f + x * x); }

        static AudioClip Giggle(System.Random rnd, int variant)
        {
            int syll = 5 + rnd.Next(5);
            float f0 = R(rnd, 430f, 560f);
            // child "ha": F1 ~1000, F2 ~1600, F3 ~3200; a few variants say "hee" (high F2) for the shriller ones
            bool hee = variant % 3 == 2;
            float F1 = hee ? 420f : R(rnd, 900f, 1100f), F2 = hee ? 2700f : R(rnd, 1450f, 1750f), F3 = R(rnd, 3000f, 3400f);
            float gap = R(rnd, 0.055f, 0.08f), len = R(rnd, 0.09f, 0.13f);
            float total = syll * (len + gap) + 0.6f;
            int N = Mathf.CeilToInt(total * Rate);
            var data = new float[N];
            double phase = 0;
            float noiseLp = 0f, noiseHp = 0f;
            float t0 = 0.02f;
            for (int k = 0; k < syll; k++)
            {
                float sLen = len * R(rnd, 0.85f, 1.15f);
                float pitch = f0 * (k < 2 ? 1f + k * 0.06f : Mathf.Pow(0.965f, k - 1)) * R(rnd, 0.97f, 1.03f);
                float amp = (k == 0 ? 0.75f : 1f) * Mathf.Pow(0.93f, k);
                int a = Mathf.RoundToInt(t0 * Rate), b = Mathf.Min(N, a + Mathf.RoundToInt((sLen + 0.03f) * Rate));
                for (int i = a; i < b; i++)
                {
                    float t = (i - a) / (float)Rate;
                    // the "h": breath before the voice comes in
                    float hEnv = Mathf.Clamp01(1f - Mathf.Abs(t - 0.015f) / 0.02f);
                    float white = (float)(rnd.NextDouble() * 2 - 1);
                    noiseLp += (white - noiseLp) * 0.55f; noiseHp = white - noiseLp * 0.6f;
                    float breath = noiseHp * hEnv * 0.25f;

                    float vt = t - 0.02f;
                    float vEnv = vt <= 0f ? 0f : Mathf.Clamp01(vt / 0.012f) * Mathf.Clamp01((sLen - vt) / 0.04f);
                    float f = pitch * (1f + 0.04f * Mathf.Sin(vt * 40f) - 0.08f * vt / sLen);   // each "ha" bends down
                    phase += 2.0 * Mathf.PI * f / Rate;
                    float voiced = 0f;
                    int H = Mathf.Min(24, (int)(5000f / f));
                    for (int h = 1; h <= H; h++)
                    {
                        float fh = f * h;
                        float env = Formant(fh, F1, 140f) + 0.7f * Formant(fh, F2, 200f) + 0.35f * Formant(fh, F3, 320f);
                        voiced += Mathf.Sin((float)(phase * h)) * env / Mathf.Pow(h, 0.6f);
                    }
                    data[i] += (voiced * 0.22f * vEnv + breath * (0.6f + vEnv * 0.4f)) * amp;
                }
                t0 += sLen + gap * R(rnd, 0.8f, 1.25f);
            }
            // a sharp intake of breath at the end: the nastiest bit
            {
                int a = Mathf.RoundToInt(t0 * Rate), b = Mathf.Min(N, a + Mathf.RoundToInt(0.28f * Rate));
                for (int i = a; i < b; i++)
                {
                    float t = (i - a) / (float)(b - a);
                    float white = (float)(rnd.NextDouble() * 2 - 1);
                    noiseLp += (white - noiseLp) * 0.35f;
                    data[i] += (white - noiseLp) * Mathf.Sin(t * Mathf.PI) * 0.12f;
                }
            }
            // the hall: three feedback echoes, a hollow classroom
            int[] taps = { 1123, 1597, 2269 };
            var outp = (float[])data.Clone();
            foreach (int d in taps)
                for (int i = d; i < N; i++) outp[i] += outp[i - d] * 0.28f;
            float peak = 0.001f;
            foreach (var x in outp) peak = Mathf.Max(peak, Mathf.Abs(x));
            for (int i = 0; i < N; i++) outp[i] = outp[i] / peak * 0.85f;
            var clip = AudioClip.Create("Giggle" + variant, N, 1, Rate, false);
            clip.SetData(outp, 0);
            return clip;
        }
    }
}
