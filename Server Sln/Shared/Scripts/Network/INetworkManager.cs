using System;
using LiteNetLib;

namespace MH.Network
{
    /// <summary>
    /// Receive pipeline for <see cref="PacketDispatcher"/> plus server-style send/disconnect hooks.
    /// Clients may implement <see cref="SendPacket"/> with a single peer and raise <see cref="OnClientDisconnected"/> when the server connection drops.
    /// </summary>
    public interface INetworkManager
    {
        void RegisterReceivedEvent(Action<int, NetPacketReader> callback);
        void UnregisterHandler(Action<int, NetPacketReader> callback);

        void SendPacket<TPacket>(int peerId, TPacket packet) where TPacket : INetPacket;

        event Action<int>? OnClientDisconnected;
    }
}
