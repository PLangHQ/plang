using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.IdentityModule;
using System.Buffers;
using System.ComponentModel;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;

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
		public async Task<(object? Object, IError? Error)> Deserialize(Stream stream, string? serializer = "json")
		{
			if (stream.Length == 0) return (null, null);
			if (serializer == null) return (null, new ProgramError("Serializer not defined", goalStep, function, Key: "SerializerNotDefined"));

			if (serializer.Contains("json"))
			{

				try
				{
					var dict = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, object>>(stream);
					if (dict == null) return (null, null);

					return (dict, null);
				}
				catch (Exception ex)
				{
					return (null, new ProgramError("Error deserializing json. Is it json?", goalStep, function, Exception: ex));
				}
			}
			else if (serializer.Contains("xml"))
			{
				var doc = new XmlDocument();
				doc.Load(stream);

				return (doc.InnerXml, null);
			}
			else if (serializer.Contains("text") || string.IsNullOrEmpty(serializer))
			{
				using (StreamReader reader = new StreamReader(stream))
				{
					return (await reader.ReadToEndAsync(), null);
				}
			}

			return (null, new ProgramError($"serializer {serializer} is not supported", goalStep, function, Key: "SerializerNotSupported"));

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
