using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    /// <summary>
    /// Defines a generic object pool contract
    /// </summary>
    public interface IObjectPool<T>
    {
        T? Get();
        void Release(T item);
    }

    public class ObjectPool<T> : IObjectPool<T>
    {
        /// <summary>
        /// Queue storing pooled object references (FIFO)
        /// </summary>
        private readonly ConcurrentQueue<T> _pool = new();

        /// <summary>
        /// Factory method used when pool is empty
        /// </summary>
        private readonly Func<T> _factory;
        private Action<T> _releasCallback;

        public ObjectPool(int initCount, Func<T> funcCreate, Action<T> releaseCallBack)
        {
            
            _factory = funcCreate;

            for (int i = 0; i < initCount; i++)
            {
                T entity = _factory();
                _pool.Enqueue(entity);  
            }

            _releasCallback = releaseCallBack;
        }


        public T? Get()
        {
            if ( _pool.TryDequeue(out T? entity))
            {
                return entity;  
            }

            return default;
        }

        public void Release(T item)
        {
            _pool.Enqueue(item);
            _releasCallback?.Invoke(item);  
        }
    }
