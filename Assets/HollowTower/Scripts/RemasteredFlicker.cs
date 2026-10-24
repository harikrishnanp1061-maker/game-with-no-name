using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Subtle atmospheric horror light flickering.
    /// Simulates aged ballast fluorescent tube / failing incandescent bulb flicker.
    /// </summary>
    public class RemasteredFlicker : MonoBehaviour
    {
        private Light targetLight;
        private Renderer tubeRenderer;
        private float baseIntensity;
        private float nextFlickerTime = 0f;

        public void Setup(Light light, Renderer tubeR)
        {
            targetLight = light;
            tubeRenderer = tubeR;
            if (targetLight != null) baseIntensity = targetLight.intensity;
            nextFlickerTime = Time.time + Random.Range(2f, 8f);
        }

        void Start()
        {
            if (targetLight == null) targetLight = GetComponent<Light>();
            if (targetLight != null && baseIntensity <= 0f) baseIntensity = targetLight.intensity;
            if (nextFlickerTime <= 0f) nextFlickerTime = Time.time + Random.Range(1f, 5f);
        }

        void Update()
        {
            if (targetLight == null || !targetLight.enabled) return;

            if (Time.time >= nextFlickerTime)
            {
                // Trigger a burst of 1-3 micro-dips
                float dip = Random.Range(0.1f, 0.45f);
                targetLight.intensity = baseIntensity * dip;

                nextFlickerTime = Time.time + Random.Range(0.04f, 0.15f);
                if (Random.value < 0.25f)
                {
                    nextFlickerTime = Time.time + Random.Range(2f, 9f);
                    targetLight.intensity = baseIntensity;
                }
            }
        }
    }
}
