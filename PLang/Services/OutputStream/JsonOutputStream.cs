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

		public JsonOutputStream(Stream stream, Encoding encoding, bool isStateful)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.isStateful = isStateful;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }

		public string Output => "json";

		public bool IsStateful => isStateful;

		public bool IsFlushed { get; set; }

		public async Task<(string?, IError?)> Ask(string text, string type, int statusCode = 200, Dictionary<string, object?>? parameters = null, Callback? callback = null, List<Option>? options = null)
		{
			
			if (parameters == null || !parameters.ContainsKey("url"))
			{
				throw new Exception("url parameter must be defined");
			}

			Dictionary<int, object> dictOptions = new();
			foreach (var option in options)
			{
				dictOptions.Add(option.ListNumber, option.SelectionInfo);
			}

			using (var writer = new StreamWriter(stream, encoding))
			{
				if (text != null)
				{
					var url = parameters["url"];
					parameters.Remove("url");

					var askObj = new
					{
						type = "ask",
						statusCode,
						url,
						body = text,
						parameters,
						options = dictOptions,
						callback
					};


					await writer.WriteAsync(JsonConvert.SerializeObject(askObj));
				}
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

			await using var writer = new StreamWriter(stream, encoding, bufferSize: 4096, leaveOpen: true);
			await writer.WriteAsync(content);
			await writer.FlushAsync();

			IsFlushed = true;

		}

	}
}
