using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// A failing lamp: a slow uneven hum in brightness, quick stutters, and now and then it cuts out for a second or two
    /// before struggling back on. brightness scales the lamp's own intensity (a dim room's lamps run low); the tube
    /// renderer, if given, goes dark with the light.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class DyingLight : MonoBehaviour
    {
        public float brightness = 1f;
        [Tooltip("Chance that a flicker event is a full cut-out rather than a stutter.")]
        public float outChance = 0.3f;
        public Vector2 eventGap = new Vector2(2f, 7f);
        public Renderer tube;
        [Tooltip("Off: a healthy lamp, steady but for a faint hum.")]
        public bool failing = true;
        [Tooltip("Buzz on a stutter, pop on a cut-out, and a quiet hum loop while it's on.")]
        public bool sounds = true;
        public float soundVolume = 0.55f;

        Light lamp;
        MaterialPropertyBlock block;
        Color tubeEmission;
        bool hasEmission;
        AudioSource humSource;
        float baseIntensity = -1f, seed, nextEvent, outUntil, stutterUntil;

        void Start()
        {
            lamp = GetComponent<Light>();
            if (baseIntensity < 0f) baseIntensity = lamp.intensity;
            seed = Random.value * 100f;
            if (sounds && failing && brightness > 0f)
            {
                humSource = gameObject.AddComponent<AudioSource>();
                humSource.clip = LampSfx.Hum; humSource.loop = true; humSource.volume = 0f;
                humSource.spatialBlend = 1f; humSource.rolloffMode = AudioRolloffMode.Linear; humSource.minDistance = 1f; humSource.maxDistance = 7f;
                humSource.time = Random.value * 0.9f;
                humSource.Play();
            }
            if (tube != null && tube.sharedMaterial != null && tube.sharedMaterial.HasProperty("_EmissionColor"))
            {
                hasEmission = true;
                tubeEmission = tube.sharedMaterial.GetColor("_EmissionColor");
                block = new MaterialPropertyBlock();
            }
            nextEvent = Time.time + Random.Range(0.5f, eventGap.y);
        }

        void Update()
        {
            if (brightness <= 0f)   // a dead lamp (kept at zero here, so the light optimiser switching it on doesn't matter)
            {
                lamp.intensity = 0f;
                if (tube != null) tube.enabled = false;
                return;
            }
            float t = Time.time;
            if (!failing)
            {
                lamp.intensity = baseIntensity * brightness * (0.95f + 0.05f * Mathf.PerlinNoise(seed, t * 2f));
                return;
            }
            if (t >= nextEvent)
            {
                if (Random.value < outChance)
                {
                    outUntil = t + Random.Range(0.6f, 2.2f);
                    if (sounds) SfxBank.At(LampSfx.Pop, transform.position, soundVolume, Random.Range(0.85f, 1.15f), 16f);
                }
                else
                {
                    stutterUntil = t + Random.Range(0.2f, 0.7f);
                    if (sounds) SfxBank.At(LampSfx.Buzz, transform.position, soundVolume, Random.Range(0.9f, 1.1f), 14f);
                }
                nextEvent = t + Random.Range(eventGap.x, eventGap.y);
            }

            float k;
            if (t < outUntil) k = Random.value < 0.04f ? 0.6f : 0f;            // out, with the odd spark
            else if (t < stutterUntil) k = Random.value < 0.5f ? 0.1f : 1.1f;  // stuttering
            else k = 0.75f + 0.25f * Mathf.PerlinNoise(seed, t * 1.7f);          // uneven hum
            lamp.intensity = baseIntensity * brightness * k;
            if (humSource != null) humSource.volume = k > 0.05f ? soundVolume * 0.25f : 0f;
            if (tube != null)
            {
                tube.enabled = k > 0.05f;
                if (hasEmission)   // the glowing tube dims and flickers with its light
                {
                    if (block == null) block = new MaterialPropertyBlock();   // lost on a script reload in play mode
                    tube.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", tubeEmission * (brightness * k));
                    tube.SetPropertyBlock(block);
                }
            }
        }
    }
}
