using MH.Core;

namespace MH.GameLogic{
    public class Puck : Entity
    {
        public CircleCollider Collider => GetComponent<CircleCollider>();
        
        public Puck(float size) : base()
        {
            AddComponent(new Root2D(this));
            AddComponent(new CircleCollider( this, size));
            AddComponent(new MoveComponent(this));
        }
    }
}