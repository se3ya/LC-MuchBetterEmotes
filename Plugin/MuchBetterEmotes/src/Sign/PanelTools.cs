using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MuchBetterEmotes;

internal enum HintMode
{
    Draw,
    Pick,
    Type,
    Text,
    Gallery
}

internal sealed class PanelTools
{
    private const float HexTail = 0.1f;
    private const float ChipOffset = 12f;
    private const float ChipHalf = 9f;
    private const float MarkerRadius = 72f;
    private const float MinRing = 8f;
    private const float TintFloor = 0.3f;
    private const int HexDigits = 6;
    private const int PadSizeStep = 2;

    private static float _hue;
    private static float _sat;
    private static float _val;

    private readonly Pen _pen;
    private readonly Image[] _swatchFills = new Image[Constants.Panel.Palette.Length];
    private readonly GameObject[] _swatchLights = new GameObject[Constants.Panel.Palette.Length];
    private readonly GameObject[] _sizeLights = new GameObject[Constants.Panel.Presets.Length];

    private bool _picking;
    private bool _hexFocused;
    private bool _hexDirty;
    private bool _wheelHeld;
    private bool _textFocused;
    private float _textEnded = float.NegativeInfinity;
    private float _hexEnded = float.NegativeInfinity;
    private RectTransform _viewRect = null!;
    private RectTransform _ring = null!;
    private RectTransform _chip = null!;
    private Image _chipImage = null!;
    private RawImage _wheel = null!;
    private RectTransform _marker = null!;
    private RawImage _bright = null!;
    private RectTransform _brightRect = null!;
    private RectTransform _brightThumb = null!;
    private Image _previewFill = null!;
    private TMP_InputField _hex = null!;
    private RectTransform _hexRect = null!;
    private GameObject _hexFrame = null!;
    private GameObject _pickLight = null!;
    private GameObject _eraserLight = null!;
    private GameObject _brushLight = null!;
    private GameObject _fillLight = null!;
    private GameObject _textLight = null!;
    private GameObject _textNode = null!;
    private Button _textTool = null!;
    private TMP_InputField _text = null!;
    private RectTransform _textRect = null!;
    private RectTransform _ghostRect = null!;
    private RawImage _ghost = null!;
    private Texture2D? _ghostTexture;
    private Color32[]? _ghostPixels;
    private string _ghostText = string.Empty;
    private int _ghostColor = -1;
    private TMP_Text _sizeValue = null!;
    private RectTransform _sizeRect = null!;
    private RectTransform _sizeFill = null!;
    private RectTransform _sizeThumb = null!;

    public PanelTools(Pen pen)
    {
        _pen = pen;
    }

    public bool Picking => _picking;

    public bool Typing => HexTyping || TextTyping;

    public string Text => _text.text;

    public HintMode Mode =>
        HexTyping ? HintMode.Type : _picking ? HintMode.Pick : _pen.Tool == PenTool.Text ? HintMode.Text : HintMode.Draw;

    private bool HexTyping => _hexFocused || _hex.isFocused || Time.unscaledTime - _hexEnded < HexTail;

    private bool TextTyping => _textFocused || _text.isFocused || Time.unscaledTime - _textEnded < HexTail;

    public void Bind(Transform content)
    {
        _viewRect = content.Find("View").GetComponent<RectTransform>();
        _ring = content.Find("View/Ring").GetComponent<RectTransform>();
        Transform chip = content.Find("View/PickChip");
        _chip = chip.GetComponent<RectTransform>();
        _chipImage = chip.GetComponent<Image>();
        Transform wheel = content.Find("Wheel");
        _wheel = wheel.GetComponent<RawImage>();
        _marker = content.Find("Wheel/Marker").GetComponent<RectTransform>();
        Transform bright = content.Find("Bright");
        _bright = bright.GetComponent<RawImage>();
        _brightRect = bright.GetComponent<RectTransform>();
        _brightThumb = content.Find("Bright/Thumb").GetComponent<RectTransform>();
        _previewFill = content.Find("Preview/Fill").GetComponent<Image>();
        Transform hex = content.Find("Hex");
        _hex = hex.GetComponent<TMP_InputField>();
        _hexRect = hex.GetComponent<RectTransform>();
        _hexFrame = content.Find("HexFrame").gameObject;
        _pickLight = content.Find("Pick/Highlight").gameObject;
        _sizeValue = content.Find("SizeValue").GetComponent<TMP_Text>();
        Transform sizeBar = content.Find("SizeBar");
        _sizeRect = sizeBar.GetComponent<RectTransform>();
        _sizeFill = content.Find("SizeBar/Fill").GetComponent<RectTransform>();
        _sizeThumb = content.Find("SizeBar/Thumb").GetComponent<RectTransform>();
        _eraserLight = content.Find("Eraser/Highlight").gameObject;
        _brushLight = content.Find("Brush/Highlight").gameObject;
        _fillLight = content.Find("Fill/Highlight").gameObject;
        _textTool = content.Find("Text").GetComponent<Button>();
        _textLight = content.Find("Text/Highlight").gameObject;
        Transform field = content.Find("TextField");
        _text = field.GetComponent<TMP_InputField>();
        _textRect = field.GetComponent<RectTransform>();
        _textNode = field.gameObject;
        Transform ghost = content.Find("View/TextGhost");
        _ghost = ghost.GetComponent<RawImage>();
        _ghostRect = ghost.GetComponent<RectTransform>();

        _text.onValidateInput = GlyphFont.ValidateChar;
        _text.onEndEdit.AddListener(OnTextEnd);
        if (!AssetsLoader.CanType) _textTool.gameObject.SetActive(false);
        _hex.onValidateInput = ValidateChar;
        _hex.onValueChanged.AddListener(OnHexChanged);
        _hex.onEndEdit.AddListener(OnHexEnd);
        content.Find("Pick").GetComponent<Button>().onClick.AddListener(TogglePick);
        content.Find("Eraser").GetComponent<Button>().onClick.AddListener(() => SetTool(PenTool.Eraser));
        content.Find("Brush").GetComponent<Button>().onClick.AddListener(() => SetTool(PenTool.Brush));
        content.Find("Fill").GetComponent<Button>().onClick.AddListener(() => SetTool(PenTool.Fill));
        _textTool.onClick.AddListener(() => SetTool(PenTool.Text));
        for (int i = 0; i < _swatchFills.Length; i++)
        {
            int palette = i;
            content.Find($"Swatch{i}").GetComponent<Button>().onClick.AddListener(() => PickSwatch(palette));
            _swatchFills[i] = content.Find($"Swatch{i}/Fill").GetComponent<Image>();
            _swatchFills[i].color = ToColor(ColorMath.ToRgb(Constants.Panel.Palette[i]));
            _swatchLights[i] = content.Find($"Swatch{i}/Highlight").gameObject;
        }

        for (int i = 0; i < _sizeLights.Length; i++)
        {
            int preset = i;
            content.Find($"Size{i}").GetComponent<Button>().onClick.AddListener(() => SetSize(GetPresets()[preset]));
            float diameter = GetDiameter(Constants.Panel.Presets[i]);
            content.Find($"Size{i}/Dot").GetComponent<RectTransform>().sizeDelta = new Vector2(diameter, diameter);
            _sizeLights[i] = content.Find($"Size{i}/Highlight").gameObject;
        }

        wheel.gameObject.AddComponent<DragArea>().Changed = OnWheel;
        bright.gameObject.AddComponent<DragArea>().Changed = OnBright;
        sizeBar.gameObject.AddComponent<DragArea>().Changed = OnSizeBar;
    }

    public void Reset()
    {
        _picking = false;
        _hexFocused = false;
        _hexDirty = false;
        _hexEnded = float.NegativeInfinity;
        if (ColorMath.FromHsv(_hue, _sat, _val) != _pen.Color) ColorMath.ToHsv(_pen.Color, ref _hue, ref _sat, out _val);
        _textFocused = false;
        _textEnded = float.NegativeInfinity;
        if (_text.isFocused) _text.DeactivateInputField();
        if (_hex.isFocused) _hex.DeactivateInputField();
        _hexFrame.SetActive(false);
        _chip.gameObject.SetActive(false);
        SetOn(_pickLight, false);
        RefreshColor();
        RefreshTools();
    }

    public void Update(Mouse mouse, Camera? camera, bool inView, Vector2 local, int index)
    {
        bool focused = _hex.isFocused;
        if (focused != _hexFocused)
        {
            _hexFocused = focused;
            _hexFrame.SetActive(focused);
        }

        if (_hexDirty)
        {
            EventSystem events = EventSystem.current;
            if (!events.alreadySelecting)
            {
                _hexDirty = false;
                events.SetSelectedGameObject(null);
            }
        }

        int steps = PadMath.ScrollStep(mouse.scroll.ReadValue().y);
        if (steps != 0) SetSize(_pen.ActiveSize + steps);

        bool pressed = mouse.leftButton.wasPressedThisFrame;
        if (pressed && focused && !RectTransformUtility.RectangleContainsScreenPoint(_hexRect, mouse.position.ReadValue(), camera))
        {
            _hex.DeactivateInputField();
        }

        bool typed = _text.isFocused;
        _textFocused = typed;
        if (pressed && typed && !RectTransformUtility.RectangleContainsScreenPoint(_textRect, mouse.position.ReadValue(), camera))
        {
            _text.DeactivateInputField();
        }

        if (!_picking) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            StopPick();
            return;
        }

        ShowChip(inView, local, index);
        if (inView && pressed && TryReadPixel(local, index, out uint pixel)) SetColor(ColorMath.ToRgb(pixel) & 0xFFFFFF);
    }

    public bool CancelPick()
    {
        if (!_picking) return false;

        StopPick();
        return true;
    }

    public void ClearText() => _text.SetTextWithoutNotify(string.Empty);

    public Vector2 GetPixel(Vector2 local)
    {
        Vector2 unit = Rect.PointToNormalized(_viewRect.rect, local);
        return new Vector2(Mathf.Clamp01(unit.x) * (Constants.Draw.Width - 1), Mathf.Clamp01(unit.y) * (Constants.Draw.Height - 1));
    }

    public void SetRing(bool show, Vector2 local)
    {
        bool text = _pen.Tool == PenTool.Text;
        bool ghost = show && text && !_picking && _text.text.Length > 0;
        show &= !_picking && _pen.Tool != PenTool.Fill && !text;
        if (show) _ring.localPosition = new Vector3(local.x, local.y, 0f);
        if (show != _ring.gameObject.activeSelf) _ring.gameObject.SetActive(show);
        if (ghost != _ghost.gameObject.activeSelf) _ghost.gameObject.SetActive(ghost);
        if (!ghost) return;

        Rect rect = _viewRect.rect;
        Vector2 pixel = GetPixel(local);
        int pixelX = InkCanvas.ToPixel(InkCanvas.ToUnit(pixel.x, Constants.Draw.Width), Constants.Draw.Width);
        int pixelY = InkCanvas.ToPixel(InkCanvas.ToUnit(pixel.y, Constants.Draw.Height), Constants.Draw.Height);
        float cellX = rect.width / Constants.Draw.Width;
        float cellY = rect.height / Constants.Draw.Height;
        string typed = _text.text;
        FillGhost(typed);
        int fullWidth = typed.Length * Constants.Glyph.Width * _pen.TextScale;
        int fullHeight = Constants.Glyph.Height * _pen.TextScale;
        int width = Mathf.Min(fullWidth, Constants.Draw.Width - pixelX);
        int height = Mathf.Min(fullHeight, pixelY + 1);
        _ghostRect.sizeDelta = new Vector2(width * cellX, height * cellY);
        _ghostRect.localPosition = new Vector3(rect.xMin + (pixelX * cellX), rect.yMin + ((pixelY + 1) * cellY), 0f);
        float shareX = width / (float)fullWidth;
        float shareY = height / (float)fullHeight;
        Rect uv = new(0f, 1f - shareY, typed.Length / (float)Constants.Draw.MaxText * shareX, shareY);
        if (_ghost.uvRect != uv) _ghost.uvRect = uv;
    }

    private void FillGhost(string text)
    {
        GlyphFont? font = AssetsLoader.Glyphs;
        if (font == null) return;

        if (_ghostTexture == null)
        {
            int width = Constants.Draw.MaxText * Constants.Glyph.Width;
            _ghostTexture = new Texture2D(width, Constants.Glyph.Height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _ghostPixels = new Color32[width * Constants.Glyph.Height];
            _ghost.texture = _ghostTexture;
        }

        if (text == _ghostText && _pen.Color == _ghostColor) return;

        _ghostText = text;
        _ghostColor = _pen.Color;
        Color32[] pixels = _ghostPixels!;
        Array.Clear(pixels, 0, pixels.Length);
        Color32 ink = new((byte)(_pen.Color >> 16), (byte)(_pen.Color >> 8), (byte)_pen.Color, 255);
        int stride = Constants.Draw.MaxText * Constants.Glyph.Width;
        for (int i = 0; i < text.Length; i++)
        {
            if (!GlyphFont.IsGlyph(text[i])) continue;

            for (int gy = 0; gy < Constants.Glyph.Height; gy++)
            {
                for (int gx = 0; gx < Constants.Glyph.Width; gx++)
                {
                    if (font.IsInk(text[i], gx, gy))
                    {
                        pixels[((Constants.Glyph.Height - 1 - gy) * stride) + (i * Constants.Glyph.Width) + gx] = ink;
                    }
                }
            }
        }

        _ghostTexture.SetPixels32(pixels);
        _ghostTexture.Apply(false);
    }

    public void Destroy()
    {
        if (_ghostTexture != null) UnityEngine.Object.Destroy(_ghostTexture);
        _ghostTexture = null;
    }

    private static Color ToColor(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    private static void SetOn(GameObject node, bool on)
    {
        if (node.activeSelf != on) node.SetActive(on);
    }

    private static char ValidateChar(string text, int index, char added) => ColorMath.ValidateHex(text, index, added);

    private float GetDiameter(int size)
    {
        float unit = _viewRect.rect.width / Constants.Draw.Width;
        return ((2 * (size >> 1)) + 1f) * unit;
    }

    private void StopPick()
    {
        if (!_picking) return;

        _picking = false;
        _chip.gameObject.SetActive(false);
        SetOn(_pickLight, false);
    }

    private void TogglePick()
    {
        _pen.End();
        if (_hex.isFocused) _hex.DeactivateInputField();
        if (_picking)
        {
            StopPick();
            return;
        }

        _picking = true;
        SetOn(_pickLight, true);
        _ring.gameObject.SetActive(false);
    }

    private bool TryReadPixel(Vector2 local, int index, out uint pixel)
    {
        Vector2 at = GetPixel(local);
        return SignCanvases.TryRead(index, Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.y), out pixel);
    }

    private void ShowChip(bool inView, Vector2 local, int index)
    {
        if (inView != _chip.gameObject.activeSelf) _chip.gameObject.SetActive(inView);
        if (!inView) return;

        Rect rect = _viewRect.rect;
        float chipX = local.x + ChipOffset;
        float chipY = local.y + ChipOffset;
        if (chipX + ChipHalf > rect.x + rect.width) chipX = local.x - ChipOffset;
        if (chipY + ChipHalf > rect.y + rect.height) chipY = local.y - ChipOffset;
        _chip.localPosition = new Vector3(chipX, chipY, 0f);
        if (TryReadPixel(local, index, out uint pixel)) _chipImage.color = ToColor(ColorMath.ToRgb(pixel));
    }

    private void SetColor(int rgb)
    {
        _pen.Color = rgb;
        if (_pen.Tool == PenTool.Eraser) _pen.Tool = PenTool.Brush;
        _pen.End();
        StopPick();
        ColorMath.ToHsv(rgb, ref _hue, ref _sat, out _val);
        RefreshColor();
        RefreshTools();
    }

    private void SetHsv(float hue, float sat, float val)
    {
        _hue = hue;
        _sat = sat;
        _val = val;
        _pen.Color = ColorMath.FromHsv(hue, sat, val);
        if (_pen.Tool == PenTool.Eraser) _pen.Tool = PenTool.Brush;
        _pen.End();
        StopPick();
        RefreshColor();
        RefreshTools();
    }

    private void PickSwatch(int index) => SetColor(ColorMath.ToRgb(Constants.Panel.Palette[index]));

    public void CycleTool() => SetTool(PadMath.NextTool(_pen.Tool), false);

    public void StepSize(int dir) => SetSize(_pen.ActiveSize + (_pen.Tool == PenTool.Text ? dir : dir * PadSizeStep));

    public void SetPad(bool pad)
    {
        _textTool.interactable = !pad;
        _text.interactable = !pad;
        _hex.interactable = !pad;
    }

    public void StepSwatch(int dir)
    {
        int current = -1;
        if (_pen.Tool != PenTool.Eraser)
        {
            for (int i = 0; i < Constants.Panel.Palette.Length; i++)
            {
                if (ColorMath.ToRgb(Constants.Panel.Palette[i]) == _pen.Color)
                {
                    current = i;
                    break;
                }
            }
        }

        PickSwatch(PadMath.Swatch(current, dir, Constants.Panel.Palette.Length));
    }

    private void SetTool(PenTool tool, bool focus = true)
    {
        _pen.Tool = tool;
        _pen.End();
        StopPick();
        RefreshTools();
        if (tool == PenTool.Text && focus) _text.ActivateInputField();
    }

    private int[] GetPresets() => _pen.Tool == PenTool.Text ? Constants.Panel.TextPresets : Constants.Panel.Presets;

    private int GetMin() => _pen.Tool == PenTool.Text ? 1 : Constants.Draw.MinSize;

    private int GetMax() => _pen.Tool == PenTool.Text ? Constants.Draw.MaxScale : Constants.Draw.MaxSize;

    private void SetSize(int size)
    {
        int next = Mathf.Clamp(size, GetMin(), GetMax());
        StopPick();
        if (next == _pen.ActiveSize) return;

        _pen.ActiveSize = next;
        _pen.End();
        RefreshTools();
    }

    private void OnWheel(Vector2 unit, bool press)
    {
        if (!press && !_wheelHeld) return;

        float hue = _hue;
        float sat = _sat;
        float val = _val;
        bool picked = ColorMath.PickWheel(unit.x, unit.y, press, ref hue, ref sat, ref val);
        if (press) _wheelHeld = picked;
        if (picked) SetHsv(hue, sat, val);
    }

    private void OnBright(Vector2 unit, bool press) => SetHsv(_hue, _sat, Mathf.Clamp01(unit.x));

    private void OnSizeBar(Vector2 unit, bool press)
    {
        int min = GetMin();
        SetSize(Mathf.RoundToInt(min + (Mathf.Clamp01(unit.x) * (GetMax() - min))));
    }

    private void OnHexChanged(string text)
    {
        int digits = text.Length > 0 && text[0] == '#' ? text.Length - 1 : text.Length;
        if (digits == HexDigits && ColorMath.TryParseHex(text, out int rgb) && (rgb != _pen.Color || _pen.Tool == PenTool.Eraser))
        {
            SetColor(rgb);
        }
    }

    private void OnHexEnd(string text)
    {
        if (ColorMath.TryParseHex(text, out int rgb) && (rgb != _pen.Color || _pen.Tool == PenTool.Eraser)) SetColor(rgb);

        _hex.SetTextWithoutNotify(ColorMath.ToHex(_pen.Color));
        _hexEnded = Time.unscaledTime;
        _hexDirty = true;
    }

    private void OnTextEnd(string text)
    {
        _textEnded = Time.unscaledTime;
        _hexDirty = true;
    }

    private void RefreshColor()
    {
        ColorMath.WheelPoint(_hue, _sat, out float x, out float y);
        _marker.anchoredPosition = new Vector2(x * MarkerRadius, y * MarkerRadius);
        float tint = TintFloor + ((1f - TintFloor) * _val);
        _wheel.color = new Color(tint, tint, tint, 1f);
        _bright.color = ToColor(ColorMath.FromHsv(_hue, _sat, 1f));
        _brightThumb.anchoredPosition = new Vector2(_val * _brightRect.rect.width, 0f);
        _previewFill.color = ToColor(_pen.Color);
        if (!_hex.isFocused) _hex.SetTextWithoutNotify(ColorMath.ToHex(_pen.Color));
    }

    private void RefreshTools()
    {
        int size = _pen.ActiveSize;
        bool text = _pen.Tool == PenTool.Text;
        int[] presets = GetPresets();
        for (int i = 0; i < _swatchLights.Length; i++)
        {
            SetOn(_swatchLights[i], _pen.Tool != PenTool.Eraser && ColorMath.ToRgb(Constants.Panel.Palette[i]) == _pen.Color);
        }

        for (int i = 0; i < _sizeLights.Length; i++)
        {
            SetOn(_sizeLights[i], presets[i] == size);
        }

        SetOn(_eraserLight, _pen.Tool == PenTool.Eraser);
        SetOn(_brushLight, _pen.Tool == PenTool.Brush);
        SetOn(_fillLight, _pen.Tool == PenTool.Fill);
        SetOn(_textLight, text);
        SetOn(_textNode, text);
        _sizeValue.text = text ? $"x{size}" : (size * 0.5f).ToString("0.0", CultureInfo.InvariantCulture);
        float fraction = (size - GetMin()) / (float)(GetMax() - GetMin());
        float width = _sizeRect.rect.width;
        _sizeFill.sizeDelta = new Vector2(fraction * width, 0f);
        _sizeThumb.anchoredPosition = new Vector2(fraction * width, 0f);
        float diameter = Mathf.Max(GetDiameter(size), MinRing);
        _ring.sizeDelta = new Vector2(diameter, diameter);
    }
}
