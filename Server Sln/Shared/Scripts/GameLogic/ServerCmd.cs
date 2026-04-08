using LiteNetLib.Utils;
using MH.Network;

namespace MH.GameLogic
{
    public enum EServerCmd : int
    {
        MatchFound = 1,
        BoardStatus = 2,
        MatchResult = 3,
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

    public struct s2c_board_status : INetPacket
    {
        public int MatchId;

        public float PuckX;
        public float PuckY;
        public float PuckVelX;
        public float PuckVelY;

        public float Paddle0X;
        public float Paddle0Y;
        public float Paddle1X;
        public float Paddle1Y;

        public void Deserialize(NetDataReader reader)
        {
            MatchId = reader.GetInt();

            PuckX = reader.GetFloat();
            PuckY = reader.GetFloat();
            PuckVelX = reader.GetFloat();
            PuckVelY = reader.GetFloat();

            Paddle0X = reader.GetFloat();
            Paddle0Y = reader.GetFloat();
            Paddle1X = reader.GetFloat();
            Paddle1Y = reader.GetFloat();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EServerCmd.BoardStatus);
            writer.Put(MatchId);

            writer.Put(PuckX);
            writer.Put(PuckY);
            writer.Put(PuckVelX);
            writer.Put(PuckVelY);

            writer.Put(Paddle0X);
            writer.Put(Paddle0Y);
            writer.Put(Paddle1X);
            writer.Put(Paddle1Y);
        }
    }

    public struct s2c_match_result : INetPacket
    {
        public int MatchId;
        public int WinnerPlayerIndex; // 0 or 1 on the server authoritative match.

        public void Deserialize(NetDataReader reader)
        {
            MatchId = reader.GetInt();
            WinnerPlayerIndex = reader.GetInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EServerCmd.MatchResult);
            writer.Put(MatchId);
            writer.Put(WinnerPlayerIndex);
        }
    }
}
