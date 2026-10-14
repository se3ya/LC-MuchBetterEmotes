using System;

namespace MuchBetterEmotes;

[Serializable]
public struct StartData
{
    public int Player;
    public int Emote;

    public StartData(int player, int emote)
    {
        Player = player;
        Emote = emote;
    }

    public bool IsValid => Player >= 0 && EmoteDefs.IsWireId(Emote);
}
