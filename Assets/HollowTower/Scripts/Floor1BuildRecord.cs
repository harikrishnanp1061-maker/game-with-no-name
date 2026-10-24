using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// What the floor 1 obstacle builder changed outside its own objects (slabs switched off, colliders disabled, doors
    /// locked, lamps put out), so a rebuild can put the floor back exactly as it was first.
    /// </summary>
    public class Floor1BuildRecord : MonoBehaviour
    {
        public List<GameObject> deactivated = new List<GameObject>();
        public List<Collider> disabledColliders = new List<Collider>();
        public List<Renderer> disabledRenderers = new List<Renderer>();
        public List<Component> addedComponents = new List<Component>();
        public List<InteractiveDoor> lockedDoors = new List<InteractiveDoor>();
        public List<string> doorMessages = new List<string>();
    }
}
