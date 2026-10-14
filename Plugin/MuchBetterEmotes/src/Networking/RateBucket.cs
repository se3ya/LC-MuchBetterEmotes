using System;
using System.Collections.Generic;

namespace MuchBetterEmotes;

internal sealed class RateBucket
{
    private readonly int _size;
    private readonly float _rate;
    private float _tokens;
    private float _time;

    public RateBucket(int size, float rate)
    {
        _size = size;
        _rate = rate;
        _tokens = size;
    }

    public static bool TakeToken(Dictionary<int, RateBucket> buckets, int index, float now, int size, float rate)
    {
        if (!buckets.TryGetValue(index, out RateBucket? bucket))
        {
            bucket = new RateBucket(size, rate);
            buckets[index] = bucket;
        }

        return bucket.Take(now);
    }

    public bool Take(float now)
    {
        _tokens = Math.Min(_size, _tokens + (Math.Max(0f, now - _time) * _rate));
        _time = now;
        if (_tokens < 1f) return false;

        _tokens -= 1f;
        return true;
    }
}
