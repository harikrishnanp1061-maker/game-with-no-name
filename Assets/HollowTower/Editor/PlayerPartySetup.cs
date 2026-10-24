using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Puts the collage game tutorial's four players into the level: the existing player becomes Player_1, three copies
    /// wearing the Player 2-4 joint colours join it, all four start squeezed into the spawn elevator cab, and a
    /// Player_Party object (PlayerParty) switches between them. The level-wide helpers that used to sit on the player
    /// (map HUD, lamp optimiser, floor culling) move onto Player_Party so there is one of each. Abilities as in the
    /// collage game design: only P4 keeps the torch, P1-P3 carry two items and P4 four. Safe to run again.
    /// </summary>
    public static class PlayerPartySetup
    {
        const string JointMat = "Assets/Materials/Mat_Player{0}_Joints.mat";

        // Inside the 1.9 x 2 m spawn cab, door to the west (-x): Player 1 front-left, nearest the door.
        static readonly Vector3[] Spots = { new Vector3(141.15f, 0f, 11.5f), new Vector3(141.15f, 0f, 12.5f), new Vector3(142.15f, 0f, 11.5f), new Vector3(142.15f, 0f, 12.5f) };
        static readonly float[] Yaws = { 268f, 276f, 262f, 281f };

        [MenuItem("Hollow Tower/Players/Set Up Four Players")]
        public static void SetupMenu() { Debug.Log("[PlayerPartySetup] " + Setup()); }

        public static string Setup()
        {
            var p1 = FindBody("Player_1") ?? FindBody("Player_Character_Controlled");
            if (p1 == null) return "no RemasteredPlayerController found";
            p1.name = "Player_1";
            p1.isControlled = true;

            var partyGo = GameObject.Find("Player_Party") ?? new GameObject("Player_Party");
            var party = partyGo.GetComponent<PlayerParty>() ?? partyGo.AddComponent<PlayerParty>();
            MoveComponent<RemasteredMapHUD>(p1.gameObject, partyGo);
            MoveComponent<RemasteredLightOptimizer>(p1.gameObject, partyGo);
            MoveComponent<TowerFloorCulling>(p1.gameObject, partyGo);

            var cam = p1.GetComponentInChildren<Camera>(true);
            party.playerCamera = cam;
            party.players = new RemasteredPlayerController[4];
            party.players[0] = p1;

            for (int i = 1; i < 4; i++)
            {
                var old = GameObject.Find("Player_" + (i + 1));
                if (old != null && old.GetComponent<RemasteredPlayerController>() != null) Object.DestroyImmediate(old);
                var go = Object.Instantiate(p1.gameObject);
                go.name = "Player_" + (i + 1);
                // teammates have no camera of their own: PlayerParty hands Player_Camera over on F1-F4
                foreach (var c in go.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
                var pc = go.GetComponent<RemasteredPlayerController>();
                pc.isControlled = false;
                pc.playerCamera = null;
                go.tag = "Untagged";
                SetJoints(go, i + 1);
                party.players[i] = pc;
            }
            SetJoints(p1.gameObject, 1);

            for (int i = 0; i < 4; i++)
            {
                var pc = party.players[i];
                Place(pc, Spots[i], Yaws[i]);
                // P4 is the torch bearer; the others' torches are left behind
                pc.hasTorch = i == 3;
                pc.flashlightEnabled = i == 3;
                if (pc.flashlightLight != null) pc.flashlightLight.gameObject.SetActive(i == 3);
                var inv = pc.GetComponent<PlayerInventory>() ?? pc.gameObject.AddComponent<PlayerInventory>();
                inv.capacity = i == 3 ? 4 : 2;
                EditorUtility.SetDirty(pc);
            }
            party.startingPlayer = 0;
            if (partyGo.GetComponent<PartyInteractor>() == null) partyGo.AddComponent<PartyInteractor>();
            // everyone carries a spray can in their own colour for marking hidden pits and false walls
            for (int i = 0; i < 4; i++) GiveCan(party.players[i], i);
            var spray = partyGo.GetComponent<SprayPaint>() ?? partyGo.AddComponent<SprayPaint>();
            spray.paintMaterial = PaintMaterial();
            party.pingMaterial = spray.paintMaterial;   // spectators' pings use the same see-through tinted material
            var oldView = partyGo.transform.Find("ViewCan");
            if (oldView != null) Object.DestroyImmediate(oldView.gameObject);
            var view = BuildCan(partyGo.transform, CanMaterial("View", Color.white), true);
            view.name = "ViewCan";
            spray.viewCan = view.transform;
            spray.viewCanBody = view.transform.Find("Body").GetComponent<Renderer>();
            EditorUtility.SetDirty(spray);

            var hud = partyGo.GetComponent<RemasteredMapHUD>();
            if (hud != null) { hud.switchSceneKey = KeyCode.F9; EditorUtility.SetDirty(hud); }   // F1-F4 pick the player now

            EditorUtility.SetDirty(party);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return "4 players in the spawn cab, party on " + partyGo.name + ", camera " + (cam != null ? cam.name : "missing");
        }

        // ------------------------------------------------------------------ spray cans
        const string GameplayMats = "Assets/HollowTowerRemastered/Materials/Gameplay";

        static void GiveCan(RemasteredPlayerController body, int index)
        {
            Transform hand = null;
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":RightHand")) { hand = t; break; }
            if (hand == null) { Debug.LogWarning("[PlayerPartySetup] no right hand on " + body.name); return; }
            var old = hand.Find("SprayCan");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var can = BuildCan(hand, CanMaterial("P" + (index + 1), PlayerParty.Colors[index]), false);
            can.name = "SprayCan";
            // in the palm: a little way down the fingers, its axis across the hand (thumb to little finger)
            can.transform.position = hand.position + hand.up * 0.075f + hand.forward * 0.025f;
            can.transform.rotation = Quaternion.LookRotation(hand.forward, -hand.right);
        }

        /// <summary>A 19 cm aerosol can: coloured body, grey shoulder, white cap and nozzle. Pivot at its centre.</summary>
        static GameObject BuildCan(Transform parent, Material bodyMat, bool viewModel)
        {
            var root = new GameObject("SprayCan");
            root.transform.SetParent(parent, false);
            var ls = parent.lossyScale;
            root.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            var grey = CanMaterial("Shoulder", new Color(0.55f, 0.56f, 0.57f), 0.8f);
            var cap = CanMaterial("Cap", new Color(0.85f, 0.85f, 0.83f), 0.1f);
            Part(root.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, -0.015f, 0f), new Vector3(0.066f, 0.075f, 0.066f), bodyMat, viewModel);
            Part(root.transform, "Shoulder", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0f), new Vector3(0.066f, 0.03f, 0.066f), grey, viewModel);
            Part(root.transform, "Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.03f, 0.012f, 0.03f), cap, viewModel);
            Part(root.transform, "Nozzle", PrimitiveType.Cube, new Vector3(0f, 0.095f, 0.008f), new Vector3(0.012f, 0.012f, 0.016f), cap, viewModel);
            return root;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, bool viewModel)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (viewModel) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material CanMaterial(string name, Color c, float metal = 0.55f)
        {
            string path = GameplayMats + "/Mat_SprayCan_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder(GameplayMats)) AssetDatabase.CreateFolder("Assets/HollowTowerRemastered/Materials", "Gameplay");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Transparent, unlit, double-sided, tinted by vertex colour (the player's colour per dot).</summary>
        static Material PaintMaterial()
        {
            string path = GameplayMats + "/Mat_SprayPaint.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static RemasteredPlayerController FindBody(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<RemasteredPlayerController>() : null;
        }

        static void MoveComponent<T>(GameObject from, GameObject to) where T : Component
        {
            var c = from.GetComponent<T>();
            if (c == null || to.GetComponent<T>() != null) return;
            ComponentUtility.CopyComponent(c);
            ComponentUtility.PasteComponentAsNew(to);
            Object.DestroyImmediate(c);
        }

        static void SetJoints(GameObject body, int n)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(string.Format(JointMat, n));
            if (mat == null) { Debug.LogWarning("[PlayerPartySetup] missing " + string.Format(JointMat, n)); return; }
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.Contains("Joints")) r.sharedMaterial = mat;
        }

        /// <summary>Stands a body on whatever floor is under the spot (the cab floor sits on a skid).</summary>
        static void Place(RemasteredPlayerController p, Vector3 spot, float yaw)
        {
            var cc = p.GetComponent<CharacterController>();
            bool was = cc.enabled;
            cc.enabled = false;
            float y = spot.y;
            foreach (var hit in Physics.RaycastAll(spot + Vector3.up * 2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<RemasteredPlayerController>() == null) y = Mathf.Max(y, hit.point.y);
            p.transform.SetPositionAndRotation(new Vector3(spot.x, y + 0.01f, spot.z), Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = was;
            Physics.SyncTransforms();
        }
    }
}
