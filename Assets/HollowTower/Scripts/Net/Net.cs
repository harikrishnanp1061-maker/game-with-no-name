using Unity.Netcode;

namespace HollowTower.Net
{
    /// <summary>
    /// The one question every game system asks: is a network session running, and is this machine the authority?
    /// Offline (playing a scene straight from the editor, or solo) everything is local and this machine is the
    /// authority, so the game behaves exactly as it did before networking. In a session the host is the authority for
    /// the world (monsters, the music cycle, items, doors, fuse cells, deaths) and each player for their own body.
    /// </summary>
    public static class Net
    {
        public static NetworkManager Manager { get { return NetworkManager.Singleton; } }
        /// <summary>A host or client session is running.</summary>
        public static bool Active { get { return Manager != null && Manager.IsListening; } }
        /// <summary>This machine decides what happens in the world (offline, or the host).</summary>
        public static bool IsAuthority { get { return !Active || Manager.IsServer; } }
        public static bool IsClientOnly { get { return Active && !Manager.IsServer; } }
        public static ulong LocalId { get { return Active ? Manager.LocalClientId : 0; } }
    }
}
