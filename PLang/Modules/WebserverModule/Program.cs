using LightInject;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;

namespace PLang.Modules.WebserverModule
{
	[Description("Start webserver, write to Body, Header, Cookie")]
	internal class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly IEventRuntime eventRuntime;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly IOutputStream outputStream;
		private readonly PrParser prParser;
		private readonly HttpHelper httpHelper;

		public Program(ILogger logger, IEventRuntime eventRuntime, IPLangFileSystem fileSystem
			, ISettings settings, IOutputStream outputStream
			, PrParser prParser, HttpHelper httpHelper) : base()
		{
			this.logger = logger;
			this.eventRuntime = eventRuntime;
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.outputStream = outputStream;
			this.prParser = prParser;
			this.httpHelper = httpHelper;
		}

		public async Task StartWebserver(string scheme = "http", string host = "localhost",
			int port = 8080, int maxContentLengthInBytes = 4096 * 1024,
			string defaultResponseContentEncoding = "utf-8",
			bool signedRequestRequired = false,
			List<string>? publicPaths = null)
		{
			publicPaths = publicPaths ?? new List<string> { "api", "api.goal" };
			var listener = new HttpListener();
			listener.Prefixes.Add(scheme + "://" + host + ":" + port + "/");

			listener.Start();

			logger.LogDebug($"Listening on {host}:{port}...");
			Console.WriteLine($"Listening on {host}:{port}...");

			await eventRuntime.RunStartEndEvents(context, EventType.After, EventScope.StartOfApp);
			KeepAlive(listener, "Webserver");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Task.Run(async () =>
			{
				try
				{
					while (true)
					{
						var httpContext = listener.GetContext();
						var request = httpContext.Request;
						var resp = httpContext.Response;

						if (signedRequestRequired && string.IsNullOrEmpty(request.Headers.Get("X-Signature")))
						{
							await WriteError(httpContext.Response, $"You must sign your request to user this web service. Using plang, you simply say. '- GET http://... sign request");
							continue;
						}

						Goal? goal = null;
						string? goalPath = null;
						string? requestedFile = null;
						try
						{

							requestedFile = httpContext.Request.Url?.LocalPath;
							goalPath = GetGoalPath(publicPaths, httpContext.Request);
							if (string.IsNullOrEmpty(goalPath))
							{
								ProcessGeneralRequest(httpContext);
								continue;
							}

							goal = prParser.GetGoal(Path.Combine(goalPath, ISettings.GoalFileName));
							if (goal == null)
							{
								await WriteNotfound(resp, $"Goal could not be loaded");
								continue;
							}

							int maxContentLength = (goal.GoalApiInfo != null && goal.GoalApiInfo.MaxContentLengthInBytes != null) ? goal.GoalApiInfo.MaxContentLengthInBytes : maxContentLengthInBytes;
							if (httpContext.Request.ContentLength64 > maxContentLength)
							{
								httpContext.Response.StatusCode = 413;
								httpContext.Response.Close();
								continue;
							}



							if (goal.GoalApiInfo == null || String.IsNullOrEmpty(goal.GoalApiInfo.Method))
							{
								await WriteError(resp, $"METHOD is not defined on goal");
								continue;
							}
							httpContext.Response.ContentEncoding = Encoding.GetEncoding(defaultResponseContentEncoding);
							httpContext.Response.ContentType = "application/json";
							httpContext.Response.SendChunked = true;
							if (goal.GoalApiInfo != null)
							{
								if (goal.GoalApiInfo.ContentEncoding != null)
								{
									httpContext.Response.ContentEncoding = Encoding.GetEncoding(defaultResponseContentEncoding);
								}
								if (goal.GoalApiInfo.ContentType != null)
								{
									httpContext.Response.ContentType = goal.GoalApiInfo.ContentType;
								}

								if (goal.GoalApiInfo.NoCacheOrNoStore != null)
								{
									httpContext.Response.Headers["Cache-Control"] = goal.GoalApiInfo.NoCacheOrNoStore;
								}
								else if (goal.GoalApiInfo.CacheControlPrivateOrPublic != null || goal.GoalApiInfo.CacheControlMaxAge != null)
								{
									string publicOrPrivate = goal.GoalApiInfo.CacheControlPrivateOrPublic;
									if (publicOrPrivate == null) { publicOrPrivate = "public"; }


									httpContext.Response.Headers["Cache-Control"] = $"{publicOrPrivate}, {goal.GoalApiInfo.CacheControlMaxAge}";
								}
							}

							var container = new ServiceContainer();
							container.RegisterForPLangWebserver(goal.AbsoluteAppStartupFolderPath, goal.RelativeGoalFolderPath);

							var context = container.GetInstance<PLangAppContext>();
							context.Add(ReservedKeywords.HttpContext, httpContext);
							context.Add(ReservedKeywords.IsHttpRequest, true);

							var engine = container.GetInstance<IEngine>();
							engine.Init(container);
							engine.HttpContext = httpContext;

							var requestMemoryStack = container.GetInstance<MemoryStack>();
							await ParseRequest(httpContext, goal.GoalApiInfo.Method, requestMemoryStack);
							await engine.RunGoal(goal);
							if (context.TryGetValue("OutputStream", out object? list))
							{

								using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
								{
									if (list != null)
									{
										string content = list.ToString();
										if (JsonHelper.IsJson(content))
										{
											content = JsonConvert.SerializeObject(list);
										}

										await writer.WriteAsync(content);
									}
									await writer.FlushAsync();
								}
							}
							resp.StatusCode = (int)HttpStatusCode.OK;
							resp.StatusDescription = "Status OK";
						}
						catch (Exception ex)
						{
							logger.LogError("WebServerError - requestedFile:{0} - goalPath:{1} - goal:{2} - Exception:{3}", requestedFile, goalPath, goal, ex);
							resp.StatusCode = (int)HttpStatusCode.InternalServerError;
							resp.StatusDescription = "Error";
							using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
							{
								await writer.WriteAsync(JsonConvert.SerializeObject(ex));
								await writer.FlushAsync();
							}

						}
						finally
						{
							context.Remove("IsHttpRequest");
							resp.Close();
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogError("Webserver crashed");
				}
			});
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

		}

		private void ProcessGeneralRequest(HttpListenerContext httpContext)
		{
			var requestedFile = httpContext.Request.Url?.LocalPath;
			if (requestedFile == null) return;

			var container = new ServiceContainer();
			container.RegisterForPLangWebserver(goal.AbsoluteAppStartupFolderPath, goal.RelativeGoalFolderPath);

			requestedFile = requestedFile.Replace("/", Path.DirectorySeparatorChar.ToString()).Replace(@"\", Path.DirectorySeparatorChar.ToString());

			var filePath = Path.Join(settings.GoalsPath!, requestedFile);
			var fileSystem = container.GetInstance<IPLangFileSystem>();

			if (fileSystem.File.Exists(filePath))
			{
				var buffer = fileSystem.File.ReadAllBytes(filePath);
				httpContext.Response.ContentLength64 = buffer.Length;
				var extension = Path.GetExtension(filePath);
				httpContext.Response.ContentType = GetMimeType(extension);
				httpContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
			}
			else
			{
				httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
			}
			httpContext.Response.OutputStream.Close();
		}
		private string GetMimeType(string extension)
		{
			switch (extension)
			{
				case ".txt": return "text/plain";
				case ".jpg": case ".jpeg": return "image/jpeg";
				case ".png": return "image/png";
				case ".gif": return "image/gif";
				case ".html": return "text/html";
				case ".css": return "text/css";
				case ".js": return "application/javascript";
				case ".mp4": return "video/mp4";
				// add more MIME types here as required
				default: return "application/octet-stream";
			}
		}
		private async Task WriteNotfound(HttpListenerResponse resp, string error)
		{
			resp.StatusCode = (int)HttpStatusCode.NotFound;

			await outputStream.Write(JsonConvert.SerializeObject(error), "text");

		}
		private async Task WriteError(HttpListenerResponse resp, string error)
		{
			resp.StatusCode = (int)HttpStatusCode.InternalServerError;
			resp.StatusDescription = "Error";
			using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
			{
				await writer.WriteAsync(JsonConvert.SerializeObject(error));
				await writer.FlushAsync();
			}
			await outputStream.Write(JsonConvert.SerializeObject(error), "text");

		}

		public async Task WriteToResponseHeader(string key, string value)
		{
			if (HttpListenerContext == null)
			{
				throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
			}
			HttpListenerContext.Response.AddHeader(key, value);
		}

		[Description("headerKey should be null unless specified by user")]
		public async Task<string> GetUserIp(string headerKey = null)
		{
			if (headerKey != null)
			{
				return HttpListenerContext.Request.Headers[headerKey];
			}
			return HttpListenerContext.Request.UserHostAddress;
		}

		public async Task<string> GetRequestHeader(string key)
		{
			if (HttpListenerContext == null)
			{
				throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
			}

			string ble = HttpListenerContext.Request.Headers[key];
			if (ble != null) return ble;

			ble = HttpListenerContext.Request.Headers[key.ToUpper()];
			if (ble != null) return ble;

			ble = HttpListenerContext.Request.Headers[key.ToLower()];
			if (ble != null) return ble;

			return "";
		}

		public async Task<string> GetCookie(string name)
		{
			if (HttpListenerContext.Request.Cookies.Count == 0) return "";

			var cookie = HttpListenerContext.Request.Cookies.FirstOrDefault(x => x.Name == name);
			if (cookie == null) return "";
			return cookie.Value;
		}
		public async Task WriteCookie(string name, string value, int expiresInSeconds = 60 * 60 * 24 * 7)
		{
			if (HttpListenerContext == null) return;

			var cookie = new Cookie(name, value);
			cookie.Expires = DateTime.Now.AddSeconds(expiresInSeconds);

			HttpListenerContext.Response.Cookies.Add(cookie);
		}


		public async Task DeleteCookie(string name, string value)
		{
			if (HttpListenerContext == null) return;

			var cookie = new Cookie(name, value);
			cookie.Expires = DateTime.Now.AddSeconds(-1);

			HttpListenerContext.Response.Cookies.Add(cookie);
		}


		private string GetGoalPath(List<string> publicPaths, HttpListenerRequest request)
		{
			if (request == null || request.Url == null) return "";

			var goalName = request.Url.LocalPath;
			if (goalName.StartsWith("/"))
			{
				goalName = goalName.Substring(1);
			}
			goalName = Path.GetFileNameWithoutExtension(goalName);

			var directories = fileSystem.Directory.GetDirectories(settings.BuildPath!, goalName, SearchOption.AllDirectories);

			foreach (var directory in directories)
			{
				foreach (var publicPath in publicPaths)
				{

					if (directory.StartsWith(Path.Combine(settings.BuildPath!, publicPath)))
					{
						return directory;
					}

				}
			}
			return "";
		}



		private async Task ParseRequest(HttpListenerContext? context, string? method, MemoryStack memoryStack)
		{
			if (context == null) return;

			var request = context.Request;
			string contentType = request.ContentType ?? "application/json";
			if (string.IsNullOrWhiteSpace(contentType))
			{
				throw new HttpRequestException("ContentType is missing");
			}
			if (method == null) return;

			if (request.HttpMethod != method)
			{
				throw new HttpRequestException($"Only {method} is supported. You sent {request.HttpMethod}");
			}
			string body = "";
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				body = await reader.ReadToEndAsync();
			}
			httpHelper.VerifySignature(request, body, memoryStack);


			if (request.HttpMethod == method)
			{
				var nvc = request.QueryString;
				foreach (var key in nvc.AllKeys)
				{
					if (key == null) continue;
					if (ReservedKeywords.IsReserved(key))
					{
						throw new HttpRequestException($"{key} is reserved. You cannot submit it to the server");
					}
					var value = nvc.Get(key) ?? "";
					memoryStack.Put(key, value);
				}
			}

			/*
			 * @ingig - Not really sure what is happening here, so decide to remove it for now. 
			if (request.HttpMethod == method && contentType.StartsWith("multipart/form-data"))
			{
				var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType), 70);
				var multipart = new MultipartReader(boundary, request.InputStream);

				while (true)
				{
					var section = await multipart.ReadNextSectionAsync();
					if (section == null) break;

					var formData = section.AsFormDataSection();
					memoryStack.Put(formData.Name, await formData.GetValueAsync());
				}
			}
			*/

			if (request.HttpMethod == method && contentType.StartsWith("application/json") && !string.IsNullOrEmpty(body))
			{

				var obj = JsonConvert.DeserializeObject(body) as JObject;
				memoryStack.Put("body", obj);
				try
				{
					var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
					foreach (var item in dict)
					{
						if (ReservedKeywords.IsReserved(item.Key))
						{
							throw new HttpRequestException($"{item.Key} is reserved. You cannot submit it to the server");
						}

						memoryStack.Put(item.Key, item.Value);
					}
				}
				catch {}


			}


			if (request.HttpMethod == method && contentType.StartsWith("application/x-www-form-urlencoded"))
			{
				var parsedFormData = System.Web.HttpUtility.ParseQueryString(body);
				foreach (var key in parsedFormData.AllKeys)
				{
					if (key == null) continue;
					if (ReservedKeywords.IsReserved(key))
					{
						throw new HttpRequestException($"{key} is reserved. You cannot submit it to the server");
					}

					var value = parsedFormData[key] ?? "";
					memoryStack.Put(key, value);
				}
			}

		}


		private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
		{
			var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
			if (string.IsNullOrWhiteSpace(boundary))
			{
				throw new InvalidDataException("Missing content-type boundary.");
			}
			if (boundary.Length > lengthLimit)
			{
				throw new InvalidDataException(
					$"Multipart boundary length limit {lengthLimit} exceeded.");
			}
			return boundary;
		}
	}
}

