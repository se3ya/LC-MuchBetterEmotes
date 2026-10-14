using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace MuchBetterEmotes;

internal static class LCVRCompat
{
    private const string PluginType = "LCVR.Plugin";
    private const string FlagsType = "LCVR.Flags";
    private const string FlagsMember = "Flags";
    private const string VRMember = "VR";

    private static bool _ready;
    private static bool _inVR;

    public static bool InVR
    {
        get
        {
            if (!_ready)
            {
                Init();
            }

            return _inVR;
        }
    }

    private static void Init()
    {
        _ready = true;
        if (!Chainloader.PluginInfos.TryGetValue(ModGUIDs.LCVR, out PluginInfo info)) return;

        try
        {
            object? plugin = info.Instance;
            if (plugin != null && TryRead(plugin.GetType().Assembly, out bool vr))
            {
                _inVR = vr;
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning(
                $"Failed to read LCVR VR flag: {(ex.InnerException ?? ex).Message}");
            return;
        }

        Plugin.Log.LogWarning("Could not read LCVR VR flag.");
    }

    private static bool TryRead(Assembly assembly, out bool vr)
    {
        vr = false;
        Type? plugin = assembly.GetType(PluginType, false);
        Type? flags = assembly.GetType(FlagsType, false);
        PropertyInfo? property = plugin?.GetProperty(FlagsMember, BindingFlags.Public | BindingFlags.Static);
        if (property == null || flags == null || !flags.IsEnum || property.PropertyType != flags || !Enum.IsDefined(flags, VRMember))
        {
            return false;
        }

        object? value = property.GetValue(null);
        if (value == null) return false;

        vr = ((Enum)value).HasFlag((Enum)Enum.Parse(flags, VRMember));
        return true;
    }
}
