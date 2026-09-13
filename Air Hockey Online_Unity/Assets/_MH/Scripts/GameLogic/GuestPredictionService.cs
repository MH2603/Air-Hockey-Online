using System;
using System.Collections.Generic;
using MH.Core;

namespace MH.GameLogic
{
    /// <summary>
    /// Guest-side prediction: local <see cref="Match.Tick"/> plus tick-keyed rewind-and-replay
    /// against <c>s2c_board_status</c> (no lerp of the local paddle toward a stale snapshot).
    /// </summary>
    public class GuestPredictionService
    {
        #region FIELDS

        const int HistoryCapacity = 120;

        readonly Queue<PredictedInput> _history = new Queue<PredictedInput>(HistoryCapacity);

        s2c_board_status _lastAuthoritativeBoard;
        bool _hasAuthoritativeBoard;
        bool _hasAppliedServerTick;
        uint _lastAppliedServerTick;
        uint _clientTick;
        uint _lastAckedInputTick;
        float _fixedDt = 1f / 60f;

        #endregion

        #region PROPERTIES

        /// <summary>Last snapshot’s <see cref="MatchPhase"/> (Playing when no snapshot yet).</summary>
        public byte LastAuthoritativeMatchPhase =>
            _hasAuthoritativeBoard ? _lastAuthoritativeBoard.MatchPhase : (byte)MatchPhase.Playing;

        public uint ClientTick => _clientTick;
        public uint LastAppliedServerTick => _lastAppliedServerTick;
        public uint LastAckedInputTick => _lastAckedInputTick;
        public int PendingHistoryCount => _history.Count;

        #endregion

        /// <summary>Config is retained for the serialized GameRunner field; Playing rewind does not use lerp.</summary>
        public GuestPredictionService(GuestPredictionConfig config = null)
        {
            _ = config;
        }

        public void Reset()
        {
            _hasAuthoritativeBoard = false;
            _hasAppliedServerTick = false;
            _lastAppliedServerTick = 0;
            _clientTick = 0;
            _lastAckedInputTick = 0;
            _history.Clear();
        }

        /// <summary>
        /// Guest: stamp one input tick, record history, run shared <see cref="Match.Tick"/>.
        /// Returns the stamped tick, or 0 if the step was skipped (PostGoal / invalid dt).
        /// </summary>
        public uint FixedStep(
            Match match,
            int localPlayerIndex,
            bool hasLatestLocalTarget,
            CustomVector2 latestLocalTarget,
            float dt)
        {
            if (dt <= 0f)
                return 0;

            if (_hasAuthoritativeBoard && _lastAuthoritativeBoard.MatchPhase == (byte)MatchPhase.PostGoal)
                return 0;

            if (!hasLatestLocalTarget)
                return 0;

            _fixedDt = dt;
            _clientTick++;
            PushHistory(_clientTick, latestLocalTarget);

            ApplyPredictedTick(match, localPlayerIndex, latestLocalTarget, _hasAuthoritativeBoard, _lastAuthoritativeBoard);
            return _clientTick;
        }

        /// <summary>
        /// Apply a server snapshot: drop stale <see cref="s2c_board_status.ServerTick"/>, snap,
        /// then replay unacked local inputs. <paramref name="beforeReconcile"/> runs after validation
        /// and before correction (debug error capture).
        /// </summary>
        public void ApplyBoardStatus(
            Match match,
            int activeMatchId,
            int localPlayerIndex,
            s2c_board_status status,
            Action<s2c_board_status> beforeReconcile = null)
        {
            var p0 = match.GetPlayer(0);
            var p1 = match.GetPlayer(1);
            if (p0 == null || p1 == null || match.Puck == null)
                return;

            if (status.MatchId != activeMatchId)
                return;

            if (_hasAppliedServerTick && status.ServerTick <= _lastAppliedServerTick)
                return;

            beforeReconcile?.Invoke(status);

            ReconcileTowardServerState(match, localPlayerIndex, status);

            _lastAuthoritativeBoard = status;
            _hasAuthoritativeBoard = true;
            _lastAppliedServerTick = status.ServerTick;
            _hasAppliedServerTick = true;
            _lastAckedInputTick = status.LastProcessedInputTick;
        }

        #region PRIVATE_METHODS

        void ReconcileTowardServerState(Match match, int localPlayerIndex, s2c_board_status s)
        {
            SnapToSnapshot(match, s);

            if (s.MatchPhase == (byte)MatchPhase.PostGoal)
            {
                _history.Clear();
                return;
            }

            // No ack yet: keep history so the first real ack can replay.
            if (s.LastProcessedInputTick == 0)
                return;

            if (HasHistoryGap(s.LastProcessedInputTick))
            {
                _history.Clear();
                return;
            }

            DropAckedHistory(s.LastProcessedInputTick);
            ReplayUnackedInputs(match, localPlayerIndex, s);
        }

        void SnapToSnapshot(Match match, s2c_board_status s)
        {
            var puckRoot = match.Puck.GetComponent<Root2D>();
            var puckMove = match.Puck.GetComponent<MoveComponent>();
            var p0 = match.GetPlayer(0);
            var p1 = match.GetPlayer(1);

            puckRoot.Position = new CustomVector2(s.PuckX, s.PuckY);
            puckMove.SetVelocity(new CustomVector2(s.PuckVelX, s.PuckVelY));

            SnapPaddle(p0.Paddle, new CustomVector2(s.Paddle0X, s.Paddle0Y));
            SnapPaddle(p1.Paddle, new CustomVector2(s.Paddle1X, s.Paddle1Y));
        }

        static void SnapPaddle(Paddle paddle, CustomVector2 serverPos)
        {
            paddle.GetComponent<Root2D>().Position = serverPos;
            paddle.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);
        }

        void ReplayUnackedInputs(Match match, int localPlayerIndex, s2c_board_status snapshot)
        {
            if (_history.Count == 0 || _fixedDt <= 0f)
                return;

            foreach (var input in _history)
                ApplyPredictedTick(match, localPlayerIndex, input.Target, hasRemoteBoard: true, snapshot);
        }

        void ApplyPredictedTick(
            Match match,
            int localPlayerIndex,
            CustomVector2 localTarget,
            bool hasRemoteBoard,
            s2c_board_status remoteBoard)
        {
            int remoteId = localPlayerIndex == 0 ? 1 : 0;

            match.ApplyPaddleTargetFromWorld(localPlayerIndex, localTarget);

            if (hasRemoteBoard)
            {
                var remoteTarget = PaddlePositionFromStatus(remoteBoard, remoteId);
                match.ApplyPaddleTargetFromWorld(remoteId, remoteTarget);
            }

            match.Tick(_fixedDt);
        }

        void PushHistory(uint tick, CustomVector2 target)
        {
            if (_history.Count >= HistoryCapacity)
                _history.Dequeue();

            _history.Enqueue(new PredictedInput(tick, target));
        }

        void DropAckedHistory(uint lastProcessedInputTick)
        {
            while (_history.Count > 0 && _history.Peek().Tick <= lastProcessedInputTick)
                _history.Dequeue();
        }

        bool HasHistoryGap(uint lastProcessedInputTick)
        {
            if (_history.Count == 0)
                return _clientTick > lastProcessedInputTick;

            return _history.Peek().Tick > lastProcessedInputTick + 1;
        }

        static CustomVector2 PaddlePositionFromStatus(s2c_board_status s, int playerId)
        {
            return playerId == 0
                ? new CustomVector2(s.Paddle0X, s.Paddle0Y)
                : new CustomVector2(s.Paddle1X, s.Paddle1Y);
        }

        #endregion

        #region INNER_TYPES

        readonly struct PredictedInput
        {
            public readonly uint Tick;
            public readonly CustomVector2 Target;

            public PredictedInput(uint tick, CustomVector2 target)
            {
                Tick = tick;
                Target = target;
            }
        }

        #endregion
    }
}
