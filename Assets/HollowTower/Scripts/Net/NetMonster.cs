using Unity.Netcode;

namespace HollowTower.Net
{
    /// <summary>
    /// A monster seen from a client. Monsters only think on the host (their NetworkTransform carries where they are);
    /// this carries what they're doing - their state, which animation and how fast, and whether they can be seen (an
    /// invisible Needlelimbs) - so a client's copy plays the same thing (PartyMonster / NeedleMonster puppet mode).
    /// </summary>
    public class NetMonster : NetworkBehaviour
    {
        readonly NetworkVariable<int> state = new NetworkVariable<int>();
        readonly NetworkVariable<int> anim = new NetworkVariable<int>();
        readonly NetworkVariable<float> animSpeed = new NetworkVariable<float>(1f);
        readonly NetworkVariable<bool> shown = new NetworkVariable<bool>(true);

        public int State { get { return state.Value; } }
        public int Anim { get { return anim.Value; } }
        public float AnimSpeed { get { return animSpeed.Value; } }
        public bool Shown { get { return shown.Value; } }
        /// <summary>This machine should play the monster as a puppet (a client in a session).</summary>
        public bool Puppet { get { return IsSpawned && !IsServer; } }

        /// <summary>Host: what the monster is doing now.</summary>
        public void Push(int s, int a, float speed, bool visible)
        {
            if (!IsSpawned || !IsServer) return;
            if (state.Value != s) state.Value = s;
            if (anim.Value != a) anim.Value = a;
            if (UnityEngine.Mathf.Abs(animSpeed.Value - speed) > 0.05f) animSpeed.Value = speed;
            if (shown.Value != visible) shown.Value = visible;
        }
    }
}
