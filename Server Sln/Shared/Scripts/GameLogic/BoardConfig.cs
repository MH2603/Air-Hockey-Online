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

        /// <summary>World X offset applied to each goal frame position.</summary>
        public float GoalFrameOffsetX = 0f;

        /// <summary>Offset along the goal line toward table center (positive moves both goals inward).</summary>
        public float GoalFrameOffsetY = 0f;

        // OBJECTS — puck / paddle circle radii (1 = reference scale in design notes).
        public float PuckRadius = 0.5f;
        public float PaddleRadius = 1f;

        // WALLS — outer boundary wall thickness for puck collision (AABB depth).
        public float WallThickness = 2f;

        /// <summary> Gap along y = 0 between upper and lower vertical side walls (see reference table art).</summary>
        public float VerticalWallCenterGap = 0.12f;

        // PHYSICS — elasticity e for puck bounces; paddle influence f (blend of paddle velocity after hit).
        public float Bounciness     = 0.95f;
        public float f = 0.5f;

        // SPEEDS — puck min/max after collision; paddle max speed and position-follow gain (target − position).
        public float MinPuckSpeed = 0.1f;
        public float MaxPuckSpeed = 30f;
        public float PaddleMaxSpeed = 100f;
        public float PaddlePositionFollow = 50f;

        /// <summary>After a goal, wait this many seconds before respawning the puck and resuming play.</summary>
        public float PostGoalResetDelaySeconds = 1f;

        /// <summary>Puck X offset when respawning beside the conceding player’s paddle row (world X).</summary>
        public float PostGoalPuckSpawnOffsetX = 0f;

        /// <summary>Puck Y offset from the conceding player’s paddle row toward table center (positive moves both spawns inward).</summary>
        public float PostGoalPuckSpawnOffsetY = 0f;
    }
}
