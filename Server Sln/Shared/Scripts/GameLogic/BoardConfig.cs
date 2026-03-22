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

    public class BoardConfig{

        public float PuckRadius = 1f;
        public float PaddleRadius = 2.5f;
        public float GoalWidth = 4.5f;
        public float TableWidth = 9f;
        public float TableLenght = 18f;
        public float WallThickness = 0.5f;
        
        public float Bounciness     = 0.95f;
        public float f = 0.5f;
    }
}