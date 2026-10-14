using System;

namespace MuchBetterEmotes;

internal sealed class DrawLog
{
    private readonly ushort[] _x;
    private readonly ushort[] _y;
    private readonly int[] _starts;
    private readonly int[] _colors;
    private readonly byte[] _sizes;
    private int _left;

    public DrawLog(int capacity)
    {
        Capacity = capacity;
        _x = new ushort[capacity];
        _y = new ushort[capacity];
        _starts = new int[capacity];
        _colors = new int[capacity];
        _sizes = new byte[capacity];
    }

    public int Capacity { get; }
    public int PointCount { get; private set; }
    public int StrokeCount { get; private set; }
    public bool Open { get; private set; }

    public static bool IsValidStroke(int color, int size)
    {
        if (color < -1 || color > 0xFFFFFF) return false;
        if (size == Constants.Brush.FillSize || IsText(size)) return color >= 0;
        return size >= Constants.Draw.MinSize && size <= Constants.Draw.MaxSize;
    }

    public static bool IsText(int size) => size >= Constants.Brush.TextSize;

    public static int TextSize(int length, int scale) => Constants.Brush.TextSize + ((length - 1) * Constants.Draw.MaxScale) + scale - 1;

    public static int TextLength(int size) => ((size - Constants.Brush.TextSize) / Constants.Draw.MaxScale) + 1;

    public static int TextScale(int size) => ((size - Constants.Brush.TextSize) % Constants.Draw.MaxScale) + 1;

    public static int StrokePoints(int size) => IsText(size) ? 1 + ((TextLength(size) + 1) / 2) : 1;

    public ushort X(int point) => _x[point];

    public ushort Y(int point) => _y[point];

    public int StrokeStart(int stroke) => _starts[stroke];

    public int StrokeEnd(int stroke) => stroke + 1 < StrokeCount ? _starts[stroke + 1] : PointCount;

    public int StrokeColor(int stroke) => _colors[stroke];

    public int StrokeSize(int stroke) => _sizes[stroke];

    public int StrokeOf(int point)
    {
        int low = 0;
        int high = StrokeCount - 1;
        while (low < high)
        {
            int mid = (low + high + 1) >> 1;
            if (_starts[mid] <= point)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }
        return low;
    }

    public Stroke GetStroke(int stroke)
    {
        int start = _starts[stroke];
        Stroke copy = new(_colors[stroke], _sizes[stroke], StrokeEnd(stroke) - start);
        Array.Copy(_x, start, copy.X, 0, copy.X.Length);
        Array.Copy(_y, start, copy.Y, 0, copy.Y.Length);
        return copy;
    }

    public bool AddStroke(Stroke stroke)
    {
        if (!Begin(stroke.Color, stroke.Size, stroke.X[0], stroke.Y[0])) return false;

        for (int i = 1; i < stroke.X.Length; i++)
        {
            if (!Add(stroke.X[i], stroke.Y[i])) return false;
        }
        return true;
    }

    public bool Begin(int color, int size, ushort x, ushort y)
    {
        if (!IsValidStroke(color, size) || PointCount >= Capacity) return false;
        _starts[StrokeCount] = PointCount;
        _colors[StrokeCount] = color;
        _sizes[StrokeCount] = (byte)size;
        StrokeCount++;
        _x[PointCount] = x;
        _y[PointCount] = y;
        PointCount++;
        _left = IsText(size) ? StrokePoints(size) - 1 : 0;
        Open = size != Constants.Brush.FillSize;
        return true;
    }

    public bool Add(ushort x, ushort y)
    {
        if (!Open || PointCount >= Capacity) return false;
        if (_left > 0)
        {
            if (!IsValidPair(x, y)) return false;
            _left--;
            if (_left == 0) Open = false;
        }
        _x[PointCount] = x;
        _y[PointCount] = y;
        PointCount++;
        return true;
    }

    public void Undo()
    {
        if (StrokeCount > 0)
        {
            StrokeCount--;
            PointCount = _starts[StrokeCount];
        }
        Open = false;
        _left = 0;
    }

    public void Clear()
    {
        PointCount = 0;
        StrokeCount = 0;
        Open = false;
        _left = 0;
    }

    public void CopyFrom(DrawLog other)
    {
        PointCount = other.PointCount;
        StrokeCount = other.StrokeCount;
        Open = other.Open;
        _left = other._left;
        Array.Copy(other._x, _x, PointCount);
        Array.Copy(other._y, _y, PointCount);
        Array.Copy(other._starts, _starts, StrokeCount);
        Array.Copy(other._colors, _colors, StrokeCount);
        Array.Copy(other._sizes, _sizes, StrokeCount);
    }

    private bool IsValidPair(ushort x, ushort y)
    {
        if (!GlyphFont.IsGlyph(x)) return false;

        bool lone = _left == 1 && (TextLength(_sizes[StrokeCount - 1]) & 1) == 1;
        return lone ? y == 0 : GlyphFont.IsGlyph(y);
    }
}
