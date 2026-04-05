using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MH.UI
{
    public class UIMainMenu : UIWindow
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _startButton;

        public string Title
        {
            get => _titleText != null ? _titleText.text : null;
            set
            {
                if (_titleText != null)
                    _titleText.text = value;
            }
        }

        private void Awake()
        {
            if (_backgroundImage != null)
                _backgroundImage.raycastTarget = true;

            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartButtonClicked);
        }

        private void OnDestroy()
        {
            if (_startButton != null)
                _startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            OnStartClicked();
        }

        protected virtual void OnStartClicked()
        {
        }
    }
}
