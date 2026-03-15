using MH.Core;
using UnityEngine;

namespace MH.GameLogic{
    public class MonoRoot2D : MonoBehaviour
    {
        private Transform _transform;
        private Vector3 _currentPos;
        private Root2D _root;

        private void Awake()
        {
            _transform = this.transform;
        }

        public void Init( Root2D root){
            _root = root;
        }

        void Update()
        {
            _currentPos = _transform.position;
            _currentPos.x = _root.Position.x;
            _currentPos.y = _root.Position.y;
            _transform.position = _currentPos;
        }

    }
}