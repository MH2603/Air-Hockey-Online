using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    /// <typeparam name="T">The class type to be singleton</typeparam>
    public abstract class Singleton<T> where T : class, new()
    {
        /// <summary>
        /// Lazy instance ensures:
        /// - Thread safety
        /// - Lazy initialization (created only when accessed)
        /// </summary>
        private static readonly Lazy<T> _instance = new Lazy<T>(() =>
        {
            return new T();
        });

        /// <summary>
        /// Global access point to the singleton instance.
        /// </summary>
        public static T Instance => _instance.Value;

        /// <summary>
        /// Protected constructor prevents external instantiation.
        /// Ensures only derived classes can call it.
        /// </summary>
        protected Singleton()
        {
            // Optional: Guard against reflection-based multiple instantiation
            if (_instance.IsValueCreated)
            {
                throw new InvalidOperationException(
                    $"Singleton instance of type {typeof(T)} already exists."
                );
            }
        }
    }


