using AngleSharp.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.OutputModule;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using Python.Runtime;
using Scriban;
using Scriban.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static PLang.Modules.OutputModule.Program;
using static PLang.Modules.UiModule.Program;
using static PLang.Services.Transformers.PlangTransformer;
using static PLang.Utils.StepHelper;

namespace PLang.Services.Transformers
{
	public class PlangTransformer : ITransformer
	{
		JsonTransformer jsonTransformer;

		public PlangTransformer(Encoding encoding)
		{
			jsonTransformer = new JsonTransformer(encoding);
		}
		public Encoding Encoding { get { return jsonTransformer.Encoding; } }
		public string ContentType { get { return "application/plang+json"; } }

		public string Output => "jsonl";

		public record OutputData(string Type, object Data, Dictionary<string, object?>? Properties = null)
		{
			SignedMessage? Signature { get; set; }
		}
		public record ErrorOutputData(string Type, IError Data, Dictionary<string, object?>? Properties = null)
		{
			SignedMessage? Signature { get; set; }
		}


		public (object?, IError?) Transform(object data, Dictionary<string, object?>? properties = null, string type = "html")
		{
			var outputData = new OutputData(type, data, properties);
			return jsonTransformer.Transform(outputData, properties, type);
		}

		public async Task<IError?> Transform(Stream stream, object data, Dictionary<string, object?>? properties, string type = "html")
		{
			
			if (data is IError error)
			{
				var errorOutputData = new ErrorOutputData("error", error, properties);
				return await jsonTransformer.Transform(stream, errorOutputData, properties, type);
				
			}

			//todo: hack, remove this
			if (type == "text") type = "html";
			var outputData = new OutputData(type, data, properties);
			return await jsonTransformer.Transform(stream, outputData, properties, type);
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
			}
			else if (obj is IList)
			{
				if (obj.GetType().GetGenericArguments().Length > 0)
				{
					outputType = obj.GetType().GetGenericArguments()[0].Name;
					outputType = outputType.Substring(0, 1).ToLower() + outputType.Substring(1);
				}
				else
				{
					Console.WriteLine($"!!! Why this? {obj} | {JsonConvert.SerializeObject(obj)}");
					outputType = "html";
				}
			}
			else
			{
				outputType = "html";
			}
			return outputType;
		}


	}
}
