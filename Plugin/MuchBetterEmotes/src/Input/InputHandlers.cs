using System;
using UnityEngine.InputSystem;

namespace MuchBetterEmotes;

internal static class InputHandlers
{
    public static EmoteInputs Inputs { get; private set; } = null!;

    public static void Init()
    {
        Inputs = new EmoteInputs();
        for (int id = EmoteDefs.MiddleFinger; id <= EmoteDefs.SixSeven; id++)
        {
            int emote = id;
            GetAction(emote)!.performed += _ => StartEmote(emote);
        }

        Inputs.Wheel.started += _ => UseWheel(wheel => wheel.Open());
        Inputs.Wheel.canceled += _ => UseWheel(wheel => wheel.Release());
        Inputs.WheelNext.performed += _ => UseWheel(wheel => wheel.ChangePage(1));
        Inputs.WheelPrevious.performed += _ => UseWheel(wheel => wheel.ChangePage(-1));
        Inputs.SignShow.performed += _ => DrawPanel.OnShowKey();
        Inputs.SignAway.performed += _ => DrawPanel.OnAwayKey();
    }

    public static string GetKey(InputAction action, int index)
    {
        return index < action.bindings.Count ? action.GetBindingDisplayString(index) ?? "" : "";
    }

    public static InputAction? GetAction(int id)
    {
        if (id == EmoteDefs.Dance) return InputSystem.actions.FindAction("Emote1");

        if (id == EmoteDefs.Point) return InputSystem.actions.FindAction("Emote2");

        return id switch
        {
            EmoteDefs.MiddleFinger => Inputs.MiddleFinger,
            EmoteDefs.Clap => Inputs.Clap,
            EmoteDefs.Shy => Inputs.Shy,
            EmoteDefs.Griddy => Inputs.Griddy,
            EmoteDefs.Twerk => Inputs.Twerk,
            EmoteDefs.Salute => Inputs.Salute,
            EmoteDefs.Prisyadka => Inputs.Prisyadka,
            EmoteDefs.Sign => Inputs.Sign,
            EmoteDefs.ThumbsUp => Inputs.ThumbsUp,
            EmoteDefs.Wave => Inputs.Wave,
            EmoteDefs.SixSeven => Inputs.SixSeven,
            _ => null
        };
    }

    private static void StartEmote(int id)
    {
        EmoteControl.TryStart(GameNetworkManager.Instance.localPlayerController, id);
    }

    private static void UseWheel(Action<EmoteWheel> action)
    {
        EmoteWheel? wheel = EmoteWheel.Current;
        if (wheel != null) action(wheel);
    }
}
