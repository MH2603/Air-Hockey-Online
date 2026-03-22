namespace MH.GameLogic
{

    public class HockeyPlayer
    {
        public int Id { get; set; }
        public Paddle Paddle { get; set; }
        public GoalFrame GoalFrame { get; set; }

        public HockeyPlayer(int id, BoardConfig config)
        {
            Id = id;
            Paddle = new Paddle(config.PaddleRadius);
            GoalFrame = new GoalFrame(config.GoalWidth);
        }

    }
}