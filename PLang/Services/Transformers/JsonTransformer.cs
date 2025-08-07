using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Modules;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static PLang.Modules.OutputModule.Program;
using static PLang.Services.Transformers.PlangTransformer;
using static PLang.Utils.StepHelper;

namespace PLang.Services.Transformers
{
	public class JsonTransformer : ITransformer
	{
		private readonly Encoding encoding;

		public JsonTransformer(Encoding encoding)
		{
			this.encoding = encoding;
		}
		public Encoding Encoding { get { return encoding; } }
		public string ContentType { get { return "application/json"; } }
		public string Output => "json";


		public (object?, IError?) Transform(object data, Dictionary<string, object?>? properties = null, string type = "json")
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = false,
				ReferenceHandler = ReferenceHandler.IgnoreCycles,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			};
			options.Converters.Add(new IErrorConverter());

			try
			{
				var result = System.Text.Json.JsonSerializer.Serialize(data, options);
				return (result, null);
			}
			catch (Exception ex)
			{
				return (null, new ServiceError(ex.Message, GetType(), "TransformError", StatusCode: 500, Exception: ex));
			}

		}

		public async Task<IError?> Transform(Stream stream, object data, Dictionary<string, object?>? properties = null, string type = "text")
		{
			try
			{
				var options = new JsonSerializerOptions { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
				options.Converters.Add(new IErrorConverter());

				await System.Text.Json.JsonSerializer.SerializeAsync(stream, data, options);
				var nl = Encoding.UTF8.GetBytes("\n");
				await stream.WriteAsync(nl.AsMemory(0, nl.Length));
				await stream.FlushAsync();
			}
			catch (Exception ex)
			{
				return new ServiceError(ex.Message, GetType(), "TransformError", 500, true, ex);
			}

			return null;
		}


	}
}
