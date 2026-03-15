using MH.Core;

namespace MH.GameLogic
{

    public class GoalFrame : Entity
    {
        public GoalFrame(float size) : base()
        {
            AddComponent(new Root2D(this));
            AddComponent(new RectCollider(this, size, size));
        }
    }

    public class Paddle : Entity
    {
        public Paddle(float size) : base()
        {
            AddComponent(new Root2D(this));
            AddComponent(new CircleCollider(this, size));
        }

    }

    public class Puck : Entity
    {
        public Puck(float size) : base()
        {
            AddComponent(new Root2D(this));
            AddComponent(new CircleCollider( this, size));
        }
    }
}