using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

namespace HollowTower
{
    /// <summary>
    /// Puts the usable items (UsableItem) about the tower - not many: on each floor a few bags of sweets (sanity), cans of
    /// soda (stamina), party poppers (lures) and glow sticks (light) - on tables and desks where there are some, else on
    /// the floor, one per room, never over a hole. Also gives the sound bank its panting clip. Run by the floor 2 hunt
    /// build; menu: Hollow Tower > Build Items.
    /// </summary>
    public static class TowerItemsBuilder
    {
        const string RootName = "Tower_Items";
        const string MatDir = "Assets/HollowTower/Generated/Hunt";

        struct Spec { public UsableItem.Kind kind; public int floor, count; }
        static readonly Spec[] Specs =
        {
            new Spec { kind = UsableItem.Kind.Sweets, floor = 0, count = 3 }, new Spec { kind = UsableItem.Kind.Soda, floor = 0, count = 2 },
            new Spec { kind = UsableItem.Kind.Popper, floor = 0, count = 1 }, new Spec { kind = UsableItem.Kind.GlowStick, floor = 0, count = 2 },
            new Spec { kind = UsableItem.Kind.Sweets, floor = 1, count = 3 }, new Spec { kind = UsableItem.Kind.Soda, floor = 1, count = 2 },
            new Spec { kind = UsableItem.Kind.Popper, floor = 1, count = 2 }, new Spec { kind = UsableItem.Kind.GlowStick, floor = 1, count = 1 },
        };

        [MenuItem("Hollow Tower/Build Items")]
        public static void BuildMenu() { Debug.Log("[Items] " + Build()); }

        public static string Build()
        {
            var env = GameObject.Find("HollowTower_Remastered_Environment");
            if (env == null) return "no environment";
            var old = env.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(env.transform, false);

            var bank = Object.FindAnyObjectByType<SfxBank>();
            if (bank != null)
            {
                bank.breathing = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Breathing_Exhausted.mp3");
                EditorUtility.SetDirty(bank);
            }

            var rnd = new Random(909);
            var used = new HashSet<string>();
            int placed = 0, wanted = 0;
            foreach (var s in Specs)
                for (int k = 0; k < s.count; k++)
                {
                    wanted++;
                    Vector3 at; string room;
                    if (!FindSpot(s.floor, rnd, used, out at, out room)) continue;
                    used.Add(s.floor + room);
                    Make(root, s.kind, at, rnd).name = s.kind + "_F" + (s.floor + 1) + "_" + room;
                    placed++;
                }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return placed + "/" + wanted + " items placed" + (bank != null && bank.breathing != null ? ", panting clip set" : ", NO panting clip");
        }

        static bool FindSpot(int floor, Random rnd, HashSet<string> used, out Vector3 at, out string room)
        {
            at = Vector3.zero; room = null;
            var plan = LevelLayout.Plans[floor];
            float fy = floor * 4.2f;
            var rooms = new List<Room>();
            foreach (var r in plan.rooms)
            {
                if (r == plan.corridor || used.Contains(floor + r.name)) continue;
                if (r.type == RoomType.Stair || r.type == RoomType.StairLobby) continue;
                rooms.Add(r);
            }
            for (int tries = 0; tries < 200 && rooms.Count > 0; tries++)
            {
                var r = rooms[rnd.Next(rooms.Count)];
                var R = r.rect;
                var rr = Rect.MinMaxRect(R.x * 4f + 0.8f, (24 - R.y - R.height) * 4f + 0.8f, (R.x + R.width) * 4f - 0.8f, (24 - R.y) * 4f - 0.8f);
                if (rr.width <= 0f || rr.height <= 0f) continue;
                var p = new Vector3(rr.xMin + (float)rnd.NextDouble() * rr.width, fy + 2.6f, rr.yMin + (float)rnd.NextDouble() * rr.height);
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 3.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float h = hit.point.y - fy;
                bool onFloor = h > -0.05f && h < 0.15f, onTable = h > 0.55f && h < 1.15f;
                if (!onFloor && !onTable) continue;   // over a hole (it went through), on a shelf top, on a lamp...
                if (hit.normal.y < 0.9f) continue;
                if (onFloor && tries < 140) continue;   // tables first: things are put down on things
                if (Physics.CheckBox(hit.point + Vector3.up * 0.2f, new Vector3(0.12f, 0.16f, 0.12f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<PartyHideSpot>() != null) continue;
                at = hit.point;
                room = r.name;
                return true;
            }
            return false;
        }

        static GameObject Make(Transform root, UsableItem.Kind kind, Vector3 at, Random rnd)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(root, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            var t = go.transform;
            var item = go.AddComponent<PartyItem>();
            item.needsTorch = false;
            item.glowRenderers = new Renderer[0];
            switch (kind)
            {
                case UsableItem.Kind.Sweets:
                    item.itemId = "sweets"; item.displayName = "Sweets"; item.color = new Color(1f, 0.45f, 0.75f);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.16f, 0.2f, 0.07f), Mat("Mat_Item_SweetBag", new Color(1f, 0.45f, 0.75f), 0f, 0.6f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.165f, 0.035f, 0.075f), Mat("Mat_Item_White", new Color(0.95f, 0.95f, 0.92f), 0f, 0.5f));
                    for (int i = 0; i < 3; i++)
                        Part(t, PrimitiveType.Sphere, new Vector3(0.12f + i * 0.05f, 0.015f, 0.04f - i * 0.03f), Vector3.one * 0.03f, Mat("Mat_Item_Candy" + i, Color.HSVToRGB(i * 0.3f, 0.7f, 1f), 0f, 0.8f));
                    break;
                case UsableItem.Kind.Soda:
                    item.itemId = "soda"; item.displayName = "Soda"; item.color = new Color(0.95f, 0.2f, 0.2f);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.066f, 0.06f, 0.066f), Mat("Mat_Item_Can", new Color(0.85f, 0.1f, 0.12f), 0.6f, 0.7f));
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.122f, 0f), new Vector3(0.06f, 0.003f, 0.06f), Mat("Mat_Item_Silver", new Color(0.8f, 0.8f, 0.82f), 1f, 0.8f));
                    break;
                case UsableItem.Kind.Popper:
                {
                    item.itemId = "popper"; item.displayName = "Popper"; item.color = new Color(1f, 0.8f, 0.2f);
                    var cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HollowTower/Generated/Party/PartyHatCone.asset");
                    var p = new GameObject("Popper");
                    p.transform.SetParent(t, false);
                    p.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                    p.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lying on its side
                    p.transform.localScale = new Vector3(0.06f, 0.14f, 0.06f);
                    p.AddComponent<MeshFilter>().sharedMesh = cone;
                    p.AddComponent<MeshRenderer>().sharedMaterial = Mat("Mat_Item_PopperGold", new Color(1f, 0.78f, 0.2f), 0.8f, 0.6f);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.035f, -0.08f), new Vector3(0.008f, 0.008f, 0.06f), Mat("Mat_Item_White", new Color(0.95f, 0.95f, 0.92f), 0f, 0.5f));
                    break;
                }
                default:
                    item.itemId = "glowstick"; item.displayName = "Glow stick"; item.color = new Color(0.4f, 1f, 0.5f);
                    var g = Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(0.022f, 0.1f, 0.022f), Mat("Mat_Item_GlowStick", new Color(0.4f, 1f, 0.5f), 0f, 0.8f, new Color(0.25f, 0.9f, 0.35f)));
                    g.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
            }
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.12f, 0f);
            col.size = new Vector3(0.4f, 0.3f, 0.4f);
            go.AddComponent<UsableItem>().kind = kind;
            return go;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Material Mat(string name, Color c, float metal, float smooth, Color? emission = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
