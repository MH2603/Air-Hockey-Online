using MH.Network;

namespace MH.GameLogic
{
    public class MatchmakingHandler : IPacketHandler, IDisposable
    {
        private readonly PacketDispatcher _dispatcher;
        private readonly NetworkManager _network;
        private readonly List<int> _waiting = new();
        private int _nextMatchId = 1;

        public MatchmakingHandler(PacketDispatcher dispatcher, NetworkManager network)
        {
            _dispatcher = dispatcher;
            _network = network;
            dispatcher.RegisterHandler<c2s_find_match>((int)EClientCmd.FindMatch, this);
            network.OnClientDisconnected += OnClientDisconnected;
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
                if (_waiting.Count > 0)
                {
                    var other = _waiting[0];
                    _waiting.RemoveAt(0);
                    var matchId = _nextMatchId++;
                    _network.SendPacket(other, new s2c_match_found { MatchId = matchId, LocalPlayerIndex = 0 });
                    _network.SendPacket(fromId, new s2c_match_found { MatchId = matchId, LocalPlayerIndex = 1 });
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
