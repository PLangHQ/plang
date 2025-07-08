using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using static PLang.Modules.OutputModule.Program;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class HttpOutputStream : IOutputStream
	{
		private readonly HttpResponse response;
		private readonly IPLangFileSystem fileSystem;
		private readonly string contentType;
		private readonly int bufferSize;
		private LiveConnection? liveResponse;
		private readonly string path;
		private readonly Encoding encoding;

		public HttpOutputStream(HttpResponse response, IPLangFileSystem fileSystem, string contentType, int bufferSize, string path, LiveConnection? liveResponse)
		{
			this.response = response;
			this.fileSystem = fileSystem;
			this.contentType = contentType;
			this.bufferSize = bufferSize;
			this.liveResponse = liveResponse;
			this.path = path;
			this.encoding = Encoding.UTF8;

		}

		public Stream Stream { get { return this.response.Body; } }
		public Stream ErrorStream { get { return this.response.Body; } }

		public void SetLiveResponse(LiveConnection liveResponse)
		{
			this.liveResponse = liveResponse;
		}
		public GoalStep Step { get; set; }
		public bool IsStateful { get { return false; } }
		public string Output
		{
			get
			{
				return contentType.Contains("json") ? "json" : "html";
			}
		}

		public bool IsFlushed { get; set; }


		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			(var response, var isFlushed, error) = GetResponse();
			if (error != null) return (null, error);

			if (response == null) throw new Exception("Response is null");

			if (!isFlushed)
			{
				response.StatusCode = askOptions.StatusCode;
				response.ContentType = contentType;
			}

			IOutputStream outputStream;
			if (contentType.Contains("plang"))
			{
				outputStream = new PlangOutputStream(response.Body, encoding, false, bufferSize, path, fileSystem);
			}
			else if (contentType.Contains("json"))
			{
				outputStream = new JsonOutputStream(response.Body, encoding, false);
			}
			else if (contentType.Contains("html"))
			{
				outputStream = new HtmlOutputStream(response.Body, encoding, fileSystem, path, false);
			}
			else
			{
				outputStream = new TextOutputStream(response.Body, encoding, false, bufferSize, path);
			}
			outputStream.Step = Step;
			


			var result = await outputStream.Ask(askOptions, callback, error);
			IsFlushed = true;

			return result;


		}


		public string Read()
		{
			return "";
		}

		public async Task Write(object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{

			(var response, var isFlushed, var error) = GetResponse();
			if (error != null) throw new ExceptionWrapper(error);
			if (response == null) throw new Exception("Response is null");

			if (!isFlushed)
			{
				try
				{
					if (!response.HasStarted)
					{
						response.StatusCode = httpStatusCode;
						response.Headers.TryAdd("Content-Type", contentType);
					}
				}
				catch (Exception ex)
				{
					int i = 0;
				}
			}

			IOutputStream outputStream;
			if (contentType.Contains("plang"))
			{
				outputStream = new PlangOutputStream(response.Body, encoding, false, bufferSize, path, fileSystem);
			}
			else if (contentType.Contains("json"))
			{
				outputStream = new JsonOutputStream(response.Body, encoding, false);
			}
			else if (contentType.Contains("html"))
			{
				outputStream = new HtmlOutputStream(response.Body, encoding, fileSystem, path.ToString(), false);
			}
			else if (contentType.Contains("text"))
			{
				outputStream = new TextOutputStream(response.Body, encoding, false, callbackUri: path.ToString());
			}
			else
			{
				outputStream = new BinaryOutputStream(response.Body, encoding, false);
			}

			await outputStream.Write(obj);

			IsFlushed = true;

		}



		private (HttpResponse?, bool IsFlushed, IError? Error) GetResponse()
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
