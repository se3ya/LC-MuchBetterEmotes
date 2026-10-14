using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace MuchBetterEmotes;

internal static class ConfigManager
{
    private const float MinSpeed = 0.05f;

    private static class Sections
    {
        public const string Emotes = "1. Emotes";
        public const string Movement = "2. Movement";
        public const string Wheel = "3. Wheel";
        public const string Sign = "4. Sign";
    }

    public static ConfigEntry<bool> MiddleFinger { get; private set; } = null!;
    public static ConfigEntry<bool> Clap { get; private set; } = null!;
    public static ConfigEntry<bool> Shy { get; private set; } = null!;
    public static ConfigEntry<bool> Griddy { get; private set; } = null!;
    public static ConfigEntry<bool> Twerk { get; private set; } = null!;
    public static ConfigEntry<bool> Salute { get; private set; } = null!;
    public static ConfigEntry<bool> Prisyadka { get; private set; } = null!;
    public static ConfigEntry<bool> Sign { get; private set; } = null!;
    public static ConfigEntry<bool> ThumbsUp { get; private set; } = null!;
    public static ConfigEntry<bool> Wave { get; private set; } = null!;
    public static ConfigEntry<bool> SixSeven { get; private set; } = null!;
    public static ConfigEntry<bool> MoveWhileEmoting { get; private set; } = null!;
    public static ConfigEntry<bool> HideHeldItem { get; private set; } = null!;
    public static ConfigEntry<bool> ShowOthers { get; private set; } = null!;
    public static ConfigEntry<bool> ShowWhileDrawing { get; private set; } = null!;
    public static ConfigEntry<bool> RememberDrawing { get; private set; } = null!;
    public static ConfigEntry<float> GriddySpeed { get; private set; } = null!;
    public static ConfigEntry<float> PrisyadkaSpeed { get; private set; } = null!;
    public static ConfigEntry<bool> StopOutside { get; private set; } = null!;
    public static ConfigEntry<float> Deadzone { get; private set; } = null!;

    public static void Initialize(ConfigFile config)
    {
        config.SaveOnConfigSet = false;

        MiddleFinger = config.Bind(Sections.Emotes, "Middle Finger", true,
            "Emote toggle.");
        Clap = config.Bind(Sections.Emotes, "Clap", true,
            "Emote toggle.");
        Shy = config.Bind(Sections.Emotes, "Shy", true,
            "Emote toggle.");
        Griddy = config.Bind(Sections.Emotes, "The Griddy", true,
            "Emote toggle.");
        Twerk = config.Bind(Sections.Emotes, "Twerk", true,
            "Emote toggle.");
        Salute = config.Bind(Sections.Emotes, "Salute", true,
            "Emote toggle.");
        Prisyadka = config.Bind(Sections.Emotes, "Prisyadka", true,
            "Emote toggle.");
        Sign = config.Bind(Sections.Emotes, "Sign", true,
            "Emote toggle.");
        ThumbsUp = config.Bind(Sections.Emotes, "Thumbs Up", true,
            "Emote toggle.");
        Wave = config.Bind(Sections.Emotes, "Wave", true,
            "Emote toggle.");
        SixSeven = config.Bind(Sections.Emotes, "6 7", true,
            "Emote toggle.");
        MoveWhileEmoting = config.Bind(Sections.Emotes, "Move While Emoting", false,
            "Lets you walk during every emote.");
        HideHeldItem = config.Bind(Sections.Emotes, "Hide Held Item", true,
            "Hides item in players hand while emoting.");

        GriddySpeed = config.Bind(Sections.Movement, "Griddy Speed", Constants.Emotes.GriddySpeed, new ConfigDescription(
            "Walk speed during the Griddy. REQUIRES 'The Griddy' SET TO TRUE.",
            new AcceptableValueRange<float>(MinSpeed, 1f)));
        PrisyadkaSpeed = config.Bind(Sections.Movement, "Prisyadka Speed", Constants.Emotes.PrisyadkaSpeed, new ConfigDescription(
            "Walk speed during Prisyadka. REQUIRES 'Prisyadka' SET TO TRUE.",
            new AcceptableValueRange<float>(MinSpeed, 1f)));

        StopOutside = config.Bind(Sections.Wheel, "Stop Outside Slots", true,
            "Releasing the wheel outside the slots stops your emote.");
        Deadzone = config.Bind(Sections.Wheel, "Controller Deadzone", 0.25f, new ConfigDescription(
            "Fraction of full tilt the right stick needs to pick slot. Letting go inside it cancels.",
            new AcceptableValueRange<float>(0f, 0.9f)));

        ShowOthers = config.Bind(Sections.Sign, "Show Other Players Drawings", true,
            "If false, keeps other players signs empty for you. If true, signs shows their drawings when they show the sign.");
        ShowWhileDrawing = config.Bind(Sections.Sign, "Show While Drawing", false,
            "If true, other players see each line as you draw. If false, they see your drawing only when you show the sign.");
        RememberDrawing = config.Bind(Sections.Sign, "Remember Drawing", true,
            "Saves your drawing when panel closes.");

        ClearOrphanedEntries(config);
        config.Save();
        config.SaveOnConfigSet = true;

        SignCanvases.ShowOthers = ShowOthers.Value;
        ShowOthers.SettingChanged += OnShowChanged;

        LethalConfigCompat.Register(config);
    }

    public static bool Enabled(int emote)
    {
        return emote switch
        {
            EmoteDefs.MiddleFinger or EmoteDefs.DoubleMiddleFinger => MiddleFinger.Value,
            EmoteDefs.Clap or EmoteDefs.DoubleClap => Clap.Value,
            EmoteDefs.Shy => Shy.Value,
            EmoteDefs.Griddy => Griddy.Value,
            EmoteDefs.Twerk => Twerk.Value,
            EmoteDefs.Salute => Salute.Value,
            EmoteDefs.Prisyadka => Prisyadka.Value,
            EmoteDefs.Sign or EmoteDefs.SignShown => Sign.Value,
            EmoteDefs.ThumbsUp => ThumbsUp.Value,
            EmoteDefs.Wave => Wave.Value,
            EmoteDefs.SixSeven => SixSeven.Value,
            _ => true
        };
    }

    private static void OnShowChanged(object sender, EventArgs args)
    {
        SignCanvases.ShowOthers = ShowOthers.Value;
    }

    private static void ClearOrphanedEntries(ConfigFile config)
    {
        try
        {
            PropertyInfo? orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            if (orphans != null && orphans.GetValue(config) is Dictionary<ConfigDefinition, string> entries)
            {
                entries.Clear();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not clear orphaned config entries: {ex.Message}");
        }
    }
}
