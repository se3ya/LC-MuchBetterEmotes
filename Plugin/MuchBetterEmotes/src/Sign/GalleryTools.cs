using System;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuchBetterEmotes;

internal sealed class GalleryTools
{
    private const string EmptyText = "Empty";
    private const string DamagedText = "Damaged";

    private static InkCanvas? _ink;
    private static DrawLog? _scratch;
    private static uint[]? _pixels;

    private readonly Pen _pen;
    private readonly Func<int, bool> _load;
    private readonly Slot[] _slots = new Slot[Constants.Draw.Slots];
    private readonly Texture2D[] _textures = new Texture2D[Constants.Draw.Slots];
    private int _job = -1;
    private GameObject _root = null!;
    private GameObject _drawingsLight = null!;

    public GalleryTools(Pen pen, Func<int, bool> load)
    {
        _pen = pen;
        _load = load;
    }

    public bool IsOpen { get; private set; }

    public int Index { get; set; }

    public void Bind(Transform content)
    {
        _root = content.Find("Gallery").gameObject;
        _drawingsLight = content.Find("Drawings/Highlight").gameObject;
        content.Find("Drawings").GetComponent<Button>().onClick.AddListener(Toggle);
        for (int i = 0; i < _slots.Length; i++)
        {
            int slot = i;
            _slots[i] = new Slot(content, i);
            _slots[i].Save.Button.onClick.AddListener(() => OnSave(slot));
            _slots[i].Load.Button.onClick.AddListener(() => OnLoad(slot));
            _slots[i].Delete.Button.onClick.AddListener(() => OnDelete(slot));
        }

        Close();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;

        IsOpen = true;
        _pen.End();
        _root.SetActive(true);
        _drawingsLight.SetActive(true);
        CreateTextures();
        for (int i = 0; i < _slots.Length; i++)
        {
            Show(i);
        }
    }

    public void Close()
    {
        IsOpen = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Reset();
        }

        _root.SetActive(false);
        _drawingsLight.SetActive(false);
    }

    public void Update(float now)
    {
        if (!IsOpen) return;

        int strokes = SignCanvases.Book.Get(Index).StrokeCount;
        for (int i = 0; i < _slots.Length; i++)
        {
            Slot slot = _slots[i];
            slot.Refresh(now);
            slot.Save.Button.interactable = strokes > 0;
            slot.Load.Button.interactable = slot.Cell == Cell.Ready;
            slot.Delete.Button.interactable = slot.Cell == Cell.Ready || slot.Cell == Cell.Damaged;
        }

        Render();
    }

    public void Destroy()
    {
        for (int i = 0; i < _textures.Length; i++)
        {
            if (_textures[i] != null) UnityEngine.Object.Destroy(_textures[i]);
        }
    }

    private void CreateTextures()
    {
        for (int i = 0; i < _textures.Length; i++)
        {
            if (_textures[i] != null) continue;

            Texture2D texture = new(Constants.Panel.ThumbWidth, Constants.Panel.ThumbHeight, TextureFormat.RGBA32, false, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            _textures[i] = texture;
            _slots[i].Thumb.texture = texture;
        }
    }

    private void Show(int index)
    {
        Slot slot = _slots[index];
        slot.Thumb.enabled = slot.Cell == Cell.Ready;
        slot.Label.text = slot.Cell switch
        {
            Cell.Empty => EmptyText,
            Cell.Damaged => DamagedText,
            _ => $"Slot {index + 1}"
        };
    }

    private void SetCell(int index, Cell cell)
    {
        _slots[index].Cell = cell;
        Show(index);
    }

    private void Invalidate(int index)
    {
        if (_job == index) _job = -1;
        SetCell(index, Cell.Unknown);
    }

    private void Render()
    {
        if (_job < 0)
        {
            int next = -1;
            for (int i = 0; i < _slots.Length && next < 0; i++)
            {
                if (_slots[i].Cell == Cell.Unknown) next = i;
            }

            if (next < 0) return;

            _ink ??= new InkCanvas(Constants.Draw.Width, Constants.Draw.Height, AssetsLoader.BasePixels!) { Font = AssetsLoader.Glyphs };
            _scratch ??= new DrawLog(Constants.Draw.MaxPoints);
            _pixels ??= new uint[Constants.Panel.ThumbWidth * Constants.Panel.ThumbHeight];
            _scratch.Clear();
            DrawingFiles.Status status = DrawingFiles.Read(next, _scratch, null);
            if (status != DrawingFiles.Status.Ok)
            {
                SetCell(next, status == DrawingFiles.Status.Empty ? Cell.Empty : Cell.Damaged);
                return;
            }

            _ink.StartReplay();
            _job = next;
        }

        int budget = Constants.Replay.Budget;
        long deadline = InkCanvas.Clock() + (long)(Constants.Replay.StepTime * Stopwatch.Frequency);
        if (!_ink!.StepReplay(_scratch!, ref budget, deadline)) return;

        Texture2D texture = _textures[_job];
        ThumbMath.Downsample(
            _ink.Pixels, Constants.Draw.Width, Constants.Draw.Height, _pixels!, Constants.Panel.ThumbWidth, Constants.Panel.ThumbHeight);
        texture.SetPixelData(_pixels!, 0);
        texture.Apply(false);
        SetCell(_job, Cell.Ready);
        _job = -1;
    }

    private void OnSave(int index)
    {
        DrawLog log = SignCanvases.Book.Get(Index);
        if (log.StrokeCount == 0) return;

        Slot slot = _slots[index];
        if (!slot.Save.Press(Time.unscaledTime, slot.Cell != Cell.Empty)) return;

        if (DrawingFiles.Write(index, log, _pen)) Invalidate(index);
    }

    private void OnLoad(int index)
    {
        Slot slot = _slots[index];
        if (slot.Cell != Cell.Ready) return;

        bool confirm = SignCanvases.Book.Get(Index).StrokeCount > 0;
        if (!slot.Load.Press(Time.unscaledTime, confirm)) return;

        if (_load(index)) Close();
        else Invalidate(index);
    }

    private void OnDelete(int index)
    {
        Slot slot = _slots[index];
        if (slot.Cell != Cell.Ready && slot.Cell != Cell.Damaged) return;
        if (!slot.Delete.Press(Time.unscaledTime, true)) return;

        if (DrawingFiles.Delete(index)) SetCell(index, Cell.Empty);
    }

    private enum Cell
    {
        Unknown,
        Empty,
        Ready,
        Damaged
    }

    private sealed class Slot
    {
        public Slot(Transform content, int index)
        {
            string path = $"Gallery/Slot{index}";
            Thumb = content.Find($"{path}/Thumb").GetComponent<RawImage>();
            Label = content.Find($"{path}/Label").GetComponent<TMP_Text>();
            Save = new Choice(content, $"{path}/Save", "Save");
            Load = new Choice(content, $"{path}/Load", "Load");
            Delete = new Choice(content, $"{path}/Delete", "Del");
        }

        public RawImage Thumb { get; }
        public TMP_Text Label { get; }
        public Choice Save { get; }
        public Choice Load { get; }
        public Choice Delete { get; }
        public Cell Cell { get; set; }

        public void Refresh(float now)
        {
            Save.Refresh(now);
            Load.Refresh(now);
            Delete.Refresh(now);
        }

        public void Reset()
        {
            Save.Reset();
            Load.Reset();
            Delete.Reset();
        }
    }
}
