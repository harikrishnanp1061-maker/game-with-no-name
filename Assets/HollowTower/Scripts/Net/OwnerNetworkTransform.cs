using Unity.Netcode.Components;

namespace HollowTower.Net
{
    /// <summary>A player body's transform, driven by whoever steers it (the tutorial's OwnerNetworkTransform): the owning
    /// machine moves it, everyone else interpolates.</summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() { return false; }
    }
}
