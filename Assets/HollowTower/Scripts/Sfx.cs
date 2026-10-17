using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Procedurally generated sound effects (no audio files needed).
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static AudioClip gunshot, groan, roar, heartbeat, whisper, drone, pickup, ding, hurt, click, scream, step, bark, howl;
        static System.Random rnd = new System.Random(1234);

        static float Noise() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                data[i] = Mathf.Clamp(f(t, t / seconds), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip Gunshot
        {
            get
            {
                if (gunshot == null)
                {
                    float lp = 0f;
                    gunshot = Make("gunshot", 0.35f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.35f);
                        float env = Mathf.Exp(-t * 22f);
                        float thump = Mathf.Sin(t * 2f * Mathf.PI * (90f - t * 120f)) * Mathf.Exp(-t * 30f);
                        return (lp * 0.9f + thump * 0.8f) * env * 1.2f;
                    });
                }
                return gunshot;
            }
        }

        public static AudioClip Groan
        {
            get
            {
                if (groan == null)
                {
                    float lp = 0f;
                    groan = Make("groan", 1.6f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.05f);
                        float f0 = 70f + Mathf.Sin(t * 3.1f) * 12f;
                        float saw = ((t * f0) % 1f) * 2f - 1f;
                        float env = Mathf.Sin(k * Mathf.PI);
                        return (saw * 0.35f + lp * 1.4f) * env * 0.6f;
                    });
                }
                return groan;
            }
        }

        public static AudioClip Roar
        {
            get
            {
                if (roar == null)
                {
                    float lp = 0f;
                    roar = Make("roar", 1.4f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.12f);
                        float f0 = 55f + Mathf.Sin(t * 9f) * 8f;
                        float saw = ((t * f0) % 1f) * 2f - 1f;
                        float env = Mathf.Clamp01(t * 8f) * Mathf.Exp(-k * 2f);
                        return (saw * 0.6f + lp * 1.2f) * env;
                    });
                }
                return roar;
            }
        }

        public static AudioClip Heartbeat
        {
            get
            {
                if (heartbeat == null)
                {
                    heartbeat = Make("heartbeat", 0.9f, (t, k) =>
                    {
                        float b1 = Mathf.Sin(t * 2f * Mathf.PI * 50f) * Mathf.Exp(-t * 25f);
                        float t2 = Mathf.Max(0f, t - 0.22f);
                        float b2 = t > 0.22f ? Mathf.Sin(t2 * 2f * Mathf.PI * 45f) * Mathf.Exp(-t2 * 25f) * 0.7f : 0f;
                        return (b1 + b2) * 0.9f;
                    });
                }
                return heartbeat;
            }
        }

        public static AudioClip Whisper
        {
            get
            {
                if (whisper == null)
                {
                    float lp = 0f, hp = 0f;
                    whisper = Make("whisper", 2.4f, (t, k) =>
                    {
                        float n = Noise();
                        lp = Mathf.Lerp(lp, n, 0.5f);
                        hp = n - lp;
                        float trem = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * (3f + Mathf.Sin(t * 1.3f) * 2f));
                        float tone = Mathf.Sin(t * 2f * Mathf.PI * (420f + Mathf.Sin(t * 0.7f) * 60f)) * 0.08f;
                        return (hp * 0.5f * trem + tone) * Mathf.Sin(k * Mathf.PI);
                    });
                }
                return whisper;
            }
        }

        public static AudioClip Drone
        {
            get
            {
                if (drone == null)
                {
                    float lp = 0f;
                    // 8 seconds and whole-cycle frequencies so the loop is seamless.
                    drone = Make("drone", 8f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.01f);
                        float a = Mathf.Sin(t * 2f * Mathf.PI * 41f) * 0.25f;
                        float b = Mathf.Sin(t * 2f * Mathf.PI * 61.5f) * 0.15f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 0.25f));
                        float c = Mathf.Sin(t * 2f * Mathf.PI * 82f) * 0.07f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 0.125f));
                        return (a + b + c + lp * 1.5f) * 0.5f;
                    });
                }
                return drone;
            }
        }

        public static AudioClip Pickup
        {
            get
            {
                if (pickup == null)
                    pickup = Make("pickup", 0.5f, (t, k) =>
                        (Mathf.Sin(t * 2f * Mathf.PI * 660f) + Mathf.Sin(t * 2f * Mathf.PI * (t > 0.12f ? 990f : 660f))) * 0.25f * Mathf.Exp(-t * 6f));
                return pickup;
            }
        }

        public static AudioClip Ding
        {
            get
            {
                if (ding == null)
                    ding = Make("ding", 2.2f, (t, k) =>
                        (Mathf.Sin(t * 2f * Mathf.PI * 880f) * 0.4f + Mathf.Sin(t * 2f * Mathf.PI * 1318f) * 0.2f) * Mathf.Exp(-t * 2.2f));
                return ding;
            }
        }

        public static AudioClip Hurt
        {
            get
            {
                if (hurt == null)
                {
                    float lp = 0f;
                    hurt = Make("hurt", 0.35f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.2f);
                        return (lp + Mathf.Sin(t * 2f * Mathf.PI * 140f) * 0.4f) * Mathf.Exp(-t * 9f);
                    });
                }
                return hurt;
            }
        }

        public static AudioClip Click
        {
            get
            {
                if (click == null)
                    click = Make("click", 0.08f, (t, k) => Noise() * Mathf.Exp(-t * 120f) * 0.7f);
                return click;
            }
        }

        public static AudioClip Scream
        {
            get
            {
                if (scream == null)
                {
                    float lp = 0f;
                    scream = Make("scream", 1.2f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.6f);
                        float f0 = 700f + Mathf.Sin(t * 30f) * 80f - t * 250f;
                        return (Mathf.Sin(t * 2f * Mathf.PI * f0) * 0.3f + lp * 0.3f) * Mathf.Sin(k * Mathf.PI);
                    });
                }
                return scream;
            }
        }

        public static AudioClip Step
        {
            get
            {
                if (step == null)
                {
                    float lp = 0f;
                    step = Make("step", 0.12f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.15f);
                        return lp * Mathf.Exp(-t * 45f) * 1.5f;
                    });
                }
                return step;
            }
        }

        /// <summary>The zombie dog: a short, ragged, throaty bark.</summary>
        public static AudioClip Bark
        {
            get
            {
                if (bark == null)
                {
                    float lp = 0f, ph = 0f;
                    bark = Make("bark", 0.32f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.3f);
                        float f0 = 420f - k * 200f + Noise() * 25f;
                        ph += f0 / Rate;
                        float buzz = ((ph % 1f) * 2f - 1f) * 0.5f + Mathf.Sin(ph * 2f * Mathf.PI) * 0.5f;
                        float env = Mathf.Clamp01(t * 60f) * Mathf.Exp(-k * 4.5f);
                        return (buzz * 0.55f + lp * 0.7f) * env * 1.2f;
                    });
                }
                return bark;
            }
        }

        /// <summary>The zombie dog calling the pack: a long, wavering, broken howl.</summary>
        public static AudioClip Howl
        {
            get
            {
                if (howl == null)
                {
                    float lp = 0f, ph = 0f;
                    howl = Make("howl", 2.2f, (t, k) =>
                    {
                        lp = Mathf.Lerp(lp, Noise(), 0.08f);
                        float f0 = 260f + 300f * Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI * 0.5f) - 140f * Mathf.Max(0f, k - 0.6f) + Mathf.Sin(t * 37f) * 14f;
                        ph += f0 / Rate;
                        float tone = Mathf.Sin(ph * 2f * Mathf.PI) * 0.6f + Mathf.Sin(ph * 4f * Mathf.PI) * 0.25f + ((ph % 1f) * 2f - 1f) * 0.12f;
                        float env = Mathf.Clamp01(t * 5f) * Mathf.Clamp01((1f - k) * 4f);
                        return (tone * 0.6f + lp * 0.5f) * env;
                    });
                }
                return howl;
            }
        }

        /// <summary>Plays a one-shot 3D sound at a world position.</summary>
        public static void At(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 40f)
        {
            var g = new GameObject("Sfx_" + clip.name);
            g.transform.position = pos;
            var s = g.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * 1.15f); // everything a touch louder
            s.pitch = pitch;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = maxDistance;
            s.Play();
            Object.Destroy(g, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }
    }
}
