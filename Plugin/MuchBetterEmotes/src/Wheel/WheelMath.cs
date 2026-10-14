using System;
using System.Collections.Generic;

namespace MuchBetterEmotes;

internal enum WheelZone
{
    Center,
    Ring,
    Outside
}

internal enum WheelAction
{
    None,
    Stop,
    Vanilla,
    Start
}

internal static class WheelMath
{
    public const float Step = 360f / Constants.Wheel.Blocks;

    public static WheelZone Zone(float dx, float dy, float scale)
    {
        float dist = (dx * dx) + (dy * dy);
        float inner = Constants.Wheel.Inner * scale;
        float outer = Constants.Wheel.Outer * scale;
        if (dist < inner * inner) return WheelZone.Center;
        return dist >= outer * outer ? WheelZone.Outside : WheelZone.Ring;
    }

    public static bool Stops(WheelZone zone, bool onStick, bool stopOutside, int id)
    {
        return stopOutside && !onStick && (zone != WheelZone.Ring || id == EmoteDefs.None);
    }

    public static bool HasSlot(WheelZone zone, bool stopOutside)
    {
        return zone == WheelZone.Ring || (zone == WheelZone.Outside && !stopOutside);
    }

    public static float Angle(float dx, float dy)
    {
        float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        return angle <= 0f ? angle + 360f : angle;
    }

    public static int Block(float dx, float dy)
    {
        return (int)Math.Round((Angle(dx, dy) - (Step * 1.5f)) / Step, MidpointRounding.ToEven);
    }

    public static int Slot(int block, int page) => block + 1 + (Constants.Wheel.Blocks * page);

    public static int PageCount(int count) => Math.Max(1, (count + Constants.Wheel.Blocks - 1) / Constants.Wheel.Blocks);

    public static int ClampPage(int page, int pages) => Math.Min(page, pages - 1);

    public static int[] Layout(Func<int, bool> enabled)
    {
        List<int> ids = new() { EmoteDefs.Dance, EmoteDefs.Point };
        for (int id = EmoteDefs.MiddleFinger; id <= EmoteDefs.SixSeven; id++)
        {
            if (enabled(id))
            {
                ids.Add(id);
            }
        }

        return ids.ToArray();
    }

    public static int Pick(int[] layout, int block, int page)
    {
        int slot = Slot(block, page);
        return slot >= 0 && slot < layout.Length ? layout[slot] : EmoteDefs.None;
    }

    public static WheelAction GetAction(bool stop, int id, bool performing)
    {
        if (stop) return performing ? WheelAction.Stop : WheelAction.None;
        if (id >= EmoteDefs.Dance && id <= EmoteDefs.Point) return WheelAction.Vanilla;

        return id >= EmoteDefs.MiddleFinger && id <= EmoteDefs.SixSeven ? WheelAction.Start : WheelAction.None;
    }
}
