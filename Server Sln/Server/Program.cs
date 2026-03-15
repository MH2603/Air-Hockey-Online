// See https://aka.ms/new-console-template for more information
using MH.GameLogic;
using MH.Network;

Console.WriteLine("Hello, World!");

var networkListener = new NetworkManager();
networkListener.Init();
Console.WriteLine("Server running on port 9050. Press any key to stop...");

var packetDispatcher = new PacketDispatcher(networkListener);

var testHandler = new TestPacketHandler(packetDispatcher);  

Console.ReadKey(true); // consume the key

networkListener.Stop();
Console.WriteLine("Server stopped!");
