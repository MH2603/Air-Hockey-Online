using LiteNetLib.Utils;
using MH.Network;

namespace MH.GameLogic
{
    public enum EServerCmd : int
    {
        MatchFound = 1,
        BoardStatus = 2,
        MatchResult = 3,
        GoalScored = 4,
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

        public int Score0;
        public int Score1;
        /// <summary><see cref="MatchPhase"/> on the authoritative match.</summary>
        public byte MatchPhase;

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

            Score0 = reader.GetInt();
            Score1 = reader.GetInt();
            MatchPhase = reader.GetByte();
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

            writer.Put(Score0);
            writer.Put(Score1);
            writer.Put(MatchPhase);
        }
    }

    public struct s2c_goal_scored : INetPacket
    {
        public int MatchId;
        public int ScoringPlayerIndex;
        public int ConcedingPlayerIndex;
        public int Score0;
        public int Score1;
        public int ResetDurationMs;

        public void Deserialize(NetDataReader reader)
        {
            MatchId = reader.GetInt();
            ScoringPlayerIndex = reader.GetInt();
            ConcedingPlayerIndex = reader.GetInt();
            Score0 = reader.GetInt();
            Score1 = reader.GetInt();
            ResetDurationMs = reader.GetInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)EServerCmd.GoalScored);
            writer.Put(MatchId);
            writer.Put(ScoringPlayerIndex);
            writer.Put(ConcedingPlayerIndex);
            writer.Put(Score0);
            writer.Put(Score1);
            writer.Put(ResetDurationMs);
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
