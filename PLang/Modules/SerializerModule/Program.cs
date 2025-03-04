using MessagePack;
using Newtonsoft.Json;
using PLang.Interfaces;
using System.Buffers;
using System.ComponentModel;
using System.Text;

namespace PLang.Modules.SerializerModule
{
	public class Program : BaseProgram
	{
		private readonly IAppCache appCache;
		/*
		 * Need to inject ISerializer
		 * Switch out Newtonsoft, but this needs to be done in MemoryStack
		 * */
		public Program(IAppCache appCache)
		{
			this.appCache = appCache;
		}

		[Description("serializer(message_pack|json). User can also define his own")]
		public async Task<byte[]?> Serialize(object data, string serializer = "message_pack")
		{
			if (serializer == "json")
			{
				return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
			}

			var writer = new ArrayBufferWriter<byte>();
			MessagePackSerializer.Serialize(writer, data);
			return writer.WrittenSpan.ToArray(); //I should be return ReadOnlySpan<byte>
		}

		public async Task<object?> Deserialize(byte[] data, string serializer = "message_pack")
		{
			if (serializer == "json")
			{
				return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data));
			}

			//I should be use ReadOnlySpan<byte>
			return MessagePackSerializer.Deserialize(typeof(object), data);

		}

	}
}
