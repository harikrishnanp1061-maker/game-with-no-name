using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// Keeps the night affordable: of all the campus lamps, only the nearest few (within range of the camera) get a
    /// real Light; the rest show just their glowing heads. Runs a few times a second.
    /// </summary>
    public class CampusLightCuller : MonoBehaviour
    {
        public int budget = 40;
        [Tooltip("Already-lit lamps stay lit until this many places past the budget (hysteresis).")]
        public int keepExtra = 6;
        public float range = 65f;
        /// <summary>A second place that needs its lamps (the CCTV feed the control room is watching), or null.</summary>
        public static Vector3? Focus;
        public int focusBudget = 14;
        float next;
        readonly List<CampusLamp> near = new List<CampusLamp>();
        readonly List<CampusLamp> sorted = new List<CampusLamp>();

        static float Rank(CampusLamp l, Vector3 c)
        {
            Vector3 d = l.transform.position - c;
            float dy = Mathf.Max(0f, Mathf.Abs(d.y) - 2.5f);
            return new Vector2(d.x, d.z).magnitude + dy * 6f;
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.2f;
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera : Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            sorted.Clear();
            foreach (var l in CampusLamp.All)
            {
                if (l == null || l.lightSource == null) continue;
                float d = (l.transform.position - c).sqrMagnitude;
                float r = l.Wanted ? range + 10f : range;   // the same give at the edge of the range
                if (d > r * r || !l.powered) { l.Wanted = false; continue; }
                sorted.Add(l);
            }
            // lamps on another storey count as much further away (the floor above is full of fittings you can't see)
            sorted.Sort((a, b) => Rank(a, c).CompareTo(Rank(b, c)));
            // a lit lamp keeps its place until it falls well past the budget, so lamps at the edge don't flip back and forth
            for (int i = 0; i < sorted.Count; i++)
            {
                var l = sorted[i];
                l.Wanted = l.important || i < budget || (l.Wanted && i < budget + keepExtra);
            }
            if (Focus.HasValue)
            {
                Vector3 f = Focus.Value;
                near.Clear();
                foreach (var l in CampusLamp.All)
                    if (l != null && l.lightSource != null && l.powered && (l.transform.position - f).sqrMagnitude < 50f * 50f) near.Add(l);
                near.Sort((a, b) => Rank(a, f).CompareTo(Rank(b, f)));
                for (int i = 0; i < near.Count && i < focusBudget; i++) near[i].Wanted = true;
            }
        }
    }
}
