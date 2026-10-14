namespace MuchBetterEmotes;

internal sealed class Stroke
{
    public readonly int Color;
    public readonly int Size;
    public readonly ushort[] X;
    public readonly ushort[] Y;

    public Stroke(int color, int size, int count)
    {
        Color = color;
        Size = size;
        X = new ushort[count];
        Y = new ushort[count];
    }
}
