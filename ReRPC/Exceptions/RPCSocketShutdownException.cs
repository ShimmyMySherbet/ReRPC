namespace ReRPC.Exceptions
{
	/// <summary>
	/// Raised when the ReRPC Socket is shutdown while a query was still pending
	/// </summary>
	public class RPCSocketShutdownException : Exception
	{
		public RPCSocketShutdownException() : base("The ReRPC Socket shut down before a response was received from the remote client")
		{
		}
	}
}
