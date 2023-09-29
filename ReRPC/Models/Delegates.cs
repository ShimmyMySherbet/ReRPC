using ReRPC.Interfaces;

namespace ReRPC.Models.Delegates
{
	/// <summary>
	/// Raised when a socket is faulted and has to shut down
	/// </summary>
	/// <param name="socket">The socket that faulted</param>
	/// <param name="exception">If applicable, the exception that caused the socket to exit</param>
	public delegate void OnSocketFault(ReRPCClient socket, Exception? exception);

	/// <summary>
	/// Raised when an RPC handler errors
	/// </summary>
	/// <param name="handler">The handler that errored</param>
	/// <param name="exception">The unhandled exception the handler raised</param>
	public delegate void OnSocketHandlerError(IRPCHandler handler, Exception exception);
}
