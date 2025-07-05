using Newtonsoft.Json;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules.OutputModule;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Utils;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	public class PlangOutputStream : IOutputStream
	{
		private readonly Stream stream;
		private readonly Encoding encoding;
		private readonly bool isStateful;

		public PlangOutputStream(Stream stream, Encoding encoding, bool isStateful)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.isStateful = isStateful;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }

		public string Output => "jsonl";
		public bool IsStateful { get { return isStateful; } }

		public bool IsFlushed { get; set; }

		public record OutputData(string Type, object Value, string? TargetElement);
		public record ErrorOutputData(string Type, IError Value, string? TargetElement);
		public record AskData(string Type, string text, string? TargetElement, StepHelper.Callback Callback, List<Program.Option>? Options);
		public async Task<(string?, IError?)> Ask(string text, string type = "ask", int statusCode = 200, Dictionary<string, object?>? parameters = null, StepHelper.Callback? callback = null, List<Program.Option>? options = null)
		{
			if (string.IsNullOrEmpty(text)) return (null, null);

			string? targetElement = null;
			if (parameters?.TryGetValue("targetElement", out object? value) == true)
			{
				targetElement = value?.ToString();
			}

			var outputData = new AskData(type, text, targetElement, callback, options);
			var json = JsonConvert.SerializeObject(outputData);
			byte[] buffer = encoding.GetBytes(json);
			await Stream.WriteAsync(buffer, 0, buffer.Length);
			await Stream.FlushAsync();

			IsFlushed = true;

			return (null, null);
		}

		public string Read()
		{
			throw new NotImplementedException();
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? parameters = null)
		{
			if (obj == null) return;

			string? targetElement = null;
			if (parameters?.TryGetValue("targetElement", out object? value) == true)
			{
				targetElement = value?.ToString();
			}
			string outputType = GetOutputType(obj);
			object outputData;
			if (obj is IError)
			{
				outputData = new ErrorOutputData(outputType, (IError) obj, targetElement);
			} else
			{
				outputData = new OutputData(outputType, obj, targetElement);
			}

				var options = new JsonSerializerOptions { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles };
			options.Converters.Add(new IErrorConverter());
			await System.Text.Json.JsonSerializer.SerializeAsync(Stream, outputData, options);
			var nl = Encoding.UTF8.GetBytes("\n");
			await Stream.WriteAsync(nl.AsMemory(0, nl.Length));
			await Stream.FlushAsync();

			IsFlushed = true;
		}

		private static string GetOutputType(object obj)
		{
			string outputType = obj.GetType().Name;
			if (obj is IError)
			{
				outputType = "Error";
			} else if (obj is string)
			{
				outputType = "Html";
			}
				return outputType;
		}
	}
}
