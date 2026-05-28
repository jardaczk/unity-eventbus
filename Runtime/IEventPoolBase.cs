namespace DemonDragon.EventBus
{
	/// <summary>
	/// Non-generic base interface for event object pooling.
	/// </summary>
	public interface IEventPoolBase
	{
		/// <summary>
		/// Gets an object from the pool.
		/// </summary>
		/// <returns>An object from the pool.</returns>
		object GetObject();

		/// <summary>
		/// Returns an object to the pool.
		/// </summary>
		/// <param name="item">The object to return.</param>
		void ReturnObject(object item);

		/// <summary>
		/// Clears the pool and removes all objects.
		/// </summary>
		void Clear();

		/// <summary>
		/// Pre-warms the pool by pre-allocating the specified number of instances.
		/// </summary>
		/// <param name="count">The number of instances to pre-allocate.</param>
		void Prewarm(int count);

		/// <summary>
		/// Gets the current size of the pool.
		/// </summary>
		int PoolSize { get; }
	}
}

