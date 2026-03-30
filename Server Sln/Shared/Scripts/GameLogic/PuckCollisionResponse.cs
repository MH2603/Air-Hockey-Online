using MH.Core;
using System;

namespace MH.GameLogic
{
    /// <summary>
    /// Puck vs paddle (circle–circle) and puck vs wall (circle–AABB) separation + velocity (see SharedLibrary Doc).
    /// </summary>
    public static class PuckCollisionResponse
    {
        const float DegenerateNormalSq = 1e-10f;

        public static void ResolvePuckPaddle(
            Puck puck,
            Paddle paddle,
            BoardConfig config,
            CustomVector2 paddleVelocity,
            ref bool puckVelocityConsumedThisTick)
        {
            var puckRoot = puck.GetComponent<Root2D>();
            var paddleRoot = paddle.GetComponent<Root2D>();
            var puckMove = puck.GetComponent<MoveComponent>();
            var puckCol = puck.GetComponent<CircleCollider>();
            var paddleCol = paddle.GetComponent<CircleCollider>();

            CustomVector2 puckPos = puckRoot.Position;
            CustomVector2 paddlePos = paddleRoot.Position;
            float rp = puckCol.Radius;
            float rd = paddleCol.Radius;
            float sumR = rp + rd;
            float dist = CustomVector2.Distance(puckPos, paddlePos);
            if (dist > sumR + 1e-4f) return;

            CustomVector2 delta = puckPos - paddlePos;
            if (CustomVector2.SqrMagnitude(delta) < DegenerateNormalSq)
                return;

            CustomVector2 n = CustomVector2.Normalize(delta);
            float penetration = sumR - dist;
            if (penetration > 0f)
                puckRoot.Position = puckPos + n * penetration;

            CustomVector2 vPuck = puckMove.CurrentVelocity;
            if (CustomVector2.Dot(vPuck, n) >= 0f) return;

            if (puckVelocityConsumedThisTick) return;

            float e = config.Bounciness;
            float f = config.f;
            CustomVector2 vRel = vPuck - paddleVelocity;
            CustomVector2 vReflect = CustomVector2.Reflect(vRel, n);
            CustomVector2 vNew = vReflect * e + paddleVelocity * f;
            vNew = ClampPuckSpeed(vNew, config);

            puckMove.SetVelocity(vNew);
            puckVelocityConsumedThisTick = true;
        }

        public static void ResolvePuckWall(
            Puck puck,
            Wall wall,
            BoardConfig config,
            ref bool puckVelocityConsumedThisTick)
        {
            var puckRoot = puck.GetComponent<Root2D>();
            var puckMove = puck.GetComponent<MoveComponent>();
            var puckCol = puck.GetComponent<CircleCollider>();
            var rect = wall.GetComponent<RectCollider>();

            CustomVector2 puckPos = puckRoot.Position;
            float rp = puckCol.Radius;
            CustomVector2 c = rect.Center;
            float hx = rect.Width * 0.5f;
            float hy = rect.Height * 0.5f;

            CustomVector2 q = ClosestPointOnRect(puckPos, c, hx, hy);
            float dist = CustomVector2.Distance(puckPos, q);
            if (dist > rp + 1e-4f) return;

            CustomVector2 toPuck = puckPos - q;
            CustomVector2 n;
            if (CustomVector2.SqrMagnitude(toPuck) < DegenerateNormalSq)
                n = PickFallbackWallNormal(puckPos, c, hx, hy);
            else
                n = CustomVector2.Normalize(toPuck);

            if (CustomVector2.SqrMagnitude(n) < DegenerateNormalSq)
                return;

            float penetration = rp - dist;
            if (penetration > 0f)
                puckRoot.Position = puckPos + n * penetration;

            CustomVector2 vPuck = puckMove.CurrentVelocity;
            if (CustomVector2.Dot(vPuck, n) >= 0f) return;

            if (puckVelocityConsumedThisTick) return;

            CustomVector2 vNew = CustomVector2.Reflect(vPuck, n) * config.Bounciness;
            vNew = ClampPuckSpeed(vNew, config);
            puckMove.SetVelocity(vNew);
            puckVelocityConsumedThisTick = true;
        }

        public static CustomVector2 ClosestPointOnRect(CustomVector2 p, CustomVector2 rectCenter, float halfW, float halfH)
        {
            float qx = Math.Clamp(p.x, rectCenter.x - halfW, rectCenter.x + halfW);
            float qy = Math.Clamp(p.y, rectCenter.y - halfH, rectCenter.y + halfH);
            return new CustomVector2(qx, qy);
        }

        static CustomVector2 PickFallbackWallNormal(CustomVector2 puckPos, CustomVector2 rectCenter, float hx, float hy)
        {
            float dx = puckPos.x - rectCenter.x;
            float dy = puckPos.y - rectCenter.y;
            float adx = Math.Abs(dx);
            float ady = Math.Abs(dy);

            if (adx * hy >= ady * hx)
                return dx >= 0f ? new CustomVector2(1f, 0f) : new CustomVector2(-1f, 0f);
            return dy >= 0f ? new CustomVector2(0f, 1f) : new CustomVector2(0f, -1f);
        }

        static CustomVector2 ClampPuckSpeed(CustomVector2 v, BoardConfig config)
        {
            float mag = CustomVector2.Magnitude(v);
            if (mag > config.MaxPuckSpeed && mag > 1e-6f)
                v = v * (config.MaxPuckSpeed / mag);
            mag = CustomVector2.Magnitude(v);
            if (config.MinPuckSpeed > 0f && mag < config.MinPuckSpeed && mag > 1e-6f)
                v = CustomVector2.Normalize(v) * config.MinPuckSpeed;
            return v;
        }
    }
}
