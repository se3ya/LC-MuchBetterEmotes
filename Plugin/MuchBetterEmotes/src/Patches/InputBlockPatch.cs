using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace MuchBetterEmotes;

[HarmonyPatch]
internal static class InputBlockPatch
{
    [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "ItemSecondaryUse_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "ItemTertiaryUse_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "QEItemInteract_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "ActivateItem_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "ActivateItem_canceled")]
    [HarmonyPatch(typeof(PlayerControllerB), "Discard_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "UseUtilitySlot_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "InspectItem_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "OpenMenu_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "Jump_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "Crouch_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "Emote1_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "Emote2_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "SetFreeCamera_performed")]
    [HarmonyPatch(typeof(PlayerControllerB), "SpeedCheat_performed")]
    [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
    [HarmonyPatch(typeof(HUDManager), "EnableChat_performed")]
    [HarmonyPatch(typeof(HUDManager), "OpenMenu_performed")]
    [HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
    [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
    [HarmonyPatch(typeof(ShipBuildModeManager), "EnterBuildMode")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockInput()
    {
        return !(DrawPanel.IsOpen || DrawPanel.ClosedFrame == Time.frameCount);
    }

    [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockLook()
    {
        return !DrawPanel.IsOpen;
    }
}
