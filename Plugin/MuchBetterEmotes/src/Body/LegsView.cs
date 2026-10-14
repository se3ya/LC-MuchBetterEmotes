using GameNetcodeStuff;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class LegsView
{
    public static void Show(PlayerControllerB player)
    {
        if (!EmoteControl.IsLocal(player)) return;

        Renderer? legs = GetLegs(player);
        if (legs == null) return;

        legs.sharedMaterial = player.thisPlayerModel.sharedMaterial;
        legs.forceRenderingOff = false;
    }

    public static void Hide(PlayerControllerB player)
    {
        Renderer? legs = GetLegs(player);
        if (legs != null)
        {
            legs.forceRenderingOff = true;
        }
    }

    private static Renderer? GetLegs(PlayerControllerB player)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        return rig != null ? rig.Legs : null;
    }
}
