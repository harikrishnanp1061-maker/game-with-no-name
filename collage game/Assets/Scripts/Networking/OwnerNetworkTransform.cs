using Unity.Netcode.Components;
using UnityEngine;

namespace FlipFall.Networking
{
    /// <summary>
    /// Client-authoritative NetworkTransform for player characters.
    /// The owning client drives transform updates; the server and non-owning clients smoothly interpolate.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FLIP//FALL/Networking/Owner Network Transform")]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
