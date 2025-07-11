using NBitcoin;
using Newtonsoft.Json;
using PLang.Building.Model;
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
		private readonly int bufferSize;
		Encoding encoding;
		Stream stream;

		public TextOutputStream(Stream stream, Encoding encoding, bool isStatefull = true, int bufferSize = 4096, string? callbackUri = null) {
			this.encoding = encoding;
			this.stream = stream;

			this.isStatefull = isStatefull;
			this.callbackUri = callbackUri;
			this.bufferSize = bufferSize;
		}
		public Stream Stream => stream;
		public Stream ErrorStream => stream;
		public GoalStep Step { get; set; }


		public string Output { get => "text"; }
		public bool IsStateful => isStatefull;

		public bool IsFlushed { get; set; }
		public IEngine Engine { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			string? strOptions = null;
			if (askOptions.Choices != null)
			{
				foreach (var option in askOptions.Choices)
				{
					if (option.Key != option.Value)
					{
						strOptions += $"\n\t{option.Key}. {option.Value}";
					} else
					{
						strOptions += $"\n\t{option.Key}.";
					}
				}
			}

			var bytes = encoding.GetBytes($"[Ask] {askOptions.Question}{strOptions}");
			
			await this.stream.WriteAsync(bytes);
			await this.stream.FlushAsync();
			IsFlushed = true;

			if (!IsStateful) return (null, null);

			string endMarker = "\n";
			if (askOptions.Parameters != null && askOptions.Parameters.ContainsKey("endMarker"))
			{
				endMarker = askOptions.Parameters["endMarker"].ToString() ?? "\n";
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
			await this.stream.FlushAsync();


		}

	}
}
