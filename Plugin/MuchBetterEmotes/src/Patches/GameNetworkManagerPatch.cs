using System;
using HarmonyLib;

namespace MuchBetterEmotes;

[HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect))]
internal static class GameNetworkManagerPatch
{
    [HarmonyPrefix]
    private static void OnDisconnect()
    {
        try
        {
            DrawPanel? panel = DrawPanel.Current;
            if (panel != null)
            {
                panel.Close();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to close the drawing panel on disconnect: {ex}");
        }

        try
        {
            DrawSync.Reset();
            SignCanvases.DestroyAll();
            SignCanvases.ClearAll();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to reset the signs on disconnect: {ex}");
        }
    }
}
