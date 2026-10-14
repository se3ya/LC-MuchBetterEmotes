using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MuchBetterEmotes;

internal enum RigPart
{
    RightArm,
    LeftArm,
    FpRightArm,
    FpLeftArm,
    LookHead,
    LookHead2
}

internal sealed class RigWeights
{
    private const int PartCount = (int)RigPart.LookHead2 + 1;

    private static readonly int[][] _inputs =
    {
        new[] { 8, 10, 11 },
        new[] { 10, 11 },
        new[] { 6, 9, 10, 11 },
        new[] { 6, 9, 10, 11 },
        new[] { 7, 10, 11 },
        new[] { 10 }
    };

    private readonly IRigConstraint?[] _constraints = new IRigConstraint?[PartCount];
    private readonly float[] _rest = new float[PartCount];
    private bool _rested;

    public RigWeights(PlayerControllerB player)
    {
        _constraints[(int)RigPart.RightArm] = player.rightArmRig;
        _constraints[(int)RigPart.LeftArm] = player.leftArmRig;
        _constraints[(int)RigPart.FpRightArm] = player.rightArmRigSecondaryLocal;
        _constraints[(int)RigPart.FpLeftArm] = player.leftArmRigSecondaryLocal;
        _constraints[(int)RigPart.LookHead] = player.cameraLookRig1;
        _constraints[(int)RigPart.LookHead2] = player.cameraLookRig2;
    }

    public void Capture()
    {
        for (int i = 0; i < PartCount; i++)
        {
            IRigConstraint? constraint = _constraints[i];
            if (constraint != null && IsFp((RigPart)i))
            {
                _rest[i] = constraint.weight;
            }
        }
        _rested = true;
    }

    public void Apply(EmoteRig rig)
    {
        Blend blend = rig.Blend;
        if (rig.Output == 0f && blend.Target == 0 && !blend.Fading)
        {
            if (!_rested)
            {
                WriteRest();
                _rested = true;
            }
            return;
        }

        _rested = false;
        for (int i = 0; i < PartCount; i++)
        {
            IRigConstraint? constraint = _constraints[i];
            if (constraint == null) continue;

            float baseValue = IsFp((RigPart)i) ? _rest[i] : constraint.weight;
            constraint.weight = baseValue * (1f - Mathf.Clamp01(rig.Output * blend.Share(_inputs[i])));
        }
    }

    private void WriteRest()
    {
        for (int i = 0; i < PartCount; i++)
        {
            IRigConstraint? constraint = _constraints[i];
            if (constraint != null && IsFp((RigPart)i))
            {
                constraint.weight = _rest[i];
            }
        }
    }

    private static bool IsFp(RigPart part) => part == RigPart.FpRightArm || part == RigPart.FpLeftArm;
}
