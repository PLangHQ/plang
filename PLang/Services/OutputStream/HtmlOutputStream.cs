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
	/*
	public class HtmlOutputStream : IOutputStream
	{
		private readonly Stream stream;
		private readonly Encoding encoding;
		private readonly string url;
		private readonly bool isStateful;
		private readonly int bufferSize;

		public HtmlOutputStream(Stream stream, Encoding encoding, string url, bool isStateful, int bufferSize = 4096)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.url = url;
			this.isStateful = isStateful;
			this.bufferSize = bufferSize;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }

		public string Output => "html";
		public bool IsStateful { get { return isStateful; } }

		public bool IsFlushed { get; set; }

		public async Task<(object?, IError?)> Ask(GoalStep step, object question, Callback? callback = null, IError? error = null)
		{
			if (!stream.CanWrite) return (null, null);

			string? content = TypeHelper.GetAsString(question, Output);
			if (content == null) return (null, null);

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

		public async Task Write(GoalStep step, object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			if (!stream.CanWrite) return;

			string? content = TypeHelper.GetAsString(obj, Output);
			if (content == null) return;

			await using var writer = new StreamWriter(stream, encoding, bufferSize: this.bufferSize, leaveOpen: true);
			await writer.WriteAsync(content);
			await writer.FlushAsync();

			IsFlushed = true;

		}

	}*/
}
