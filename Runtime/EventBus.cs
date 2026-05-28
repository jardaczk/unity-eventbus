// 4. EventBus implementace - optimalizované s multicast delegates
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EventBusSystem
{
    /// <summary>
    /// Central event bus for subscribing, publishing, and pooling events.
    /// Implements <see cref="IEventBusDiagnostics"/> for runtime introspection.
    /// Optimized for Unity with automatic cleanup of destroyed MonoBehaviour handlers.
    /// </summary>
    public class EventBus : IEventBus, IEventBusDiagnostics
    {
        private readonly Dictionary<Type, Delegate> _subscriptions =
            new Dictionary<Type, Delegate>();

        private readonly Dictionary<Type, IEventPoolBase> _eventPools =
            new Dictionary<Type, IEventPoolBase>();

        // Tracks which event instances were obtained via GetPooledEvent to prevent double-return.
        private readonly HashSet<object> _pooledInstances = new HashSet<object>();

        // Cache main thread ID for thread validation.
        private readonly int _mainThreadId;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        public EventBus()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Subscribes a handler to the specified event type.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="handler">The handler to subscribe.</param>
        public void Subscribe<T>(Action<T> handler) where T : class, IEvent
        {
            ValidateMainThread();

            if (handler == null)
            {
                Debug.LogWarning($"[EventBus] Attempted to subscribe null handler for {typeof(T).Name}");
                return;
            }

            var eventType = typeof(T);

            if (_subscriptions.TryGetValue(eventType, out var existingHandler))
                _subscriptions[eventType] = (Action<T>)existingHandler + handler;
            else
                _subscriptions[eventType] = handler;
        }

        /// <summary>
        /// Unsubscribes a handler from the specified event type.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="handler">The handler to unsubscribe.</param>
        public void Unsubscribe<T>(Action<T> handler) where T : class, IEvent
        {
            ValidateMainThread();

            if (handler == null) return;

            var eventType = typeof(T);

            if (_subscriptions.TryGetValue(eventType, out var existingHandler))
            {
                var newHandler = (Action<T>)existingHandler - handler;

                if (newHandler == null)
                    _subscriptions.Remove(eventType);
                else
                    _subscriptions[eventType] = newHandler;
            }
        }

        /// <summary>
        /// Publishes an event to all subscribed handlers.
        /// If the event was obtained via <see cref="GetPooledEvent{T}"/>, it is automatically returned to the pool after publishing.
        /// Automatically skips and cleans up destroyed MonoBehaviour handlers.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="eventObj">The event instance to publish.</param>
        public void Publish<T>(T eventObj) where T : class, IEvent
        {
            ValidateMainThread();

            var eventType = typeof(T);

            if (_subscriptions.TryGetValue(eventType, out var handlers))
                InvokeHandlersSafely<T>(handlers, eventObj, eventType);

            ReturnToPoolInternal(eventObj);
        }

        /// <summary>
        /// Publishes an event without returning it to the pool.
        /// Use this when you need to reuse the event instance after publishing.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="eventObj">The event instance to publish.</param>
        public void PublishWithoutAutoReturn<T>(T eventObj) where T : class, IEvent
        {
            ValidateMainThread();

            var eventType = typeof(T);

            if (_subscriptions.TryGetValue(eventType, out var handlers))
                InvokeHandlersSafely<T>(handlers, eventObj, eventType);
        }

        private void InvokeHandlersSafely<T>(Delegate handlers, T eventObj, Type eventType) where T : class, IEvent
        {
            var invocationList = handlers.GetInvocationList();
            bool hasDeadHandlers = false;

            foreach (var handler in invocationList)
            {
                // Unity-specific null check — UnityEngine.Object overrides == operator.
                if (handler.Target is UnityEngine.Object unityObj && unityObj == null)
                {
                    hasDeadHandlers = true;
                    continue;
                }

                try
                {
                    ((Action<T>)handler).Invoke(eventObj);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventBus] Error in handler for {eventType.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            if (hasDeadHandlers)
                CleanupDeadHandlers<T>(eventType);
        }

        private void CleanupDeadHandlers<T>(Type eventType) where T : class, IEvent
        {
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
                return;

            Action<T> cleanedHandler = null;

            foreach (var handler in handlers.GetInvocationList())
            {
                bool isDead = handler.Target is UnityEngine.Object unityObj && unityObj == null;

                if (!isDead)
                {
                    if (cleanedHandler == null)
                        cleanedHandler = (Action<T>)handler;
                    else
                        cleanedHandler += (Action<T>)handler;
                }
            }

            if (cleanedHandler == null)
                _subscriptions.Remove(eventType);
            else
                _subscriptions[eventType] = cleanedHandler;

            Debug.LogWarning($"[EventBus] Cleaned up destroyed handlers for {eventType.Name}. Consider calling Unsubscribe in OnDestroy.");
        }

        private void ValidateMainThread()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
#if UNITY_EDITOR
                throw new InvalidOperationException("[EventBus] EventBus methods must be called from the main thread!");
#else
                Debug.LogError("[EventBus] EventBus methods must be called from the main thread!");
#endif
            }
#endif
        }

        /// <summary>
        /// Gets an event instance from the pool or creates a new one if the pool is empty.
        /// The returned instance is tracked and will be automatically returned to the pool by <see cref="Publish{T}"/>.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <returns>A pooled or new event instance.</returns>
        public T GetPooledEvent<T>() where T : class, IEvent, new()
        {
            var eventType = typeof(T);

            if (!_eventPools.TryGetValue(eventType, out var pool))
            {
                pool = new EventPool<T>();
                _eventPools[eventType] = pool;
            }

            var instance = (T)pool.GetObject();
            _pooledInstances.Add(instance);
            return instance;
        }

        /// <summary>
        /// Returns an event instance to its pool.
        /// Note: Called automatically by <see cref="Publish{T}"/>. Only call manually when using <see cref="PublishWithoutAutoReturn{T}"/>.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="eventObj">The event instance to return.</param>
        public void ReturnToPool<T>(T eventObj) where T : class, IEvent
        {
            ReturnToPoolInternal(eventObj);
        }

        private void ReturnToPoolInternal<T>(T eventObj) where T : class, IEvent
        {
            if (eventObj == null) return;

            // Only return instances that were obtained via GetPooledEvent to prevent double-return.
            if (!_pooledInstances.Remove(eventObj)) return;

            var eventType = typeof(T);

            if (_eventPools.TryGetValue(eventType, out var pool))
                pool.ReturnObject(eventObj);
        }

        /// <summary>
        /// Pre-warms the pool for the specified event type by pre-allocating instances.
        /// Call this at scene/game startup to avoid allocations during hot paths.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="count">The number of instances to pre-allocate.</param>
        public void PrewarmPool<T>(int count) where T : class, IEvent, new()
        {
            var eventType = typeof(T);

            if (!_eventPools.TryGetValue(eventType, out var pool))
            {
                pool = new EventPool<T>();
                _eventPools[eventType] = pool;
            }

            pool.Prewarm(count);
        }

        /// <summary>
        /// Clears all event pools and removes all pooled objects.
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _eventPools.Values)
                pool.Clear();

            _eventPools.Clear();
            _pooledInstances.Clear();
        }

        /// <summary>
        /// Clears all event subscriptions.
        /// </summary>
        public void ClearAllSubscriptions()
        {
            _subscriptions.Clear();
        }

        // ─── IEventBusDiagnostics ────────────────────────────────────────────────

        /// <summary>
        /// Gets the number of subscribers for the specified event type.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <returns>The number of subscribers.</returns>
        public int GetSubscriberCount<T>() where T : class, IEvent
        {
            var eventType = typeof(T);
            return _subscriptions.TryGetValue(eventType, out var handlers)
                ? handlers.GetInvocationList().Length
                : 0;
        }

        /// <summary>
        /// Gets the current size of the pool for the specified event type.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <returns>The number of pooled objects.</returns>
        public int GetPoolSize<T>() where T : class, IEvent
        {
            var eventType = typeof(T);
            return _eventPools.TryGetValue(eventType, out var pool) ? pool.PoolSize : 0;
        }

        /// <summary>
        /// Gets the sizes of all active event pools.
        /// </summary>
        /// <returns>A dictionary mapping event type names to pool sizes.</returns>
        public Dictionary<string, int> GetAllPoolSizes()
        {
            var result = new Dictionary<string, int>();
            foreach (var kvp in _eventPools)
                result[kvp.Key.Name] = kvp.Value.PoolSize;
            return result;
        }

        /// <summary>
        /// Gets all event types that currently have at least one subscriber.
        /// </summary>
        /// <returns>An array of subscribed event types.</returns>
        public Type[] GetSubscribedEventTypes()
        {
            return _subscriptions.Keys.ToArray();
        }

        /// <summary>
        /// Returns a string representation of the EventBus including subscribed event types and pool sizes.
        /// </summary>
        public override string ToString()
        {
            var subscribedEvents = GetSubscribedEventTypes();
            var poolSizes = GetAllPoolSizes();
            var eventTypesString = $"Types of subscribed events: [{string.Join(", ", subscribedEvents.Select(t => t.Name))}]";
            var poolSizesString = $"Event Types Pool sizes: [{string.Join(", ", poolSizes.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}]";
            return $"{eventTypesString}, {poolSizesString}";
        }
    }
}
