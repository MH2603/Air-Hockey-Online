using TMPro;
using UnityEngine;

namespace MH.UI
{
    /// <summary>Gameplay HUD: two player scores (assign labels in prefab or leave null for no-op).</summary>
    public class UIScoreHud : UIView
    {
        [SerializeField] private TMP_Text _scorePlayer0;
        [SerializeField] private TMP_Text _scorePlayer1;

        public void SetScores(int score0, int score1)
        {
            if (_scorePlayer0 != null)
                _scorePlayer0.text = score0.ToString();
            if (_scorePlayer1 != null)
                _scorePlayer1.text = score1.ToString();
        }
    }
}
