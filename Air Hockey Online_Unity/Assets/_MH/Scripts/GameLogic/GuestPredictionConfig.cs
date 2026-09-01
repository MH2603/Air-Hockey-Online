using UnityEngine;

namespace MH.GameLogic
{
    /// <summary>
    /// Editor asset kept for GameRunner assignment. Playing rewind-replay no longer uses lerp / snap distances.
    /// </summary>
    [CreateAssetMenu(fileName = "GuestPredictionConfig", menuName = "MH/Game Logic/Guest Prediction Config", order = 0)]
    public sealed class GuestPredictionConfig : ScriptableObject
    {
        // Blend toward server state when error is below snap distance (full snap when at/above threshold).
        [SerializeField, Tooltip("Lerp factor toward server puck/paddle when under snap distance (0–1).")]
        private float _reconcileSoftLerp = 0.35f;

        [SerializeField, Tooltip("Puck position/velocity error at or above this uses full snap instead of soft lerp.")]
        private float _puckSnapDistance = 0.85f;

        [SerializeField, Tooltip("Paddle position error at or above this uses full snap instead of soft lerp.")]
        private float _paddleSnapDistance = 1.2f;

        public float ReconcileSoftLerp => _reconcileSoftLerp;
        public float PuckSnapDistance => _puckSnapDistance;
        public float PaddleSnapDistance => _paddleSnapDistance;
    }
}
