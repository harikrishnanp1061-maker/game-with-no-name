using UnityEngine;
using UnityEngine.Rendering;

namespace HollowTower
{
    /// <summary>
    /// Entry point. Drop this on any GameObject in an empty scene (the editor menu
    /// "Hollow Tower > Create Game Scene" does it for you) and press Play:
    /// it sets up the lighting, builds the tower, bakes navigation, and spawns everyone.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [HideInInspector] public Color ambient = new Color(0.1f, 0.105f, 0.125f);   // legacy (built-in pipeline)
        [HideInInspector] public float fogDensity = 0.022f;                        // legacy (built-in pipeline)

        void Awake()
        {
            SetupRendering();
            NoiseSystem.Clear();

            var tower = new GameObject("Halvorsen Tower").transform;
            var level = LevelBuilder.Build(tower);
            Physics.SyncTransforms();
            LevelBuilder.BakeNavMesh(level.bounds);
            LevelBuilder.PruneUnreachableAnnex(level);

            var player = PlayerController.Spawn(level.playerStart, level.playerYaw);
            gameObject.AddComponent<SafetyNet>();

            var enemies = new GameObject("Enemies").transform;
            foreach (var sp in level.spawns)
            {
                if (sp.kind == EnemyKind.Ghost) Ghost.Spawn(sp.position, enemies, false);
                else Zombie.Spawn(sp.kind, sp.position, enemies);
            }

            var gm = gameObject.AddComponent<GameManager>();
            gm.Setup(level, enemies);
            if (Benchmark.Requested) gameObject.AddComponent<Benchmark>();
            if (PlayTest.Requested) PlayTest.Ensure();
            Debug.Log(string.Format("Hollow Tower ready: {0} enemies, {1} fuses, player at {2}", level.spawns.Count, level.fuses.Count, player.transform.position));
        }

        void SetupRendering()
        {
            // HDRP: exposure, fog, ray tracing etc. live in a code-built Volume (see HDRPLook).
            HDRPLook.Create();
            RenderSettings.fog = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            // No V-Sync: on a 144 Hz panel it snaps frames to 144/72/48 fps. Cap at the screen's refresh rate instead.
            QualitySettings.vSyncCount = 0;
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = hz > 30 ? hz : 144;

            Physics.queriesHitTriggers = false;
            Time.timeScale = 1f;
        }
    }
}
