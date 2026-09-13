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
        /// <summary>Guest prediction / input tick (starts at 1). 0 is reserved as “no ack”.</summary>
        public uint Tick;

        public void Deserialize(NetDataReader reader)
        {
            X = reader.GetFloat();
            Y = reader.GetFloat();
            Tick = reader.GetUInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EClientCmd.MousePos);
            writer.Put(X);
            writer.Put(Y);
            writer.Put(Tick);
        }
    }
}
