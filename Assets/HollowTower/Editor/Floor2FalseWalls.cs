using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Floor 2's P1 false walls: a secret way round the collapsed corridor. The Principal's office only opens onto the
    /// collapse, but two false walls connect it to its neighbours - Classroom 2J (west, on the stair side of the
    /// collapse) and Classroom 2K (east, on the exit lift's side). So P1 can lead the team round the hole through the
    /// office while P2 guides them over the plank walkway: two ways past, each needing a different player. They also
    /// make a loop to lose the party guests in when the music stops.
    /// </summary>
    public static class Floor2FalseWalls
    {
        const string RootName = "Floor_2_FalseWalls";

        [MenuItem("Hollow Tower/Floor 2/Build False Walls (P1)")]
        public static void Menu() { Debug.Log("[Floor2] " + Build()); }

        public static string Build()
        {
            var floor = CollegeDresser.FloorRoot(1);
            if (floor == null) return "Floor_2_School not found (open the tower scene)";
            var old = floor.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform; root.SetParent(floor, false);
            int n = 0;
            int moved = 0;
            foreach (var x in new[] { 76f, 92f })
            {
                if (Floor1GameplayBuilder.SecretWallOnFloor(1, root, x < 80f ? "FalseWall_2J_Principal" : "FalseWall_Principal_2K", new Vector3(x, 2f, 10f), false)) n++;
                moved += Clear(floor, new Vector3(x, LevelBuilder.Story, 10f));
            }
            int trimmed = TrimFloatingLitter(floor);
            EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
            return n + " false walls on floor 2 (" + moved + " props slid out of the doorways, " + trimmed + " floating litter pieces removed)";
        }

        /// <summary>The rooms' litter is merged into one mesh per material: books that lay on a shelf that has just been
        /// slid aside would hang in the air. Those triangles (off the floor, beside the openings) are cut from a copy.</summary>
        static int TrimFloatingLitter(Transform floor)
        {
            int n = 0;
            foreach (var rn in new[] { "Room_Principals_office", "Room_Classroom_2J", "Room_Classroom_2K" })
            {
                var litter = floor.Find("College_Dressing/" + rn + "/Litter_Merged");
                if (litter == null) continue;
                foreach (var mf in litter.GetComponentsInChildren<MeshFilter>(true))
                {
                    var src = mf.sharedMesh;
                    if (src == null || !src.isReadable) continue;
                    var v = src.vertices; var tris = src.triangles; var keep = new System.Collections.Generic.List<int>();
                    var m = mf.transform.localToWorldMatrix;
                    int cut = 0;
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        Vector3 c = m.MultiplyPoint3x4((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                        bool nearWall = (Mathf.Abs(c.x - 76f) < 1.4f || Mathf.Abs(c.x - 92f) < 1.4f) && Mathf.Abs(c.z - 10f) < 3.4f;
                        if (nearWall && c.y > LevelBuilder.Story + 0.35f) { cut++; continue; }
                        keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                    }
                    if (cut == 0) continue;
                    var copy = Object.Instantiate(src);
                    copy.name = src.name + "_FalseWallTrim";
                    copy.SetTriangles(keep, 0);
                    string path = "Assets/HollowTowerRemastered/Generated";
                    if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder("Assets/HollowTowerRemastered", "Generated");
                    path += "/" + rn + "_" + mf.name + "_trim.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(copy, path);
                    mf.sharedMesh = copy;
                    n += cut / 12 + 1;
                }
            }
            return n;
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Slides furniture standing in front of a false wall (either side) along the wall, out of the way.</summary>
        static int Clear(Transform floor, Vector3 at)
        {
            var zone = new Bounds(at + Vector3.up * 1.2f, new Vector3(3.6f, 2.2f, 2.4f));   // across the wall (x) and along it (z)
            int n = 0;
            var done = new System.Collections.Generic.HashSet<Transform>();
            foreach (var groupName in new[] { "College_Dressing", "Floor_2_Party", "Floor_2_Hunt" })
            {
                var g = floor.Find(groupName);
                if (g == null) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    if (!zone.Intersects(r.bounds)) continue;
                    // the prop itself: the dressing is grouped per room (containers at the origin), so stop one level below
                    // the room's container - never move the container
                    var t = r.transform;
                    while (t.parent != null && t.parent != g && t.parent.parent != g) t = t.parent;
                    if (t.parent == g && t.position == Vector3.zero) continue;   // a room container itself
                    if (!done.Add(t)) continue;
                    if (t.GetComponentInParent<HollowTower.PartyMonster>() != null || t.GetComponentInParent<HollowTower.PartyItem>() != null) continue;
                    float dir = t.position.z >= at.z ? 1f : -1f;
                    var before = Bounds(t);
                    t.position += new Vector3(0f, 0f, dir * 2.8f);
                    n++;
                    // whatever stood on it (books, clocks on a shelf) goes with it
                    foreach (Transform other in t.parent)
                    {
                        if (other == t || done.Contains(other) || other.GetComponentInChildren<Renderer>() == null) continue;
                        var ob = Bounds(other);
                        if (ob.center.x > before.min.x - 0.1f && ob.center.x < before.max.x + 0.1f && ob.center.z > before.min.z - 0.1f && ob.center.z < before.max.z + 0.1f
                            && ob.min.y > before.min.y + 0.2f && ob.size.x < before.size.x + 0.2f)
                        { done.Add(other); other.position += new Vector3(0f, 0f, dir * 2.8f); n++; }
                    }
                }
            }
            return n;
        }
    }
}