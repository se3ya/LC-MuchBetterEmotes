using System.Collections.Generic;

namespace MuchBetterEmotes;

internal enum PenTool
{
    Brush,
    Eraser,
    Fill,
    Text
}

internal sealed class Pen
{
    private readonly List<Stroke> _redo = new();

    private float _time;
    private float _x;
    private float _y;

    public int Color { get; set; }
    public int Size { get; set; } = Constants.Brush.Size;
    public int EraseSize { get; set; } = Constants.Brush.EraseSize;
    public int TextScale { get; set; } = 1;
    public PenTool Tool { get; set; }

    public bool Down { get; private set; }

    public int ActiveSize
    {
        get => Tool == PenTool.Text ? TextScale : Tool == PenTool.Eraser ? EraseSize : Size;
        set
        {
            if (Tool == PenTool.Text) TextScale = value;
            else if (Tool == PenTool.Eraser) EraseSize = value;
            else Size = value;
        }
    }

    public bool Press(DrawLog log, DrawWriter pending, float time, float x, float y, out int firstNew)
    {
        End();
        firstNew = log.PointCount;
        if (Tool == PenTool.Fill || Tool == PenTool.Text || ActiveSize < Constants.Draw.MinSize) return false;

        int color = Tool == PenTool.Eraser ? -1 : Color;
        int size = ActiveSize;
        ushort unitX = InkCanvas.ToUnit(x, Constants.Draw.Width);
        ushort unitY = InkCanvas.ToUnit(y, Constants.Draw.Height);
        if (!log.Begin(color, size, unitX, unitY)) return false;

        _redo.Clear();
        pending.Begin(color, size, unitX, unitY);
        _time = time;
        _x = x;
        _y = y;
        Down = true;
        return true;
    }

    public bool Fill(DrawLog log, DrawWriter pending, float x, float y, out int firstNew)
    {
        End();
        firstNew = log.PointCount;
        ushort unitX = InkCanvas.ToUnit(x, Constants.Draw.Width);
        ushort unitY = InkCanvas.ToUnit(y, Constants.Draw.Height);
        if (!log.Begin(Color, Constants.Brush.FillSize, unitX, unitY)) return false;

        _redo.Clear();
        pending.Begin(Color, Constants.Brush.FillSize, unitX, unitY);
        return true;
    }

    public bool Text(DrawLog log, DrawWriter pending, string text, float x, float y, out int firstNew)
    {
        End();
        firstNew = log.PointCount;
        if (!GlyphFont.IsValidText(text) || TextScale < 1 || TextScale > Constants.Draw.MaxScale) return false;

        int size = DrawLog.TextSize(text.Length, TextScale);
        if (log.PointCount + DrawLog.StrokePoints(size) > log.Capacity) return false;

        ushort unitX = InkCanvas.ToUnit(x, Constants.Draw.Width);
        ushort unitY = InkCanvas.ToUnit(y, Constants.Draw.Height);
        if (!log.Begin(Color, size, unitX, unitY)) return false;

        pending.Begin(Color, size, unitX, unitY);
        for (int i = 0; i < text.Length; i += 2)
        {
            ushort second = i + 1 < text.Length ? text[i + 1] : (ushort)0;
            log.Add(text[i], second);
            pending.Point(text[i], second);
        }

        _redo.Clear();
        return true;
    }

    public bool Move(DrawLog log, DrawWriter pending, float time, float x, float y, out int firstNew)
    {
        firstNew = log.PointCount;
        if (!Down || time - _time < Constants.Draw.SampleInterval) return false;
        if (SqrDistance(x, y) < Constants.Brush.MinMove * Constants.Brush.MinMove) return false;

        _time = time;
        _x = x;
        _y = y;
        return Add(log, pending, x, y);
    }

    public bool Release(DrawLog log, DrawWriter pending, float x, float y, out int firstNew)
    {
        firstNew = log.PointCount;
        if (!Down) return false;

        Down = false;
        return SqrDistance(x, y) >= Constants.Brush.ReleaseMove * Constants.Brush.ReleaseMove && Add(log, pending, x, y);
    }

    public bool Undo(DrawLog log, DrawWriter pending)
    {
        End();
        if (log.StrokeCount == 0) return false;

        _redo.Add(log.GetStroke(log.StrokeCount - 1));
        log.Undo();
        pending.Undo();
        return true;
    }

    public bool Redo(DrawLog log, DrawWriter pending, out int firstNew)
    {
        firstNew = log.PointCount;
        if (_redo.Count == 0) return false;

        Stroke stroke = _redo[_redo.Count - 1];
        if (log.PointCount + stroke.X.Length > log.Capacity) return false;

        End();
        if (!log.AddStroke(stroke)) return false;

        pending.AddStroke(stroke);
        _redo.RemoveAt(_redo.Count - 1);
        return true;
    }

    public void ClearRedo() => _redo.Clear();

    public bool Clear(DrawLog log, DrawWriter pending)
    {
        End();
        _redo.Clear();
        if (log.StrokeCount == 0) return false;

        log.Clear();
        pending.Clear();
        return true;
    }

    public void End() => Down = false;

    private float SqrDistance(float x, float y)
    {
        float dx = x - _x;
        float dy = y - _y;
        return (dx * dx) + (dy * dy);
    }

    private bool Add(DrawLog log, DrawWriter pending, float x, float y)
    {
        ushort unitX = InkCanvas.ToUnit(x, Constants.Draw.Width);
        ushort unitY = InkCanvas.ToUnit(y, Constants.Draw.Height);
        if (!log.Add(unitX, unitY))
        {
            End();
            return false;
        }

        pending.Point(unitX, unitY);
        return true;
    }
}
