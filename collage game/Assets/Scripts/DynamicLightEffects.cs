using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class DynamicLightEffects : MonoBehaviour
{
    public enum EffectType
    {
        Flicker,         // Fluorescent bulb hum and irregular flicker
        Pulse,           // Smooth breathing emergency beacon
        RotatingBeacon,  // Sweeping rotating alarm beacon
        ElectricalSpark  // Short circuit rapid sharp bursts
    }

    [Header("Effect Configuration")]
    [SerializeField] private EffectType effectType = EffectType.Flicker;
    public EffectType CurrentEffectType
    {
        get => effectType;
        set => effectType = value;
    }

    [Header("Intensity & Range")]
    public float baseIntensity = 3.0f;
    public float minIntensity = 0.2f;
    public float maxIntensity = 4.5f;
    public float speed = 12.0f;

    [Header("Flicker Tuning")]
    [Range(0f, 1f)] public float flickerRoughness = 0.65f;
    [Range(0f, 1f)] public float blackoutChance = 0.06f;

    [Header("Beacon Rotation")]
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 180.0f;

    [Header("Linked Visuals (Optional)")]
    public Renderer targetRenderer;
    public int materialIndex = 0;
    public string emissionColorProperty = "_EmissionColor";
    [ColorUsage(true, true)]
    public Color emissiveColor = Color.white;

    private Light targetLight;
    private MaterialPropertyBlock propBlock;
    private float noiseSeed;
    private float sparkTimer = 0f;
    private float currentSparkIntensity = 0f;

    void Awake()
    {
        targetLight = GetComponent<Light>();
        noiseSeed = Random.Range(0f, 1000f);
        propBlock = new MaterialPropertyBlock();
        if (targetLight != null && baseIntensity <= 0f)
        {
            baseIntensity = targetLight.intensity;
        }
    }

    void OnEnable()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorUpdate;
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorUpdate;
        }
#endif
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (!Application.isPlaying)
        {
            UpdateLighting((float)EditorApplication.timeSinceStartup);
        }
    }
#endif

    void Update()
    {
        if (Application.isPlaying)
        {
            UpdateLighting(Time.time);
        }
    }

    private void UpdateLighting(float time)
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
            if (targetLight == null) return;
        }

        float calculatedIntensity = baseIntensity;

        switch (effectType)
        {
            case EffectType.Flicker:
                float perlin = Mathf.PerlinNoise(noiseSeed, time * speed);
                float randomSpike = (Random.value < blackoutChance) ? 0.05f : 1.0f;
                float blend = Mathf.Lerp(1.0f, perlin, flickerRoughness) * randomSpike;
                calculatedIntensity = Mathf.Lerp(minIntensity, maxIntensity, blend);
                break;

            case EffectType.Pulse:
                float sinWave = (Mathf.Sin(time * speed) + 1.0f) * 0.5f;
                calculatedIntensity = Mathf.Lerp(minIntensity, maxIntensity, sinWave);
                break;

            case EffectType.RotatingBeacon:
                transform.Rotate(rotationAxis, rotationSpeed * (Application.isPlaying ? Time.deltaTime : 0.016f), Space.Self);
                float sweepWave = (Mathf.Sin(time * 3f) + 1.0f) * 0.5f;
                calculatedIntensity = Mathf.Lerp(baseIntensity * 0.85f, baseIntensity * 1.15f, sweepWave);
                break;

            case EffectType.ElectricalSpark:
                sparkTimer -= (Application.isPlaying ? Time.deltaTime : 0.016f);
                if (sparkTimer <= 0f)
                {
                    if (Random.value < 0.25f)
                    {
                        // Sudden burst of 2-5 sharp micro flashes
                        currentSparkIntensity = Random.Range(baseIntensity * 1.5f, maxIntensity);
                        sparkTimer = Random.Range(0.04f, 0.12f);
                    }
                    else
                    {
                        currentSparkIntensity = minIntensity;
                        sparkTimer = Random.Range(0.3f, 1.8f); // Quiet interval between sparks
                    }
                }
                calculatedIntensity = currentSparkIntensity;
                break;
        }

        targetLight.intensity = calculatedIntensity;

        // Synchronize emissive mesh renderer if present
        if (targetRenderer != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propBlock, materialIndex);

            float emissiveFactor = Mathf.Clamp01(calculatedIntensity / Mathf.Max(0.01f, maxIntensity));
            Color activeEmission = emissiveColor * (emissiveFactor * 3.0f);
            propBlock.SetColor(emissionColorProperty, activeEmission);
            targetRenderer.SetPropertyBlock(propBlock, materialIndex);
        }
    }
}
