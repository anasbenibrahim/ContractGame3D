using System;
using System.Collections.Generic;

namespace Contract.Core
{
    /// <summary>
    /// Generic service locator used by GameManager to expose typed services
    /// (save, audio, missions, NPCs, map, economy, UI) without tight coupling.
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public T Get<T>()
        {
            if (services.TryGetValue(typeof(T), out object value) && value is T typed)
            {
                return typed;
            }

            return default;
        }

        public bool TryGet<T>(out T service)
        {
            if (services.TryGetValue(typeof(T), out object value) && value is T typed)
            {
                service = typed;
                return true;
            }

            service = default;
            return false;
        }

        public bool Has<T>()
        {
            return services.ContainsKey(typeof(T));
        }

        public void Register<T>(T service)
        {
            services[typeof(T)] = service;
        }

        public bool Unregister<T>()
        {
            return services.Remove(typeof(T));
        }

        public void Clear()
        {
            services.Clear();
        }
    }
}