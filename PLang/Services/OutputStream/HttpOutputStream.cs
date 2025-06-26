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
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class HttpOutputStream : IOutputStream
	{
		private readonly HttpListenerContext httpContext;
		private readonly IPLangFileSystem fileSystem;
		private readonly string contentType;

		public HttpOutputStream(HttpListenerContext httpContext, IPLangFileSystem fileSystem, string contentType)
		{
			this.httpContext = httpContext;
			this.fileSystem = fileSystem;
			this.contentType = contentType;
		}

		public Stream Stream { get { return this.httpContext.Response.OutputStream; } }
		public Stream ErrorStream { get { return this.httpContext.Response.OutputStream; } }

		public bool IsStateful { get { return false; } }
		public string Output
		{
			get
			{
				return contentType.Contains("json") ? "json" : "html";
			}
		}

		public async Task<(string?, IError?)> Ask(string text, string type, int statusCode = 200, Dictionary<string, object?>? parameters = null, Callback? callback = null, List<Option>? options = null)
		{
			httpContext.Response.SendChunked = true;
			httpContext.Response.StatusCode = statusCode;
			httpContext.Response.StatusDescription = type;
			httpContext.Response.ContentType = contentType;

			if (contentType.Contains("json"))
			{
				var jsonOutputStream = new JsonOutputStream(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding ?? Encoding.UTF8, false);
				return await jsonOutputStream.Ask(text, type, statusCode, parameters, callback, options);
			}

			if (contentType.Contains("html"))
			{
				var htmlOutputStream = new HtmlOutputStream(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding ?? Encoding.UTF8, fileSystem, httpContext.Request.Url.ToString(), false);
				return await htmlOutputStream.Ask(text, type, statusCode, parameters, callback, options);
				
			}

			if (contentType.Contains("text"))
			{
				var textOutputStream = new TextOutputStream(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding ?? Encoding.UTF8, false, httpContext.Request.Url.ToString());
				return await textOutputStream.Ask(text, type, statusCode, parameters, callback, options);

			}

			httpContext.Response.StatusCode = 400;

			return (null, new Error($"Content type {contentType} is not supported."));
		}


		public string Read()
		{
			return "";
		}
	
		public async Task Write(object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			httpContext.Response.StatusCode = httpStatusCode;
			httpContext.Response.StatusDescription = type;
			httpContext.Response.SendChunked = true;

			string? content = TypeHelper.GetAsString(obj, Output);
			if (content == null) return;

			byte[] buffer = Encoding.UTF8.GetBytes(content);
			httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
			//httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);


			return;

		}

	}
}
