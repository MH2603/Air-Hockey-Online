using UnityEngine;
using MH.Core;
using MH.Gamelogic;

namespace MH.Views
{
    /// <summary>
    /// Binds an `MH.Core.Entity` (with Root2D + custom colliders) to a Unity 2D visual.
    /// </summary>
    public abstract class EntityView2D : MonoBehaviour
    {
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool autoScaleFromCollider = true;

        [Header("Circle")]
        [SerializeField] private float circleScaleMultiplier = 1f;

        [Header("Rect")]
        [SerializeField] private float rectWidthScaleMultiplier = 1f;
        [SerializeField] private float rectHeightScaleMultiplier = 1f;

        private Root2D _root;

        public void Bind(Entity entity)
        {
            if (entity == null)
            {
                _root = null;
                enabled = false;
                return;
            }

            _root = entity.GetComponent<Root2D>();
            enabled = true;

            bool hasMoveableObject = TryGetComponent(out MoveableObject moveableObject);

            // Compute scale from our model colliders (so prefabs can be simple sprites).
            if (autoScaleFromCollider)
            {
                if (TryGetCircleCollider(entity, out var circleCollider))
                {
                    var size = circleCollider.Radius * circleScaleMultiplier;
                    if (hasMoveableObject)
                    {
                        // Prefer MoveableObject if present, since it already handles transform updates.
                        moveableObject.SetUp(_root, size);
                        followPosition = false;
                    }
                    else
                    {
                        transform.localScale = new Vector3(size, size, 1f);
                    }
                }
                else if (TryGetRectCollider(entity, out var rectCollider))
                {
                    var width = rectCollider.Width * rectWidthScaleMultiplier;
                    var height = rectCollider.Height * rectHeightScaleMultiplier;
                    transform.localScale = new Vector3(width, height, 1f);
                }
            }

            // Apply initial position immediately (Update can handle continuous updates).
            if (_root != null)
            {
                var z = transform.position.z;
                transform.position = new Vector3(_root.Position.x, _root.Position.y, z);
            }
        }

        private void Update()
        {
            if (!followPosition) return;
            if (_root == null) return;

            var z = transform.position.z;
            transform.position = new Vector3(_root.Position.x, _root.Position.y, z);
        }

        private static bool TryGetCircleCollider(Entity entity, out CircleCollider collider)
        {
            collider = null;
            if (entity == null) return false;
            if (!entity.Components.TryGetValue(typeof(CircleCollider), out var component)) return false;
            collider = component as CircleCollider;
            return collider != null;
        }

        private static bool TryGetRectCollider(Entity entity, out RectCollider collider)
        {
            collider = null;
            if (entity == null) return false;
            if (!entity.Components.TryGetValue(typeof(RectCollider), out var component)) return false;
            collider = component as RectCollider;
            return collider != null;
        }
    }
}

