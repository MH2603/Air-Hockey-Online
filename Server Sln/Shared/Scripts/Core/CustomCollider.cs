using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MH.Core
{
    public enum CollisionType
    {
        Circle,
        Rect,
    }

    public class CollisionInfo
    {
        public BaseCollider Collider1 { get; }
        public BaseCollider Collider2 { get; }
        //public CustomVector2[] CollisionPoint { get; }
        public CollisionInfo(BaseCollider collider1, BaseCollider collider2)
        {
            Collider1 = collider1;
            Collider2 = collider2;
            // CollisionPoint = collisionPoint;
        }
    }

    public class BaseCollider : EntityComponent
    {
        public CustomVector2 Center => Entity.GetComponent<Root2D>().Position;
        public CollisionType Type { get; }
        public List<BaseCollider> TrackOthers = new List<BaseCollider>();
        public Action<CollisionInfo> OnCollision;   

        public BaseCollider(Entity entity) : base(entity) { }

        public override void Tick(float deltaTime)
        {
            foreach (var other in TrackOthers)
            {
                if (other == this) continue;
                CheckCollision(other);
            }
        }

        public virtual void CheckCollision(BaseCollider other)
        {
            
            
        }

    
    }

    public class CircleCollider : BaseCollider
    {
        public float Radius { get; }
        public CircleCollider(Entity entity, float radius) : base(entity)
        {
            Radius = radius;
        }

        public override void CheckCollision(BaseCollider other)
        {
            if(other.Type == CollisionType.Circle)
            {
                CheckCircleCollision((CircleCollider)other);
            }
            else if (other.Type == CollisionType.Rect)
            {
                CheckRectCollision((RectCollider)other);
            }
        }

        public void CheckCircleCollision(CircleCollider other)
        {
            var distance = CustomVector2.Distance(Center, other.Center);
            if (distance <= Radius + other.Radius)
            {
                OnCollision?.Invoke(new CollisionInfo(this, other));
            }
        }

        public void CheckRectCollision(RectCollider other)
        {
            float dst_x = Math.Abs(Center.x - other.Center.x);
            float dst_y = Math.Abs(Center.y - other.Center.y);
            if (dst_x > (other.Width / 2) + Radius) return;
            if (dst_y > (other.Height / 2) + Radius) return;
            OnCollision?.Invoke(new CollisionInfo(this, other));

        }
    }
 
    public class RectCollider : BaseCollider
    {
        public float Width { get; }
        public float Height { get; }
        public RectCollider(Entity entity, float width, float height) : base(entity)
        {
            Width = width;
            Height = height;
        }
        
        
    }
}
