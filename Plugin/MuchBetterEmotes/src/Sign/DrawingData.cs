using System;

namespace MuchBetterEmotes;

[Serializable]
internal sealed class DrawingData
{
    public const int Current = 1;
    public const int MaxDepth = 4;

    public enum Result
    {
        Ok,
        Newer,
        Bad
    }

    public int Version = Current;
    public string[] Colors = Array.Empty<string>();
    public float[] Radii = Array.Empty<float>();
    public bool[] Erase = Array.Empty<bool>();
    public int[] Counts = Array.Empty<int>();
    public int[] X = Array.Empty<int>();
    public int[] Y = Array.Empty<int>();
    public string PenColor = "";
    public float PenRadius;
    public float EraserRadius;

    public static DrawingData From(DrawLog log, Pen pen)
    {
        int strokes = log.StrokeCount;
        DrawingData data = new()
        {
            Colors = new string[strokes],
            Radii = new float[strokes],
            Erase = new bool[strokes],
            Counts = new int[strokes],
            X = new int[log.PointCount],
            Y = new int[log.PointCount],
            PenColor = ColorMath.ToHex(pen.Color),
            PenRadius = pen.Size / 2f,
            EraserRadius = pen.EraseSize / 2f
        };

        int next = 0;
        for (int i = 0; i < strokes; i++)
        {
            Stroke stroke = log.GetStroke(i);
            data.Colors[i] = ColorMath.ToHex(stroke.Color < 0 ? 0 : stroke.Color);
            data.Radii[i] = stroke.Size / 2f;
            data.Erase[i] = stroke.Color < 0;
            data.Counts[i] = stroke.X.Length;
            for (int j = 0; j < stroke.X.Length; j++)
            {
                data.X[next] = stroke.X[j];
                data.Y[next] = stroke.Y[j];
                next++;
            }
        }
        return data;
    }

    public static int Hash(DrawLog log, Pen pen)
    {
        unchecked
        {
            int hash = (int)2166136261;
            hash = (hash ^ pen.Color) * 16777619;
            hash = (hash ^ pen.Size) * 16777619;
            hash = (hash ^ pen.EraseSize) * 16777619;
            hash = (hash ^ log.StrokeCount) * 16777619;
            for (int i = 0; i < log.StrokeCount; i++)
            {
                hash = (hash ^ log.StrokeColor(i)) * 16777619;
                hash = (hash ^ log.StrokeSize(i)) * 16777619;
                hash = (hash ^ log.StrokeStart(i)) * 16777619;
            }
            for (int i = 0; i < log.PointCount; i++)
            {
                hash = (hash ^ log.X(i)) * 16777619;
                hash = (hash ^ log.Y(i)) * 16777619;
            }
            return hash;
        }
    }

    public Result CopyTo(DrawLog target)
    {
        if (Version > Current) return Result.Newer;
        if (Version < 1 || Colors == null || Radii == null || Erase == null || Counts == null || X == null || Y == null) return Result.Bad;

        int strokes = Counts.Length;
        if (Colors.Length != strokes || Radii.Length != strokes || Erase.Length != strokes) return Result.Bad;

        long total = 0;
        foreach (int count in Counts)
        {
            if (count < 1) return Result.Bad;
            total += count;
        }
        if (X.Length != total || Y.Length != total) return Result.Bad;

        DrawLog scratch = new(target.Capacity);
        int next = 0;
        for (int i = 0; i < strokes; i++)
        {
            if (!Replay(i, next, scratch)) return Result.Bad;
            next += Counts[i];
        }

        target.CopyFrom(scratch);
        return Result.Ok;
    }

    public Result Load(DrawLog target, Pen? pen)
    {
        Result result = CopyTo(target);
        if (result == Result.Ok && pen != null) ApplyPen(pen);
        return result;
    }

    public void ApplyPen(Pen pen)
    {
        if (ColorMath.TryParseHex(PenColor, out int rgb) && DrawLog.IsValidStroke(rgb, Constants.Draw.MinSize)) pen.Color = rgb;
        if (TryGetSize(PenRadius, out int size) && size >= Constants.Draw.MinSize && size <= Constants.Draw.MaxSize) pen.Size = size;
        if (TryGetSize(EraserRadius, out int eraser) && eraser >= Constants.Draw.MinSize && eraser <= Constants.Draw.MaxSize)
        {
            pen.EraseSize = eraser;
        }
    }

    public static bool IsTooDeep(string json)
    {
        int depth = 0;
        bool quoted = false;
        bool escaped = false;
        foreach (char c in json)
        {
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
                continue;
            }

            if (c == '"') quoted = true;
            else if (c == '[' || c == '{')
            {
                depth++;
                if (depth > MaxDepth) return true;
            }
            else if ((c == ']' || c == '}') && depth > 0) depth--;
        }
        return false;
    }

    private static bool TryGetSize(float radius, out int size)
    {
        size = 0;
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0 || radius > byte.MaxValue / 2f) return false;
        size = (int)Math.Round(radius * 2);
        return DrawLog.IsValidStroke(0, size);
    }

    private bool Replay(int stroke, int first, DrawLog log)
    {
        if (!TryGetSize(Radii[stroke], out int size)) return false;

        int color = -1;
        if (!Erase[stroke] && !ColorMath.TryParseHex(Colors[stroke], out color)) return false;

        Stroke copy = new(color, size, Counts[stroke]);
        for (int i = 0; i < copy.X.Length; i++)
        {
            int x = X[first + i];
            int y = Y[first + i];
            if (x < 0 || x > ushort.MaxValue || y < 0 || y > ushort.MaxValue) return false;
            copy.X[i] = (ushort)x;
            copy.Y[i] = (ushort)y;
        }
        return log.AddStroke(copy) && (!DrawLog.IsText(size) || !log.Open);
    }
}
