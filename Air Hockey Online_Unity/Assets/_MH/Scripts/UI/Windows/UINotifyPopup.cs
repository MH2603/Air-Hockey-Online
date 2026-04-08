using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MH.UI
{
    public sealed class UINotifyPopup : UIWindow
    {
        [Header("Text")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _contentText;

        [Header("Buttons")]
        [SerializeField] private Button _yesButton;
        [SerializeField] private TMP_Text _yesLabel;
        [SerializeField] private Button _noButton;
        [SerializeField] private TMP_Text _noLabel;

        private Action _onYes;
        private Action _onNo;

        private void Awake()
        {
            // Initialization: bind button click handlers once.
            if (_yesButton != null)
                _yesButton.onClick.AddListener(OnYesClicked);
            if (_noButton != null)
                _noButton.onClick.AddListener(OnNoClicked);

            // Default: No button hidden unless configured by Show().
            if (_noButton != null)
                _noButton.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // Cleanup: unbind listeners.
            if (_yesButton != null)
                _yesButton.onClick.RemoveListener(OnYesClicked);
            if (_noButton != null)
                _noButton.onClick.RemoveListener(OnNoClicked);
        }

        public void Show(
            string title,
            string content,
            string yesLabel,
            Action onYes,
            string noLabel = null,
            Action onNo = null)
        {
            // Validation: keep UI stable even with missing references.
            _onYes = onYes;
            _onNo = onNo;

            // Main logic: set text + toggle buttons.
            if (_titleText != null)
                _titleText.text = title ?? string.Empty;
            if (_contentText != null)
                _contentText.text = content ?? string.Empty;

            if (_yesLabel != null)
                _yesLabel.text = string.IsNullOrWhiteSpace(yesLabel) ? "OK" : yesLabel;

            var showNo = !string.IsNullOrWhiteSpace(noLabel);
            if (_noButton != null)
                _noButton.gameObject.SetActive(showNo);
            if (_noLabel != null)
                _noLabel.text = showNo ? noLabel : string.Empty;

            base.Show();
        }

        private void OnYesClicked()
        {
            // Main logic: close first to avoid double-click issues.
            Hide();
            var cb = _onYes;
            _onYes = null;
            _onNo = null;
            cb?.Invoke();
        }

        private void OnNoClicked()
        {
            // Main logic: close first to avoid double-click issues.
            Hide();
            var cb = _onNo;
            _onYes = null;
            _onNo = null;
            cb?.Invoke();
        }
    }
}

