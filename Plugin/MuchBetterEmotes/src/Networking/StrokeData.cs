using System;

namespace MuchBetterEmotes;

[Serializable]
public struct StrokeData
{
    public int Player;
    public bool Continue;
    public int[] Starts;
    public int[] Colors;
    public byte[] Sizes;
    public ushort[] X;
    public ushort[] Y;

    public StrokeData(int player, bool extend, int[] starts, int[] colors, byte[] sizes, ushort[] x, ushort[] y)
    {
        Player = player;
        Continue = extend;
        Starts = starts;
        Colors = colors;
        Sizes = sizes;
        X = x;
        Y = y;
    }

    internal bool IsValid(DrawLog log)
    {
        if (Starts == null || Colors == null || Sizes == null || X == null || Y == null) return false;

        int points = X.Length;
        int strokes = Starts.Length;
        if (points < 1 || points > Constants.Sync.ChunkSize || Y.Length != points) return false;
        if (Colors.Length != strokes || Sizes.Length != strokes) return false;
        if (Continue ? !log.Open : (strokes == 0 || Starts[0] != 0)) return false;

        int previous = -1;
        for (int i = 0; i < strokes; i++)
        {
            if (Starts[i] <= previous || Starts[i] >= points || !DrawLog.IsValidStroke(Colors[i], Sizes[i])) return false;
            previous = Starts[i];
            int end = i + 1 < strokes ? Starts[i + 1] : points;
            if ((Sizes[i] == Constants.Brush.FillSize || DrawLog.IsText(Sizes[i])) && end - Starts[i] != DrawLog.StrokePoints(Sizes[i]))
            {
                return false;
            }
        }

        return log.PointCount + points <= log.Capacity;
    }
}
