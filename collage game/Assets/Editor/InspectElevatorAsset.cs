using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class InspectElevatorAsset
{
    static InspectElevatorAsset()
    {
        EditorApplication.delayCall += RunInspection;
    }

    private static void RunInspection()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== ASSET INSPECTION REPORT ===");
        sb.AppendLine("Time: " + System.DateTime.Now);

        string elevPath = "Assets/ReadyMadeAssets/Elevator/Elevator.fbx";
        GameObject elevPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(elevPath);
        sb.AppendLine("\n--- ELEVATOR PREFAB: " + (elevPrefab != null ? elevPrefab.name : "NULL") + " ---");
        if (elevPrefab != null)
        {
            DumpHierarchy(elevPrefab.transform, sb, 0);
        }

        Object[] elevAssets = AssetDatabase.LoadAllAssetsAtPath(elevPath);
        sb.AppendLine("\nSub-assets in Elevator.fbx: " + elevAssets.Length);
        foreach (var a in elevAssets)
        {
            if (a is AnimationClip clip)
            {
                sb.AppendLine("  AnimationClip: " + clip.name + " (len: " + clip.length + "s, wrap: " + clip.wrapMode + ", isLoop: " + clip.isLooping + ")");
            }
            else if (a is Material mat)
            {
                sb.AppendLine("  Material: " + mat.name + " (shader: " + (mat.shader != null ? mat.shader.name : "null") + ")");
            }
        }

        string fusePath = "Assets/ReadyMadeAssets/FuseCell/SM_FuseCell.fbx";
        GameObject fusePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fusePath);
        sb.AppendLine("\n--- FUSE CELL PREFAB: " + (fusePrefab != null ? fusePrefab.name : "NULL") + " ---");
        if (fusePrefab != null)
        {
            DumpHierarchy(fusePrefab.transform, sb, 0);
        }

        Object[] fuseAssets = AssetDatabase.LoadAllAssetsAtPath(fusePath);
        sb.AppendLine("\nSub-assets in SM_FuseCell.fbx: " + fuseAssets.Length);
        foreach (var a in fuseAssets)
        {
            if (a is AnimationClip clip)
            {
                sb.AppendLine("  AnimationClip: " + clip.name + " (len: " + clip.length + "s, wrap: " + clip.wrapMode + ", isLoop: " + clip.isLooping + ")");
            }
            else if (a is Material mat)
            {
                sb.AppendLine("  Material: " + mat.name + " (shader: " + (mat.shader != null ? mat.shader.name : "null") + ")");
            }
        }

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets/inspect_output.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[InspectElevatorAsset] Saved report to " + outPath);
    }

    private static void DumpHierarchy(Transform t, StringBuilder sb, int indent)
    {
        string prefix = new string(' ', indent * 2);
        Renderer r = t.GetComponent<Renderer>();
        string rendInfo = r != null ? $" [Rend: {r.sharedMaterials.Length} mats]" : "";
        sb.AppendLine($"{prefix}- {t.name} (pos: {t.localPosition}, rot: {t.localEulerAngles}, scale: {t.localScale}){rendInfo}");
        for (int i = 0; i < t.childCount; i++)
        {
            DumpHierarchy(t.GetChild(i), sb, indent + 1);
        }
    }
}
