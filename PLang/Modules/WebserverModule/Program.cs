using AngleSharp.Common;
using LightInject;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Net.Http.Headers;
using Nethereum.RPC.Eth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.Transformers;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.WebSockets;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UAParser;
using static Dapper.SqlMapper;
using static PLang.Runtime.Engine;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.WebserverModule;

[Description("Start webserver, add route, set certificate, read/write to Header, Cookie, send file to client")]
public class Program : BaseProgram, IDisposable
{
	private readonly ILogger logger;
	private readonly IEventRuntime eventRuntime;
	private readonly IPLangFileSystem fileSystem;
	private readonly ISettings settings;
	private readonly IOutputStreamFactory outputStreamFactory;
	private readonly PrParser prParser;
	private readonly IPseudoRuntime pseudoRuntime;
	private readonly IEngine engine;
	private readonly ProgramFactory programFactory;
	private readonly static List<WebserverProperties> listeners = new();
	private bool disposed;
	
	 
	public Program(ILogger logger, IEventRuntime eventRuntime, IPLangFileSystem fileSystem
		, ISettings settings, IOutputStreamFactory outputStreamFactory
		, PrParser prParser,
		IPseudoRuntime pseudoRuntime, IEngine engine, Modules.ProgramFactory programFactory) : base()
	{
		this.logger = logger;
		this.eventRuntime = eventRuntime;
		this.fileSystem = fileSystem;
		this.settings = settings;
		this.outputStreamFactory = outputStreamFactory;
		this.prParser = prParser;
		this.pseudoRuntime = pseudoRuntime;
		this.engine = engine;
		this.programFactory = programFactory;

	}



	public async Task<long> GetNumberOfLiveConnections()
	{
		if (listeners.Count == 0) return 0;

		return listeners.Sum(p => p.Engine.LiveConnections.Count);
		
	}

	public async Task<WebserverProperties?> ShutdownWebserver(string webserverName)
	{
		var webserverInfo = listeners.FirstOrDefault(p => p.Name == webserverName);
		if (webserverInfo == null)
		{
			await outputStreamFactory.CreateHandler().Write(goalStep, $"Webserver named '{webserverName}' does not exist");
			return null;
		}

		await webserverInfo.Listener.StopAsync();

		listeners.Remove(webserverInfo);
		return webserverInfo;
	}

	public async Task<bool> RestartWebserver(string webserverName = "default")
	{
		var webserverInfo = await ShutdownWebserver(webserverName);
		if (webserverInfo == null) return false;

		await StartWebserver(webserverInfo);

		return true;
	}

	public virtual void Dispose()
	{
		if (this.disposed)
		{
			return;
		}

		this.disposed = true;
	}

	protected virtual void ThrowIfDisposed()
	{
		if (this.disposed)
		{
			throw new ObjectDisposedException(this.GetType().FullName);
		}
	}
	

	[Description("Default Methods=[\"GET\"]")]
	public record RequestProperties(List<string> Methods, long MaxContentLengthInBytes = 1024 * 8, bool SignedRequestRequired = false)
	{
		public RequestProperties() : this(["GET"]) { }
	}
	public record ResponseProperties(string ContentType = "text/html", string ResponseEncoding = "utf-8");

	public record WebserverProperties(string Name = "default", string Host = "*", int Port = 8080,
		RequestProperties? DefaultRequestProperties = null, ResponseProperties? DefaultResponseProperties = null,
		GoalToCallInfo? OnStart = null, GoalToCallInfo? OnShutdown = null,
		GoalToCallInfo? OnRequestBegin = null, GoalToCallInfo? OnRequestEnd = null,
		GoalToCallInfo? OnPollStart = null, GoalToCallInfo? OnPollEnd = null
		)
		: IDisposable
	{
		[LlmIgnore]
		public IHost Listener { get; set; }

		[LlmIgnore]
		public List<Routing> Routings { get; set; } = new();

		[LlmIgnore]
		public X509Certificate2? Certificate { get; set; }

		[System.Text.Json.Serialization.JsonIgnore]
		[Newtonsoft.Json.JsonIgnore]
		public IEngine Engine { get; set; }

		public void Dispose()
		{
			Console.WriteLine($"Shutting down {Host}:{Port}");
			this.Listener.StopAsync();
		}
	}

	public async Task<(WebserverProperties?, IError?)> StartWebserver(WebserverProperties webserverProperties)
	{
		if (listeners.FirstOrDefault(p => p.Name == webserverProperties.Name) != null)
		{
			throw new RuntimeException($"Webserver '{webserverProperties.Name}' already exists. Give it a different name");
		}

		if (listeners.FirstOrDefault(p => p.Port == webserverProperties.Port) != null)
		{
			throw new RuntimeException($"Port {webserverProperties.Port} is already in use. Select different port to run on, e.g.\n-Start webserver, port 4687");
		}

		var assembly = Assembly.GetAssembly(this.GetType());
		string version = assembly!.GetName().Version!.ToString();

		var requestProperties = TypeHelper.SetProperties(webserverProperties.DefaultRequestProperties);
		var responseProperties = TypeHelper.SetProperties(webserverProperties.DefaultResponseProperties);
		webserverProperties = webserverProperties with { DefaultRequestProperties = requestProperties, DefaultResponseProperties = responseProperties };

		var webserverContainer = new ServiceContainer();
		webserverContainer.RegisterForPLangWebserver(goalStep, this.engine);

		var webserverEngine = webserverContainer.GetInstance<IEngine>();
		webserverEngine.Init(webserverContainer, context);
		webserverEngine.Name = "WebserverEngine";
		goal.AddVariable(webserverProperties);
		
		engine.ChildEngines.Add(webserverEngine);

		RequestHandler requestHandler = webserverContainer.GetInstance<RequestHandler>();

		if (webserverProperties.OnStart != null)
		{
			var (_, error) = await engine.RunGoal(webserverProperties.OnStart, goal);
			if (error != null) return (webserverProperties, error);
		}


		var builder = Host.CreateDefaultBuilder()
			.ConfigureLogging(l => l.ClearProviders())
			.ConfigureWebHostDefaults(web =>
			{

				web.UseKestrel(k =>
				{
					if (webserverProperties.Host == "localhost")
					{
						k.Listen(IPAddress.Loopback, webserverProperties.Port, l =>
						{
							l.Protocols = HttpProtocols.Http1AndHttp2;
							if (webserverProperties.Certificate != null)
							{
								l.UseHttps(webserverProperties.Certificate);
							}
						});

						k.Listen(IPAddress.IPv6Loopback, webserverProperties.Port, l =>
						{
							l.Protocols = HttpProtocols.Http1AndHttp2;
							if (webserverProperties.Certificate != null)
							{
								l.UseHttps(webserverProperties.Certificate);
							}
						});
					}
					else
					{


						IPAddress address = (webserverProperties.Host == "*") ? IPAddress.Any : IPAddress.TryParse(webserverProperties.Host, out var ip) ? ip : IPAddress.Any;
						k.Listen(address, webserverProperties.Port, l =>
						{
							l.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
							if (webserverProperties.Certificate != null)
							{
								l.UseHttps(webserverProperties.Certificate);
							}
						});
					}

					k.AddServerHeader = false;
				});
				web.ConfigureServices(s =>
				{
					s.AddSingleton(webserverProperties);
					s.AddResponseCompression(o =>
					{
						o.EnableForHttps = true;
						o.Providers.Add<GzipCompressionProvider>();
						o.Providers.Add<BrotliCompressionProvider>();
					});
					s.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
					s.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
				});

				web.Configure(app =>
				{
					app.UseForwardedHeaders();
					if (webserverProperties.Certificate != null)
					{
						app.UseHttpsRedirection();
					}
					app.UseResponseCompression();
					app.Run(async ctx => {
						IEngine? requestEngine = null;
						IError? error = null;
						bool poll = false;
						string? identity = null;
						Stopwatch stopwatch = Stopwatch.StartNew();
						try
						{
							logger.LogInformation(" ---------- Request Starts ({0}) ---------- {1}", ctx.Request.Path.Value, stopwatch.ElapsedMilliseconds);

							var httpOutputStream = new HttpOutputStream(ctx, webserverProperties, webserverEngine.LiveConnections);
							requestEngine = await webserverEngine.RentAsync(goalStep, httpOutputStream);
							requestEngine.HttpContext = ctx;
							requestEngine.Name = "RequestEngine_" + ctx.Request.Path.Value;
							(poll, identity, error) = await requestHandler.HandleRequestAsync(requestEngine, ctx, webserverProperties);

							if (error != null)
							{
								if (!ctx.Response.HasStarted)
								{
									ctx.Response.StatusCode = error.StatusCode;

									var encoding = Encoding.GetEncoding(webserverProperties.DefaultResponseProperties.ResponseEncoding);
									ctx.Response.ContentType = $"{webserverProperties.DefaultResponseProperties.ContentType}; charset={encoding.BodyName}";
								}

								if (error is StatelessCallbackError)
								{
									await requestEngine.OutputStream.Write(goalStep, error);
									return;
								}

								(_, error) = await requestEngine.GetEventRuntime().AppErrorEvents(error);
								
								if (error != null)
								{
									//AppError had error, this is a critical thing and should not happen
									//So we write the error to the console as last resort.	
									//
									try
									{

										await requestEngine.OutputStream.Write(goalStep, error);
									} catch (Exception ex)
									{
										string strError = error.ToString();
										Console.WriteLine(" ---- Could not write error to output stream - Critical Error  ---- ");
										Console.WriteLine(strError);
										Console.WriteLine(" ---- Critical Error  ---- ");
									}

								}
							} else
							{
								if (!ctx.Response.HasStarted)
								{
									ctx.Response.StatusCode = 200;
								}
							}
						}
						catch (Exception ex)
						{
							try
							{
								// something bad happend, write error using the original app engine
								(_, error) = await this.eventRuntime.AppErrorEvents(new ExceptionError(ex, ex.Message, goal, goalStep));
								if (error != null)
								{
									//AppError had error, this is a critical thing and should not happen
									//So we write the error to the console as last resort.								
									Console.WriteLine(" ---- Critical Error  ---- ");
									Console.WriteLine(error);
									Console.WriteLine(" ---- Critical Error  ---- ");
								}
							}
							catch (Exception ex2)
							{
								Console.WriteLine(" ---- Critical Exception  ---- ");
								Console.WriteLine(ex2);
								Console.WriteLine(ex);
								Console.WriteLine(" ---- Critical Exception  ---- ");
							}
						}
						finally
						{
							if (requestEngine != null)
							{
								webserverEngine.Return(requestEngine, true);
							}
							
							logger.LogInformation(" ---------- Request Done ({0}) ---------- {1}", ctx.Request.Path.Value, stopwatch.ElapsedMilliseconds);

						}

						if (poll)
						{
							await DoPoll(ctx, identity, webserverEngine, webserverProperties);
						}
					});

				});
			});

		var app = builder.Build();


		webserverProperties.Listener = app;
		webserverProperties.Engine = webserverEngine;

		await app.StartAsync();

		listeners.Add(webserverProperties);

		logger.LogDebug($"Listening on {webserverProperties.Host}:{webserverProperties.Port}...");
		Console.WriteLine($" - Listening on {webserverProperties.Host}:{webserverProperties.Port}...");

		KeepAlive(listeners, "Webserver");

		return (webserverProperties, null);
	}

	private async Task DoPoll(HttpContext context, string? identity, IEngine webserverEngine, WebserverProperties webserverProperties)
	{
		if (string.IsNullOrEmpty(identity)) return;

		var response = context.Response;

		try
		{
			var payload = JsonConvert.SerializeObject("ping");
			var buffer = Encoding.UTF8.GetBytes(payload + Environment.NewLine);

			var ct = context.RequestAborted;
			await response.StartAsync(ct);

			while (!ct.IsCancellationRequested)
			{
				await response.Body.WriteAsync(buffer, 0, buffer.Length, ct);
				await response.Body.FlushAsync(ct);
				await Task.Delay(TimeSpan.FromSeconds(20), ct);
			}

			if (ct.IsCancellationRequested)
			{
				int b = 0;
			}

		}
		catch (OperationCanceledException)
		{
			webserverEngine.LiveConnections.Remove(identity, out _);
			if (webserverProperties.OnPollEnd != null)
			{
				//run onpoll end
			}
		}
		catch (Exception ex)
		{
			webserverEngine.LiveConnections.Remove(identity, out _);
			if (webserverProperties.OnPollEnd != null)
			{
				//run onpoll end
			}
			Console.WriteLine(ex);
		} finally
		{
			try { await response.CompleteAsync(); } catch (Exception ex) {

				int i = 0;
			}
		}
	}

	public async Task<IError?> SetCertificate(string permFilePath, string? privateKeyFile = null)
	{
		var webserver = goal.GetVariable<WebserverProperties>();
		if (webserver == null)
		{
			return new ProgramError("You can only set certificate on start of webserver", goalStep,
				FixSuggestion: @"Add on start to call a goal, e.g. `- start webserver, on start call OnStartingWebserver`, then in in the OnStartingWebserver goal add each route, e.g.
```plang
OnStartingWebserver
- set certificate, ""premFile.pem"", ""privatekey.pem""
```


");
		}
		if (!string.IsNullOrEmpty(permFilePath))
		{
			if (!File.Exists(permFilePath))
			{
				return new ProgramError($"Could not find {permFilePath}");
			}
		}
		if (!string.IsNullOrEmpty(privateKeyFile))
		{
			if (!File.Exists(privateKeyFile))
			{
				return new ProgramError($"Could not find {privateKeyFile}");
			}
		}

		var cert = X509Certificate2
		   .CreateFromPemFile(permFilePath, privateKeyFile);
		webserver.Certificate = cert;
		return null;
	}

	public record ParamInfo(string Name, string VariableOrValue, string Type, string? RegexValidation = null, string? ErrorMessage = null, object? DefaultValue = null);
	public record GoalToCallWithParamInfo(string Name, List<ParamInfo> Parameters);
	public record Routing(string Path, Route Route, RequestProperties RequestProperties, ResponseProperties ResponseProperties);
	public record Route(Regex PathRegex, Dictionary<string, string>? QueryMap, GoalToCallInfo Goal);

	public async Task<IError?> AddRoute([HandlesVariable] string path, List<ParamInfo> pathParameters, GoalToCallInfo goalToCall,
		RequestProperties? requestProperties = null, ResponseProperties? responseProperties = null)
	{
		var webserverInfo = goal.GetVariable<WebserverProperties>();
		if (webserverInfo == null)
		{
			return new ProgramError("You can only add route on start of webserver", goalStep,
				FixSuggestion: @"Add on start to call a goal, e.g. `- start webserver, on start call AddRoutes`, then in in the AddRoutes goal add each route, e.g.
```plang
AddRoutes
- add route ""/user"", call goal User
- add route ""/cart"", call goal Cart
```
");
		}

		requestProperties = TypeHelper.SetProperties(requestProperties);
		responseProperties = TypeHelper.SetProperties(responseProperties);

		if (goalToCall == null)
		{
			var goal = prParser.GetAllGoals().FirstOrDefault(p => p.RelativeGoalPath.Replace(".goal", "").Equals(path, StringComparison.OrdinalIgnoreCase));
			if (goal == null) return new ProgramError($"Could not find goal {path}.goal");

			goalToCall = new GoalToCallInfo(goal.GoalName)
			{
				Path = goal.RelativePrPath
			};
		}

		var route = BuildRoute(path, pathParameters, goalToCall);

		webserverInfo.Routings.Add(new Routing(path, route, requestProperties, responseProperties));

		return null;
	}



	private Program.Route BuildRoute(string pattern, List<ParamInfo> paramInfos, GoalToCallInfo goal)
	{
		// split path / query
		var queryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var qm = pattern.IndexOf('?', StringComparison.Ordinal);
		var pathPart = qm >= 0 ? pattern[..qm] : pattern;
		var queryPart = qm >= 0 ? pattern[(qm + 1)..] : null;

		// build path regex
		var regex = new StringBuilder("^");
		for (int i = 0; i < pathPart.Length;)
		{
			if (pathPart[i] == '%')
			{
				var j = pathPart.IndexOf('%', i + 1);
				var name = pathPart[(i + 1)..j];
				regex.Append($"(?<{name}>[^/?&]+)");   // stop at / ? & or - (hyphen safe)
				i = j + 1;
			}
			else
			{
				regex.Append(Regex.Escape(pathPart[i].ToString()));
				i++;
			}
		}
		regex.Append(@"\/?$");

		// collect query placeholders: q=%q%  =>  "q" ↦ "q"
		if (queryPart != null)
		{
			foreach (var kv in queryPart.Split('&', StringSplitOptions.RemoveEmptyEntries))
			{
				var pair = kv.Split('=', 2);
				if (pair.Length == 2 && pair[1].StartsWith('%') && pair[1].EndsWith('%'))
					queryMap[pair[0]] = pair[1][1..^1]; // strip %
			}
		}

		return new Program.Route(new Regex(regex.ToString(),
								   RegexOptions.Compiled | RegexOptions.IgnoreCase),
						 queryMap.Count == 0 ? null : queryMap,
						 goal);
	}

	public record CertInfo(string FileName, string Password);

	public async Task<IError?> SetSelfSignedCertificate()
	{
		var webserver = goal.GetVariable<WebserverProperties>();
		if (webserver == null)
		{
			return new ProgramError("You can only set certificate on start of webserver", goalStep,
				FixSuggestion: @"Add on start to call a goal, e.g. `- start webserver, on start call OnStartingWebserver`, then in in the OnStartingWebserver goal add each route, e.g.
```plang
OnStartingWebserver
- set self signed cert
```


");
		}
		var certHelper = container.GetInstance<ICertHelper>();
		var certResult = certHelper.GetOrCreateCert(null);
		if (certResult.Error != null) return certResult.Error;

		webserver.Certificate = certResult.Certificate;
		return null;
	}

	public async Task<IError?> Redirect(string url, bool permanent = false, bool preserveMethod = false)
	{
		var os = engine.OutputStream;
		if (os is HttpOutputStream hos)
		{
			(var response, var isFlushed, var error) = hos.GetResponse();
			if (response != null && !isFlushed && !response.HasStarted)
			{
				response.Redirect(url, permanent, preserveMethod);
				await response.Body.FlushAsync();
				await response.CompleteAsync();
				os.IsFlushed = true;
				hos.IsComplete = true;

			}
		}

		var topGoal = goal;
		int counter=0;
		while (topGoal.ParentGoal != null)
		{
			topGoal = topGoal.ParentGoal;
			if (counter++ > 100)
			{
				Console.WriteLine($"To deep: Webserver.Redirect - goalName: {topGoal.GoalName}");
				break;
			}
		}


		return new EndGoal(topGoal, goalStep, "Redirect", Levels: 999);
		/*
		if (HttpContext == null)
		{
			return new ProgramError("Header has been sent to browser. Redirect cannot be sent after that.", StatusCode: 500);
		}
		if (HttpContext.Response.HasStarted) return new ProgramError("Header has been sent to browser. Redirect cannot be sent after that.", StatusCode: 500);

		HttpContext.Response.Redirect(url, permanent, preserveMethod);
		await HttpContext.Response.Body.FlushAsync();
		*/

	}

	public async Task WriteToResponseHeader(Dictionary<string, object> headers)
	{
		if (HttpContext == null)
		{
			throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
		}
		foreach (var header in headers)
		{
			if (header.Value == null) continue;
			HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToString());
		}
	}

	[Description("headerKey should be null unless specified by user")]
	public async Task<string?> GetUserIp(string? headerKey = null)
	{
		if (HttpContext == null) return null;

		if (headerKey != null)
		{

			if (HttpContext.Request.Headers.TryGetValue(headerKey, out var value))
			{
				return value;
			}
		}
		return HttpContext.Request.Host.Host;
	}

	public async Task<string> GetRequestHeader(string key)
	{
		if (HttpContext == null)
		{
			throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
		}

		string? headerValue = HttpContext.Request.Headers[key];
		if (headerValue != null) return headerValue;

		headerValue = HttpContext.Request.Headers[key.ToUpper()];
		if (headerValue != null) return headerValue;

		headerValue = HttpContext.Request.Headers[key.ToLower()];
		if (headerValue != null) return headerValue;

		return "";
	}

	string missingHttpContextFixSuggestion = @"This can only be called in a web request, e.g. `- start webserver
- add route /, call Frontpage

Frontpage
- get cookie 'name', write to %cookieValue%
";

	public async Task<(string?, IError?)> GetCookie(string name)
	{
		if (HttpContext == null) return (null, new ProgramError("HttpContext is empty. Is this being called during a web request?",
			FixSuggestion: missingHttpContextFixSuggestion));


		HttpContext.Request.Cookies.TryGetValue(name, out var value);
		return (value, null);
	}

	public async Task<IError?> WriteCookie(string name, string value, int expiresInSeconds = 60 * 60 * 24 * 7)
	{
		CookieOptions options = new();
		options.Expires = DateTime.Now.AddSeconds(expiresInSeconds);
		options.HttpOnly = true;
		options.Secure = true;

		var os = engine.OutputStream;
		if (os is HttpOutputStream hos)
		{
			(var response, var isFlushed, var error) = hos.GetResponse();
			if (response != null)
			{
				response.Cookies.Append(name, value, options);
			}
		}
		return null;
	}


	public async Task<IError?> DeleteCookie(string name)
	{


		HttpContext.Response.Cookies.Delete(name);
		return null;
	}

	public async Task<IError?> SendFileToClient(string path, string? fileName = null)
	{
		var absolutePath = GetPath(path);
		
		if (!fileSystem.File.Exists(absolutePath))
		{
			return new NotFoundError("File not found");
		}

		var mimeType = RequestHandler.GetMimeType(path);
		if (mimeType == null) mimeType = "application/octet-stream";

		var response = HttpContext.Response;
		response.StatusCode = StatusCodes.Status200OK;
		response.ContentType = mimeType;

		if (string.IsNullOrEmpty(fileName))
		{
			var fileInfo = fileSystem.FileInfo.New(absolutePath);
			fileName = fileInfo.Name;
		}


		var cd = new ContentDispositionHeaderValue("attachment");
		cd.SetHttpFileName(fileName);
		response.Headers[HeaderNames.ContentDisposition] = cd.ToString();

		await using var s = fileSystem.File.OpenRead(absolutePath);
		response.ContentLength = s.Length;      
		await s.CopyToAsync(response.Body, response.HttpContext.RequestAborted);
		await response.Body.FlushAsync(response.HttpContext.RequestAborted);
		

		return null;
	}



	private async Task<IError?> ProcessWebsocketRequest(HttpListenerContext httpContext, IOutputStream outputStream)
	{
		return new Error("Not Supported");
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
	public record WebSocketInfo(ClientWebSocket ClientWebSocket, string Url, GoalToCallInfo GoalToCall, string WebSocketName, string ContentRecievedVariableName);
	public record WebSocketData(GoalToCallInfo GoalToCall, string Url, string Method, string Contract)
	{
		public Dictionary<string, object?> Parameters = new();
		public SignedMessage? Signature = null;
	};
	public async Task SendToWebSocket(object data, Dictionary<string, object?>? headers = null, string webSocketName = "default")
	{
		throw new NotImplementedException();
	}

	public async Task SendToWebSocket(GoalToCallInfo goalToCall, Dictionary<string, object?>? parameters = null, string webSocketName = "default")
	{
		var webSocketInfo = websocketInfos.FirstOrDefault(p => p.WebSocketName == webSocketName);
		if (webSocketInfo == null)
		{
			throw new RuntimeException($"Websocket with name '{webSocketName}' does not exists");
		}

		string url = webSocketInfo.Url;
		string method = "Websocket";
		string[] contracts = ["C0"];

		var obj = new WebSocketData(goalToCall, url, method, null);
		obj.Parameters = parameters;

		var signature = await programFactory.GetProgram<Modules.IdentityModule.Program>(goalStep).Sign(obj);
		obj.Signature = signature;

		byte[] message = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));

		await webSocketInfo.ClientWebSocket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true, CancellationToken.None);

	}
	public async Task<WebSocketInfo> StartWebSocketConnection(string url, GoalToCallInfo goalToCall, string webSocketName = "default", string contentRecievedVariableName = "%content%")
	{
		if (string.IsNullOrEmpty(url))
		{
			throw new RuntimeException($"url cannot be empty");
		}

		if (goalToCall != null)
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

		_ = Task.Run(async () =>
		{
			byte[] buffer = new byte[1024];
			MemoryStream messageStream = new MemoryStream();
			while (true)
			{

				WebSocketReceiveResult result;
				do
				{
					result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					await messageStream.WriteAsync(buffer, 0, result.Count);
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

					var signature = websocketData.Signature;
					var verifiedSignature = await programFactory.GetProgram<Modules.IdentityModule.Program>(goalStep).VerifySignature(signature);
					// todo: missing verifiedSignature.Error check
					if (verifiedSignature.Signature == null)
					{
						continue;
					}

					websocketData.GoalToCall.Parameters.AddOrReplace(ReservedKeywords.Identity, verifiedSignature.Signature.Identity);

					await pseudoRuntime.RunGoal(engine, context, fileSystem.RootDirectory, websocketData.GoalToCall);
				}
				messageStream.SetLength(0);
			}
			messageStream.Dispose();
		});

		return webSocketInfo;
	}





}


