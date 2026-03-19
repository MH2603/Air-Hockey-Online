using MH.Core;

namespace MH.GameLogic{
    public class GoalFrame : Entity
    {
        public GoalFrame(float size) : base()
        {
            AddComponent(new Root2D(this));
            AddComponent(new RectCollider(this, size, size));
        }
    }
}