using System.Collections.Generic;
using GameNetcodeStuff;
using LethalNetworkAPI;
using LethalNetworkAPI.Utils;
using Unity.Netcode;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class EmoteSync
{
    private static LNetworkMessage<StartData> _startMessage = null!;
    private static readonly Dictionary<int, RateBucket> _buckets = new();

    public static void Initialize()
    {
        _startMessage = LNetworkMessage<StartData>.Connect(
            "MuchBetterEmotes_Start",
            onServerReceived: OnServerReceived,
            onClientReceived: OnClientReceived);
    }

    public static void SendStart(int player, int emote)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (!network.IsConnectedClient) return;

        _startMessage.SendServer(new StartData(player, emote));
    }

    private static void OnServerReceived(StartData message, ulong sender)
    {
        if (!message.IsValid) return;

        PlayerControllerB? player = EmoteControl.GetPlayer(message.Player);
        if (player == null || player.OwnerClientId != sender) return;

        if (!RateBucket.TakeToken(_buckets, message.Player, Time.realtimeSinceStartup,
                Constants.Emotes.BucketSize, Constants.Emotes.BucketRate))
        {
            return;
        }

        if (message.Emote != EmoteDefs.None && (!player.isPlayerControlled || player.isPlayerDead)) return;

        NetworkManager network = NetworkManager.Singleton;
        _startMessage.SendClients(message, LNetworkUtils.AllConnectedClientsExcept(sender, network.LocalClientId));
        if (network.IsHost && !player.IsOwner)
        {
            EmoteControl.OnRemoteStart(player, message.Emote);
        }
    }

    private static void OnClientReceived(StartData message)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            ReceiveOnClient(message);
        }
    }

    private static void ReceiveOnClient(StartData message)
    {
        if (GameNetworkManager.Instance.localPlayerController == null) return;
        if (!message.IsValid) return;

        PlayerControllerB? player = EmoteControl.GetPlayer(message.Player);
        if (player == null || player.IsOwner) return;

        EmoteControl.OnRemoteStart(player, message.Emote);
    }
}
