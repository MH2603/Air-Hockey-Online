using UnityEngine;

namespace MH.UI
{
    public class UIView : MonoBehaviour
    {
        [SerializeField] private bool _showWhenStart;

        /// <summary>When true, <see cref="UIManager"/> shows this view during <c>Start</c> (after instantiation).</summary>
        public bool ShowWhenStart => _showWhenStart;

        public virtual UILayer Layer => UILayer.HUD;

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        public bool IsVisible => gameObject.activeSelf;
    }
}
