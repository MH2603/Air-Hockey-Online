using LiteNetLib;
using LiteNetLib.Utils;

namespace MH.Network
{
    public class NetworkManager : INetworkManager
    {
        private CancellationTokenSource _cts;
        private NetManager _server;
        private EventBasedNetListener _listener;
        private Dictionary<int, NetPeer> _connectedPeers = new Dictionary<int, NetPeer>();

        public Action<int> OnClientConnected;
        public Action<int> OnClientDisconnected;
        public Action<int, NetPacketReader> OnReceived;

        public void Init()
        {
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener);
            _server.Start(9050 /* port */);

            _cts = new CancellationTokenSource();

            _listener.ConnectionRequestEvent += HandleConnectionRequest;
            _listener.PeerConnectedEvent += HandlePeerConnected;
            _listener.PeerDisconnectedEvent += HandlePeerDisconnected;
            _listener.NetworkReceiveEvent += HandleReceived;

            Task.Run(PollEvents, _cts.Token);
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

        #endregion

    }
}
