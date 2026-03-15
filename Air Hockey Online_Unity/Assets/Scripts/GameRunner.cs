using MH.Core;
using UnityEngine;
using NaughtyAttributes;

namespace MH.GameLogic{

    public class GameRunner : MonoBehaviour{

        private Match _currentMatch;

        void Awake()
        {
            
        }

        void Update()
        {
            
        }

        [Button]
        void TestMatch(){
            _currentMatch = new Match(0,1);
            var monoRoot = new MonoRoot2D();
            monoRoot.Init( _currentMatch.GetPlayer(0).Paddle.GetComponent<Root2D>());
        }
    }
}