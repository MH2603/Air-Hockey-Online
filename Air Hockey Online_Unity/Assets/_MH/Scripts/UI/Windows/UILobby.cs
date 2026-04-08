using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MH.GameLogic;
using MH.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MH.UI
{
    public sealed class UILobby : UIWindow
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _backButton;

        [Header("Find Hosts")]
        [SerializeField] private Button _findHostButton;
        [SerializeField] private TMP_Text _findHostLabel;
        [SerializeField] private float _listenSeconds = 1.5f;

        [Header("List")]
        [Tooltip("Optional. If not set, a ScrollView is built at runtime under ScrollRoot.")]
        [SerializeField] private Transform _listContent;
        [SerializeField] private RectTransform _scrollRoot;
        [SerializeField] private UIHostIPItem _itemPrefab;
        [SerializeField] private TMP_Text _emptyLabel;

        private LanHostDiscovery _discovery;
        private readonly List<UIHostIPItem> _spawned = new();
        private bool _isFinding;

        private void Awake()
        {
            // Initialization: create discovery service (client-side only).
            _discovery = new LanHostDiscovery();
            _discovery.Start();
            _discovery.HostsChanged += RefreshList;

            // UI: ensure we have a ScrollView to populate.
            EnsureScrollView();

            if (_findHostButton != null)
                _findHostButton.onClick.AddListener(OnFindHostClicked);
            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked);

            RefreshList();
        }

        private void Update()
        {
            _discovery?.Poll();
        }

        private void OnDestroy()
        {
            // Cleanup: unbind + dispose discovery.
            if (_findHostButton != null)
                _findHostButton.onClick.RemoveListener(OnFindHostClicked);
            if (_backButton != null)
                _backButton.onClick.RemoveListener(OnBackClicked);

            if (_discovery != null)
            {
                _discovery.HostsChanged -= RefreshList;
                _discovery.Dispose();
                _discovery = null;
            }
        }

        public override void Show()
        {
            // Main logic: update title and show view.
            if (_titleText != null && string.IsNullOrWhiteSpace(_titleText.text))
                _titleText.text = "Lobby";

            RefreshList();
            base.Show();
        }

        private void OnBackClicked()
        {
            // UI flow: return to main menu.
            UIManager.Instance?.Hide<UILobby>();
            UIManager.Instance?.Show<UIMainMenu>();
        }

        private async void OnFindHostClicked()
        {
            if (_isFinding)
                return;

            _isFinding = true;
            SetFindingState(true);

            // Main logic: broadcast and wait briefly for responses.
            try
            {
                await _discovery.FindHostsAsync(_listenSeconds);
            }
            finally
            {
                _isFinding = false;
                SetFindingState(false);
                RefreshList();
            }
        }

        private void SetFindingState(bool finding)
        {
            if (_findHostButton != null)
                _findHostButton.interactable = !finding;

            if (_findHostLabel != null)
                _findHostLabel.text = finding ? "Searching..." : "Find Host";
        }

        private void RefreshList()
        {
            EnsureScrollView();
            if (_listContent == null || _itemPrefab == null)
                return;

            // Cleanup: remove previous items.
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();

            // Main logic: order by name/address and spawn items.
            var hosts = _discovery?.Hosts
                ?.OrderBy(h => h.Name)
                ?.ThenBy(h => h.Address)
                ?.ToList() ?? new List<LanHostDiscovery.HostInfo>();

            if (_emptyLabel != null)
                _emptyLabel.gameObject.SetActive(hosts.Count == 0);

            foreach (var host in hosts)
            {
                var item = Instantiate(_itemPrefab, _listContent);
                item.Bind(host, OnHostSelected);
                _spawned.Add(item);
            }
        }

        private void OnHostSelected(LanHostDiscovery.HostInfo host)
        {
            // UI flow: connect only after the player selects a host.
            UIManager.Instance?.Hide<UILobby>();
            if (UIManager.Instance != null && UIManager.Instance.TryGet<UILoading>(out _))
                UIManager.Instance.Show<UILoading>();

            GameRunner.Instance?.ConnectAndRequestMatchmaking(host.Address, host.Port);
        }

        private void EnsureScrollView()
        {
            if (_listContent != null)
                return;

            if (_scrollRoot == null)
                return;

            // Build: ScrollRect + Viewport + Content (minimal, functional).
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(_scrollRoot, false);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            viewportRt.anchorMin = new Vector2(0f, 0f);
            viewportRt.anchorMax = new Vector2(1f, 1f);
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;

            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.15f);
            var mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(0f, 0f);
            contentRt.offsetMax = new Vector2(0f, 0f);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(16, 16, 16, 16);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            _listContent = contentRt;
        }
    }
}

