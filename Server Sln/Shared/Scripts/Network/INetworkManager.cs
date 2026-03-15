
using LiteNetLib;
using System;

namespace MH.Network
{
    public interface INetworkManager 
    {
        public void RegisterReceivedEvent(Action<int,NetPacketReader> callback);
        public void UnregisterHandler(Action<int, NetPacketReader> callback);
    }
}
