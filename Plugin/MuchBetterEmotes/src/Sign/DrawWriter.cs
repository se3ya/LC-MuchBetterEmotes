using System.Collections.Generic;

namespace MuchBetterEmotes;

internal enum DrawKind
{
    Stroke,
    Undo,
    Clear
}

internal sealed class DrawWriter
{
    private readonly List<Entry> _entries = new();
    private readonly List<ushort> _x = new();
    private readonly List<ushort> _y = new();
    private readonly List<int> _colors = new();
    private readonly List<byte> _sizes = new();
    private readonly List<int> _starts = new();

    public int Player { get; private set; }

    public int Count => _entries.Count;

    public DrawKind GetKind(int message) => _entries[message].Kind;

    public StrokeData GetMessage(int message)
    {
        Entry entry = _entries[message];
        int[] starts = new int[entry.StrokeCount];
        int[] colors = new int[entry.StrokeCount];
        byte[] sizes = new byte[entry.StrokeCount];
        ushort[] x = new ushort[entry.PointCount];
        ushort[] y = new ushort[entry.PointCount];
        _starts.CopyTo(entry.StrokeStart, starts, 0, entry.StrokeCount);
        _colors.CopyTo(entry.StrokeStart, colors, 0, entry.StrokeCount);
        _sizes.CopyTo(entry.StrokeStart, sizes, 0, entry.StrokeCount);
        _x.CopyTo(entry.PointStart, x, 0, entry.PointCount);
        _y.CopyTo(entry.PointStart, y, 0, entry.PointCount);
        return new StrokeData(Player, entry.Continue, starts, colors, sizes, x, y);
    }

    public void Start(int player)
    {
        Player = player;
        Drop();
    }

    public void Reset()
    {
        Drop();
        _entries.Add(new Entry { Kind = DrawKind.Clear });
    }

    public void Begin(int color, int size, ushort x, ushort y)
    {
        int at = OpenChunk(false, DrawLog.StrokePoints(size));
        Entry entry = _entries[at];
        _colors.Add(color);
        _sizes.Add((byte)size);
        _starts.Add(entry.PointCount);
        _x.Add(x);
        _y.Add(y);
        entry.StrokeCount++;
        entry.PointCount++;
        _entries[at] = entry;
    }

    public void Point(ushort x, ushort y)
    {
        int at = OpenChunk(true, 1);
        Entry entry = _entries[at];
        _x.Add(x);
        _y.Add(y);
        entry.PointCount++;
        _entries[at] = entry;
    }

    public void AddStroke(Stroke stroke)
    {
        Begin(stroke.Color, stroke.Size, stroke.X[0], stroke.Y[0]);
        for (int i = 1; i < stroke.X.Length; i++)
        {
            Point(stroke.X[i], stroke.Y[i]);
        }
    }

    public void Undo() => _entries.Add(new Entry { Kind = DrawKind.Undo });

    public void Clear() => _entries.Add(new Entry { Kind = DrawKind.Clear });

    public void AddLog(DrawLog log)
    {
        Reset();
        for (int s = 0; s < log.StrokeCount; s++)
        {
            AddStroke(log.GetStroke(s));
        }
    }

    private void Drop()
    {
        _entries.Clear();
        _x.Clear();
        _y.Clear();
        _colors.Clear();
        _sizes.Clear();
        _starts.Clear();
    }

    private int OpenChunk(bool extend, int need)
    {
        int last = _entries.Count - 1;
        if (last >= 0 && _entries[last].Kind == DrawKind.Stroke && _entries[last].PointCount + need <= Constants.Sync.ChunkSize)
        {
            return last;
        }

        _entries.Add(new Entry { Kind = DrawKind.Stroke, Continue = extend, PointStart = _x.Count, StrokeStart = _starts.Count });
        return _entries.Count - 1;
    }

    private struct Entry
    {
        public DrawKind Kind;
        public bool Continue;
        public int PointStart;
        public int PointCount;
        public int StrokeStart;
        public int StrokeCount;
    }
}
