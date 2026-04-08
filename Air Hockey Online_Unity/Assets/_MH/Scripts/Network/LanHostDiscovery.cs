using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MH.Network
{
    /// <summary>
    /// LAN host discovery via LiteNetLib unconnected messages on the same UDP port as the game server (9050).
    /// Protocol must stay in sync with server <c>NetworkManager</c> unconnected handling.
    /// </summary>
    public sealed class LanHostDiscovery : IDisposable
    {
        public const int GamePort = 9050;
        public const string Query = "AHO_DISCOVER";
        private const string ResponsePrefix = "AHO_HOST";

        private readonly Dictionary<string, HostInfo> _hostsByKey = new();
        private EventBasedNetListener _listener;
        private NetManager _net;

        public event Action HostsChanged;

        public IReadOnlyCollection<HostInfo> Hosts => _hostsByKey.Values;

        public void Start()
        {
            if (_net != null)
                return;

            _listener = new EventBasedNetListener();
            _listener.NetworkReceiveUnconnectedEvent += OnUnconnectedReceived;

            _net = new NetManager(_listener)
            {
                UnconnectedMessagesEnabled = true
            };

            if (!_net.Start())
                Debug.LogError("LanHostDiscovery: NetManager.Start failed.");
        }

        /// <summary>Call from the Unity main thread (e.g. <c>Update</c>) so LiteNetLib processes incoming packets.</summary>
        public void Poll()
        {
            _net?.PollEvents();
        }

        public void Stop()
        {
            if (_listener != null)
            {
                _listener.NetworkReceiveUnconnectedEvent -= OnUnconnectedReceived;
                _listener = null;
            }

            if (_net != null)
            {
                _net.Stop();
                _net = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void Clear()
        {
            if (_hostsByKey.Count == 0)
                return;

            _hostsByKey.Clear();
            HostsChanged?.Invoke();
        }

        public async Task FindHostsAsync(float listenSeconds = 1.5f)
        {
            if (listenSeconds <= 0f)
                listenSeconds = 0.5f;

            if (_net == null)
                Start();

            Clear();
            BroadcastQuery();

            var ms = Mathf.Clamp((int)(listenSeconds * 1000f), 200, 5000);
            await Task.Delay(ms);
        }

        private void BroadcastQuery()
        {
            try
            {
                var writer = new NetDataWriter();
                writer.Put(Query);
                _net.SendUnconnectedMessage(writer, new IPEndPoint(IPAddress.Broadcast, GamePort));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LAN discovery broadcast failed: {e.Message}");
            }
        }

        private void OnUnconnectedReceived(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
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

                if (!TryParseResponse(msg, remoteEndPoint.Address, out var info))
                    return;

                var key = $"{info.Address}:{info.Port}";
                if (_hostsByKey.TryGetValue(key, out var existing))
                {
                    existing.LastSeenUtc = DateTime.UtcNow;
                    _hostsByKey[key] = existing;
                    return;
                }

                _hostsByKey[key] = info;
                HostsChanged?.Invoke();
            }
            finally
            {
                reader.Recycle();
            }
        }

        private static bool TryParseResponse(string msg, IPAddress remoteAddress, out HostInfo info)
        {
            info = default;

            var parts = msg.Split('|');
            if (parts.Length < 2)
                return false;

            if (!string.Equals(parts[0], ResponsePrefix, StringComparison.Ordinal))
                return false;

            if (!int.TryParse(parts[1], out var port))
                return false;

            var name = parts.Length >= 3 ? parts[2] : remoteAddress.ToString();
            info = new HostInfo
            {
                Name = name,
                Address = remoteAddress.ToString(),
                Port = port,
                LastSeenUtc = DateTime.UtcNow
            };
            return true;
        }

        public struct HostInfo
        {
            public string Name;
            public string Address;
            public int Port;
            public DateTime LastSeenUtc;
        }
    }
}
