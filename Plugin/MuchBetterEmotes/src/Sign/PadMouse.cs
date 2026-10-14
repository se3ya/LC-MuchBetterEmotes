using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MuchBetterEmotes;

[Flags]
internal enum PadKeys
{
    None = 0,
    Undo = 1,
    Redo = 2,
    SizeDown = 4,
    SizeUp = 8,
    Tool = 16,
    SwatchPrev = 32,
    SwatchNext = 64
}

internal sealed class PadMouse
{
    private const float BaseHeight = 1080f;
    private const float MaxStep = 0.1f;

    private Mouse? _mouse;
    private Vector2 _pos;
    private double _last;
    private bool _open;
    private bool _settle;
    private bool _recent;

    public bool Active { get; private set; }

    public bool Hint => Active || _recent;

    public PadKeys Keys { get; private set; }

    public Vector2 Position => _pos;

    public void Open()
    {
        _open = true;
        Active = false;
        _recent = StartOfRound.Instance.localPlayerUsingController;
        Keys = PadKeys.None;
    }

    public void Close()
    {
        if (Active) StartOfRound.Instance.localPlayerUsingController = true;

        _open = false;
        Active = false;
        _recent = false;
        Keys = PadKeys.None;
        if (_mouse == null || _settle) return;

        _settle = true;
        Application.onBeforeRender += OnSettle;
    }

    public void Destroy()
    {
        _open = false;
        Active = false;
        _recent = false;
        Keys = PadKeys.None;
        InputSystem.onAfterUpdate -= OnAfterUpdate;
        if (_settle) Application.onBeforeRender -= OnSettle;
        _settle = false;
        Mouse? mouse = _mouse;
        _mouse = null;
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
    }

    private void OnSettle()
    {
        Application.onBeforeRender -= OnSettle;
        _settle = false;
        if (_open || _mouse == null || !_mouse.added) return;

        SetButton(false);
        Mouse? real = FindReal();
        InputSystem.DisableDevice(_mouse);
        if (real != null && Mouse.current == _mouse) real.MakeCurrent();
    }

    private void Create()
    {
        _mouse = InputSystem.AddDevice<Mouse>("MuchBetterEmotesPad");
        InputSystem.onAfterUpdate += OnAfterUpdate;
    }

    public Mouse? Pick(Mouse? fallback)
    {
        if (_mouse == null) return fallback;
        if (Active) return _mouse;
        if (fallback != _mouse) return fallback;

        return FindReal();
    }

    private Mouse? FindReal()
    {
        for (int i = 0; i < InputSystem.devices.Count; i++)
        {
            if (InputSystem.devices[i] is Mouse other && other != _mouse) return other;
        }

        return null;
    }

    public void Poll()
    {
        Keys = PadKeys.None;
        Gamepad? pad = Gamepad.current;
        if (!_open || pad == null)
        {
            if (Active) SetActive(false);
            _recent = false;
            return;
        }

        float deadzone = ConfigManager.Deadzone.Value;
        Vector2 stick = pad.leftStick.ReadValue();
        bool padInput = stick.sqrMagnitude > deadzone * deadzone || AnyButton(pad);
        if (padInput)
        {
            if (_mouse == null) Create();
            else if (!_mouse.enabled) InputSystem.EnableDevice(_mouse);
            SetActive(true);
        }
        else if (Hint && RealInput())
        {
            SetActive(false);
            _recent = false;
        }

        if (!Active) return;

        if (pad.leftShoulder.wasPressedThisFrame) Keys |= PadKeys.Undo;
        if (pad.rightShoulder.wasPressedThisFrame) Keys |= PadKeys.Redo;
        if (pad.leftTrigger.wasPressedThisFrame) Keys |= PadKeys.SizeDown;
        if (pad.rightTrigger.wasPressedThisFrame) Keys |= PadKeys.SizeUp;
        if (pad.buttonNorth.wasPressedThisFrame) Keys |= PadKeys.Tool;
        if (pad.dpad.left.wasPressedThisFrame) Keys |= PadKeys.SwatchPrev;
        if (pad.dpad.right.wasPressedThisFrame) Keys |= PadKeys.SwatchNext;
    }

    private static bool AnyButton(Gamepad pad)
    {
        return pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
            || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame
            || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;
    }

    private bool RealInput()
    {
        Keyboard? keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        for (int i = 0; i < InputSystem.devices.Count; i++)
        {
            if (InputSystem.devices[i] is not Mouse mouse || mouse == _mouse) continue;

            if (mouse.delta.ReadValue() != Vector2.zero || mouse.leftButton.wasPressedThisFrame
                || mouse.scroll.ReadValue() != Vector2.zero)
            {
                return true;
            }
        }

        return false;
    }

    private void SetActive(bool on)
    {
        if (on == Active) return;

        Active = on;
        if (_mouse == null) return;

        if (on)
        {
            Mouse? real = FindReal();
            _pos = real != null ? real.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _last = 0.0;
            InputState.Change(_mouse.position, _pos);
        }
        else
        {
            SetButton(false);
        }
    }

    private void SetButton(bool down)
    {
        if (_mouse == null || _mouse.leftButton.isPressed == down) return;

        _mouse.CopyState<MouseState>(out MouseState state);
        state.WithButton(MouseButton.Left, down);
        InputState.Change(_mouse, state);
    }

    private void OnAfterUpdate()
    {
        Gamepad? pad = Gamepad.current;
        if (!Active || !_open || _mouse == null || pad == null) return;

        double now = InputState.currentTime;
        float dt = _last <= 0.0 ? 0f : Mathf.Min((float)(now - _last), MaxStep);
        _last = now;
        Vector2 stick = pad.leftStick.ReadValue();
        PadMath.Velocity(stick.x, stick.y, ConfigManager.Deadzone.Value, out float vx, out float vy);
        float scale = Screen.height / BaseHeight;
        Vector2 step = new(vx * scale * dt, vy * scale * dt);
        if (step != Vector2.zero)
        {
            _pos.x = Mathf.Clamp(_pos.x + step.x, 0f, Screen.width - 1f);
            _pos.y = Mathf.Clamp(_pos.y + step.y, 0f, Screen.height - 1f);
            InputState.Change(_mouse.position, _pos);
            InputState.Change(_mouse.delta, step);
        }

        SetButton(pad.buttonSouth.isPressed);
    }
}
