using GameNetcodeStuff;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class HeldItem
{
    public static void OnStarted(PlayerControllerB player, int emote)
    {
        EmoteRig rig = EmoteRig.Of(player)!;
        Show(rig);
        DisableBags(player, rig);
        rig.HidingItem = ConfigManager.HideHeldItem.Value || EmoteDefs.HidesItem(emote);
        if (rig.HidingItem)
        {
            Hide(player, rig);
        }
    }

    public static void OnEnded(PlayerControllerB player)
    {
        EmoteRig rig = EmoteRig.Of(player)!;
        Show(rig);
        rig.HidingItem = false;
        EnableBags(player, rig);
    }

    public static void Check(PlayerControllerB player, EmoteRig rig)
    {
        if (!rig.HidingItem || rig.Current == 0) return;

        GrabbableObject? now = player.isHoldingObject ? player.currentlyHeldObjectServer : null;
        if (now != rig.Held)
        {
            Show(rig);
            Hide(player, rig);
        }
    }

    public static void OnPocket(BeltBagItem bag)
    {
        PlayerControllerB? player = bag.playerHeldBy;
        if (player == null || !player.performingEmote) return;

        EmoteRig? rig = EmoteRig.Of(player);
        if (rig != null && rig.Current != 0) DisableBag(bag, rig);
    }

    private static void DisableBags(PlayerControllerB player, EmoteRig rig)
    {
        foreach (GrabbableObject? slot in player.ItemSlots)
        {
            DisableBag(slot, rig);
        }

        DisableBag(player.ItemOnlySlot, rig);
    }

    private static void DisableBag(GrabbableObject? item, EmoteRig rig)
    {
        if (item is not BeltBagItem bag || !bag.isPocketed) return;

        Collider trigger = bag.useBagTrigger.GetComponent<Collider>();
        if (!trigger.enabled) return;

        trigger.enabled = false;
        if (!rig.Bags.Contains(bag)) rig.Bags.Add(bag);
    }

    private static void EnableBags(PlayerControllerB player, EmoteRig rig)
    {
        foreach (BeltBagItem bag in rig.Bags)
        {
            if (bag == null || !bag.isPocketed || bag.playerHeldBy != player) continue;
            bag.useBagTrigger.GetComponent<Collider>().enabled = true;
        }
        rig.Bags.Clear();
    }

    private static void Hide(PlayerControllerB player, EmoteRig rig)
    {
        GrabbableObject? item = player.isHoldingObject ? player.currentlyHeldObjectServer : null;
        if (item == null || item.isPocketed) return;

        item.EnableItemMeshes(false);
        rig.Held = item;
    }

    private static void Show(EmoteRig rig)
    {
        GrabbableObject? item = rig.Held;
        if (item == null) return;

        rig.Held = null;
        if (!item.isPocketed)
        {
            item.EnableItemMeshes(true);
        }
    }
}
