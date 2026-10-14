using GameNetcodeStuff;
using HarmonyLib;

namespace MuchBetterEmotes;

[HarmonyPatch(typeof(StartOfRound))]
internal static class StartOfRoundPatch
{
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void OnStart(StartOfRound __instance)
    {
        SignCanvases.ClearAll();
        if (AssetsLoader.CanDraw)
        {
            SignPainter.Create(__instance);
        }
    }

    [HarmonyPatch("OnPlayerConnectedClientRpc")]
    [HarmonyPostfix]
    private static void OnConnect()
    {
        DrawSync.OnJoin();
    }

    [HarmonyPatch("OnPlayerDC")]
    [HarmonyPostfix]
    private static void ClearPlayer(int playerObjectNumber)
    {
        EndEmote(playerObjectNumber);
        SignCanvases.ClearSlot(playerObjectNumber);
    }

    private static void EndEmote(int index)
    {
        PlayerControllerB? player = EmoteControl.GetPlayer(index);
        if (player == null) return;

        EmoteControl.End(player, FadeMode.Decay);
        player.performingEmote = false;
    }
}
