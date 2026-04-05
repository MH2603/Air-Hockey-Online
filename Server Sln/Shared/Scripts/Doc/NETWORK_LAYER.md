# Network layer (current behavior)

This document describes how networking is structured **today**: transport (LiteNetLib), shared types in `MH.Network` / `MH.GameLogic`, and how the Unity client (`NetworkClient`) fits in.

## Transport: LiteNetLib

The project uses **[LiteNetLib](https://github.com/RevenantX/LiteNetLib)** on top of UDP. It provides connection handling, channels, and delivery modes (for example `ReliableOrdered`).

LiteNetLib **must be polled** regularly so connection events and incoming data are processed:

- **Server**: a background loop calls `PollEvents()` about every 15 ms.
- **Unity client**: `PollEvents()` is called every frame from `Bootstrap.Update()`.

## Shared layer (`Assets/_MH/SharedLibrary/Network`)

### `INetworkManager`

Defines a thin hook for the **receive pipeline**:

- `RegisterReceivedEvent(Action<int, NetPacketReader> callback)` — subscribe to incoming payloads.
- `UnregisterHandler(Action<int, NetPacketReader> callback)` — unsubscribe.

The `int` is the **peer id** (LiteNetLib’s id for the connection). The `NetPacketReader` is positioned at the start of the **application payload** for that message.

This interface lets game code stay agnostic of whether the implementation is a dedicated server `NetManager` or something else.

### `PacketDispatcher`

Bridges `INetworkManager` to **typed command handling**:

1. On receive, it reads the **first `int`** from the reader and treats it as the **command id** (`cmdType`).
2. It looks up a **factory** for that id (registered per command) and builds an `INetPacket` by calling `Deserialize` on the remaining bytes.
3. It invokes every `IPacketHandler` registered for that `cmdType`.

**Registration**: `RegisterHandler<TPacket>(int cmdType, IPacketHandler handler)` — registers both the handler list and the deserializer for `TPacket`. The dispatcher assumes the **first int in the buffer is always `cmdType`**; the packet’s `Deserialize` only reads the **rest** of the fields (see wire format below).

**Lifecycle**: Construct with an `INetworkManager` (which auto-subscribes `HandleReceived`). `Dispose()` unsubscribes and clears maps.

### `INetPacket` and `IPacketHandler`

- **`INetPacket`** extends LiteNetLib’s `INetSerializable` — packets implement `Serialize` / `Deserialize` with `NetDataWriter` / `NetDataReader`.
- **`IPacketHandler`**: `HandlePacket(int fromId, int packType, INetPacket packet)` — `packType` matches the leading `int` on the wire.

### `Session`

Currently an **empty placeholder** (`Session.cs`) — no session logic is wired yet.

## Wire format (application messages)

For commands that go through `PacketDispatcher`, the **first 4 bytes are always a little-endian `int` command id** (for example `EClientCmd` values).

Example: `c2s_mouse_pos` in `SharedLibrary/GameLogic/ClientCmd.cs`:

- **Serialize** writes: `cmd` (`int`), then `X` (`float`), then `Y` (`float`).
- **Deserialize** reads only: `X`, `Y` — because `PacketDispatcher` has already consumed the leading `int` to choose the packet type.

**Important**: `Serialize` includes the command id so **outbound** messages are self-describing for the peer’s dispatcher. The server’s dispatcher **re-reads** that same leading int inside `HandleReceived` (the reader is still at position 0 for the full payload).

## Server (`Server Sln`) — reference implementation

The headless server implements the shared pattern end-to-end:

- **`NetworkManager`** implements `INetworkManager`: starts LiteNetLib on port **9050**, accepts connections with key **`SomeConnectionKey`**, maps peers by id, and forwards `NetworkReceiveEvent` to `OnReceived` (then recycles the reader).
- **`Program.cs`** creates `NetworkManager`, then **`PacketDispatcher(networkListener)`**, then registers handlers (e.g. `TestPacketHandler`).

So on the server, **typed dispatch is live** for registered `EClientCmd` values.

## Unity client (`Assets/_MH/Scripts/NetworkClient.cs`)

The client is a **thin LiteNetLib client** wrapper:

| Piece | Behavior |
|--------|----------|
| **Init** | Creates `EventBasedNetListener` + `NetManager`, starts the client, connects to **`localhost:9050`** with key **`SomeConnectionKey`**. |
| **Receive** | `NetworkReceiveEvent` currently **logs a string** (max length 100) — this matches the server’s initial test send (`"Hello client!"`), not the binary `PacketDispatcher` format. |
| **PollEvents** | Must be called every frame (documented on the method; used from `Bootstrap`). |
| **Send&lt;TPacket&gt;** | Requires `INetPacket`, serializes with `packet.Serialize(_writer)`, sends on **`FirstPeer`** with **`DeliveryMethod.ReliableOrdered`**. Guards if not connected. |

So **sending** uses the same `INetPacket` types as the server (e.g. `c2s_mouse_pos` from `Bootstrap` on click). **Receiving** on the client is **not** yet routed through `PacketDispatcher` / `INetworkManager` — it is still demo logging unless you replace that handler.

## Summary

| Layer | Role |
|--------|------|
| LiteNetLib | UDP + connections + poll-driven I/O |
| `INetworkManager` | Inject receive callbacks with `(peerId, reader)` |
| `PacketDispatcher` | `int` command id → deserialize `INetPacket` → `IPacketHandler`s |
| `NetworkClient` | Connect, poll, send typed packets; receive path is still ad hoc |

To mirror the server’s dispatch on the client, you would implement `INetworkManager` (or adapt the listener) and construct a `PacketDispatcher` the same way, then replace the string `Debug.Log` receive handler with that pipeline.
