using System;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace MuchBetterEmotes;

internal sealed class DrawPanel : MonoBehaviour
{
    private const float HeightShare = 0.9f;
    private const float WidthShare = 0.96f;
    private const string ClearText = "Clear";
    private const string FullText = "full";
    private const string PickHint = "[LMB] Pick a colour  [RMB] or [{0}] Cancel";
    private const string MousePickHint = "[LMB] Pick a colour  [RMB] Cancel";
    private const string TextHint = "Type then click the sign to place it";
    private const string GalleryHint = "Save to a slot or load one onto the sign";
    private const string KeyDrawHint = "[LMB] Draw  [Wheel] Size  [Ctrl+Z] Undo  [Ctrl+Y] Redo";
    private const string PadDrawHint = "[A] Draw  [LB] Undo  [RB] Redo  [LT/RT] Size  [Y] Tool";
    private const string PadPickHint = "[A] Pick a colour";
    private const string TypeHint = "Type a hex colour  [Enter] Apply  [Esc] Cancel";

    private static readonly Pen _pen = new();

    private bool _open;
    private bool _disabling;
    private int _index;
    private int _closedFrame = -1;
    private int _shownCount = -1;
    private float _nextFlush;
    private Vector2 _laidOut;
    private HintMode _hintMode = HintMode.Draw;
    private bool _hintPad;
    private readonly PadMouse _pad = new();
    private RectTransform _padCursor = null!;
    private PanelTools _tools = null!;
    private GalleryTools _gallery = null!;
    private KeyControl? _undoKey;
    private KeyControl? _redoKey;
    private Canvas _canvas = null!;
    private RectTransform _canvasRect = null!;
    private RectTransform _content = null!;
    private RectTransform _viewRect = null!;
    private RawImage _view = null!;
    private Choice _clear = null!;
    private TMP_Text _counter = null!;
    private TMP_Text _hint = null!;
    private RectTransform _inkRect = null!;
    private RectTransform _inkFill = null!;
    private PlayerControllerB? _player;
    private EmoteRig? _rig;

    public static DrawPanel? Current { get; private set; }

    public static event Action<int>? Closed;

    public static bool IsOpen => Current != null && Current._open;

    public static int ClosedFrame => Current != null ? Current._closedFrame : -1;

    public static Pen Pen => _pen;

    public bool Typing => _tools.Typing;

    private DrawLog Log => SignCanvases.Book.Get(_index);

    public static void Create(Transform parent, TMP_FontAsset? font)
    {
        if (!AssetsLoader.CanDraw) return;

        GameObject root = UnityEngine.Object.Instantiate(AssetsLoader.PanelPrefab!, parent);
        root.SetActive(false);
        DrawPanel panel = root.AddComponent<DrawPanel>();
        Current = panel;
        try
        {
            panel.Bind(font);
        }
        catch (Exception ex)
        {
            Current = null;
            Destroy(root);
            Plugin.Log.LogError($"Failed to create the drawing panel: {ex}");
        }
    }

    public static void OnShowKey()
    {
        DrawPanel? panel = Current;
        if (panel != null && !panel.Typing) panel.ShowSign();
    }

    public static void OnAwayKey()
    {
        DrawPanel? panel = Current;
        if (panel != null && !panel.Typing && !panel.CloseGallery() && !panel.CancelPick()) panel.PutAway();
    }

    public static void OnStarted(PlayerControllerB player, int emote)
    {
        if (!EmoteControl.IsLocal(player)) return;

        DrawSync.TrySend(player, emote);

        DrawPanel? panel = Current;
        if (emote != EmoteDefs.Sign)
        {
            if (panel != null)
            {
                panel.Close();
            }

            return;
        }

        if (panel != null)
        {
            panel.Open(player);
        }
    }

    public static void OnEnded(PlayerControllerB player)
    {
        DrawPanel? panel = Current;
        if (panel != null && player == panel._player)
        {
            panel.Close();
        }
    }

    public void Open(PlayerControllerB player)
    {
        if (_open) return;

        int index = EmoteControl.GetIndex(player);
        try
        {
            SignCanvases.Take(player);
            Texture2D? texture = SignCanvases.TextureOf(index);
            if (texture == null) return;

            _player = player;
            _rig = EmoteRig.Of(player);
            _index = index;
            _gallery.Index = index;
            _open = true;
            _view.texture = texture;
            if (DrawSync.Pending.Player != index) DrawSync.Pending.Start(index);
            gameObject.SetActive(true);
            if (!gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            transform.SetAsLastSibling();
            ApplyLayout();
            _pad.Open();
            ResetState();
            EventSystem.current.SetSelectedGameObject(null);
            HideHud();
            player.isTypingChat = true;
            SetCursor();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to open the drawing panel: {ex}");
            Close();
        }
    }

    public void Close()
    {
        if (!_open) return;

        PlayerControllerB? player = _player;
        int index = _index;
        try
        {
            _open = false;
            _closedFrame = Time.frameCount;
            if (player != null)
            {
                player.isTypingChat = false;
            }

            ShowHud(player);
        }
        finally
        {
            _pad.Close();
            RestoreCursor(player);
            Finish();
            Closed?.Invoke(index);
        }
    }

    public void ShowSign()
    {
        PlayerControllerB? player = _player;
        if (!_open || player == null) return;

        if (Log.PointCount == 0)
        {
            if (DrawSync.Hold) DrawSync.SendLog(player);
            EmoteControl.StopLocal(player);
            Close();
        }
        else if (EmoteControl.TryStart(player, EmoteDefs.SignShown))
        {
            Close();
        }
    }

    public void PutAway()
    {
        PlayerControllerB? player = _player;
        if (!_open || player == null) return;

        EmoteControl.StopLocal(player);
        Close();
    }

    public bool CancelPick() => _tools.CancelPick();

    public bool CloseGallery()
    {
        if (!_gallery.IsOpen) return false;

        _gallery.Close();
        return true;
    }

    private static string GetHint(HintMode mode, bool pad)
    {
        if (mode == HintMode.Type) return TypeHint;
        if (mode == HintMode.Text) return TextHint;
        if (mode == HintMode.Gallery) return GalleryHint;

        int index = pad ? 1 : 0;
        string show = InputHandlers.GetKey(InputHandlers.Inputs.SignShow, index);
        string away = InputHandlers.GetKey(InputHandlers.Inputs.SignAway, index);
        if (mode == HintMode.Pick)
        {
            if (!pad) return away.Length > 0 ? string.Format(PickHint, away) : MousePickHint;

            return away.Length > 0 ? $"{PadPickHint}  [{away}] Cancel" : PadPickHint;
        }

        string hint = pad ? PadDrawHint : KeyDrawHint;
        if (show.Length > 0)
        {
            hint += $"  [{show}] Show";
        }

        if (away.Length > 0)
        {
            hint += $"  [{away}] Put away";
        }

        return hint;
    }

    private void Bind(TMP_FontAsset? font)
    {
        _content = transform.Find("Content").GetComponent<RectTransform>();
        Transform view = _content.Find("View");
        _viewRect = view.GetComponent<RectTransform>();
        _view = view.GetComponent<RawImage>();
        _clear = new Choice(_content, "Clear", ClearText);
        _counter = _content.Find("Counter").GetComponent<TMP_Text>();
        _hint = _content.Find("Hint").GetComponent<TMP_Text>();
        _inkRect = _content.Find("InkBar").GetComponent<RectTransform>();
        _inkFill = _content.Find("InkBar/Fill").GetComponent<RectTransform>();
        _padCursor = transform.Find("PadCursor").GetComponent<RectTransform>();
        _tools = new PanelTools(_pen);
        _tools.Bind(_content);
        _gallery = new GalleryTools(_pen, LoadSlot);
        _gallery.Bind(_content);
        _canvas = GetComponent<Canvas>().rootCanvas;
        _canvasRect = (RectTransform)_canvas.transform;
        _content.Find("Undo").GetComponent<Button>().onClick.AddListener(UndoStroke);
        _content.Find("Redo").GetComponent<Button>().onClick.AddListener(RedoStroke);
        _clear.Button.onClick.AddListener(OnClear);
        _content.Find("Show").GetComponent<Button>().onClick.AddListener(ShowSign);
        _content.Find("PutAway").GetComponent<Button>().onClick.AddListener(PutAway);
        if (font != null)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].font = font;
            }
        }
    }

    private void ApplyLayout()
    {
        Rect bounds = _canvasRect.rect;
        if (bounds.width <= 0f || bounds.height <= 0f) return;

        _laidOut = bounds.size;
        Rect size = _content.rect;
        if (size.width <= 0f || size.height <= 0f) return;

        float scale = Mathf.Min(bounds.height * HeightShare / size.height, bounds.width * WidthShare / size.width);
        _content.localScale = new Vector3(scale, scale, 1f);
    }

    private void ResetState()
    {
        _pen.End();
        _pen.ClearRedo();
        _clear.Reset();
        _shownCount = -1;
        _nextFlush = 0f;
        _undoKey = null;
        _redoKey = null;
        _tools.Reset();
        _gallery.Close();
        _hintMode = HintMode.Draw;
        _hintPad = _pad.Hint;
        _hint.text = GetHint(_hintMode, _hintPad);
        _padCursor.gameObject.SetActive(false);
        _tools.SetPad(false);
    }

    private static void HideHud()
    {
        HUDManager hud = HUDManager.Instance;
        hud.PingHUDElement(hud.Inventory, 0f, 0f, 0f);
        hud.PingHUDElement(hud.PlayerInfo, 0f, 0f, 0f);
        hud.PingHUDElement(hud.Chat, 0f, 0f, 0f);
        hud.PingHUDElement(hud.Tooltips, 0f, 0f, 0f);
        hud.ClearControlTips();
    }

    private static void ShowHud(PlayerControllerB? player)
    {
        HUDManager hud = HUDManager.Instance;
        hud.PingHUDElement(hud.Inventory, 0f, 0.5f, 0.5f);
        hud.PingHUDElement(hud.PlayerInfo, 0f, 1f, 1f);
        hud.PingHUDElement(hud.Chat, 0f, 1f, 1f);
        hud.PingHUDElement(hud.Tooltips, 0f, 1f, 1f);
        if (player != null && player.isHoldingObject && player.currentlyHeldObjectServer != null)
        {
            player.currentlyHeldObjectServer.SetControlTipsForItem();
        }
        else
        {
            hud.ClearControlTips();
        }
    }

    private static void SetCursor()
    {
        HUDManager.Instance.SetMouseCursorSprite(null);
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private static void RestoreCursor(PlayerControllerB? player)
    {
        if (player != null && player.quickMenuManager.isMenuOpen) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Finish()
    {
        Flush();
        _pen.End();
        if (_disabling) return;

        _gallery.Close();
        _clear.Reset();
        gameObject.SetActive(false);
    }

    private void Flush()
    {
        _nextFlush = Time.unscaledTime + Constants.Sync.FlushInterval;
        if (!ConfigManager.ShowWhileDrawing.Value && DrawSync.Pending.Count > 0) DrawSync.Hold = true;
        DrawSync.Flush();
    }

    private void OnApplied(int firstNew) => SignCanvases.OnApplied(_index, false, firstNew);

    private void OnEnable()
    {
        if (!_open)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        _disabling = true;
        try
        {
            Close();
        }
        finally
        {
            _disabling = false;
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }

        _pad.Destroy();
        _gallery?.Destroy();
        _tools?.Destroy();
    }

    private void Update()
    {
        if (!_open) return;

        if (ShouldClose())
        {
            Close();
            return;
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = !_pad.Active;
        if (_canvasRect.rect.size != _laidOut)
        {
            ApplyLayout();
        }

        float now = Time.unscaledTime;
        Keyboard? keyboard = Keyboard.current;
        if (keyboard != null && !_tools.Typing && !_gallery.IsOpen)
        {
            UpdateKeys(keyboard);
        }

        _pad.Poll();
        if (_pad.Active != _padCursor.gameObject.activeSelf) _pen.End();
        if (_pad.Active && !_tools.Typing && !_gallery.IsOpen) UpdatePad(_pad.Keys);
        UpdateMouse(_pad.Pick(Mouse.current), now);
        UpdateCursor();
        _gallery.Update(now);
        UpdateHint();
        _clear.Refresh(now);
        UpdateCounter();
        if (now >= _nextFlush && DrawSync.Pending.Count > 0)
        {
            Flush();
        }
    }

    private bool ShouldClose()
    {
        PlayerControllerB? player = _player;
        EmoteRig? rig = _rig;
        return player == null || rig == null || !rig.Built || rig.Current != EmoteDefs.Sign
            || player.isPlayerDead || !player.isPlayerControlled || !player.performingEmote;
    }

    private void UpdateKeys(Keyboard keyboard)
    {
        if (!keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed) return;

        _undoKey ??= keyboard.FindKeyOnCurrentKeyboardLayout("z") ?? keyboard.zKey;
        _redoKey ??= keyboard.FindKeyOnCurrentKeyboardLayout("y") ?? keyboard.yKey;
        if (_undoKey.wasPressedThisFrame)
        {
            if (keyboard.shiftKey.isPressed) RedoStroke();
            else UndoStroke();
        }
        else if (_redoKey.wasPressedThisFrame)
        {
            RedoStroke();
        }
    }

    private void UpdatePad(PadKeys keys)
    {
        if (keys == PadKeys.None) return;

        if ((keys & PadKeys.Undo) != 0) UndoStroke();
        if ((keys & PadKeys.Redo) != 0) RedoStroke();
        if ((keys & PadKeys.SizeDown) != 0) _tools.StepSize(-1);
        if ((keys & PadKeys.SizeUp) != 0) _tools.StepSize(1);
        if ((keys & PadKeys.Tool) != 0) _tools.CycleTool();
        if ((keys & PadKeys.SwatchPrev) != 0) _tools.StepSwatch(-1);
        if ((keys & PadKeys.SwatchNext) != 0) _tools.StepSwatch(1);
    }

    private void UpdateCursor()
    {
        bool show = _pad.Active;
        if (show != _padCursor.gameObject.activeSelf)
        {
            _padCursor.gameObject.SetActive(show);
            _tools.SetPad(show);
        }

        if (!show) return;

        Camera? camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, _pad.Position, camera, out Vector2 local))
        {
            _padCursor.localPosition = new Vector3(local.x, local.y, 0f);
        }

        _padCursor.SetAsLastSibling();
    }

    private void UpdateHint()
    {
        HintMode mode = _gallery.IsOpen ? HintMode.Gallery : _tools.Mode;
        bool pad = _pad.Hint;
        if (mode == _hintMode && pad == _hintPad) return;

        _hintMode = mode;
        _hintPad = pad;
        _hint.text = GetHint(mode, pad);
    }

    private void UpdateMouse(Mouse? mouse, float now)
    {
        if (mouse == null) return;

        if (_gallery.IsOpen)
        {
            _pen.End();
            _tools.CancelPick();
        }

        bool picking = _tools.Picking;
        Camera? camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        Vector2 screen = mouse.position.ReadValue();
        bool mapped = RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewRect, screen, camera, out Vector2 local);
        bool inside = mapped && _viewRect.rect.Contains(local);
        _tools.Update(mouse, camera, inside, local, _index);
        _tools.SetRing(inside && !_gallery.IsOpen, local);
        if (picking || !mapped || _gallery.IsOpen) return;

        Vector2 pixel = _tools.GetPixel(local);
        if (inside && mouse.leftButton.wasPressedThisFrame)
        {
            PressStroke(now, pixel.x, pixel.y);
        }

        if (!_pen.Down) return;

        if (mouse.leftButton.isPressed)
        {
            MoveStroke(now, pixel.x, pixel.y);
        }
        else
        {
            ReleaseStroke(pixel.x, pixel.y);
        }
    }

    private bool LoadSlot(int slot)
    {
        _pen.End();
        if (DrawingFiles.Read(slot, Log, _pen) != DrawingFiles.Status.Ok) return false;

        _pen.ClearRedo();
        SignCanvases.OnApplied(_index, true, 0);
        _tools.Reset();
        if (!ConfigManager.ShowWhileDrawing.Value) DrawSync.Hold = true;
        else if (_player != null) DrawSync.SendLog(_player);

        return true;
    }

    private void PressFill(float x, float y)
    {
        if (!SignCanvases.CanFill(_index, x, y, _pen.Color)) return;

        if (_pen.Fill(Log, DrawSync.Pending, x, y, out int firstNew))
        {
            OnApplied(firstNew);
            Flush();
        }
    }

    private void PressText(float x, float y)
    {
        string text = _tools.Text;
        if (text.Length == 0) return;

        if (_pen.Text(Log, DrawSync.Pending, text, x, y, out int firstNew))
        {
            _tools.ClearText();
            OnApplied(firstNew);
            Flush();
        }
    }

    private void PressStroke(float now, float x, float y)
    {
        if (_pen.Tool == PenTool.Text) PressText(x, y);
        else if (_pen.Tool == PenTool.Fill) PressFill(x, y);
        else if (_pen.Press(Log, DrawSync.Pending, now, x, y, out int firstNew)) OnApplied(firstNew);
    }

    private void MoveStroke(float now, float x, float y)
    {
        if (_pen.Move(Log, DrawSync.Pending, now, x, y, out int firstNew)) OnApplied(firstNew);
    }

    private void ReleaseStroke(float x, float y)
    {
        if (_pen.Release(Log, DrawSync.Pending, x, y, out int firstNew)) OnApplied(firstNew);

        Flush();
    }

    private void UndoStroke()
    {
        if (_pen.Undo(Log, DrawSync.Pending))
        {
            SignCanvases.OnLocalUndo(_index);
            Flush();
        }
    }

    private void RedoStroke()
    {
        if (_pen.Redo(Log, DrawSync.Pending, out int firstNew))
        {
            OnApplied(firstNew);
            Flush();
        }
    }

    private void OnClear()
    {
        if (!_clear.Press(Time.unscaledTime, true) || !_pen.Clear(Log, DrawSync.Pending)) return;

        DrawSync.Hold = false;
        SignCanvases.OnApplied(_index, true, 0);
        Flush();
    }

    private void UpdateCounter()
    {
        int count = Log.PointCount;
        if (count == _shownCount) return;

        _shownCount = count;
        _counter.text = count >= Constants.Draw.MaxPoints ? FullText : $"{count}/{Constants.Draw.MaxPoints}";
        float used = Mathf.Clamp01(count / (float)Constants.Draw.MaxPoints);
        _inkFill.sizeDelta = new Vector2(used * _inkRect.rect.width, 0f);
    }
}
