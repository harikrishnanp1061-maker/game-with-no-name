using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// A box-built prop that is generated a limited number of times and then reused: each variant is merged
    /// into one mesh per material and saved as a prefab, so hundreds of radiators or locker banks share a
    /// handful of meshes instead of each storing its own.
    /// </summary>
    public class ProcVariant
    {
        public string key;
        public int variants;
        public Func<RoomCtx, GameObject> build;
    }

    public static class ProcVariants
    {
        public const string Dir = "Assets/HollowTowerRemastered/Props/ProcPrefabs";
        static readonly Dictionary<string, List<GameObject>> cache = new Dictionary<string, List<GameObject>>();

        public static void Reset()
        {
            cache.Clear();
            if (AssetDatabase.IsValidFolder(Dir)) AssetDatabase.DeleteAsset(Dir);
        }

        public static GameObject Instantiate(ProcVariant v, RoomCtx c, Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/HollowTowerRemastered/Props", "ProcPrefabs");
            List<GameObject> list;
            if (!cache.TryGetValue(v.key, out list))
            {
                list = new List<GameObject>();
                for (int i = 0; ; i++)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + v.key + "_" + i + ".prefab");
                    if (existing == null) break;
                    list.Add(existing);
                }
                cache[v.key] = list;
            }
            GameObject prefab;
            if (list.Count < v.variants)
            {
                prefab = Bake(v, c, list.Count);
                if (prefab == null) return null;
                list.Add(prefab);
            }
            else prefab = list[c.Range(0, list.Count)];
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        static GameObject Bake(ProcVariant v, RoomCtx c, int index)
        {
            var src = v.build(c);
            if (src == null) return null;
            src.transform.position = Vector3.zero;
            src.transform.rotation = Quaternion.identity;
            string basePath = Dir + "/" + v.key + "_" + index;

            // one renderer per material on the root; part objects that carry colliders are kept
            var renderers = new List<MeshRenderer>(src.GetComponentsInChildren<MeshRenderer>(true));
            bool shadows = false;
            foreach (var r in renderers) if (r.shadowCastingMode != ShadowCastingMode.Off) shadows = true;
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var toLocal = src.transform.worldToLocalMatrix;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || r.sharedMaterial == null) continue;
                List<CombineInstance> l;
                if (!byMat.TryGetValue(r.sharedMaterial, out l)) byMat[r.sharedMaterial] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * r.transform.localToWorldMatrix });
            }
            var meshes = new List<Mesh>();
            foreach (var kv in byMat)
            {
                // the index format must be chosen before combining: changing it afterwards clears the triangles
                int verts = 0; foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                var mesh = new Mesh { name = v.key + "_" + index + "_" + kv.Key.name, indexFormat = verts < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                bool textured = kv.Key.HasProperty("_BaseMap") && kv.Key.GetTexture("_BaseMap") != null;
                bool bumped = kv.Key.HasProperty("_BumpMap") && kv.Key.GetTexture("_BumpMap") != null;
                if (!bumped) mesh.tangents = null;
                if (!textured && !bumped) mesh.uv = null;
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                var go = new GameObject("Mesh_" + kv.Key.name);
                go.transform.SetParent(src.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.lightProbeUsage = LightProbeUsage.Off;
            }
            foreach (var r in renderers)
            {
                var go = r.gameObject;
                Object.DestroyImmediate(r);
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf);
            }
            // remove part objects left with nothing on them (deepest first)
            var all = src.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                var t = all[i];
                if (t == src.transform || t.name.StartsWith("Mesh_")) continue;
                if (t.childCount == 0 && t.GetComponents<Component>().Length == 1) Object.DestroyImmediate(t.gameObject);
            }
            if (meshes.Count == 0) { Object.DestroyImmediate(src); return null; }
            AssetDatabase.CreateAsset(meshes[0], basePath + "_mesh.asset");
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], basePath + "_mesh.asset");
            var prefab = PrefabUtility.SaveAsPrefabAsset(src, basePath + ".prefab");
            Object.DestroyImmediate(src);
            return prefab;
        }
    }
}
