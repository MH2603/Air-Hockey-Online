using UnityEngine;

namespace MH.GameLogic
{
    /// <summary>Editor asset holding <see cref="BoardConfig"/> (table, physics, paddle tuning) for matches.</summary>
    [CreateAssetMenu(fileName = "MatchConfig", menuName = "MH/Game Logic/Match Config", order = 0)]
    public sealed class MatchConfig : ScriptableObject
    {
        // Serialized tuning shared with server logic via BoardConfig.
        [SerializeField] private BoardConfig _board = new BoardConfig();

        /// <summary>Runtime config passed to <see cref="Match"/> and <see cref="MH.Network.HostGameSession"/>.</summary>
        public BoardConfig Board => _board;
    }
}
