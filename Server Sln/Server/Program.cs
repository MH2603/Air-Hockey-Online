// See https://aka.ms/new-console-template for more information
using Server.Network;

Console.WriteLine("Hello, World!");

var networkListener = new NetworkListener();
networkListener.Init();

Console.WriteLine("Server running on port 9050. Press any key to stop...");

// LiteNetLib requires PollEvents to process connections - poll until user presses key
while (!Console.KeyAvailable)
{
    networkListener.PollEvents();
    Thread.Sleep(15); // ~66 updates/sec
}
Console.ReadKey(true); // consume the key

networkListener.Stop();
Console.WriteLine("Server stopped!");
