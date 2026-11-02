using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The level 3 sounds nobody recorded, synthesised once and cached: a car alarm's whoop, breaking glass, an old
    /// office phone's bell, walkie-talkie static and its squelch beeps, a relay clunk, a generator's backfire and the
    /// tick of a gauge needle. Small and procedural, like the puzzle siren.
    /// </summary>
    public static class CampusSynth
    {
        const int Rate = 44100;
        static AudioClip alarm, glass, ring, staticHiss, squelchOn, squelchOff, relay, backfire, tick, buzz;

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f((float)i / Rate), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static float Noise(int seed, float t) { return Mathf.PerlinNoise(t * 2200f, seed) * 2f - 1f; }
        static readonly System.Random rnd = new System.Random(7);
        static float White() { return (float)rnd.NextDouble() * 2f - 1f; }

        /// <summary>A car alarm cycle (rising whoop, then fast chirps), 4 s, made to loop.</summary>
        public static AudioClip CarAlarm
        {
            get
            {
                if (alarm != null) return alarm;
                float ph = 0f;
                alarm = Make("CarAlarm", 4f, t =>
                {
                    float f;
                    if (t < 2f) f = 700f + 900f * Mathf.Repeat(t, 0.5f) / 0.5f;            // whoop whoop whoop whoop
                    else f = Mathf.Repeat(t, 0.25f) < 0.125f ? 1500f : 900f;               // hi-lo chirps
                    ph += f / Rate;
                    float sq = Mathf.Repeat(ph, 1f) < 0.5f ? 1f : -1f;
                    return (sq * 0.35f + Mathf.Sin(ph * 6.283f) * 0.25f);
                });
                return alarm;
            }
        }

        public static AudioClip Glass
        {
            get
            {
                if (glass != null) return glass;
                glass = Make("GlassSmash", 1.1f, t =>
                {
                    float env = Mathf.Exp(-t * 5f);
                    float tinkle = 0f;
                    for (int k = 0; k < 6; k++) tinkle += Mathf.Sin(t * (3100f + k * 917f) * 6.283f) * Mathf.Exp(-Mathf.Repeat(t + k * 0.13f, 0.4f) * 18f);
                    return White() * env * 0.7f + tinkle * 0.08f * env;
                });
                return glass;
            }
        }

        /// <summary>An old office phone: two bursts of bell, then silence (3 s, loops).</summary>
        public static AudioClip PhoneRing
        {
            get
            {
                if (ring != null) return ring;
                ring = Make("PhoneRing", 3f, t =>
                {
                    bool on = t < 0.4f || (t > 0.6f && t < 1.0f);
                    if (!on) return 0f;
                    float trem = Mathf.Repeat(t, 0.05f) < 0.025f ? 1f : 0.3f;              // the clapper hammering
                    return (Mathf.Sin(t * 1100f * 6.283f) * 0.4f + Mathf.Sin(t * 1650f * 6.283f) * 0.25f) * trem;
                });
                return ring;
            }
        }

        public static AudioClip Static
        {
            get
            {
                if (staticHiss != null) return staticHiss;
                staticHiss = Make("RadioStatic", 2f, t => White() * 0.25f * (0.8f + 0.2f * Mathf.Sin(t * 13f)));
                return staticHiss;
            }
        }

        /// <summary>Key-up chirp (push to talk) and the roger beep when a transmission ends.</summary>
        public static AudioClip SquelchOn { get { return squelchOn ?? (squelchOn = Make("SquelchOn", 0.18f, t => (Mathf.Sin(t * 1800f * 6.283f) * 0.3f + White() * 0.2f) * (1f - t / 0.18f))); } }
        public static AudioClip SquelchOff { get { return squelchOff ?? (squelchOff = Make("SquelchOff", 0.32f, t => t < 0.1f ? Mathf.Sin(t * 1200f * 6.283f) * 0.3f : White() * 0.3f * (1f - (t - 0.1f) / 0.22f))); } }

        public static AudioClip Relay
        {
            get
            {
                return relay ?? (relay = Make("Relay", 0.35f, t =>
                    (White() * 0.8f + Mathf.Sin(t * 90f * 6.283f) * 0.6f) * Mathf.Exp(-t * 30f) + Mathf.Sin(t * 120f * 6.283f) * 0.15f * Mathf.Exp(-t * 6f)));
            }
        }

        /// <summary>A flooded engine coughing: a sharp bang and a rumble.</summary>
        public static AudioClip Backfire
        {
            get
            {
                return backfire ?? (backfire = Make("Backfire", 1.4f, t =>
                    White() * Mathf.Exp(-t * 14f) + Mathf.Sin(t * 55f * 6.283f) * 0.6f * Mathf.Exp(-t * 3f)));
            }
        }

        public static AudioClip Tick { get { return tick ?? (tick = Make("Tick", 0.04f, t => White() * Mathf.Exp(-t * 160f) * 0.6f)); } }

        /// <summary>Mains hum and the buzz of tubes warming up, looped.</summary>
        public static AudioClip Buzz { get { return buzz ?? (buzz = Make("Buzz", 1f, t => Mathf.Sin(t * 100f * 6.283f) * 0.2f + Mathf.Sin(t * 200f * 6.283f) * 0.1f + White() * 0.03f)); } }

        /// <summary>A 3D looping source on go (stopped; call Play).</summary>
        public static AudioSource Loop(GameObject go, AudioClip clip, float volume, float maxDistance)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 1f; s.volume = volume;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = maxDistance; s.dopplerLevel = 0f;
            return s;
        }
    }

    /// <summary>
    /// Every distraction in the campus goes through here: a car alarm, a phone rung from the control room, a bank of
    /// lights thrown on, a generator backfiring. On the host (where the monsters think) the giant walks over to look if
    /// it is outdoors and in range (a car alarm makes it stomp the car), and every Nailed within earshot investigates.
    /// </summary>
    public static class CampusLure
    {
        public enum Kind { CarAlarm, Phone, Lights, Backfire, Siren }

        public static event System.Action<Vector3, float, Kind> Heard;

        /// <param name="giantRange">How far the giant notices it (0: it doesn't).</param>
        /// <param name="nailedRange">How far a Nailed hears / sees it.</param>
        public static void Emit(Vector3 pos, Kind kind, float giantRange, float nailedRange, CarAlarm car = null)
        {
            if (!Net.Net.IsAuthority) return;
            var g = CampusGiant.Instance;
            if (g != null && giantRange > 0f && !CampusGiant.Indoors(pos))
            {
                Vector3 d = g.transform.position - pos; d.y = 0f;
                if (d.magnitude < giantRange) g.Lure(pos, car);
            }
            if (nailedRange > 0f) NailedMonster.HearAt(pos, nailedRange, kind == Kind.Lights ? 2 : 1);
            if (Heard != null) Heard(pos, nailedRange, kind);
        }
    }
}
