using Newtonsoft.Json;

namespace ReRPC.Models
{
	/// <summary>
	/// Represents an RPC message that has been read from the socket
	/// </summary>
	public struct ReRPCResponse
	{
		public uint MessageID { get; }

		public string? MethodName { get; }

		public string[] Payloads { get; }

		public bool IsFault { get; }

		public ReRPCResponse(uint messageID, string? methodName, string[] payloads, bool isFault)
		{
			MessageID = messageID;
			MethodName = methodName;
			Payloads = payloads;
			IsFault = isFault;
		}

		public T? ReadObject<T>(int index)
		{
			if (index >= Payloads.Length)
			{
				return default;
			}

			return JsonConvert.DeserializeObject<T>(Payloads[index]);
		}

		public object ReadObject(Type type, int index)
		{
			if (index >= Payloads.Length)
			{
				return default!;
			}

			return JsonConvert.DeserializeObject(Payloads[index], type)!;
		}

		public T? ReadResponse<T>()
		{
			if (Payloads.Length == 0)
			{
				return default;
			}

			return JsonConvert.DeserializeObject<T>(Payloads[0]);
		}
	}
}
