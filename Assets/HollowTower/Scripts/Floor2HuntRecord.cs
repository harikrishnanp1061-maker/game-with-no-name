using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>What the floor 2 hunt builder changed outside its own root (floor slabs it broke, components it added),
    /// so a rebuild or Remove can put it all back.</summary>
    public class Floor2HuntRecord : MonoBehaviour
    {
        public List<GameObject> deactivated = new List<GameObject>();
        public List<Collider> disabledColliders = new List<Collider>();
        public List<Component> addedComponents = new List<Component>();
    }
}
