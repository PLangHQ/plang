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
		private readonly HttpListenerResponse response;
		private readonly IPLangFileSystem fileSystem;
		private readonly string contentType;
		private readonly LiveConnection? liveResponse;
		private readonly Uri uri;

		public HttpOutputStream(HttpListenerResponse response, IPLangFileSystem fileSystem, string contentType, LiveConnection? liveResponse, Uri uri)
		{
			this.response = response;
			this.fileSystem = fileSystem;
			this.contentType = contentType;
			this.liveResponse = liveResponse;
			this.uri = uri;
		}

		public Stream Stream { get { return this.response.OutputStream; } }
		public Stream ErrorStream { get { return this.response.OutputStream; } }

		public bool IsStateful { get { return false; } }
		public string Output
		{
			get
			{
				return contentType.Contains("json") ? "json" : "html";
			}
		}

		public bool IsFlushed { get; set; }

		private (HttpListenerResponse?, bool IsFlushed, IError? Error) GetResponse()
		{
			try
			{
				if (response.OutputStream.CanWrite)
				{
					return (response, IsFlushed, null);
				}
			} catch (Exception ex)
			{

			}

			try
			{
				if (liveResponse == null) return (null, false, null);

				bool isFlushed = liveResponse.IsFlushed;
				liveResponse.IsFlushed = true;
				return (liveResponse?.Response, isFlushed, null);
			} catch (Exception ex)
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
				response.SendChunked = true;
				response.StatusCode = statusCode;
				response.StatusDescription = type;
				response.ContentType = contentType;
			}

			if (contentType.Contains("json"))
			{
				var jsonOutputStream = new JsonOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, false);
				return await jsonOutputStream.Ask(text, type, statusCode, parameters, callback, options);
			}

			if (contentType.Contains("html"))
			{
				var htmlOutputStream = new HtmlOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, fileSystem, uri.ToString(), false);
				return await htmlOutputStream.Ask(text, type, statusCode, parameters, callback, options);
				
			}

			if (contentType.Contains("text"))
			{
				var textOutputStream = new TextOutputStream(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8, false, uri.ToString());
				return await textOutputStream.Ask(text, type, statusCode, parameters, callback, options);

			}

			response.StatusCode = 400;

			return (null, new Error($"Content type {contentType} is not supported."));
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
			}

			string? content = TypeHelper.GetAsString(obj, Output);
			if (content == null) return;

			byte[] buffer = Encoding.UTF8.GetBytes(content);

			await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
			await response.OutputStream.FlushAsync();

			IsFlushed = true;

			return;

		}

	}
}
