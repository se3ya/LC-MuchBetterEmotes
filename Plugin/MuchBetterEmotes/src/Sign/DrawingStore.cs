using GameNetcodeStuff;

namespace MuchBetterEmotes;

internal static class DrawingStore
{
    private static StartOfRound? _lobby;
    private static bool _stopped;
    private static bool _hashed;
    private static int _hash;

    public static void Initialize()
    {
        DrawPanel.Closed += OnClosed;
    }

    public static void OnStarted(PlayerControllerB player, int emote)
    {
        if (emote != EmoteDefs.Sign || !ConfigManager.RememberDrawing.Value || !EmoteControl.IsLocal(player)) return;
        StartOfRound round = StartOfRound.Instance;
        if (_lobby == round) return;

        _lobby = round;
        _hashed = false;
        Load(EmoteControl.GetLocalIndex());
    }

    private static void Load(int index)
    {
        DrawLog log = SignCanvases.Book.Get(index);
        if (log.StrokeCount > 0) return;

        DrawingFiles.Status status = DrawingFiles.Read(DrawingFiles.LastPath, log, DrawPanel.Pen);
        if (status == DrawingFiles.Status.Newer) _stopped = true;
        if (status != DrawingFiles.Status.Ok) return;

        SignCanvases.OnApplied(index, true, 0);
        if (log.StrokeCount > 0)
        {
            DrawSync.Hold = true;
            _hash = DrawingData.Hash(log, DrawPanel.Pen);
            _hashed = true;
        }
    }

    private static void OnClosed(int index)
    {
        if (_stopped || _lobby == null || _lobby != StartOfRound.Instance || !ConfigManager.RememberDrawing.Value) return;

        Save(index);
    }

    private static void Save(int index)
    {
        Pen pen = DrawPanel.Pen;
        DrawLog log = SignCanvases.Book.Get(index);
        int hash = DrawingData.Hash(log, pen);
        if (_hashed && hash == _hash) return;

        if (log.StrokeCount == 0)
        {
            if (!_hashed || !DrawingFiles.Delete(DrawingFiles.LastPath)) return;
        }
        else if (!DrawingFiles.Write(DrawingFiles.LastPath, log, pen))
        {
            return;
        }

        _hash = hash;
        _hashed = true;
    }
}
