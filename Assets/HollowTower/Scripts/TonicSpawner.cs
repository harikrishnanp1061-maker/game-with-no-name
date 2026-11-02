using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Puts small bottles of calming tonic about every level when it loads (the only way to win sanity back - it drains
    /// all the time). Every machine makes the same bottles in the same places and names (positions from the level's own
    /// geometry and a seed from the scene's name), so NetWorld finds the same bottle everywhere when one is picked up.
    /// </summary>
    public class TonicSpawner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Spawn();
            Spawn();
        }

        static Material glass, label, cap;

        static void Spawn()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene == Net.NetLobby.LobbyScene || GameObject.Find("Tonics") != null) return;
            if (Object.FindAnyObjectByType<PlayerParty>() == null) return;
            var points = new List<Vector3>();
            var rnd = new System.Random(scene.GetHashCode() & 0x7fffffff);
            if (scene == Net.NetLobby.CampusLevel)
            {
                foreach (var p in new[] { new Vector3(-8, 0, -20), new Vector3(-20, 0, 12), new Vector3(30, 0, -12), new Vector3(-26, 0, -6), new Vector3(-18, 4.2f, -18),
                                          new Vector3(22, 4.2f, -16), new Vector3(18, 0, 231), new Vector3(144, 0, -46.5f), new Vector3(88, 0, -20), new Vector3(-54, 0, 92), new Vector3(-158, 0, -30), new Vector3(60, 0, 190) })
                    points.Add(p);
            }
            else if (scene == Net.NetLobby.TowerLevel)
            {
                for (int f = 0; f < 2 && f < LevelLayout.Plans.Length; f++)
                {
                    var rooms = new List<Room>(LevelLayout.Plans[f].rooms);
                    rooms.RemoveAll(r => r == LevelLayout.Plans[f].corridor || r.name.StartsWith("Stairs"));
                    for (int i = 0; i < 4 && rooms.Count > 0; i++)
                    {
                        var room = rooms[rnd.Next(rooms.Count)]; rooms.Remove(room);
                        var R = room.rect;
                        float x = (R.x + R.width * (0.3f + 0.4f * (float)rnd.NextDouble())) * 4f, z = (24 - R.y - R.height * (0.3f + 0.4f * (float)rnd.NextDouble())) * 4f;
                        points.Add(new Vector3(x, f * LevelBuilder.Story, z));
                    }
                }
            }
            else
            {
                // the tutorial (or any other level): spots on its navmesh, picked by the seed
                var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
                for (int i = 0; i < 4 && tri.indices.Length >= 3; i++)
                {
                    int t = rnd.Next(tri.indices.Length / 3) * 3;
                    points.Add((tri.vertices[tri.indices[t]] + tri.vertices[tri.indices[t + 1]] + tri.vertices[tri.indices[t + 2]]) / 3f);
                }
            }
            var root = new GameObject("Tonics").transform;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                RaycastHit h;
                if (Physics.Raycast(p + Vector3.up * 2.4f, Vector3.down, out h, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = h.point;
                Make(root, i, p, (float)rnd.NextDouble() * 360f);
            }
        }

        static GameObject Make(Transform root, int i, Vector3 at, float yaw)
        {
            if (glass == null)
            {
                glass = Gfx.Lit(new Color(0.35f, 0.12f, 0.05f), 0.92f, 0f);
                label = Gfx.Lit(new Color(0.88f, 0.82f, 0.62f), 0.2f, 0f);
                cap = Gfx.Lit(new Color(0.15f, 0.15f, 0.16f), 0.5f, 0.8f);
            }
            var go = new GameObject("Tonic_" + i);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var rs = new List<Renderer>();
            rs.Add(Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.055f, 0), new Vector3(0.06f, 0.055f, 0.06f), glass));
            rs.Add(Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.125f, 0), new Vector3(0.025f, 0.02f, 0.025f), glass));
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(0.028f, 0.008f, 0.028f), cap);
            rs.Add(Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 0.055f, 0), new Vector3(0.0615f, 0.025f, 0.0615f), label));
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.1f, 0); col.size = new Vector3(0.3f, 0.25f, 0.3f);
            var item = go.AddComponent<PartyItem>();
            item.itemId = "tonic"; item.displayName = "Calming tonic"; item.color = new Color(1f, 0.7f, 0.35f); item.needsTorch = false;
            item.glowRenderers = rs.ToArray(); item.glowIntensity = 0.8f;
            var use = go.AddComponent<UsableItem>(); use.kind = UsableItem.Kind.Tonic;
            return go;
        }

        static Renderer Part(Transform p, PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
            return r;
        }
    }
}
