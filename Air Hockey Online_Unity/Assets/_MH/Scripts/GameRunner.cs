using System;
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
        #region FIELDS

        [SerializeField] private BoardConfig _config;

        private ClientNetwork _clientNetwork;
        private HostGameSession _hostSession;
        private bool _isHost;
        private Match _currentMatch;
        private int _activeMatchId;
        private bool _isMouseDown;
        private EGameState _gameState;
        private int _localPlayerIndex;
        private Quaternion _defaultMainCameraRotation;
        private bool _hasDefaultMainCameraRotation;

        // Guest client prediction: local sim between server snapshots (host uses authoritative session only).
        private readonly GuestPredictionService _guestPrediction = new GuestPredictionService();
        private CustomVector2 _latestLocalTarget;
        private bool _hasLatestLocalTarget;

        [Header("Debug (guest prediction)")]
        [Tooltip("Draw error segments (predicted → server) on snapshot; HUD shows magnitudes.")]
        [SerializeField] private bool _showPredictionDebug;

        private Vector3 _dbgPuckPred;
        private Vector3 _dbgPuckServer;
        private Vector3 _dbgPad0Pred;
        private Vector3 _dbgPad0Server;
        private Vector3 _dbgPad1Pred;
        private Vector3 _dbgPad1Server;
        private float _dbgPuckErrMag;
        private float _dbgPad0ErrMag;
        private float _dbgPad1ErrMag;
        private bool _dbgHasSnapshotErrors;

        public Match CurrentMatch => _currentMatch;
        public EGameState GameState => _gameState;

        public bool IsHosting => _hostSession != null;

        /// <summary>LiteNetLib RTT to the matchmaking/server peer (ms), or -1 when not connected.</summary>
        public int ServerRoundTripPingMs => _clientNetwork?.ConnectedPeerRoundTripMs ?? -1;

        #endregion

        #region UNITY METHODS

        void Update()
        {
            _hostSession?.Poll();

            if (_gameState != EGameState.Playing || _currentMatch == null)
                return;

            // Input: guest sends to server; host applies directly to authoritative simulation.
            if (Input.GetMouseButtonDown(0))
            {
                _isMouseDown = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isMouseDown = false;
            }

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

            if (_isHost)
                _hostSession?.ApplyHostInput(target.x, target.y);
            else
            {
                _latestLocalTarget = target;
                _hasLatestLocalTarget = true;
                _clientNetwork?.SendToServer(new c2s_mouse_pos { X = target.x, Y = target.y });
            }
        }

        void FixedUpdate()
        {
            if (_hostSession != null)
                _hostSession.TickSimulation(Time.fixedDeltaTime);
            else
                GuestPredictionFixedStep();
        }

        void OnDrawGizmos()
        {
            if (!_showPredictionDebug || !_dbgHasSnapshotErrors)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_dbgPuckPred, _dbgPuckServer);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(_dbgPad0Pred, _dbgPad0Server);
            Gizmos.color = new Color(0.5f, 0f, 1f);
            Gizmos.DrawLine(_dbgPad1Pred, _dbgPad1Server);
        }

        void OnGUI()
        {
            if (!_showPredictionDebug || !_dbgHasSnapshotErrors || _isHost)
                return;

            var style = GUI.skin.box;
            GUI.Box(
                new Rect(8f, 8f, 280f, 72f),
                $"Prediction vs server (last snapshot)\n" +
                $"puck Δ {_dbgPuckErrMag:F3}  |  pad0 Δ {_dbgPad0ErrMag:F3}  |  pad1 Δ {_dbgPad1ErrMag:F3}",
                style);
        }

        protected override void OnDestroy()
        {
            StopHosting();

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

        #endregion

        #region API

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
            Time.fixedDeltaTime = 1f / 60f;
        }

        /// <summary>Listen on game port and wait for one guest (LAN). Host is always bottom player (index 0).</summary>
        public bool StartHosting()
        {
            if (_gameState == EGameState.Connecting || _gameState == EGameState.Matching ||
                _gameState == EGameState.WaitingForGuest || _gameState == EGameState.Playing)
                return false;

            StopHosting();

            var session = new HostGameSession(_config, OnHostMatchReady, OnHostMatchResultFromSession);
            if (!session.TryListen())
            {
                ShowHostListenFailed();
                return false;
            }

            session.BeginWaitingForGuest();
            _hostSession = session;
            _isHost = true;
            _gameState = EGameState.WaitingForGuest;

            // Stay on UILobby so the player can cancel with Back; lobby script switches title / buttons.
            return true;
        }

        public void StopHosting()
        {
            if (_hostSession == null)
                return;

            _hostSession.Dispose();
            _hostSession = null;
            _isHost = false;

            if (_gameState == EGameState.WaitingForGuest)
                _gameState = EGameState.MainMenu;
        }

        public void RequestMatchmaking()
        {
            ConnectAndRequestMatchmaking(host: null, port: 0);
        }

        public void ConnectAndRequestMatchmaking(string host, int port)
        {
            // Validation: prevent double-connect / double-matchmaking.
            if (_gameState == EGameState.Connecting || _gameState == EGameState.Matching ||
                _gameState == EGameState.WaitingForGuest || _isHost)
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
                    if (_isHost)
                        return;

                    if (_gameState != EGameState.Playing || _currentMatch == null)
                        return;

                    _guestPrediction.ApplyBoardStatus(
                        _currentMatch,
                        _activeMatchId,
                        (s2c_board_status)packet,
                        beforeReconcile: _showPredictionDebug ? CaptureSnapshotPredictionErrors : null);
                    break;
                }
                case EServerCmd.MatchResult:
                {
                    HandleMatchResult((s2c_match_result)packet);
                    break;
                }
            }
        }

        #endregion

        #region EVENT_HANDLER

        private void OnHostMatchReady(int matchId, Match authoritativeMatch, int localPlayerIndex)
        {
            BeginLocalMatchAsHost(authoritativeMatch, matchId, localPlayerIndex);
        }

        private void OnHostMatchResultFromSession(s2c_match_result result)
        {
            HandleMatchResult(result);
        }

        private void OnServerConnected()
        {
            if (_gameState != EGameState.Connecting)
                return;

            _gameState = EGameState.Matching;
            _clientNetwork.SendToServer(new c2s_find_match());
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

        #endregion

        private void ShowHostListenFailed()
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.TryGet<UINotifyPopup>(out var popup))
            {
                popup.Show(
                    title: "Cannot start host",
                    content: "UDP port may be in use. Stop other game servers or choose another machine.",
                    yesLabel: "OK",
                    onYes: () =>
                    {
                        ui.Hide<UINotifyPopup>();
                        ui.Show<UIMainMenu>();
                    });
                ui.Show<UINotifyPopup>();
            }
            else
            {
                Debug.LogError("Cannot bind host port (9050).");
            }
        }

        private void BeginLocalMatch(int matchId, int localPlayerIndex)
        {
            Debug.Log($"Match started: id={matchId}, localPlayer={localPlayerIndex}");

            _currentMatch = new Match(0, 1, _config);
            FinishMatchSetup(matchId, localPlayerIndex);
        }

        private void BeginLocalMatchAsHost(Match authoritativeMatch, int matchId, int localPlayerIndex)
        {
            Debug.Log($"Host match started: id={matchId}, localPlayer={localPlayerIndex}");

            _currentMatch = authoritativeMatch;
            FinishMatchSetup(matchId, localPlayerIndex);
        }

        private void FinishMatchSetup(int matchId, int localPlayerIndex)
        {
            var matchView = GetComponent<MatchView2D>() ?? gameObject.AddComponent<MatchView2D>();
            matchView.SetMatch(_currentMatch);
            _activeMatchId = matchId;
            _localPlayerIndex = localPlayerIndex;
            var initialLocal = _currentMatch.GetPlayer(localPlayerIndex);
            if (initialLocal != null)
            {
                _latestLocalTarget = initialLocal.Paddle.GetComponent<Root2D>().Position;
                _hasLatestLocalTarget = true;
            }
            else
            {
                _hasLatestLocalTarget = false;
            }

            _guestPrediction.Reset();
            _dbgHasSnapshotErrors = false;
            ApplyClientViewForLocalPlayer(_localPlayerIndex);
            _gameState = EGameState.Playing;

            var ui = UIManager.Instance;
            ui?.Hide<UILoading>();
            ui?.Hide<UILobby>();
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
            StopHosting();

            // Cleanup: clear local match visuals/state.
            var matchView = GetComponent<MatchView2D>();
            if (matchView != null)
                matchView.SetMatch(null);
            _currentMatch = null;
            _activeMatchId = 0;
            _hasLatestLocalTarget = false;
            _guestPrediction.Reset();
            _dbgHasSnapshotErrors = false;
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

        private void GuestPredictionFixedStep()
        {
            if (_isHost || _gameState != EGameState.Playing || _currentMatch == null)
                return;

            _guestPrediction.FixedStep(
                _currentMatch,
                _localPlayerIndex,
                _hasLatestLocalTarget,
                _latestLocalTarget,
                Time.fixedDeltaTime);
        }

        private void CaptureSnapshotPredictionErrors(s2c_board_status s)
        {
            var puckRoot = _currentMatch.Puck.GetComponent<Root2D>();
            var p0 = _currentMatch.GetPlayer(0);
            var p1 = _currentMatch.GetPlayer(1);
            if (puckRoot == null || p0 == null || p1 == null)
                return;

            var r0 = p0.Paddle.GetComponent<Root2D>();
            var r1 = p1.Paddle.GetComponent<Root2D>();
            if (r0 == null || r1 == null)
                return;

            _dbgPuckPred = new Vector3(puckRoot.Position.x, puckRoot.Position.y, 0f);
            _dbgPuckServer = new Vector3(s.PuckX, s.PuckY, 0f);
            _dbgPad0Pred = new Vector3(r0.Position.x, r0.Position.y, 0f);
            _dbgPad0Server = new Vector3(s.Paddle0X, s.Paddle0Y, 0f);
            _dbgPad1Pred = new Vector3(r1.Position.x, r1.Position.y, 0f);
            _dbgPad1Server = new Vector3(s.Paddle1X, s.Paddle1Y, 0f);

            _dbgPuckErrMag = CustomVector2.Distance(puckRoot.Position, new CustomVector2(s.PuckX, s.PuckY));
            _dbgPad0ErrMag = CustomVector2.Distance(r0.Position, new CustomVector2(s.Paddle0X, s.Paddle0Y));
            _dbgPad1ErrMag = CustomVector2.Distance(r1.Position, new CustomVector2(s.Paddle1X, s.Paddle1Y));
            _dbgHasSnapshotErrors = true;
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
            _clientNetwork?.SendToServer(packet);
        }

        #endregion
    }
}
