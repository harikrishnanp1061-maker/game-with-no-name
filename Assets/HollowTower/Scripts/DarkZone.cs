using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// A room whose lights are dead (floor 2's photo darkroom). The lamps don't cast shadows, so the rooms around would
    /// light it through the walls, and the party floor's ambient is bright: while the view is inside, PartyFloor takes
    /// the ambient almost to black and dims the lamps nearby. Inside counts as the dark for sanity, torch or glow stick
    /// aside.
    /// </summary>
    public class DarkZone : MonoBehaviour
    {
        public static readonly List<DarkZone> All = new List<DarkZone>();
        public Rect rect;
        public float floorY = 4.2f;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(Vector3 p) { return Mathf.Abs(p.y - floorY - 1f) < 2.2f && rect.Contains(new Vector2(p.x, p.z)); }

        public static DarkZone At(Vector3 p)
        {
            foreach (var z in All) if (z != null && z.Contains(p)) return z;
            return null;
        }
    }
}
