using System;
using GameNetcodeStuff;
using UnityEngine.InputSystem;

namespace MuchBetterEmotes;

internal static class EmoteControl
{
    public static Func<PlayerControllerB, bool> Blocked = _ => false;

    public static event Action<PlayerControllerB, int>? Started;
    public static event Action<PlayerControllerB>? Ended;

    public static bool TryStart(PlayerControllerB? player, int pressed)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        if (player == null || rig == null || !rig.Built) return false;

        bool show = pressed == EmoteDefs.SignShown;
        bool typingOk = show && rig.Current == EmoteDefs.Sign;
        if (!CanStart(player, typingOk, show ? 0f : Constants.Emotes.Cooldown, true)) return false;

        if (pressed == EmoteDefs.Sign && IsUsingItem()) return false;

        int next = EmoteDefs.Next(rig.Current, pressed);
        if (next == -1 || !EmoteDefs.TryGet(next, out _) || !ConfigManager.Enabled(next)) return false;

        if (!CanPerform(player, rig, next)) return false;

        player.timeSinceStartingEmote = 0f;
        player.performingEmote = true;
        player.playerBodyAnimator.SetInteger("emoteNumber", 1);
        player.StartPerformingEmoteServerRpc();

        rig.Play(next);
        SendStart(GetIndex(player), next);
        Started?.Invoke(player, next);
        return true;
    }

    public static bool TryVanilla(PlayerControllerB player, int id)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        if (rig == null || !CanStart(player, false, Constants.Emotes.Cooldown, false)) return false;

        if (!CanPerform(player, rig, id)) return false;

        player.timeSinceStartingEmote = 0f;
        player.performingEmote = true;
        player.playerBodyAnimator.SetInteger("emoteNumber", id);
        player.StartPerformingEmoteServerRpc();
        OnVanillaStart(player);
        return true;
    }

    public static void StopLocal(PlayerControllerB player)
    {
        if (EmoteRig.Of(player) == null) return;

        player.performingEmote = false;
        player.timeSinceStartingEmote = 0f;
        player.StopPerformingEmoteServerRpc();
    }

    public static void OnRemoteStart(PlayerControllerB player, int emote)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        if (rig == null || !rig.Built) return;

        if (emote == 0)
        {
            End(player, FadeMode.Decay);
            return;
        }

        player.performingEmote = true;
        rig.Play(emote);
        Started?.Invoke(player, emote);
    }

    public static void OnVanillaStart(PlayerControllerB player)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        if (rig == null || rig.Current == 0) return;

        End(player, FadeMode.Decay);
        SendStart(GetIndex(player), 0);
    }

    public static void End(PlayerControllerB player, FadeMode mode)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        if (rig == null || rig.Current == 0) return;

        rig.Stop();
        rig.Mode = mode;
        Ended?.Invoke(player);
    }

    public static bool IsLocal(PlayerControllerB? player)
    {
        return player != null && player == GameNetworkManager.Instance.localPlayerController;
    }

    public static int GetIndex(PlayerControllerB player)
    {
        return Array.IndexOf(StartOfRound.Instance.allPlayerScripts, player);
    }

    public static int GetLocalIndex()
    {
        PlayerControllerB? local = GameNetworkManager.Instance.localPlayerController;
        return local != null ? GetIndex(local) : -1;
    }

    public static PlayerControllerB? GetPlayer(int index)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        return index >= 0 && index < players.Length ? players[index] : null;
    }

    private static void SendStart(int index, int emote)
    {
        try
        {
            EmoteSync.SendStart(index, emote);
        }
        catch (Exception ex)
        {
            DrawSync.Report(ex);
        }
    }

    private static bool IsUsingItem()
    {
        InputAction? use = InputSystem.actions.FindAction("ActivateItem");
        return use != null && use.IsPressed();
    }

    private static bool CanStart(PlayerControllerB player, bool typingOk, float cooldown, bool ownEmote)
    {
        if (!player.IsOwner || !player.isPlayerControlled || (player.IsServer && !player.isHostPlayerObject)) return false;
        if (player.isPlayerDead || player.inTerminalMenu || (player.isTypingChat && !typingOk)) return false;
        if (player.quickMenuManager.isMenuOpen || player.inSpecialMenu) return false;

        bool blocked = ownEmote && (player.disableInteract || Blocked(player));
        return !blocked && player.timeSinceStartingEmote >= cooldown;
    }

    private static bool CanPerform(PlayerControllerB player, EmoteRig rig, int id)
    {
        rig.Requested = id;
        try
        {
            return player.CheckConditionsForEmote();
        }
        finally
        {
            rig.Requested = 0;
        }
    }
}
