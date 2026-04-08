# LAN host discovery (architecture)

This document describes how **local network host discovery** works in this project: goals, wire protocol, server behavior, Unity client behavior, and how it relates to the main game connection on port **9050**.

For general transport and `PacketDispatcher` details, see [NETWORK_LAYER.md](NETWORK_LAYER.md).

## Goals

- Let a player open a **lobby UI**, **scan the LAN** for game servers, and **pick a host** before starting matchmaking.
- Use the **same UDP port as the game server** (**9050**) so discovery does not require opening a second firewall port for the headless server.
- Keep discovery traffic **separate** from connected game packets: discovery uses LiteNetLib **unconnected** messages, not the `PacketDispatcher` command stream.

## Transport choice: LiteNetLib unconnected (port 9050)

Discovery is implemented with **LiteNetLib** primitives:

- **`UnconnectedMessagesEnabled = true`** on the `NetManager` that binds **9050** (server) or a client-side manager used only for discovery.
- **Send**: `NetManager.SendUnconnectedMessage(NetDataWriter, IPEndPoint)` — payload is built with `NetDataWriter.Put(string)` so the peer reads it with `NetPacketReader.GetString()`.
- **Receive**: `NetworkReceiveUnconnectedEvent` — handler must **`reader.Recycle()`** in a `finally` block (same discipline as connected `NetworkReceiveEvent`).

**Why not a second raw UDP socket on another port?** A separate `UdpClient` on e.g. **9051** is simpler to reason about in isolation, but it adds **another listening port** on the server and another protocol surface. Unconnected messages on **9050** reuse the server’s existing LiteNetLib socket.

**Client-side sockets:** The Unity **game client** uses `ClientNetwork` for the **connected** session. The current implementation also uses a dedicated **`LanHostDiscovery`** instance with its **own** `NetManager` for discovery (second local UDP socket, ephemeral bind). An alternative is to **fold** unconnected discovery into `ClientNetwork` so **one** `NetManager` and **one** `PollEvents()` path handle both discovery and connect; the protocol and server behavior below are unchanged.

## Wire protocol (application strings)

All payloads are a **single length-prefixed UTF-8 string** (LiteNetLib `Put` / `GetString`).

| Direction | Payload | Meaning |
|-----------|---------|---------|
| Client → LAN broadcast | `AHO_DISCOVER` | “Any game server here?” |
| Server → sender | `AHO_HOST\|<port>\|<name>` | “I am a server.” |

- **`<port>`** is the **game listen port** (today **9050**). Clients use it for `ClientNetwork.SetConnectionTarget` + `StartConnect`.
- **`<name>`** is a display hint (today the server machine name, e.g. `Environment.MachineName` on the headless server).

Constants are duplicated in server and Unity code; **keep them in sync** if you change the protocol.

### Server constants (reference)

- Query: `AHO_DISCOVER`
- Response prefix: `AHO_HOST`
- Listen port: **9050** (`ListenPort` / `GamePort`)

## Server (`Server Sln/Server/Network/NetworkManager.cs`)

1. **`Init()`** creates `NetManager` with **`UnconnectedMessagesEnabled = true`** and **`Start(ListenPort)`** on **9050**.
2. Subscribes **`NetworkReceiveUnconnectedEvent`** → **`HandleLanDiscoveryUnconnected`**.
3. On unconnected receive:
   - Read **`GetString()`**; if it equals **`AHO_DISCOVER`**, build  
     `AHO_HOST|9050|<MachineName>`, **`Put`** into **`NetDataWriter`**, **`SendUnconnectedMessage`** back to **`remoteEndPoint`** (the discoverer’s address).
   - **`reader.Recycle()`** in `finally`.
4. Normal **connected** traffic is unchanged: **`NetworkReceiveEvent`** → `OnReceived` / `PacketDispatcher` pipeline.

The simulation loop continues to call **`Tick()`** → **`PollEvents()`** so unconnected and connected packets are both processed.

## Unity client

### Polling

LiteNetLib is **poll-driven**. Any `NetManager` that participates in discovery must have **`PollEvents()`** called regularly on a **single thread** (typically Unity’s main thread):

- **`Bootstrap.Update`** calls **`ClientNetwork.PollEvents()`** for the game client.
- **`LanHostDiscovery`** uses a **separate** `NetManager`; **`UILobby.Update`** calls **`_discovery.Poll()`** so responses arrive while the lobby is open.

If discovery is merged into `ClientNetwork`, only **`Bootstrap`** (or one owner) needs to poll **that** manager.

### `LanHostDiscovery` (`Assets/_MH/Scripts/Network/LanHostDiscovery.cs`)

- Own **`NetManager`** + **`EventBasedNetListener`**, **`UnconnectedMessagesEnabled = true`**, **`Start()`** with default client bind (ephemeral local port).
- **`BroadcastQuery`**: `Put("AHO_DISCOVER")`, **`SendUnconnectedMessage`** to **`IPEndPoint(IPAddress.Broadcast, GamePort)`** where **`GamePort = 9050`**.
- **`OnUnconnectedReceived`**: parse **`AHO_HOST|port|name`**, dedupe by **`address:port`**, raise **`HostsChanged`**.
- **`FindHostsAsync`**: clear list, broadcast once, then **`Task.Delay`** for a short listen window (e.g. ~1.5s). Responses still depend on **`Poll()`** during that window.

### `UILobby` (`Assets/_MH/Scripts/UI/Windows/UILobby.cs`)

- **Start** → show lobby; **Find Host** → **`FindHostsAsync`**.
- **`HostsChanged`** / refresh → populate **`UIHostIPItem`** rows.
- **Selecting a row** → **`GameRunner.ConnectAndRequestMatchmaking(host, port)`** (no game connection until the user picks a host).

### `UIHostIPItem`

Row UI binds **`LanHostDiscovery.HostInfo`** (or an equivalent struct if renamed) and invokes a callback with **address + port** for connect.

## Flow (sequence)

1. Player opens lobby → **`LanHostDiscovery.Start()`**, lobby **`Update`** polls discovery **`NetManager`**.
2. Player clicks **Find Host** → broadcast **`AHO_DISCOVER`** to **255.255.255.255:9050** (via LiteNetLib unconnected).
3. Each LAN server on **9050** with the responder replies **`AHO_HOST|9050|…`** to the sender.
4. Client **`PollEvents`** ingests replies → list updates.
5. Player picks a host → **`SetConnectionTarget`** + **`StartConnect`** with **`SomeConnectionKey`** (normal client path).

## Limitations and operational notes

- **LAN / broadcast**: Behavior depends on OS, Wi‑Fi isolation, VPN, and mobile permissions. **Android** may require `INTERNET` / multicast or explicit network state handling; test on device.
- **Security**: Discovery strings are **not authenticated**. Treat them as **hints** for local matchmaking only.
- **Multiple NICs / Docker**: Source address on the reply is correct for routing; display name is informational.
- **Protocol changes**: Version or magic-prefix the string if you evolve the format so old clients ignore unknown payloads.

## Summary

| Piece | Role |
|--------|------|
| LiteNetLib **unconnected** | Discovery without `PacketDispatcher`; same **9050** as game UDP |
| Server **`NetworkManager`** | Responds to **`AHO_DISCOVER`** with **`AHO_HOST|…`** |
| **`LanHostDiscovery`** | Client-side broadcast + collect hosts (dedicated `NetManager` today) |
| **`UILobby`** | UX + **`Poll`** for discovery manager + connect on row click |
| **`ClientNetwork`** | Connected game traffic after host selection |
