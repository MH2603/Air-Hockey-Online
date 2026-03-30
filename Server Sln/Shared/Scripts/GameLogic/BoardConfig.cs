using System;
using MH.Core;
using UnityEngine;

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
        
        [Header("TABLE")]
        public float TableWidth = 9f;
        public float TableLenght = 18f;
        public float GoalWidth = 4.5f;
        
        [Header("OBJECTS")]
        public float PuckRadius = 1f;
        public float PaddleRadius = 2.5f;
        
        [Header("WALLS")]
        public float WallThickness = 0.5f;
        
        [Header("PHYSICS")]
        public float Bounciness     = 0.95f;
        public float f = 0.5f;

        [Header(" SPEEDS")]
        public float MinPuckSpeed = 0.5f;
        public float MaxPuckSpeed = 50f;

        public float PaddleMaxSpeed = 28f;

        /// <summary> Scales (mouse target − paddle position) into velocity when driving paddle from a position target. </summary>
        public float PaddlePositionFollow = 12f;
    }
}