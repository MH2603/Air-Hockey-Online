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
        [Tooltip("Full width of the playable table along X (game units).")]
        public float TableWidth = 9f;
        [Tooltip("Full length of the playable table along Y (game units).")]
        public float TableLenght = 18f;
        [Tooltip("Width of the goal opening at each end of the table (game units).")]
        public float GoalWidth = 4.5f;
        
        [Header("OBJECTS")]
        [Tooltip("Puck circle collider radius (1 = reference scale in design notes).")]
        public float PuckRadius = 1f;
        [Tooltip("Paddle circle collider radius (larger than puck for reach).")]
        public float PaddleRadius = 2.5f;
        
        [Header("WALLS")]
        [Tooltip("Thickness of outer boundary walls used for puck collision (AABB depth).")]
        public float WallThickness = 0.5f;
        
        [Header("PHYSICS")]
        [Tooltip("Elasticity e for puck bounces (paddle/wall); higher = more energy kept (typical ~0.7–1).")]
        public float Bounciness     = 0.95f;
        [Tooltip("Paddle influence f: blend of paddle velocity added to puck after hit (typical ~0.4–0.6).")]
        public float f = 0.5f;

        [Header(" SPEEDS")]
        [Tooltip("Minimum puck speed after collision clamp (keeps the puck from stalling).")]
        public float MinPuckSpeed = 0.5f;
        [Tooltip("Maximum puck speed after collision clamp (limits tunneling and shot power).")]
        public float MaxPuckSpeed = 50f;

        [Tooltip("Maximum magnitude of paddle velocity from input each tick.")]
        public float PaddleMaxSpeed = 28f;

        /// <summary> Scales (mouse target − paddle position) into velocity when driving paddle from a position target. </summary>
        [Tooltip("Gain applied to paddle velocity when following a target position (e.g. cursor): scales (target − paddle position).")]
        public float PaddlePositionFollow = 12f;
    }
}