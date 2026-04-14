using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;

namespace MH.Network
{
    public class NetworkManager : INetworkManager
    {
        private const int ListenPort = 9050;
        private const string LanDiscoveryQuery = "AHO_DISCOVER";
        private const string LanDiscoveryResponsePrefix = "AHO_HOST";

        private CancellationTokenSource _cts;
        private NetManager _server;
        private EventBasedNetListener _listener;
        private Dictionary<int, NetPeer> _connectedPeers = new Dictionary<int, NetPeer>();

        public event Action<int>? OnClientConnected;
        public event Action<int>? OnClientDisconnected;
        public Action<int, NetPacketReader>? OnReceived;

        public void Init()
        {
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener)
            {
                UnconnectedMessagesEnabled = true
            };
            _server.Start(ListenPort);

            _cts = new CancellationTokenSource();

            _listener.ConnectionRequestEvent += HandleConnectionRequest;
            _listener.PeerConnectedEvent += HandlePeerConnected;
            _listener.PeerDisconnectedEvent += HandlePeerDisconnected;
            _listener.NetworkReceiveEvent += HandleReceived;
            _listener.NetworkReceiveUnconnectedEvent += HandleLanDiscoveryUnconnected;
        }

        /// <summary>
        /// Must be called regularly - LiteNetLib requires polling to process connections and messages.
        /// </summary>
        void PollEvents()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                _server?.PollEvents();
                Thread.Sleep(15); // ~66 updates/sec
            }
        }
        
        public void Tick()
        {
            if(!_cts.Token.IsCancellationRequested)
                _server?.PollEvents();
        }

        public void RegisterReceivedEvent(Action<int, NetPacketReader> callback)
        {
            OnReceived += callback;
        }

        public void UnregisterHandler(Action<int, NetPacketReader> callback)
        {
            OnReceived -= callback;
        }


        public void Stop()
        {
            _cts.Cancel();

            _listener.ConnectionRequestEvent -= HandleConnectionRequest;
            _listener.PeerConnectedEvent -= HandlePeerConnected;
            _listener.PeerDisconnectedEvent -= HandlePeerDisconnected;
            _listener.NetworkReceiveEvent -= HandleReceived;
            _listener.NetworkReceiveUnconnectedEvent -= HandleLanDiscoveryUnconnected;

            _server.Stop();
        }

        public void SendPacket<TPacket>(int peerId, TPacket packet) where TPacket : INetPacket
        {
            if (!_connectedPeers.TryGetValue(peerId, out var peer))
                return;

            var writer = new NetDataWriter();
            packet.Serialize(writer);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }



        #region Callback methods

        void HandleConnectionRequest(ConnectionRequest request)
        {
            // Handle incoming connection request
            if (_server.ConnectedPeersCount < 10 /* max connections */)
            {
                request.AcceptIfKey("SomeConnectionKey");
                Console.WriteLine("Connection request accepted.");
            }
            else
                request.Reject();
        }

        void HandlePeerConnected(NetPeer peer)
        {
            Console.WriteLine("We got connection: {0}", peer);

            _connectedPeers[peer.Id] = peer;
            OnClientConnected?.Invoke(peer.Id);
        }   

        void HandlePeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            Console.WriteLine($"Peer {peer} disconnected: {info.Reason}");
            _connectedPeers.Remove(peer.Id);
            OnClientDisconnected?.Invoke(peer.Id);
        }   

        void HandleReceived(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            OnReceived?.Invoke(peer.Id, reader);  
            reader.Recycle(); // Recycle the reader after processing
        }

        void HandleLanDiscoveryUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
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

                var response = $"{LanDiscoveryResponsePrefix}|{ListenPort}|{Environment.MachineName}";
                var writer = new NetDataWriter();
                writer.Put(response);
                _server.SendUnconnectedMessage(writer, remoteEndPoint);
            }
            finally
            {
                reader.Recycle();
            }
        }

        #endregion

    }
}
