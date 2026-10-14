using GameNetcodeStuff;

namespace MuchBetterEmotes;

internal static class SignProp
{
    public static void OnStarted(PlayerControllerB player, int emote)
    {
        bool sign = emote == EmoteDefs.Sign || emote == EmoteDefs.SignShown;
        bool legs = emote == EmoteDefs.Prisyadka;

        if (sign)
        {
            Show(player);
        }
        else
        {
            Hide(player);
        }

        if (legs)
        {
            LegsView.Show(player);
        }
        else
        {
            LegsView.Hide(player);
        }

        if (sign || legs)
        {
            ArmsTarget.Free(player);
        }
        else
        {
            ArmsTarget.Restore(player);
        }
    }

    public static void OnEnded(PlayerControllerB player)
    {
        Hide(player);
        LegsView.Hide(player);
        ArmsTarget.Restore(player);
    }

    public static void Show(PlayerControllerB player)
    {
        SetRenderers(player, true);
        SignCanvases.Take(player);
    }

    public static void Hide(PlayerControllerB player)
    {
        SignCanvases.Return(player);
        SetRenderers(player, false);
    }

    private static void SetRenderers(PlayerControllerB player, bool on)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        if (rig == null) return;

        if (rig.Board != null)
        {
            rig.Board.enabled = on;
        }

        if (rig.Back != null)
        {
            rig.Back.enabled = on;
        }
    }
}
