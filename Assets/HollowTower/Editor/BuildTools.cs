using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Standalone build support. The game creates its HDRP materials at runtime, so the build has to be
    /// told which shader variants they use: "Record Shader Variants" plays the game (benchmark tour) and
    /// saves every variant it rendered into Resources/HollowTowerVariants.shadervariants, which the
    /// player build then keeps. "Build Windows Player" writes Build/HollowTower.exe.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildTools
    {
        const string VariantsPath = "Assets/HollowTower/Resources/HollowTowerVariants.shadervariants";
        const string BuildPath = "Build/HollowTower.exe";
        const string RecordKey = "HT.RecordVariants";
        const string QuitKey = "HT.RecordVariants.Quit";
        static double startedAt = -1;
        static bool bossesShown, ghostsShown;

        static BuildTools()
        {
            if (SessionState.GetBool(RecordKey, false)) EditorApplication.update += WatchRecording;
        }

        [MenuItem("Hollow Tower/Build/1. Record Shader Variants", priority = 60)]
        public static void RecordVariants() { StartRecording(false); }

        /// <summary>Batch entry point: record, then quit the editor.</summary>
        public static void RecordVariantsAndQuit() { StartRecording(true); }

        static void StartRecording(bool quit)
        {
            EditorSceneManager.OpenScene("Assets/HollowTower/Scenes/HollowTower.unity");
            Call("ClearCurrentShaderVariantCollection");
            SessionState.SetBool(RecordKey, true);
            SessionState.SetBool(QuitKey, quit);
            EditorApplication.update += WatchRecording;
            EditorApplication.isPlaying = true;
        }

        static void WatchRecording()
        {
            if (!EditorApplication.isPlaying) return;
            if (startedAt < 0) startedAt = EditorApplication.timeSinceStartup;
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm == null) return;
            var bench = gm.GetComponent<Benchmark>();
            if (bench == null)
            {
                bench = gm.gameObject.AddComponent<Benchmark>();
                return;
            }
            // Fire a few rounds partway through so the muzzle flash and tracer variants get recorded.
            var p = PlayerController.Instance;
            double el = EditorApplication.timeSinceStartup - startedAt;
            if (p != null && el > 12 && el < 12.5)
            {
                var fire = typeof(MP5).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fire != null) fire.Invoke(p.Gun, null);
            }
            // The pistol too (its model and materials).
            if (p != null && el > 13 && el < 13.5 && p.Gun == p.Rifle) p.SwapWeapon();
            if (p != null && el > 15 && el < 15.5 && p.Gun == p.Pistol) p.SwapWeapon();
            // Put both boss models in front of the camera (frozen) so their material variants get recorded.
            if (p != null && el > 14 && !bossesShown)
            {
                bossesShown = true;
                Vector3 fwd = Vector3.ProjectOnPlane(p.Cam.transform.forward, Vector3.up).normalized;
                for (int v = 0; v < BossRig.Variants; v++)
                {
                    Zombie.NextBossVariant = v;
                    var z = Zombie.Spawn(EnemyKind.Brute, p.transform.position + fwd * 4f + p.transform.right * (v * 2.4f - 1.2f), null);
                    z.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                    z.enabled = false;
                    Object.Destroy(z.gameObject, 3f);
                }
            }
            // The ghost model (see-through, depth prepass, cut-out hair), its smoke and decoy, and a dark floor.
            if (p != null && el > 17 && !ghostsShown)
            {
                ghostsShown = true;
                Vector3 fwd = Vector3.ProjectOnPlane(p.Cam.transform.forward, Vector3.up).normalized;
                var g = Ghost.Spawn(p.transform.position + fwd * 3.5f + Vector3.up * 1.3f, null, false);
                g.enabled = false;
                Object.Destroy(g.gameObject, 4f);
                var smoke = GhostSmoke.Create(p.transform.position + fwd * 6f - p.transform.right * 1.5f, Quaternion.identity);
                Object.Destroy(smoke.gameObject, 5f);
                PowerGrid.Cut(p.Floor, p.transform.position);
            }
            if (p != null && el > 24 && !PowerGrid.IsOn(p.Floor)) PowerGrid.Restore(p.Floor);
            if (bench.enabled && el < 180) return;

            EditorApplication.update -= WatchRecording;
            SessionState.SetBool(RecordKey, false);
            Directory.CreateDirectory(Path.GetDirectoryName(VariantsPath));
            Call("SaveCurrentShaderVariantCollection", VariantsPath);
            AssetDatabase.ImportAsset(VariantsPath);
            var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsPath);
            Debug.Log("Hollow Tower: recorded " + (svc != null ? svc.variantCount + " shader variants in " + svc.shaderCount + " shaders" : "no variants") + " -> " + VariantsPath);
            bool quit = SessionState.GetBool(QuitKey, false);
            if (quit) { EditorApplication.Exit(svc != null ? 0 : 1); return; }
            EditorApplication.isPlaying = false;
        }

        [MenuItem("Hollow Tower/Build/2. Build Windows Player", priority = 61)]
        public static void BuildPlayer()
        {
            if (!File.Exists(VariantsPath)) Debug.LogWarning("Hollow Tower: record shader variants first (Hollow Tower > Build > 1), or materials may render pink in the build.");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/HollowTower/Scenes/HollowTower.unity" },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Hollow Tower: build " + report.summary.result + " in " + report.summary.totalTime + ", " + (report.summary.totalSize / (1024 * 1024)) + " MB -> " + BuildPath);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void Call(string method, params object[] args)
        {
            var m = typeof(ShaderUtil).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                                      .FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            if (m == null) { Debug.LogWarning("Hollow Tower: ShaderUtil." + method + " not available"); return; }
            m.Invoke(null, args);
        }
    }
}
