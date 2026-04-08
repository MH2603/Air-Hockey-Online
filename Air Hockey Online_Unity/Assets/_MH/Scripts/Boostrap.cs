using MH.Network;
using MH.UI;
using UnityEngine;

namespace MH.GameLogic
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameRunner _gameRunner;
        [SerializeField] private UIManager _uiManager;
        [Tooltip("Editor/PC: localhost. Physical Android on Wi‑Fi: your dev machine's LAN IPv4. Android Emulator: 10.0.2.2")]
        [SerializeField] private string _serverHost = "localhost";
        [SerializeField] private int _serverPort = 9050;

        private ClientNetwork _clientNetwork;

        void Awake()
        {
            _clientNetwork = new ClientNetwork();
            _clientNetwork.Init();
            _clientNetwork.SetConnectionTarget(_serverHost, _serverPort);

            var gameRunner = Instantiate(_gameRunner);
            gameRunner.Init(_clientNetwork);

            Instantiate(_uiManager);
        }

        void Update()
        {
            _clientNetwork?.PollEvents();
        }

        void OnDestroy()
        {
            _clientNetwork?.Dispose();
        }

        void OnApplicationQuit()
        {
            _clientNetwork?.Dispose();
        }
    }
}
