using MH.Core;
using System;
using System.Collections.Generic;

namespace MH.GameLogic
{

    // | Object         | Size (puck = 1) |
    // | -------------- | --------------- |
    // | Puck           | 1               |
    // | Paddle         | 2.5             |
    // | Goal width     | 4.5             |
    // | Table width    | 9               |
    // | Table length   | 18              |
    // | Wall thickness | 0.5             |

    // Puck mass      = 1
    // Paddle mass    = 5 – 8
    // Bounciness     = 0.95 – 1
    // Friction       = 0
    // Linear drag    = 0

    public enum MatchPhase : byte
    {
        Playing = 0,
        PostGoal = 1,
    }

    public readonly struct GoalScoredEventData
    {
        public readonly int ScoringPlayerIndex;
        public readonly int ConcedingPlayerIndex;
        public readonly int Score0;
        public readonly int Score1;
        public readonly int ResetDurationMs;

        public GoalScoredEventData(int scoringPlayerIndex, int concedingPlayerIndex, int score0, int score1, int resetDurationMs)
        {
            ScoringPlayerIndex = scoringPlayerIndex;
            ConcedingPlayerIndex = concedingPlayerIndex;
            Score0 = score0;
            Score1 = score1;
            ResetDurationMs = resetDurationMs;
        }
    }

    public class Match
    {
        private readonly Dictionary<int, HockeyPlayer> _playerMap = new Dictionary<int, HockeyPlayer>();
        private readonly List<Wall> _walls = new List<Wall>();

        private readonly int _playerIdBottom;
        private readonly int _playerIdTop;

        private Puck _puck;
        private BoardConfig _config;

        /// <summary> Only one puck velocity bounce per tick (see plan: corner / multi-contact). </summary>
        bool _puckVelocityConsumedThisTick;

        int _score0;
        int _score1;
        MatchPhase _phase = MatchPhase.Playing;
        float _postGoalTimeRemaining;
        int _concedingPlayerId = -1;
        bool _hasPendingGoalBroadcast;

        GoalScoredEventData _pendingGoalBroadcast;

        /// <summary>When false, puck does not track goal colliders (guest prediction / non-authoritative clients).</summary>
        readonly bool _registerGoalTriggers;

        public Puck Puck => _puck;
        public IReadOnlyList<Wall> Walls => _walls;

        public MatchPhase Phase => _phase;
        public int Score0 => _score0;
        public int Score1 => _score1;

        /// <param name="registerGoalTriggers">Set false for clients that only mirror the authoritative sim (goals from snapshots only).</param>
        public Match(int playerId1, int playerId2, BoardConfig config, bool registerGoalTriggers = true)
        {
            _config = config;
            _registerGoalTriggers = registerGoalTriggers;
            _playerIdBottom = playerId1;
            _playerIdTop = playerId2;

            _playerMap[playerId1] = new HockeyPlayer(playerId1, config);
            _playerMap[playerId2] = new HockeyPlayer(playerId2, config);

            InitPuck(config, _playerMap[playerId1].Paddle, _playerMap[playerId2].Paddle);

            CreateDefaultWalls();
            RegisterPuckAgainstWallsAndHandlers();
            SetInitialObjectPositions(playerId1, playerId2);
        }

        private void InitPuck(BoardConfig config, Paddle paddle01, Paddle paddle02)
        {
            _puck = new Puck(config.PuckRadius);
            _puck.Collider.TrackOthers.Add(paddle01.GetComponent<CircleCollider>());
            _puck.Collider.TrackOthers.Add(paddle02.GetComponent<CircleCollider>());
        }

        /// <summary>
        /// Tick order: paddles integrate velocity → puck move + collision → clamp paddle positions.
        /// During <see cref="MatchPhase.PostGoal"/>, physics integration is skipped; timers and reset run instead.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_phase == MatchPhase.PostGoal)
            {
                TickPostGoal(deltaTime);
                return;
            }

            _puckVelocityConsumedThisTick = false;

            foreach (var player in _playerMap.Values)
                player.Paddle.Tick(deltaTime);

            foreach (var player in _playerMap.Values)
                ClampPaddlePosition(player);

            _puck.Tick(deltaTime);
        }

        /// <summary>Authoritative session reads this once per goal for <c>s2c_goal_scored</c>.</summary>
        public bool TryConsumeGoalScoredEvent(out GoalScoredEventData evt)
        {
            if (!_hasPendingGoalBroadcast)
            {
                evt = default;
                return false;
            }

            evt = _pendingGoalBroadcast;
            _hasPendingGoalBroadcast = false;
            return true;
        }

        public HockeyPlayer GetPlayer(int playerId)
        {
            if (!_playerMap.ContainsKey(playerId))
            {
                Logger.LogError($" Not found player with id = {playerId}");
                return null;
            }
            return _playerMap[playerId];
        }

        /// <summary> Phase 0 option 2: paddle moves via MoveComponent integration each tick. </summary>
        public void SetPaddleVelocity(int playerId, CustomVector2 velocity)
        {
            if (_phase != MatchPhase.Playing)
                return;

            var player = GetPlayer(playerId);
            if (player == null) return;

            velocity = CustomVector2.ClampMagnitude(velocity, _config.PaddleMaxSpeed);
            player.Paddle.GetComponent<MoveComponent>().SetVelocity(velocity);
        }

        /// <summary> Same as server <c>ApplyMouseToPlayer</c>: target world point → follow velocity (clamped). </summary>
        public void ApplyPaddleTargetFromWorld(int playerId, CustomVector2 targetWorld)
        {
            if (_phase != MatchPhase.Playing)
                return;

            var player = GetPlayer(playerId);
            if (player == null)
                return;

            var paddlePos = player.Paddle.GetComponent<Root2D>().Position;
            var vel = (targetWorld - paddlePos) * _config.PaddlePositionFollow;
            SetPaddleVelocity(playerId, vel);
        }

        /// <summary>
        /// Immediately place the puck at the post-goal spawn beside the conceding player and resume play.
        /// Used by editor Test buttons; skips <see cref="BoardConfig.PostGoalResetDelaySeconds"/>.
        /// </summary>
        public void RespawnPuckBesideConcedingPlayer(int concedingPlayerId)
        {
            if (!_playerMap.ContainsKey(concedingPlayerId))
                return;

            ResetPuckAfterGoal(concedingPlayerId);
            _phase = MatchPhase.Playing;
            _concedingPlayerId = -1;
        }

        void TickPostGoal(float deltaTime)
        {
            if (deltaTime > 0f)
                _postGoalTimeRemaining -= deltaTime;

            ZeroAllVelocities();

            foreach (var player in _playerMap.Values)
                ClampPaddlePosition(player);

            if (_postGoalTimeRemaining <= 0f)
            {
                ResetPuckAfterGoal(_concedingPlayerId);
                _phase = MatchPhase.Playing;
                _concedingPlayerId = -1;
            }
        }

        void ZeroAllVelocities()
        {
            _puck.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);
            foreach (var player in _playerMap.Values)
                player.Paddle.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);
        }

        void ResetPuckAfterGoal(int concedingPlayerId)
        {
            float halfL = _config.TableLenght * 0.5f;
            float paddleRowY = concedingPlayerId == _playerIdBottom
                ? -halfL + _config.PaddleRadius
                : halfL - _config.PaddleRadius;

            // Positive OffsetY moves the puck toward center on both ends (same sign convention as GoalFrameOffsetY).
            float spawnY = concedingPlayerId == _playerIdBottom
                ? paddleRowY + _config.PostGoalPuckSpawnOffsetY
                : paddleRowY - _config.PostGoalPuckSpawnOffsetY;

            var pos = new CustomVector2(_config.PostGoalPuckSpawnOffsetX, spawnY);
            _puck.GetComponent<Root2D>().Position = pos;
            _puck.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);
        }

        void TryScoreGoal(GoalFrame goalFrame)
        {
            if (_phase != MatchPhase.Playing)
                return;

            int conceding = -1;
            foreach (var p in _playerMap.Values)
            {
                if (p.GoalFrame == goalFrame)
                {
                    conceding = p.Id;
                    break;
                }
            }

            if (conceding < 0)
                return;

            int scoring = conceding == _playerIdBottom ? _playerIdTop : _playerIdBottom;
            if (scoring == 0)
                _score0++;
            else
                _score1++;

            _phase = MatchPhase.PostGoal;
            _postGoalTimeRemaining = Math.Max(0f, _config.PostGoalResetDelaySeconds);
            _concedingPlayerId = conceding;

            _puck.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);

            int resetMs = (int)Math.Round(_config.PostGoalResetDelaySeconds * 1000f);
            _pendingGoalBroadcast = new GoalScoredEventData(scoring, conceding, _score0, _score1, resetMs);
            _hasPendingGoalBroadcast = true;
        }

        void RegisterPuckAgainstWallsAndHandlers()
        {
            _puck.Collider.OnCollision += HandlePuckCollision;
            foreach (var wall in _walls)
                _puck.Collider.TrackOthers.Add(wall.Collider);

            if (_registerGoalTriggers)
            {
                foreach (var player in _playerMap.Values)
                    _puck.Collider.TrackOthers.Add(player.GoalFrame.GetComponent<RectCollider>());
            }
        }

        void HandlePuckCollision(CollisionInfo info)
        {
            var other = info.Collider1 == _puck.Collider ? info.Collider2 : info.Collider1;
            switch (other.Entity)
            {
                case Paddle paddle:
                    PuckCollisionResponse.ResolvePuckPaddle(
                        _puck,
                        paddle,
                        _config,
                        paddle.GetComponent<MoveComponent>().CurrentVelocity,
                        ref _puckVelocityConsumedThisTick);
                    break;
                case Wall wall:
                    PuckCollisionResponse.ResolvePuckWall(_puck, wall, _config, ref _puckVelocityConsumedThisTick);
                    break;
                case GoalFrame goalFrame:
                    TryScoreGoal(goalFrame);
                    break;
            }
        }

        void ClampPaddlePosition(HockeyPlayer player)
        {
            var root = player.Paddle.GetComponent<Root2D>();
            var move = player.Paddle.GetComponent<MoveComponent>();
            float maxX = _config.TableWidth * 0.5f - _config.PaddleRadius;
            float maxY = _config.TableLenght * 0.5f - _config.PaddleRadius;
            float px = root.Position.x;
            float py = root.Position.y;
            float x = Math.Clamp(px, -maxX, maxX);
            float y = py;
            const float guard = 0.05f;
            if (player.Id == _playerIdBottom)
                y = Math.Clamp(py, -maxY, -guard);
            else if (player.Id == _playerIdTop)
                y = Math.Clamp(py, guard, maxY);
            else
                y = Math.Clamp(py, -maxY, maxY);

            root.Position = new CustomVector2(x, y);

            // clamp paddle velocity to zero if hitting the wall, to prevent "sticking" from move integration pushing into the wall each tick.
            var vel = move.CurrentVelocity;
            if (Math.Abs(px - x) > 1e-4f) vel.x = 0f;
            if (Math.Abs(py - y) > 1e-4f) vel.y = 0f;
            move.SetVelocity(vel);

        }

        private void CreateDefaultWalls()
        {
            float t = _config.WallThickness;
            float halfL = _config.TableLenght * 0.5f;
            float halfW = _config.TableWidth * 0.5f;
            float gw = _config.GoalWidth;
            float midGap = Math.Max(0f, _config.VerticalWallCenterGap);

            _walls.Clear();

            // Corner horizontal segments: goal opening (width GoalWidth) centered on top/bottom edges.
            float cornerSegW = Math.Max(1e-4f, halfW - gw * 0.5f);
            float leftCenterX = -(halfW + gw * 0.5f) * 0.5f;
            float rightCenterX = -leftCenterX;
            float yBottom = -halfL - t * 0.5f;
            float yTop = halfL + t * 0.5f;

            var bottomLeft = new Wall(cornerSegW, t);
            bottomLeft.GetComponent<Root2D>().Position = new CustomVector2(leftCenterX, yBottom);
            _walls.Add(bottomLeft);

            var bottomRight = new Wall(cornerSegW, t);
            bottomRight.GetComponent<Root2D>().Position = new CustomVector2(rightCenterX, yBottom);
            _walls.Add(bottomRight);

            var topLeft = new Wall(cornerSegW, t);
            topLeft.GetComponent<Root2D>().Position = new CustomVector2(leftCenterX, yTop);
            _walls.Add(topLeft);

            var topRight = new Wall(cornerSegW, t);
            topRight.GetComponent<Root2D>().Position = new CustomVector2(rightCenterX, yTop);
            _walls.Add(topRight);

            // Vertical walls split at center line (small gap); height matches former full side rails including corner overlap.
            float vertH = halfL + t - midGap * 0.5f;
            float xLeft = -halfW - t * 0.5f;
            float xRight = halfW + t * 0.5f;
            float yTopSeg = (midGap * 0.5f + halfL + t) * 0.5f;
            float yBotSeg = (-halfL - t + -midGap * 0.5f) * 0.5f;

            var leftTop = new Wall(t, vertH);
            leftTop.GetComponent<Root2D>().Position = new CustomVector2(xLeft, yTopSeg);
            _walls.Add(leftTop);

            var leftBottom = new Wall(t, vertH);
            leftBottom.GetComponent<Root2D>().Position = new CustomVector2(xLeft, yBotSeg);
            _walls.Add(leftBottom);

            var rightTop = new Wall(t, vertH);
            rightTop.GetComponent<Root2D>().Position = new CustomVector2(xRight, yTopSeg);
            _walls.Add(rightTop);

            var rightBottom = new Wall(t, vertH);
            rightBottom.GetComponent<Root2D>().Position = new CustomVector2(xRight, yBotSeg);
            _walls.Add(rightBottom);
        }

        private void SetInitialObjectPositions(int playerIdBottom, int playerIdTop)
        {
            float WallThickness = _config.WallThickness;
            var TableLength = _config.TableLenght;
            var TableWidth = _config.TableWidth;
            var PaddleSize = _config.PaddleRadius;

            // Puck starts at the center.
            _puck.GetComponent<Root2D>().Position = CustomVector2.Zero;

            var goalOffsetX = _config.GoalFrameOffsetX;
            var goalOffsetY = _config.GoalFrameOffsetY;

            var bottom = GetPlayer(playerIdBottom);
            if (bottom != null)
            {
                bottom.Paddle.GetComponent<Root2D>().Position = new CustomVector2(0f, -TableLength / 2f + PaddleSize);
                bottom.GoalFrame.GetComponent<Root2D>().Position = new CustomVector2(
                    goalOffsetX,
                    -TableLength / 2f + WallThickness / 2f + goalOffsetY);
            }

            var top = GetPlayer(playerIdTop);
            if (top != null)
            {
                top.Paddle.GetComponent<Root2D>().Position = new CustomVector2(0f, TableLength / 2f - PaddleSize);
                top.GoalFrame.GetComponent<Root2D>().Position = new CustomVector2(
                    goalOffsetX,
                    TableLength / 2f - WallThickness / 2f - goalOffsetY);
            }
        }
    }


}
