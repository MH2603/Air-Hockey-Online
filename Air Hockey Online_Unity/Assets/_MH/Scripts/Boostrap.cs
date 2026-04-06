using MH.Network;
using MH.UI;
using UnityEngine;

namespace MH.GameLogic
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameRunner _gameRunner;
        [SerializeField] private UIManager _uiManager;
        
        private ClientNetwork _clientNetwork;

        void Awake()
        {
            _clientNetwork = new ClientNetwork();
            _clientNetwork.Init();

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
