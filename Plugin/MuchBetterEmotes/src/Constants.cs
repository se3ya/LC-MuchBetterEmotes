namespace MuchBetterEmotes;

internal static class Constants
{
    internal static class Rig
    {
        public const ushort SortingOrder = 600;
        public const float Floor = 0.001f;
        public const float FadeShare = 0.3f;
        public const float StopFade = 0.1f;
        public const float DecayRate = 10f;
    }

    internal static class Emotes
    {
        public const float Cooldown = 0.5f;
        public const float GriddySpeed = 0.5f;
        public const float PrisyadkaSpeed = 0.34f;
        public const float StrafeLimit = 0.3f;
        public const int BucketSize = 16;
        public const float BucketRate = 4f;
    }

    internal static class Assets
    {
        public const string Bundle = "muchbetteremotes";
        public const string Sign = "Sign";
        public const string Legs = "plegs";
        public const string SignBase = "SignBase";
        public const string Glyphs = "SignGlyphs";
        public const string Wheel = "MoreEmotesMenu";
        public const string Panel = "SignPanel";
        public static readonly string[] Claps = { "SingleClapEmote1", "SingleClapEmote2" };
    }

    internal static class Wheel
    {
        public const float Inner = 74.39f;
        public const float Outer = 167.32f;
        public const int Blocks = 8;
    }

    internal static class Draw
    {
        public const int Width = 512;
        public const int Height = 340;
        public const int MaxPoints = 8192;
        public const float SampleInterval = 1f / 30f;
        public const int MinSize = 2;
        public const int MaxSize = 32;
        public const int MaxText = 32;
        public const int MaxScale = 4;
        public const int Slots = 8;
    }

    internal static class Sync
    {
        public const int ChunkSize = 255;
        public const float FlushInterval = 0.15f;
        public const int BucketSize = 128;
        public const float BucketRate = 20f;
        public const int EditBucketSize = 12;
        public const float EditBucketRate = 12f;
    }

    internal static class Replay
    {
        public const int Budget = 280000;
        public const int LocalBudget = 1120000;
        public const float StepTime = 0.0012f;
        public const float LocalTime = 0.0048f;
        public const int ClockEvery = 32;
    }

    internal static class Brush
    {
        public const int Size = 8;
        public const int EraseSize = 16;
        public const int FillSize = 1;
        public const int TextSize = 128;
        public const int WallLuma = 128;
        public const float MinMove = 3f;
        public const float ReleaseMove = 1f;
    }

    internal static class Glyph
    {
        public const int Width = 8;
        public const int Height = 16;
        public const int Columns = 16;
        public const int Rows = 6;
        public const int First = 32;
        public const int Last = 126;
    }

    internal static class Panel
    {
        public const float Confirm = 2f;
        public const int ThumbWidth = 128;
        public const int ThumbHeight = 85;
        public static readonly int[] Presets = { 4, 8, 16 };
        public static readonly int[] TextPresets = { 1, 2, 4 };
        public static readonly uint[] Palette =
        {
            0xFF000000u, 0xFFFFFFFFu, 0xFF2828DCu, 0xFF1E8CF0u, 0xFF28D7F5u, 0xFF46AA3Cu, 0xFFDC6428u, 0xFFBE3C8Cu
        };
    }

    internal static class Pad
    {
        public const float Speed = 1400f;
        public const float SlowSpeed = 140f;
        public const float Zone = 0.4f;
    }
}
