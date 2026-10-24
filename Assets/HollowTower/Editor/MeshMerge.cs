using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Cuts the renderer count of a dressed room: loose litter (paper, books, tiles, rubble...) is merged into
    /// one mesh per material for the whole room, and each box-built prop is merged into one mesh per material
    /// on its own root (its colliders stay as they were). Meshes are saved to one asset per room.
    /// </summary>
    public static class MeshMerge
    {
        public const string Dir = "Assets/HollowTowerRemastered/Props/Generated";

        static readonly HashSet<string> Litter = new HashSet<string>
        { "Paper", "Rubble", "FallenCeilingTile", "Book", "BookStack", "BookRow", "Clothes", "Tray", "Puddle", "Plank", "SpecimenJar" };

        static readonly HashSet<string> Built = new HashSet<string>
        { "Lockers", "ToiletStalls", "SinkCounter", "Counter", "LabBench", "CubiclePanel", "ServerRack", "VendingMachine", "Fridge",
          "Washer", "Mattress", "BunkBed", "Bench", "Pallet", "Pedestal", "UprightPiano", "Photocopier", "MusicStand", "Rug",
          "WaterFountain", "CellBars", "NoticeBoard", "Blackboard", "Whiteboard", "Mirror", "Urinal", "Radiator", "CeilingHole", "Bucket" };

        public static void ClearFloor(int floor)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) return;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("F" + (floor + 1) + "_")) AssetDatabase.DeleteAsset(path);
            }
        }

        public static void MergeRoom(Transform group, int floor)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/HollowTowerRemastered/Props", "Generated");
            var meshes = new List<Mesh>();

            // 1) litter: everything collider-less with a litter name, also when it sits on a host prop
            var litter = new List<Transform>();
            foreach (var t in group.GetComponentsInChildren<Transform>(true))
                if (t != group && Litter.Contains(t.name) && t.GetComponentInChildren<Collider>() == null && !PrefabUtility.IsPartOfPrefabInstance(t)) litter.Add(t);
            if (litter.Count > 0)
            {
                var holder = new GameObject("Litter_Merged");
                holder.transform.SetParent(group, false);
                var rs = new List<MeshRenderer>();
                foreach (var t in litter) rs.AddRange(t.GetComponentsInChildren<MeshRenderer>(true));
                Merge(rs, holder.transform, meshes, false);
                foreach (var t in litter) if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            // 2) box-built props: merge each one into itself
            foreach (Transform t in group)
            {
                if (!Built.Contains(t.name) || PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                var rs = new List<MeshRenderer>(t.GetComponentsInChildren<MeshRenderer>(true));
                if (rs.Count < 2) continue;
                bool shadows = false;
                foreach (var r in rs) if (r.shadowCastingMode != ShadowCastingMode.Off) shadows = true;
                Merge(rs, t, meshes, shadows);
                // drop the now-empty part objects, but keep any that carry colliders (stall partitions and doors)
                foreach (var r in rs)
                {
                    if (r == null) continue;
                    var go = r.gameObject;
                    Object.DestroyImmediate(r);
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);
                    if (go.GetComponent<Collider>() == null && go.transform.childCount == 0 && go.transform != t) Object.DestroyImmediate(go);
                }
            }

            if (meshes.Count == 0) return;
            string path = Dir + "/F" + (floor + 1) + "_" + group.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], path);
        }

        /// <summary>Merges renderers into children of 'target', one per material (in target's local space).</summary>
        static void Merge(List<MeshRenderer> renderers, Transform target, List<Mesh> outMeshes, bool castShadows)
        {
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var toLocal = target.worldToLocalMatrix;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = r.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null) continue;
                    List<CombineInstance> list;
                    if (!byMat.TryGetValue(mats[s], out list)) byMat[mats[s]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = toLocal * r.transform.localToWorldMatrix });
                }
            }
            foreach (var kv in byMat)
            {
                // the index format must be chosen before combining: changing it afterwards clears the triangles
                int verts = 0; foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                var mesh = new Mesh { name = target.name + "_" + kv.Key.name, indexFormat = verts < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                // drop vertex data the material never reads (saves most of the stored size)
                bool textured = kv.Key.HasProperty("_BaseMap") && kv.Key.GetTexture("_BaseMap") != null;
                bool bumped = kv.Key.HasProperty("_BumpMap") && kv.Key.GetTexture("_BumpMap") != null;
                if (!bumped) mesh.tangents = null;
                if (!textured && !bumped) mesh.uv = null;
                mesh.uv2 = null;
                mesh.RecalculateBounds();
                var go = new GameObject("Merged_" + kv.Key.name);
                go.transform.SetParent(target, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.lightProbeUsage = LightProbeUsage.Off;
                outMeshes.Add(mesh);
            }
        }
    }
}
