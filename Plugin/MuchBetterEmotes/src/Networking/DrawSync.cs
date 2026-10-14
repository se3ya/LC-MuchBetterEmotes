using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using LethalNetworkAPI;
using LethalNetworkAPI.Utils;
using Unity.Netcode;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class DrawSync
{
    private static LNetworkMessage<StrokeData> _strokeMessage = null!;
    private static LNetworkMessage<int> _undoMessage = null!;
    private static LNetworkMessage<int> _clearMessage = null!;
    private static readonly Dictionary<int, RateBucket> _buckets = new();
    private static readonly Dictionary<int, RateBucket> _editBuckets = new();
    private static bool _resend = true;
    private static bool _reported;

    public static DrawWriter Pending { get; } = new();

    public static bool Hold { get; set; }

    public static void Initialize()
    {
        _strokeMessage = LNetworkMessage<StrokeData>.Connect(
            "MuchBetterEmotes_Stroke",
            onServerReceived: OnServerStroke,
            onClientReceived: OnClientStroke);
        _undoMessage = LNetworkMessage<int>.Connect(
            "MuchBetterEmotes_Undo",
            onServerReceived: OnServerUndo,
            onClientReceived: OnClientUndo);
        _clearMessage = LNetworkMessage<int>.Connect(
            "MuchBetterEmotes_Clear",
            onServerReceived: OnServerClear,
            onClientReceived: OnClientClear);
    }

    public static void Reset()
    {
        _resend = true;
        _reported = false;
        Hold = false;
        Pending.Start(-1);
    }

    public static void OnJoin()
    {
        _resend = true;
    }

    public static void Report(Exception ex)
    {
        if (_reported) return;

        _reported = true;
        Plugin.Log.LogError($"Failed to send to other players: {ex}");
    }

    public static void Flush()
    {
        try
        {
            if (!Hold) SendPending();
        }
        catch (Exception ex)
        {
            _resend = true;
            Report(ex);
        }
        finally
        {
            Pending.Start(EmoteControl.GetLocalIndex());
        }
    }

    public static void TrySend(PlayerControllerB local, int emote)
    {
        bool send = emote switch
        {
            EmoteDefs.Sign => !Hold && _resend,
            EmoteDefs.SignShown => Hold || _resend,
            _ => false
        };
        if (send) SendLog(local);
    }

    public static void SendLog(PlayerControllerB local)
    {
        int index = EmoteControl.GetIndex(local);
        if (index < 0) return;

        Pending.Start(index);
        Pending.AddLog(SignCanvases.Book.Get(index));
        _resend = false;
        Hold = false;
        Flush();
    }

    private static void SendPending()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (!network.IsConnectedClient) return;

        ulong local = network.LocalClientId;
        for (int i = 0; i < Pending.Count; i++)
        {
            switch (Pending.GetKind(i))
            {
                case DrawKind.Stroke:
                    Send(_strokeMessage, network, Pending.GetMessage(i), local);
                    break;
                case DrawKind.Undo:
                    Send(_undoMessage, network, Pending.Player, local);
                    break;
                default:
                    Send(_clearMessage, network, Pending.Player, local);
                    break;
            }
        }
    }

    private static void Send<T>(LNetworkMessage<T> message, NetworkManager network, T data, ulong sender)
    {
        if (network.IsServer)
        {
            message.SendClients(data, LNetworkUtils.AllConnectedClientsExcept(sender, network.LocalClientId));
        }
        else
        {
            message.SendServer(data);
        }
    }

    private static void OnServerStroke(StrokeData data, ulong sender)
    {
        if (!AcceptFrom(data.Player, sender)) return;

        if (SignCanvases.Book.TryApply(data, GetCount(), out int firstNew))
        {
            Send(_strokeMessage, NetworkManager.Singleton, data, sender);
            SignCanvases.OnApplied(data.Player, false, firstNew);
        }
    }

    private static void OnClientStroke(StrokeData data)
    {
        if (NetworkManager.Singleton.IsServer || !IsRemote(data.Player)) return;

        if (SignCanvases.Book.TryApply(data, GetCount(), out int firstNew))
        {
            SignCanvases.OnApplied(data.Player, false, firstNew);
        }
    }

    private static void OnServerUndo(int player, ulong sender) => OnServerEdit(_undoMessage, player, sender, true);

    private static void OnServerClear(int player, ulong sender) => OnServerEdit(_clearMessage, player, sender, false);

    private static void OnClientUndo(int player)
    {
        if (NetworkManager.Singleton.IsServer || !IsRemote(player)) return;

        if (Edit(player, true)) SignCanvases.OnApplied(player, true, 0);
    }

    private static void OnClientClear(int player)
    {
        if (NetworkManager.Singleton.IsServer || !IsRemote(player)) return;

        if (Edit(player, false)) SignCanvases.OnApplied(player, true, 0);
    }

    private static void OnServerEdit(LNetworkMessage<int> message, int player, ulong sender, bool undo)
    {
        if (!AcceptEdit(player, sender) || !Edit(player, undo)) return;

        Send(message, NetworkManager.Singleton, player, sender);
        SignCanvases.OnApplied(player, true, 0);
    }

    private static bool Edit(int player, bool undo)
    {
        DrawBook book = SignCanvases.Book;
        return undo ? book.TryUndo(player, GetCount()) : book.TryClear(player, GetCount());
    }

    private static bool AcceptFrom(int index, ulong sender)
    {
        PlayerControllerB? player = EmoteControl.GetPlayer(index);
        return player != null && player.OwnerClientId == sender && player.isPlayerControlled
            && RateBucket.TakeToken(_buckets, index, Time.realtimeSinceStartup, Constants.Sync.BucketSize, Constants.Sync.BucketRate);
    }

    private static bool AcceptEdit(int index, ulong sender)
    {
        return AcceptFrom(index, sender)
            && RateBucket.TakeToken(_editBuckets, index, Time.realtimeSinceStartup,
                Constants.Sync.EditBucketSize, Constants.Sync.EditBucketRate);
    }

    private static bool IsRemote(int index)
    {
        if (GameNetworkManager.Instance.localPlayerController == null) return false;

        PlayerControllerB? player = EmoteControl.GetPlayer(index);
        return player != null && !player.IsOwner;
    }

    private static int GetCount() => StartOfRound.Instance.allPlayerScripts.Length;
}
