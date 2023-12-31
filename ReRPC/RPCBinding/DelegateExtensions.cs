﻿using System.Reflection;

namespace ReRPC.RPCBinding
{
	public static class DelegateExtensions
	{
		public static async Task<object?> EvaluateAsync(this Delegate del, params object?[]? parameters)
		{
			DelegateTools.GetReturnTypeInfo(del.Method.ReturnType, out var isAsync, out _, out var returns, out var resultPropInfo);

			var res = del.DynamicInvoke(parameters);

			if (isAsync && res is Task tsk)
			{
				await tsk;
				if (returns)
				{
					if (resultPropInfo == null)
					{
						return null;
					}
					var taskRes = resultPropInfo.GetValue(tsk);
					return taskRes;
				}
				return null;
			}
			else
			{
				return res;
			}
		}

		public static object? EvaluateBlocking(this Delegate del, params object?[]? parameters)
		{
			return del.EvaluateAsync(parameters).GetAwaiter().GetResult();
		}

		public static async Task<object?> EvaluateAsync(this MethodInfo del, object instance, params object?[]? parameters)
		{
			DelegateTools.GetReturnTypeInfo(del.ReturnType, out var isAsync, out _, out var returns, out var resultPropInfo);

			var res = del.Invoke(instance, parameters: parameters);

			if (isAsync && res is Task tsk)
			{
				await tsk;
				if (returns)
				{
					if (resultPropInfo == null)
					{
						return null;
					}
					var taskRes = resultPropInfo.GetValue(tsk);
					return taskRes;
				}
				return null;
			}
			else
			{
				return res;
			}
		}

		public static object? EvaluateBlocking(this MethodInfo del, object instance, params object?[]? parameters)
		{
			return del.EvaluateAsync(instance, parameters: parameters).GetAwaiter().GetResult();
		}
	}
}
