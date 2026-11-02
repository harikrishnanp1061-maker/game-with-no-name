using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Hides the end of the camera's view distance (TowerFloorCulling keeps it short for speed): far things sink into
    /// the fog, and past the far plane there is fog-coloured dark instead of the default blue sky. A level without fog
    /// (the tutorial) gets a near-black one, which also keeps its rooms dark beyond the lamps. Added by PlayerParty.
    /// </summary>
    public class DistanceFade : MonoBehaviour
    {
        [Tooltip("How much of the scene is still visible at the far plane: the fog is made at least this thick.")]
        public float leftAtFarPlane = 0.12f;

        /// <summary>Set by a level that has a sky to show (the campus at night): keep the skybox behind the fog.</summary>
        public static bool KeepSky;

        Camera cam;
        float baseDensity;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (!RenderSettings.fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.015f);
                RenderSettings.fogDensity = 0f;
            }
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            baseDensity = RenderSettings.fogDensity;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (KeepSky) cam.clearFlags = CameraClearFlags.Skybox;
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
            }
            // exp2 fog: visible = exp(-(density * d)^2), so this density leaves leftAtFarPlane at the far plane
            float need = Mathf.Sqrt(-Mathf.Log(leftAtFarPlane)) / Mathf.Max(1f, cam.farClipPlane);
            RenderSettings.fogDensity = Mathf.Max(baseDensity, need);
        }
    }
}
