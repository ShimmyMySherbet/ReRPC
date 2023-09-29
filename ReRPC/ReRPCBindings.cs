using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ReRPC.RPCBinding;
using ReRPC.RPCBinding.Attributes;

namespace ReRPC
{
	public partial class ReRPCClient
	{
		public ReRPCHandlerRegistry HandlerRegistry { get; } = new ReRPCHandlerRegistry();

		/// <summary>
		/// Deregisters all RPC handlers associated with this socket
		/// </summary>
		public void DeregisterAll()
		{
			HandlerRegistry.Clear();
		}

		/// <summary>
		/// Registers all RPC handlers from a class instance.
		/// </summary>
		/// <remarks>
		/// RPC Handlers are decorated with <seealso cref="RPCAttribute"/>, can be synchronous, asynchronous, and return any serializable type, and include any number of serializable arguments
		/// </remarks>
		/// <typeparam name="T">The type to register handlers from</typeparam>
		/// <param name="instance">The instance of the type to register handlers from</param>

		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public virtual void RegisterFrom<T>(T instance) where T : class
		{
			var methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
			if (methods == null)
			{
				return;
			}
			foreach (var method in methods)
			{
				if (method == null) continue;
				if (method.GetCustomAttributes<RPCAttribute>().Any())
				{
					var del = DelegateFactory.CreateDelegate(method, instance);
					if (del != null)
					{
						foreach (var rpcAttrib in method.GetCustomAttributes<RPCAttribute>())
						{
							var name = rpcAttrib.MethodName ?? method.Name;
							var handler = new GlobalDelegateHandler(name, del);
							HandlerRegistry.Register(name, handler);
						}
					}
					else
					{
						Debug.WriteLine($"[WARN] Failed to generate delegate handler for RPC {method.DeclaringType?.FullName}::{method.Name}");
					}
				}
			}
		}
		/// <summary>
		/// Creates a delegate handle for the remote RPC, using the method name attributed to the delegate type.
		/// Uses <see cref="RPCAttribute"/> from the delegate type.
		/// </summary>
		/// <typeparam name="T">Delegate type tagged with <see cref="RPCAttribute"/></typeparam>
		/// <returns>RPC delegate handle</returns>
		public T GetRPC<T>() where T : Delegate
		{
			var attr = typeof(T).GetCustomAttribute<RPCAttribute>();
			if (attr == null || attr.MethodName == null)
			{
				throw new ArgumentException("Delegate is not tagged with remote RPC name. Use GetRPC<T>(string method) to specify remote RPC name.");
			}
			else
			{
				return GetRPC<T>(attr.MethodName);
			}
		}

		/// <summary>
		/// Creates a delegate handle for the remote RPC, using the specified remote RPC name.
		/// </summary>
		/// <typeparam name="T">Delegate type</typeparam>
		/// <param name="method">Name of the remote RPC method</param>
		/// <returns>RPC delegate handle</returns>
		public T GetRPC<T>(string method) where T : Delegate
		{
			return (T)GetRPC(method, typeof(T));
		}

		/// <summary>
		/// Creates an implementation of a delegate and binds it to a remote RPC method
		/// </summary>
		/// <remarks>
		/// This method is typically only used internally by ReRPC, but can be used in conjunction with reflection.
		/// </remarks>
		/// <param name="method">The method name to bind to</param>
		/// <param name="delegateType">The delegate type to generate an implementation of</param>
		/// <returns>A delegate instance bound to the remote method</returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="InvalidCastException"></exception>
		/// <exception cref="ArgumentException"></exception>
		public Delegate GetRPC(string method, Type delegateType)
		{
			if (DelegateTools.GetDelegateInfo(delegateType, out var ReturnType, out var delegateParameters))
			{
				DelegateTools.GetReturnTypeInfo(ReturnType, out var delegateIsAsync, out var delegateReturnType, out bool delegateReturns, out _);

				foreach (var proxyClass in Assembly.GetExecutingAssembly().GetTypes().Where(x => typeof(RPCProxy).IsAssignableFrom(x)))
				{
					var proxyMethod = proxyClass.GetMethod("Execute", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (proxyMethod == null) continue;
					DelegateTools.GetReturnTypeInfo(proxyMethod.ReturnType, out var proxyIsAsync, out var proxyReturnType, out var proxyReturns, out _);

					var proxyParameters = proxyMethod.GetParameters();
					var proxyGenerics = proxyClass.GetGenericArguments();

					if (proxyParameters.Length == delegateParameters.Length)
					{
						if (delegateIsAsync != proxyIsAsync)
							continue;
						if (delegateReturns != proxyReturns)
							continue;

						var buildGenerics = new Type[proxyGenerics.Length];

						var compatible = false;

						if (proxyReturnType == delegateReturnType)
						{
							compatible = true;
						}
						else if (proxyReturnType.IsGenericParameter)
						{
							buildGenerics[proxyReturnType.GenericParameterPosition] = delegateReturnType;
							compatible = true;
						}
						else
						{
							// bad return
							compatible = false;
						}

						if (compatible)
						{
							for (int i = 0; i < delegateParameters.Length; i++)
							{
								var proxyParam = proxyParameters[i];
								var delegateParam = delegateParameters[i];

								if (proxyParam.ParameterType.IsGenericParameter)
								{
									var existing = buildGenerics[proxyParam.ParameterType.GenericParameterPosition];

									if (existing == null)
									{
										buildGenerics[proxyParam.ParameterType.GenericParameterPosition] = delegateParam.ParameterType;
									}
									else if (existing != delegateParam.ParameterType)
									{
										// generic is consumed and of a different typpe
										compatible = false;
										break;
									}
								}
								else if (proxyParam.ParameterType != delegateParam.ParameterType)
								{
									// incompatible non-generic type
									compatible = false;
									break;
								}
							}
						}

						if (compatible)
						{
							for (int i = 0; i < buildGenerics.Length; i++)
							{
								if (buildGenerics[i] == null)
								{
									// incomplete generic set.
									compatible = false;
								}
							}

							if (!compatible)
								continue;

							Type proxyClassBuildType;
							if (buildGenerics.Length > 0)
							{
								proxyClassBuildType = proxyClass.MakeGenericType(typeArguments: buildGenerics);
							}
							else if (!proxyClass.ContainsGenericParameters)
							{
								proxyClassBuildType = proxyClass;
							}
							else
							{
								// Delegate has generic parameters that are not accounted for.
								compatible = false;
								continue;
							}

							var activationArguments = new object[] { this, method, delegateType };
							RPCProxy? proxyInstance;

							try
							{
								proxyInstance = (RPCProxy?)Activator.CreateInstance(proxyClassBuildType, args: activationArguments);
								if (proxyInstance == null)
									throw new InvalidOperationException();
							}
							catch (Exception) // catch delegate activation exceptions
							{
								compatible = false;
								continue;
							}

							try
							{
								var delegateBinding = Delegate.CreateDelegate(delegateType, proxyInstance, "Execute", true, true);

								if (delegateBinding == null)
									throw new InvalidOperationException();

								return delegateBinding;
							}
							catch (Exception) // catch delegate binding and casting exceptions
							{
								compatible = false;
								continue;
							}
						}
					}
				}
			}
			else
			{
				throw new InvalidCastException("T has to be a valid delegate");
			}
			throw new ArgumentException("Failed to bind proxy delegate for specified delegate type");
		}
	}
}
