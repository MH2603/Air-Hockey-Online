using System;
using MH.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MH.UI
{
    public sealed class UIHostIPItem : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        private LanHostDiscovery.HostInfo _host;
        private Action<LanHostDiscovery.HostInfo> _onClick;

        private void Awake()
        {
            // Initialization: bind click.
            if (_button != null)
                _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            // Cleanup: unbind click.
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }

        public void Bind(LanHostDiscovery.HostInfo host, Action<LanHostDiscovery.HostInfo> onClick)
        {
            // Main logic: store data and update UI.
            _host = host;
            _onClick = onClick;

            if (_label != null)
                _label.text = $"{host.Name}  ({host.Address}:{host.Port})";
        }

        private void OnClicked()
        {
            _onClick?.Invoke(_host);
        }
    }
}

