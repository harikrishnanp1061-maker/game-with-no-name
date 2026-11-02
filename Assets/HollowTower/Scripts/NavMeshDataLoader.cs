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
            Wake();
            StartCoroutine(Rewake());
        }

        static void Wake()
        {
            int woke = 0, on = 0, off = 0;
            foreach (var a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (!a.enabled || !a.gameObject.activeInHierarchy) continue;
                if (!a.isOnNavMesh) { a.enabled = false; a.enabled = true; woke++; }
                if (a.isOnNavMesh) on++; else off++;
            }
            if (woke > 0) Debug.Log("[Nav] woke " + woke + " agents: " + on + " on the mesh, " + off + " off");
        }

        System.Collections.IEnumerator Rewake()
        {
            // agents are native and wake before any script: in a player build the monsters can enable before this mesh
            // is added ("Failed to create agent because there is no valid NavMesh") and then stay stuck. Waking them
            // again once the mesh is in puts them on it - at load, and whenever a culled floor switches back on.
            yield return null;
            Wake();
            yield return new WaitForSeconds(6f);
            Wake();
            // report what is still off the mesh (should be nothing: a monster stuck here never moves)
            var off = new System.Text.StringBuilder(); int on = 0;
            foreach (var a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                if (a.enabled && a.gameObject.activeInHierarchy) { if (a.isOnNavMesh) on++; else off.Append(a.name).Append(' '); }
            Debug.Log("[Nav] " + name + ": " + on + " active agents on the mesh" + (off.Length > 0 ? ", OFF: " + off : ""));
        }

        void OnDisable()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
