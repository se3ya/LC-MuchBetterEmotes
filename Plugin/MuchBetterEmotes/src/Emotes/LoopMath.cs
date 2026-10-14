namespace MuchBetterEmotes;

internal static class LoopMath
{
    public static float Wrap(float time, float length, float loopStart)
    {
        if (loopStart <= 0f || loopStart >= length || time < length) return time;
        return loopStart + ((time - length) % (length - loopStart));
    }

    public static bool TryWrap(float time, float dt, float speed, float length, float loopStart, out float wrapped)
    {
        float next = time + (dt * speed);
        wrapped = Wrap(next, length, loopStart);
        return wrapped != next;
    }
}
