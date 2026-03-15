using MH.Core;
using System.Collections;
using System.Collections.Generic;   

namespace MH.GameLogic
{

    // | Object         | Size (puck = 1) |
    // | -------------- | --------------- |
    // | Puck           | 1               |
    // | Paddle         | 2.5             |
    // | Goal width     | 4.5             |
    // | Table width    | 9               |
    // | Table length   | 18              |
    // | Wall thickness | 0.5             |

    // Puck mass      = 1
    // Paddle mass    = 5 – 8
    // Bounciness     = 0.95 – 1
    // Friction       = 0
    // Linear drag    = 0
    
    public class Match
    {
        private Dictionary<int, HockeyPlayer> _playerMap = new Dictionary<int, HockeyPlayer>();
        private Puck _puck;

        public Match(int playerId1, int playerId2)
        {
            _playerMap[playerId1] = new HockeyPlayer(playerId1);
            _playerMap[playerId2] = new HockeyPlayer(playerId2);
            _puck = new Puck(1f);
        }

        public void Tick(float deltaTime)
        {
            _puck.Tick(deltaTime);
        }

        public HockeyPlayer GetPlayer(int playerId){
            if(!_playerMap.ContainsKey(playerId)){
                Logger.LogError($" Not found player with id = {playerId}");
                return null;
            }
            return _playerMap[playerId];
        }

        public void MovePaddle(int playerId, CustomVector2 target){
            var player = GetPlayer(playerId);
            if(player == null) return;
            
            player.Paddle.GetComponent<Root2D>().Position = target;
        }
    }

    public class HockeyPlayer
    {
        public int Id { get; set; }
        public Paddle Paddle { get; set; }
        public GoalFrame GoalFrame { get; set; }

        public HockeyPlayer(int id)
        {
            Id = id;
            Paddle = new Paddle(2.5f);
            GoalFrame = new GoalFrame(4.5f);
        }

    }
}