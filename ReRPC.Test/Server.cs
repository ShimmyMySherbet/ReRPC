using System.Net;
using System.Net.Sockets;
using System.Text;
using ReRPC.RPCBinding.Attributes;

namespace ReRPC.Test
{
	public class Server
	{
		public TcpListener Listener { get; }

		public List<ServerInstance> Clients { get; } = new List<ServerInstance>();

		public Server(int port)
		{
			Listener = new TcpListener(IPAddress.Loopback, port);
		}

		public void Start()
		{
			Listener.Start();
			Task.Run(Loop);
		}

		public async Task Loop()
		{
			while (true)
			{
				var client = await Listener.AcceptTcpClientAsync();
				await Console.Out.WriteLineAsync("[Server] Client Connected");
				var connection = new ReRPCClient(client.GetStream());

				var instance = new ServerInstance(connection);
				connection.RegisterFrom(instance);
				Clients.Add(instance);
				connection.Start();
			}
		}
	}

	public class ServerInstance
	{
		public ReRPCClient RPC { get; }

		private Random m_Random = new Random();

		public ServerInstance(ReRPCClient rPC)
		{
			RPC = rPC;
			RPC.Faulted += RPC_Faulted;
			RPC.HandlerError += RPC_HandlerError;
		}

		private void RPC_HandlerError(Interfaces.IRPCHandler handler, Exception exception)
		{
			Console.WriteLine($"[Server] Handler {handler.Name} Faulted: {exception.Message}");
		}

		private void RPC_Faulted(ReRPCClient socket, Exception? exception)
		{
			Console.WriteLine($"[Server] Faulted");
		}

		[RPC]
		public string GetName()
		{
			return "Server";
		}

		[RPC]
		public async Task<string> GetAddress(string repo)
		{
			await Task.Delay(1000);
			return $"//UNC/source/repos/{repo}/";
		}

		[RPC]
		public string? GetMaybeNull()
		{
			var next = m_Random.Next(10);

			if (next > 5)
			{
				return next.ToString();
			}

			return null;
		}

		[RPC]
		public DateTime GetTime()
		{
			return DateTime.Now;
		}

		[RPC]
		public int Multiply(int a, int b)
		{
			return a * b;
		}

		[RPC]
		public Task<string> ThrowException()
		{
			throw new NotImplementedException();
		}

		[RPC]
		public void Disconnect()
		{
			Console.WriteLine("Disconnecting...");
			RPC.Dispose();
		}

		[RPC]
		public byte[] ConvertToBuffer(string message)
		{
			return Encoding.UTF8.GetBytes(message);
		}

		[RPC]
		public string ConvertToString(byte[] buffer)
		{
			return Encoding.UTF8.GetString(buffer);
		}
	}
}
