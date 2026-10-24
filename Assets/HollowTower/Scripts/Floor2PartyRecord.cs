using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>What the floor 2 party builder changed outside its own root, so Remove can put it back.</summary>
    public class Floor2PartyRecord : MonoBehaviour
    {
        public List<Renderer> swappedRenderers = new List<Renderer>();
        public List<Material> originalMaterials = new List<Material>();   // one per swapped renderer slot, in order
        public List<int> slotIndex = new List<int>();
    }
}
