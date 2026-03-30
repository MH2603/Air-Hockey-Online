# Collision physics — implementation plan

This plan implements the behaviour described in [`CollisionPhysicDoc.md`](CollisionPhysicDoc.md): compute the **puck’s new velocity** after overlap with a **paddle** or **wall**, using pure C# (`CustomVector2`, `MoveComponent`, `BoardConfig`).

**Scope:** response only (velocity + optional positional separation). Detection stays on existing `CircleCollider` / `RectCollider` overlap.

**Code location:** apply changes in your shared assembly (e.g. `Assets/_MH/SharedLibrary/…` and the matching `Server Sln/Shared/…` tree if you keep two copies—keep them aligned).

---

## Phase 0 — Preconditions

**Chosen:** Option **2 — Input velocity** — paddles use `MoveComponent.SetVelocity` and integrate each tick; `Match.SetPaddleVelocity` replaces teleport-to-target. `GameRunner` drives player `0` with `(mouse − paddlePos) * BoardConfig.PaddlePositionFollow`, clamped by `PaddleMaxSpeed`.

- [x] Single source of tuning: `BoardConfig.Bounciness`, `f`, `MinPuckSpeed`, `MaxPuckSpeed`, `PaddleMaxSpeed`, `PaddlePositionFollow`
- [x] Paddle velocity from `MoveComponent` (option 2)
- [x] Tick order: paddles → puck (move + collision) → clamp paddle positions (see `Match.Tick` comment)

| Item | Action |
|------|--------|
| Single source of tuning | Confirm puck response reads `BoardConfig.Bounciness` (`e`) and `BoardConfig.f` (`f`). |
| Paddle velocity | `MovePaddle` currently sets **position** only. The doc needs `V_d` for paddle hits. Decide one approach (see below) before Phase 3. |
| Tick order | Define order in `Match.Tick`: e.g. update paddle velocity estimate → integrate puck (or integrate then resolve collisions once per frame). |

**Paddle velocity options (pick one):**

1. **Kinematic estimate:** Store last paddle position; each tick `V_d = (P_now - P_last) / deltaTime`, then update `P_last`.
2. **Input velocity:** If inputs are “stick direction × speed”, set `MoveComponent.SetVelocity` on the paddle and move the paddle with `MoveComponent` instead of teleport (larger change). **← implemented**
3. **Zero for MVP:** `V_d = 0`; paddle still deflects via normal only (no “carry” until you add velocity).

---

## Phase 1 — Math helpers

**Goal:** Shared, testable vector ops used by collision response.

| Step | Task |
|------|------|
| 1.1 | Add `Magnitude`, `Normalize` (epsilon guard), `Dot`, `Reflect` per [CollisionPhysicDoc.md § Vector helpers](CollisionPhysicDoc.md). |
| 1.2 | Place them either as `static` methods on `CustomVector2` or a small `Physics2DMath` / `CustomVector2Util` class in `MH.Core`. |
| 1.3 | Add `Clamp(float v, float min, float max)` locally or use `Math.Clamp` for AABB closest-point. |

- [x] 1.1–1.3 on `CustomVector2` (`SqrMagnitude`, `Magnitude`, `Normalize`, `Dot`, `Reflect`, `ClampMagnitude`; `Math.Clamp` in geometry)

**Exit criteria:** Helpers compile; `Reflect(V, n)` matches hand checks for horizontal/vertical `n`.

---

## Phase 2 — Geometric helpers for response

**Goal:** Normals and optional separation distances.

| Step | Task |
|------|------|
| 2.1 | **Circle–circle:** Implement `ClosestNormalPuckFromPaddle(CustomVector2 puckCenter, CustomVector2 paddleCenter)` → unit `n = Normalize(puck - paddle)` or `Zero` if degenerate. |
| 2.2 | **Circle–AABB:** Implement `ClosestPointOnRect(CustomVector2 p, CustomVector2 rectCenter, float halfW, float halfH)` using clamp on each axis (doc § Closest point on AABB). |
| 2.3 | **Penetration (optional):** Circle–circle: `d = r_p + r_d - Distance`; circle–AABB: `d = r_p - Distance(p, Q)` when overlapping. |

- [x] 2.1–2.3 inlined in `PuckCollisionResponse` (`ClosestPointOnRect` public; circle normal implicit; separation with penetration depth)

**Exit criteria:** For a known puck/wall layout, `Q` and `n` match expectations (e.g. puck left of vertical wall → `n` points right into play).

---

## Phase 3 — `PuckCollisionResponse` (single entry point)

**Goal:** One place that applies the doc’s formulas and writes `puck.GetComponent<MoveComponent>().SetVelocity(...)`.

| Step | Task |
|------|------|
| 3.1 | Add a static class e.g. `PuckCollisionResponse` in `MH.GameLogic` or `MH.Core` with `BoardConfig` + entity references passed in (avoid static global config if possible). |
| 3.2 | **`ResolvePuckPaddle(Puck puck, Paddle paddle, CustomVector2 paddleVelocity)`:** compute `n`, `V_rel = V_puck - V_paddle`, `V_new = Reflect(V_rel, n) * e + V_paddle * f`; handle degenerate `n` (no-op or skip). |
| 3.3 | **`ResolvePuckWall(Puck puck, Wall wall)`:** get `RectCollider` center/size, closest `Q`, `n`, `V_new = Reflect(V_puck, n) * e`. |
| 3.4 | **Separation (recommended):** After computing `n`, if overlap: move `Root2D.Position` of puck along `n` by penetration depth; then set velocity. |

- [x] 3.1–3.4 `GameLogic/PuckCollisionResponse.cs` (`ResolvePuckPaddle`, `ResolvePuckWall`; speed clamp via `MinPuckSpeed` / `MaxPuckSpeed`)

**Exit criteria:** Unit-testable static methods (or pure functions taking vectors + radii + config) produce the summary table in the doc.

---

## Phase 4 — Wire detection to response

**Goal:** When overlap fires, call Phase 3 once per hit (avoid double application same frame if possible).

| Step | Task |
|------|------|
| 4.1 | Subscribe to **`_puck.Collider.OnCollision`** (or equivalent) in `Match` after `InitPuck` / wall creation. |
| 4.2 | In handler, inspect **`CollisionInfo`**: identify other collider’s entity type (`Paddle` vs `GoalFrame` vs `Wall` — ignore unknown). |
| 4.3 | Register **walls** on puck: add each wall’s `RectCollider` to `_puck.Collider.TrackOthers` (mirror how paddles are tracked). |
| 4.4 | Invoke `ResolvePuckPaddle` or `ResolvePuckWall` with current `MoveComponent` velocities. |

**Collision spam mitigation (simple):**

- **Cooldown:** Store last resolve time or frame index; ignore puck–paddle for N ms after resolve.
- **Or** resolve only the **first** overlapping pair per tick in a defined priority (walls vs paddle).

- [x] 4.1–4.4 `Match.RegisterPuckAgainstWallsAndHandlers` + `HandlePuckCollision`
- [x] Mitigation: `ref bool puckVelocityConsumedThisTick` — at most **one** puck velocity bounce per `Match.Tick`; separation still runs when approaching along `n` (`Dot(v, n) < 0` gate for bounce)

**Exit criteria:** Simulating puck moving into a wall reverses the correct velocity component (with `Bounciness`). Puck–paddle with `V_d = 0` gives a symmetric bounce about `n`.

---

## Phase 5 — Simulation tick completeness

**Goal:** All dynamic bodies that need velocity participate in integration consistently.

| Step | Task |
|------|------|
| 5.1 | Today `Match.Tick` may only call `_puck.Tick`. If paddles should move by velocity, call `player.Paddle.Tick(deltaTime)` too; if paddles stay teleported, only update stored velocity estimate for collision (Phase 0). |
| 5.2 | Ensure **walls** are not tick-moved (static); only position at creation. |

- [x] 5.1 `Puck` / `Paddle` override `Tick`: `MoveComponent` then `CircleCollider`; `Match.Tick` runs all paddles then puck
- [x] 5.2 Walls unchanged (no `MoveComponent`)

**Exit criteria:** One clear `Tick` pipeline documented in a short comment on `Match`.

---

## Phase 6 — Optional polish (from doc)

| Step | Task |
|------|------|
| 6.1 | Add `MinPuckSpeed` / `MaxPuckSpeed` to `BoardConfig` and clamp after resolve. |
| 6.2 | Add hit logging or debug draw hooks only in development if useful. |

- [x] 6.1 `BoardConfig.MinPuckSpeed`, `MaxPuckSpeed` + `PuckCollisionResponse.ClampPuckSpeed`
- [ ] 6.2 (not implemented — add if needed)

---

## Testing checklist (manual or automated)

1. **Head-on wall:** Puck moves perpendicular into long wall; speed scales by ~`Bounciness`; direction flips along wall normal.  
2. **Glancing wall:** Oblique hit preserves tangential component magnitude (within float error).  
3. **Head-on paddle (stationary):** Puck reflects away from paddle centerline.  
4. **Overlapping frame:** With separation, puck does not remain embedded after resolve.  
5. **Degenerate centers:** Puck and paddle centers identical (rare); handler does not throw and does not set NaN velocity.

---

## Deliverables summary

| Artifact | Purpose |
|----------|---------|
| Math + geometry helpers | Reuse for response and tests |
| `PuckCollisionResponse` (or equivalent) | Implements [CollisionPhysicDoc.md](CollisionPhysicDoc.md) formulas |
| `Match` wiring + wall `TrackOthers` | connects detection → response |
| Paddle velocity strategy | Supplies `V_d` for `Reflect(V_p - V_d, n) + V_d * f` |

---

## Task order (recommended)

1. Phase 1 → Phase 2 (pure functions, easy to verify).  
2. Phase 3 (response using `BoardConfig`).  
3. Phase 0 decision + Phase 4 wiring.  
4. Phase 5 tick cleanup.  
5. Phase 6 as needed.

This keeps the system **simple**, matches the doc, and avoids Unity physics while staying ready for a small networked air hockey loop.

---

## Implementation notes (post-merge)

- **`BaseCollider.Type`** is set via `protected BaseCollider(Entity, CollisionType)` so **rectangle** dispatch uses `CollisionType.Rect` (fixes prior default-enum bug).
- **`MovePaddle` removed** — use **`Match.SetPaddleVelocity`**; sample client: `GameRunner` + `BoardConfig.PaddlePositionFollow`.
