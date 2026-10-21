using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Read-only diagnostic for the NavMesh / enemy combat chain.
///
/// Reports whether the floor geometry is marked Navigation Static (required for
/// it to be baked), how much of the play area ends up walkable, and whether the
/// EnemyAI -> NavMeshAgent -> PlayerCombatSystem -> PlayerHealthHUD chain is
/// wired so the player actually gets a health bar and takes damage.
///
/// Unity 6000.6.5f1 keeps the classic runtime NavMesh, so baking goes through
/// UnityEditor.AI.NavMeshBuilder and coverage is measured with NavMesh.SamplePosition.
/// Static flags are read straight from the 'm_StaticEditorFlags' field because
/// this build does not expose EditorUtility.GetStaticEditorFlags.
/// </summary>
public static class NavMeshDiagnostic
{
    private const int NavigationStaticMask = (int)StaticEditorFlags.NavigationStatic;

    // ---------------------------------------------------------------------
    // STATIC FLAGS - read and write through the serialized field directly
    // ---------------------------------------------------------------------
    private static int GetStaticFlags(GameObject go)
    {
        var so = new SerializedObject(go);
        var prop = so.FindProperty("m_StaticEditorFlags");
        if (prop == null) return 0;
        return prop.intValue;
    }

    private static void SetStaticFlags(GameObject go, int flags)
    {
        var so = new SerializedObject(go);
        var prop = so.FindProperty("m_StaticEditorFlags");
        if (prop == null) return;
        prop.intValue = flags;
        so.ApplyModifiedProperties();
    }

    private static bool IsNavStatic(GameObject go)
    {
        return go.isStatic
            && (GetStaticFlags(go) & NavigationStaticMask) != 0;
    }

    // ---------------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------------
    private static string Path(Transform t)
    {
        var sb = new StringBuilder();
        for (var n = t; n != null; n = n.parent)
        {
            if (sb.Length > 0) sb.Insert(0, '/');
            sb.Insert(0, n.name);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Samples a coarse grid above the floor and reports what fraction is on the
    /// NavMesh. This is the number that decides whether the enemy can actually
    /// path across the scene.
    /// </summary>
    private static void ReportCoverage(StringBuilder sb, float y, float gridStep)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool have = false;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                Renderer rend = t.GetComponent<Renderer>();
                if (rend == null && t.GetComponent<Collider>() == null) continue;
                var b = rend != null ? rend.bounds : new Bounds(t.position, Vector3.one * 3f);
                if (!have) { bounds = b; have = true; } else bounds.Encapsulate(b);
            }

        if (!have) { sb.AppendLine("  (no geometry found)"); return; }

        bounds.size += Vector3.one * gridStep;
        int total = 0, walkable = 0;
        for (float x = bounds.min.x; x <= bounds.max.x; x += gridStep)
            for (float z = bounds.min.z; z <= bounds.max.z; z += gridStep)
            {
                total++;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(new Vector3(x, y, z), out hit, gridStep, -1))
                    walkable++;
            }

        sb.AppendLine($"  Grid {bounds.size.x:F0}x{bounds.size.z:F0}m at y={y}, " +
                      $"step={gridStep}m -> {walkable}/{total} walkable " +
                      $"({(total > 0 ? walkable * 100f / total : 0f):F1}%)");
    }

    // ---------------------------------------------------------------------
    // REPORT
    // ---------------------------------------------------------------------
    [MenuItem("Tools/Diagnostics/NavMesh + Combat Report")]
    public static void Report()
    {
        var sb = new StringBuilder();

        int total = 0, navStatic = 0, collidersNotStatic = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                total++;
                if (IsNavStatic(t.gameObject)) navStatic++;
                else if (t.GetComponent<Collider>() != null) collidersNotStatic++;
            }

        sb.AppendLine("=== NAVMESH SOURCES ===");
        sb.AppendLine($"Transforms: {total}  NavigationStatic: {navStatic}  " +
                      $"colliders NOT static (never bake): {collidersNotStatic}");

        sb.AppendLine("=== WALKABLE COVERAGE ===");
        ReportCoverage(sb, 0.5f, 2f);

        sb.AppendLine("=== PLAYERS ===");
        foreach (var f in Object.FindObjectsOfType<FirstPersonController>())
        {
            NavMeshHit hit;
            bool onMesh = NavMesh.SamplePosition(f.transform.position + Vector3.up, out hit, 2f, -1);
            sb.AppendLine($"  {Path(f.transform)}  tag={f.gameObject.tag} pos={f.transform.position}");
            sb.AppendLine($"     onNavMesh={onMesh}  CharacterController=" +
                          $"{(f.GetComponent<CharacterController>() != null)}  " +
                          $"PlayerCombatSystem={(f.GetComponent<PlayerCombatSystem>() != null)}  " +
                          $"camera={(f.playerCamera != null ? f.playerCamera.name : "null")}");
        }

        sb.AppendLine("=== ENEMIES ===");
        foreach (var e in Object.FindObjectsOfType<EnemyAI>())
        {
            var agent = e.GetComponent<NavMeshAgent>();
            var cc = e.GetComponent<CharacterController>();
            NavMeshHit hit;
            bool near = NavMesh.SamplePosition(e.transform.position + Vector3.up, out hit, 3f, -1);
            sb.AppendLine($"  {Path(e.transform)}  state={e.CurrentState} pos={e.transform.position}");
            sb.AppendLine($"     NavMeshAgent={(agent != null)}  " +
                          $"isOnNavMesh={(agent != null && agent.isOnNavMesh)}  " +
                          $"walkableWithin3m={near}  CharacterController={(cc != null)}");
        }

        foreach (var e in Object.FindObjectsOfType<Entity38EnemyAI>())
            sb.AppendLine($"  Entity38EnemyAI  {Path(e.transform)}  pos={e.transform.position}");

        sb.AppendLine("=== COMBAT CHAIN ===");
        foreach (var c in Object.FindObjectsOfType<PlayerCombatSystem>())
            sb.AppendLine($"  PlayerCombatSystem  {Path(c.transform)}");
        foreach (var h in Object.FindObjectsOfType<PlayerHealthHUD>())
            sb.AppendLine($"  PlayerHealthHUD  {Path(h.transform)}");
        foreach (var can in Object.FindObjectsOfType<Canvas>())
            sb.AppendLine($"  Canvas  {Path(can.transform)}  mode={can.renderMode} sort={can.sortingOrder}");

        sb.AppendLine("=== CHARACTER CONTROLLERS ===");
        foreach (var cc in Object.FindObjectsOfType<CharacterController>())
            sb.AppendLine($"  {Path(cc.transform)} r={cc.radius} h={cc.height} center={cc.center}");

        sb.AppendLine("=== COLLIDERS WITHOUT NavigationStatic (first 25) ===");
        int shown = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Collider>() == null || IsNavStatic(t.gameObject)) continue;
                sb.AppendLine($"   {Path(t)}  layer={t.gameObject.layer}");
                if (++shown >= 25) { sb.AppendLine("   ...(truncated)"); break; }
            }

        Debug.Log(sb.ToString());

        // The Console window collapses the report to its first line, so also
        // write it out in full for anyone reading the raw data.
        string outPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", "NavMeshReport.txt"));
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[NavMesh] Full report -> " + outPath);
    }

    // ---------------------------------------------------------------------
    // FIX - make floor geometry bakeable, then bake
    // ---------------------------------------------------------------------
    /// <summary>
    /// Marks every non-Player collider on layer 0 (Default) as NavigationStatic,
    /// then bakes. Static-marking is the only thing the bake reads, so this is
    /// additive: it adds the NavigationStatic bit and clears nothing else.
    /// </summary>
    [MenuItem("Tools/Setup/Mark Floor Static + Bake NavMesh")]
    public static void MarkStaticAndBake()
    {
        int marked = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            // Never static-mark the player or any enemy - they must stay dynamic.
            if (root.GetComponent<FirstPersonController>() != null
                || root.GetComponent<EnemyAI>() != null) continue;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Collider>() == null) continue;
                if (t.gameObject.layer != 0) continue;
                if (IsNavStatic(t.gameObject)) continue;

                t.gameObject.isStatic = true;
                SetStaticFlags(t.gameObject, GetStaticFlags(t.gameObject) | NavigationStaticMask);
                marked++;
            }
        }

        Debug.Log($"[NavMesh] Marked {marked} collider(s) as NavigationStatic. Baking...");
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        if (EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            Debug.Log("[NavMesh] Scene saved.");

        Report();
    }

    [MenuItem("Tools/Setup/Bake NavMesh (current scene)", priority = 1)]
    public static void BakeOnly()
    {
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        if (EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            Debug.Log("[NavMesh] Bake done, scene saved.");
        Report();
    }
}
