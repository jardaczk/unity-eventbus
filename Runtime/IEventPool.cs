namespace EventBusSystem
{
	/// <summary>
	/// Interface for a generic event pool.
	/// </summary>
	public interface IEventPool
	{
		/// <summary>
		/// Gets an object from the pool.
		/// </summary>
		/// <returns>An object from the pool.</returns>
		object Get();

		/// <summary>
		/// Returns an object to the pool.
		/// </summary>
		/// <param name="item">The object to return.</param>
		void Return(object item);

		/// <summary>
		/// Clears the pool and removes all objects.
		/// </summary>
		void Clear();
	}
}
