namespace ReRPC.Exceptions
{
	/// <summary>
	/// Represents an exception or cancellation of a request on a remote client
	/// </summary>
	public class RPCRemoteException : Exception
	{
		public RPCRemoteException() : base("The remote client encountered a problem while processing the RPC call, and cannot fulfil the response")
		{
		}
	}
}
