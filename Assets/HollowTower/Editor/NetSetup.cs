using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HollowTower.Net;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Wires the project for LAN play (Hollow Tower > Net > Set Up Networking): the NetLobby prefab (Resources/Net), the
    /// Lobby scene (camera + LobbyMenu), and in a gameplay scene (the open one) NetworkObject + OwnerNetworkTransform +
    /// NetBody on the four player bodies and one NetGame. Build settings: Lobby first, then the tutorial and the tower.
    /// Safe to run again.
    /// </summary>
    public static class NetSetup
    {
        public const string LobbyScenePath = "Assets/HollowTower/Scenes/Lobby.unity";
        public const string TutorialScenePath = "Assets/Tutorial/Scenes/Tutorial.unity";
        public const string TowerScenePath = "Assets/HollowTowerRemastered/Scenes/HollowTower_Remastered.unity";
        const string PrefabPath = "Assets/HollowTower/Resources/Net/NetLobby.prefab";

        [MenuItem("Hollow Tower/Net/Set Up Networking")]
        public static void Menu() { Debug.Log("[NetSetup] " + SetupAll()); }

        public static string SetupAll()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(LobbyPrefab()).Append("; ");
            var open = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.SaveOpenScenes();
            sb.Append(LobbyScene()).Append("; ");
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            sb.Append(GameplayScene()).Append("; ");
            sb.Append(BuildList());
            return sb.ToString();
        }

        static string LobbyPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/HollowTower/Resources/Net")) AssetDatabase.CreateFolder("Assets/HollowTower/Resources", "Net");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return "lobby prefab ok";
            var go = new GameObject("NetLobby");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetLobby>();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return "lobby prefab made";
        }

        static string LobbyScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/HollowTower/Scenes")) AssetDatabase.CreateFolder("Assets/HollowTower", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            var c = cam.GetComponent<Camera>(); c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
            new GameObject("LobbyMenu").AddComponent<LobbyMenu>();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            return "lobby scene saved";
        }

        /// <summary>The open scene's player bodies and session object.</summary>
        public static string GameplayScene()
        {
            var party = Object.FindAnyObjectByType<PlayerParty>();
            if (party == null) return "no PlayerParty in " + EditorSceneManager.GetActiveScene().name;
            int bodies = 0;
            foreach (var p in party.players)
            {
                if (p == null) continue;
                var go = p.gameObject;
                if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
                var nt = go.GetComponent<OwnerNetworkTransform>() ?? go.AddComponent<OwnerNetworkTransform>();
                nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;   // bodies only turn about up
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                nt.Interpolate = true;
                if (go.GetComponent<NetBody>() == null) go.AddComponent<NetBody>();
                EditorUtility.SetDirty(go);
                bodies++;
            }
            var game = Object.FindAnyObjectByType<NetGame>();
            if (game == null)
            {
                var go = new GameObject("NetGame");
                go.AddComponent<NetworkObject>();
                game = go.AddComponent<NetGame>();
            }
            if (game.GetComponent<NetWorld>() == null) game.gameObject.AddComponent<NetWorld>();
            int monsters = NetworkMonsters();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return bodies + " bodies and " + monsters + " monsters networked in " + EditorSceneManager.GetActiveScene().name;
        }

        /// <summary>Every monster in the open scene: host-driven transform + NetMonster (their state for the clients).
        /// Run again after a builder remakes them (the floor 2 build does it itself).</summary>
        public static int NetworkMonsters()
        {
            int n = 0;
            var all = new List<GameObject>();
            foreach (var m in Object.FindObjectsByType<PartyMonster>(FindObjectsInactive.Include, FindObjectsSortMode.None)) all.Add(m.gameObject);
            foreach (var m in Object.FindObjectsByType<NeedleMonster>(FindObjectsInactive.Include, FindObjectsSortMode.None)) all.Add(m.gameObject);
            foreach (var go in all)
            {
                if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
                var nt = go.GetComponent<NetworkTransform>() ?? go.AddComponent<NetworkTransform>();
                nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                nt.Interpolate = true;
                if (go.GetComponent<NetMonster>() == null) go.AddComponent<NetMonster>();
                EditorUtility.SetDirty(go);
                n++;
            }
            return n;
        }

        static string BuildList()
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var path in new[] { LobbyScenePath, TutorialScenePath, TowerScenePath })
                if (System.IO.File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            return "build list: " + list.Count + " scenes";
        }
    }
}
