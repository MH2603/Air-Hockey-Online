using MH.Core;

namespace MH.GameLogic
{
    /// <summary>
    /// Static rectangle wall used as a visual placeholder (and future collision primitive).
    /// </summary>
    public class Wall : Entity
    {
        public RectCollider Collider => GetComponent<RectCollider>();

        public Wall(float width, float height)
        {
            AddComponent(new Root2D(this));
            AddComponent(new RectCollider(this, width, height));
        }
    }
}

