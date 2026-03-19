using UnityEngine;
using MH.Core;

namespace MH.Gamelogic{

    public class MoveableObject : MonoBehaviour{
        private Root2D _root;
        private Transform _transform;

        public void SetUp(Root2D root, float size){
            _root = root;
            _transform = this.transform;
            _transform.localScale = Vector3.one * size;
        }

        void Update(){
            Vector3 pos = _transform.position;
            pos.x = _root.Position.x;
            pos.y = _root.Position.y;
            _transform.position = pos;
        }
    }
}