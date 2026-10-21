using UnityEngine;
using UnityEditor;

public static class TestEditorPing
{
    [InitializeOnLoadMethod]
    private static void Ping()
    {
        Debug.Log("[TestEditorPing] Unity Editor successfully reloaded and pinged at " + System.DateTime.Now);
    }
}
