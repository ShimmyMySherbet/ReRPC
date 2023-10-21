using System.Text;
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

		public byte[][] Payloads { get; }

		public bool IsFault { get; }

		public ReRPCResponse(uint messageID, string? methodName, byte[][] payloads, bool isFault)
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

			var payload = Payloads[index];

			if (payload is T t)
			{
				return t;
			}

			var payloadJson = Encoding.UTF8.GetString(payload);

			return JsonConvert.DeserializeObject<T>(payloadJson);
		}

		public object ReadObject(Type type, int index)
		{
			if (index >= Payloads.Length)
			{
				return default!;
			}

			if (type == typeof(byte[]))
			{
				return Payloads[index];
			}
			var payload = Payloads[index];
			var payloadJson = Encoding.UTF8.GetString(payload);

			return JsonConvert.DeserializeObject(payloadJson, type)!;
		}

		public T? ReadResponse<T>()
		{
			return ReadObject<T>(0);
		}
	}
}
