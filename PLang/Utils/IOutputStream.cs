
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Interfaces;
using System.Net;
using System.Net.Http;
using System.Text;

namespace PLang.Utils
{
	public interface IOutputStream
	{
		public PLangAppContext Context { get; set; }
		public Task Write(object? obj, string type = "text", int statusCode = 200);
		public Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200);
		public string Read();
		public Task<string> Ask(string text, string type = "text", int statusCode = 200);
	}

	public class JsonOutputStream : IOutputStream
	{
		public PLangAppContext Context { get; set; }

		public async Task<string> Ask(string text, string type, int statusCode = 400)
		{
			var httpContext = Context[ReservedKeywords.HttpContext] as HttpListenerContext;

			httpContext.Response.SendChunked = true;
			httpContext.Response.StatusCode = 400;

			using (var writer = new StreamWriter(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding))
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
			return null;
		}

		public string Read()
		{
			return "";
		}

		public async Task Write(object? obj, string type, int httpStatusCode = 200)
		{
			
			var httpContext = Context[ReservedKeywords.HttpContext] as HttpListenerContext;
			httpContext.Response.StatusCode = httpStatusCode;
			using (var writer = new StreamWriter(httpContext.Response.OutputStream, httpContext.Response.ContentEncoding))
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

						string content = obj.ToString();
						if (!JsonHelper.IsJson(content))
						{
							content = JsonConvert.SerializeObject(obj);
						}

						await writer.WriteAsync(content);
					}
				}
				await writer.FlushAsync();
			}
		}

		public async Task WriteToBuffer(object? obj, string type, int httpStatusCode = 200)
		{
			var httpContext = Context[ReservedKeywords.HttpContext] as HttpListenerContext;
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

	public class ConsoleOutputStream : IOutputStream
	{
		public PLangAppContext Context { get; set; }

		public async Task<string> Ask(string text, string type, int statusCode = 200)
		{
			Console.WriteLine(text);
			return Console.ReadLine();
		}

		public string Read()
		{
			return Console.ReadLine();
		}

		public async Task Write(object obj, string type, int statusCode = 200)
		{
			if (obj == null) return;
			if (obj.GetType().IsPrimitive || obj is string)
			{
				Console.WriteLine(obj);
			} else
			{
				Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
			}
			
		}

		public async Task WriteToBuffer(object obj, string type, int statusCode = 200)
		{
			await Write(obj, type);
		}
	}
}
