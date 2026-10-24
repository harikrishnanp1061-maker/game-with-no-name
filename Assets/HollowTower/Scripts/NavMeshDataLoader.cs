using UnityEngine;
using UnityEngine.AI;

namespace HollowTower
{
    /// <summary>Adds a floor's baked NavMesh while that floor is active (floor culling switches it with the floor).</summary>
    [DefaultExecutionOrder(-1000)]   // before the monsters wake and look for it
    public class NavMeshDataLoader : MonoBehaviour
    {
        public NavMeshData data;
        NavMeshDataInstance instance;

        void OnEnable()
        {
            if (data != null && !instance.valid) instance = NavMesh.AddNavMeshData(data);
        }

        void OnDisable()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
