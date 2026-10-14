using System;
using System.Diagnostics;

namespace MuchBetterEmotes;

internal sealed class InkCanvas
{
    private const int PointCost = 7;
    private const int RowCost = 7;
    private const int PixelShift = 3;

    private static readonly int[][] _discs = CreateDiscs();

    private static uint[]? _wallSource;
    private static bool[] _wallMask = Array.Empty<bool>();
    private static int[] _seen = Array.Empty<int>();
    private static int[] _stack = new int[256];
    private static int _stamp;

    private readonly uint[] _base;
    private readonly bool[] _wall;
    private bool _filled;
    private uint[]? _snap;
    private int _next;
    private int _snapStrokes;
    private int _snapPoints;
    private bool _snapValid;

    public InkCanvas(int width, int height, uint[] basePixels)
    {
        Width = width;
        Height = height;
        _base = basePixels;
        _wall = GetWalls(basePixels);
        Pixels = new uint[width * height];
        Array.Copy(basePixels, Pixels, Pixels.Length);
    }

    public static Func<long> Clock { get; set; } = Stopwatch.GetTimestamp;

    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }
    public GlyphFont? Font { get; set; }
    public bool Dirty { get; set; }
    public bool Replaying { get; private set; }
    public int Painted => _next;

    public static ushort ToUnit(float pixel, int size)
    {
        double unit = Math.Round(pixel / (double)(size - 1) * 65535.0);
        if (unit >= 65535.0) return 65535;
        return unit > 0.0 ? (ushort)unit : (ushort)0;
    }

    public static int ToPixel(ushort unit, int size) => (int)Math.Round(unit * (size - 1) / 65535.0);

    public void StartReplay()
    {
        Array.Copy(_base, Pixels, Pixels.Length);
        _next = 0;
        _snapValid = false;
        Replaying = true;
    }

    public bool StepReplay(DrawLog log, ref int budget, long deadline = 0)
    {
        if (!Replaying) return true;

        int steps = 0;
        while (_next < log.PointCount && budget > 0)
        {
            _filled = false;
            budget -= PaintPoint(log, _next);
            _next++;
            if (deadline != 0 && (++steps % Constants.Replay.ClockEvery == 0 || _filled) && Clock() >= deadline) break;
        }

        if (_next < log.PointCount) return false;

        Replaying = false;
        Dirty = true;
        return true;
    }

    public bool PaintNew(DrawLog log, ref int budget, long deadline = 0)
    {
        Replaying = true;
        return StepReplay(log, ref budget, deadline);
    }

    public void Snapshot(int strokes, int points)
    {
        _snap ??= new uint[Pixels.Length];
        Array.Copy(Pixels, _snap, Pixels.Length);
        _snapStrokes = strokes;
        _snapPoints = points;
        _snapValid = true;
    }

    public bool Restore(DrawLog log)
    {
        if (!_snapValid || Replaying || log.StrokeCount != _snapStrokes || log.PointCount != _snapPoints) return false;

        Array.Copy(_snap!, Pixels, Pixels.Length);
        _next = _snapPoints;
        _snapValid = false;
        Dirty = true;
        return true;
    }

    public bool CanFill(int x, int y, uint color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;

        int at = (y * Width) + x;
        if (_wall[at]) return false;

        uint pixel = Pixels[at];
        return pixel == _base[at] || pixel != color;
    }

    public int Flood(int x, int y, uint color, out int rows)
    {
        rows = 0;
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;

        int first = (y * Width) + x;
        if (_wall[first]) return 0;

        uint seed = Pixels[first];
        bool empty = seed == _base[first];
        if (!empty && seed == color) return 0;

        if (_seen.Length < Pixels.Length)
        {
            _seen = new int[Pixels.Length];
            _stamp = 0;
        }
        if (_stamp == int.MaxValue)
        {
            Array.Clear(_seen, 0, _seen.Length);
            _stamp = 0;
        }

        int stamp = ++_stamp;
        int top = y;
        int bottom = y;
        int written = 0;
        int count = 0;
        _stack[count++] = first;
        while (count > 0)
        {
            int at = _stack[--count];
            if (!Matches(at, empty, seed, stamp)) continue;

            int row = at / Width;
            int start = row * Width;
            int left = at - start;
            int right = left;
            while (left > 0 && Matches(start + left - 1, empty, seed, stamp)) left--;
            while (right < Width - 1 && Matches(start + right + 1, empty, seed, stamp)) right++;
            for (int i = start + left; i <= start + right; i++)
            {
                _seen[i] = stamp;
                Pixels[i] = color;
            }

            written += right - left + 1;
            if (row < top) top = row;
            if (row > bottom) bottom = row;
            for (int next = row - 1; next <= row + 1; next += 2)
            {
                if (next < 0 || next >= Height) continue;

                int line = next * Width;
                bool inRun = false;
                for (int k = left; k <= right; k++)
                {
                    bool match = Matches(line + k, empty, seed, stamp);
                    if (match && !inRun)
                    {
                        if (count == _stack.Length) Array.Resize(ref _stack, count * 2);
                        _stack[count++] = line + k;
                    }
                    inRun = match;
                }
            }
        }

        rows = bottom - top + 1;
        return written;
    }

    public int Segment(int x0, int y0, int x1, int y1, int size, uint color, bool erase) =>
        DrawSegment(x0, y0, x1, y1, size, color, erase, out _);

    private int DrawSegment(int x0, int y0, int x1, int y1, int size, uint color, bool erase, out int rows)
    {
        int radius = size >> 1;
        int top = (int)Math.Max((long)Math.Min(y0, y1) - radius, 0);
        int bottom = (int)Math.Min((long)Math.Max(y0, y1) + radius, Height - 1);
        rows = (int)Math.Max((long)bottom - top + 1, 0);
        long dx = (long)x1 - x0;
        long dy = (long)y1 - y0;
        long len = (dx * dx) + (dy * dy);
        long reach = IntSqrt((long)size * size * len / 4);
        int[] disc = _discs[size];
        int written = 0;
        for (int y = top; y <= bottom; y++)
        {
            long ay = y - (long)y0;
            long near0 = Math.Abs(ay);
            long near1 = Math.Abs(y - (long)y1);
            long left = long.MaxValue;
            long right = long.MinValue;
            if (near0 <= radius)
            {
                long half = disc[near0];
                left = x0 - half;
                right = x0 + half;
            }
            if (near1 <= radius)
            {
                long half = disc[near1];
                left = Math.Min(left, x1 - half);
                right = Math.Max(right, x1 + half);
            }
            if (len > 0 && TryBand(dx, dy, len, reach, ay, out long low, out long high))
            {
                left = Math.Min(left, x0 + low);
                right = Math.Max(right, x0 + high);
            }
            left = Math.Max(left, 0);
            right = Math.Min(right, Width - 1);
            if (left > right) continue;
            int start = (y * Width) + (int)left;
            int count = (int)(right - left) + 1;
            if (erase)
            {
                Array.Copy(_base, start, Pixels, start, count);
            }
            else
            {
                Array.Fill(Pixels, color, start, count);
            }
            written += count;
        }
        return written;
    }

    private int PaintPoint(DrawLog log, int point)
    {
        int stroke = log.StrokeOf(point);
        int color = log.StrokeColor(stroke);
        bool erase = color < 0;
        uint pixel = erase ? 0u : ColorMath.ToPixel(color);
        int size = log.StrokeSize(stroke);
        int x = ToPixel(log.X(point), Width);
        int y = ToPixel(log.Y(point), Height);
        if (size == Constants.Brush.FillSize)
        {
            _filled = true;
            int flooded = Flood(x, y, pixel, out int floodRows);
            return PointCost + (RowCost * floodRows) + (flooded >> PixelShift);
        }

        bool first = log.StrokeStart(stroke) == point;
        if (DrawLog.IsText(size))
        {
            if (!first) return 1;

            _filled = true;
            int stamped = Stamp(log, stroke, pixel, x, y, out int textRows);
            return PointCost + (RowCost * textRows) + (stamped >> PixelShift);
        }

        int fromX = first ? x : ToPixel(log.X(point - 1), Width);
        int fromY = first ? y : ToPixel(log.Y(point - 1), Height);
        int written = DrawSegment(fromX, fromY, x, y, size, pixel, erase, out int rows);
        return PointCost + (RowCost * rows) + (written >> PixelShift);
    }

    private int Stamp(DrawLog log, int stroke, uint pixel, int x, int y, out int rows)
    {
        rows = 0;
        GlyphFont? font = Font;
        if (font == null) return 0;

        int size = log.StrokeSize(stroke);
        int length = DrawLog.TextLength(size);
        int scale = DrawLog.TextScale(size);
        int start = log.StrokeStart(stroke);
        int cell = Constants.Glyph.Width * scale;
        rows = Math.Max(Math.Min(y + 1, Height) - Math.Max(y - (Constants.Glyph.Height * scale) + 1, 0), 0);
        int written = 0;
        for (int i = 0; i < length; i++)
        {
            int left = x + (i * cell);
            if (left >= Width) break;
            if (left + cell <= 0) continue;

            int code = (i & 1) == 0 ? log.X(start + 1 + (i >> 1)) : log.Y(start + 1 + (i >> 1));
            for (int gy = 0; gy < Constants.Glyph.Height; gy++)
            {
                for (int gx = 0; gx < Constants.Glyph.Width; gx++)
                {
                    if (!font.IsInk(code, gx, gy)) continue;

                    for (int sy = 0; sy < scale; sy++)
                    {
                        int py = y - (gy * scale) - sy;
                        if (py < 0 || py >= Height) continue;

                        for (int sx = 0; sx < scale; sx++)
                        {
                            int px = left + (gx * scale) + sx;
                            if (px < 0 || px >= Width) continue;

                            Pixels[(py * Width) + px] = pixel;
                            written++;
                        }
                    }
                }
            }
        }
        return written;
    }

    private bool Matches(int at, bool empty, uint seed, int stamp)
    {
        if (_wall[at] || _seen[at] == stamp) return false;

        uint pixel = Pixels[at];
        return empty ? pixel == _base[at] : pixel == seed && pixel != _base[at];
    }

    private static bool[] GetWalls(uint[] basePixels)
    {
        if (ReferenceEquals(_wallSource, basePixels) && _wallMask.Length == basePixels.Length) return _wallMask;

        bool[] mask = new bool[basePixels.Length];
        for (int i = 0; i < mask.Length; i++)
        {
            uint pixel = basePixels[i];
            uint luma = ((54 * (pixel & 0xFF)) + (183 * ((pixel >> 8) & 0xFF)) + (19 * ((pixel >> 16) & 0xFF))) >> 8;
            mask[i] = luma < Constants.Brush.WallLuma;
        }

        _wallSource = basePixels;
        _wallMask = mask;
        return mask;
    }

    private static bool TryBand(long dx, long dy, long len, long reach, long ay, out long low, out long high)
    {
        low = long.MinValue;
        high = long.MaxValue;
        return Narrow(dx, -ay * dy, len - (ay * dy), ref low, ref high)
            && Narrow(dy, (ay * dx) - reach, (ay * dx) + reach, ref low, ref high)
            && low <= high;
    }

    private static bool Narrow(long step, long min, long max, ref long low, ref long high)
    {
        if (step == 0) return min <= 0 && max >= 0;
        if (step < 0)
        {
            step = -step;
            long swap = min;
            min = -max;
            max = -swap;
        }
        low = Math.Max(low, CeilDiv(min, step));
        high = Math.Min(high, FloorDiv(max, step));
        return true;
    }

    private static long FloorDiv(long value, long step) => value >= 0 ? value / step : -((-value + step - 1) / step);

    private static long CeilDiv(long value, long step) => value > 0 ? (value + step - 1) / step : -(-value / step);

    private static long IntSqrt(long value)
    {
        if (value <= 0) return 0;
        long root = (long)Math.Sqrt(value);
        while (root > value / root)
        {
            root--;
        }
        while (root + 1 <= value / (root + 1))
        {
            root++;
        }
        return root;
    }

    private static int[][] CreateDiscs()
    {
        int[][] discs = new int[Constants.Draw.MaxSize + 1][];
        for (int n = 0; n < discs.Length; n++)
        {
            int radius = n >> 1;
            discs[n] = new int[radius + 1];
            for (int row = 0; row <= radius; row++)
            {
                discs[n][row] = (int)IntSqrt((((long)n * n) - (4L * row * row)) / 4);
            }
        }
        return discs;
    }
}
