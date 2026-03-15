using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;

namespace MH.Network
{
    public interface INetPacket : INetSerializable
    {
        
    }

    public interface IPacketHandler
    {
        void HandlePacket(int fromId,int packType, INetPacket packet);
    }   

    public class PacketDispatcher : IDisposable
    {
        // Dictionary lưu trữ cách tạo ra một instance mới của struct dựa trên ID
        private readonly Dictionary<int, Func<NetPacketReader, INetPacket>> _createPackMethodMap = new();
        private Dictionary<int, List<IPacketHandler>> _handlers = new Dictionary<int, List<IPacketHandler>>();
        
        private INetworkManager _networkManager;



        public PacketDispatcher(INetworkManager networkManager) 
        {
        
            _networkManager = networkManager;

            _networkManager.RegisterReceivedEvent(HandleReceived);
        }

        public void Dispose()
        {
            _networkManager.UnregisterHandler(HandleReceived);   

            _createPackMethodMap.Clear();
            foreach (var handlers in _handlers.Values)
            {
                    handlers.Clear();
            }
            _handlers.Clear();
        }

        #region PUBLIC API

        //public void RegisterPacket<T>(int id) where T : INetPacket, new()
        //{
        //    _createPackMethodMap[id] = (reader) =>
        //    {
        //        T packet = new T();
        //        packet.Deserialize(reader);
        //        return packet;
        //    };
        //}

        public void RegisterHandler<TPacket>(int cmdType, IPacketHandler handler) where TPacket : INetPacket, new()
        {
            // Đảm bảo rằng đã có danh sách handlers cho cmdType này
            if (!_handlers.ContainsKey(cmdType))
            {
                _handlers[cmdType] = new List<IPacketHandler>();
            }
            _handlers[cmdType].Add(handler);

            // Đăng ký cách tạo packet nếu chưa có
            _createPackMethodMap[cmdType] = (reader) =>
            {
                TPacket packet = new TPacket();
                packet.Deserialize(reader);
                return packet;
            };
        }


        public void UnregisterHandler(int cmdType, IPacketHandler handler)
        {
            if (_handlers.TryGetValue(cmdType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlers.Remove(cmdType);
                }
            }
        }

        #endregion

        private void HandleReceived(int fromId, NetPacketReader reader)
        {
            try
            {
                int cmdType = reader.GetInt();

                if (_createPackMethodMap.TryGetValue(cmdType, out var createPacketMethod))
                {
                    var packet = createPacketMethod(reader);
                    DispatchPacket(fromId, cmdType, packet);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling received packet: {ex}");
                return;
            }   

            
        }

        void DispatchPacket(int fromId, int cmdType, INetPacket packet)
        {
            if (_handlers.TryGetValue(cmdType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.HandlePacket(fromId, cmdType, packet);
                }
            }
        }


    }
}
