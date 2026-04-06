using LiteNetLib.Utils;
using MH.Network;

namespace MH.GameLogic
{
    public enum EServerCmd : int
    {
        MatchFound = 1,
    }

    public struct s2c_match_found : INetPacket
    {
        public int MatchId;
        public int LocalPlayerIndex;

        public void Deserialize(NetDataReader reader)
        {
            MatchId = reader.GetInt();
            LocalPlayerIndex = reader.GetInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EServerCmd.MatchFound);
            writer.Put(MatchId);
            writer.Put(LocalPlayerIndex);
        }
    }
}
