using System;
using MH.Core;

namespace MH.GameLogic{

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

    [Serializable]
    public class BoardConfig{

        // TABLE — full width (X) and length (Y) of playable area; goal width at each end (game units).
        public float TableWidth = 9f;
        public float TableLenght = 18f;
        public float GoalWidth = 4.5f;

        // OBJECTS — puck / paddle circle radii (1 = reference scale in design notes).
        public float PuckRadius = 0.5f;
        public float PaddleRadius = 1f;

        // WALLS — outer boundary wall thickness for puck collision (AABB depth).
        public float WallThickness = 2f;

        // PHYSICS — elasticity e for puck bounces; paddle influence f (blend of paddle velocity after hit).
        public float Bounciness     = 0.95f;
        public float f = 0.5f;

        // SPEEDS — puck min/max after collision; paddle max speed and position-follow gain (target − position).
        public float MinPuckSpeed = 0.1f;
        public float MaxPuckSpeed = 30f;
        public float PaddleMaxSpeed = 100f;
        public float PaddlePositionFollow = 50f;
    }
}
