using AngleSharp.Dom;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.OutputModule;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.Utils;
using Python.Runtime;
using Scriban;
using Scriban.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class PlangOutputStream : IOutputStream
	{
		private readonly Stream stream;
		private readonly Encoding encoding;
		private readonly bool isStateful;
		private readonly int bufferSize;
		private readonly string path;
		private IEngine engine;
		private readonly Dictionary<string, string> responseProperties;

		public PlangOutputStream(Stream stream, Encoding encoding, bool isStateful, int bufferSize, string path, IEngine engine, Dictionary<string, string> responseProperties)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.isStateful = isStateful;
			this.bufferSize = bufferSize;
			this.path = path;
			this.engine = engine;
			this.responseProperties = responseProperties;
		}

		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }
		public GoalStep Step { get; set; }

		public string Output => "jsonl";
		public bool IsStateful { get { return isStateful; } }

		public bool IsFlushed { get; set; }
		public IEngine Engine { get { return engine; } set { this.engine = value; } }

		public record OutputData(string Type, object Data, Dictionary<string, string> Properties, SignedMessage? Signature);
		public record ErrorOutputData(string Type, IError Data, Dictionary<string, string> Properties, SignedMessage? Signature);
		public record AskData(string Type, string text, string? TargetElement, StepHelper.Callback Callback, List<Program.Option>? Options);
		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			Dictionary<string, object?> parameters = new();
			parameters.AddOrReplace("askOptions", askOptions);
			parameters.AddOrReplace("callback", JsonConvert.SerializeObject(callback).ToBase64());
			parameters.AddOrReplace("url", path);
			parameters.AddOrReplace("error", error);

			
			var templateEngine = new Modules.TemplateEngineModule.Program(engine.FileSystem, engine.GetMemoryStack(), null);
			templateEngine.SetGoal(Step.Goal);
			
			string? content = null;
			IError? renderError = null;

			if (!string.IsNullOrEmpty(askOptions.TemplateFile))
			{
				(content, renderError) = await templateEngine.RenderFile(askOptions.TemplateFile, parameters);
			} else
			{
				(content, renderError) = await templateEngine.RenderFile("/modules/OutputModule/ask.html", parameters);
			}				
			if (renderError != null) return (null, renderError);

			var outputData = new OutputData("html", content, responseProperties, null);
			var options = new JsonSerializerOptions { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
			options.Converters.Add(new IErrorConverter());

			await System.Text.Json.JsonSerializer.SerializeAsync(Stream, outputData, options);
			var nl = Encoding.UTF8.GetBytes("\n");
			await Stream.WriteAsync(nl.AsMemory(0, nl.Length));

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
			string outputType = GetOutputType(obj);
			object outputData;
			if (obj is IError)
			{
				outputData = new ErrorOutputData(outputType, (IError) obj, responseProperties, null);
			} else
			{
				outputData = new OutputData(outputType, obj, responseProperties, null);
			}

			var options = new JsonSerializerOptions { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
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
				outputType = "error";
			} else if (obj is string)
			{
				outputType = "html";
			} else if (obj is IList)
			{
				if (obj.GetType().GetGenericArguments().Length > 0)
				{
					outputType = obj.GetType().GetGenericArguments()[0].Name;
					outputType = outputType.Substring(0, 1).ToLower() + outputType.Substring(1);
				} else
				{
					Console.WriteLine($"!!! Why this? {obj} | {JsonConvert.SerializeObject(obj)}");
					outputType = "html";
				}
			} 
			return outputType;
		}
	}
}
