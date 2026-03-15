using MH.Network;

namespace MH.GameLogic
{
    public class TestPacketHandler : IPacketHandler
    {
        public TestPacketHandler(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler<c2s_mouse_pos>((int)EClientCmd.MousePos, this);
        }

        public void HandlePacket(int fromId, int packType, INetPacket packet)
        {
            EClientCmd cmdType = (EClientCmd)packType;

            switch (cmdType)
            {
                case EClientCmd.MousePos:
                    var mousePos = (c2s_mouse_pos)packet;
                    Console.WriteLine($"Received mouse position from client {fromId}: ({mousePos.X}, {mousePos.Y})");
                    break;  
            }
        }
    }
}
