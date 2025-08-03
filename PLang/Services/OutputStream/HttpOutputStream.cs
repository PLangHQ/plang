using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Runtime;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System;
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
		private IEngine engine;
		private readonly IPLangFileSystem fileSystem;
		private readonly string contentType;
		private readonly int bufferSize;
		private LiveConnection? liveResponse;
		private readonly string path;
		private readonly Encoding encoding;
		private Dictionary<string, object?> responseProperties;

		public HttpOutputStream(HttpResponse response, IEngine engine, string contentType, int bufferSize, string path, LiveConnection? liveResponse)
		{
			this.response = response;
			this.engine = engine;
			this.contentType = contentType;
			this.bufferSize = bufferSize;
			this.liveResponse = liveResponse;
			this.path = path;
			this.encoding = Encoding.UTF8;
			this.responseProperties = new();

		}

		public Stream Stream { get { return this.response.Body; } }
		public Stream ErrorStream { get { return this.response.Body; } }

		public void SetLiveResponse(LiveConnection liveResponse)
		{
			this.liveResponse = liveResponse;
		}

		public bool IsStateful { get { return false; } }
		public Dictionary<string, object?> ResponseProperties
		{

			get { 
				return responseProperties; 
			}
			set { 
				responseProperties = value;
			}

		}
		public string Output
		{
			get
			{
				return contentType.Contains("json") ? "json" : "html";
			}
		}

		public bool IsFlushed { get; set; }
		public IEngine Engine {
			get { return engine; }
			set { engine = value; }
		}

		public async Task<(object?, IError?)> Ask(GoalStep step, AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			(var response, var isFlushed, error) = GetResponse();
			if (error != null) return (null, error);

			if (response == null) throw new Exception("Response is null");

			if (!isFlushed)
			{
				response.StatusCode = (askOptions.StatusCode == 0) ? 202 : askOptions.StatusCode;
				response.ContentType = contentType;
			}
			var responseProperties = GetResponseProperties(step, response);

			IOutputStream outputStream;
			if (contentType.Contains("plang"))
			{
				outputStream = new PlangOutputStream(response.Body, encoding, false, bufferSize, path, engine, responseProperties);
			}
			else if (contentType.Contains("json"))
			{
				outputStream = new JsonOutputStream(response.Body, encoding, false);
			}
			else if (contentType.Contains("html"))
			{
				outputStream = new HtmlOutputStream(response.Body, encoding, engine, path, false);
			}
			else
			{
				outputStream = new TextOutputStream(response.Body, encoding, false, bufferSize, path);
			}

			var result = await outputStream.Ask(step, askOptions, callback, error);
			IsFlushed = true;

			return result;


		}


		public string Read()
		{
			return "";
		}

		public async Task Write(GoalStep step, object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{

			(var response, var isFlushed, var error) = GetResponse();
			if (error != null) throw new ExceptionWrapper(error);
			if (response == null || !response.Body.CanWrite)
			{
				Console.WriteLine("Response is null, so to console it goes: " + obj);
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
						response.Headers.TryAdd("Content-Type", contentType);
					}
				}
				catch (Exception ex)
				{
					int i = 0;
				}
			}

			var responseProperties = GetResponseProperties(step, response, paramaters);
			

			IOutputStream outputStream;
			if (contentType.Contains("plang"))
			{
				outputStream = new PlangOutputStream(response.Body, encoding, false, bufferSize, path, engine, responseProperties);
			}
			else if (contentType.Contains("json"))
			{
				outputStream = new JsonOutputStream(response.Body, encoding, false);
			}
			else if (contentType.Contains("html"))
			{ 
				outputStream = new HtmlOutputStream(response.Body, encoding, engine, path.ToString(), false);
			}
			else if (contentType.Contains("text"))
			{
				outputStream = new TextOutputStream(response.Body, encoding, false, callbackUri: path.ToString());
			}
			else
			{
				outputStream = new BinaryOutputStream(response.Body, encoding, false);
			}

			await outputStream.Write(step, obj, parameters: responseProperties);

			IsFlushed = true;

		}

		private Dictionary<string, object?> GetResponseProperties(GoalStep step, HttpResponse response, Dictionary<string, object?>? parameters = null)
		{
			if (parameters == null) parameters = new();
			try
			{
				parameters.AddOrReplace("path", response.HttpContext.Request.Path.Value);
				parameters.AddOrReplace("id", Path.Join(path, step.Goal.GoalName, step.Number.ToString()).Replace("\\", "/"));
			} catch (Exception ex)
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
				if (liveResponse == null) return (null, false, null);

				bool isFlushed = liveResponse.IsFlushed;
				liveResponse.IsFlushed = true;
				return (liveResponse?.Response, isFlushed, null);
			}
			catch (Exception ex)
			{
				return (null, true, new ExceptionError(ex));
			}

		}

	}
}
