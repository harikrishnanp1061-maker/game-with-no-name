using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Shows or hides an object depending on which of the four players the local player is steering (the collage game
    /// tutorial's PerceptionCover). A hidden-pit floor cover is HiddenFrom P2, so only P2 sees the hole under it; a
    /// false-wall outline or a crossing girder is VisibleOnlyTo its seer. Colliders are left alone unless syncColliders
    /// is on: the cover over a hidden pit has no collider at all (everyone falls through it), and the girder is solid
    /// for everyone (anyone can walk it once P2 says where it is).
    /// </summary>
    public class PartyPerception : MonoBehaviour
    {
        public enum Rule { VisibleOnlyTo, HiddenFrom }

        public Rule rule = Rule.HiddenFrom;
        [Tooltip("0 = Player 1 ... 3 = Player 4")]
        public int player = 1;
        public bool syncColliders = false;

        Renderer[] renderers;
        Collider[] colliders;

        public bool VisibleFor(int index) { bool has = PlayerParty.BodyHas(index, player); return rule == Rule.VisibleOnlyTo ? has : !has; }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        void OnEnable()
        {
            PlayerParty.ActiveChanged += Apply;
            Apply(PlayerParty.ActiveIndex);
        }

        void OnDisable()
        {
            PlayerParty.ActiveChanged -= Apply;
        }

        void Apply(int index)
        {
            bool visible = VisibleFor(index);
            foreach (var r in renderers) if (r != null) r.enabled = visible;
            if (syncColliders) foreach (var c in colliders) if (c != null) c.enabled = visible;
        }
    }
}
