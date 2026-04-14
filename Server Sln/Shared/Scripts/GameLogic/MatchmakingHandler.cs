using System;
using System.Collections.Generic;
using MH.Network;

namespace MH.GameLogic
{
    public class MatchmakingHandler : IPacketHandler, IDisposable
    {
        private readonly PacketDispatcher _dispatcher;
        private readonly INetworkManager _network;
        private readonly List<int> _waiting = new();
        private int _nextMatchId = 1;

        /// <summary>Host is always bottom (player 0): (matchId, HostLocalPeerId, remotePeerTop).</summary>
        public event Action<int, int, int>? OnMatchCreated;

        private bool _hostAwaitingGuest;

        public MatchmakingHandler(PacketDispatcher dispatcher, INetworkManager network)
        {
            _dispatcher = dispatcher;
            _network = network;
            dispatcher.RegisterHandler<c2s_find_match>((int)EClientCmd.FindMatch, this);
            _network.OnClientDisconnected += OnClientDisconnected;
        }

        /// <summary>Listen-server mode: first remote <see cref="EClientCmd.FindMatch"/> pairs with the local host (bottom player).</summary>
        public void BeginHosting()
        {
            lock (_waiting)
            {
                _hostAwaitingGuest = true;
            }
        }

        public void CancelHosting()
        {
            lock (_waiting)
            {
                _hostAwaitingGuest = false;
            }
        }

        public void Dispose()
        {
            _network.OnClientDisconnected -= OnClientDisconnected;
            _dispatcher.UnregisterHandler((int)EClientCmd.FindMatch, this);
        }

        public void HandlePacket(int fromId, int packType, INetPacket packet)
        {
            if (packType != (int)EClientCmd.FindMatch)
                return;

            lock (_waiting)
            {
                if (_hostAwaitingGuest)
                {
                    _hostAwaitingGuest = false;
                    var matchId = _nextMatchId++;
                    _network.SendPacket(fromId, new s2c_match_found { MatchId = matchId, LocalPlayerIndex = 1 });
                    OnMatchCreated?.Invoke(matchId, NetworkConstants.HostLocalPeerId, fromId);
                    return;
                }

                if (_waiting.Count > 0)
                {
                    var other = _waiting[0];
                    _waiting.RemoveAt(0);
                    var matchId = _nextMatchId++;
                    _network.SendPacket(other, new s2c_match_found { MatchId = matchId, LocalPlayerIndex = 0 });
                    _network.SendPacket(fromId, new s2c_match_found { MatchId = matchId, LocalPlayerIndex = 1 });

                    OnMatchCreated?.Invoke(matchId, other, fromId);
                }
                else
                {
                    _waiting.Add(fromId);
                }
            }
        }

        private void OnClientDisconnected(int peerId)
        {
            lock (_waiting)
            {
                _waiting.Remove(peerId);
            }
        }
    }
}
