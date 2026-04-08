using MH.Core;
using MH.Network;

namespace MH.GameLogic
{
    /// <summary>
    /// Owns authoritative matches on the server:
    /// - maps peers -> match + playerId
    /// - applies MousePos to paddle velocity
    /// - ticks Match and sends BoardStatus to both peers
    /// </summary>
    public sealed class MatchSessionManager : IPacketHandler, IDisposable
    {
        private readonly PacketDispatcher _dispatcher;
        private readonly NetworkManager _network;
        private readonly BoardConfig _config;

        private readonly Dictionary<int, RunningMatch> _matchesById = new();
        private readonly Dictionary<int, (int matchId, int playerId)> _peerToMatch = new();

        public MatchSessionManager(PacketDispatcher dispatcher, NetworkManager network, BoardConfig config)
        {
            _dispatcher = dispatcher;
            _network = network;
            _config = config;

            dispatcher.RegisterHandler<c2s_mouse_pos>((int)EClientCmd.MousePos, this);
            network.OnClientDisconnected += OnClientDisconnected;
        }

        public void Dispose()
        {
            _dispatcher.UnregisterHandler((int)EClientCmd.MousePos, this);
            _network.OnClientDisconnected -= OnClientDisconnected;
        }

        // Match creation (called by MatchmakingHandler)
        public void CreateMatch(int matchId, int peerBottom, int peerTop)
        {
            // Initialization
            var match = new Match(playerId1: 0, playerId2: 1, _config);
            var running = new RunningMatch(matchId, peerBottom, peerTop, match);

            _matchesById[matchId] = running;
            _peerToMatch[peerBottom] = (matchId, playerId: 0);
            _peerToMatch[peerTop] = (matchId, playerId: 1);
        }

        // Packet receive path (MousePos)
        public void HandlePacket(int fromId, int packType, INetPacket packet)
        {
            if (packType != (int)EClientCmd.MousePos)
                return;

            if (!_peerToMatch.TryGetValue(fromId, out var link))
                return;

            if (!_matchesById.TryGetValue(link.matchId, out var running))
                return;

            var mouse = (c2s_mouse_pos)packet;
            var player = running.Match.GetPlayer(link.playerId);
            if (player == null)
                return;

            // Main logic: convert target position -> velocity (same idea as client used locally).
            var target = new CustomVector2(mouse.X, mouse.Y);
            var paddlePos = player.Paddle.GetComponent<Root2D>().Position;
            var vel = (target - paddlePos) * _config.PaddlePositionFollow;
            running.Match.SetPaddleVelocity(link.playerId, vel);
        }

        // Authoritative tick + broadcast
        public void TickAndBroadcast(float deltaTime)
        {
            foreach (var running in _matchesById.Values)
            {
                // Simulation
                running.Match.Tick(deltaTime);

                // Snapshot
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

                // Broadcast
                _network.SendPacket(running.PeerBottom, status);
                _network.SendPacket(running.PeerTop, status);
            }
        }

        private void OnClientDisconnected(int peerId)
        {
            // Cleanup any match containing this peer; if it was running, remaining peer wins.
            if (!_peerToMatch.TryGetValue(peerId, out var link))
                return;

            if (_matchesById.TryGetValue(link.matchId, out var running))
            {
                // Determine winner (the other peer still connected).
                var winnerPeer = running.PeerBottom == peerId ? running.PeerTop : running.PeerBottom;
                var winnerPlayerIndex = running.PeerBottom == peerId ? 1 : 0;

                _network.SendPacket(winnerPeer, new s2c_match_result
                {
                    MatchId = running.MatchId,
                    WinnerPlayerIndex = winnerPlayerIndex
                });

                // Cleanup mappings + match.
                _peerToMatch.Remove(running.PeerBottom);
                _peerToMatch.Remove(running.PeerTop);
                _matchesById.Remove(link.matchId);
            }
            else
            {
                _peerToMatch.Remove(peerId);
            }
        }

        private readonly record struct RunningMatch(int MatchId, int PeerBottom, int PeerTop, Match Match);
    }
}

