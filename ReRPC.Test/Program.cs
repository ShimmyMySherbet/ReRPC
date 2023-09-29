using System.Net;
using System.Net.Sockets;

namespace ReRPC.Test
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			var server = new Server(1024);
			server.Start();


			var client = new Client();

			await client.Connect(1024);


			if (client.Instance == null)
			{
				return;
			}


			await client.Instance.RunTest();
		}
	}
}