using LiteNetLib;
using LiteNetLib.Utils;
using MH.Network;
using UnityEngine;

namespace MH.Scripts
{
    public class NetworkClient 
    {
        private NetManager _client;
        private EventBasedNetListener _listener;

        private NetDataWriter _writer = new ();

        public void Init()
        {
            _listener = new EventBasedNetListener();
            _client = new NetManager(_listener);
            _client.Start();

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
            {
                Debug.Log($"We got: {dataReader.GetString(100 /* max length of string */)}");
                dataReader.Recycle();
            };

            _listener.PeerConnectedEvent += (peer) =>
            {
                Debug.Log("Connected to server!");
            };

            // _listener.ConnectionFailedEvent += (peer, info) =>
            // {
            //     Debug.LogWarning($"Connection failed: {info}");
            // };

            _client.Connect("localhost", 9050, "SomeConnectionKey");
            Debug.Log("Connecting to server...");
        }

        /// <summary>
        /// Must be called every frame (e.g. from Update) - LiteNetLib requires polling to process events.
        /// </summary>
        public void PollEvents()
        {
            _client?.PollEvents();
        }

        public void Send<TPacket>(TPacket packet)  where TPacket : INetPacket
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
    }
}