using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReRPC.RPCBinding
{
	public abstract class RPCProxy
	{
		public ReRPCClient RPC { get; }
		public string Method { get; }

		public Type DelegateHandler { get; }

		public MethodInfo ExecuteMethod { get; }

		protected RPCProxy(ReRPCClient rPC, string method, Type delegateHandler)
		{
			RPC = rPC;
			Method = method;
			DelegateHandler = delegateHandler;
			var m = GetType().GetMethod("Execute", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (m == null)
			{
				throw new InvalidOperationException($"RPCProxy {GetType().FullName} does not have an Execute() method.");
			}
			ExecuteMethod = m;
		}

		public Task<object?> EvaluateAsync(params object?[]? parameters)
		{
			return ExecuteMethod.EvaluateAsync(this, parameters);
		}
	}
}
