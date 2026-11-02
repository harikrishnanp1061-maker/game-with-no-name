using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Fixes found in the SFX audit (2026-10-31), written as new WAVs in Assets/Audio/SFX/Fixed (the originals stay):
    ///   breathing    Player_Breathing_Exhausted was ~26 dB quieter than everything else (peak 0.05): inaudible panting.
    ///   itemPickup   Item_Pickup_Equip peaked at 0.17: barely there, and it is played at 0.35-0.8 volume.
    ///   fallImpact   Player_Pit_Fall_Impact.mp3 was a byte-for-byte copy of the fall scream: a synthesised body thud.
    ///   deathGasp    was the same 10.8 s ghost breath as the monster attack (both fire on a guest kill): a short human
    ///                gasp made from the wake-up breath, slowed and darkened.
    ///   monsterAttack  the 10.8 s breath as a one-shot per swipe: cut to its first 3 s with a fade.
    /// "Repair SFX" builds the files (safe to run again) and points every SfxBank in the open scene at them.
    /// </summary>
    public static class SfxRepair
    {
        const string Src = "Assets/Audio/SFX/";
        const string Out = "Assets/Audio/SFX/Fixed/";
        public const string Breathing = Out + "Player_Breathing_Exhausted_Fixed.wav";
        public const string Pickup = Out + "Item_Pickup_Equip_Fixed.wav";
        public const string Impact = Out + "Player_Pit_Fall_Impact_Fixed.wav";
        public const string Gasp = Out + "Player_Death_Gasp.wav";
        public const string Attack = Out + "Monster_Attack_GhostBreath_Short.wav";

        [MenuItem("Hollow Tower/Audio/Repair SFX")]
        public static void RepairMenu() { Debug.Log("[SfxRepair] " + Repair()); }

        public static string Repair()
        {
            BuildFiles();
            int n = 0;
            foreach (var bank in Object.FindObjectsByType<SfxBank>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assign(bank);
                EditorUtility.SetDirty(bank);
                n++;
            }
            if (n > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return "fixed clips built; " + n + " SfxBank(s) updated";
        }

        /// <summary>Points a bank at the repaired clips (the builders call this too).</summary>
        public static void Assign(SfxBank bank)
        {
            bank.breathing = Load(Breathing) ?? bank.breathing;
            bank.itemPickup = Load(Pickup) ?? bank.itemPickup;
            bank.fallImpact = Load(Impact) ?? bank.fallImpact;
            bank.deathGasp = Load(Gasp) ?? bank.deathGasp;
            bank.monsterAttack = Load(Attack) ?? bank.monsterAttack;
        }

        static AudioClip Load(string p) { return AssetDatabase.LoadAssetAtPath<AudioClip>(p); }

        public static void BuildFiles()
        {
            if (!AssetDatabase.IsValidFolder(Out.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Audio/SFX", "Fixed");
            int rate;
            int ch;
            float[] d;

            d = Read(Src + "Player/Player_Breathing_Exhausted.mp3", out rate, out ch);
            if (d != null) Write(Breathing, Normalise(d, 0.75f), rate, ch);

            d = Read(Src + "Items/Item_Pickup_Equip.mp3", out rate, out ch);
            if (d != null) Write(Pickup, Normalise(d, 0.85f), rate, ch);

            Write(Impact, Normalise(Thud(44100), 0.9f), 44100, 1);

            d = Read("Assets/Tutorial/Audio/SFX/Player/Player_WakeUp_Breath.wav", out rate, out ch);
            if (d != null)
            {
                d = Resample(d, ch, 0.82f);                       // slower and lower: a dying breath, not a waking one
                Fade(d, ch, rate, 0.01f, 0.45f);
                Write(Gasp, Normalise(d, 0.85f), rate, ch);
            }

            d = Read(Src + "Monster/Monster_Attack_GhostBreath.mp3", out rate, out ch);
            if (d != null)
            {
                int keep = Mathf.Min(d.Length, Mathf.RoundToInt(3.0f * rate) * ch);
                var cut = new float[keep];
                System.Array.Copy(d, cut, keep);
                Fade(cut, ch, rate, 0f, 0.7f);
                Write(Attack, cut, rate, ch);
            }

            AssetDatabase.Refresh();
            foreach (var p in new[] { Breathing, Pickup, Impact, Gasp, Attack })
            {
                var imp = AssetImporter.GetAtPath(p) as AudioImporter;
                if (imp == null) continue;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ audio helpers
        static float[] Read(string path, out int rate, out int channels)
        {
            rate = 44100; channels = 1;
            var c = Load(path);
            if (c == null) { Debug.LogWarning("[SfxRepair] missing " + path); return null; }
            c.LoadAudioData();
            var d = new float[c.samples * c.channels];
            if (!c.GetData(d, 0)) { Debug.LogWarning("[SfxRepair] can't read " + path + " (set it to Decompress On Load)"); return null; }
            rate = c.frequency; channels = c.channels;
            return d;
        }

        static float[] Normalise(float[] d, float peak)
        {
            float m = 1e-5f;
            foreach (var x in d) m = Mathf.Max(m, Mathf.Abs(x));
            float g = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            return d;
        }

        static void Fade(float[] d, int ch, int rate, float inSec, float outSec)
        {
            int frames = d.Length / ch, fin = Mathf.RoundToInt(inSec * rate), fout = Mathf.RoundToInt(outSec * rate);
            for (int f = 0; f < frames; f++)
            {
                float g = 1f;
                if (fin > 0 && f < fin) g = f / (float)fin;
                if (fout > 0 && f > frames - fout) g *= (frames - f) / (float)fout;
                for (int c = 0; c < ch; c++) d[f * ch + c] *= g;
            }
        }

        static float[] Resample(float[] d, int ch, float speed)
        {
            int frames = d.Length / ch, outFrames = Mathf.RoundToInt(frames / speed);
            var o = new float[outFrames * ch];
            for (int f = 0; f < outFrames; f++)
            {
                float s = f * speed;
                int i = Mathf.Min((int)s, frames - 2);
                float k = s - i;
                for (int c = 0; c < ch; c++) o[f * ch + c] = Mathf.Lerp(d[i * ch + c], d[(i + 1) * ch + c], k);
            }
            return o;
        }

        /// <summary>A body hitting a concrete floor from a height: a deep thump, a short crunch, a little settle.</summary>
        static float[] Thud(int rate)
        {
            var rnd = new System.Random(77);
            int n = Mathf.RoundToInt(0.9f * rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f0 = Mathf.Lerp(62f, 34f, Mathf.Clamp01(t / 0.35f));
                phase += f0 / rate;
                float thump = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 7.5f);
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                lp += (noise - lp) * 0.18f;                      // dull crunch
                lp2 += (lp - lp2) * 0.35f;
                float crunch = lp2 * 2.2f * Mathf.Exp(-t * 28f);
                float settle = t > 0.12f ? lp * 0.5f * Mathf.Exp(-(t - 0.12f) * 9f) * (Mathf.PerlinNoise(t * 40f, 3f) > 0.6f ? 1f : 0.2f) : 0f;
                d[i] = thump * 0.9f + crunch + settle;
            }
            return d;
        }

        static void Write(string assetPath, float[] d, int rate, int ch)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            using (var w = new BinaryWriter(File.Create(full)))
            {
                int bytes = d.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch);
                w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var x in d) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(x, -1f, 1f) * 32767f));
            }
        }
    }
}
