using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    public class ServiceLocator
    {
        private static Dictionary<Type, object> _serviceMap = new () ;

        public static T GetService<T>()
        {
            if (_serviceMap.ContainsKey(typeof(T)))
            {
                return (T)_serviceMap[typeof(T)] ;
            }

            LogError($"Not found type {typeof(T).Name} in Service Locator");
            return default(T);  
        }

        public static void RegisterService<T>(T service)
        {
            if ( _serviceMap.ContainsKey(typeof(T)))
            {
                LogError($"{typeof(T).Name} was registed !!!");
                return;
            }

            _serviceMap[typeof(T)] = service;
        }

        public static void UnregisterService<T>()
        {
            if ( _serviceMap.ContainsKey(typeof(T)))
            {
                _serviceMap.Remove(typeof(T));
            }
            else
            {
                LogError($"{typeof(T).Name} can't be unregisted bacause it was not registed !!!");
            }
        }

        static void LogError(string content)
        {
            Logger.Log($"[Error] [Service Locator] {content}", ELogLevel.Error);
        }
    }

