using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MuchBetterEmotes;

internal sealed class EmoteWheel : MonoBehaviour
{
    private const float PageDelay = 0.1f;
    private const float ArrowSpeed = 20f;
    private const float MouseOffset = 3.3f;
    private const float StickOffset = 0.033f;
    private const string StopText = "Stop";

    private readonly string[] _infos = new string[EmoteDefs.SixSeven + 1];

    private bool _ready;
    private bool _open;
    private bool _onStick;
    private bool _hasSlot;
    private bool _stop;
    private bool _selectionShown;
    private int _page;
    private int _pageCount = 1;
    private int _block;
    private int _id;
    private int _openFrame;
    private float _angle;
    private float _pageTime;
    private Vector2 _mousePos;
    private Vector2 _stickPos;
    private Canvas _canvas = null!;
    private PlayerControllerB? _player;
    private QuickMenuManager _menu = null!;
    private RectTransform _selection = null!;
    private RectTransform _arrow = null!;
    private Text _info = null!;
    private Text _pageText = null!;
    private GameObject[] _pages = Array.Empty<GameObject>();
    private Image[] _slots = Array.Empty<Image>();
    private Sprite?[] _icons = Array.Empty<Sprite>();
    private int[] _layout = Array.Empty<int>();

    public static EmoteWheel? Current { get; private set; }

    private static bool StopOutside => ConfigManager.StopOutside.Value;

    public static void Create(Transform canvas)
    {
        GameObject? prefab = AssetsLoader.WheelPrefab;
        if (prefab == null) return;

        GameObject wheel = Instantiate(prefab, canvas);
        wheel.SetActive(false);
        Current = wheel.AddComponent<EmoteWheel>();
    }

    public void Open()
    {
        if (_open || LCVRCompat.InVR) return;

        PlayerControllerB? player = GameNetworkManager.Instance.localPlayerController;
        if (player == null || player.isPlayerDead || player.inTerminalMenu || player.isTypingChat
            || player.inSpecialMenu || player.disableInteract || DrawPanel.IsOpen)
        {
            return;
        }

        QuickMenuManager menu = player.quickMenuManager;
        if (menu.isMenuOpen) return;

        if (ShipBuildModeManager.Instance.InBuildMode) return;

        Setup();
        LoadInfos();
        LoadLayout();

        bool pad = StartOfRound.Instance.localPlayerUsingController;
        Mouse? mouse = Mouse.current;
        Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);

        _player = player;
        _menu = menu;
        _onStick = pad;
        _mousePos = pad && mouse != null ? mouse.position.ReadValue() : center;
        _stickPos = ReadStick();
        _hasSlot = false;
        _stop = false;
        _id = 0;
        _openFrame = Time.frameCount;
        _selectionShown = false;
        _selection.gameObject.SetActive(false);

        if (!pad)
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            HUDManager.Instance.SetMouseCursorSprite(null);
            if (mouse != null)
            {
                mouse.WarpCursorPosition(center);
            }
        }

        menu.isMenuOpen = true;
        _open = true;
        gameObject.SetActive(true);
    }

    public void Release()
    {
        if (!_open || !CheckMenu()) return;

        Refresh();
        PlayerControllerB? player = _player;
        int id = _id;
        bool stop = _stop;
        Close();

        if (player == null) return;

        switch (WheelMath.GetAction(stop, id, player.performingEmote))
        {
            case WheelAction.Stop:
                EmoteControl.StopLocal(player);
                break;
            case WheelAction.Vanilla:
                EmoteControl.TryVanilla(player, id);
                break;
            case WheelAction.Start:
                EmoteControl.TryStart(player, id);
                break;
        }
    }

    public void ChangePage(int step)
    {
        if (!_open || _pageCount < 2 || Time.unscaledTime - _pageTime < PageDelay) return;

        _pageTime = Time.unscaledTime;
        _page = (_page + step + _pageCount) % _pageCount;
        ShowPage();
    }

    private void Update()
    {
        if (!_open || !CheckMenu()) return;

        Refresh();
        UpdateView();
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Close()
    {
        _open = false;
        gameObject.SetActive(false);
        _menu.isMenuOpen = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private bool CheckMenu()
    {
        if (_player != null && !_player.isPlayerDead && _menu.isMenuOpen) return true;

        Close();
        return false;
    }

    private void Setup()
    {
        if (_ready) return;

        _canvas = GetComponentInParent<Canvas>(true);
        _selection = (RectTransform)transform.Find("SelectedEmote");
        _arrow = (RectTransform)transform.Find("Graphics/SelectionArrow");
        _info = transform.Find("Graphics/EmoteInfo").GetComponent<Text>();
        _pageText = transform.Find("Graphics/PageNumber").GetComponent<Text>();

        Transform content = transform.Find("FunctionalContent");
        _pages = new GameObject[content.childCount];
        List<Image> slots = new();
        for (int i = 0; i < _pages.Length; i++)
        {
            Transform page = content.GetChild(i);
            _pages[i] = page.gameObject;
            for (int j = 0; j < page.childCount; j++)
            {
                slots.Add(page.GetChild(j).GetComponent<Image>());
            }
        }

        _slots = slots.ToArray();
        _icons = new Sprite?[_slots.Length + 1];
        for (int i = 0; i < _slots.Length; i++)
        {
            _icons[i + 1] = _slots[i].sprite;
        }

        _ready = true;
    }

    private void LoadLayout()
    {
        _layout = WheelMath.Layout(ConfigManager.Enabled);
        _pageCount = WheelMath.PageCount(_layout.Length);
        _page = WheelMath.ClampPage(_page, _pageCount);
        for (int i = 0; i < _slots.Length; i++)
        {
            bool used = i < _layout.Length;
            _slots[i].gameObject.SetActive(used);
            if (used)
            {
                _slots[i].sprite = _icons[_layout[i]];
            }
        }

        ShowPage();
    }

    private void Refresh()
    {
        if (Time.frameCount == _openFrame) return;

        Vector2 pos = _mousePos;
        Mouse? mouse = Mouse.current;
        if (mouse != null)
        {
            pos = mouse.position.ReadValue();
            if (pos != _mousePos)
            {
                _mousePos = pos;
                _onStick = false;
            }
        }

        Vector2 stick = ReadStick();
        if (stick != _stickPos)
        {
            _stickPos = stick;
            _onStick = true;
        }

        float dx;
        float dy;
        float offset;
        WheelZone zone;
        if (_onStick)
        {
            dx = stick.x;
            dy = stick.y;
            offset = StickOffset;
            float deadzone = ConfigManager.Deadzone.Value;
            zone = (dx * dx) + (dy * dy) <= deadzone * deadzone ? WheelZone.Center : WheelZone.Ring;
        }
        else
        {
            dx = pos.x - (Screen.width * 0.5f);
            dy = pos.y - (Screen.height * 0.5f);
            offset = MouseOffset;
            zone = WheelMath.Zone(dx, dy, _canvas.scaleFactor);
        }

        if ((dx * dx) + (dy * dy) >= offset * offset)
        {
            _angle = WheelMath.Angle(dx, dy);
        }

        _id = 0;
        if (WheelMath.HasSlot(zone, StopOutside))
        {
            _block = WheelMath.Block(dx, dy);
            _id = WheelMath.Pick(_layout, _block, _page);
        }

        _stop = WheelMath.Stops(zone, _onStick, StopOutside, _id);
        _hasSlot = _id != 0;
    }

    private void UpdateView()
    {
        if (_hasSlot != _selectionShown)
        {
            _selectionShown = _hasSlot;
            _selection.gameObject.SetActive(_hasSlot);
        }

        if (_hasSlot)
        {
            _selection.localRotation = Quaternion.Euler(0f, 0f, WheelMath.Step * _block);
        }

        Quaternion target = Quaternion.Euler(0f, 0f, _angle - (WheelMath.Step * 2f));
        _arrow.localRotation = Quaternion.Lerp(_arrow.localRotation, target, Time.deltaTime * ArrowSpeed);
        bool stopping = _stop && _player != null && _player.performingEmote;
        string text = stopping ? StopText : _infos[_id];
        if (_info.text != text)
        {
            _info.text = text;
        }
    }

    private void ShowPage()
    {
        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].SetActive(i == _page);
        }

        _pageText.text = $"<color=#fe6b02><</color> Page {_page + 1}/{_pageCount} <color=#fe6b02>></color>";
    }

    private void LoadInfos()
    {
        for (int i = 0; i < _infos.Length; i++)
        {
            _infos[i] = EmoteDefs.Label(i) + "\n" + GetKeys(i);
        }
    }

    private static Vector2 ReadStick()
    {
        InputAction stick = InputHandlers.Inputs.WheelStick;
        InputControl? active = stick.activeControl;
        if (active != null && active is not InputControl<Vector2>) return Vector2.zero;

        return stick.ReadValue<Vector2>();
    }

    private static string GetKeys(int id)
    {
        InputAction? action = InputHandlers.GetAction(id);
        if (action == null) return "";

        string keyboard = InputHandlers.GetKey(action, 0);
        string pad = InputHandlers.GetKey(action, 1).Replace("Left Stick", "LS").Replace("Right Stick", "RS");
        if (keyboard.Length == 0) return pad.Length == 0 ? "" : $"[{pad}]";

        return pad.Length == 0 ? $"[{keyboard}]" : $"[{keyboard}/{pad}]";
    }
}
