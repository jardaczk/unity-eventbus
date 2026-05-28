namespace EventBusSystem
{
	/// <summary>
	/// Defines the contract for an event bus that supports publishing, subscribing, and pooling events.
	/// </summary>
	public interface IEventBus
	{
		/// <summary>
		/// Publishes an event to all subscribed handlers.
		/// If the event was obtained via GetPooledEvent, it will be automatically returned to the pool after publishing.
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <param name="eventObj">The event instance to publish.</param>
		void Publish<T>(T eventObj) where T : class, IEvent;

		/// <summary>
		/// Publishes an event without returning it to the pool.
		/// Use this when you need to reuse the event instance after publishing.
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <param name="eventObj">The event instance to publish.</param>
		void PublishWithoutAutoReturn<T>(T eventObj) where T : class, IEvent;

		/// <summary>
		/// Subscribes a handler to the specified event type.
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <param name="handler">The handler to subscribe.</param>
		void Subscribe<T>(System.Action<T> handler) where T : class, IEvent;

		/// <summary>
		/// Unsubscribes a handler from the specified event type.
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <param name="handler">The handler to unsubscribe.</param>
		void Unsubscribe<T>(System.Action<T> handler) where T : class, IEvent;

		/// <summary>
		/// Gets an event instance from the pool or creates a new one if the pool is empty.
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <returns>A pooled or new event instance.</returns>
		T GetPooledEvent<T>() where T : class, IEvent, new();

		/// <summary>
		/// Returns an event instance to its pool.
		/// Note: This is called automatically by Publish(). Only call manually if using PublishWithoutAutoReturn().
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <param name="eventObj">The event instance to return.</param>
		void ReturnToPool<T>(T eventObj) where T : class, IEvent;

		/// <summary>
		/// Pre-warms the pool for the specified event type by pre-allocating instances.
		/// Call this at scene/game startup to avoid allocations in hot paths.
		/// </summary>
		/// <typeparam name="T">The event type.</typeparam>
		/// <param name="count">The number of instances to pre-allocate.</param>
		void PrewarmPool<T>(int count) where T : class, IEvent, new();

		/// <summary>
		/// Clears all event subscriptions.
		/// </summary>
		void ClearAllSubscriptions();

		/// <summary>
		/// Clears all event pools and removes all pooled objects.
		/// </summary>
		void ClearAllPools();
	}
}
