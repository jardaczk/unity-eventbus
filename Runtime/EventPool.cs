// 3. Generický Event Pool - implementuje base interface
using System.Collections.Generic;

namespace DemonDragon.EventBus
{
	/// <summary>
	/// Generic event pool for pooling and reusing event objects of a specific type.
	/// Uses a Stack for better cache locality (LIFO — recently returned objects stay warm).
	/// </summary>
	/// <typeparam name="T">The event type.</typeparam>
	public class EventPool<T> : IEventPoolBase where T : class, IEvent, new()
	{
		private readonly Stack<T> _pool = new Stack<T>();
		private readonly int _maxSize;

		/// <summary>
		/// Gets the current size of the pool.
		/// </summary>
		public int PoolSize => _pool.Count;

		/// <summary>
		/// Initializes a new instance of the <see cref="EventPool{T}"/> class.
		/// </summary>
		/// <param name="maxSize">The maximum size of the pool.</param>
		public EventPool(int maxSize = 50)
		{
			_maxSize = maxSize;
		}

		/// <summary>
		/// Pre-warms the pool by pre-allocating the specified number of instances.
		/// </summary>
		/// <param name="count">The number of instances to pre-allocate.</param>
		public void Prewarm(int count)
		{
			for (int i = 0; i < count && _pool.Count < _maxSize; i++)
				_pool.Push(new T());
		}

		/// <summary>
		/// Gets an event object from the pool or creates a new one if the pool is empty.
		/// </summary>
		/// <returns>A pooled or new event object.</returns>
		public T Get()
		{
			return _pool.Count > 0 ? _pool.Pop() : new T();
		}

		/// <summary>
		/// Returns an event object to the pool.
		/// </summary>
		/// <param name="item">The event object to return.</param>
		public void Return(T item)
		{
			if (item == null || _pool.Count >= _maxSize)
				return;

			item.Reset();
			_pool.Push(item);
		}

		/// <summary>
		/// Clears the pool and resets all pooled objects.
		/// </summary>
		public void Clear()
		{
			while (_pool.Count > 0)
				_pool.Pop().Reset();
		}

		/// <summary>
		/// Gets an object from the pool (non-generic interface implementation).
		/// </summary>
		/// <returns>An object from the pool.</returns>
		object IEventPoolBase.GetObject()
		{
			return Get();
		}

		/// <summary>
		/// Returns an object to the pool (non-generic interface implementation).
		/// </summary>
		/// <param name="item">The object to return.</param>
		void IEventPoolBase.ReturnObject(object item)
		{
			if (item is T typedItem)
				Return(typedItem);
		}
	}
}

