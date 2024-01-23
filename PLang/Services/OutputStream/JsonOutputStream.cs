using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Utils;
using System.Net;
using System.Text;

namespace PLang.Services.OutputStream
{
	public class JsonOutputStream : IOutputStream
	{
		private readonly HttpListenerContext httpContext;

		public JsonOutputStream(HttpListenerContext httpContext)
		{
			this.httpContext = httpContext;

			httpContext.Response.ContentType = "application/json";
		}

		public Stream Stream => httpContext.Response.OutputStream;
		public Stream ErrorStream => httpContext.Response.OutputStream;

		public async Task<string> Ask(string text, string type, int statusCode = 400)
		{
			httpContext.Response.SendChunked = true;
			httpContext.Response.StatusCode = 400;

			using (var writer = new StreamWriter(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding ?? Encoding.UTF8))
			{
				if (text != null)
				{
					string content = text.ToString();
					if (!JsonHelper.IsJson(content))
					{
						content = JsonConvert.SerializeObject(content);
					}

					await writer.WriteAsync(content);
				}
				await writer.FlushAsync();
			}
			return "";
		}

		public void Dispose()
		{
		}

		public string Read()
		{
			return "";
		}

		public async Task Write(object? obj, string type, int httpStatusCode = 200)
		{

			httpContext.Response.StatusCode = httpStatusCode;
			using (var writer = new StreamWriter(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding ?? Encoding.UTF8))
			{
				if (obj != null)
				{
					if (obj is JValue || obj is JObject || obj is JArray)
					{
						await writer.WriteAsync(obj.ToString());
					}
					else
					{
						if (type != "text")
						{
							JObject jsonObj = new JObject();
							jsonObj[type] = JToken.FromObject(obj);
							writer.Write(jsonObj.ToString());
							return;
						}

						string content = obj.ToString()!;
						if (!JsonHelper.IsJson(content))
						{
							content = JsonConvert.SerializeObject(obj);
						}

						await writer.WriteAsync(content);
					}
				}
				try
				{
					await writer.FlushAsync();
				} catch (System.Net.HttpListenerException ex)
				{
					if (ex.Message.Contains("An operation was attempted on a nonexistent network connection")) return;
					throw;
				}
			}
		}

		public async Task WriteToBuffer(object? obj, string type, int httpStatusCode = 200)
		{
			httpContext.Response.StatusCode = httpStatusCode;
			using (var writer = new StreamWriter(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding))
			{
				if (obj != null)
				{
					if (type != "text")
					{
						JObject jsonObj = new JObject();
						jsonObj[type] = JToken.FromObject(obj);
						writer.WriteAsync(jsonObj.ToString());
						return;
					}

					string content = obj.ToString();
					if (!JsonHelper.IsJson(content))
					{
						content = JsonConvert.SerializeObject(content);
					}

					await writer.WriteAsync(content);
				}
			}

		}
	}
}
