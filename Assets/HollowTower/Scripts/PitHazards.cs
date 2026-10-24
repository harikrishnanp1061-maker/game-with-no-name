using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The pits of one floor. A player whose centre is over a hole (by more than a small grace margin) drops into it and
    /// dies: the body falls to the bottom of the shaft and stays there, whatever they carried is left on the last solid
    /// floor they stood on (so the fuse cell can't be lost), and the player carries on as a spectator (PlayerParty).
    /// Holes are tested by position rather than by colliders so a fake floor (a hidden pit's cover) and the narrow
    /// girder/ledge strips work exactly as drawn.
    /// </summary>
    public class PitHazards : MonoBehaviour
    {
        [System.Serializable]
        public class Hole
        {
            public Rect rect;
            public bool hidden;
        }

        public float floorY = 0f;
        public List<Hole> holes = new List<Hole>();
        public float dropDepth = 3.6f;
        [Tooltip("Depth of the shaft floor below the floor level (where a body comes to rest).")]
        public float shaftDepth = 4.2f;
        [Tooltip("How far past a hole's edge a player's centre must be before they fall (a stumble at the lip is forgiven).")]
        public float grace = 0.2f;
        [Tooltip("The tutorial's pits: a fall puts you back at the edge instead of killing you.")]
        public bool respawn;

        static readonly List<PitHazards> all = new List<PitHazards>();
        readonly HashSet<RemasteredPlayerController> falling = new HashSet<RemasteredPlayerController>();

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>True if a body standing at this point would be over a hole of any floor's pits.</summary>
        public static bool IsOverHole(Vector3 p)
        {
            foreach (var h in all) if (h.HoleAt(p) != null) return true;
            return false;
        }

        /// <summary>
        /// Where a ray meets the fake floor over a hidden pit (which has nothing to hit), if that is closer than maxDist.
        /// </summary>
        public static bool HiddenFloorHit(Ray ray, float maxDist, out Vector3 point)
        {
            point = Vector3.zero;
            bool found = false;
            foreach (var ph in all)
            {
                if (Mathf.Abs(ray.direction.y) < 0.02f) continue;
                float t = (ph.floorY - ray.origin.y) / ray.direction.y;
                if (t <= 0f || t >= maxDist) continue;
                Vector3 p = ray.GetPoint(t);
                foreach (var h in ph.holes)
                    if (h.hidden && h.rect.Contains(new Vector2(p.x, p.z))) { point = p; maxDist = t; found = true; break; }
            }
            return found;
        }

        bool OverAnyShaft(Vector3 p)
        {
            foreach (var h in holes) if (h.rect.Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        Hole HoleAt(Vector3 p)
        {
            if (p.y < floorY - 0.6f || p.y > floorY + 0.8f) return null;
            foreach (var h in holes)
                if (p.x > h.rect.xMin + grace && p.x < h.rect.xMax - grace && p.z > h.rect.yMin + grace && p.z < h.rect.yMax - grace) return h;
            return null;
        }

        void Update()
        {
            var party = PlayerParty.Instance;
            if (party == null) return;
            foreach (var body in party.players)
            {
                if (body == null || body.IsDead || falling.Contains(body) || body.IsFrozen || !Net.NetBody.Mine(body)) continue;   // the steering machine sees its own falls
                var hole = HoleAt(body.transform.position);
                if (hole != null) { StartCoroutine(Fall(body, hole)); continue; }
                // safety net: anything that ends up well below this floor's pits (inside this floor's area) goes back
                Vector3 p = body.transform.position;
                if (p.y < floorY - 6f && p.y > floorY - 200f && OverAnyShaft(p)) StartCoroutine(Fall(body, null));
            }
        }

        IEnumerator Fall(RemasteredPlayerController body, Hole hole)
        {
            falling.Add(body);
            var party = PlayerParty.Instance;
            bool steered = party != null && party.Current == body;
            Vector3 edge = body.SafePosition(1.0f);
            body.SetFrozen(true);
            var bank = SfxBank.Instance;
            if (bank != null) SfxBank.At(bank.fallScream, body.transform.position + Vector3.up * 1.5f, 1f, 1f, 30f);

            // whatever they carried stays up on the edge for the others
            var inv = body.GetComponent<PlayerInventory>();
            if (!respawn && inv != null && inv.Items.Count > 0) inv.DropAllAt(edge);

            // down to the bottom of the shaft, drifting to the middle of the hole so the walls don't catch the camera
            Vector3 start = body.transform.position;
            if (hole != null)
            {
                Vector3 mid = new Vector3(Mathf.Clamp(start.x, hole.rect.xMin + 0.45f, hole.rect.xMax - 0.45f), start.y, Mathf.Clamp(start.z, hole.rect.yMin + 0.45f, hole.rect.yMax - 0.45f));
                float drop = start.y - (floorY - shaftDepth);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
                {
                    body.transform.position = Vector3.Lerp(start, mid, Mathf.Clamp01(t * 3f)) + Vector3.down * drop * t * t;
                    if (steered && party != null) party.SetFade(Mathf.Clamp01((t - 0.5f) / 0.5f));
                    yield return null;
                }
                body.transform.position = new Vector3(mid.x, floorY - shaftDepth, mid.z);
            }
            if (bank != null) SfxBank.At(bank.fallImpact, body.transform.position, 1f, 1f, 20f);
            if (respawn)
            {
                // the tutorial: a lesson, not a death - back at the edge, and the narrator has something to say
                yield return new WaitForSeconds(0.6f);
                body.transform.position = edge;
                body.SetFrozen(false);
                falling.Remove(body);
                if (steered && party != null) { party.SetFade(0f); party.Flash("You fell - mind the pits", 2.5f); }
                if (NarrationManager.Instance != null) NarrationManager.Instance.TriggerNarration("FIRST_PIT_FALL");
                yield break;
            }
            body.Die();
            falling.Remove(body);
            if (party != null) party.OnPlayerDied(body);
        }
    }
}
