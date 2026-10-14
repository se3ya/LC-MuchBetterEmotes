using HarmonyLib;

namespace MuchBetterEmotes;

[HarmonyPatch(typeof(BeltBagItem))]
internal static class BeltBagItemPatch
{
    [HarmonyPatch(nameof(BeltBagItem.PocketItem))]
    [HarmonyPostfix]
    private static void KeepOff(BeltBagItem __instance) => HeldItem.OnPocket(__instance);
}
