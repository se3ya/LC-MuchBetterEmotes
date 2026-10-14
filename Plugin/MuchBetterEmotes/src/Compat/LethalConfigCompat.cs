using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;

namespace MuchBetterEmotes;

internal static class LethalConfigCompat
{
    private static bool? _present;

    private static bool Present => _present ??= Chainloader.PluginInfos.ContainsKey(ModGUIDs.LethalConfig);

    public static void Register(ConfigFile config)
    {
        if (!Present) return;

        try
        {
            RegisterEntries(config);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to register settings with LethalConfig: {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void RegisterEntries(ConfigFile config)
    {
        AssemblyDescriptionAttribute? attribute = typeof(Plugin).Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
        if (attribute != null)
        {
            LethalConfigManager.SetModDescription(attribute.Description);
        }

        foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in config)
        {
            switch (pair.Value)
            {
                case ConfigEntry<bool> flag:
                    LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(flag, requiresRestart: false));
                    break;

                case ConfigEntry<float> number:
                    LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(number, requiresRestart: false));
                    break;
            }
        }
    }
}
