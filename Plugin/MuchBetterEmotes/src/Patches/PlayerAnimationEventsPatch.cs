using GameNetcodeStuff;
using HarmonyLib;

namespace MuchBetterEmotes;

[HarmonyPatch(typeof(PlayerAnimationEvents))]
internal static class PlayerAnimationEventsPatch
{
    [HarmonyPatch(nameof(PlayerAnimationEvents.PlayFootstepLocal))]
    [HarmonyPrefix]
    private static bool SkipLocalStep(PlayerAnimationEvents __instance) => !HasEmoteSteps(__instance.thisPlayerController);

    [HarmonyPatch(nameof(PlayerAnimationEvents.PlayFootstepServer))]
    [HarmonyPrefix]
    private static bool SkipServerStep(PlayerAnimationEvents __instance) => !HasEmoteSteps(__instance.thisPlayerController);

    private static bool HasEmoteSteps(PlayerControllerB player)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        return rig != null && EmoteDefs.LoopsSteps(rig.Current) && player.performingEmote;
    }
}
