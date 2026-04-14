using System;
using MH.Network;

namespace MH.GameLogic
{
    /// <summary>
    /// Listen-server session inside Unity: authoritative <see cref="MatchSessionManager"/> + matchmaking for one LAN guest.
    /// </summary>
    public sealed class HostGameSession : IDisposable
    {
        private readonly UnityHostNetwork _net = new();
        private PacketDispatcher? _dispatcher;
        private MatchmakingHandler? _matchmaking;
        private MatchSessionManager? _sessions;
        private readonly BoardConfig _config;
        private readonly Action<int, Match, int> _onHostMatchStarted;
        private readonly Action<s2c_match_result> _onHostMatchEnded;
        private bool _disposed;

        public HostGameSession(
            BoardConfig config,
            Action<int, Match, int> onHostMatchStarted,
            Action<s2c_match_result> onHostMatchEnded)
        {
            _config = config;
            _onHostMatchStarted = onHostMatchStarted;
            _onHostMatchEnded = onHostMatchEnded;
        }

        /// <summary>Starts UDP listen + dispatcher; call <see cref="MatchmakingHandler.BeginHosting"/> after success.</summary>
        public bool TryListen()
        {
            if (!_net.TryStart(NetworkConstants.DefaultGamePort))
                return false;

            _dispatcher = new PacketDispatcher(_net);
            _sessions = new MatchSessionManager(_dispatcher, _net, _config);
            _matchmaking = new MatchmakingHandler(_dispatcher, _net);
            _matchmaking.OnMatchCreated += OnMatchCreated;
            _sessions.OnLocalHostMatchResult += _onHostMatchEnded;
            return true;
        }

        public void BeginWaitingForGuest()
        {
            _matchmaking?.BeginHosting();
        }

        public void CancelWaitingForGuest()
        {
            _matchmaking?.CancelHosting();
        }

        public void Poll()
        {
            if (!_disposed)
                _net.PollEvents();
        }

        public void TickSimulation(float deltaTime)
        {
            _sessions?.TickAndBroadcast(deltaTime);
        }

        public void ApplyHostInput(float x, float y)
        {
            _sessions?.ApplyHostBottomPaddleTarget(x, y);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_matchmaking != null)
            {
                _matchmaking.OnMatchCreated -= OnMatchCreated;
                _matchmaking.CancelHosting();
                _matchmaking.Dispose();
                _matchmaking = null;
            }

            if (_sessions != null)
            {
                _sessions.OnLocalHostMatchResult -= _onHostMatchEnded;
                _sessions.Dispose();
                _sessions = null;
            }

            _dispatcher?.Dispose();
            _dispatcher = null;

            _net.Dispose();
        }

        private void OnMatchCreated(int matchId, int peerBottom, int peerTop)
        {
            if (_sessions == null)
                return;

            _sessions.CreateMatch(matchId, peerBottom, peerTop);
            if (peerBottom == NetworkConstants.HostLocalPeerId && _sessions.TryGetMatch(matchId, out var match) && match != null)
                _onHostMatchStarted(matchId, match, 0);
        }
    }
}
