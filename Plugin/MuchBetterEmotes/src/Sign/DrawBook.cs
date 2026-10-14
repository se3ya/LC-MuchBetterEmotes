using System;

namespace MuchBetterEmotes;

internal sealed class DrawBook
{
    private readonly int _capacity;
    private DrawLog?[] _logs = new DrawLog?[8];
    private DrawLog _scratch;

    public DrawBook(int capacity)
    {
        _capacity = capacity;
        _scratch = new DrawLog(capacity);
    }

    public DrawLog Get(int player)
    {
        if (player >= _logs.Length)
        {
            Array.Resize(ref _logs, Math.Max(player + 1, _logs.Length * 2));
        }
        return _logs[player] ??= new DrawLog(_capacity);
    }

    public bool TryApply(StrokeData data, int playerCount, out int firstNew)
    {
        firstNew = 0;
        if (data.Player < 0 || data.Player >= playerCount) return false;
        DrawLog slot = Get(data.Player);
        firstNew = slot.PointCount;
        if (!data.IsValid(slot)) return false;
        _scratch.CopyFrom(slot);
        if (!Append(data, _scratch)) return false;
        _logs[data.Player] = _scratch;
        _scratch = slot;
        return true;
    }

    public bool TryUndo(int player, int playerCount)
    {
        if (player < 0 || player >= playerCount) return false;
        DrawLog log = Get(player);
        bool changed = log.StrokeCount > 0;
        log.Undo();
        return changed;
    }

    public bool TryClear(int player, int playerCount)
    {
        if (player < 0 || player >= playerCount) return false;
        DrawLog log = Get(player);
        bool changed = log.PointCount > 0;
        log.Clear();
        return changed;
    }

    public void ClearSlot(int player)
    {
        if (player >= 0 && player < _logs.Length)
        {
            _logs[player]?.Clear();
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < _logs.Length; i++)
        {
            _logs[i]?.Clear();
        }
    }

    private static bool Append(StrokeData data, DrawLog log)
    {
        int next = 0;
        for (int i = 0; i < data.X.Length; i++)
        {
            bool begin = next < data.Starts.Length && data.Starts[next] == i;
            bool added = begin ? log.Begin(data.Colors[next], data.Sizes[next], data.X[i], data.Y[i]) : log.Add(data.X[i], data.Y[i]);
            if (!added) return false;
            if (begin) next++;
        }
        return true;
    }
}
