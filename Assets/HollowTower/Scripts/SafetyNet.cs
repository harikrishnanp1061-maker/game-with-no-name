using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>
    /// Keeps you inside the game. Every quarter second it remembers the last place you stood that the dead could also
    /// walk to; if you end up somewhere you shouldn't be - outside a building on the bare ground slab, falling into the
    /// void, or stuck off the walkable area - it puts you back there.
    /// </summary>
    public class SafetyNet : MonoBehaviour
    {
        public static SafetyNet Instance { get; private set; }
        public int Rescues { get; private set; }
        public readonly List<Vector3> RescuedFrom = new List<Vector3>();
        Vector3 lastSafe;
        bool haveSafe;
        float nextCheck, badSince = -1f;
        Vector3 prevFeet;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Is the player standing somewhere legitimate?</summary>
        public static bool Valid(Vector3 feet)
        {
            RaycastHit h;
            if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out h, 1.6f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)
                && h.collider.name == "AnnexGround") return false;   // outside, between the buildings
            NavMeshHit n;
            if (!NavMesh.SamplePosition(feet, out n, 1.8f, NavMesh.AllAreas)) return false;
            return Mathf.Abs(n.position.y - feet.y) < 1.4f;
        }

        void Update()
        {
            var p = PlayerController.Instance;
            var gm = GameManager.Instance;
            if (p == null || p.IsDead || p.IsHidden || gm == null || gm.IsPaused) { badSince = -1f; return; }
            Vector3 feet = p.transform.position;
            // A teleport (checkpoint, test bot): start the bad-spot timer afresh.
            if ((feet - prevFeet).sqrMagnitude > 36f) badSince = -1f;
            prevFeet = feet;
            // Falling into nothing (nothing in the game is lower than the spike pits at -2.4): straight back.
            if (feet.y < -6f) { Rescue(p, feet); return; }
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            if (!p.Grounded) return;
            bool inPit = false;
            foreach (var t in TrapPit.All) if (t.Contains(feet) && feet.y < t.Center.y - 0.4f) inPit = true;
            if (inPit) { badSince = -1f; return; }
            if (Valid(feet))
            {
                badSince = -1f;
                lastSafe = feet;
                haveSafe = true;
                return;
            }
            if (badSince < 0f) badSince = Time.time;
            else if (Time.time - badSince > 0.5f) Rescue(p, feet);
        }

        void Rescue(PlayerController p, Vector3 from)
        {
            badSince = -1f;
            if (!haveSafe) return;
            Rescues++;
            RescuedFrom.Add(from);
            Debug.LogWarning("Hollow Tower: player left the walkable area at " + from.ToString("F1") + " - put back at " + lastSafe.ToString("F1"));
            p.Teleport(lastSafe + Vector3.up * 0.05f, p.Yaw, 0f);
        }
    }
}
