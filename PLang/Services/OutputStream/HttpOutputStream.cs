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
		private readonly LiveConnection? liveResponse;
		private readonly Uri uri;

		public HttpOutputStream(HttpResponse response, IPLangFileSystem fileSystem, string contentType, LiveConnection? liveResponse, Uri uri)
		{
			this.response = response;
			this.fileSystem = fileSystem;
			this.contentType = contentType;
			this.liveResponse = liveResponse;
			this.uri = uri;
		}

		public Stream Stream { get { return this.response.Body; } }
		public Stream ErrorStream { get { return this.response.Body; } }

		public bool IsStateful { get { return false; } }
		public string Output
		{
			get
			{
				return contentType.Contains("json") ? "json" : "html";
			}
		}

		public bool IsFlushed { get; set; }

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

		public async Task<(string?, IError?)> Ask(string text, string type, int statusCode = 200, Dictionary<string, object?>? parameters = null, Callback? callback = null, List<Option>? options = null)
		{
			(var response, var isFlushed, var error) = GetResponse();
			if (error != null) return (null, error);

			if (response == null) throw new Exception("Response is null");

			if (!isFlushed)
			{
				response.StatusCode = statusCode;
				response.ContentType = contentType;
			}

			IOutputStream outputStream;
			if (contentType.Contains("plang"))
			{
				outputStream = new PlangOutputStream(response.Body, response.Headers..ContentEncoding ?? Encoding.UTF8, false);
			}
			else if (contentType.Contains("json"))
			{
				outputStream = new JsonOutputStream(response.Body, response.ContentEncoding ?? Encoding.UTF8, false);
			}
			else if (contentType.Contains("html"))
			{
				outputStream = new HtmlOutputStream(response.Body, response.ContentEncoding ?? Encoding.UTF8, fileSystem, uri.ToString(), false);
			}
			else
			{
				outputStream = new TextOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, false, uri.ToString());
			}
			
			var result = await outputStream.Ask(text, type, statusCode, parameters, callback, options);
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
				response.StatusCode = httpStatusCode;
				response.StatusDescription = type;
				response.SendChunked = true;
				response.Headers.Add("Content-Type", contentType);
			}

			IOutputStream outputStream;
			if (contentType.Contains("plang"))
			{
				outputStream = new PlangOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, false);
			}
			else if (contentType.Contains("json"))
			{
				outputStream = new JsonOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, false);
			}
			else if (contentType.Contains("html"))
			{
				outputStream = new HtmlOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, fileSystem, uri.ToString(), false);
			}
			else
			{
				outputStream = new TextOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, false, uri.ToString());
			}

			await outputStream.Write(obj);

			IsFlushed = true;

		}

	}
}
