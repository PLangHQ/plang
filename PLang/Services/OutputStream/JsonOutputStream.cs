using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Modules;
using PLang.Utils;
using System;
using System.Net;
using System.Text;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class JsonOutputStream : IOutputStream
	{
		private readonly Stream stream;
		private readonly Encoding encoding;
		private bool isStateful;
		private readonly int bufferSize;

		public JsonOutputStream(Stream stream, Encoding encoding, bool isStateful, int bufferSize = 4096)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.isStateful = isStateful;
			this.bufferSize = bufferSize;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }
		public GoalStep Step { get; set; }

		public string Output => "json";

		public bool IsStateful => isStateful;

		public bool IsFlushed { get; set; }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			using (var writer = new StreamWriter(stream, encoding, bufferSize: this.bufferSize, leaveOpen: true))
			{
				await writer.WriteAsync(JsonConvert.SerializeObject(new
				{
					askOptions,
					callback,
					error
				}));

				await writer.FlushAsync();
				IsFlushed = true;
			}
			return (null, null);
		}

		public string Read()
		{
			return "";
		}

		public async Task Write(object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{

			string? content = TypeHelper.GetAsString(obj);
			if (content == null) return;

			await using var writer = new StreamWriter(stream, encoding, bufferSize: this.bufferSize, leaveOpen: true);
			await writer.WriteAsync(content);
			await writer.FlushAsync();

			IsFlushed = true;

		}

	}
}
