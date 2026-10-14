using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class AssetsLoader
{
    public static bool CanDraw => BasePixels != null && PanelPrefab != null;
    public static AnimationClip[] Clips { get; private set; } = Array.Empty<AnimationClip>();
    public static AudioClip[] Claps { get; private set; } = Array.Empty<AudioClip>();
    public static GameObject? SignPrefab { get; private set; }
    public static GameObject? LegsPrefab { get; private set; }
    public static uint[]? BasePixels { get; private set; }
    public static GlyphFont? Glyphs { get; private set; }
    public static bool CanType => Glyphs != null;
    public static GameObject? WheelPrefab { get; private set; }
    public static GameObject? PanelPrefab { get; private set; }

    public static bool TryLoad()
    {
        string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        string path = Path.Combine(folder, Constants.Assets.Bundle);

        if (!File.Exists(path))
        {
            Plugin.Log.LogError($"Could not find the asset bundle at '{path}'.");
            return false;
        }

        AssetBundle? bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
        {
            Plugin.Log.LogError($"Could not load the asset bundle at '{path}'.");
            return false;
        }

        bool clipsMissing = false;
        AnimationClip[] clips = new AnimationClip[EmoteDefs.InputCount];
        AnimationClip? rest = Load<AnimationClip>(bundle, EmoteDefs.RestClip);
        if (rest != null)
        {
            clips[0] = rest;
        }
        else
        {
            clipsMissing = true;
        }

        for (int i = 0; i < EmoteDefs.All.Count; i++)
        {
            EmoteDef def = EmoteDefs.All[i];
            AnimationClip? clip = Load<AnimationClip>(bundle, def.Clip);
            if (clip != null)
            {
                clips[def.Input] = clip;
            }
            else
            {
                clipsMissing = true;
            }
        }

        List<AudioClip> claps = new();
        for (int i = 0; i < Constants.Assets.Claps.Length; i++)
        {
            AudioClip? clap = Load<AudioClip>(bundle, Constants.Assets.Claps[i]);
            if (clap != null)
            {
                claps.Add(clap);
            }
        }

        Claps = claps.ToArray();
        SignPrefab = Load<GameObject>(bundle, Constants.Assets.Sign);
        LegsPrefab = Load<GameObject>(bundle, Constants.Assets.Legs);
        WheelPrefab = Load<GameObject>(bundle, Constants.Assets.Wheel);
        PanelPrefab = Load<GameObject>(bundle, Constants.Assets.Panel);

        Texture2D? signBase = Load<Texture2D>(bundle, Constants.Assets.SignBase);
        if (signBase != null)
        {
            BasePixels = ReadPixels(signBase);
        }

        Texture2D? atlas = Load<Texture2D>(bundle, Constants.Assets.Glyphs);
        if (atlas != null)
        {
            Glyphs = ReadGlyphs(atlas);
        }

        if (clipsMissing) return false;

        Clips = clips;
        return true;
    }

    private static T? Load<T>(AssetBundle bundle, string name) where T : UnityEngine.Object
    {
        T? asset = bundle.LoadAsset<T>(name);
        if (asset == null)
        {
            Plugin.Log.LogError($"Could not find {typeof(T).Name} '{name}' in the asset bundle.");
        }

        return asset;
    }

    private static GlyphFont? ReadGlyphs(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        try
        {
            Color32[] colors = texture.GetPixels32();
            byte[] alpha = new byte[colors.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    alpha[(y * width) + x] = colors[((height - 1 - y) * width) + x].a;
                }
            }

            if (GlyphFont.TryCreate(alpha, width, height, out GlyphFont? font)) return font;

            Plugin.Log.LogError($"Could not use Texture2D '{Constants.Assets.Glyphs}' because it is {width} x {height} "
                + $"and not {Constants.Glyph.Columns * Constants.Glyph.Width} x {Constants.Glyph.Rows * Constants.Glyph.Height}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Could not read Texture2D '{Constants.Assets.Glyphs}': {ex.Message}");
        }

        return null;
    }

    private static uint[]? ReadPixels(Texture2D texture)
    {
        int width = Constants.Draw.Width;
        int height = Constants.Draw.Height;

        try
        {
            if (texture.width == width && texture.height == height && texture.format == TextureFormat.RGBA32)
            {
                return texture.GetPixelData<uint>(0).ToArray();
            }

            Plugin.Log.LogError($"Could not use Texture2D '{Constants.Assets.SignBase}' because it is "
                + $"{texture.width} x {texture.height} {texture.format} and not {width} x {height} RGBA32.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Could not read Texture2D '{Constants.Assets.SignBase}': {ex.Message}");
        }

        return null;
    }
}
