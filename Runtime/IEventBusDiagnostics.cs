using System;
using System.Collections.Generic;

namespace DemonDragon.EventBus
{
    /// <summary>
    /// Provides diagnostic and lifecycle management capabilities for an event bus.
    /// Implement alongside <see cref="IEventBus"/> to expose debugging and introspection APIs
    /// without polluting the core event bus interface.
    /// </summary>
    public interface IEventBusDiagnostics
    {
        /// <summary>
        /// Gets the number of subscribers for the specified event type.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <returns>The number of subscribers.</returns>
        int GetSubscriberCount<T>() where T : class, IEvent;

        /// <summary>
        /// Gets the current size of the pool for the specified event type.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <returns>The number of pooled objects.</returns>
        int GetPoolSize<T>() where T : class, IEvent;

        /// <summary>
        /// Gets the sizes of all active event pools.
        /// </summary>
        /// <returns>A dictionary mapping event type names to their pool sizes.</returns>
        Dictionary<string, int> GetAllPoolSizes();

        /// <summary>
        /// Gets all event types that currently have at least one subscriber.
        /// </summary>
        /// <returns>An array of subscribed event types.</returns>
        Type[] GetSubscribedEventTypes();
    }
}
