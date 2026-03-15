using LiteNetLib.Utils;

namespace MH.Network
{
    public class SessionManager : IDisposable
    {
        private NetworkManager _networkManager;
        private Dictionary<int, Session> _sessions = new Dictionary<int, Session>();

        public SessionManager(NetworkManager networkManager) 
        {
            _networkManager = networkManager;
            _networkManager.OnClientConnected += HandleConnected;
 
        }

        private void HandleReceived(int fromId, int cmdType, NetDataReader reader)
        {
            
        }

        public void Dispose()
        {
            
        }

        void HandleConnected(int peerId)
        {

        }
    }
}
