using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// A P1 false wall in a doorway-sized gap left in a wall: a wall segment of the same material fills the gap (so to
    /// everyone else it is just wall), P1 sees it painted bright orange, and anyone can push it once P1 says where it
    /// is (HiddenWallDoor). Shared by the floor 2 build and the campus build; each passes its own box maker so the
    /// meshes are saved the way that builder saves its geometry.
    /// </summary>
    public static class FalseWall
    {
        public delegate GameObject BoxFn(string name, Transform parent, Vector3 worldCentre, Vector3 size, Material m, bool collider);

        public const string OrangePath = "Assets/HollowTowerRemastered/Materials/Gameplay/Mat_FalseWallOrange.mat";

        /// <param name="floorCentre">The gap's centre at floor level.</param>
        /// <param name="alongX">The wall runs along x (else along z).</param>
        public static HiddenWallDoor Make(Transform parent, string name, Vector3 floorCentre, bool alongX, float thickness, float openW, float openH,
                                          Material wallMat, Material trim, BoxFn box)
        {
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 across = alongX ? Vector3.forward : Vector3.right;
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = floorCentre;
            var door = g.AddComponent<HiddenWallDoor>();
            door.seer = 0;

            Vector3 segSize = Abs(along * openW + across * thickness) + Vector3.up * openH;
            var seg = box("FalseWall_Segment", g.transform, floorCentre + Vector3.up * openH * 0.5f, segSize, wallMat, true);
            door.hideOnOpen = new[] { seg };

            var trigger = g.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * openH * 0.5f;
            trigger.size = Abs(along * openW + across * (thickness + 0.5f)) + Vector3.up * openH;

            var frame = new GameObject("OpenFrame");
            frame.transform.SetParent(g.transform, false);
            foreach (float side in new[] { -1f, 1f })
                box("Jamb", frame.transform, floorCentre + along * side * (openW * 0.5f - 0.04f) + Vector3.up * openH * 0.5f, Abs(along * 0.08f + across * (thickness + 0.06f)) + Vector3.up * openH, trim, false);
            box("Header", frame.transform, floorCentre + Vector3.up * (openH - 0.04f), Abs(along * openW + across * (thickness + 0.06f)) + Vector3.up * 0.08f, trim, false);
            frame.SetActive(false);
            door.openFrame = frame;

            var panel = box("Panel", g.transform, floorCentre + Vector3.up * openH * 0.5f, segSize, wallMat, false);
            panel.SetActive(false);
            door.panel = panel.transform;

            var orange = AssetDatabase.LoadAssetAtPath<Material>(OrangePath);
            var outline = new GameObject("Outline_P1");
            outline.transform.SetParent(g.transform, false);
            foreach (float face in new[] { -1f, 1f })
                box("Paint", outline.transform, floorCentre + across * face * (thickness * 0.5f + 0.008f) + Vector3.up * openH * 0.5f, Abs(along * openW + across * 0.01f) + Vector3.up * openH, orange, false);
            var perc = outline.AddComponent<PartyPerception>();
            perc.rule = PartyPerception.Rule.VisibleOnlyTo; perc.player = 0;
            door.outline = outline;
            return door;
        }

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }
    }
}
