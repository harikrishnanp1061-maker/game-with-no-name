using Unity.Netcode;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The campus giant as a client sees it. It only thinks on the host (its NetworkTransform carries where it walks);
    /// this carries what it is doing - its mode, who it is after, where its head looks, everyone's exposure and where a
    /// strike lands - so a client's copy plays the same animation and shows the same exposure bar.
    /// </summary>
    public class GiantNet : NetworkBehaviour
    {
        readonly NetworkVariable<int> mode = new NetworkVariable<int>();
        readonly NetworkVariable<int> target = new NetworkVariable<int>(-1);
        readonly NetworkVariable<Vector2> look = new NetworkVariable<Vector2>(new Vector2(0f, 50f));
        readonly NetworkVariable<Vector4> exposure = new NetworkVariable<Vector4>();
        readonly NetworkVariable<Vector3> strike = new NetworkVariable<Vector3>();

        public int Mode { get { return mode.Value; } }
        public int Target { get { return target.Value; } }
        public Vector2 Look { get { return look.Value; } }
        public Vector4 Exposure { get { return exposure.Value; } }
        public Vector3 Strike { get { return strike.Value; } }
        /// <summary>This machine plays the giant as a puppet (a client in a session).</summary>
        public bool Puppet { get { return IsSpawned && !IsServer; } }

        /// <summary>Host: what the giant is doing now.</summary>
        public void Push(int m, int t, Vector2 l, Vector4 e, Vector3 s)
        {
            if (!IsSpawned || !IsServer) return;
            if (mode.Value != m) mode.Value = m;
            if (target.Value != t) target.Value = t;
            if ((look.Value - l).sqrMagnitude > 1f) look.Value = l;
            if ((exposure.Value - e).sqrMagnitude > 0.0004f) exposure.Value = e;
            if ((strike.Value - s).sqrMagnitude > 0.01f) strike.Value = s;
        }
    }
}
