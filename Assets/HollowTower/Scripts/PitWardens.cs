using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// Once the party has made it up to floor 2, floor 1's monsters stop roaming and wait under floor 2's broken floors:
    /// each takes the holes nearest to it and stands a few steps from where a faller lands, looking up at the hole, moving
    /// on to another of its holes every so often. A landing (HardLanding's noise) brings it straight over. Until someone
    /// reaches floor 2 they patrol as before. The host decides (monsters think on the host); added by PartyFloor.
    /// </summary>
    public class PitWardens : MonoBehaviour
    {
        public float floor2Y = 4.2f;
        [Tooltip("How far from the landing point it waits (a stunned faller gets a moment before it is on them).")]
        public float standOff = 2.6f;
        public Vector2 waitAtEach = new Vector2(12f, 25f);
        [Tooltip("A monster is moved to its first hole at once only if no living player is this close to either end.")]
        public float unseenDistance = 20f;

        bool armed;

        public bool Armed { get { return armed; } }

        void Update()
        {
            if (armed || !Net.Net.IsAuthority) return;
            var party = PlayerParty.Instance;
            if (party == null) return;
            foreach (var p in party.players)
                if (p != null && !p.IsDead && Mathf.Abs(p.transform.position.y - floor2Y) < 1.2f) { Arm(); return; }
        }

        /// <summary>Sends floor 1's monsters to the holes (also callable for testing).</summary>
        public void Arm()
        {
            armed = true;
            var holes = Holes();
            var monsters = new List<NeedleMonster>();
            foreach (var m in NeedleMonster.All) if (m != null && m.transform.position.y < floor2Y - 2f) monsters.Add(m);
            if (holes.Count == 0 || monsters.Count == 0) { Debug.Log("[PitWardens] nothing to guard (" + holes.Count + " holes, " + monsters.Count + " monsters)"); return; }

            // each hole goes to the nearest monster, then any monster left without one takes the nearest spare
            var share = new Dictionary<NeedleMonster, List<int>>();
            foreach (var m in monsters) share[m] = new List<int>();
            for (int i = 0; i < holes.Count; i++)
            {
                NeedleMonster best = null; float bd = float.MaxValue;
                foreach (var m in monsters) { float d = Vector3.Distance(m.transform.position, holes[i].spot); if (d < bd) { bd = d; best = m; } }
                share[best].Add(i);
            }
            foreach (var m in monsters)
            {
                if (share[m].Count > 0) continue;
                foreach (var other in monsters)
                    if (share[other].Count > 1) { share[m].Add(share[other][share[other].Count - 1]); share[other].RemoveAt(share[other].Count - 1); break; }
            }

            var party = PlayerParty.Instance;
            foreach (var m in monsters)
            {
                var mine = share[m];
                if (mine.Count == 0) continue;
                // start at its nearest hole
                mine.Sort((a, b) => Vector3.Distance(m.transform.position, holes[a].spot).CompareTo(Vector3.Distance(m.transform.position, holes[b].spot)));
                var spots = new Vector3[mine.Count];
                var look = new Vector3[mine.Count];
                for (int k = 0; k < mine.Count; k++) { spots[k] = holes[mine[k]].spot; look[k] = holes[mine[k]].under; }
                bool unseen = party == null || (Clear(party, m.transform.position) && Clear(party, spots[0]));
                m.GuardSpots(spots, look, waitAtEach, unseen);
                Debug.Log("[PitWardens] " + m.name + " guards " + mine.Count + " hole(s)" + (unseen ? " (moved there)" : " (walking there)"));
            }
        }

        bool Clear(PlayerParty party, Vector3 at)
        {
            foreach (var p in party.players)
                if (p != null && !p.IsDead && Vector3.Distance(p.transform.position, at) < unseenDistance) return false;
            return true;
        }

        struct Hole { public Vector3 under, spot; }

        /// <summary>Where each of floor 2's holes comes down on floor 1, and a reachable spot a few steps from it.</summary>
        List<Hole> Holes()
        {
            var list = new List<Hole>();
            var root = GameObject.Find("Floor_2_Hunt");
            if (root == null) return list;
            foreach (Transform t in root.transform)
            {
                if (!t.name.StartsWith("Pit_")) continue;
                var edge = t.Find("Edge");
                if (edge == null) continue;
                Bounds b = new Bounds(); bool any = false;
                foreach (var r in edge.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (!any) continue;
                Vector3 under = new Vector3(b.center.x, 0f, b.center.z);
                NavMeshHit land;
                if (!NavMesh.SamplePosition(under, out land, 2.5f, NavMesh.AllAreas) || land.position.y > floor2Y - 2f) continue;
                float reach = standOff + Mathf.Max(b.extents.x, b.extents.z) * 0.5f;
                Vector3 spot; if (!SpotNear(land.position, b, reach, out spot)) continue;
                list.Add(new Hole { under = land.position, spot = spot });
            }
            return list;
        }

        // a spot on floor 1 round the landing point, outside the hole's footprint, that can be walked to from it
        bool SpotNear(Vector3 land, Bounds hole, float reach, out Vector3 spot)
        {
            var path = new NavMeshPath();
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 want = land + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach;
                NavMeshHit h;
                if (!NavMesh.SamplePosition(want, out h, 1.2f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - land.y) > 0.5f) continue;
                if (h.position.x > hole.min.x - 0.4f && h.position.x < hole.max.x + 0.4f && h.position.z > hole.min.z - 0.4f && h.position.z < hole.max.z + 0.4f) continue;
                if (!NavMesh.CalculatePath(land, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                spot = h.position;
                return true;
            }
            spot = land;
            return true;   // boxed in: wait right under it
        }
    }
}
