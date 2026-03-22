
namespace MH.Core{
    public class MoveComponent : EntityComponent{
    
        CustomVector2 _currentVelocity;
        private Root2D _root;

        public MoveComponent( Entity entity) : base(entity){
            _root = entity.GetComponent<Root2D>();
        }

        public override void Tick(float deltaTime){
            _root.Position += _currentVelocity * deltaTime;
        }

        public CustomVector2 CurrentVelocity => _currentVelocity;

        public void SetVelocity(CustomVector2 newVel){
            _currentVelocity = newVel;
        }

    }
}


