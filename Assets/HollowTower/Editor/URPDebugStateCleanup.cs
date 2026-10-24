using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine.Rendering;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// After the HDRP package was removed, the Rendering Debugger's in-memory state (DebugDisplaySerializer, which
    /// survives domain reloads) came back with null entries where HDRP's debug settings used to be. URP's pipeline
    /// constructor trips over them (a NullReferenceException, then "Blitter is already initialized" on every retry),
    /// so nothing renders. This drops the null entries right after each domain reload, before the pipeline is created.
    /// The state is memory-only, so after an editor restart there is nothing left to clean and this does nothing.
    /// </summary>
    [InitializeOnLoad]
    static class URPDebugStateCleanup
    {
        static URPDebugStateCleanup()
        {
            var field = typeof(DebugDisplaySerializer).GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic);
            var settings = field != null ? field.GetValue(DebugDisplaySerializer.instance) as IList : null;
            if (settings == null) return;
            for (int i = settings.Count - 1; i >= 0; i--)
                if (settings[i] == null) settings.RemoveAt(i);
        }
    }
}
