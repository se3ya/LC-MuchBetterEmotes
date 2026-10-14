using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using GameNetcodeStuff;

namespace MuchBetterEmotes;

internal static class TooManyEmotesCompat
{
    private const string TypeName = "TooManyEmotes.Compatibility.TooManyEmotesCompat";
    private const string MethodName = "IsPlayerEmoting";

    private static bool _ready;
    private static Func<PlayerControllerB, bool>? _isEmoting;

    public static bool IsEmoting(PlayerControllerB player)
    {
        if (!_ready)
        {
            Init();
        }

        Func<PlayerControllerB, bool>? check = _isEmoting;
        if (check == null) return false;

        try
        {
            return check(player);
        }
        catch (Exception ex)
        {
            _isEmoting = null;
            Plugin.Log.LogWarning(
                $"Failed to call TooManyEmotes' IsPlayerEmoting: "
                + $"{(ex.InnerException ?? ex).Message}");
            return false;
        }
    }

    private static void Init()
    {
        _ready = true;
        if (!Chainloader.PluginInfos.TryGetValue(ModGUIDs.TooManyEmotes, out PluginInfo info)) return;

        try
        {
            object? plugin = info.Instance;
            Type? type = plugin?.GetType().Assembly.GetType(TypeName, false);
            MethodInfo? method = type?.GetMethod(
                MethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(PlayerControllerB) }, null);
            if (method != null && method.ReturnType == typeof(bool))
            {
                _isEmoting = (Func<PlayerControllerB, bool>)Delegate.CreateDelegate(typeof(Func<PlayerControllerB, bool>), method);
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning(
                $"Failed to look up TooManyEmotes' IsPlayerEmoting: "
                + $"{(ex.InnerException ?? ex).Message}");
            return;
        }

        Plugin.Log.LogWarning("Could not find TooManyEmotes IsPlayerEmoting.");
    }
}
