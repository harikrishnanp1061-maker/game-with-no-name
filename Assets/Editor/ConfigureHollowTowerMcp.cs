using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport.Transports;
using MCPForUnity.Editor.Helpers;

namespace HollowTower.EditorSetup
{
    [InitializeOnLoad]
    public static class ConfigureHollowTowerMcp
    {
        static ConfigureHollowTowerMcp()
        {
            try
            {
                File.AppendAllText("mcp_boot.log", $"[{DateTime.UtcNow}] Static constructor called\n");
                InitializeBridge();
            }
            catch (Exception ex)
            {
                File.AppendAllText("mcp_boot.log", $"[{DateTime.UtcNow}] Ctor error: {ex}\n");
            }
        }

        [MenuItem("Tools/Start MCP Stdio Bridge")]
        public static void InitializeBridge()
        {
            try
            {
                EditorConfigurationCache.Instance.PinStdioForSession();
                EditorConfigurationCache.Instance.SetUseHttpTransport(false);
                
                File.AppendAllText("mcp_boot.log", $"[{DateTime.UtcNow}] Calling StdioBridgeHost.StartAutoConnect...\n");
                StdioBridgeHost.StartAutoConnect();
                File.AppendAllText("mcp_boot.log", $"[{DateTime.UtcNow}] StdioBridgeHost port: {StdioBridgeHost.GetCurrentPort()}, running: {StdioBridgeHost.IsRunning}\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText("mcp_boot.log", $"[{DateTime.UtcNow}] Error: {ex}\n");
            }
        }
    }
}
