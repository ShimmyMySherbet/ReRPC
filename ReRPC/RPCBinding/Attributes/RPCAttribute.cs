using System.ComponentModel;

namespace ReRPC.RPCBinding.Attributes
{
	/// <summary>
	/// Designates a method as an RPC Handler, or is used to specify the remote RPC name when decorated on a delegate;
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = true), DisplayName("RPC")]
	public class RPCAttribute : Attribute
	{
		public string? MethodName { get; }

		/// <param name="name">The name of the RPC Method</param>
		public RPCAttribute(string name)
		{
			MethodName = name;
		}

		public RPCAttribute()
		{
			MethodName = null;
		}
	}
}
