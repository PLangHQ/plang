using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Runtime;
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
		private IEngine engine;
		private readonly string url;
		private readonly bool isStateful;
		private readonly int bufferSize;

		public HtmlOutputStream(Stream stream, Encoding encoding, IEngine engine, string url, bool isStateful, int bufferSize = 4096)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.engine = engine;
			this.url = url;
			this.isStateful = isStateful;
			this.bufferSize = bufferSize;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }
		public IEngine Engine
		{
			get
			{
				return engine;
			}
			set { engine = value; }
		}
		public GoalStep Step { get; set; }
		public string Output => "html";
		public bool IsStateful { get { return isStateful; } }

		public bool IsFlushed { get; set; }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			Dictionary<string, object?> parameters = new();
			parameters.Add("askOptions", askOptions);
			parameters.Add("callback", JsonConvert.SerializeObject(callback).ToBase64());
			parameters.Add("url", url);
			parameters.Add("error", error);

			string? content = null;
			IError? renderError = null;

			var templateEngine = new Modules.TemplateEngineModule.Program(engine.FileSystem, engine.GetMemoryStack(), null);
			templateEngine.SetGoal(Step.Goal);
			if (!string.IsNullOrEmpty(askOptions.TemplateFile))
			{
				(content, renderError) = await templateEngine.RenderFile(askOptions.TemplateFile, parameters);
			}
			else
			{
				(content, renderError) = await templateEngine.RenderFile("/modules/OutputModule/ask.html", parameters);
			}
			if (renderError != null) return (null, renderError);

			using var writer = new StreamWriter(stream, encoding, bufferSize: this.bufferSize, leaveOpen: true);
			await writer.WriteAsync(content);
			await writer.FlushAsync();

			IsFlushed = true;

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

			await using var writer = new StreamWriter(stream, encoding, bufferSize: this.bufferSize, leaveOpen: true);
			await writer.WriteAsync(content);
			await writer.FlushAsync();

			IsFlushed = true;

		}

	}
}
