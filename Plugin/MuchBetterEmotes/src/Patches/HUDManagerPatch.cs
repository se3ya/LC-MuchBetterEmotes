using System;
using HarmonyLib;
using UnityEngine;

namespace MuchBetterEmotes;

[HarmonyPatch(typeof(HUDManager))]
internal static class HUDManagerPatch
{
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void CreateUi(HUDManager __instance)
    {
        try
        {
            Transform canvas = __instance.HUDContainer.transform.parent;
            DrawPanel.Create(canvas, __instance.chatText.font);
            EmoteWheel.Create(canvas);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to create the HUD: {ex}");
        }
    }
}
