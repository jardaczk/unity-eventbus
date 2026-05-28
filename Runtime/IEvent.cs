namespace EventBusSystem
{

	/// <summary>
	/// Represents an event that can be reset to its initial state.
	/// </summary>
	/// <remarks>This interface is commonly used in scenarios involving object pooling or reusable components.
	/// Implementations of <see cref="IEvent"/> should define the behavior of the <see cref="Reset"/> method to ensure the
	/// event is returned to a clean state for reuse.</remarks>
	public interface IEvent
	{
		void Reset(); // Pro object pooling
	}

}
