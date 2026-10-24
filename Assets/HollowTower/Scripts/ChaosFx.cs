using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HollowTower
{
    /// <summary>
    /// Floor 2's chaos on screen (PartyFloor adds it). While the music is dead and you are on the floor: a pulsing red
    /// vignette, colour drained, chromatic split, film grain and a lens throb in time with a heartbeat, and the camera
    /// shakes - a little all the time, hard when the music dies or a guest makes a kill, and more the closer a hunting
    /// guest gets (the heartbeat speeds up with it). Kick() adds a one-off jolt.
    /// </summary>
    public class ChaosFx : MonoBehaviour
    {
        static float kick;

        Volume vol;
        Vignette vig;
        ChromaticAberration ca;
        LensDistortion lens;
        FilmGrain grain;
        ColorAdjustments grade;
        AudioSource heart;
        Transform shaken;
        Vector3 lastPos;
        Quaternion lastRot = Quaternion.identity;
        float beat, glitchUntil, nextGlitch;

        /// <summary>A jolt of shake and colour (0..~1.5): the music dying, a kill nearby.</summary>
        public static void Kick(float amount) { kick = Mathf.Max(kick, amount); }

        void Start()
        {
            var go = new GameObject("Chaos FX (volume)");
            go.transform.SetParent(transform, false);
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 60f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;
            vig = p.Add<Vignette>(true);
            vig.color.Override(new Color(0.5f, 0f, 0.02f));
            vig.smoothness.Override(0.55f);
            vig.intensity.Override(0f);
            ca = p.Add<ChromaticAberration>(true);
            ca.intensity.Override(0f);
            lens = p.Add<LensDistortion>(true);
            lens.intensity.Override(0f);
            grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium4);
            grain.intensity.Override(0f);
            grade = p.Add<ColorAdjustments>(true);
            grade.saturation.Override(0f);
            grade.hueShift.Override(0f);
            grade.postExposure.Override(1f);   // the URP look's base exposure; raised a little in the red so the room stays readable
            vol.weight = 0f;

            heart = gameObject.AddComponent<AudioSource>();
            heart.loop = true; heart.spatialBlend = 0f; heart.volume = 0f; heart.playOnAwake = false;
            PartyFloor.ChaosStarted += OnChaos;
        }

        void OnDestroy() { PartyFloor.ChaosStarted -= OnChaos; }

        void OnChaos() { if (PartyFloor.Instance != null && PartyFloor.Instance.PlayerOnFloor) Kick(1f); }

        void Update()
        {
            var floor = PartyFloor.Instance;
            float chaos = floor != null && floor.PlayerOnFloor ? floor.RedMix : 0f;
            float near = Closeness();
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 1.4f);
            float k = Mathf.Clamp01(kick);

            // the heartbeat quickens as they get close
            float bpm = Mathf.Lerp(70f, 150f, near);
            beat += Time.deltaTime * bpm / 60f;
            float thump = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(beat * Mathf.PI * 2f)), 12f);

            if (chaos > 0.02f && Time.time > nextGlitch) { glitchUntil = Time.time + Random.Range(0.05f, 0.14f); nextGlitch = Time.time + Random.Range(1.5f, 4.5f); }
            bool glitch = Time.time < glitchUntil && chaos > 0.5f;

            float w = Mathf.Max(chaos, k);
            vol.weight = Mathf.MoveTowards(vol.weight, w > 0.01f ? 1f : 0f, Time.deltaTime * 3f);
            vig.intensity.value = chaos * (0.22f + 0.12f * thump + 0.2f * near) + k * 0.2f;
            ca.intensity.value = chaos * (0.25f + 0.5f * near) + k * 0.6f + (glitch ? 0.8f : 0f);
            lens.intensity.value = -(chaos * thump * (0.06f + 0.12f * near)) - k * 0.15f + (glitch ? 0.25f : 0f);
            grain.intensity.value = chaos * 0.55f;
            grade.saturation.value = -chaos * (10f + 25f * near);   // keep the red readable: you need to find a hiding spot
            grade.hueShift.value = glitch ? Random.Range(-40f, 40f) : 0f;
            grade.postExposure.value = 1f + chaos * 0.6f;

            var bank = SfxBank.Instance;
            if (heart.clip == null && bank != null) heart.clip = bank.heartbeat;
            float hv = chaos * near * 0.9f;
            heart.volume = Mathf.MoveTowards(heart.volume, hv, Time.deltaTime);
            heart.pitch = Mathf.Lerp(0.9f, 1.35f, near);
            if (heart.clip != null)
            {
                if (heart.volume > 0.01f && !heart.isPlaying) heart.Play();
                else if (heart.volume <= 0.01f && heart.isPlaying) heart.Stop();
            }
        }

        // how close the nearest hunting guest is to the view (1 = on top of you, 0 = 14 m or more)
        float Closeness()
        {
            var view = PlayerParty.Active;
            if (view == null) return 0f;
            float best = 14f;
            foreach (var m in PartyMonster.All)
                if (m != null && m.Hunting) best = Mathf.Min(best, Vector3.Distance(m.transform.position, view.position));
            return 1f - best / 14f;
        }

        void LateUpdate()
        {
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera : Camera.main;
            if (cam == null) return;
            var t = cam.transform;
            if (t != shaken)
            {
                shaken = t;
                lastPos = Vector3.zero; lastRot = Quaternion.identity;
            }
            else
            {
                // take back last frame's shake before adding this frame's (the camera's pose is its parent's business)
                t.localPosition -= lastPos;
                t.localRotation = t.localRotation * Quaternion.Inverse(lastRot);
            }

            var floor = PartyFloor.Instance;
            float chaos = floor != null && floor.PlayerOnFloor ? floor.RedMix : 0f;
            float near = Closeness();
            float amp = chaos * (0.15f + 0.85f * near * near) + Mathf.Clamp01(kick) * 1.2f;
            if (amp < 0.001f) { lastPos = Vector3.zero; lastRot = Quaternion.identity; return; }
            float s = Time.time * 22f;
            lastPos = new Vector3(Mathf.PerlinNoise(s, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, s) - 0.5f, 0f) * 0.05f * amp;
            lastRot = Quaternion.Euler((Mathf.PerlinNoise(s * 0.8f, 5.1f) - 0.5f) * 2.4f * amp, (Mathf.PerlinNoise(9.2f, s * 0.8f) - 0.5f) * 2.4f * amp, (Mathf.PerlinNoise(s * 0.6f, 2.2f) - 0.5f) * 3f * amp);
            t.localPosition += lastPos;
            t.localRotation = t.localRotation * lastRot;
        }
    }
}
