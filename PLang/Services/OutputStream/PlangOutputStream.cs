using AngleSharp.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using static PLang.Modules.UiModule.Program;
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
		private readonly Dictionary<string, object?> responseProperties;

		public PlangOutputStream(Stream stream, Encoding encoding, bool isStateful, int bufferSize, string path, IEngine engine, Dictionary<string, object?> responseProperties)
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

		public string Output => "jsonl";
		public bool IsStateful { get { return isStateful; } }

		public bool IsFlushed { get; set; }
		public IEngine Engine { get { return engine; } set { this.engine = value; } }

		public record OutputData(string Type, object Data, Dictionary<string, object?> Properties, SignedMessage? Signature);
		public record ErrorOutputData(string Type, IError Data, Dictionary<string, object?> Properties, SignedMessage? Signature);
		public record AskData(string Type, string text, string? TargetElement, StepHelper.Callback Callback);
		public async Task<(object?, IError?)> Ask(GoalStep step, AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			Dictionary<string, object?> parameters = new();
			parameters.AddOrReplace("askOptions", askOptions);
			parameters.AddOrReplace("callback", JsonConvert.SerializeObject(callback).ToBase64());
			parameters.AddOrReplace("error", error);
			parameters.Add("url", path);
			foreach (var rp in responseProperties)
			{
				parameters.AddOrReplace(rp.Key, rp.Value);
			}
			
			var templateEngine = new Modules.TemplateEngineModule.Program(engine.FileSystem, engine.GetMemoryStack(), null);
			templateEngine.SetGoal(step.Goal);
			
			string? content = null;
			IError? renderError = null;

			if (askOptions.IsTemplateFile)
			{
				(content, renderError) = await templateEngine.RenderFile(askOptions.QuestionOrTemplateFile, parameters);
			} else
			{
				(content, renderError) = await templateEngine.RenderFile("/modules/OutputModule/ask.html", parameters);
			}				
			if (renderError != null) return (null, renderError);

			var outputData = new OutputData("html", content, parameters, null);
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

		public async Task Write(GoalStep step, object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? parameters = null)
		{
			if (obj == null) return;

			if (parameters == null) parameters = new();
			parameters.AddOrReplace("url", path);
			parameters.AddOrReplace("id", Path.Join(path, step.Goal.GoalName, step.Number.ToString()).Replace("\\", "/"));

			string? targetElement = null;
			string outputType = GetOutputType(obj);
			object outputData;
			if (obj is IError)
			{
				outputData = new ErrorOutputData(outputType, (IError) obj, parameters, null);
			} else
			{
				outputData = new OutputData(outputType, obj, parameters, null);
			}

			var options = new JsonSerializerOptions { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
			options.Converters.Add(new IErrorConverter());
			options.Converters.Add(new ObjectValueConverter());
			options.Converters.Add(new JTokenConverter());
			await System.Text.Json.JsonSerializer.SerializeAsync(Stream, outputData, options);
			var nl = Encoding.UTF8.GetBytes("\n");
			await Stream.WriteAsync(nl.AsMemory(0, nl.Length));
			await Stream.FlushAsync();


			string ble = System.Text.Json.JsonSerializer.Serialize(outputData, options);

			IsFlushed = true;
		}


		sealed class JTokenConverter : System.Text.Json.Serialization.JsonConverter<JToken>
		{
			public override bool CanConvert(System.Type t) => typeof(JToken).IsAssignableFrom(t);

			public override JToken Read(ref Utf8JsonReader r, System.Type t, JsonSerializerOptions o) =>
				throw new NotSupportedException();      // we never need to read

			public override void Write(Utf8JsonWriter w, JToken token, JsonSerializerOptions o) =>
				w.WriteRawValue(token.ToString(Newtonsoft.Json.Formatting.None), skipInputValidation: true);
		}

		private static string GetOutputType(object obj)
		{
			string outputType = obj.GetType().Name;
			if (obj is IError)
			{
				outputType = "error";
			}
			else if (obj is JavascriptFunction)
			{
				outputType = "js";
			}
			else if (obj is string)
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
			} else
			{
				outputType = "html";
			}
				return outputType;
		}
	}
}
