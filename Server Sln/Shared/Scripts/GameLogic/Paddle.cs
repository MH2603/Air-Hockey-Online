using MH.Core;

namespace MH.GameLogic
{
    public class Paddle : Entity
    {
        public Paddle(float size) : base()
        {
            AddComponent(new Root2D(this));
            AddComponent(new CircleCollider(this, size));
            AddComponent(new MoveComponent(this));
        }

    }
}