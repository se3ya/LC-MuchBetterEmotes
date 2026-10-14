using LethalCompanyInputUtils.Api;
using UnityEngine.InputSystem;

namespace MuchBetterEmotes;

internal sealed class EmoteInputs : LcInputActions
{
    [InputAction("", Name = "Middle Finger")]
    public InputAction MiddleFinger { get; set; } = null!;

    [InputAction("", Name = "Clap")]
    public InputAction Clap { get; set; } = null!;

    [InputAction("", Name = "Shy")]
    public InputAction Shy { get; set; } = null!;

    [InputAction("", Name = "The Griddy")]
    public InputAction Griddy { get; set; } = null!;

    [InputAction("", Name = "Twerk")]
    public InputAction Twerk { get; set; } = null!;

    [InputAction("", Name = "Salute")]
    public InputAction Salute { get; set; } = null!;

    [InputAction("", Name = "Prisyadka")]
    public InputAction Prisyadka { get; set; } = null!;

    [InputAction("", Name = "Sign")]
    public InputAction Sign { get; set; } = null!;

    [InputAction("", Name = "Thumbs Up")]
    public InputAction ThumbsUp { get; set; } = null!;

    [InputAction("", Name = "Wave")]
    public InputAction Wave { get; set; } = null!;

    [InputAction("", Name = "6 7")]
    public InputAction SixSeven { get; set; } = null!;

    [InputAction("<Keyboard>/v", GamepadPath = "<Gamepad>/select", Name = "Emote Wheel")]
    public InputAction Wheel { get; set; } = null!;

    [InputAction("<Mouse>/scroll/up", GamepadPath = "<Gamepad>/dpad/right", Name = "Next Wheel Page")]
    public InputAction WheelNext { get; set; } = null!;

    [InputAction("<Mouse>/scroll/down", GamepadPath = "<Gamepad>/dpad/left", Name = "Previous Wheel Page")]
    public InputAction WheelPrevious { get; set; } = null!;

    [InputAction("", GamepadPath = "<Gamepad>/rightStick", ActionType = InputActionType.Value, Name = "Wheel Stick")]
    public InputAction WheelStick { get; set; } = null!;

    [InputAction("<Keyboard>/enter", GamepadPath = "<Gamepad>/buttonWest", Name = "Show Sign")]
    public InputAction SignShow { get; set; } = null!;

    [InputAction("<Keyboard>/escape", GamepadPath = "<Gamepad>/buttonEast", Name = "Put Away Sign")]
    public InputAction SignAway { get; set; } = null!;
}
