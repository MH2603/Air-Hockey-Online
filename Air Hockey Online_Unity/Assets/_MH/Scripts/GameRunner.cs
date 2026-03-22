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
        
        void Awake()
        {
            
        }

        void Update()
        {
            if(_currentMatch == null) return;
            
            _currentMatch.Tick(Time.deltaTime);

            if(Input.GetMouseButtonDown(0)){
                _isMouseDown = true;
            }else if(Input.GetMouseButtonUp(0)){
                _isMouseDown = false;
            }

            if(_isMouseDown){
                Vector3 mousePos = MouseUtils.GetMouseWorldPosition();
                CustomVector2 pos = new CustomVector2(mousePos.x, mousePos.y);
                _currentMatch.MovePaddle(0, pos);
            }
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