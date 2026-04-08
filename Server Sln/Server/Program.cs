using MH.GameLogic;
using MH.Network;

Console.WriteLine("Start application!");

var networkListener = new NetworkManager();
networkListener.Init();
Console.WriteLine("Server running on port 9050. Press any key to stop...");

var packetDispatcher = new PacketDispatcher(networkListener);
// var testHandler = new TestPacketHandler(packetDispatcher);
var config = new BoardConfig();
using var sessions = new MatchSessionManager(packetDispatcher, networkListener, config);
using var matchmaking = new MatchmakingHandler(packetDispatcher, networkListener);
matchmaking.OnMatchCreated += sessions.CreateMatch;

// Server simulation loop (authoritative).
var simCts = new CancellationTokenSource();
var simTask = Task.Run(async () =>
{
    const float fixedDelta = 1f / 60f;
    var delayMs = (int)Math.Round(fixedDelta * 1000f);
    if (delayMs < 1) delayMs = 1;

    while (!simCts.IsCancellationRequested)
    {
        networkListener.Tick();
        sessions.TickAndBroadcast(fixedDelta);
        await Task.Delay(delayMs, simCts.Token);
    }
}, simCts.Token);

var isRunning = true;
while (isRunning)
{
    
    var cmd = Console.ReadLine();

    switch (cmd)
    {
        case "exit":
            isRunning = false;
            break;
    }
}

simCts.Cancel();
try { simTask.Wait(500); } catch { /* ignored */ }
networkListener.Stop();
Console.WriteLine("Server stopped!");
