using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ReRPC.RPCBinding.Attributes;

namespace ReRPC.Test
{
	public class Client
	{
		public ClientInstance? Instance { get; private set; }

		public async Task Connect(int port)
		{
			var client = new TcpClient();
			await client.ConnectAsync(IPAddress.Loopback, port);

			var socket = new ReRPCClient(client.GetStream());
			Instance = new ClientInstance(socket);
			socket.RegisterFrom(Instance);
			socket.Start();
		}
	}

	[RPC("GetName")]
	public delegate Task<string> GetNameRPC();

	[RPC("GetAddress")]
	public delegate Task<string> GetAddressRPC(string repo);

	[RPC("GetMaybeNull")]
	public delegate Task<string?> GetMaybeNullRPC();

	[RPC("Multiply")]
	public delegate Task<int> MultiplyRPC(int a, int b);

	[RPC("ThrowException")]
	public delegate Task<string> ThrowExceptionRPC();

	public class ClientInstance
	{
		public ReRPCClient RPC { get; }

		public GetNameRPC GetName { get; }

		public GetAddressRPC GetAddress { get; }

		public GetMaybeNullRPC GetMaybeNull { get; }

		public MultiplyRPC Multiply { get; }

		public ThrowExceptionRPC ThrowException { get; }

		public ClientInstance(ReRPCClient rPC)
		{
			RPC = rPC;
			GetName = RPC.GetRPC<GetNameRPC>();
			GetAddress = RPC.GetRPC<GetAddressRPC>();
			GetMaybeNull = RPC.GetRPC<GetMaybeNullRPC>();
			Multiply = RPC.GetRPC<MultiplyRPC>();
			ThrowException = RPC.GetRPC<ThrowExceptionRPC>();
		}

		public async Task RunTest()
		{
			await Task.Delay(200);

			var serverName = await GetName();

			await Console.Out.WriteLineAsync($"Server's Name: {serverName}");

			var address = await GetAddress("NetMirror");

			Console.WriteLine($"Repo Path: {address}");

			await Console.Out.WriteLineAsync($"10*21 = {await Multiply(10, 21)}");

			for (int i = 0; i < 10; i++)
			{
				await Console.Out.WriteLineAsync($"MaybeNull: {(await GetMaybeNull()) ?? "Null"}");
			}

			try
			{
				await Console.Out.WriteLineAsync("Throwing exception...");
				await ThrowException();
			}
			catch (Exception ex)
			{
				await Console.Out.WriteLineAsync($"Exception invoking ThrowException: {ex.Message}");
			}

			await Task.Delay(1000);

			Console.WriteLine();
			Console.WriteLine();
			var sw = new Stopwatch();
			sw.Start();
			var rounds = 10000;
			for (int i = 0; i < rounds; i++)
			{
				await RPC.QueryAsync<DateTime>("GetTime");
			}
			sw.Stop();

			await Console.Out.WriteLineAsync($"{rounds} queries took {Math.Round(sw.ElapsedTicks / 10000f, 2)}ms ({Math.Round((sw.ElapsedTicks / (double)rounds) / 10000f, 8)}ms/query)");
		}
	}
}
