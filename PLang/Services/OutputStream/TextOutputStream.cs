using NBitcoin;
using Newtonsoft.Json;
using PLang.Errors;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	

	public class TextOutputStream : IOutputStream
	{
		private readonly bool isStatefull;
		private readonly string? callbackUri;
		Encoding encoding;
		Stream stream;

		public TextOutputStream(Stream stream, Encoding encoding, bool isStatefull = true, string? callbackUri = null) {
			this.encoding = encoding;
			this.stream = stream;

			this.isStatefull = isStatefull;
			this.callbackUri = callbackUri;
		}
		public Stream Stream => stream;
		public Stream ErrorStream => stream;

		public string Output { get => "text"; }
		public bool IsStateful => isStatefull;

		public async Task<(string?, IError?)> Ask(string text, string type = "text", int statusCode = 202, Dictionary<string, object>? parameters = null,
			Callback? callback = null, List<Option>? options = null)
		{
			string? strOptions = null;
			foreach (var option in options)
			{
				strOptions += $"\n\t{option.ListNumber}. {option.SelectionInfo}";
			}

			var bytes = encoding.GetBytes($"[Ask] {text}{options}");
			await this.stream.WriteAsync(bytes);

			if (!IsStateful) return (null, null);

			string endMarker = "\n";
			if (parameters != null && parameters.ContainsKey("endMarker"))
			{
				endMarker = parameters["endMarker"].ToString() ?? "\n";
				if (string.IsNullOrEmpty(endMarker)) endMarker = "\n";
			}

			string line = await ReadUntilAsync(stream, encoding, endMarker);
			return (line, null);
		}


		public static async Task<string> ReadUntilAsync(Stream stream, Encoding encoding, string endMarker)
		{
			var buffer = new List<byte>();
			int markerLen = encoding.GetByteCount(endMarker);
			var markerBytes = encoding.GetBytes(endMarker);
			var readBuf = new byte[1];
			while (true)
			{
				int b = await stream.ReadAsync(readBuf.AsMemory(0, 1));
				if (b == -1) break;
				buffer.Add((byte)b);

				if (buffer.Count >= markerLen)
				{
					bool isMarker = true;
					for (int i = 0; i < markerLen; i++)
					{
						if (buffer[buffer.Count - markerLen + i] != markerBytes[i])
						{
							isMarker = false;
							break;
						}
					}
					if (isMarker)
					{
						buffer.RemoveRange(buffer.Count - markerLen, markerLen);
						break;
					}
				}
			}
			return encoding.GetString(buffer.ToArray());
		}


		public string Read()
		{
			return Console.ReadLine() ?? "";
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			if (obj == null) return;

			string content = obj.ToString() ?? string.Empty;
			var fullName = obj.GetType().FullName ?? "";
			if (fullName.IndexOf("[") != -1)
			{
				fullName = fullName.Substring(0, fullName.IndexOf("["));
			}

			byte[]? bytes = null;
			if (content != null && !content.StartsWith(fullName))
			{
				if (!TypeHelper.IsConsideredPrimitive(obj.GetType()) && !TypeHelper.IsRecordWithToString(obj))
				{
					bytes = encoding.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented));
				}
				else
				{
					bytes = encoding.GetBytes(content);
				}
			}
			else
			{
				bytes = encoding.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented));
			}

			await this.stream.WriteAsync(bytes);
			

		}

	}
}
