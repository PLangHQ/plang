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
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using static PLang.Modules.OutputModule.Program;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Runtime.Engine;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public interface IResponseProperties
	{
		Dictionary<string, object?> ResponseProperties { get; set; }
	}

	public class HttpOutputStream : IOutputStream, IResponseProperties
	{
		private readonly HttpContext httpContext;
		private readonly WebserverProperties webserverProperties;
		private readonly ConcurrentDictionary<string, LiveConnection> liveConnections;
		private readonly ITransformer transformer;

		private Dictionary<string, object?> responseProperties;
		private string identity;
		private string path;
		public string Id { get; set; }
		public bool IsComplete { get; set; } = false;
		public HttpOutputStream(HttpContext httpContext, WebserverProperties webserverProperties, ConcurrentDictionary<string, LiveConnection> liveConnections)
		{

			path = httpContext.Request.Path.Value ?? "/";
			this.httpContext = httpContext;
			this.webserverProperties = webserverProperties;
			this.liveConnections = liveConnections;
			this.transformer = GetTransformer(webserverProperties, httpContext);
			this.responseProperties = new();
			Id = Guid.NewGuid().ToString();

		}

		private ITransformer GetTransformer(WebserverProperties props, HttpContext httpContext)
		{
			string? contentType = httpContext.Request.Headers.Accept.FirstOrDefault();
			if (contentType == null) contentType = webserverProperties.DefaultResponseProperties!.ContentType;
			Encoding encoding = Encoding.GetEncoding(webserverProperties.DefaultResponseProperties!.ResponseEncoding);

			if (contentType.StartsWith("application/plang")) return new PlangTransformer(encoding);
			if (contentType.StartsWith("application/json")) return new JsonTransformer(encoding);
			if (contentType.StartsWith("text/html")) return new HtmlTransformer(encoding);

			return new TextTransformer(encoding);
		}

		public Stream Stream
		{
			get
			{
				if (httpContext == null)
				{
					throw new Exception("HttpContext is null. This should be");
				}

				return httpContext.Response.Body;
			}
		}
		public Stream ErrorStream
		{
			get
			{
				if (httpContext == null)
				{
					throw new Exception("HttpContext is null. This should be");
				}
				return httpContext.Response.Body;
			}
		}

		public void SetIdentity(string identity)
		{
			this.identity = identity;
		}

		public bool IsStateful { get { return false; } }
		public bool MainResponseIsDone { get; set; }
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

		private Dictionary<string, object?> GetResponseProperties(GoalStep step, Dictionary<string, object?>? parameters = null, Callback? callback = null)
		{
			if (parameters == null) parameters = new();
			try
			{
				parameters.AddOrReplace("path", path);
				parameters.AddOrReplace("id", Path.Join(path, step.Goal.GoalName, step.Number.ToString()).Replace("\\", "/"));

				//todo: just while I haven't fixed
				if (parameters.ContainsKey("target"))
				{
					parameters.AddOrReplace("cssSelector", parameters["target"]);
				}

				if (callback != null)
				{
					parameters.AddOrReplace("callback", JsonConvert.SerializeObject(callback).ToBase64());
				}
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

		public HttpContext HttpContext => httpContext;

		public ConcurrentDictionary<string, LiveConnection> LiveConnections => liveConnections;

		public async Task<(object?, IError?)> Ask(GoalStep step, object? question, int statusCode, 
			Callback? callback = null, IError? error = null, Dictionary<string, object?>? parameters = null)
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

			if (!isFlushed && !response.HasStarted && response.StatusCode == 200)
			{
				response.StatusCode = statusCode;
				response.ContentType = $"{transformer.ContentType}; charset={transformer.Encoding.WebName}";
			}

			var responseProperties = GetResponseProperties(step, parameters, callback);
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

				Console.WriteLine($"Response is null - {Id}, so to console it goes: " + obj.ToString().ClearHtml().Replace("\n", "").MaxLength(200));
				return;
				//throw new Exception("Response is null");
			}

			if (!isFlushed)
			{
				try
				{
					if (!response.HasStarted && response.StatusCode == 200)
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
				if (!MainResponseIsDone && httpContext.Response.Body.CanWrite)
				{
					return (httpContext.Response, IsFlushed, null);
				}
			}
			catch (Exception ex)
			{

			}

			try
			{
				if (liveConnections == null || string.IsNullOrEmpty(this.identity)) return (null, false, null);

				if (!liveConnections.TryGetValue(identity, out LiveConnection? liveConnection))
				{
					return (null, false, null);
				}

				bool isFlushed = liveConnection.IsFlushed;
				liveConnection.IsFlushed = true;
				return (liveConnection?.Response, isFlushed, null);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Live connection no longer available:" + ex);
				liveConnections.TryRemove(identity, out var _);

				return (null, true, null);
			}

		}

	}
}
