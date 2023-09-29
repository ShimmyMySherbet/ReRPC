using System.Collections.Concurrent;
using ReRPC.Exceptions;
using ReRPC.Models;

namespace ReRPC
{
	/// <summary>
	/// ReRPC Socket Client. Provides a lightweight bi-directional and thread safe RPC socket.
	/// </summary>
	public partial class ReRPCClient : IDisposable
	{
		/// <summary>
		/// The number of pending outbound messages
		/// </summary>
		/// <remarks>
		/// During operation this number is typically 1 or 0.
		/// </remarks>
		public int Pending => m_MessageQueue.Count;

		private Stream m_Network;

		private ConcurrentQueue<ReRPCMessage> m_MessageQueue = new ConcurrentQueue<ReRPCMessage>();
		private SemaphoreSlim m_QueueSemaphore = new SemaphoreSlim(0);

		private ConcurrentDictionary<uint, TaskCompletionSource<ReRPCResponse>> m_Pending = new ConcurrentDictionary<uint, TaskCompletionSource<ReRPCResponse>>();

		private uint m_OutboundIndex = 1;

		private Action<string>? m_Debug;

		private CancellationTokenSource? m_Source;

		private bool m_DisposeStream;

		/// <summary>
		/// Creates a new instance of the ReRPC Client
		/// </summary>
		/// <param name="network">The network stream to read/write traffic to</param>
		/// <param name="disposeStream">Specifies if the network stream should be disposed when the socket is disposed</param>
		public ReRPCClient(Stream network/*, Action<string>? debug = null*/, bool disposeStream = true)
		{
			m_Network = network;
			m_DisposeStream = disposeStream;
			//m_Debug = debug;
		}

		/// <summary>
		/// Starts the socket client
		/// </summary>
		/// <param name="token">Lifetime token to stop the socket without needing to call Stop</param>
		public void Start(CancellationToken token = default)
		{
			if (m_Source != null)
			{
				m_Source.Cancel();
				m_Source.Dispose();
			}

			m_Source = CancellationTokenSource.CreateLinkedTokenSource(token);

			Task.Run(async () => await WriteLoopWrapper(m_Source.Token));
			Task.Run(async () => await ReadLoopWrapper(m_Source.Token));
		}

		/// <summary>
		/// Stops the socket, cancelling all pending queries in the process
		/// </summary>
		public void Stop()
		{
			var waits = m_Pending.Keys.ToArray();

			foreach (var key in waits)
			{
				m_Pending[key].TrySetException(new RPCSocketShutdownException());
			}

			m_Pending.Clear();

			if (m_Source == null)
			{
				return;
			}

			m_Source.Cancel();
			m_Source.Dispose();
			m_Source = null;
		}

		/// <summary>
		/// Manually queries the remote socket
		/// </summary>
		/// <typeparam name="T">The return type of the query</typeparam>
		/// <param name="method">The method name to call</param>
		/// <param name="parameters">Parameters for the remote rpc call</param>
		/// <returns>Result from the remote socket</returns>
		public async Task<T?> QueryAsync<T>(string method, params object[] parameters)
		{
			var id = m_OutboundIndex++;

			var message = new ReRPCMessage(id, method, parameters);

			var waiter = new TaskCompletionSource<ReRPCResponse>();

			m_Pending[id] = waiter;

			m_MessageQueue.Enqueue(message);
			m_QueueSemaphore.Release();

			var response = await waiter.Task;

			return response.ReadResponse<T>();
		}

		/// <summary>
		/// Invokes a remote method, and returns without waiting for a response
		/// </summary>
		/// <param name="method">The method to execute</param>
		/// <param name="parameters">Parameters for the remote method</param>
		public Task InvokeAsync(string method, params object[] parameters)
		{
			var message = new ReRPCMessage(0, method, parameters);
			m_MessageQueue.Enqueue(message);
			m_QueueSemaphore.Release();

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			Stop();
			m_QueueSemaphore.Dispose();
			if (m_DisposeStream)
			{
				m_Network.Dispose();
			}
		}
	}
}
