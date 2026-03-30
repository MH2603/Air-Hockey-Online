using MH.Core;
using System;
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
        private readonly Dictionary<int, HockeyPlayer> _playerMap = new Dictionary<int, HockeyPlayer>();
        private readonly List<Wall> _walls = new List<Wall>();

        private readonly int _playerIdBottom;
        private readonly int _playerIdTop;

        private Puck _puck;
        private BoardConfig _config;

        /// <summary> Only one puck velocity bounce per tick (see plan: corner / multi-contact). </summary>
        bool _puckVelocityConsumedThisTick;
        
        public Puck Puck => _puck;
        public IReadOnlyList<Wall> Walls => _walls;

        public Match(int playerId1, int playerId2, BoardConfig config)
        {
            _config = config;
            _playerIdBottom = playerId1;
            _playerIdTop = playerId2;
            
            _playerMap[playerId1] = new HockeyPlayer(playerId1, config);
            _playerMap[playerId2] = new HockeyPlayer(playerId2, config);
            
            InitPuck(config, _playerMap[playerId1].Paddle, _playerMap[playerId2].Paddle);

            CreateDefaultWalls();
            RegisterPuckAgainstWallsAndHandlers();
            SetInitialObjectPositions(playerId1, playerId2);
        }

        private void InitPuck(BoardConfig config, Paddle paddle01, Paddle paddle02)
        {
            _puck = new Puck(config.PuckRadius);
            _puck.Collider.TrackOthers.Add( paddle01.GetComponent<CircleCollider>());
            _puck.Collider.TrackOthers.Add( paddle02.GetComponent<CircleCollider>());
        }

        /// <summary>
        /// Tick order: paddles integrate velocity → puck move + collision → clamp paddle positions.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _puckVelocityConsumedThisTick = false;

            foreach (var player in _playerMap.Values)
                player.Paddle.Tick(deltaTime);

            _puck.Tick(deltaTime);

            foreach (var player in _playerMap.Values)
                ClampPaddlePosition(player);
        }

        public HockeyPlayer GetPlayer(int playerId){
            if(!_playerMap.ContainsKey(playerId)){
                Logger.LogError($" Not found player with id = {playerId}");
                return null;
            }
            return _playerMap[playerId];
        }

        /// <summary> Phase 0 option 2: paddle moves via MoveComponent integration each tick. </summary>
        public void SetPaddleVelocity(int playerId, CustomVector2 velocity){
            var player = GetPlayer(playerId);
            if(player == null) return;
            
            velocity = CustomVector2.ClampMagnitude(velocity, _config.PaddleMaxSpeed);
            player.Paddle.GetComponent<MoveComponent>().SetVelocity(velocity);
        }

        void RegisterPuckAgainstWallsAndHandlers()
        {
            _puck.Collider.OnCollision += HandlePuckCollision;
            foreach (var wall in _walls)
                _puck.Collider.TrackOthers.Add(wall.Collider);
        }

        void HandlePuckCollision(CollisionInfo info)
        {
            var other = info.Collider1 == _puck.Collider ? info.Collider2 : info.Collider1;
            switch (other.Entity)
            {
                case Paddle paddle:
                    PuckCollisionResponse.ResolvePuckPaddle(
                        _puck,
                        paddle,
                        _config,
                        paddle.GetComponent<MoveComponent>().CurrentVelocity,
                        ref _puckVelocityConsumedThisTick);
                    break;
                case Wall wall:
                    PuckCollisionResponse.ResolvePuckWall(_puck, wall, _config, ref _puckVelocityConsumedThisTick);
                    break;
            }
        }

        void ClampPaddlePosition(HockeyPlayer player)
        {
            var root = player.Paddle.GetComponent<Root2D>();
            float maxX = _config.TableWidth * 0.5f - _config.PaddleRadius;
            float maxY = _config.TableLenght * 0.5f - _config.PaddleRadius;
            float x = Math.Clamp(root.Position.x, -maxX, maxX);
            float y = root.Position.y;
            const float guard = 0.05f;
            if (player.Id == _playerIdBottom)
                y = Math.Clamp(y, -maxY, -guard);
            else if (player.Id == _playerIdTop)
                y = Math.Clamp(y, guard, maxY);
            else
                y = Math.Clamp(y, -maxY, maxY);

            root.Position = new CustomVector2(x, y);
        }

        private void CreateDefaultWalls()
        {
            float WallThickness = _config.WallThickness;
            var TableLength = _config.TableLenght;
            var TableWidth = _config.TableWidth;
            
            _walls.Clear();

            // Vertical walls (left/right)
            var leftWall = new Wall(WallThickness, TableLength + 2f * WallThickness);
            leftWall.GetComponent<Root2D>().Position = new CustomVector2(-TableWidth / 2f - WallThickness / 2f, 0f);
            _walls.Add(leftWall);

            var rightWall = new Wall(WallThickness, TableLength + 2f * WallThickness);
            rightWall.GetComponent<Root2D>().Position = new CustomVector2(TableWidth / 2f + WallThickness / 2f, 0f);
            _walls.Add(rightWall);

            // Horizontal walls (bottom/top)
            var bottomWall = new Wall(TableWidth + 2f * WallThickness, WallThickness);
            bottomWall.GetComponent<Root2D>().Position = new CustomVector2(0f, -TableLength / 2f - WallThickness / 2f);
            _walls.Add(bottomWall);

            var topWall = new Wall(TableWidth + 2f * WallThickness, WallThickness);
            topWall.GetComponent<Root2D>().Position = new CustomVector2(0f, TableLength / 2f + WallThickness / 2f);
            _walls.Add(topWall);
        }

        private void SetInitialObjectPositions(int playerIdBottom, int playerIdTop)
        {
            float WallThickness = _config.WallThickness;
            var TableLength = _config.TableLenght;
            var TableWidth = _config.TableWidth;
            var PaddleSize = _config.PaddleRadius;
            
            // Puck starts at the center.
            _puck.GetComponent<Root2D>().Position = CustomVector2.Zero;

            var bottom = GetPlayer(playerIdBottom);
            if (bottom != null)
            {
                bottom.Paddle.GetComponent<Root2D>().Position = new CustomVector2(0f, -TableLength / 2f + PaddleSize);
                bottom.GoalFrame.GetComponent<Root2D>().Position = new CustomVector2(0f, -TableLength / 2f + WallThickness / 2f);
            }

            var top = GetPlayer(playerIdTop);
            if (top != null)
            {
                top.Paddle.GetComponent<Root2D>().Position = new CustomVector2(0f, TableLength / 2f - PaddleSize);
                top.GoalFrame.GetComponent<Root2D>().Position = new CustomVector2(0f, TableLength / 2f - WallThickness / 2f);
            }
        }
    }

    
}
