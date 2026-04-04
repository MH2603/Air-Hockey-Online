using MH.Core;
using System;

namespace MH.GameLogic
{
    /// <summary>
    /// Puck vs paddle (circle–circle) and puck vs wall (circle–AABB) separation + velocity (see SharedLibrary Doc).
    /// </summary>
    public static class PuckCollisionResponse
    {
        // Small epsilons: degenerate geometry and “separating” velocity gate.
        const float DegenerateNormalSq = 1e-10f;
        /// <summary> If puck speed is below this, do not treat Dot(v_rel,n) &gt;= 0 as "separating" — stationary puck must still get paddle impulse. </summary>
        const float MinPuckVelSqForSeparatingCheck = 1e-12f;

        public static void ResolvePuckPaddle(
            Puck puck,
            Paddle paddle,
            BoardConfig config,
            CustomVector2 paddleVelocity,
            ref bool puckVelocityConsumedThisTick)
        {
            // Resolve entity components used for position, velocity, and radii.
            var puckRoot = puck.GetComponent<Root2D>();
            var paddleRoot = paddle.GetComponent<Root2D>();
            var puckMove = puck.GetComponent<MoveComponent>();
            var puckCol = puck.GetComponent<CircleCollider>();
            var paddleCol = paddle.GetComponent<CircleCollider>();

            // Circle–circle overlap test (early out if separated).
            CustomVector2 puckPos = puckRoot.Position;
            CustomVector2 paddlePos = paddleRoot.Position;
            float rp = puckCol.Radius;
            float rd = paddleCol.Radius;
            float sumR = rp + rd;
            float dist = CustomVector2.Distance(puckPos, paddlePos);
            if (dist > sumR + 1e-4f) return;

            // Contact normal paddle → puck; bail if centers coincide.
            CustomVector2 delta = puckPos - paddlePos;
            if (CustomVector2.SqrMagnitude(delta) < DegenerateNormalSq)
                return;

            // Push puck out along n to resolve penetration.
            CustomVector2 n = CustomVector2.Normalize(delta);
            float penetration = sumR - dist;
            if (penetration > 0f)
                puckRoot.Position = puckPos + n * penetration;

            // Skip if puck and paddle separate along n in the contact frame (relative velocity).
            // |vPuck| gate: dot(v_rel,n)==0 for a resting puck would otherwise skip; stationary still gets paddle hit.
            CustomVector2 vPuck = puckMove.CurrentVelocity;
            CustomVector2 vRel = vPuck - paddleVelocity;
            if (CustomVector2.SqrMagnitude(vPuck) > MinPuckVelSqForSeparatingCheck &&
                CustomVector2.Dot(vRel, n) >= 0f)
                return;

            // Match rule: at most one puck velocity change from bounce logic per tick.
            if (puckVelocityConsumedThisTick) return;

            // Reflect relative velocity, blend paddle influence, clamp speed, apply.
            float e = config.Bounciness;
            float f = config.f;
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
            // Resolve puck and wall (AABB) colliders.
            var puckRoot = puck.GetComponent<Root2D>();
            var puckMove = puck.GetComponent<MoveComponent>();
            var puckCol = puck.GetComponent<CircleCollider>();
            var rect = wall.GetComponent<RectCollider>();

            // Circle vs axis-aligned rectangle: closest point on rect, distance to puck center.
            CustomVector2 puckPos = puckRoot.Position;
            float rp = puckCol.Radius;
            CustomVector2 c = rect.Center;
            float hx = rect.Width * 0.5f;
            float hy = rect.Height * 0.5f;

            CustomVector2 q = ClosestPointOnRect(puckPos, c, hx, hy);
            float dist = CustomVector2.Distance(puckPos, q);
            if (dist > rp + 1e-4f) return;

            // Outward normal from wall toward puck (fallback if puck center projects inside edge).
            CustomVector2 toPuck = puckPos - q;
            CustomVector2 n;
            if (CustomVector2.SqrMagnitude(toPuck) < DegenerateNormalSq)
                n = PickFallbackWallNormal(puckPos, c, hx, hy);
            else
                n = CustomVector2.Normalize(toPuck);

            // Could not build a valid outward normal — skip this contact.
            if (CustomVector2.SqrMagnitude(n) < DegenerateNormalSq)
                return;

            // Separate puck from wall along n.
            float penetration = rp - dist;
            if (penetration > 0f)
                puckRoot.Position = puckPos + n * penetration;

            // Bounce only when moving into the wall along n.
            CustomVector2 vPuck = puckMove.CurrentVelocity;
            if (CustomVector2.Dot(vPuck, n) >= 0f) return;

            // Match rule: at most one puck velocity change from bounce logic per tick.
            if (puckVelocityConsumedThisTick) return;

            // Reflect puck velocity with bounciness, clamp, apply.
            CustomVector2 vNew = CustomVector2.Reflect(vPuck, n) * config.Bounciness;
            vNew = ClampPuckSpeed(vNew, config);
            puckMove.SetVelocity(vNew);
            puckVelocityConsumedThisTick = true;
        }

        public static CustomVector2 ClosestPointOnRect(CustomVector2 p, CustomVector2 rectCenter, float halfW, float halfH)
        {
            // Closest point on AABB to p (same as clamping p to the rectangle).
            float qx = Math.Clamp(p.x, rectCenter.x - halfW, rectCenter.x + halfW);
            float qy = Math.Clamp(p.y, rectCenter.y - halfH, rectCenter.y + halfH);
            return new CustomVector2(qx, qy);
        }

        static CustomVector2 PickFallbackWallNormal(CustomVector2 puckPos, CustomVector2 rectCenter, float hx, float hy)
        {
            // When puck sits on a flat face, pick left/right vs top/bottom by dominant axis offset.
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
            // Cap max speed, then optionally bump up sub-min speeds (keeps direction).
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
