// See https://aka.ms/new-console-template for more information
using MH.GameLogic;
using MH.Network;

Console.WriteLine("Start application!");

var networkListener = new NetworkManager();
networkListener.Init();
Console.WriteLine("Server running on port 9050. Press any key to stop...");

var packetDispatcher = new PacketDispatcher(networkListener);
var testHandler = new TestPacketHandler(packetDispatcher);
using var matchmaking = new MatchmakingHandler(packetDispatcher, networkListener);

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

networkListener.Stop();
Console.WriteLine("Server stopped!");
