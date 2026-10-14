using GameNetcodeStuff;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class ArmsTarget
{
    private static Transform? _original;
    private static Transform? _free;

    public static void Free(PlayerControllerB player)
    {
        if (!EmoteControl.IsLocal(player)) return;

        if (_original == null)
        {
            _original = player.localArmsRotationTarget;
        }

        if (_free == null)
        {
            Transform? parent = _original.parent != null ? _original.parent.parent : null;
            if (parent == null) return;
            _free = Object.Instantiate(_original, parent);
        }
        player.localArmsRotationTarget = _free;
    }

    public static void Restore(PlayerControllerB player)
    {
        if (EmoteControl.IsLocal(player) && _original != null)
        {
            player.localArmsRotationTarget = _original;
        }
    }
}
