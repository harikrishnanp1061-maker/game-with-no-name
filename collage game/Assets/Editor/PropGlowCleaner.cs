using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Removes the runaway emissive glow that PolyhavenPropPlacer.ApplyRedGlow
/// stamped onto every prop.
///
/// The placer's heuristic ("highest + smallest renderer is probably a bulb")
/// is fine for a lamp, but it was applied unconditionally, so props with no
/// light source at all - stone_01, dead_tree_trunk_02, rock_moss_set_01, a
/// chair, a desk - ended up with _EmissionColor = (3.2, 0.38, 0.19). At that
/// intensity the emission saturates every channel and the albedo is invisible,
/// so 1352 of 1784 prop renderers rendered as flat orange silhouettes.
///
/// This pass keeps emission only where it belongs: the handful of renderer
/// names that really are a bulb, lamp glass or fixture, and only when the
/// material has no emission map of its own.
/// </summary>
public static class PropGlowCleaner
{
    /// <summary>Renderer names that are genuinely a light-emitting surface.
    /// Matched as substrings so multi-part fixtures (industrial_wall_lamp_glass,
    /// mounted_fluorescent_lights_tube) are all covered.</summary>
    static readonly string[] EmissiveNames =
    {
        "bulb", "lamp_glass", "light_glass", "sconce_light", "glass",
        "tube", "light_bulb", "emissive",
    };

    /// <summary>Prop roots that should keep NO emission at all.</summary>
    static readonly string[] NeverEmissive =
    {
        "stone", "rock_moss", "dead_tree", "trashbag", "crowbar",
        "chair", "desk", "table", "shelf", "bookshelf", "cabinet",
        "toolbox", "chalkboard", "picture_frame", "wall_clock", "clock",
        "barrel", "cement_bag", "suitcase", "stapler", "laptop",
        "chemistry_set", "office_notepads", "stationery_supplies",
        "spray_paint", "wooden_", "GothicCabinet", "SchoolChair", "SchoolDesk",
        "Shelf_01", "Barrel", "crane", "ladder", "handrail", "pipe",
    };

    [MenuItem("Tools/Props/Clean Runaway Emission")]
    public static void Clean()
    {
        var props = GameObject.Find("Props");
        if (props == null)
        {
            Debug.LogError("[GlowClean] no 'Props' root in the scene.");
            return;
        }

        // Collect the materials that legitimately glow, by renderer name.
        var keep = new HashSet<Material>();
        foreach (var r in props.GetComponentsInChildren<MeshRenderer>(true))
        {
            var n = r.name.ToLowerInvariant();
            if (!Matches(n, EmissiveNames)) continue;
            foreach (var m in r.sharedMaterials)
                if (m != null) keep.Add(m);
        }

        int cleared = 0, leftOn = 0, seen = 0;
        var touched = new List<Material>();

        foreach (var r in props.GetComponentsInChildren<MeshRenderer>(true))
        {
            var propName = r.name.ToLowerInvariant();
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (!m.HasProperty("_EmissionColor")) continue;
                seen++;

                var c = m.GetColor("_EmissionColor");
                if (c.maxColorComponent <= 0.001f) continue;   // already off

                bool blocked = Matches(propName, NeverEmissive);
                bool wanted = keep.Contains(m) && !blocked;

                if (wanted) { leftOn++; continue; }

                // Kill the emission, but keep a real emission map intact - some
                // fixtures ship an authored emissive texture.
                bool hasEmissionMap = m.GetTexture("_EmissionMap") != null;
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

                if (hasEmissionMap)
                {
                    // Restore a modest glow driven by the authored map.
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1.6f, 1.3f, 0.9f, 1f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                cleared++;
                if (!touched.Contains(m)) touched.Add(m);
            }
        }

        foreach (var m in touched) EditorUtility.SetDirty(m);
        UnityEditor.SceneManagement.EditorSceneManager
            .MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log(string.Format(
            "[GlowClean] slots examined={0}  emission turned OFF={1}  left ON={2}  materials changed={3}",
            seen, cleared, leftOn, touched.Count));
    }

    static bool Matches(string name, string[] patterns)
    {
        foreach (var p in patterns)
            if (name.Contains(p)) return true;
        return false;
    }
}