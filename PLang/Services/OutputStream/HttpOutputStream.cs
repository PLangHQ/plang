using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Runtime;
using PLang.Services.Transformers;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using static PLang.Modules.OutputModule.Program;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public interface IResponseProperties
	{
		Dictionary<string, object?> ResponseProperties { get; set; }
	}

	public class HttpOutputStream : IOutputStream, IResponseProperties
	{
		private readonly HttpResponse response;
		private ConcurrentDictionary<string, LiveConnection> liveResponses;
		private readonly ITransformer transformer;

		private Dictionary<string, object?> responseProperties;
		private string identity;
		public bool IsComplete { get; set; } = false;
		public HttpOutputStream(HttpResponse response, ConcurrentDictionary<string, LiveConnection> liveResponses, ITransformer transformer)
		{
			this.response = response;
			this.transformer = transformer;
			this.responseProperties = new();

		}

		public Stream Stream { get { return this.response.Body; } }
		public Stream ErrorStream { get { return this.response.Body; } }

		public void SetIdentity(string identity)
		{
			this.identity = identity;
		}

		public bool IsStateful { get { return false; } }
		public Dictionary<string, object?> ResponseProperties
		{

			get
			{
				return responseProperties;
			}
			set
			{
				responseProperties = value;
			}

		}

		private Dictionary<string, object?> GetResponseProperties(GoalStep step, Dictionary<string, object?>? parameters = null)
		{
			if (parameters == null) parameters = new();
			try
			{
				string path = response.HttpContext.Request.Path.Value;
				parameters.AddOrReplace("path", path);
				parameters.AddOrReplace("id", Path.Join(path, step.Goal.GoalName, step.Number.ToString()).Replace("\\", "/"));
			}
			catch (Exception ex)
			{
				int i = 0;
			}

			foreach (var prop in responseProperties)
			{
				if (prop.Key.Equals("data-plang-cssSelector", StringComparison.OrdinalIgnoreCase))
				{
					if (!parameters.ContainsKey("cssSelector"))
					{
						parameters.AddOrReplace("cssSelector", prop.Value);
					}
				}
				else if (prop.Key.Equals("data-plang-action", StringComparison.OrdinalIgnoreCase))
				{
					if (!parameters.ContainsKey("action"))
					{
						parameters.AddOrReplace("action", prop.Value);
					}
				}
				else
				{
					if (!parameters.ContainsKey(prop.Key))
					{
						parameters.AddOrReplace(prop.Key, prop.Value);
					}
				}
			}
			return parameters;
		}
		public string Output
		{
			get
			{
				return "html";
			}
		}

		public bool IsFlushed { get; set; }


		public async Task<(object?, IError?)> Ask(GoalStep step, object? question, int statusCode, Callback? callback = null, IError? error = null)
		{
			if (question == null) return (null, null);

			if (IsComplete)
			{
				Console.WriteLine("IsComplete");
				return (null, new EndGoal(new Goal { RelativePrPath = "RootOfApp" }, step, "Response complete"));
			}

			(var response, var isFlushed, error) = GetResponse();
			if (error != null) return (null, error);

			if (response == null) throw new Exception("Response is null");

			if (!isFlushed)
			{
				response.StatusCode = statusCode;
				response.ContentType = $"{transformer.ContentType}; charset={transformer.Encoding.WebName}";
			}

			var responseProperties = GetResponseProperties(step);
			error = await transformer.Transform(Stream, question, responseProperties);

			IsFlushed = true;

			return (null, error);


		}


		public string Read()
		{
			return "";
		}

		public async Task Write(GoalStep step, object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? parameters = null)
		{
			if (IsComplete)
			{
				Console.WriteLine("IsComplete");
				return;
			}

			(var response, var isFlushed, var error) = GetResponse();
			if (error != null) throw new ExceptionWrapper(error);
			if (response == null || !response.Body.CanWrite)
			{
				Console.WriteLine("Response is null, so to console it goes: " + obj.ToString().Replace("\n", "").MaxLength(200));
				return;
				//throw new Exception("Response is null");
			}

			if (!isFlushed)
			{
				try
				{
					if (!response.HasStarted)
					{
						response.StatusCode = (httpStatusCode == 0) ? 200 : httpStatusCode;
						response.ContentType = $"{transformer.ContentType}; charset={transformer.Encoding.WebName}";
					}
				}
				catch (Exception ex)
				{
					int i = 0;
				}
			}

			if (obj is IError) type = "error";
			if (type == "text") type = "html";

			var responseProperties = GetResponseProperties(step, parameters);

			error = await transformer.Transform(response.Body, obj, responseProperties, type);

			IsFlushed = true;

		}



		public bool SetContentType(string contentType)
		{
			(var response, var isFlushed, _) = GetResponse();

			if (response == null) return false;
			if (response.HasStarted) return false;

			response.Headers.ContentType = contentType;
			return true;

		}

		public (HttpResponse?, bool IsFlushed, IError? Error) GetResponse()
		{
			try
			{
				if (response.Body.CanWrite)
				{
					return (response, IsFlushed, null);
				}
			}
			catch (Exception ex)
			{

			}

			try
			{
				if (liveResponses == null || string.IsNullOrEmpty(this.identity)) return (null, false, null);

				if (!liveResponses.TryGetValue(identity, out LiveConnection? liveConnection))
				{
					return (null, false, null);
				}

				bool isFlushed = liveConnection.IsFlushed;
				liveConnection.IsFlushed = true;
				return (liveConnection?.Response, isFlushed, null);
			}
			catch (Exception ex)
			{
				return (null, true, new ExceptionError(ex));
			}

		}

	}
}
