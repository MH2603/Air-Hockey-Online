# Collision physics (server / shared logic)

Pure **C#** notes for updating the **puck’s velocity** after hitting a **paddle** (circle) or **wall** (axis-aligned rectangle). This matches your stack: `Puck`, `Paddle`, and `Wall` in `MH.GameLogic`, motion via `MoveComponent`, positions via `Root2D`, and tuning values in `BoardConfig`. No Unity physics components are required.

## Design goals (keep it small)

- One clear formula for **puck vs paddle** (moving circle vs circle).
- One clear formula for **puck vs wall** (circle vs axis-aligned box).
- Use **`BoardConfig.Bounciness`** as elasticity \(e\) and **`BoardConfig.f`** as paddle influence (how much the paddle’s velocity is mixed in).
- Optional clamps (`minSpeed`, `maxSpeed`) avoid stuck or tunneling-like behaviour; add later if needed.

We **do not** model mass-based impulses, friction, or spin unless you extend the design.

## Data you already have

| Piece | Role |
|--------|------|
| `MoveComponent.CurrentVelocity` / `SetVelocity` | Puck and paddle linear velocity. |
| `Root2D.Position` | Center of puck, paddle, or wall collider. |
| `CircleCollider.Center`, `Radius` | Circle geometry (puck & paddle). |
| `RectCollider`: `Center`, `Width`, `Height` | Wall as an AABB. |
| `BoardConfig` | `Bounciness` ≈ \(e\), `f` ≈ paddle push factor. |

Collision **detection** is already represented by `CircleCollider` / `RectCollider` overlap tests and `OnCollision` / `TrackOthers` in shared core; this document is about the **response**: computing the new puck velocity and calling `puckMove.SetVelocity(...)`.

## Vector helpers (`CustomVector2`)

`CustomVector2` provides arithmetic and `Distance`. For reflections you need **length**, **normalize**, **dot**, and **reflect**. Minimal implementations (same namespace as your math type or a small static helper class):

```csharp
static float Magnitude(CustomVector2 v)
{
    return (float)System.Math.Sqrt(v.x * v.x + v.y * v.y);
}

static CustomVector2 Normalize(CustomVector2 v)
{
    float m = Magnitude(v);
    if (m < 1e-6f) return CustomVector2.Zero;
    return v / m;
}

static float Dot(CustomVector2 a, CustomVector2 b) => a.x * b.x + a.y * b.y;

// Reflect direction `dir` off a surface whose unit normal is `n` (same convention as typical game math).
static CustomVector2 Reflect(CustomVector2 dir, CustomVector2 n)
{
    return dir - 2f * Dot(dir, n) * n;
}
```

## Puck vs paddle (circle vs circle)

**Idea:** Treat the collision normal as the direction from the **paddle center to the puck center** (stable for two circles). Work in the **relative** velocity frame so a moving paddle correctly affects the bounce.

1. Positions: \(P_p\), \(P_d\) (puck, paddle).
2. Normal (unit):  
   \[
   n = \mathrm{normalize}(P_p - P_d)
   \]
   If \(\|P_p - P_d\|\) is ~0, skip or use a fallback axis (e.g. last frame’s \(n\)).
3. Velocities: \(V_p\) (puck), \(V_d\) (paddle).
4. Relative velocity:  
   \[
   V_{rel} = V_p - V_d
   \]
5. Reflect the relative velocity:  
   \[
   V_{reflect} = \mathrm{Reflect}(V_{rel}, n)
   \]
6. Combine bounce and paddle carry (tunables from `BoardConfig`):  
   \[
   V_{p,\mathrm{new}} = V_{reflect} \cdot e + V_d \cdot f
   \]
   with \(e =\) `Bounciness`, \(f =\) `BoardConfig.f`.

Then `puckMove.SetVelocity(V_p_new)`.

**Intuition**

- Stationary paddle (\(V_d = 0\)): \(V_{p,\mathrm{new}} \approx e \cdot \mathrm{Reflect}(V_p, n)\) — classic bounce, scaled by elasticity.
- Paddle moving into the puck: the \(V_d \cdot f\) term adds “carry”, so hits feel like air hockey.

## Puck vs wall (circle vs axis-aligned rectangle)

**Idea:** Walls use `RectCollider` (AABB). From the **puck center**, find the **closest point** on that rectangle. The collision normal points from that closest point **toward the puck** (outward from the wall into the playfield), which is what you plug into `Reflect` for a simple elastic bounce.

### Closest point on AABB

Half-extents: \(h_x = \mathrm{width}/2\), \(h_y = \mathrm{height}/2\). Center \(C_w\). Puck center \(C_p\).

\[
q_x = \mathrm{clamp}(C_{p,x},\, C_{w,x} - h_x,\, C_{w,x} + h_x)
\]
\[
q_y = \mathrm{clamp}(C_{p,y},\, C_{w,y} - h_y,\, C_{w,y} + h_y)
\]

Closest point \(Q = (q_x, q_y)\).

### Normal and new velocity

\[
n = \mathrm{normalize}(C_p - Q)
\]

If \(n\) is degenerate (puck center exactly on an edge/corner interior), pick a unit axis from the rectangle edge you know was hit, or skip for one frame.

For a wall that does not move:

\[
V_{p,\mathrm{new}} = \mathrm{Reflect}(V_p,\, n) \cdot e
\]

Use the same \(e =\) `Bounciness`. There is no paddle term for static walls.

## Overlap / tunneling (optional but useful)

Your `MoveComponent` integrates position as \(x \leftarrow x + v\,\Delta t\). If overlaps are detected **after** integration, the puck can sit slightly inside a paddle or wall for several ticks.

A simple mitigation (still lightweight):

1. On collision, compute \(n\) as above.
2. Push the puck center along \(n\) so circle–circle or circle–AABB is just touching (separation along \(n\) by penetration depth).

Exact penetration depth:

- **Circle–circle:** \(d = r_p + r_d - \|C_p - C_d\|\), move puck by \(+n \cdot d\) (with \(n\) from puck away from paddle, i.e. `Normalize(C_p - C_d)`).
- **Circle–AABB:** \(d = r_p - \|C_p - Q\|\) if overlapping (distance from puck to closest point \(Q\) on rect).

Then apply the velocity formula. Order: **separate, then set velocity**.

## Optional speed clamps

For feel and stability (not in `BoardConfig` today; add fields if you want them):

- **`maxSpeed`**: if `Magnitude(V_new) > max`, scale to `max`.
- **`minSpeed`**: if `Magnitude(V_new) < min` but the puck should keep moving, set direction to `Normalize(V_new)` or fallback to `n`, times `min`.

This avoids “dead” pucks after grazing collisions.

## Integration checklist

1. When your colliders signal overlap (**puck vs paddle** or **puck vs wall**), resolve **puck only** (paddle and wall velocities are inputs).
2. Read/write **`MoveComponent`** on the puck (and paddle velocity for paddle hits).
3. Use **`BoardConfig.Bounciness`** and **`BoardConfig.f`** so tuning stays in one place.
4. Walls are **`Wall` + `RectCollider`**; paddles and puck are **`CircleCollider`** with radii from `BoardConfig` (`PuckRadius`, `PaddleRadius`).

## Summary

| Pair | New puck velocity (conceptually) |
|------|-----------------------------------|
| Puck–paddle | \(\mathrm{Reflect}(V_p - V_d,\, n)\cdot e + V_d\cdot f\) with \(n = \mathrm{normalize}(P_p - P_d)\). |
| Puck–wall | \(\mathrm{Reflect}(V_p,\, n)\cdot e\) with \(n = \mathrm{normalize}(P_p - Q)\), \(Q\) closest point on wall AABB. |

\(e =\) `Bounciness`, \(f =\) `BoardConfig.f`. Implement helpers on `CustomVector2` or a small static `Physics2DUtil` in shared C# — no Unity types required.
