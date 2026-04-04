using MH.Core;
using UnityEngine;
using NaughtyAttributes;
using MH.Gamelogic;
using MH.Views;

namespace MH.GameLogic{

    public class GameRunner : MonoBehaviour
    {

        [SerializeField] private BoardConfig _config;
        
        private Match _currentMatch;
        private bool _isMouseDown;

        public Match CurrentMatch => _currentMatch;
        
        void Awake()
        {
            
        }

        void Update()
        {
            if(_currentMatch == null) return;
            
            if(Input.GetMouseButtonDown(0)){
                _isMouseDown = true;
            }else if(Input.GetMouseButtonUp(0)){
                _isMouseDown = false;
            }

            if(_isMouseDown){
                Vector3 mousePos = MouseUtils.GetMouseWorldPosition();
                CustomVector2 pos = new CustomVector2(mousePos.x, mousePos.y);
                var p0 = _currentMatch.GetPlayer(0);
                if (p0 != null)
                {
                    var paddlePos = p0.Paddle.GetComponent<Root2D>().Position;
                    var vel = (pos - paddlePos) * _config.PaddlePositionFollow;
                    _currentMatch.SetPaddleVelocity(0, vel);
                }
            }
            else
            {
                _currentMatch.SetPaddleVelocity(0, CustomVector2.Zero);
            }
            
            _currentMatch.Tick(Time.deltaTime);
        }

        [Button]
        void TestMatch(){
            _currentMatch = new Match(0,1, _config);

            // Spawn/bind the visuals.
            var matchView = GetComponent<MatchView2D>() ?? gameObject.AddComponent<MatchView2D>();
            matchView.SetMatch(_currentMatch);
        }
    }
}