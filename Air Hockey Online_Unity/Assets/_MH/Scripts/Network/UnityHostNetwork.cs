using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MH.Network
{
    /// <summary>
    /// LiteNetLib listen-server for Unity main thread: game port + LAN discovery (same behavior as headless <c>NetworkManager</c>).
    /// </summary>
    public sealed class UnityHostNetwork : INetworkManager, IDisposable
    {
        private const string LanDiscoveryQuery = "AHO_DISCOVER";
        private const string LanDiscoveryResponsePrefix = "AHO_HOST";

        private readonly Dictionary<int, NetPeer> _connectedPeers = new();
        private EventBasedNetListener _listener = null!;
        private NetManager _server = null!;
        private bool _started;
        private bool _disposed;
        private int _listenPort = NetworkConstants.DefaultGamePort;

        private event Action<int, NetPacketReader>? _received;

        public event Action<int>? OnClientConnected;
        public event Action<int>? OnClientDisconnected;

        /// <summary>True if UDP listen succeeded.</summary>
        public bool TryStart(int port = NetworkConstants.DefaultGamePort)
        {
            if (_disposed)
                return false;

            _listenPort = port;
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener)
            {
                UnconnectedMessagesEnabled = true
            };

            if (!_server.Start(port))
            {
                Debug.LogError($"UnityHostNetwork: failed to bind UDP port {port}.");
                _server = null!;
                _listener = null!;
                return false;
            }

            _listener.ConnectionRequestEvent += HandleConnectionRequest;
            _listener.PeerConnectedEvent += HandlePeerConnected;
            _listener.PeerDisconnectedEvent += HandlePeerDisconnected;
            _listener.NetworkReceiveEvent += HandleReceived;
            _listener.NetworkReceiveUnconnectedEvent += HandleLanDiscoveryUnconnected;

            _started = true;
            Debug.Log($"UnityHostNetwork: listening on UDP {port}.");
            return true;
        }

        public void PollEvents()
        {
            if (_started && !_disposed)
                _server?.PollEvents();
        }

        public void RegisterReceivedEvent(Action<int, NetPacketReader> callback)
        {
            _received += callback;
        }

        public void UnregisterHandler(Action<int, NetPacketReader> callback)
        {
            _received -= callback;
        }

        public void SendPacket<TPacket>(int peerId, TPacket packet) where TPacket : INetPacket
        {
            if (peerId == NetworkConstants.HostLocalPeerId)
                return;

            if (!_connectedPeers.TryGetValue(peerId, out var peer))
                return;

            var writer = new NetDataWriter();
            packet.Serialize(writer);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_listener != null)
            {
                _listener.ConnectionRequestEvent -= HandleConnectionRequest;
                _listener.PeerConnectedEvent -= HandlePeerConnected;
                _listener.PeerDisconnectedEvent -= HandlePeerDisconnected;
                _listener.NetworkReceiveEvent -= HandleReceived;
                _listener.NetworkReceiveUnconnectedEvent -= HandleLanDiscoveryUnconnected;
            }

            _connectedPeers.Clear();
            _server?.Stop();
            _started = false;
        }

        private void HandleConnectionRequest(ConnectionRequest request)
        {
            if (_server != null && _server.ConnectedPeersCount < 10)
                request.AcceptIfKey(NetworkConstants.ConnectionKey);
            else
                request.Reject();
        }

        private void HandlePeerConnected(NetPeer peer)
        {
            _connectedPeers[peer.Id] = peer;
            OnClientConnected?.Invoke(peer.Id);
        }

        private void HandlePeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            _connectedPeers.Remove(peer.Id);
            OnClientDisconnected?.Invoke(peer.Id);
        }

        private void HandleReceived(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
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

        private void HandleLanDiscoveryUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            try
            {
                if (reader.AvailableBytes <= 0)
                    return;

                string msg;
                try
                {
                    msg = reader.GetString();
                }
                catch
                {
                    return;
                }

                if (!string.Equals(msg, LanDiscoveryQuery, StringComparison.Ordinal))
                    return;

                var response = $"{LanDiscoveryResponsePrefix}|{_listenPort}|{Environment.MachineName}";
                var writer = new NetDataWriter();
                writer.Put(response);
                _server?.SendUnconnectedMessage(writer, remoteEndPoint);
            }
            finally
            {
                reader.Recycle();
            }
        }
    }
}
