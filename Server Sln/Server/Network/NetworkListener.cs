using LiteNetLib;
using LiteNetLib.Utils;

namespace Server.Network
{
    public class NetworkListener
    {
        private CancellationTokenSource _cts;
        private NetManager _server;
        private EventBasedNetListener _listener;

        public void Init()
        {
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener);
            _server.Start(9050 /* port */);

            _cts = new CancellationTokenSource();

            _listener.ConnectionRequestEvent += HandleConnectionRequest;
            _listener.PeerConnectedEvent += HandlePeerConnected;
        }

        /// <summary>
        /// Must be called regularly - LiteNetLib requires polling to process connections and messages.
        /// </summary>
        public void PollEvents()
        {
            _server?.PollEvents();
        }

        public void Stop()
        {
            _cts.Cancel();

            _listener.ConnectionRequestEvent -= HandleConnectionRequest;
            _listener.PeerConnectedEvent -= HandlePeerConnected;

            _server.Stop();
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
            Console.WriteLine("We got connection: {0}", peer);  // Show peer IP
            var writer = new NetDataWriter();         // Create writer class
            writer.Put("Hello client!");                        // Put some string
            peer.Send(writer, DeliveryMethod.ReliableOrdered);  // Send with reliability
        }   

        #endregion

    }
}
