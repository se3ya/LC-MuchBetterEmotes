namespace MuchBetterEmotes;

internal sealed class GlyphFont
{
    private const int Count = Constants.Glyph.Last - Constants.Glyph.First + 1;
    private const int AtlasWidth = Constants.Glyph.Columns * Constants.Glyph.Width;
    private const int AtlasHeight = Constants.Glyph.Rows * Constants.Glyph.Height;

    private readonly bool[] _ink;

    private GlyphFont(bool[] ink)
    {
        _ink = ink;
    }

    public static bool TryCreate(byte[] alpha, int width, int height, out GlyphFont? font)
    {
        font = null;
        if (width != AtlasWidth || height != AtlasHeight || alpha.Length != width * height) return false;

        bool[] ink = new bool[Count * Constants.Glyph.Width * Constants.Glyph.Height];
        for (int glyph = 0; glyph < Count; glyph++)
        {
            int left = (glyph % Constants.Glyph.Columns) * Constants.Glyph.Width;
            int top = glyph / Constants.Glyph.Columns * Constants.Glyph.Height;
            for (int y = 0; y < Constants.Glyph.Height; y++)
            {
                for (int x = 0; x < Constants.Glyph.Width; x++)
                {
                    ink[(((glyph * Constants.Glyph.Height) + y) * Constants.Glyph.Width) + x] = alpha[((top + y) * width) + left + x] > 127;
                }
            }
        }

        font = new GlyphFont(ink);
        return true;
    }

    public static bool IsGlyph(int code) => code >= Constants.Glyph.First && code <= Constants.Glyph.Last;

    public static bool IsValidText(string? text)
    {
        if (text == null || text.Length < 1 || text.Length > Constants.Draw.MaxText) return false;
        foreach (char c in text)
        {
            if (!IsGlyph(c)) return false;
        }
        return true;
    }

    public static char ValidateChar(string text, int index, char added)
    {
        if (text.Length >= Constants.Draw.MaxText || !IsGlyph(added)) return '\0';
        return added;
    }

    public bool IsInk(int code, int x, int y) =>
        _ink[((((code - Constants.Glyph.First) * Constants.Glyph.Height) + y) * Constants.Glyph.Width) + x];
}
