# Implementation plan: Player got score (goal)

This document plans **goal detection**, **authoritative scoring**, **network notification**, and **client UX** (input freeze, score UI, delayed puck reset) for air hockey.

## Context (current codebase)

- **`GoalFrame`** (`Server Sln/Shared/Scripts/GameLogic/GoalFrame.cs`): entity with `Root2D` + square `RectCollider` sized by `BoardConfig.GoalWidth`. Each `HockeyPlayer` owns one; positions are set in `Match.SetInitialObjectPositions`.
- **`PuckCollisionResponse`** (`Air Hockey Online_Unity/Assets/_MH/SharedLibrary/GameLogic/PuckCollisionResponse.cs`): resolves **puck vs paddle** (circle–circle) and **puck vs wall** (circle–AABB). It does **not** yet define goal behavior.
- **`Match`** (`Server Sln/Shared/Scripts/GameLogic/Match.cs`): registers puck collisions with **paddles** and **walls** only. `HandlePuckCollision` has no `GoalFrame` branch, so goals are not detected today.
- **Networking** ([`MATCH_SYNC_ARCHITECTURE.md`](../../../../../Server%20Sln/Shared/Scripts/Doc/MATCH_SYNC_ARCHITECTURE.md)): authoritative simulation on **server / listen host**; clients receive `s2c_board_status` each tick. Scoring is noted as a follow-up (extend status or add events).

## Product flow (requirement)

1. **Detect goal** when the puck overlaps the defending player’s `GoalFrame` (authoritative sim only).
2. **Award a point** to the opponent of the player who conceded (bottom goal → player `1` scores; top goal → player `0` scores — align with existing `playerIdBottom` / `playerIdTop` = 0 / 1).
3. **Notify all clients** (equivalent to “host sends cmd” in listen-server setups: the **authoritative** side broadcasts; remote peers receive packets; local host consumes the same event via session callback if not sent over loopback).
4. **Freeze input** for both players during the short reset beat.
5. **Update score UI** immediately when the event is handled.
6. **After ~1 s**, place the **puck beside the losing player’s** side (clear offset from paddle/goal), **zero puck velocity**, then **resume** normal play and input.

## Design notes

- **Goal vs wall**: Goals should **not** reuse `ResolvePuckWall` (bounce). Treat goal overlap as a **trigger**: increment score once, enter a **post-goal phase**, then reset puck.
- **Double firing**: `CircleCollider` vs `RectCollider` can report overlap every tick while the puck sits in the goal. Add a **guard** (e.g. `Match` state `Idle | Playing | PostGoal`, or “goal consumed until puck reset”) so only **one** score event is emitted per goal.
- **Simulation during PostGoal**: Option A — **skip** `Match.Tick` physics for puck/paddles except optional idle clamp; Option B — **freeze** positions/velocities in sim. Pick one and keep **snapshots consistent** with what clients render.
- **Protocol**: Prefer a dedicated **`s2c_goal_scored`** (or similar) with `MatchId`, `ScoringPlayerIndex`, `ConcedingPlayerIndex`, `Score0`, `Score1`, and optionally `ResumeTick` / `ResetDurationMs` so UI and reset timing stay in sync. Alternatively, extend `s2c_board_status` with scores + a `MatchPhase` enum; document versioning if older clients exist.
- **`PuckCollisionResponse`**: Only add a helper here if goal needs **geometric** reuse (e.g. shared circle–rect test). Otherwise, goal logic can live entirely in `Match` / a small `GoalScoring` helper to avoid mixing “bounce” and “score” semantics in the same API.
- **Config**: Expose **reset delay** (default 1 s) and **puck spawn offset** on `BoardConfig` or match config so tuning does not require code changes.

## Tasks

### Shared game logic (authoritative `Match`)

- [x] Register both players’ `GoalFrame` colliders on the puck’s `TrackOthers` (same pattern as walls/paddles in `InitPuck` / `RegisterPuckAgainstWallsAndHandlers`).
- [x] In `HandlePuckCollision`, add a `case GoalFrame` (or resolve owner via player map) that **does not** call wall bounce code.
- [x] Define **conceding player** from which `GoalFrame` was hit; set **scoring player** to the other id; increment per-match scores (new fields on `Match` or small `MatchScore` struct).
- [x] Implement **post-goal phase**: ignore new goal triggers until reset complete; stop or bypass normal puck integration for the delay window as per chosen option above.
- [x] Implement **puck reset**: after delay, set puck position to “beside losing player” (sign of Y and X offset from `BoardConfig`), clear velocity; exit post-goal phase.
- [ ] Add unit-style tests if the repo has a test harness for `Match.Tick` / collisions; otherwise add a minimal deterministic test for “one goal → one score” and “no double score while overlapping”.

### Network (shared `ServerCmd` / dispatcher)

- [x] Add `EServerCmd` value and `INetPacket` struct for goal/score notification (fields: match id, scorer, scores, optional timing).
- [x] Serialize/deserialize in the same file pattern as `s2c_board_status`.
- [x] In **`MatchSessionManager.TickAndBroadcast`**: when the authoritative `Match` signals a goal this tick, **broadcast** the new packet to both peers (and invoke a **local host callback** for `HostLocalPeerId` so the listen host updates UI without a self-send).
- [x] Optionally include **updated scores** in every subsequent `s2c_board_status` so late join / packet loss can recover; if so, extend `s2c_board_status` fields and Unity reconcile path.

### Unity client — `GameRunner` / session

- [x] Register handler for the new server command; unregister on teardown (mirror `BoardStatus` / `MatchResult`).
- [x] On receive: **freeze gameplay input** (skip `ApplyPaddleTargetFromWorld` / stop sending `c2s_mouse_pos` for both sides, or gate in one place).
- [x] Drive **post-goal timer** (~1 s) — either from packet timestamps, fixed delay from config, or explicit “resume at” from server; **stay deterministic** with server if possible.
- [x] After timer: if server is authoritative for puck pose, **apply** the next `s2c_board_status` reflecting reset; if client mirrors local `Match`, call the same reset API the server uses.
- [x] Listen-server path: wire **host-local** event from `MatchSessionManager` into the same handler as remote packets so behavior is identical.

### Unity UI

- [x] Extend **score HUD** (see `UI_SYSTEM_ARCHITECTURE.md` / `ScoreHud`) to show two integers; update on goal event and optionally on each snapshot if scores are replicated there.
- [ ] Optional: short **feedback** (flash, sound) on goal — keep presentation out of `Match` logic.

### Docs & parity

- [x] Update [`MATCH_SYNC_ARCHITECTURE.md`](../../../../../Server%20Sln/Shared/Scripts/Doc/MATCH_SYNC_ARCHITECTURE.md) with the new packet and post-goal lifecycle diagram.
- [x] If Unity embeds duplicate `GoalFrame` / `BoardConfig` under `SharedLibrary`, ensure **file parity** with `Server Sln/Shared` or document single source of truth.

## Acceptance criteria

- [x] Scoring only on **server / authoritative** simulation; clients cannot inflate scores by local collision.
- [x] **Exactly one** point per puck entry into a goal until puck is reset.
- [x] **Input frozen** for ~1 s; **UI** shows new score for both players.
- [x] Puck **respawns** beside the **losing** player with **zero** velocity; match then continues normally.
- [x] Guest **prediction** (`GuestPredictionService`) does not fight the reset: reconcile or pause prediction during post-goal if needed.

## Open questions (resolve during implementation)

- [ ] Win condition (max score / sudden death) — out of scope for this feature or part of `s2c_match_result`?
- [ ] Should **paddles** be moved to default half positions on reset, or only the puck?
- [ ] Exact **spawn offset** from board units (reference art / `Hockey_Ref.jpg`).
