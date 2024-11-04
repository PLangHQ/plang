using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Tls;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.SigningService;
using PLang.Utils;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace PLang.Modules.WebserverModule
{
	[Description("Start webserver, write to Body, Header, Cookie, send file to client")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly IEventRuntime eventRuntime;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
		private readonly PrParser prParser;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly IPLangSigningService signingService;
		private readonly IPLangIdentityService identityService;
		private readonly static List<WebserverInfo> listeners = new();

		public Program(ILogger logger, IEventRuntime eventRuntime, IPLangFileSystem fileSystem
			, ISettings settings, IOutputSystemStreamFactory outputSystemStreamFactory
			, PrParser prParser,
			IPseudoRuntime pseudoRuntime, IEngine engine, IPLangSigningService signingService, IPLangIdentityService identityService) : base()
		{
			this.logger = logger;
			this.eventRuntime = eventRuntime;
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
			this.prParser = prParser;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.signingService = signingService;
			this.identityService = identityService;
		}

		public async Task<(WebserverInfo? WebserverInfo, IError? Error)> ShutdownWebserver(string webserverName)
		{
			var webserverInfo = listeners.FirstOrDefault(p => p.WebserverName == webserverName);
			if (webserverInfo == null)
			{
				return (null, new ProgramError($"Webserver named '{webserverName}' does not exist", goalStep, function));
			}

			try
			{
				webserverInfo.Listener.Close();
			}
			catch
			{
				webserverInfo.Listener.Abort();
			}
			listeners.Remove(webserverInfo);
			return (webserverInfo, null);
		}

		public async Task<(WebserverInfo? WebserverInfo, IError? Error)> RestartWebserver(string webserverName = "default")
		{
			var result = await ShutdownWebserver(webserverName);
			if (result.Error != null) return result;

			var webserverInfo = result.WebserverInfo!;

			result = await StartWebserver(webserverInfo.WebserverName, webserverInfo.Scheme, webserverInfo.Host, webserverInfo.Port,
				webserverInfo.DefaultMaxContentLengthInBytes, webserverInfo.DefaultResponseContentEncoding, webserverInfo.SignedRequestRequired);
			if (result.Error != null) return result;

			result.WebserverInfo.Routings = webserverInfo.Routings;

			return result;
		}


		public async Task<IError?> AddRouteToStaticFile(string path, string fileName, string contentType = "text/html", 
				string? webserverName = null, long? cacheTimeInMilliseconds = 0, string? cacheKey = null, int? cacheType = null)
		{
			(var webserverInfo, var error) = GetWebserverInfo(webserverName);
			if (error != null) return error;

			CacheInfo? cacheInfo = null;
			if (cacheTimeInMilliseconds != null && cacheTimeInMilliseconds > 0)
			{
				if (string.IsNullOrWhiteSpace(cacheKey)) cacheKey = path + "_" + fileName;
				cacheInfo = new CacheInfo(cacheTimeInMilliseconds.Value, cacheKey, cacheType ?? 1);
			}

			webserverInfo.Routings.Add(new StaticFileRouting(path, fileName, contentType, cacheInfo));
			return null;
		}

		public async Task<IError?> AddFolderToRoute(string path, string? folder = null, string? method = null, string ContentType = "text/html",
									Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null,
									string? webserverName = null, long? cacheTimeInMilliseconds = 0, string? cacheKey = null, int? cacheType = null)
		{

			(var webserverInfo, var error) = GetWebserverInfo(webserverName);
			if (error != null) return error;

			CacheInfo? cacheInfo = null;
			if (cacheTimeInMilliseconds != null && cacheTimeInMilliseconds > 0)
			{
				if (string.IsNullOrWhiteSpace(cacheKey)) cacheKey = path + "_" + folder;
				cacheInfo = new CacheInfo(cacheTimeInMilliseconds.Value, cacheKey, cacheType ?? 1);
			}
			webserverInfo.Routings.Add(new FolderRouting(path, folder, method, ContentType, Parameters, MaxContentLength, DefaultResponseContentEncoding));

			return null;
		}


		public async Task<IError?> AddGoalToCallRoute(string path, GoalToCall? goalToCall = null, string? method = null, string ContentType = "text/html",
									Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null, 
									string? webserverName = null, long? cacheTimeInMilliseconds = 0, string? cacheKey = null, int? cacheType = null)
		{

			(var webserverInfo, var error) = GetWebserverInfo(webserverName);
			if (error != null) return error;

			CacheInfo? cacheInfo = null;
			if (cacheTimeInMilliseconds != null && cacheTimeInMilliseconds > 0)
			{
				if (string.IsNullOrWhiteSpace(cacheKey)) cacheKey = path + "_" + goalToCall;
				cacheInfo = new CacheInfo(cacheTimeInMilliseconds.Value, cacheKey, cacheType ?? 1);
			}
			webserverInfo.Routings.Add(new GoalRouting(path, goalToCall, method, ContentType, Parameters, MaxContentLength, DefaultResponseContentEncoding, cacheInfo));

			return null;
		}
		private (WebserverInfo? WebserverInfo, IError? Error) GetWebserverInfo(string? webserverName = null)
		{
			WebserverInfo? webserverInfo = null;
			if (webserverName != null)
			{
				webserverInfo = listeners.FirstOrDefault(p => p.WebserverName == webserverName);
				if (webserverInfo == null)
				{
					return (null, new ProgramError($"Could not find {webserverName} webserver. Are you defining the correct name?", goalStep, function));
				}
			}
			else if (listeners.Count > 1)
			{
				return (null, new ProgramError($"There are {listeners.Count} servers, please define which webserver you want to assign this routing to.", goalStep, function,
						FixSuggestion: $"rewrite the step to include the server name e.g. `- {goalStep.Text}, on {listeners[0].WebserverName} webserver"));
			}
			else if (listeners.Count == 0)
			{
				return (null, new ProgramError($"There are 0 servers, please define a webserver.", goalStep, function,
						FixSuggestion: $"create a step before adding a route e.g. `- start webserver"));
			}

			if (webserverInfo == null) webserverInfo = listeners[0];

			if (webserverInfo.Routings == null) webserverInfo.Routings = new();
			return (webserverInfo, null);
		}

		public async Task<(WebserverInfo? WebServerInfo, IError? Error)> StartWebserver(string webserverName = "default", string scheme = "http", string host = "localhost",
			int port = 8080, long maxContentLengthInBytes = 4096 * 1024,
			string defaultResponseContentEncoding = "utf-8",
			bool signedRequestRequired = false)
		{
			if (listeners.FirstOrDefault(p => p.WebserverName == webserverName) != null)
			{
				return (null, new ProgramError($"Webserver '{webserverName}' already exists. Give it a different name", goalStep, function));
			}

			if (listeners.FirstOrDefault(p => p.Port == port) != null)
			{
				return (null, new ProgramError($"Port {port} is already in use. Select different port to run on, e.g.\n-Start webserver, port 4687", goalStep, function));
			}

			var listener = new HttpListener();
			listener.Prefixes.Add(scheme + "://" + host + ":" + port + "/");			

			var assembly = Assembly.GetAssembly(this.GetType());
			string version = assembly!.GetName().Version!.ToString();

			var webserverInfo = new WebserverInfo(listener, webserverName, scheme, host, port, maxContentLengthInBytes, defaultResponseContentEncoding, signedRequestRequired);
			listeners.Add(webserverInfo);
			
			listener.Start();
			logger.LogWarning($"Listening on {scheme}://{host}:{port}...");

			KeepAlive(listener, "Webserver");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Task.Run(async () =>
			{
				try
				{
					while (true)
					{
						var container = new ServiceContainer();
						var context = listener.GetContext();
						var requestHandler = new RequestHandler(context, container, webserverInfo, version, goalStep);
						try
						{
							var error = requestHandler.HandleRequest();
							if (error != null)
							{
								await ShowError(container, error);
								continue;
							}
						} 
						catch (Exception ex)
						{
							logger.LogError(ex, @"WebServerException - {0}", ex.ToString());
							var error = new Error(ex.Message, Key: "WebserverCore", 500, ex);
							try
							{
								var errorHandlerFactory = container.GetInstance<IErrorHandlerFactory>();
								var handler = errorHandlerFactory.CreateHandler();
								await handler.ShowError(error);
								continue;
							}
							catch (Exception ex2)
							{
								Console.WriteLine("Original exception:" + JsonConvert.SerializeObject(ex));
								Console.WriteLine("Exception while handling original exception:" + JsonConvert.SerializeObject(ex2));
							}
						}
						finally
						{
							context.Response.Close();
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Webserver crashed");
				}
			});
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

			return (webserverInfo, null);
		}

		private async Task ShowError(ServiceContainer container, IError error)
		{
			if (error is UserDefinedError)
			{
				var errorHandlerFactory = container.GetInstance<IErrorHandlerFactory>();
				var handler = errorHandlerFactory.CreateHandler();
				await handler.ShowError(error);
			}
			else
			{
				var errorHandlerFactory = container.GetInstance<IErrorSystemHandlerFactory>();
				var handler = errorHandlerFactory.CreateHandler();
				await handler.ShowError(error);
			}
		}

		private async Task WriteNotfound(HttpListenerResponse resp, string error)
		{
			resp.StatusCode = (int)HttpStatusCode.NotFound;

			await outputSystemStreamFactory.CreateHandler().Write(JsonConvert.SerializeObject(error), "text");

		}
		private async Task WriteError(HttpListenerResponse resp, IError error)
		{
			resp.StatusCode = error.StatusCode;
			resp.StatusDescription = error.Key;
			using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
			{
				await writer.WriteAsync(JsonConvert.SerializeObject(error));
				await writer.FlushAsync();
			}
			await outputSystemStreamFactory.CreateHandler().Write(JsonConvert.SerializeObject(error), "text");

		}

		public async Task Redirect(string url)
		{
			if (HttpListenerContext == null)
			{
				throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
			}

			HttpListenerContext.Response.Redirect(url);
		}

		public async Task WriteToResponseHeader(Dictionary<string, object> headers)
		{
			if (HttpListenerContext == null)
			{
				throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
			}
			foreach (var header in headers)
			{
				HttpListenerContext.Response.AddHeader(header.Key, header.Value.ToString());
			}
		}

		[Description("headerKey should be null unless specified by user")]
		public async Task<string?> GetUserIp(string? headerKey = null)
		{
			if (headerKey != null)
			{
				if (HttpListenerContext.Request.Headers != null && HttpListenerContext.Request.Headers.AllKeys.Contains(headerKey))
				{
					return HttpListenerContext.Request.Headers[headerKey];
				}
			}
			return HttpListenerContext.Request.UserHostAddress;
		}

		public async Task<string> GetRequestHeader(string key)
		{
			if (HttpListenerContext == null)
			{
				throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
			}

			string? headerValue = HttpListenerContext.Request.Headers[key];
			if (headerValue != null) return headerValue;

			headerValue = HttpListenerContext.Request.Headers[key.ToUpper()];
			if (headerValue != null) return headerValue;

			headerValue = HttpListenerContext.Request.Headers[key.ToLower()];
			if (headerValue != null) return headerValue;

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

			var cookie = new System.Net.Cookie(name, value);
			cookie.Expires = DateTime.Now.AddSeconds(expiresInSeconds);

			HttpListenerContext.Response.Cookies.Add(cookie);
		}


		public async Task DeleteCookie(string name)
		{
			if (HttpListenerContext == null) return;

			var cookie = new Cookie(name, null);
			cookie.Expires = DateTime.Now.AddSeconds(-1);

			HttpListenerContext.Response.Cookies.Add(cookie);
		}

		public async Task SendFileToClient(string path, string? fileName = null, string? mimeType = null)
		{
			var response = HttpListenerContext.Response;
			if (!fileSystem.File.Exists(path))
			{
				response.StatusCode = (int)HttpStatusCode.NotFound;
				using (StreamWriter writer = new StreamWriter(response.OutputStream))
				{
					writer.Write("File not found.");
				}
				response.Close();
				return;
			}
			
			response.ContentType = mimeType ?? MimeTypeHelper.GetWebMimeType(path);

			var fileInfo = fileSystem.FileInfo.New(path);
			response.ContentLength64 = fileInfo.Length;
			if (string.IsNullOrEmpty(fileName)) fileName = fileInfo.Name;

			response.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");

			using (var fs = fileSystem.File.OpenRead(path))
			{
				fs.CopyTo(response.OutputStream);
			}

			response.StatusCode = (int)HttpStatusCode.OK;
			response.Close();
		}

	
		
		private string GetGoalBuildDirPath(GoalRouting route)
		{
			if (route.GoalToCall == null) return "";

			var goalName = route.GoalToCall.ToString()?.AdjustPathToOs();

			return prParser.GetGoalByAppAndGoalName(route.GoalToCall);

			string goalBuildDirPath = Path.Join(fileSystem.BuildPath, goalName).AdjustPathToOs();
			if (fileSystem.Directory.Exists(goalBuildDirPath)) return goalBuildDirPath;

			logger.LogDebug($"Path doesnt exists - goalBuildDirPath:{goalBuildDirPath}");
			return "";
			
		}


		private async Task ProcessWebsocketRequest(HttpListenerContext httpContext)
		{
			/*
			 * Not tested, so remove for now.
			 * 
			HttpListenerWebSocketContext webSocketContext = await httpContext.AcceptWebSocketAsync(subProtocol: null);
			WebSocket webSocket = webSocketContext.WebSocket;

			try
			{

				var outputStream = new WebsocketOutputStream(webSocket, signingService);
				var container = new ServiceContainer();
				container.RegisterForPLangWebserver(goal.AbsoluteAppStartupFolderPath, goal.RelativeGoalFolderPath, httpContext);

				var context = container.GetInstance<PLangAppContext>();
				context.Add(ReservedKeywords.IsHttpRequest, true);

				var engine = container.GetInstance<IEngine>();
				engine.Init(container);
				engine.HttpContext = httpContext;

				byte[] buffer = new byte[1024];

				while (webSocket.State == WebSocketState.Open)
				{
					var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

					if (result.MessageType == WebSocketMessageType.Close)
					{
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
					else if (result.MessageType == WebSocketMessageType.Text)
					{
						string receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
					}
				}
				await engine.RunGoal(goal);

				await webSocket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), CancellationToken.None);
			}
			catch (Exception e)
			{
				logger.LogError(e.Message, e);
				Console.WriteLine("Exception: " + e.Message);
			}
			finally
			{
				if (webSocket != null)
					webSocket.Dispose();
			}
			*/
		}


		private List<WebSocketInfo> websocketInfos = new List<WebSocketInfo>();
		public record WebSocketInfo(ClientWebSocket ClientWebSocket, string Url, GoalToCall GoalToCall, string WebSocketName, string ContentRecievedVariableName);
		public record WebSocketData(GoalToCall GoalToCall, string Url, string Method, string Contract)
		{
			public Dictionary<string, object?> Parameters = new();
			public Dictionary<string, object>? SignatureData = null;
		};


		public async Task SendToWebSocket(GoalToCall goalToCall, Dictionary<string, object?>? parameters = null, string webSocketName = "default")
		{
			var webSocketInfo = websocketInfos.FirstOrDefault(p => p.WebSocketName == webSocketName);
			if (webSocketInfo == null)
			{
				throw new RuntimeException($"Websocket with name '{webSocketName}' does not exists");
			}

			string url = webSocketInfo.Url;
			string method = "Websocket";
			string contract = "C0";

			var obj = new WebSocketData(goalToCall, url, method, contract);
			obj.Parameters = parameters;

			var signatureData = signingService.Sign(JsonConvert.SerializeObject(obj), method, url, contract);
			obj.SignatureData = signatureData;

			byte[] message = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));

			await webSocketInfo.ClientWebSocket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true, CancellationToken.None);

		}
		public async Task<WebSocketInfo> StartWebSocketConnection(string url, GoalToCall goalToCall, string webSocketName = "default", string contentRecievedVariableName = "%content%")
		{
			if (string.IsNullOrEmpty(url))
			{
				throw new RuntimeException($"url cannot be empty");
			}

			if (string.IsNullOrEmpty(goalToCall))
			{
				throw new RuntimeException($"goalToCall cannot be empty");
			}

			if (!url.StartsWith("ws://") && !url.StartsWith("wss://"))
			{
				throw new RuntimeException($"url must start with ws:// or wss://. You url is '{url}'");
			}

			if (websocketInfos.FirstOrDefault(p => p.WebSocketName == webSocketName) != null)
			{
				throw new RuntimeException($"Websocket with name '{webSocketName}' already exists");
			}

			ClientWebSocket client = new ClientWebSocket();
			await client.ConnectAsync(new Uri(url), CancellationToken.None);
			var webSocketInfo = new WebSocketInfo(client, url, goalToCall, webSocketName, contentRecievedVariableName);

			websocketInfos.Add(webSocketInfo);

			Console.WriteLine("Connected to the server");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Task.Run(async () =>
			{
				byte[] buffer = new byte[1024];
				MemoryStream messageStream = new MemoryStream();
				while (true)
				{

					WebSocketReceiveResult result;
					do
					{
						result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
						messageStream.Write(buffer, 0, result.Count);
					}
					while (!result.EndOfMessage);

					messageStream.Seek(0, SeekOrigin.Begin);

					if (result.MessageType == WebSocketMessageType.Close)
					{
						await client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
						break;
					}
					else if (result.MessageType == WebSocketMessageType.Text)
					{
						StreamReader reader = new StreamReader(messageStream, Encoding.UTF8);
						string receivedMessage = await reader.ReadToEndAsync();
						reader.Dispose();

						var websocketData = JsonConvert.DeserializeObject<WebSocketData>(receivedMessage);
						if (websocketData == null || string.IsNullOrEmpty(websocketData.GoalToCall))
						{
							continue;
						}

						var signatureData = websocketData.SignatureData;
						var identity = await signingService.VerifySignature(JsonConvert.SerializeObject(websocketData), websocketData.Method, websocketData.Url, signatureData);

						websocketData.SignatureData = null;
						websocketData.Parameters.AddOrReplace(identity);

						await pseudoRuntime.RunGoal(engine, context, fileSystem.RootDirectory, websocketData.GoalToCall, websocketData.Parameters);
					}
					messageStream.SetLength(0);
				}
			});

			return webSocketInfo;
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

