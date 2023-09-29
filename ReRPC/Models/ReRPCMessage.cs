namespace ReRPC.Models
{
	/// <summary>
	/// Represents an RPC message to be sent over the socket
	/// </summary>
	public readonly struct ReRPCMessage
	{
		public uint ID { get; }

		public string? MethodName { get; }

		public object[] Arguments { get; }

		public bool CancelRequest { get; } = false;

		public ReRPCMessage(uint iD, string? methodName, object[] arguments, bool cancel = false)
		{
			ID = iD;
			MethodName = methodName;
			Arguments = arguments;
			CancelRequest = cancel;
		}
	}
}
