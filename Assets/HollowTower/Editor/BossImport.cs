using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Import settings for the brute boss models (Resources/Models/Bosses). "boss 2" is a Mixamo FBX whose
    /// only clip is a breathing idle: it imports as Legacy so BossRig can layer procedural motion on top.
    /// Its textures are 4K; bosses never fill the screen, so they import at 2K.
    /// </summary>
    [InitializeOnLoad]
    public class BossImport : AssetPostprocessor
    {
        const string Folder = "Assets/HollowTower/Resources/Models/Bosses/";
        // The chasers (scary zombie, parasite zombie, human deer) are rigged the same way.
        const string ZombieFolder = "Assets/HollowTower/Resources/Models/Zombies/";
        static bool Ours(string path) { return path.StartsWith(Folder) || path.StartsWith(ZombieFolder); }

        // After ModelTextureCap (HDRPSetup.cs), which turns animation off for everything under Models/.
        public override int GetPostprocessOrder() { return 10; }

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath) || !assetPath.EndsWith(".png")) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
        }

        void OnPreprocessModel()
        {
            if (!Ours(assetPath) || !assetPath.EndsWith(".fbx")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
        }

        /// <summary>Batch entry point: log the boss 2 FBX hierarchy and components.</summary>
        public static void Inspect()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Boss2/Boss2.fbx");
            var mi = (ModelImporter)AssetImporter.GetAtPath(Folder + "Boss2/Boss2.fbx");
            Debug.Log("BossInspect importer anim=" + mi.animationType + " globalScale=" + mi.globalScale + " fileScale=" + mi.fileScale + " useFileScale=" + mi.useFileScale + " skinWeights=" + mi.skinWeights);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                string c = "";
                foreach (var comp in t.GetComponents<Component>()) c += comp.GetType().Name + " ";
                if (t.name.StartsWith("mixamorig") && !t.name.EndsWith("Hips")) continue;
                Debug.Log("BossInspect " + t.name + " parent=" + (t.parent ? t.parent.name : "-") + " s=" + t.localScale + " comps=" + c);
            }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Folder + "Boss2/Boss2.fbx"))
                Debug.Log("BossInspect sub " + o.GetType().Name + " " + o.name);
            EditorApplication.Exit(0);
        }

        /// <summary>Batch entry point: log the chaser FBXs (non-finger bones, meshes, clips).</summary>
        public static void InspectZombies()
        {
            foreach (var name in new[] { "Scary/Scary", "Parasite/Parasite", "Deer/Deer" })
            {
                string path = ZombieFolder + name + ".fbx";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { Debug.Log("ZInspect missing " + path); continue; }
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name;
                    if (n.Contains("Hand") && n.Length > 20 || n.Contains("index") || n.Contains("middle") || n.Contains("ring") || n.Contains("pinkie") || n.Contains("thumb") || n.Contains("Tongue") || n.Contains("tail")) continue;
                    string c = "";
                    foreach (var comp in t.GetComponents<Component>()) c += comp.GetType().Name + " ";
                    Debug.Log("ZInspect " + name + " " + n + " parent=" + (t.parent ? t.parent.name : "-") + " p=" + t.position.ToString("F2") + " comps=" + c);
                }
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Debug.Log("ZInspect " + name + " SMR " + r.name + " mats=" + r.sharedMaterials.Length + " verts=" + (r.sharedMesh ? r.sharedMesh.vertexCount : 0) + " bones=" + r.bones.Length + " bounds=" + r.bounds.size.ToString("F2"));
                foreach (var r in go.GetComponentsInChildren<MeshFilter>(true))
                    Debug.Log("ZInspect " + name + " MF " + r.name + " verts=" + (r.sharedMesh ? r.sharedMesh.vertexCount : 0));
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip) Debug.Log("ZInspect " + name + " clip " + o.name + " len=" + ((AnimationClip)o).length);
            }
            EditorApplication.Exit(0);
        }

        const string ShotsKey = "HT.BossShots";

        static BossImport()
        {
            if (SessionState.GetBool(ShotsKey, false)) EditorApplication.update += WatchShots;
        }

        /// <summary>Batch entry point (pass -bossshots too): plays the game, saves Shots/boss*_*.png, then quits.</summary>
        public static void BossShots()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(Folder + "Boss2/Boss2.fbx");
            if (mi != null && mi.animationType != ModelImporterAnimationType.Legacy)
                AssetDatabase.ImportAsset(Folder + "Boss2/Boss2.fbx", ImportAssetOptions.ForceUpdate);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/HollowTower/Scenes/HollowTower.unity");
            SessionState.SetBool(ShotsKey, true);
            EditorApplication.update += WatchShots;
            EditorApplication.isPlaying = true;
        }

        static void WatchShots()
        {
            bool timeout = EditorApplication.timeSinceStartup > 900;
            if (!timeout && !Benchmark.Done) return;
            SessionState.SetBool(ShotsKey, false);
            EditorApplication.update -= WatchShots;
            EditorApplication.Exit(Benchmark.Done ? 0 : 1);
        }
    }
}
