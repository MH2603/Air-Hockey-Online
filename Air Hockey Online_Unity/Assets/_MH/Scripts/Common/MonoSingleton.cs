using UnityEngine;

namespace MH.Common
{
    /// <summary>
    /// One <see cref="MonoBehaviour"/> instance per application; persists across scenes via <see cref="DontDestroyOnLoad"/>.
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static bool _applicationIsQuitting;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        var go = new GameObject(typeof(T).Name);
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<T>();
                    }
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }
}
