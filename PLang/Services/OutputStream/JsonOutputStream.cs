using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Modules;
using PLang.Utils;
using System;
using System.Net;
using System.Text;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class JsonOutputStream : IOutputStream, IDisposable
	{
		private readonly HttpListenerContext httpContext;
		private readonly MemoryStream memoryStream;
		public JsonOutputStream(HttpListenerContext httpContext)
		{
			this.httpContext = httpContext;
			this.memoryStream = new MemoryStream();
			httpContext.Response.ContentType = "application/json";
		}

		public Stream Stream { get { return this.memoryStream; } }
		public Stream ErrorStream { get { return this.memoryStream; } }

		public string Output => "json";

		public async Task<string> Ask(string text, string type, int statusCode = 200, Dictionary<string, object>? parameters = null, Callback? callback = null)
		{
			
			httpContext.Response.SendChunked = true;
			httpContext.Response.StatusCode = statusCode;
			if (parameters == null || !parameters.ContainsKey("url"))
			{
				throw new Exception("url parameter must be defined");
			}

			using (var writer = new StreamWriter(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding ?? Encoding.UTF8))
			{
				if (text != null)
				{
					var url = parameters["url"];
					parameters.Remove("url");

					var askObj = new
					{
						type = "ask",
						statusCode,
						url,
						body = text,
						parameters,
						callback
					};


					await writer.WriteAsync(JsonConvert.SerializeObject(askObj));
				}
				await writer.FlushAsync();
			}
			return null;
		}

		public void Dispose()
		{
			memoryStream.Dispose();
		}

		public string Read()
		{
			return "";
		}
		private string? GetAsString(object? obj)
		{
			if (obj == null) return null;

			if (obj is JValue || obj is JObject || obj is JArray)
			{
				return obj.ToString();
			}
			if (obj is IError)
			{
				return ((IError)obj).ToFormat("json").ToString();
			}
			else
			{
				string content = obj.ToString()!;
				if (!JsonHelper.IsJson(content))
				{
					content = JsonConvert.SerializeObject(obj);
				}

				return content;
			}


		}
		public async Task Write(object? obj, string type, int httpStatusCode = 200)
		{
			httpContext.Response.StatusCode = httpStatusCode;
			httpContext.Response.StatusDescription = type;

			string? content = GetAsString(obj);
			if (content == null) return;

			byte[] buffer = Encoding.UTF8.GetBytes(content);
			memoryStream.Write(buffer, 0, buffer.Length);
			//httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);


			return;

		}

		public async Task WriteToBuffer(object? obj, string type, int httpStatusCode = 200)
		{
			httpContext.Response.StatusCode = httpStatusCode;
			httpContext.Response.StatusDescription = type;
			httpContext.Response.SendChunked = true;

			string? content = GetAsString(obj);
			if (content == null) return;

			byte[] buffer = Encoding.UTF8.GetBytes(content);
			httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);


		}
	}
}
