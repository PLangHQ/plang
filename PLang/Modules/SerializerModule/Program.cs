using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.IdentityModule;
using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace PLang.Modules.SerializerModule
{
	public class Program : BaseProgram
	{
		private readonly ProgramFactory programFactory;

		/*
* Need to inject ISerializer
* Switch out Newtonsoft, but this needs to be done in MemoryStack
* */
		public Program(Modules.ProgramFactory programFactory)
		{
			this.programFactory = programFactory;
		}

		[Description("serializer(message_pack|json). User can also define his own")]
		public async Task<byte[]?> Serialize(object data, string serializer = "json", Stream? stream = null)
		{
			if (serializer == "json")
			{
				if (stream == null)
				{
					return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
				}
				else
				{

					using var sw = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
					using var jw = new JsonTextWriter(sw);
					Newtonsoft.Json.JsonSerializer.CreateDefault().Serialize(jw, data);
					jw.Flush();

					
					return null;
				}
			}

			var writer = new ArrayBufferWriter<byte>();
			MessagePackSerializer.Serialize(writer, data);
			return writer.WrittenSpan.ToArray(); //I should be return ReadOnlySpan<byte>
		}
		public async Task<Dictionary<string, object>?> Deserialize(Stream stream, string serializer = "json")
		{
			if (serializer.Contains("json"))
			{


				var dict = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, object>>(stream);
				if (dict == null) return null;
				/*
				if (dict.ContainsKey("Signature"))
				{
					string strSig = dict["Signature"].ToString();
					var signature = JsonConvert.DeserializeObject<Signature>(strSig);
					var (sig, error) = await programFactory.GetProgram<Modules.IdentityModule.Program>().VerifySignature(signature);
					if (error != null) throw new ExceptionWrapper(error);
				}*/
				return dict;
			}
			throw new NotImplementedException();

		}
		public async Task<object?> Deserialize(byte[] data, string serializer = "json")
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
