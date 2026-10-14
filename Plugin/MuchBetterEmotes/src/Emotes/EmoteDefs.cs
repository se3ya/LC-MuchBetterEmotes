using System.Collections.Generic;

namespace MuchBetterEmotes;

internal readonly struct EmoteDef
{
    public readonly int Id;
    public readonly string Name;
    public readonly string Clip;
    public readonly int Input;
    public readonly float Speed;
    public readonly float Fade;
    public readonly float LoopStart;

    public EmoteDef(int id, string name, string clip, int input, float speed, float fade, float loopStart)
    {
        Id = id;
        Name = name;
        Clip = clip;
        Input = input;
        Speed = speed;
        Fade = fade;
        LoopStart = loopStart;
    }
}

internal static class EmoteDefs
{
    public const int None = 0;
    public const int Dance = 1;
    public const int Point = 2;
    public const int MiddleFinger = 3;
    public const int Clap = 4;
    public const int Shy = 5;
    public const int Griddy = 6;
    public const int Twerk = 7;
    public const int Salute = 8;
    public const int Prisyadka = 9;
    public const int Sign = 10;
    public const int ThumbsUp = 11;
    public const int Wave = 12;
    public const int SixSeven = 13;
    public const int DoubleMiddleFinger = 1003;
    public const int DoubleClap = 1004;
    public const int SignShown = 1010;

    public const int InputCount = 15;
    public const string RestClip = "EmoteRest";

    private static readonly EmoteDef[] _all =
    {
        new EmoteDef(MiddleFinger, "Middle Finger", "Middle_Finger", 1, 1f, 0.1f, 0f),
        new EmoteDef(DoubleMiddleFinger, "Double Middle Finger", "D_Middle_Finger", 2, 1f, 0.3f, 0f),
        new EmoteDef(Clap, "Clap", "Clap", 3, 1.11f, 0.25f, 0f),
        new EmoteDef(DoubleClap, "Double Clap", "D_Clap", 4, 1.4f, 0.1f, 0f),
        new EmoteDef(Shy, "Shy", "Shy", 5, 1f, 0.1f, 0f),
        new EmoteDef(Griddy, "The Griddy", "The_Griddy", 6, 1f, 0.25f, 0f),
        new EmoteDef(Twerk, "Twerk", "Twerk", 7, 1f, 0.2f, 0f),
        new EmoteDef(Salute, "Salute", "Salute", 8, 1f, 0.1f, 0f),
        new EmoteDef(Prisyadka, "Prisyadka", "Prisyadka", 9, 1.05f, 0.2f, 0f),
        new EmoteDef(Sign, "Sign", "Sign", 10, 1f, 0.1f, 0f),
        new EmoteDef(SignShown, "Sign Shown", "D_Sign", 11, 1f, 0.1f, 0f),
        new EmoteDef(ThumbsUp, "Thumbs Up", "ThumbsUp", 12, 1f, 0.1f, 0f),
        new EmoteDef(Wave, "Wave", "Wave", 13, 1f, 0.1f, 0.4f),
        new EmoteDef(SixSeven, "6 7", "SixSeven", 14, 1f, 0.1f, 0.3f)
    };

    public static IReadOnlyList<EmoteDef> All => _all;

    public static bool IsWireId(int id)
    {
        return id == None || TryGet(id, out _);
    }

    public static bool HidesItem(int id)
    {
        return id == ThumbsUp || id == Wave || id == SixSeven;
    }

    public static bool HasSteps(int id)
    {
        return id == Griddy || id == Salute || id == Prisyadka;
    }

    public static bool LoopsSteps(int id)
    {
        return id == Griddy || id == Prisyadka;
    }

    public static bool TryGet(int id, out EmoteDef def)
    {
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i].Id == id)
            {
                def = _all[i];
                return true;
            }
        }
        def = default;
        return false;
    }

    public static string Label(int id)
    {
        if (id == Dance) return "Dance";
        if (id == Point) return "Point";
        return TryGet(id, out EmoteDef def) ? def.Name : "Empty";
    }

    public static int Next(int current, int pressed)
    {
        if (pressed == MiddleFinger || pressed == Clap) return current == pressed ? pressed + 1000 : pressed;
        if (pressed == current) return -1;
        return pressed;
    }
}
