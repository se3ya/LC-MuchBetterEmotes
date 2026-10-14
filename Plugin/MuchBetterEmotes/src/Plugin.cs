using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace MuchBetterEmotes;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(ModGUIDs.InputUtils)]
[BepInDependency(ModGUIDs.LethalNetworkAPI)]
[BepInDependency(ModGUIDs.LethalConfig, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.TooManyEmotes, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.LCVR, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.FirstPersonView, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

    public static Plugin Instance { get; private set; } = null!;
    public static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        Log = base.Logger;

        EmoteSync.Initialize();
        DrawSync.Initialize();
        if (!AssetsLoader.TryLoad()) return;

        InputHandlers.Init();
        ConfigManager.Initialize(Config);
        _harmony.PatchAll();

        DrawingStore.Initialize();
        EmoteControl.Started += DrawingStore.OnStarted;
        EmoteControl.Started += SignProp.OnStarted;
        EmoteControl.Started += DrawPanel.OnStarted;
        EmoteControl.Started += HeldItem.OnStarted;
        EmoteControl.Ended += SignProp.OnEnded;
        EmoteControl.Ended += HeldItem.OnEnded;
        EmoteControl.Ended += DrawPanel.OnEnded;
        EmoteControl.Blocked = player => TooManyEmotesCompat.IsEmoting(player) || LCVRCompat.InVR;

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");
    }
}
