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
        private Quaternion _defaultMainCameraRotation;
        private bool _hasDefaultMainCameraRotation;

        public Match CurrentMatch => _currentMatch;
        public EGameState GameState => _gameState;

        void Update()
        {
            if (_gameState != EGameState.Playing || _currentMatch == null)
                return;

            // Input (client) -> send target mouse world position to server.
            if (Input.GetMouseButtonDown(0))
            {
                _isMouseDown = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isMouseDown = false;
            }

            // We always send a target point. When mouse is not down, use current paddle position (=> near-zero velocity on server).
            var localPlayer = _currentMatch.GetPlayer(_localPlayerIndex);
            if (localPlayer == null)
                return;

            CustomVector2 target;
            if (_isMouseDown)
            {
                var mousePos = MouseUtils.GetMouseWorldPosition();
                target = new CustomVector2(mousePos.x, mousePos.y);
            }
            else
            {
                target = localPlayer.Paddle.GetComponent<Root2D>().Position;
            }

            _clientNetwork?.Send(new c2s_mouse_pos { X = target.x, Y = target.y });
        }

        public void Init(ClientNetwork clientNetwork)
        {
            _clientNetwork = clientNetwork;
            _clientNetwork.Dispatcher.RegisterHandler<s2c_match_found>((int)EServerCmd.MatchFound, this);
            _clientNetwork.Dispatcher.RegisterHandler<s2c_board_status>((int)EServerCmd.BoardStatus, this);
            _clientNetwork.Dispatcher.RegisterHandler<s2c_match_result>((int)EServerCmd.MatchResult, this);
            _clientNetwork.OnConnected += OnServerConnected;
            _clientNetwork.OnDisconnected += OnServerDisconnected;

            _gameState = EGameState.MainMenu;
            Application.targetFrameRate = 60;
        }

        public void RequestMatchmaking()
        {
            ConnectAndRequestMatchmaking(host: null, port: 0);
        }

        public void ConnectAndRequestMatchmaking(string host, int port)
        {
            // Validation: prevent double-connect / double-matchmaking.
            if (_gameState == EGameState.Connecting || _gameState == EGameState.Matching)
                return;

            // Initialization: if provided, override the current connection target.
            if (!string.IsNullOrWhiteSpace(host) && port > 0)
                _clientNetwork?.SetConnectionTarget(host, port);

            // UI: hide menus, show loading.
            _gameState = EGameState.Connecting;
            var ui = UIManager.Instance;
            if (ui != null)
            {
                ui.Hide<UIMainMenu>();
                ui.Hide<UILobby>();
                if (ui.TryGet<UILoading>(out _))
                    ui.Show<UILoading>();
                else
                    Debug.LogWarning("UILoading is not registered on UIManager; add a UILoading prefab to view prefabs.");
            }

            // Main logic: connect (OnConnected will send c2s_find_match).
            _clientNetwork?.StartConnect();
        }

        public void HandlePacket(int fromId, int packType, INetPacket packet)
        {
            var cmd = (EServerCmd)packType;
            switch (cmd)
            {
                case EServerCmd.MatchFound:
                {
                    if (_gameState == EGameState.Playing && _currentMatch != null)
                    {
                        Debug.LogWarning("MatchFound ignored: already playing.");
                        return;
                    }

                    var found = (s2c_match_found)packet;
                    BeginLocalMatch(found.MatchId, found.LocalPlayerIndex);
                    break;
                }
                case EServerCmd.BoardStatus:
                {
                    if (_gameState != EGameState.Playing || _currentMatch == null)
                        return;

                    ApplyBoardStatus((s2c_board_status)packet);
                    break;
                }
                case EServerCmd.MatchResult:
                {
                    HandleMatchResult((s2c_match_result)packet);
                    break;
                }
            }
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
            // If we lose connection during matchmaking OR mid-match, we notify and return to main menu.
            if (_gameState == EGameState.Playing)
            {
                ShowNotifyAndBackToMainMenu("Disconnected", "Lost connection to server.");
                return;
            }

            ShowNotifyAndBackToMainMenu("Matchmaking failed", "Could not connect to server.");
        }

        private void BeginLocalMatch(int matchId, int localPlayerIndex)
        {
            Debug.Log($"Match started: id={matchId}, localPlayer={localPlayerIndex}");

            _currentMatch = new Match(0, 1, _config);
            var matchView = GetComponent<MatchView2D>() ?? gameObject.AddComponent<MatchView2D>();
            matchView.SetMatch(_currentMatch);
            _localPlayerIndex = localPlayerIndex;
            ApplyClientViewForLocalPlayer(_localPlayerIndex);
            _gameState = EGameState.Playing;

            var ui = UIManager.Instance;
            ui?.Hide<UILoading>();
        }

        private void HandleMatchResult(s2c_match_result result)
        {
            // Validation
            if (_gameState != EGameState.Playing)
                return;

            // Main logic: compute local win/lose and notify.
            var isWinner = _localPlayerIndex == result.WinnerPlayerIndex;
            var title = isWinner ? "You Win" : "You Lose";
            var content = isWinner ? "Opponent disconnected. You win!" : "You disconnected. You lose!";

            ShowNotifyAndBackToMainMenu(title, content);
        }

        private void ShowNotifyAndBackToMainMenu(string title, string content)
        {
            // Cleanup: stop match view and return UI to menu after acknowledgement.
            var ui = UIManager.Instance;
            if (ui != null && ui.TryGet<UINotifyPopup>(out var popup))
            {
                popup.Show(
                    title: title,
                    content: content,
                    yesLabel: "OK",
                    onYes: BackToMainMenuInternal);
                ui.Show<UINotifyPopup>();
            }
            else
            {
                Debug.LogWarning("UINotifyPopup is not registered on UIManager; add UINotifyPopup prefab to view prefabs.");
                BackToMainMenuInternal();
            }
        }

        private void BackToMainMenuInternal()
        {
            // Cleanup: clear local match visuals/state.
            var matchView = GetComponent<MatchView2D>();
            if (matchView != null)
                matchView.SetMatch(null);
            _currentMatch = null;
            _gameState = EGameState.MainMenu;

            // Restore camera rotation (client-only).
            if (_hasDefaultMainCameraRotation)
            {
                var cam = Camera.main;
                if (cam != null)
                    cam.transform.rotation = _defaultMainCameraRotation;
            }

            // UI: show main menu, hide loading (and any stacked windows).
            var ui = UIManager.Instance;
            if (ui != null)
            {
                ui.Hide<UILoading>();
                ui.Hide<UINotifyPopup>();
                if (ui.TryGet<UIMainMenu>(out _))
                    ui.Show<UIMainMenu>();
            }
        }

        private void ApplyClientViewForLocalPlayer(int localPlayerIndex)
        {
            // Client-only: flip the camera for the "top" player so their paddle appears at the bottom too.
            // This keeps server/world coordinates unchanged; mouse world position stays correct because it uses the same camera.
            var cam = Camera.main;
            if (cam == null)
                return;

            if (!_hasDefaultMainCameraRotation)
            {
                _defaultMainCameraRotation = cam.transform.rotation;
                _hasDefaultMainCameraRotation = true;
            }

            // Assumption: Player 0 is bottom, Player 1 is top (matches server packets + MatchView2D defaults).
            cam.transform.rotation = localPlayerIndex == 1
                ? Quaternion.Euler(0f, 0f, 180f)
                : _defaultMainCameraRotation;
        }

        private void ApplyBoardStatus(s2c_board_status status)
        {
            // Validation
            var p0 = _currentMatch.GetPlayer(0);
            var p1 = _currentMatch.GetPlayer(1);
            if (p0 == null || p1 == null || _currentMatch.Puck == null)
                return;

            // Apply authoritative transforms (server owns physics).
            _currentMatch.Puck.GetComponent<Root2D>().Position = new CustomVector2(status.PuckX, status.PuckY);
            _currentMatch.Puck.GetComponent<MoveComponent>().SetVelocity(new CustomVector2(status.PuckVelX, status.PuckVelY));

            p0.Paddle.GetComponent<Root2D>().Position = new CustomVector2(status.Paddle0X, status.Paddle0Y);
            p1.Paddle.GetComponent<Root2D>().Position = new CustomVector2(status.Paddle1X, status.Paddle1Y);
        }

        protected override void OnDestroy()
        {
            if (_clientNetwork != null)
            {
                _clientNetwork.Dispatcher.UnregisterHandler((int)EServerCmd.MatchFound, this);
                _clientNetwork.Dispatcher.UnregisterHandler((int)EServerCmd.BoardStatus, this);
                _clientNetwork.Dispatcher.UnregisterHandler((int)EServerCmd.MatchResult, this);
                _clientNetwork.OnConnected -= OnServerConnected;
                _clientNetwork.OnDisconnected -= OnServerDisconnected;
            }

            // Restore camera rotation if we changed it (client-only cleanup).
            if (_hasDefaultMainCameraRotation)
            {
                var cam = Camera.main;
                if (cam != null)
                    cam.transform.rotation = _defaultMainCameraRotation;
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
