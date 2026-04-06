using LiteNetLib.Utils;
using MH.Network;

namespace MH.GameLogic
{
    public enum EClientCmd : int
    {
        MousePos = 1,
        FindMatch = 2,
    }

    public struct c2s_find_match : INetPacket
    {
        public void Deserialize(NetDataReader reader) { }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EClientCmd.FindMatch);
        }
    }

    public struct c2s_mouse_pos : INetPacket
    {
        public float X;
        public float Y;

        public void Deserialize(NetDataReader reader)
        {
            X = reader.GetFloat();  
            Y = reader.GetFloat();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EClientCmd.MousePos); // Gửi kèm loại lệnh để server biết cách xử lý
            writer.Put(X);
            writer.Put(Y);
        }
    }
}
