namespace MuchBetterEmotes;

internal static class ThumbMath
{
    public static void Downsample(uint[] source, int width, int height, uint[] target, int thumbWidth, int thumbHeight)
    {
        for (int y = 0; y < thumbHeight; y++)
        {
            int top = y * height / thumbHeight;
            int bottom = System.Math.Max((y + 1) * height / thumbHeight, top + 1);
            for (int x = 0; x < thumbWidth; x++)
            {
                int left = x * width / thumbWidth;
                int right = System.Math.Max((x + 1) * width / thumbWidth, left + 1);
                uint r = 0;
                uint g = 0;
                uint b = 0;
                uint a = 0;
                for (int row = top; row < bottom; row++)
                {
                    for (int column = left; column < right; column++)
                    {
                        uint pixel = source[(row * width) + column];
                        r += pixel & 0xFF;
                        g += (pixel >> 8) & 0xFF;
                        b += (pixel >> 16) & 0xFF;
                        a += pixel >> 24;
                    }
                }

                uint count = (uint)((bottom - top) * (right - left));
                uint half = count / 2;
                target[(y * thumbWidth) + x] = ((r + half) / count) | (((g + half) / count) << 8)
                    | (((b + half) / count) << 16) | (((a + half) / count) << 24);
            }
        }
    }
}
