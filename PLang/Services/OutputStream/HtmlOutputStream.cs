using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Utils;
using System;
using System.Net;
using System.Text;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class HtmlOutputStream : IOutputStream
	{
		private readonly Stream stream;
		private readonly Encoding encoding;
		private readonly IPLangFileSystem fileSystem;
		private readonly string url;
		private readonly bool isStateful;

		public HtmlOutputStream(Stream stream, Encoding encoding, IPLangFileSystem fileSystem, string url, bool isStateful)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.fileSystem = fileSystem;
			this.url = url;
			this.isStateful = isStateful;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }

		public string Output => "html";
		public bool IsStateful { get { return isStateful; } }

		public async Task<(string?, IError?)> Ask(string question, string type, int statusCode = 200, Dictionary<string, object?>? parameters = null, Callback? callback = null, List<Option>? options = null)
		{
			if (parameters == null) parameters = new();
			if (parameters.ContainsKey("question"))
			{
				return (null, new Error("parameters cannot contain 'question' as this is needed by plang runtime."));
			}
			parameters.Add("question", question);
			Dictionary<int, object?> dictOptions = new();
			if (options != null)
			{
				if (parameters.ContainsKey("options"))
				{
					return (null, new Error("parameters cannot contain 'question' as this is needed by plang runtime."));
				}
				
				foreach (var option in options)
				{
					dictOptions.Add(option.ListNumber, option.SelectionInfo);
				}
				parameters.AddOrReplace("options", dictOptions);
			}
			parameters.Add("callback", JsonConvert.SerializeObject(callback).ToBase64());
			parameters.Add("url", url);

			var templateEngine = new Modules.TemplateEngineModule.Program(fileSystem, null);
			(var content, var error) = await templateEngine.RenderFile("/modules/OutputModule/ask.html", parameters);
			if (error != null) return (null, error);

			using (var writer = new StreamWriter(stream, encoding))
			{
				await writer.WriteAsync(content);
			}
			return (null, null);
		}

		public string Read()
		{
			return "";
		}
		
		public async Task Write(object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			string? content = TypeHelper.GetAsString(obj, Output);
			if (content == null) return;

			byte[] buffer = Encoding.UTF8.GetBytes(content);
			stream.Write(buffer, 0, buffer.Length);
			//httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);


			return;

		}

	}
}
