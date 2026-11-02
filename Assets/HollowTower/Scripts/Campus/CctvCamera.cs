using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>The campus grid: the generator in the maintenance yard. The CCTV inside the administration building
    /// runs on its own backup battery; everything outdoors (cameras, floodlights, the gates) needs the generator.</summary>
    public static class CampusPower
    {
        public static bool GridOn { get { var o = CampusObjective.Instance; return o != null && o.generator != null && o.generator.On; } }
    }

    /// <summary>
    /// A security camera on a wall or pole. Seen from the control room's console (SurveillanceConsole) as a grainy
    /// infrared feed, and it spots monsters for the console's map: anything it can see shows up there. Indoor cameras
    /// run on the battery from the start; outdoor ones come alive with the generator.
    /// </summary>
    public class CctvCamera : MonoBehaviour
    {
        public static readonly List<CctvCamera> All = new List<CctvCamera>();

        public string label = "CAM";
        public bool outdoor;
        [Tooltip("How far it can make out a person (the giant shows from much further).")]
        public float range = 32f;
        public Camera view;
        public Renderer statusLed;

        public bool Live { get { return !outdoor || CampusPower.GridOn; } }
        public bool Upper { get { return !outdoor && transform.position.y > 4f; } }

        MaterialPropertyBlock block;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            if (view != null) view.enabled = false;
        }

        void Update()
        {
            if (statusLed == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            bool on = Live && Mathf.Repeat(Time.time + transform.position.x * 0.1f, 1.6f) < 0.8f;
            statusLed.GetPropertyBlock(block);
            block.SetColor(Gfx.EmissiveId, Gfx.ToNits(on ? new Color(1f, 0.08f, 0.05f) * 2f : Color.black));
            statusLed.SetPropertyBlock(block);
        }

        /// <summary>Renders the feed into rt, brightened like an infrared camera (ambient raised for this render only).</summary>
        public void RenderTo(RenderTexture rt)
        {
            if (view == null || rt == null) return;
            Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor;
            bool fog = RenderSettings.fog;
            float k = outdoor ? 4.5f : 6f;
            RenderSettings.ambientSkyColor = sky * k; RenderSettings.ambientEquatorColor = eq * k; RenderSettings.ambientGroundColor = gr * k;
            RenderSettings.fog = false;
            view.targetTexture = rt;
            view.Render();
            view.targetTexture = null;
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
            RenderSettings.fog = fog;
        }

        /// <summary>Whether p (a body's feet) is in its picture: in the field of view, in range, nothing in the way.</summary>
        public bool Sees(Vector3 p, float rangeScale = 1f, Transform ignore = null)
        {
            if (view == null || !Live) return false;
            Vector3 target = p + Vector3.up * 1.2f;
            Vector3 to = target - view.transform.position;
            float d = to.magnitude;
            if (d > range * rangeScale) return false;
            Vector3 vp = view.WorldToViewportPoint(target);
            if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.1f || vp.y > 1.1f) return false;
            foreach (var h in Physics.RaycastAll(view.transform.position, to / d, d - 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }
    }
}
