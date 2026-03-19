namespace MH.GameLogic
{

    public class HockeyPlayer
    {
        public int Id { get; set; }
        public Paddle Paddle { get; set; }
        public GoalFrame GoalFrame { get; set; }

        public HockeyPlayer(int id)
        {
            Id = id;
            Paddle = new Paddle(Match.PaddleSize);
            GoalFrame = new GoalFrame(Match.GoalFrameSize);
        }

    }
}