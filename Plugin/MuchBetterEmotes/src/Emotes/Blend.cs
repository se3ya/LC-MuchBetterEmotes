using System;
using System.Collections.Generic;

namespace MuchBetterEmotes;

internal enum FadeMode
{
    Off,
    Active,
    Falling,
    Decay
}

internal sealed class Blend
{
    private readonly float[] _start;
    private readonly float[] _weights;
    private float _time;
    private float _duration;

    public Blend(int count)
    {
        _start = new float[count];
        _weights = new float[count];
        _weights[0] = 1f;
    }

    public int Target { get; private set; }
    public bool Fading { get; private set; }
    public IReadOnlyList<float> Weights => _weights;

    public float Share(int[] inputs)
    {
        float share = 0f;
        for (int i = 0; i < inputs.Length; i++)
        {
            share += _weights[inputs[i]];
        }
        return share;
    }

    public void FadeTo(int input, float duration)
    {
        Array.Copy(_weights, _start, _weights.Length);
        Target = input;
        _time = 0f;
        _duration = duration;
        Fading = true;
    }

    public void Snap(int input)
    {
        Array.Clear(_weights, 0, _weights.Length);
        _weights[0] = Constants.Rig.Floor;
        _weights[input] = 1f - Constants.Rig.Floor;
        Target = input;
        Fading = false;
    }

    public void Update(float dt)
    {
        if (!Fading) return;
        _time += dt;
        float m = Math.Min(1f, _time / _duration);
        for (int i = 0; i < _weights.Length; i++)
        {
            _weights[i] = (_start[i] * (1f - m)) + (i == Target ? m : 0f);
        }
        if (Target != 0)
        {
            KeepFloor();
        }
        if (m >= 1f)
        {
            Fading = false;
        }
    }

    private void KeepFloor()
    {
        float first = _weights[0];
        if (first >= Constants.Rig.Floor) return;
        float scale = (1f - Constants.Rig.Floor) / (1f - first);
        for (int i = 1; i < _weights.Length; i++)
        {
            _weights[i] *= scale;
        }
        _weights[0] = Constants.Rig.Floor;
    }
}
