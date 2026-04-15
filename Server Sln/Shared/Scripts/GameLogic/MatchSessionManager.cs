using System;
using System.Collections.Generic;
using MH.Core;
using MH.Network;

namespace MH.GameLogic
{
    /// <summary>
    /// Authoritative matches: peer ids may use <see cref="NetworkConstants.HostLocalPeerId"/> for the Unity host slot (no NetPeer).
    /// </summary>
    public sealed class MatchSessionManager : IPacketHandler, IDisposable
    {
        private readonly PacketDispatcher _dispatcher;
        private readonly INetworkManager _network;
        private readonly BoardConfig _config;

        private readonly Dictionary<int, RunningMatch> _matchesById = new();
        private readonly Dictionary<int, (int matchId, int playerId)> _peerToMatch = new();

        /// <summary>Raised when the host (listen-server) wins or should show match end UI locally.</summary>
        public event Action<s2c_match_result>? OnLocalHostMatchResult;

        public MatchSessionManager(PacketDispatcher dispatcher, INetworkManager network, BoardConfig config)
        {
            _dispatcher = dispatcher;
            _network = network;
            _config = config;

            dispatcher.RegisterHandler<c2s_mouse_pos>((int)EClientCmd.MousePos, this);
            _network.OnClientDisconnected += OnClientDisconnected;
        }

        public void Dispose()
        {
            _dispatcher.UnregisterHandler((int)EClientCmd.MousePos, this);
            _network.OnClientDisconnected -= OnClientDisconnected;
        }

        public void CreateMatch(int matchId, int peerBottom, int peerTop)
        {
            var match = new Match(playerId1: 0, playerId2: 1, _config);
            var running = new RunningMatch(matchId, peerBottom, peerTop, match);

            _matchesById[matchId] = running;
            if (peerBottom != NetworkConstants.HostLocalPeerId)
                _peerToMatch[peerBottom] = (matchId, playerId: 0);
            if (peerTop != NetworkConstants.HostLocalPeerId)
                _peerToMatch[peerTop] = (matchId, playerId: 1);
        }

        public bool TryGetMatch(int matchId, out Match? match)
        {
            if (_matchesById.TryGetValue(matchId, out var running))
            {
                match = running.Match;
                return true;
            }

            match = null;
            return false;
        }

        /// <summary>Listen-server: apply host (bottom player) input without a network packet.</summary>
        public void ApplyHostBottomPaddleTarget(float x, float y)
        {
            foreach (var running in _matchesById.Values)
            {
                if (running.PeerBottom != NetworkConstants.HostLocalPeerId)
                    continue;

                ApplyMouseToPlayer(running.Match, playerId: 0, x, y);
                return;
            }
        }

        public void HandlePacket(int fromId, int packType, INetPacket packet)
        {
            if (packType != (int)EClientCmd.MousePos)
                return;

            if (!_peerToMatch.TryGetValue(fromId, out var link))
                return;

            if (!_matchesById.TryGetValue(link.matchId, out var running))
                return;

            var mouse = (c2s_mouse_pos)packet;
            ApplyMouseToPlayer(running.Match, link.playerId, mouse.X, mouse.Y);
        }

        private void ApplyMouseToPlayer(Match match, int playerId, float x, float y)
        {
            match.ApplyPaddleTargetFromWorld(playerId, new CustomVector2(x, y));
        }

        public void TickAndBroadcast(float deltaTime)
        {
            foreach (var running in _matchesById.Values)
            {
                running.Match.Tick(deltaTime);

                var puckRoot = running.Match.Puck.GetComponent<Root2D>();
                var puckMove = running.Match.Puck.GetComponent<MoveComponent>();
                var p0 = running.Match.GetPlayer(0);
                var p1 = running.Match.GetPlayer(1);
                if (p0 == null || p1 == null)
                    continue;

                var status = new s2c_board_status
                {
                    MatchId = running.MatchId,

                    PuckX = puckRoot.Position.x,
                    PuckY = puckRoot.Position.y,
                    PuckVelX = puckMove.CurrentVelocity.x,
                    PuckVelY = puckMove.CurrentVelocity.y,

                    Paddle0X = p0.Paddle.GetComponent<Root2D>().Position.x,
                    Paddle0Y = p0.Paddle.GetComponent<Root2D>().Position.y,
                    Paddle1X = p1.Paddle.GetComponent<Root2D>().Position.x,
                    Paddle1Y = p1.Paddle.GetComponent<Root2D>().Position.y,
                };

                SendBoardStatus(running.PeerBottom, status);
                SendBoardStatus(running.PeerTop, status);
            }
        }

        private void SendBoardStatus(int peerId, s2c_board_status status)
        {
            if (peerId == NetworkConstants.HostLocalPeerId)
                return;

            _network.SendPacket(peerId, status);
        }

        private void OnClientDisconnected(int peerId)
        {
            if (!_peerToMatch.TryGetValue(peerId, out var link))
                return;

            if (!_matchesById.TryGetValue(link.matchId, out var running))
            {
                _peerToMatch.Remove(peerId);
                return;
            }

            var winnerPeer = running.PeerBottom == peerId ? running.PeerTop : running.PeerBottom;
            var winnerPlayerIndex = running.PeerBottom == peerId ? 1 : 0;

            var result = new s2c_match_result
            {
                MatchId = running.MatchId,
                WinnerPlayerIndex = winnerPlayerIndex
            };

            DeliverMatchResult(winnerPeer, result);

            RemovePeerMapping(running.PeerBottom);
            RemovePeerMapping(running.PeerTop);
            _matchesById.Remove(link.matchId);
        }

        private void RemovePeerMapping(int peerId)
        {
            if (peerId != NetworkConstants.HostLocalPeerId)
                _peerToMatch.Remove(peerId);
        }

        private void DeliverMatchResult(int winnerPeer, s2c_match_result result)
        {
            if (winnerPeer == NetworkConstants.HostLocalPeerId)
                OnLocalHostMatchResult?.Invoke(result);
            else
                _network.SendPacket(winnerPeer, result);
        }

        /// <summary>Holds one in-flight match and the LiteNetLib peer ids for each side (or <see cref="NetworkConstants.HostLocalPeerId"/> for the local host).</summary>
        private sealed class RunningMatch
        {
            public RunningMatch(int matchId, int peerBottom, int peerTop, Match match)
            {
                MatchId = matchId;
                PeerBottom = peerBottom;
                PeerTop = peerTop;
                Match = match;
            }

            public int MatchId { get; }
            public int PeerBottom { get; }
            public int PeerTop { get; }
            public Match Match { get; }
        }
    }
}
