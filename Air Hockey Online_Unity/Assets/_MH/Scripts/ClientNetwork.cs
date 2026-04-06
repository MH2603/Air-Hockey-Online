using System;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MH.Network
{
    public class ClientNetwork : INetworkManager, IDisposable
    {
        private NetManager _client;
        private EventBasedNetListener _listener;
        private NetDataWriter _writer = new();
        private PacketDispatcher _dispatcher;
        private bool _disposed;
        private bool _clientStarted;
        private event Action<int, NetPacketReader> _received;

        public PacketDispatcher Dispatcher => _dispatcher;

        public event Action OnConnected;
        public event Action OnDisconnected;

        public void Init()
        {
            _listener = new EventBasedNetListener();
            _client = new NetManager(_listener);
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _dispatcher = new PacketDispatcher(this);
        }

        public void RegisterReceivedEvent(Action<int, NetPacketReader> callback)
        {
            _received += callback;
        }

        public void UnregisterHandler(Action<int, NetPacketReader> callback)
        {
            _received -= callback;
        }

        public void StartConnect()
        {
            if (_disposed)
                return;

            _listener.PeerConnectedEvent -= OnConnectedHandler;
            _listener.PeerConnectedEvent += OnConnectedHandler;

            if (!_clientStarted)
            {
                _client.Start();
                _clientStarted = true;
            }

            _client.Connect("localhost", 9050, "SomeConnectionKey");
            Debug.Log("Connecting to server...");
        }

        public void PollEvents()
        {
            _client?.PollEvents();
        }

        public void Send<TPacket>(TPacket packet) where TPacket : INetPacket
        {
            if (_client == null || _client.FirstPeer == null || _client.FirstPeer.ConnectionState != ConnectionState.Connected)
            {
                Debug.LogWarning("Cannot send packet: not connected to server.");
                return;
            }

            _writer.Reset();
            packet.Serialize(_writer);
            _client.FirstPeer.Send(_writer, DeliveryMethod.ReliableOrdered);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _listener.NetworkReceiveEvent -= OnNetworkReceive;
            _listener.PeerDisconnectedEvent -= OnPeerDisconnected;
            _listener.PeerConnectedEvent -= OnConnectedHandler;

            _dispatcher?.Dispose();

            if (_client != null)
            {
                _client.Stop();
                _client = null;
            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliverymethod)
        {
            try
            {
                _received?.Invoke(peer.Id, reader);
            }
            finally
            {
                reader.Recycle();
            }
        }

        private void OnConnectedHandler(NetPeer peer)
        {
            OnConnected?.Invoke();
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            Debug.Log($"Disconnected from server: {info.Reason}");
            OnDisconnected?.Invoke();
        }
    }
}
