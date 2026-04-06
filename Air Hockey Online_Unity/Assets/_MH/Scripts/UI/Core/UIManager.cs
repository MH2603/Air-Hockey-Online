using System;
using System.Collections.Generic;
using MH.Common;
using UnityEngine;

namespace MH.UI
{
    /// <summary>
    /// Central UI entry: at <see cref="Start"/> instantiates configured view prefabs (inactive), then applies
    /// <see cref="UIView.ShowWhenStart"/>; also supports <see cref="RegisterPrefab{TView}"/> / <see cref="RegisterInstance{TView}"/>,
    /// <see cref="Show{TView}"/> / <see cref="Hide{TView}"/>, window stack, and Escape (in <see cref="Update"/>).
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        public Transform HUDParent;
        public Transform WindowParent;

        [SerializeField] private List<UIView> _viewPrefabs = new();

        private readonly Dictionary<Type, UIView> _prefabs = new();
        private readonly Dictionary<Type, UIView> _instances = new();
        private readonly List<UIView> _windowStack = new();

        private void Start()
        {
            InstantiateViewPrefabs();
            ApplyShowWhenStart();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                HandleEscapeInput();
        }

        public void RegisterPrefab<TView>(TView prefab) where TView : UIView
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            _prefabs[typeof(TView)] = prefab;
        }

        /// <summary>Registers an existing instance (e.g. placed in scene). It is deactivated after registration.</summary>
        public void RegisterInstance<TView>(TView instance) where TView : UIView
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            var type = typeof(TView);
            _instances[type] = instance;
            instance.gameObject.SetActive(false);
        }

        public TView Get<TView>() where TView : UIView
        {
            return TryGet<TView>(out var view) ? view : default;
        }

        public bool TryGet<TView>(out TView view) where TView : UIView
        {
            if (_instances.TryGetValue(typeof(TView), out var v))
            {
                view = (TView)v;
                return true;
            }

            view = default;
            return false;
        }

        public void Show<TView>() where TView : UIView
        {
            ShowInstance(GetOrCreate<TView>());
        }

        public void Hide<TView>() where TView : UIView
        {
            if (!TryGet<TView>(out var view))
                return;

            if (view.Layer == UILayer.Window)
                _windowStack.Remove(view);

            view.Hide();
        }

        public void PopWindow()
        {
            if (_windowStack.Count == 0)
                return;

            var top = _windowStack[_windowStack.Count - 1];
            _windowStack.RemoveAt(_windowStack.Count - 1);
            top.Hide();
        }

        public void ClearWindows()
        {
            for (var i = _windowStack.Count - 1; i >= 0; i--)
                _windowStack[i].Hide();
            _windowStack.Clear();
        }

        public void HandleEscapeInput()
        {
            PopWindow();
        }

        private void PushOrBringToFront(UIView window)
        {
            if (_windowStack.Contains(window))
                _windowStack.Remove(window);
            _windowStack.Add(window);
        }

        private void ShowInstance(UIView view)
        {
            if (view.Layer == UILayer.Window)
                PushOrBringToFront(view);
            view.Show();
        }

        private void InstantiateViewPrefabs()
        {
            if (_viewPrefabs == null || _viewPrefabs.Count == 0)
                return;

            foreach (var prefab in _viewPrefabs)
            {
                if (prefab == null)
                    continue;

                var type = prefab.GetType();
                if (_instances.ContainsKey(type))
                {
                    Debug.LogWarning(
                        $"UIManager: duplicate prefab type '{type.Name}' in {nameof(_viewPrefabs)}; skipping duplicate.");
                    continue;
                }

                var parent = prefab.Layer == UILayer.Window ? WindowParent : HUDParent;
                if (parent == null)
                {
                    Debug.LogError(
                        $"UIManager: cannot instantiate '{prefab.name}' — assign {(prefab.Layer == UILayer.Window ? nameof(WindowParent) : nameof(HUDParent))}.");
                    continue;
                }

                var go = UnityEngine.Object.Instantiate(prefab.gameObject, parent);
                if (!go.TryGetComponent(type, out var comp) || comp is not UIView instance)
                {
                    UnityEngine.Object.Destroy(go);
                    Debug.LogError($"UIManager: prefab '{prefab.name}' is missing a {type.Name} component on the root.");
                    continue;
                }

                go.SetActive(false);
                _instances[type] = instance;
            }
        }

        private void ApplyShowWhenStart()
        {
            foreach (var view in _instances.Values)
            {
                if (view != null && view.ShowWhenStart)
                    ShowInstance(view);
            }
        }

        private TView GetOrCreate<TView>() where TView : UIView
        {
            var type = typeof(TView);
            if (_instances.TryGetValue(type, out var existing))
                return (TView)existing;

            if (!_prefabs.TryGetValue(type, out var prefab))
                throw new InvalidOperationException(
                    $"UIView type {type.Name} is not registered. Use RegisterPrefab or RegisterInstance.");

            var parent = prefab.Layer == UILayer.Window ? WindowParent : HUDParent;
            if (parent == null)
                throw new InvalidOperationException(
                    $"UIManager has no {(prefab.Layer == UILayer.Window ? nameof(WindowParent) : nameof(HUDParent))}. Assign it before showing a registered prefab.");

            var go = UnityEngine.Object.Instantiate(prefab.gameObject, parent);
            var instance = go.GetComponent<TView>();
            if (instance == null)
            {
                UnityEngine.Object.Destroy(go);
                throw new InvalidOperationException(
                    $"Prefab for {type.Name} is missing component {type.Name}.");
            }

            go.SetActive(false);
            _instances[type] = instance;
            return instance;
        }
    }
}
