using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// One of the campus's lights: a lamp post, a ceiling fitting, a beacon. Its glowing head (emission) always shows,
    /// so the lights read across the whole campus at night, but its real Light is only switched on by CampusLightCuller
    /// when it is near the camera. Unpowered lamps (the ones on the generator) stay dark until it starts; some flicker.
    /// </summary>
    public class CampusLamp : MonoBehaviour
    {
        public static readonly List<CampusLamp> All = new List<CampusLamp>();

        public Light lightSource;
        public Renderer[] heads;
        public Color color = new Color(1f, 0.75f, 0.45f);
        public float headGlow = 2.2f;
        public bool powered = true;
        [Range(0f, 1f)] public float flicker;
        [Tooltip("Never reaches the light culler's budget: always lit when near (beacons).")]
        public bool important;

        public bool Wanted { get; set; }
        /// <summary>How far the light is faded in (0..1): the culler's choices fade rather than pop as players move.</summary>
        public float Level { get; private set; }
        const float FadeTime = 0.6f;
        float baseIntensity, seed, nextFlick, flickUntil;
        bool lastShown = true, lastPowered;
        MaterialPropertyBlock block;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            if (lightSource != null) { baseIntensity = lightSource.intensity; lightSource.enabled = false; }
            seed = Random.value * 100f;
            lastPowered = !powered;
        }

        public void SetColor(Color c)
        {
            color = c;
            if (lightSource != null) lightSource.color = c;
            lastPowered = !powered;   // repaint the heads
        }

        void Update()
        {
            bool shown = powered;
            if (powered && flicker > 0f)
            {
                if (Time.time > nextFlick) { nextFlick = Time.time + Random.Range(0.3f, 4f) / flicker; flickUntil = Time.time + Random.Range(0.04f, 0.35f) * flicker; }
                shown = Time.time > flickUntil;
            }
            if (lightSource != null)
            {
                Level = Mathf.MoveTowards(Level, Wanted && powered ? 1f : 0f, Time.deltaTime / FadeTime);
                bool on = shown && Level > 0.001f;
                if (lightSource.enabled != on) lightSource.enabled = on;
                if (on)
                {
                    float k = Mathf.SmoothStep(0f, 1f, Level);
                    if (flicker > 0f) k *= 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, seed);
                    lightSource.intensity = baseIntensity * k;
                }
            }
            if (shown != lastShown || powered != lastPowered)
            {
                lastShown = shown; lastPowered = powered;
                if (heads == null) return;
                if (block == null) block = new MaterialPropertyBlock();
                foreach (var r in heads)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(Gfx.EmissiveId, Gfx.ToNits(shown ? color * headGlow : color * 0.02f));
                    r.SetPropertyBlock(block);
                }
            }
        }
    }
}
