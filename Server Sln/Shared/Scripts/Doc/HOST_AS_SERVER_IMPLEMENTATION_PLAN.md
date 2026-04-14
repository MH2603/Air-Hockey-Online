# Host-as-server implementation plan

This document describes how to move from the **dedicated headless server + Unity clients** model to **one player’s Unity app acting as the authoritative host**, without running a separate server program. For transport and `PacketDispatcher` details, see [NETWORK_LAYER.md](NETWORK_LAYER.md). For LAN discovery, see [LAN_HOST_DISCOVERY.md](LAN_HOST_DISCOVERY.md).

## Terminology

The target is a **listen-server** (host-authoritative) design, not symmetric “pure” P2P:

- **Host:** Unity app binds **UDP 9050**, runs authoritative `Match` simulation, sends `s2c_*` to remote peers.
- **Client (guest):** Unity app connects to the host’s address; same thin client behavior as today (input up, state down).

The host device **does not** open a second LiteNetLib client connection to itself; the local player is not a `NetPeer`.

## Current split (what moves)

| Piece today | Location | After migration |
|-------------|----------|-----------------|
| Listen **9050**, `SomeConnectionKey`, peer map, send/receive, LAN `AHO_DISCOVER` | `Server Sln/Server/Network/NetworkManager.cs` | **Host path inside Unity** (main-thread `PollEvents`, no background poll thread) |
| `PacketDispatcher`, `MatchmakingHandler`, `MatchSessionManager`, **60 Hz** sim | `Server Sln/Server/Program.cs` + server game logic | Same types; constructed when user chooses **Host**; tick from **`FixedUpdate`** (or dedicated host runner) |
| Client connect, `c2s_find_match` / `c2s_mouse_pos`, apply `s2c_*` | Unity `ClientNetwork.cs`, `GameRunner.cs` | Unchanged for **guests**; **host** uses **local authoritative input** (see below) |

## Design decisions

### 1. Host is not a connected peer

Reserve a **player slot** (e.g. bottom = player 0) for the host. Feed host mouse/input **directly** into the same logic as `MatchSessionManager` uses for `c2s_mouse_pos`, without serializing `c2s_mouse_pos` over the network for the local player.

### 2. Matchmaking when hosting

Today two remote clients each send `c2s_find_match` and are paired (`MatchmakingHandler.cs`).

For host mode:

- On **Create Host**, enter a **waiting for guest** state (host slot reserved).
- On the first remote **`c2s_find_match`**, pair **host (local) + remote peer**: send `s2c_match_found` with correct `LocalPlayerIndex` to the guest, and start the match for the host UI locally.
- Policy for a second guest while a match is active: reject, queue next match, or show “full” (product choice).

### 3. Board state on the host

Recommended: after each authoritative `Match.Tick`, **apply state to the host’s match/view from memory** (same `Match` instance you tick). **Serialize and send** `s2c_board_status` **only to remote** `NetPeer`s. Avoid loopback packets for the host.

### 4. Polling and ports

Only the **host** process binds **9050**. Guests use ephemeral client ports. If LAN discovery is merged into one `NetManager` later, ensure a **single** `PollEvents()` owner on the main thread.

## Phased implementation

### Phase 1 — Host transport in Unity — **done**

- [x] Add **`UnityHostNetwork`**: LiteNetLib server `Start(9050)`, `UnconnectedMessagesEnabled = true`, same LAN strings as headless server.
- [x] Extend **`INetworkManager`** (`SendPacket`, `OnClientDisconnected`); **`PollEvents()`** from **`GameRunner.Update`**.
- [x] **`SendPacket`** skips **`NetworkConstants.HostLocalPeerId`**.

### Phase 2 — Host game loop — **done**

- [x] **`HostGameSession`**: `PacketDispatcher`, **`MatchmakingHandler`**, **`MatchSessionManager`**, **`BoardConfig`** (shared scripts via `SharedLibrary` symlink).
- [x] **`MatchSessionManager.TickAndBroadcast`** from **`GameRunner.FixedUpdate`** (60 Hz via **`Time.fixedDeltaTime`**).

### Phase 3 — Host-aware matchmaking — **done**

- [x] **`MatchmakingHandler.BeginHosting` / `CancelHosting`**: first remote **`c2s_find_match`** pairs with host (bottom); **`CreateMatch(HostLocalPeerId, remotePeer)`**.
- [x] Match simulation + **`MatchSessionManager`** moved to **Shared**; headless server uses **`INetworkManager`**.

### Phase 4 — `GameRunner` dual role — **done**

- [x] Guest: unchanged **`ClientNetwork`** + **`c2s_mouse_pos`**; **`ApplyBoardStatus`** ignored when hosting.
- [x] Host: **`ApplyHostBottomPaddleTarget`**; shared authoritative **`Match`** for **`MatchView2D`**; **`EGameState.WaitingForGuest`**.

### Phase 5 — UI (`UILobby`) — **done**

- [x] **`Create Host`** button (**assign in Inspector** on lobby prefab).
- [x] Waiting state: title + disable Find/Create; **Back** calls **`StopHosting`**.
- [x] **`Bootstrap`**: unchanged (no auto-connect for host).

### Phase 6 — Lifecycle and errors — **done**

- [x] **`OnLocalHostMatchResult`** for host win/lose UI; remote disconnect uses existing result path.
- [x] **`TryListen`** failure → **`UINotifyPopup`** (port in use).

### Phase 7 — Documentation — **done**

- [x] [NETWORK_LAYER.md](NETWORK_LAYER.md) updated (Unity host section).
- [ ] Optional: retire or demote headless **Server** exe once you rely only on Unity host (project-specific).

## Risks and checks

- **Single source of truth** for `Match`, `BoardConfig`, and packet types between Unity and server projects; avoid duplicate definitions drifting apart.
- **Threading:** all LiteNetLib receive and `PacketDispatcher` handling on the **main thread** in Unity.
- **`GameRunner` / `EGameState`:** add states such as **Hosting** / **WaitingForGuest** so connect and matchmaking do not overlap incorrectly.

## Suggested implementation order

1. [x] Unity host `NetManager` + LAN unconnected replies (verify with **Find Host** from another build/device).
2. [x] `PacketDispatcher` + `MatchSessionManager.TickAndBroadcast` on host.
3. [x] Host matchmaking + first guest end-to-end.
4. [x] `GameRunner` host local input + match view.
5. [x] `UILobby` Create Host + waiting/back behavior.

## Related files (reference)

| Area | Path |
|------|------|
| Network overview | `Server Sln/Shared/Scripts/Doc/NETWORK_LAYER.md` |
| LAN discovery | `Server Sln/Shared/Scripts/Doc/LAN_HOST_DISCOVERY.md` |
| Headless listen + LAN | `Server Sln/Server/Network/NetworkManager.cs` |
| Server composition | `Server Sln/Server/Program.cs` |
| Matchmaking / sim | `Server Sln/Server/GameLogic/MatchmakingHandler.cs`, `MatchSessionManager.cs` |
| Unity client | `Air Hockey Online_Unity/Assets/_MH/Scripts/Network/ClientNetwork.cs` |
| Unity gameplay + packets | `Air Hockey Online_Unity/Assets/_MH/Scripts/GameRunner.cs` |
| Lobby UI | `Air Hockey Online_Unity/Assets/_MH/Scripts/UI/Windows/UILobby.cs` |
| Bootstrap | `Air Hockey Online_Unity/Assets/_MH/Scripts/Boostrap.cs` |
