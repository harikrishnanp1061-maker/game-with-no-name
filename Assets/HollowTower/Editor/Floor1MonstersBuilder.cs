using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Floor 1's monsters, sound and P4's compass. Menu: Hollow Tower > Floor 1 > Build Monsters, Sound and Compass.
    /// - Bakes floor 1's NavMesh from its colliders (unlocked doors left out so monsters path through and bash them;
    ///   jammed doors, barricades, closed false walls and pit holes stay impassable) and loads it with the floor.
    /// - Two Needlelimbs (the tutorial's Entity 38 model and clips): a visible one hunting the south-west, where the
    ///   fuse and the hidden ledge pit are; an invisible one (only P3 sees it until it attacks) guarding the north, on
    ///   the way to the stairs.
    /// - The SfxBank with every clip, footstep sounds on each player, and P4's compass model (in hand and in view).
    /// Run after "Set Up Four Players" and "Build Co-op Obstacles". Safe to run again.
    /// </summary>
    public static class Floor1MonstersBuilder
    {
        const string NavPath = "Assets/HollowTowerRemastered/Navigation/Floor1_NavMesh.asset";
        const string Mats = "Assets/HollowTowerRemastered/Materials/Gameplay";
        const string Sfx = "Assets/Audio/SFX/";

        [MenuItem("Hollow Tower/Floor 1/Build Monsters, Sound and Compass")]
        public static void BuildMenu() { Debug.Log("[Floor1Monsters] " + Build()); }

        public static string Build()
        {
            var floor = CollegeDresser.FloorRoot(0);
            var partyGo = GameObject.Find("Player_Party");
            var party = partyGo != null ? partyGo.GetComponent<PlayerParty>() : null;
            if (floor == null || party == null) return "floor 1 or Player_Party missing";

            var old = floor.Find("Floor_1_Monsters");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Floor_1_Monsters");
            root.transform.SetParent(floor, false);

            string nav = BakeNavMesh(floor, party, root);
            SetupSound(partyGo, party);
            BuildCompasses(partyGo, party);

            HumanDeerAnimator.Build();
            var visible = SpawnDeer(root.transform, "HumanDeer_SouthWest", new Vector3(38f, 0f, 12f), new[]
            {
                new Vector3(38f, 0f, 12f), new Vector3(38f, 0f, 38f), new Vector3(12f, 0f, 12f), new Vector3(30f, 0f, 26f),
                new Vector3(26f, 0f, 20f), new Vector3(12f, 0f, 44f), new Vector3(20f, 0f, 26f)
            });
            var hidden = SpawnMonster(root.transform, "Needlelimbs_North_Invisible", new Vector3(44f, 0f, 84f), true, new[]
            {
                new Vector3(44f, 0f, 84f), new Vector3(12f, 0f, 60f), new Vector3(26f, 0f, 70f), new Vector3(40f, 0f, 70f),
                new Vector3(12f, 0f, 78f), new Vector3(48f, 0f, 58f), new Vector3(26f, 0f, 84f)
            });

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return nav + "; monsters: " + (visible != null) + "/" + (hidden != null);
        }

        // ------------------------------------------------------------------ navmesh
        static string BakeNavMesh(Transform floor, PlayerParty party, GameObject root)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var d in floor.GetComponentsInChildren<InteractiveDoor>(true))
                if (!d.isLocked) markups.Add(new NavMeshBuildMarkup { root = d.transform, ignoreFromBuild = true });
            foreach (var p in party.players) if (p != null) markups.Add(new NavMeshBuildMarkup { root = p.transform, ignoreFromBuild = true });

            // floor 1 only: from under the floor to just below its ceiling, so floor 2's slab is not picked up
            var bounds = new Bounds(new Vector3(72f, 1.2f, 48f), new Vector3(150f, 4.6f, 100f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 2.2f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 40f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return "NavMesh bake FAILED";
            if (!AssetDatabase.IsValidFolder("Assets/HollowTowerRemastered/Navigation")) AssetDatabase.CreateFolder("Assets/HollowTowerRemastered", "Navigation");
            AssetDatabase.DeleteAsset(NavPath);
            AssetDatabase.CreateAsset(data, NavPath);
            root.AddComponent<NavMeshDataLoader>().data = data;
            return "NavMesh baked from " + sources.Count + " colliders";
        }

        // ------------------------------------------------------------------ monsters
        static NeedleMonster SpawnMonster(Transform parent, string name, Vector3 at, bool invisible, Vector3[] patrol)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Entity38/SK_Entity_38_Needlelimbs_Animated.fbx");
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/Entity38/Entity38_AnimatorController.controller");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Entity38/Materials/Mat_Entity38_Needlelimbs.mat");
            if (fbx == null) { Debug.LogWarning("[Floor1Monsters] Entity 38 model missing"); return null; }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 1.15f;   // ~2.5 m: clears the 2.6 m doorways (the tutorial used 2, ceiling height)
            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();   // (?? misses Unity's fake-null)
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (mat != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var ms = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                    r.sharedMaterials = ms;
                }

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f; agent.height = 2.5f; agent.baseOffset = 0f;
            agent.angularSpeed = 320f; agent.acceleration = 16f; agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            var m = go.AddComponent<NeedleMonster>();
            m.invisibleUntilAggro = invisible;
            m.patrolPoints = patrol;
            m.animator = anim;
            m.renderers = model.GetComponentsInChildren<Renderer>();
            m.attackRange = 1.8f;   // reach scaled down with the body
            if (invisible) { m.sightRange = 11f; m.chaseSpeed = 5.0f; }
            return m;
        }

        /// <summary>The Human Deer: its own clips (HumanDeerAnimator), ~2.35 m tall, faster and more skittish than the Needlelimbs.</summary>
        static NeedleMonster SpawnDeer(Transform parent, string name, Vector3 at, Vector3[] patrol)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HumanDeerAnimator.Fbx);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HumanDeerAnimator.ControllerPath);
            if (fbx == null || ctrl == null) { Debug.LogWarning("[Floor1Monsters] Human Deer model or animations missing"); return null; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, go.transform);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // modelled facing -Z
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;

            // size from the toes to the antler tips; the fur cards are left out (their material doesn't port)
            float floorY = float.MaxValue, top = float.MinValue;
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("_toe_J")) floorY = Mathf.Min(floorY, t.position.y);
            var body = HumanDeerAnimator.BodyMaterial(false);
            var head = HumanDeerAnimator.BodyMaterial(true);
            var shown = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("pPlane")) { r.enabled = false; continue; }
                bool isHead = r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains("head");
                var ms = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ms.Length; i++) ms[i] = isHead ? head : body;
                r.sharedMaterials = ms;
                if (r.name.StartsWith("Antler")) top = Mathf.Max(top, r.bounds.max.y);
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;
                shown.Add(r);
            }
            float scale = 2.35f / Mathf.Max(0.01f, top - floorY);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(0f, (at.y - floorY) * scale - at.y * scale, 0f);
            float toe = float.MaxValue;
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("_toe_J")) toe = Mathf.Min(toe, t.position.y);
            model.transform.position += Vector3.up * (at.y - toe);

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f; agent.height = 2.35f; agent.angularSpeed = 360f; agent.acceleration = 20f; agent.stoppingDistance = 0.2f;
            var m = go.AddComponent<NeedleMonster>();
            m.patrolPoints = patrol;
            m.animator = anim;
            m.renderers = shown.ToArray();
            m.idleState = "Deer_Idle"; m.walkState = "Deer_Walk"; m.chaseState = "Deer_Run"; m.attackState = "Deer_Attack"; m.alertState = "Deer_Scream";
            m.attackPlaySpeed = 1f;
            m.attackRange = 1.9f;
            m.walkSpeed = 1.5f; m.chaseSpeed = 5.6f;   // creeps, then bolts
            return m;
        }

        // ------------------------------------------------------------------ sound
        static AudioClip A(string path)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + path);
            if (c == null) Debug.LogWarning("[Floor1Monsters] missing clip " + path);
            return c;
        }

        static void SetupSound(GameObject partyGo, PlayerParty party)
        {
            var bank = partyGo.GetComponent<SfxBank>();
            if (bank == null) bank = partyGo.AddComponent<SfxBank>();
            // single steps cut from the library's boots-walking and running recordings (Generated/Step_*)
            bank.footsteps = new AudioClip[6];
            bank.runSteps = new AudioClip[6];
            for (int i = 0; i < 6; i++) { bank.footsteps[i] = A("Generated/Step_Walk_0" + (i + 1) + ".wav"); bank.runSteps[i] = A("Generated/Step_Run_0" + (i + 1) + ".wav"); }
            bank.jump = A("Generated/Jump_Takeoff.wav");   // push-off scuff + cloth swish (the library jump sounded off)
            bank.land = A("Player/Player_Land_Concrete.wav");
            bank.torchOn = A("Player/Torch_Click_On.wav");
            bank.torchOff = A("Player/Torch_Click_Off.wav");
            bank.fallScream = A("Player/universfield-game-character-fall-131797.mp3");
            bank.fallImpact = A("Player/Player_Pit_Fall_Impact.mp3");
            bank.deathGasp = A("Monster/audiopapkin-ghost-breath-297876.mp3");
            bank.itemPickup = A("Items/Item_Pickup_Equip.mp3");
            bank.drone = A("Atmosphere/Atmosphere_Demonic_Drone_5Min.mp3");
            bank.doorOpen = A("Generated/Door_Open_Creak.wav");
            bank.doorClose = A("Generated/Door_Close_Thud.wav");
            bank.doorLocked = A("Generated/Door_Locked_Rattle.wav");
            bank.wallGrind = A("Generated/Wall_Stone_Grind.wav");
            bank.socketPower = A("Generated/Socket_Insert_Power.wav");
            bank.monsterLurk = A("Monster/Monster_Lurking_Proximity.mp3");
            bank.monsterScreech = A("Monster/Monster_Screech_Notice_Attack.mp3");
            bank.monsterRoar = A("Monster/Monster_Chase_Roar.mp3");
            bank.monsterAttack = A("Monster/Monster_Attack_GhostBreath.mp3");
            bank.heartbeat = A("Player/voicebosch-heart-and-breath-suspense-196083.mp3");
            bank.monsterSteps = new[] { A("Generated/Monster_Needle_Step_01.wav"), A("Generated/Monster_Needle_Step_02.wav"), A("Generated/Monster_Needle_Step_03.wav") };
            EditorUtility.SetDirty(bank);
            foreach (var p in party.players)
                if (p != null && p.GetComponent<PlayerSfx>() == null) p.gameObject.AddComponent<PlayerSfx>();
        }

        // ------------------------------------------------------------------ P4's compass
        static void BuildCompasses(GameObject partyGo, PlayerParty party)
        {
            var p4 = party.players[3];
            Transform hand = null;
            foreach (var t in p4.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":LeftHand")) { hand = t; break; }
            if (hand != null)
            {
                var old = hand.Find("Compass");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var c = BuildCompass(hand, false);
                // flat in the palm, face up out of the hand
                c.transform.position = hand.position + hand.up * 0.075f + hand.forward * 0.03f;
                c.transform.rotation = Quaternion.LookRotation(hand.up, hand.forward);
            }
            var oldView = partyGo.transform.Find("ViewCompass");
            if (oldView != null) Object.DestroyImmediate(oldView.gameObject);
            var view = BuildCompass(partyGo.transform, true);
            view.name = "ViewCompass";
        }

        static GameObject BuildCompass(Transform parent, bool viewModel)
        {
            var root = new GameObject("Compass");
            root.transform.SetParent(parent, false);
            var ls = parent.lossyScale;
            root.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            var brass = Mat("Mat_Compass_Brass", new Color(0.72f, 0.55f, 0.26f), 0.9f, 0.6f);
            var face = Mat("Mat_Compass_Face", new Color(0.88f, 0.84f, 0.74f), 0f, 0.3f);
            var ink = Mat("Mat_Compass_Ink", new Color(0.08f, 0.07f, 0.06f), 0f, 0.3f);
            var north = Mat("Mat_Compass_Needle", new Color(0.75f, 0.25f, 0.95f), 0.2f, 0.5f, new Color(0.35f, 0.1f, 0.45f));
            var glassMat = GlassMat();

            Part(root.transform, "Case", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.092f, 0.009f, 0.092f), brass, viewModel);
            Part(root.transform, "Bow", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.05f), new Vector3(0.018f, 0.004f, 0.018f), brass, viewModel);
            Part(root.transform, "Face", PrimitiveType.Cylinder, new Vector3(0f, 0.0092f, 0f), new Vector3(0.08f, 0.0005f, 0.08f), face, viewModel);
            for (int i = 0; i < 4; i++)
            {
                var tick = Part(root.transform, "Tick", PrimitiveType.Cube, Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0.0098f, 0.033f), new Vector3(0.003f, 0.0006f, 0.01f), ink, viewModel);
                tick.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
            var pivot = new GameObject("Needle");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0.0105f, 0f);
            Part(pivot.transform, "North", PrimitiveType.Cube, new Vector3(0f, 0f, 0.015f), new Vector3(0.006f, 0.0015f, 0.03f), north, viewModel);
            Part(pivot.transform, "South", PrimitiveType.Cube, new Vector3(0f, 0f, -0.012f), new Vector3(0.005f, 0.0015f, 0.024f), ink, viewModel);
            Part(pivot.transform, "Cap", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.006f, 0.0015f, 0.006f), brass, viewModel);
            var glass = Part(root.transform, "Glass", PrimitiveType.Cylinder, new Vector3(0f, 0.0125f, 0f), new Vector3(0.082f, 0.0004f, 0.082f), glassMat, viewModel);
            // the lid, swung open behind the case
            var lid = Part(root.transform, "Lid", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.092f, 0.004f, 0.092f), brass, viewModel);
            lid.transform.localPosition = new Vector3(0f, 0.04f, -0.06f);
            lid.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);

            var cm = root.AddComponent<CompassModel>();
            cm.needle = pivot.transform;
            cm.glass = glass.GetComponent<Renderer>();
            cm.viewModel = viewModel;
            return root;
        }

        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, bool viewModel)
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
            return go;
        }

        static Material Mat(string name, Color c, float metal, float smooth, Color? emission = null)
        {
            string path = Mats + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GlassMat()
        {
            var m = Mat("Mat_Compass_Glass", new Color(0.8f, 0.85f, 0.9f, 0.18f), 0f, 0.95f, new Color(0.1f, 0.05f, 0.14f));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}
