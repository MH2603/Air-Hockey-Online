# Air Hockey Online

Air Hockey Online is a simple online multiplayer air hockey game built with Unity for the client and a C# server backend. It demonstrates real-time networking, basic matchmaking, and client–server communication for a fast-paced arcade-style experience.

## Features

- **Online multiplayer**: Two players connect over the network and play in real time.
- **Client–server architecture**: Unity client communicates with a standalone C# server.
- **Basic matchmaking**: Players connect to a listener and are paired for a match.
- **Extensible codebase**: Separate Unity project and server solution for easier iteration.

## Tech Stack

- **Client**: Unity (C#)
- **Server**: .NET / C# console app
- **Networking**: Custom TCP/UDP-based networking (e.g. `NetworkClient`, `NetworkListener`)

## Project Structure

- **`Air Hockey Online_Unity/`** – Unity project containing game scenes, assets, and client-side scripts.
- **`Server Sln/Server/`** – C# server solution and networking code.
- **`.gitignore`** – Git ignore rules (e.g. Unity and build artifacts).

## Getting Started

### Prerequisites

- **Unity**: Recommended version matching your installed editor (e.g. 2021.x or later).
- **.NET SDK**: .NET 6.0 or compatible version installed.
- **Git** (optional but recommended).

### Cloning the Repository

```bash
git clone <your-repo-url>.git
cd "Air Hockey Online Project"
