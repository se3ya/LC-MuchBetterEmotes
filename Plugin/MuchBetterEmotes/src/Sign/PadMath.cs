using System;

namespace MuchBetterEmotes;

internal static class PadMath
{
    public static int ScrollStep(float delta) => delta > 0f ? 1 : delta < 0f ? -1 : 0;

    public static float Speed(float x, float y, float deadzone)
    {
        float length = (float)Math.Sqrt((x * x) + (y * y));
        if (length <= deadzone) return 0f;

        float tilt = Math.Min(1f, (length - deadzone) / (1f - deadzone));
        float zone = Constants.Pad.Zone;
        float slow = Constants.Pad.SlowSpeed;
        if (tilt < zone) return slow * tilt / zone;

        float rest = (tilt - zone) / (1f - zone);
        return slow + ((Constants.Pad.Speed - slow) * rest * rest);
    }

    public static void Velocity(float x, float y, float deadzone, out float vx, out float vy)
    {
        float length = (float)Math.Sqrt((x * x) + (y * y));
        float speed = Speed(x, y, deadzone);
        if (speed <= 0f || length <= 0f)
        {
            vx = 0f;
            vy = 0f;
            return;
        }

        vx = x / length * speed;
        vy = y / length * speed;
    }

    public static PenTool NextTool(PenTool tool)
    {
        switch (tool)
        {
            case PenTool.Brush: return PenTool.Eraser;
            case PenTool.Eraser: return PenTool.Fill;
            default: return PenTool.Brush;
        }
    }

    public static int Swatch(int current, int dir, int count)
    {
        if (current < 0) return dir > 0 ? 0 : count - 1;
        return (((current + dir) % count) + count) % count;
    }
}
