using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using ReRPC.Exceptions;
using ReRPC.Models;
using ReRPC.Models.Delegates;

namespace ReRPC
{
	public partial class ReRPCClient
	{
		/// <summary>
		/// Raises when the socket enters a faulted state and has to shutdown.
		/// </summary>
		/// <remarks>
		/// Typically caused by the socket disconnecting from the remote client
		/// </remarks>
		public event OnSocketFault? Faulted;

		/// <summary>
		/// Raises when a local handler throws an unhandled exception
		/// </summary>
		public event OnSocketHandlerError? HandlerError;

		// [Message ID] : 4 Bytes (UInt)
		// [Invoke message Length] : 1 Byte
		//	 0 => [IsResponse]
		//   1+ =>
		//  [Method Name] : 1-255 Bytes
		// [Body length] : 2 Bytes (UShort)
		// [Body] : 0+ Bytes (Protobuf)

		private async Task ReadLoopWrapper(CancellationToken token)
		{
			try
			{
				await ReadLoop(token);
			}
			catch (Exception ex)
			{
				OnFault(ex);
				throw;
			}
		}

		private async Task ReadLoop(CancellationToken token)
		{

			var headerBuffer = new byte[4];
			var methodBuffer = new byte[255];
			var lengthBuffer = new byte[2];
			var methodLengthBuffer = new byte[2];
			var buffer = new byte[1024];

			var singleByte = new byte[1];

			while (!token.IsCancellationRequested)
			{
				m_Debug?.Invoke($"[Read] Reading Header...");
				var read = await m_Network.ReadAsync(headerBuffer, 0, 4, token);

				if (read < headerBuffer.Length)
				{
					OnFault();
					return;
				}

				var messageID = BitConverter.ToUInt32(headerBuffer, 0);
				m_Debug?.Invoke($"[Read][{messageID}] Reading message with ID: {messageID}");

				read = await m_Network.ReadAsync(methodLengthBuffer, 0, 2, token);
				if (read < 2)
				{
					OnFault();
					return;
				}

				var methodLength = BitConverter.ToUInt16(methodLengthBuffer, 0);

				string? methodName = null;

				if (methodLength > 0)
				{
					read = await m_Network.ReadAsync(methodBuffer, 0, methodLength, token);
					methodName = Encoding.UTF8.GetString(methodBuffer, 0, methodLength);
				}

				m_Debug?.Invoke($"[Read][{messageID}] Read Method Name: ({methodLength}:'{methodName}')");

				await m_Network.ReadAsync(singleByte, 0, 1, token);

				var count = singleByte[0];

				var isFault = count == byte.MaxValue;

				if (isFault)
					count = 0;

				m_Debug?.Invoke($"[Read][{messageID}] Arguments: {count}, isFault: {isFault}");

				var objects = new string[count];

				for (int i = 0; i < count; i++)
				{
					m_Debug?.Invoke($"[Read][{messageID}] Reading argument {i}...");

					read = await m_Network.ReadAsync(lengthBuffer, 0, 2, token);
					if (read < lengthBuffer.Length)
					{
						m_Debug?.Invoke($"[Read][{messageID}] Not Enough Data!");
						OnFault();
						return;
					}

					var bodyLength = BitConverter.ToUInt16(lengthBuffer, 0);

					m_Debug?.Invoke($"[Read][{messageID}|Arg{i}] Length: {bodyLength}");

					if (bodyLength == 0)
					{
						m_Debug?.Invoke($"[Read][{messageID}|Arg{i}] Err, 0 length!");
						OnFault();
						return;
					}

					var payLoadBuffer = new byte[bodyLength];

					await m_Network.ReadAsync(payLoadBuffer, 0, payLoadBuffer.Length, token);

					var payloadJson = Encoding.UTF8.GetString(payLoadBuffer);

					m_Debug?.Invoke($"[Read][{messageID}|Arg{i}] Read Json");
					objects[i] = payloadJson;

				}

				m_Debug?.Invoke($"[Read][{messageID}] Dispatching Message...");

				var response = new ReRPCResponse(messageID, methodName, objects, isFault);
				_ = Task.Run(async () => await DispatchResponse(response));
			}
		}

		private async Task DispatchResponse(ReRPCResponse message)
		{
			if (message.MethodName == null)
			{
				// response
				m_Debug?.Invoke($"[Dispatch][{message.MessageID}] IsResponse. Trying to find query...");
				if (m_Pending.TryRemove(message.MessageID, out var wait))
				{
					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Setting query result.");

					if (message.IsFault)
					{
						wait.SetException(new RPCRemoteException());
					} else
					{
						wait.SetResult(message);
					}

					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] done.");

				}
				else
				{
					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Failed to find query!");

				}
			}
			else
			{
				m_Debug?.Invoke($"[Dispatch][{message.MessageID}] IsQuery. Trying to find handler...");

				// Query, Dispatch to handler or discard.
				try
				{
					var handler = HandlerRegistry.GetHandler(message.MethodName);

					if (handler == null)
					{
						m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Failed to get handler!");

						return;
					}
					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Got Handler");

					var arguments = new object[handler.Parameters.Length];
					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Constructing {arguments.Length} arguments");

					for (int i = 0; i < arguments.Length; i++)
					{
						arguments[i] = message.ReadObject(handler.Parameters[i], i);
					}

					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Executing...");

					object? obj;
					try
					{
						obj = await handler.Execute(parameters: arguments);
					}
					catch (Exception ex)
					{
						// errored

						var cancelResponse = new ReRPCMessage(message.MessageID, null, Array.Empty<object>(), true);

						m_MessageQueue.Enqueue(cancelResponse);
						m_QueueSemaphore.Release();



						HandlerError?.Invoke(handler, ex);

						throw;
					}

					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Queueing Response...");

					var response = new ReRPCMessage(message.MessageID, null, new object[] { obj! });

					m_MessageQueue.Enqueue(response);
					m_QueueSemaphore.Release();
					m_Debug?.Invoke($"[Dispatch][{message.MessageID}] Done.");
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
					Debug.WriteLine(ex.StackTrace);
				}
			}
		}

		private async Task WriteLoopWrapper(CancellationToken token)
		{
			try
			{
				await WriteLoop(token);
			}
			catch (Exception ex)
			{
				OnFault(ex);
				throw;
			}
		}

		private async Task WriteLoop(CancellationToken token)
		{
			var singleByte = new byte[1];

			while (!token.IsCancellationRequested)
			{
				m_Debug?.Invoke($"[Write] Waiting for message");
				await m_QueueSemaphore.WaitAsync(token);
				if (!m_MessageQueue.TryDequeue(out var message))
				{
					continue;
				}

				m_Debug?.Invoke($"[Write] Got message with ID: {message.ID}");

				var messageID = BitConverter.GetBytes(message.ID);

				var methodName = message.MethodName != null ? Encoding.UTF8.GetBytes(message.MethodName) : Array.Empty<byte>();
				var methodLength = BitConverter.GetBytes((ushort)methodName.Length);

				m_Debug?.Invoke($"[Write][{message.ID}] Writing Message Header: [{message.ID}, {methodLength}:'{methodName}']");

				await m_Network.WriteAsync(messageID, 0, 4, token);                                    // [Message ID] : 4 Bytes
				await m_Network.WriteAsync(methodLength, 0, methodLength.Length, token);               // [Method Name Length] : 2 Bytes
				if (methodName.Length > 0)
					await m_Network.WriteAsync(methodName, 0, methodName.Length, token);               // [Method Name] : [Method Name Length] Bytes

				var argCount = (byte)message.Arguments.Length;

				if (message.CancelRequest)
				{
					argCount = byte.MaxValue;
				}


				singleByte[0] = argCount;

				await m_Network.WriteAsync(singleByte, 0, 1, token);

				if (message.CancelRequest)
				{
					argCount = 0;
				}
				m_Debug?.Invoke($"[Write] Writing {argCount} arguments...");

				for (int i = 0; i < argCount; i++)
				{
					var argument = message.Arguments[i];
					m_Debug?.Invoke($"[Write] Writing argument {i}");

					var payloadJson = JsonConvert.SerializeObject(argument);
					var payloadBuffer = Encoding.UTF8.GetBytes(payloadJson);

					var payloadLength = (ushort)payloadBuffer.Length;
					var payloadLengthBuffer = BitConverter.GetBytes(payloadLength);

					await m_Network.WriteAsync(payloadLengthBuffer, 0, payloadLengthBuffer.Length, token); // [Payload Length] : 2 Bytes
					await m_Network.WriteAsync(payloadBuffer, 0, payloadBuffer.Length, token);             // [Payload] : [Payload Length] Bytes
				}

				m_Debug?.Invoke($"[Write] Flushing...");
				await m_Network.FlushAsync(token);
				m_Debug?.Invoke($"[Write] Done.");
			}
		}

		private void OnFault(Exception? exception = null)
		{
			Stop();
			Faulted?.Invoke(this, exception);
		}
	}
}
