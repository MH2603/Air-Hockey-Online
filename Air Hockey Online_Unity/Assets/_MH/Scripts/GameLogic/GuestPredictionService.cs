using System;
using MH.Core;
using UnityEngine;

namespace MH.GameLogic
{
    /// <summary>
    /// Guest-side prediction between <c>s2c_board_status</c> snapshots: local <see cref="Match.Tick"/>
    /// and soft reconcile toward server state.
    /// </summary>
    public class GuestPredictionService
    {
        private const float ReconcileSoftLerp = 0.35f;
        private const float PuckSnapDistance = 0.85f;
        private const float PaddleSnapDistance = 1.2f;

        private s2c_board_status _lastAuthoritativeBoard;
        private bool _hasAuthoritativeBoard;

        public void Reset()
        {
            _hasAuthoritativeBoard = false;
        }

        /// <summary>
        /// Guest: run shared <see cref="Match.Tick"/> at fixed rate between snapshots.
        /// Remote paddle target comes from the last authoritative board when available.
        /// </summary>
        public void FixedStep(
            Match match,
            int localPlayerIndex,
            bool hasLatestLocalTarget,
            CustomVector2 latestLocalTarget,
            float dt)
        {
            if (dt <= 0f)
                return;

            int remoteId = localPlayerIndex == 0 ? 1 : 0;

            if (hasLatestLocalTarget)
                match.ApplyPaddleTargetFromWorld(localPlayerIndex, latestLocalTarget);

            if (_hasAuthoritativeBoard)
            {
                var remoteTarget = PaddlePositionFromStatus(_lastAuthoritativeBoard, remoteId);
                match.ApplyPaddleTargetFromWorld(remoteId, remoteTarget);
            }

            match.Tick(dt);
        }

        /// <summary>
        /// Apply a server snapshot: optional <paramref name="beforeReconcile"/> runs after validation
        /// (e.g. debug capture of prediction error before correction).
        /// </summary>
        public void ApplyBoardStatus(
            Match match,
            int activeMatchId,
            s2c_board_status status,
            Action<s2c_board_status> beforeReconcile = null)
        {
            var p0 = match.GetPlayer(0);
            var p1 = match.GetPlayer(1);
            if (p0 == null || p1 == null || match.Puck == null)
                return;

            if (status.MatchId != activeMatchId)
                return;

            beforeReconcile?.Invoke(status);

            ReconcileTowardServerState(match, status);

            _lastAuthoritativeBoard = status;
            _hasAuthoritativeBoard = true;
        }

        private static void ReconcileTowardServerState(Match match, s2c_board_status s)
        {
            var puckRoot = match.Puck.GetComponent<Root2D>();
            var puckMove = match.Puck.GetComponent<MoveComponent>();
            var p0 = match.GetPlayer(0);
            var p1 = match.GetPlayer(1);

            var puckPos = puckRoot.Position;
            var serverPuck = new CustomVector2(s.PuckX, s.PuckY);
            float puckErr = CustomVector2.Distance(puckPos, serverPuck);
            float tPuck = puckErr >= PuckSnapDistance ? 1f : ReconcileSoftLerp;
            puckRoot.Position = LerpCv2(puckPos, serverPuck, tPuck);

            var vel = puckMove.CurrentVelocity;
            var serverVel = new CustomVector2(s.PuckVelX, s.PuckVelY);
            puckMove.SetVelocity(LerpCv2(vel, serverVel, tPuck));

            ReconcilePaddle(p0.Paddle.GetComponent<Root2D>(), new CustomVector2(s.Paddle0X, s.Paddle0Y));
            ReconcilePaddle(p1.Paddle.GetComponent<Root2D>(), new CustomVector2(s.Paddle1X, s.Paddle1Y));
        }

        private static void ReconcilePaddle(Root2D root, CustomVector2 serverPos)
        {
            var pos = root.Position;
            float err = CustomVector2.Distance(pos, serverPos);
            float t = err >= PaddleSnapDistance ? 1f : ReconcileSoftLerp;
            root.Position = LerpCv2(pos, serverPos, t);
        }

        private static CustomVector2 LerpCv2(CustomVector2 a, CustomVector2 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new CustomVector2(
                Mathf.Lerp(a.x, b.x, t),
                Mathf.Lerp(a.y, b.y, t));
        }

        private static CustomVector2 PaddlePositionFromStatus(s2c_board_status s, int playerId)
        {
            return playerId == 0
                ? new CustomVector2(s.Paddle0X, s.Paddle0Y)
                : new CustomVector2(s.Paddle1X, s.Paddle1Y);
        }
    }
}
