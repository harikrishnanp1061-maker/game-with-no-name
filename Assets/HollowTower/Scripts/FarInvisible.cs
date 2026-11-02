using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// P3's gift in every level: this monster can't be seen from further than hideBeyond metres - except by P3 (or
    /// whoever carries P3's ability in a small party), who sees it at any range. Up close everyone sees it. Purely what
    /// this machine draws; the monster itself is unchanged.
    /// </summary>
    public class FarInvisible : MonoBehaviour
    {
        public float hideBeyond = 12f;
        public const int Seer = 2;   // P3
        Renderer[] rs;
        bool shown = true;
        float next;

        void Start() { rs = GetComponentsInChildren<Renderer>(true); }

        void LateUpdate()
        {
            if (Time.time < next) return;
            next = Time.time + 0.15f;
            var party = PlayerParty.Instance;
            var view = PlayerParty.Active;
            bool show = true;
            if (party != null && view != null && !PlayerParty.BodyHas(party.CurrentIndex, Seer))
                show = Vector3.Distance(view.position, transform.position) < hideBeyond;
            if (show == shown) return;
            shown = show;
            foreach (var r in rs) if (r != null) r.enabled = show;
        }
    }
}
