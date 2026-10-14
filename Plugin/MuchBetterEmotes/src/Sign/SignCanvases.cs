using System.Collections.Generic;
using System.Diagnostics;
using GameNetcodeStuff;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class SignCanvases
{
    private const string BaseMap = "_BaseColorMap";

    private static readonly List<SignCanvas> _canvases = new();

    private static int _active;
    private static int _turn;
    private static int _stamp = -1;
    private static int _budget;
    private static int _lane;
    private static bool _showOthers = true;
    private static Material? _signMat;

    public static DrawBook Book { get; } = new(Constants.Draw.MaxPoints);

    public static bool ShowOthers
    {
        get => _showOthers;
        set
        {
            _showOthers = value;
            if (!value)
            {
                ReturnOthers();
            }
        }
    }

    public static void OnApplied(int player, bool structural, int firstNew)
    {
        SignCanvas? canvas = Find(player);
        if (canvas == null)
        {
            TakeLive(player);
        }
        else if (structural)
        {
            canvas.Ink.StartReplay();
        }
        else if (!canvas.Ink.Replaying)
        {
            PaintLive(canvas, firstNew);
        }
    }

    public static void OnLocalUndo(int player)
    {
        SignCanvas? canvas = Find(player);
        if (canvas == null) return;

        DrawLog log = Book.Get(player);
        if (canvas.Ink.Restore(log)) return;

        Refill();
        canvas.Ink.StartReplay();
        canvas.Ink.StepReplay(log, ref _lane, Deadline(Constants.Replay.LocalTime));
    }

    public static bool TryRead(int player, int x, int y, out uint pixel)
    {
        pixel = 0;
        SignCanvas? canvas = Find(player);
        if (canvas == null || canvas.Ink.Replaying) return false;
        if (x < 0 || y < 0 || x >= canvas.Ink.Width || y >= canvas.Ink.Height) return false;

        pixel = canvas.Ink.Pixels[(y * canvas.Ink.Width) + x];
        return true;
    }

    public static bool CanFill(int player, float x, float y, int rgb)
    {
        SignCanvas? canvas = Find(player);
        if (canvas == null || canvas.Ink.Replaying) return false;

        int pixelX = InkCanvas.ToPixel(InkCanvas.ToUnit(x, Constants.Draw.Width), Constants.Draw.Width);
        int pixelY = InkCanvas.ToPixel(InkCanvas.ToUnit(y, Constants.Draw.Height), Constants.Draw.Height);
        return canvas.Ink.CanFill(pixelX, pixelY, ColorMath.ToPixel(rgb));
    }

    public static void Take(PlayerControllerB player)
    {
        int index = EmoteControl.GetIndex(player);
        if (index < 0 || Find(index) != null || !AssetsLoader.CanDraw) return;

        if (EmoteControl.IsLocal(player) || (_showOthers && Book.Get(index).PointCount > 0))
        {
            Assign(index);
        }
    }

    public static void Return(PlayerControllerB player)
    {
        if (_active == 0) return;

        SignCanvas? canvas = Find(EmoteControl.GetIndex(player));
        if (canvas != null)
        {
            Release(canvas);
        }
    }

    public static Texture2D? TextureOf(int player)
    {
        SignCanvas? canvas = Find(player);
        return canvas != null ? canvas.Texture : null;
    }

    public static void Paint()
    {
        if (_active == 0) return;

        Refill();
        SignCanvas? local = Find(EmoteControl.GetLocalIndex());
        if (local != null && local.Ink.Replaying)
        {
            local.Ink.StepReplay(Book.Get(local.Player), ref _lane, Deadline(Constants.Replay.LocalTime));
        }

        long deadline = Deadline(Constants.Replay.StepTime);
        int count = _canvases.Count;
        for (int n = 0; n < count; n++)
        {
            SignCanvas canvas = _canvases[(_turn + n) % count];
            if (canvas.Player < 0 || canvas == local || !canvas.Ink.Replaying) continue;
            if (InkCanvas.Clock() >= deadline) break;

            canvas.Ink.StepReplay(Book.Get(canvas.Player), ref _budget, deadline);
        }
        _turn = (_turn + 1) % count;

        for (int i = 0; i < _canvases.Count; i++)
        {
            SignCanvas canvas = _canvases[i];
            if (canvas.Player >= 0 && canvas.Ink.Dirty && !canvas.Ink.Replaying)
            {
                Upload(canvas);
            }
        }
    }

    public static void ClearSlot(int player)
    {
        Book.ClearSlot(player);
        SignCanvas? canvas = Find(player);
        if (canvas != null)
        {
            canvas.Ink.StartReplay();
        }
    }

    public static void ClearAll()
    {
        Book.ClearAll();
        for (int i = 0; i < _canvases.Count; i++)
        {
            SignCanvas canvas = _canvases[i];
            if (canvas.Player >= 0)
            {
                canvas.Ink.StartReplay();
            }
        }
    }

    public static void DestroyAll()
    {
        for (int i = 0; i < _canvases.Count; i++)
        {
            SignCanvas canvas = _canvases[i];
            Detach(canvas);
            Object.Destroy(canvas.Texture);
            Object.Destroy(canvas.Material);
        }

        _canvases.Clear();
        _active = 0;
        _turn = 0;
    }

    private static void Refill()
    {
        int frame = Time.frameCount;
        if (frame == _stamp) return;

        _stamp = frame;
        _budget = Constants.Replay.Budget;
        _lane = Constants.Replay.LocalBudget;
    }

    private static long Deadline(float time) => InkCanvas.Clock() + (long)(time * Stopwatch.Frequency);

    private static void PaintLive(SignCanvas canvas, int firstNew)
    {
        DrawLog log = Book.Get(canvas.Player);
        Refill();
        if (canvas != Find(EmoteControl.GetLocalIndex()))
        {
            canvas.Ink.PaintNew(log, ref _budget, Deadline(Constants.Replay.StepTime));
            return;
        }

        if (firstNew < log.PointCount && canvas.Ink.Painted == firstNew)
        {
            int stroke = log.StrokeOf(firstNew);
            if (log.StrokeStart(stroke) == firstNew) canvas.Ink.Snapshot(stroke, firstNew);
        }

        int before = _lane;
        canvas.Ink.PaintNew(log, ref _lane, Deadline(Constants.Replay.LocalTime));
        _budget -= before - _lane;
    }

    private static void TakeLive(int index)
    {
        PlayerControllerB? player = EmoteControl.GetPlayer(index);
        if (player == null || !AssetsLoader.CanDraw || !IsSignOut(player) || Book.Get(index).PointCount == 0) return;

        if (EmoteControl.IsLocal(player) || _showOthers)
        {
            Assign(index);
        }
    }

    private static void Assign(int index)
    {
        SignCanvas? canvas = FindFree();
        if (canvas == null)
        {
            canvas = Create();
            if (canvas == null) return;

            _canvases.Add(canvas);
        }

        canvas.Player = index;
        canvas.Ink.StartReplay();
        if (EmoteControl.IsLocal(EmoteControl.GetPlayer(index)))
        {
            canvas.Texture.SetPixelData(canvas.Ink.Pixels, 0);
            canvas.Texture.Apply(false);
        }
        _active++;
    }

    private static void Release(SignCanvas canvas)
    {
        Detach(canvas);
        canvas.Player = -1;
        _active--;
    }

    private static void ReturnOthers()
    {
        for (int i = 0; i < _canvases.Count; i++)
        {
            SignCanvas canvas = _canvases[i];
            if (canvas.Player >= 0 && !EmoteControl.IsLocal(EmoteControl.GetPlayer(canvas.Player)))
            {
                Release(canvas);
            }
        }
    }

    private static void Upload(SignCanvas canvas)
    {
        canvas.Texture.SetPixelData(canvas.Ink.Pixels, 0);
        canvas.Texture.Apply(false);
        canvas.Ink.Dirty = false;
        if (canvas.Board == null)
        {
            Attach(canvas);
        }
    }

    private static void Attach(SignCanvas canvas)
    {
        PlayerControllerB? player = EmoteControl.GetPlayer(canvas.Player);
        EmoteRig? rig = EmoteRig.Of(player);
        MeshRenderer? board = rig != null ? rig.Board : null;
        if (board == null) return;

        board.sharedMaterial = canvas.Material;
        canvas.Board = board;
    }

    private static void Detach(SignCanvas canvas)
    {
        MeshRenderer? board = canvas.Board;
        canvas.Board = null;
        if (board == null) return;

        Material? signMat = GetSignMat();
        if (signMat != null)
        {
            board.sharedMaterial = signMat;
        }
    }

    private static SignCanvas? Create()
    {
        uint[]? basePixels = AssetsLoader.BasePixels;
        Material? signMat = GetSignMat();
        if (basePixels == null || signMat == null) return null;

        InkCanvas ink = new(Constants.Draw.Width, Constants.Draw.Height, basePixels) { Font = AssetsLoader.Glyphs };
        Texture2D texture = new(Constants.Draw.Width, Constants.Draw.Height, TextureFormat.RGBA32, false, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixelData(ink.Pixels, 0);
        texture.Apply(false);

        Material material = new(signMat);
        material.SetTexture(BaseMap, texture);
        return new SignCanvas(ink, texture, material);
    }

    private static SignCanvas? Find(int player)
    {
        if (player < 0 || _active == 0) return null;

        for (int i = 0; i < _canvases.Count; i++)
        {
            if (_canvases[i].Player == player) return _canvases[i];
        }

        return null;
    }

    private static SignCanvas? FindFree()
    {
        for (int i = 0; i < _canvases.Count; i++)
        {
            if (_canvases[i].Player < 0) return _canvases[i];
        }

        return null;
    }

    private static bool IsSignOut(PlayerControllerB player)
    {
        EmoteRig? rig = EmoteRig.Of(player);
        return rig != null && (rig.Current == EmoteDefs.Sign || rig.Current == EmoteDefs.SignShown);
    }

    private static Material? GetSignMat()
    {
        if (_signMat == null)
        {
            GameObject? prefab = AssetsLoader.SignPrefab;
            MeshRenderer? board = prefab != null ? prefab.GetComponent<MeshRenderer>() : null;
            _signMat = board != null ? board.sharedMaterial : null;
        }

        return _signMat;
    }

    private sealed class SignCanvas
    {
        public SignCanvas(InkCanvas ink, Texture2D texture, Material material)
        {
            Ink = ink;
            Texture = texture;
            Material = material;
        }

        public InkCanvas Ink { get; }
        public Texture2D Texture { get; }
        public Material Material { get; }
        public int Player { get; set; } = -1;
        public MeshRenderer? Board { get; set; }
    }
}
