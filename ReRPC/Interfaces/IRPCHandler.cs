namespace ReRPC.Interfaces
{
	/// <summary>
	/// Represents an RPC Handler
	/// </summary>
	public interface IRPCHandler
	{
		string Name { get; }
		public Type[] Parameters { get; }

		public Type ReturnType { get; }

		public bool IsAsync { get; }

		public bool HasReturn { get; }

		Task<object?> Execute(object?[]? parameters);
	}
}
