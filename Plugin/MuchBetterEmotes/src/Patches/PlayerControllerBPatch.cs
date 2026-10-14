using System;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace MuchBetterEmotes;

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class PlayerControllerBPatch
{
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    [HarmonyAfter(ModGUIDs.LethalPhonesHarmony)]
    private static void CreateRig(PlayerControllerB __instance)
    {
        try
        {
            if (EmoteRig.Of(__instance) != null) return;

            GameObject? sign = Props.AddSign(__instance, out MeshRenderer? board, out MeshRenderer? back);
            Renderer? legs = null;
            if (!Chainloader.PluginInfos.ContainsKey(ModGUIDs.FirstPersonView))
            {
                legs = Props.AddLegs(__instance);
            }

            EmoteRig rig = __instance.playerBodyAnimator.gameObject.AddComponent<EmoteRig>();
            rig.Sign = sign;
            rig.Board = board;
            rig.Back = back;
            rig.Legs = legs;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to set up emotes for player {__instance.playerClientId}: {ex}");
        }
    }

    [HarmonyPatch("Update")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void UpdateRig(PlayerControllerB __instance)
    {
        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig == null) return;

        CheckStops(__instance, rig);
        HeldItem.Check(__instance, rig);

        float dt = Time.deltaTime;
        rig.Output = GetOutput(rig, __instance.emoteLayerWeight, dt);
        rig.Apply(dt);
        rig.Weights.Apply(rig);
    }

    [HarmonyPatch("Update")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void ApplySpeed(PlayerControllerB __instance)
    {
        if (!__instance.IsOwner || !__instance.performingEmote) return;

        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig == null) return;

        int id = rig.Current;
        if (id != EmoteDefs.Griddy && id != EmoteDefs.Prisyadka) return;

        float multiplier = id == EmoteDefs.Griddy ? ConfigManager.GriddySpeed.Value : ConfigManager.PrisyadkaSpeed.Value;
        rig.SpeedOriginal = __instance.movementSpeed;
        __instance.movementSpeed *= multiplier;
        rig.SpeedApplied = __instance.movementSpeed;
        rig.SpeedMultiplier = multiplier;
    }

    [HarmonyPatch("Update")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void UndoSpeed(PlayerControllerB __instance)
    {
        if (!__instance.IsOwner) return;

        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig == null || rig.SpeedMultiplier == 0f) return;

        float speed = __instance.movementSpeed;
        __instance.movementSpeed = speed == rig.SpeedApplied ? rig.SpeedOriginal : speed / rig.SpeedMultiplier;
        rig.SpeedMultiplier = 0f;
    }

    [HarmonyPatch("Update")]
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    private static void ResetSpeed(PlayerControllerB __instance)
    {
        UndoSpeed(__instance);
    }

    [HarmonyPatch("CheckConditionsForEmote")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void CheckEmote(PlayerControllerB __instance, ref bool __result)
    {
        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig == null) return;

        int id = rig.Requested != 0 ? rig.Requested : rig.Current;
        bool free = !__instance.inSpecialInteractAnimation && !__instance.isPlayerDead && !__instance.isJumping
            && !__instance.isCrouching && !__instance.isClimbingLadder && !__instance.isGrabbingObjectAnimation
            && !__instance.inTerminalMenu;

        if (id == EmoteDefs.Griddy || id == EmoteDefs.Prisyadka)
        {
            __result = free && Mathf.Abs(__instance.moveInputVector.x) < Constants.Emotes.StrafeLimit && !__instance.isSprinting
                && !__instance.isTypingChat;
        }
        else if (id == EmoteDefs.Sign || (id == EmoteDefs.SignShown && !ConfigManager.MoveWhileEmoting.Value))
        {
            __result = free && !__instance.isWalking;
        }
        else if (ConfigManager.MoveWhileEmoting.Value)
        {
            bool vanilla = id == EmoteDefs.Dance || id == EmoteDefs.Point || (id == EmoteDefs.None && __instance.performingEmote);
            if (id == EmoteDefs.None && !vanilla) return;

            if (vanilla && TooManyEmotesCompat.IsEmoting(__instance)) return;

            __result = free && !__instance.isSprinting && (!__instance.isTypingChat || rig.Requested == EmoteDefs.SignShown);
        }
    }

    [HarmonyPatch("PerformEmote")]
    [HarmonyPrefix]
    private static void SetRequest(PlayerControllerB __instance, int emoteID, out float __state)
    {
        __state = __instance.timeSinceStartingEmote;
        if (!__instance.IsOwner) return;

        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig != null)
        {
            rig.Requested = emoteID;
        }
    }

    [HarmonyPatch("PerformEmote")]
    [HarmonyPostfix]
    private static void CheckVanilla(PlayerControllerB __instance, int emoteID, float __state)
    {
        if ((emoteID == EmoteDefs.Dance || emoteID == EmoteDefs.Point) && __instance.timeSinceStartingEmote < __state)
        {
            EmoteControl.OnVanillaStart(__instance);
        }
    }

    [HarmonyPatch("PerformEmote")]
    [HarmonyFinalizer]
    private static void ClearRequest(PlayerControllerB __instance)
    {
        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig != null)
        {
            rig.Requested = 0;
        }
    }

    [HarmonyPatch("KillPlayer")]
    [HarmonyPostfix]
    private static void StopOnDeath(PlayerControllerB __instance)
    {
        EmoteRig? rig = EmoteRig.Of(__instance);
        if (rig == null || rig.Current == 0 || !__instance.IsOwner || !__instance.isPlayerDead) return;

        EmoteControl.End(__instance, FadeMode.Falling);
        EmoteControl.StopLocal(__instance);
    }

    private static float GetOutput(EmoteRig rig, float layerWeight, float dt)
    {
        float follow = Mathf.Clamp01(layerWeight / Constants.Rig.FadeShare);
        float output = rig.Output;
        switch (rig.Mode)
        {
            case FadeMode.Active:
                return follow;
            case FadeMode.Falling:
                output = Mathf.Min(output, follow);
                break;
            case FadeMode.Decay:
                output += (0f - output) * Mathf.Min(1f, Constants.Rig.DecayRate * dt);
                break;
            default:
                return 0f;
        }

        return output < Constants.Rig.Floor ? 0f : output;
    }

    private static void CheckStops(PlayerControllerB player, EmoteRig rig)
    {
        if (rig.Current == 0) return;

        if (!player.performingEmote)
        {
            EmoteControl.End(player, FadeMode.Falling);
        }
        else if (player.isPlayerDead || !player.isPlayerControlled)
        {
            EmoteControl.End(player, player.IsOwner ? FadeMode.Falling : FadeMode.Decay);
            if (player.IsOwner)
            {
                player.performingEmote = false;
            }
        }
        else if (TooManyEmotesCompat.IsEmoting(player))
        {
            EmoteControl.End(player, FadeMode.Decay);
        }
        else
        {
            rig.Mode = FadeMode.Active;
        }
    }
}
