using MH;
using MH.Common;
using MH.Core;
using MH.Network;
using MH.UI;
using MH.Views;
using NaughtyAttributes;
using UnityEngine;

namespace MH.GameLogic
{
    public class GameRunner : MonoSingleton<GameRunner>, IPacketHandler
    {
        [SerializeField] private BoardConfig _config;

        private ClientNetwork _clientNetwork;
        private Match _currentMatch;
        private bool _isMouseDown;
        private EGameState _gameState;
        private int _localPlayerIndex;

        public Match CurrentMatch => _currentMatch;
        public EGameState GameState => _gameState;

        void Update()
        {
            if (_gameState != EGameState.Playing || _currentMatch == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _isMouseDown = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isMouseDown = false;
            }

            if (_isMouseDown)
            {
                Vector3 mousePos = MouseUtils.GetMouseWorldPosition();
                CustomVector2 pos = new CustomVector2(mousePos.x, mousePos.y);
                var player = _currentMatch.GetPlayer(_localPlayerIndex);
                if (player != null)
                {
                    var paddlePos = player.Paddle.GetComponent<Root2D>().Position;
                    var vel = (pos - paddlePos) * _config.PaddlePositionFollow;
                    _currentMatch.SetPaddleVelocity(_localPlayerIndex, vel);
                }
            }
            else
            {
                _currentMatch.SetPaddleVelocity(_localPlayerIndex, CustomVector2.Zero);
            }

            _currentMatch.Tick(Time.deltaTime);
        }

        public void Init(ClientNetwork clientNetwork)
        {
            _clientNetwork = clientNetwork;
            _clientNetwork.Dispatcher.RegisterHandler<s2c_match_found>((int)EServerCmd.MatchFound, this);
            _clientNetwork.OnConnected += OnServerConnected;
            _clientNetwork.OnDisconnected += OnServerDisconnected;

            _gameState = EGameState.MainMenu;
            Application.targetFrameRate = 60;
        }

        public void RequestMatchmaking()
        {
            if (_gameState == EGameState.Connecting || _gameState == EGameState.Matching)
                return;

            _gameState = EGameState.Connecting;
            var ui = UIManager.Instance;
            if (ui != null)
            {
                ui.Hide<UIMainMenu>();
                if (ui.TryGet<UILoading>(out _))
                    ui.Show<UILoading>();
                else
                    Debug.LogWarning("UILoading is not registered on UIManager; add a UILoading prefab to view prefabs.");
            }

            _clientNetwork?.StartConnect();
        }

        public void HandlePacket(int fromId, int packType, INetPacket packet)
        {
            if (packType != (int)EServerCmd.MatchFound)
                return;

            if (_gameState == EGameState.Playing && _currentMatch != null)
            {
                Debug.LogWarning("MatchFound ignored: already playing.");
                return;
            }

            var found = (s2c_match_found)packet;
            BeginLocalMatch(found.MatchId, found.LocalPlayerIndex);
        }

        private void OnServerConnected()
        {
            if (_gameState != EGameState.Connecting)
                return;

            _gameState = EGameState.Matching;
            _clientNetwork.Send(new c2s_find_match());
        }

        private void OnServerDisconnected()
        {
            if (_gameState == EGameState.Playing)
                return;

            _gameState = EGameState.MainMenu;
            var ui = UIManager.Instance;
            if (ui != null)
            {
                ui.Hide<UILoading>();
                if (ui.TryGet<UIMainMenu>(out _))
                    ui.Show<UIMainMenu>();
            }
        }

        private void BeginLocalMatch(int matchId, int localPlayerIndex)
        {
            Debug.Log($"Match started: id={matchId}, localPlayer={localPlayerIndex}");

            _currentMatch = new Match(0, 1, _config);
            var matchView = GetComponent<MatchView2D>() ?? gameObject.AddComponent<MatchView2D>();
            matchView.SetMatch(_currentMatch);
            _localPlayerIndex = localPlayerIndex;
            _gameState = EGameState.Playing;

            var ui = UIManager.Instance;
            ui?.Hide<UILoading>();
        }

        protected override void OnDestroy()
        {
            if (_clientNetwork != null)
            {
                _clientNetwork.Dispatcher.UnregisterHandler((int)EServerCmd.MatchFound, this);
                _clientNetwork.OnConnected -= OnServerConnected;
                _clientNetwork.OnDisconnected -= OnServerDisconnected;
            }

            base.OnDestroy();
        }

        #region Editor Methods

        [Button]
        void TestMatch()
        {
            BeginLocalMatch(0, 0);
        }

        void TestSendPacket()
        {
            var mousePos = Input.mousePosition;
            var packet = new c2s_mouse_pos
            {
                X = mousePos.x,
                Y = mousePos.y
            };
            _clientNetwork?.Send(packet);
        }

        #endregion
    }
}
