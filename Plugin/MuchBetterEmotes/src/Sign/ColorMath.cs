using System;

namespace MuchBetterEmotes;

internal static class ColorMath
{
    private const float WheelPressLimit = 1.08f;
    private const float WheelMinValue = 0.05f;

    public static uint ToPixel(int rgb)
    {
        uint red = (uint)(rgb >> 16) & 0xFF;
        uint green = (uint)(rgb >> 8) & 0xFF;
        uint blue = (uint)rgb & 0xFF;
        return 0xFF000000u | (blue << 16) | (green << 8) | red;
    }

    public static int ToRgb(uint pixel)
    {
        uint red = pixel & 0xFF;
        uint green = (pixel >> 8) & 0xFF;
        uint blue = (pixel >> 16) & 0xFF;
        return (int)((red << 16) | (green << 8) | blue);
    }

    public static string ToHex(int rgb) => "#" + (rgb & 0xFFFFFF).ToString("X6");

    public static bool TryParseHex(string? text, out int rgb)
    {
        rgb = 0;
        if (text == null) return false;

        int start = text.Length > 0 && text[0] == '#' ? 1 : 0;
        int digits = text.Length - start;
        if (digits != 3 && digits != 6) return false;

        int value = 0;
        for (int i = start; i < text.Length; i++)
        {
            int digit = HexValue(text[i]);
            if (digit < 0) return false;
            value = (value << 4) | digit;
        }

        if (digits == 3)
        {
            value = (((value >> 8) & 0xF) * 0x110000) + (((value >> 4) & 0xF) * 0x1100) + ((value & 0xF) * 0x11);
        }

        rgb = value;
        return true;
    }

    public static int FromHsv(float hue, float sat, float val)
    {
        hue -= (float)Math.Floor(hue);
        sat = Math.Clamp(sat, 0f, 1f);
        val = Math.Clamp(val, 0f, 1f);
        float scaled = hue * 6f;
        int sector = (int)scaled % 6;
        float fraction = scaled - (int)scaled;
        float low = val * (1f - sat);
        float falling = val * (1f - (sat * fraction));
        float rising = val * (1f - (sat * (1f - fraction)));
        float red;
        float green;
        float blue;
        switch (sector)
        {
            case 0: red = val; green = rising; blue = low; break;
            case 1: red = falling; green = val; blue = low; break;
            case 2: red = low; green = val; blue = rising; break;
            case 3: red = low; green = falling; blue = val; break;
            case 4: red = rising; green = low; blue = val; break;
            default: red = val; green = low; blue = falling; break;
        }

        return (Channel(red) << 16) | (Channel(green) << 8) | Channel(blue);
    }

    public static void ToHsv(int rgb, ref float hue, ref float sat, out float val)
    {
        float red = ((rgb >> 16) & 0xFF) / 255f;
        float green = ((rgb >> 8) & 0xFF) / 255f;
        float blue = (rgb & 0xFF) / 255f;
        float max = Math.Max(red, Math.Max(green, blue));
        float min = Math.Min(red, Math.Min(green, blue));
        float delta = max - min;
        val = max;
        if (max <= 0f || delta <= 0f)
        {
            if (max <= 0f) return;
            sat = 0f;
            return;
        }

        sat = delta / max;
        float sector;
        if (max == red) sector = (green - blue) / delta;
        else if (max == green) sector = 2f + ((blue - red) / delta);
        else sector = 4f + ((red - green) / delta);
        hue = sector / 6f;
        hue -= (float)Math.Floor(hue);
    }

    public static bool PickWheel(float unitX, float unitY, bool press, ref float hue, ref float sat, ref float val)
    {
        float x = (unitX - 0.5f) * 2f;
        float y = (unitY - 0.5f) * 2f;
        float radius = (float)Math.Sqrt((x * x) + (y * y));
        if (press && radius > WheelPressLimit) return false;

        if (radius > 1f)
        {
            x /= radius;
            y /= radius;
            radius = 1f;
        }

        hue = (float)(Math.Atan2(y, x) / (2.0 * Math.PI));
        hue -= (float)Math.Floor(hue);
        sat = radius;
        if (press && val < WheelMinValue) val = 1f;
        return true;
    }

    public static void WheelPoint(float hue, float sat, out float x, out float y)
    {
        double angle = hue * 2.0 * Math.PI;
        x = (float)Math.Cos(angle) * sat;
        y = (float)Math.Sin(angle) * sat;
    }

    public static char ValidateHex(string text, int index, char letter)
    {
        if (letter == '#') return index == 0 && text.IndexOf('#') < 0 ? letter : '\0';
        if (HexValue(letter) < 0) return '\0';

        int digits = 0;
        for (int i = 0; i < text.Length; i++)
            if (HexValue(text[i]) >= 0) digits++;

        return digits >= 6 ? '\0' : char.ToUpperInvariant(letter);
    }

    private static int Channel(float value) => (int)((value * 255f) + 0.5f);

    private static int HexValue(char letter)
    {
        if (letter >= '0' && letter <= '9') return letter - '0';
        if (letter >= 'a' && letter <= 'f') return letter - 'a' + 10;
        if (letter >= 'A' && letter <= 'F') return letter - 'A' + 10;
        return -1;
    }
}
