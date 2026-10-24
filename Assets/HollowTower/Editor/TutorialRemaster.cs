using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Turns the collage game's tutorial map (copied into Assets/Tutorial) into a LAST BELL level on HollowTower's
    /// systems (Hollow Tower > Tutorial > Remaster): its old players and managers go, the tower's four players and party
    /// come in (spray paint, compass, inventory, items, sanity, stamina, hiding, networking), its covers become
    /// PartyPerception, its pits PitHazards (worked out from the real floor; a fall puts you back at the edge), its fuse
    /// cell a PartyItem that shows in P4's torch, its elevator a TutorialElevator that takes everyone to the tower, its
    /// Needlelimbs a NeedleMonster on a baked NavMesh. The narration and wake-up keep their own scripts' settings.
    /// </summary>
    public static class TutorialRemaster
    {
        const string NavPath = "Assets/Tutorial/Tutorial_NavMesh.asset";

        [MenuItem("Hollow Tower/Tutorial/Remaster")]
        public static void Menu() { Debug.Log("[Tutorial] " + Run()); }

        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            var tut = EditorSceneManager.OpenScene(NetSetup.TutorialScenePath, OpenSceneMode.Single);

            // 1. the old players, managers and everything whose script didn't come along
            foreach (var go in tut.GetRootGameObjects())
                if (go.name.StartsWith("Player_") || go.name == "NetworkGameManager" || go.name == "Tutorial_Gameplay") Object.DestroyImmediate(go);
            int stripped = 0;
            foreach (var go in tut.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) stripped += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            log.Append(stripped + " old script components removed; ");

            // 2. the tower's players and party, moved over (the tower scene is closed unsaved, so it keeps its own)
            var tower = EditorSceneManager.OpenScene(NetSetup.TowerScenePath, OpenSceneMode.Additive);
            var moved = new List<GameObject>();
            foreach (var go in tower.GetRootGameObjects())
                if (go.name == "Player_Party" || go.name.StartsWith("Player_")) moved.Add(go);
            foreach (var go in moved) SceneManager_Move(go, tut);
            EditorSceneManager.CloseScene(tower, true);
            var party = Object.FindAnyObjectByType<PlayerParty>();
            if (party == null) return log + "no party came across";
            foreach (var c in party.GetComponents<MonoBehaviour>())
                if (c is RemasteredMapHUD || c is TowerFloorCulling || c is RemasteredLightOptimizer) Object.DestroyImmediate(c);
            // where the four came round in the crash room
            Vector3[] spots = { new Vector3(-2.4f, 0.1f, 45.8f), new Vector3(0.8f, 0.1f, 47.6f), new Vector3(-1.1f, 0.1f, 51.2f), new Vector3(3.2f, 0.1f, 49.5f) };
            float[] yaw = { 45f, 325f, 142f, 222f };
            for (int i = 0; i < 4; i++)
                if (party.players[i] != null) party.players[i].transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0f, yaw[i], 0f));
            log.Append(moved.Count + " party objects moved in; ");

            var root = new GameObject("Tutorial_Gameplay").transform;
            var tutRoot = GameObject.Find("TutorialScene").transform;

            // 3. covers: P1 sees through the false walls, P2 the hidden pit's floor and its girder
            int covers = 0;
            foreach (var t in tutRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("DoorCover_")) { Perceive(t.gameObject, PartyPerception.Rule.HiddenFrom, 0); covers++; }
                else if (t.name == "FloorCover_HiddenPit") { Perceive(t.gameObject, PartyPerception.Rule.HiddenFrom, 1); covers++; }
                else if (t.name == "HiddenPit_CrossingGirder") { Perceive(t.gameObject, PartyPerception.Rule.VisibleOnlyTo, 1); covers++; }
            }
            log.Append(covers + " covers; ");

            // 4. pits: wherever the corridor floor has nothing under foot
            Physics.SyncTransforms();
            var pits = root.gameObject.AddComponent<PitHazards>();
            pits.floorY = 0f; pits.respawn = true; pits.shaftDepth = 9.5f;
            float[][] spans = { new[] { 20.0f, 25.0f }, new[] { 32.0f, 37.0f }, new[] { 44.0f, 49.0f }, new[] { 55.0f, 60.5f } };
            const float step = 0.25f;
            for (int k = 0; k < spans.Length; k++)
                for (float z = 45.9f; z < 54.1f; z += step)
                {
                    float runStart = -1f;
                    for (float x = spans[k][0]; x <= spans[k][1] + 0.001f; x += step)
                    {
                        bool hole = x < spans[k][1] && !Physics.Raycast(new Vector3(x + step * 0.5f, 1f, z + step * 0.5f), Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
                        if (hole && runStart < 0f) runStart = x;
                        if (!hole && runStart >= 0f)
                        {
                            pits.holes.Add(new PitHazards.Hole { rect = new Rect(runStart, z, x - runStart, step), hidden = k == 3 });
                            runStart = -1f;
                        }
                    }
                }
            log.Append(pits.holes.Count + " pit strips; ");

            // 5. the fuse cell in the dark room
            PartyItem fuse = null;
            var cellGo = Find(tutRoot, "SM_FuseCell_Pickup");
            if (cellGo != null)
            {
                fuse = cellGo.AddComponent<PartyItem>();
                fuse.itemId = "fuse_cell"; fuse.displayName = "Fuse cell"; fuse.needsTorch = true;
                var gl = Find(cellGo.transform, "Fuse_Green_GlowLight");
                fuse.glowLight = gl != null ? gl.GetComponent<Light>() : null;
                var rs = new List<Renderer>(cellGo.GetComponentsInChildren<Renderer>());
                fuse.glowRenderers = rs.ToArray();
                foreach (var c in cellGo.GetComponents<Collider>()) Object.DestroyImmediate(c);
                var col = cellGo.AddComponent<BoxCollider>();
                col.isTrigger = true; col.center = cellGo.transform.InverseTransformPoint(cellGo.transform.position + Vector3.up * 0.15f);
                col.size = new Vector3(0.45f, 0.45f, 0.45f) / Mathf.Max(0.01f, cellGo.transform.lossyScale.x);
            }

            // 6. the elevator and its power module
            TutorialElevator lift = null;
            var elevator = Find(tutRoot, "Working_Elevator");
            var housing = Find(tutRoot, "PowerModule_MainHousing");
            if (elevator != null && housing != null)
            {
                lift = housing.AddComponent<TutorialElevator>();
                lift.cab = elevator.GetComponent<Collider>();
                if (housing.GetComponentInChildren<Collider>() == null)
                {
                    var b = PropLibrary.RendererBounds(housing);
                    var hc = housing.AddComponent<BoxCollider>();
                    hc.center = housing.transform.InverseTransformPoint(b.center);
                    hc.size = housing.transform.InverseTransformVector(b.size + Vector3.one * 0.1f);
                    hc.size = new Vector3(Mathf.Abs(hc.size.x), Mathf.Abs(hc.size.y), Mathf.Abs(hc.size.z));
                }
                var s1 = Find(housing.transform, "Socket_01"); var s2 = Find(housing.transform, "Socket_02");
                var pre = s1 != null ? Find(s1.transform, "Preinstalled_FuseCell_01") : null;
                if (pre != null && s2 != null)
                {
                    var copy = Object.Instantiate(pre, s2.transform);
                    copy.name = "Installed_FuseCell_02";
                    copy.transform.position = pre.transform.position + (s2.transform.position - s1.transform.position);
                    copy.transform.rotation = pre.transform.rotation;
                    lift.installedCell = copy;
                    var g2 = Find(s2.transform, "Socket_GlowLight");
                    lift.glow = g2 != null ? g2.GetComponent<Light>() : null;
                }
            }
            log.Append("fuse " + (fuse != null) + ", elevator " + (lift != null) + "; ");

            var obj = root.gameObject.AddComponent<FloorObjective>();
            obj.floorIndex = 0;
            obj.fuse = fuse; obj.tutorialLift = lift; obj.stairs = elevator != null ? elevator.transform : null;
            obj.findText = "Find the fuse cell - it's in the dark room, and only P4's torch shows it";
            obj.carryText = "Take the fuse cell to the elevator's power module";
            obj.poweredText = "Everyone into the elevator - [E] to go down to the tower";
            obj.socketLabel = "ELEVATOR"; obj.exitLabel = "ELEVATOR";
            obj.checkpointRoom = new Rect(-1000f, -1000f, 0f, 0f);   // no checkpoint in the tutorial

            // 7. the Needlelimbs in the creature room, and the NavMesh it walks
            string nav = Bake(root, party);
            var needle = Find(tutRoot, "Entity38_Needlelimbs");
            if (needle != null && needle.GetComponent<NeedleMonster>() == null)
            {
                var anim = needle.GetComponentInChildren<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/Entity38/Entity38_AnimatorController.controller");
                if (anim != null && anim.runtimeAnimatorController == null && ctrl != null) anim.runtimeAnimatorController = ctrl;
                if (anim != null) anim.applyRootMotion = false;
                foreach (var c in needle.GetComponents<Collider>()) Object.DestroyImmediate(c);
                var agent = needle.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f; agent.height = 2.5f; agent.angularSpeed = 320f; agent.acceleration = 16f; agent.stoppingDistance = 0.2f;
                var m = needle.AddComponent<NeedleMonster>();
                m.animator = anim;
                m.renderers = needle.GetComponentsInChildren<Renderer>();
                m.attackRange = 1.8f;
                m.patrolPoints = new[] { new Vector3(88f, 0f, 11f), new Vector3(96f, 0f, 5f), new Vector3(96f, 0f, 17f), new Vector3(80f, 0f, 16f), new Vector3(80f, 0f, 6f) };
            }
            log.Append(nav + "; needlelimbs " + (needle != null) + "; ");

            EditorSceneManager.MarkSceneDirty(tut);
            EditorSceneManager.SaveScene(tut);
            log.Append(NetSetup.GameplayScene());
            return log.ToString();
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene to) { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, to); }

        static GameObject Find(Transform under, string name)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.gameObject;
            return null;
        }

        static void Perceive(GameObject go, PartyPerception.Rule rule, int player)
        {
            var p = go.GetComponent<PartyPerception>();
            if (p == null) p = go.AddComponent<PartyPerception>();
            p.rule = rule; p.player = player; p.syncColliders = false;
        }

        static string Bake(Transform root, PlayerParty party)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var p in party.players) if (p != null) markups.Add(new NavMeshBuildMarkup { root = p.transform, ignoreFromBuild = true });
            var bounds = new Bounds(new Vector3(45f, 1.5f, 30f), new Vector3(150f, 4f, 90f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f; settings.agentHeight = 2.2f; settings.agentClimb = 0.4f; settings.agentSlope = 40f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return "NavMesh FAILED";
            AssetDatabase.DeleteAsset(NavPath);
            AssetDatabase.CreateAsset(data, NavPath);
            root.gameObject.AddComponent<NavMeshDataLoader>().data = data;
            return "NavMesh from " + sources.Count + " colliders";
        }
    }
}
