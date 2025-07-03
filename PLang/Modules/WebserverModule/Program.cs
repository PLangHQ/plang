using AngleSharp.Common;
using AngleSharp.Io;
using LightInject;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Errors.Interfaces;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.OutputStream;
using PLang.Services.SigningService;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Net.WebSockets;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using static Dapper.SqlMapper;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.WebserverModule;

[Description("Start webserver, write to Body, Header, Cookie, send file to client")]
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
	private readonly static List<WebserverInfo> listeners = new();
	private bool disposed;
	ConcurrentDictionary<string, LiveConnection> liveConnections;

	public record LiveConnection(Microsoft.AspNetCore.Http.HttpResponse Response, bool IsFlushed, GoalToCallInfo? OnConnect = null, GoalToCallInfo? OnDisconnect = null)
	{
		public bool IsFlushed { get; set; } = IsFlushed;
	};

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

		liveConnections = new();
	}



	public async Task<long> GetNumberOfLiveConnections()
	{
		return liveConnections.Count;
	}

	public async Task<WebserverInfo?> ShutdownWebserver(string webserverName)
	{
		var webserverInfo = listeners.FirstOrDefault(p => p.WebserverName == webserverName);
		if (webserverInfo == null)
		{
			await outputStreamFactory.CreateHandler().Write($"Webserver named '{webserverName}' does not exist");
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

		await StartWebserver(webserverInfo.WebserverName, webserverInfo.Scheme, webserverInfo.Host, webserverInfo.Port,
			webserverInfo.MaxContentLengthInBytes, webserverInfo.DefaultResponseContentEncoding, webserverInfo.SignedRequestRequired, webserverInfo.Routings);

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

	public record WebserverInfo(string WebserverName, string Scheme, string Host, int Port,
		long MaxContentLengthInBytes, string DefaultResponseContentEncoding, bool SignedRequestRequired, List<Routing>? Routings)
	{
		public IHost Listener { get; set; }
		public List<Routing>? Routings { get; set; } = Routings;
	}


	public record Routing(string Path, GoalToCallInfo? GoalToCall = null, string[]? Method = null, string ContentType = "text/html",
								Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null);

	[Description("When path is /api, overwite the default ContentType value to application/json unless defined by user")]
	public async Task<IError?> AddRoute(string path, GoalToCallInfo? goalToCall = null, string[]? method = null, string ContentType = "text/html",
								Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null, string? DefaultResponseContentEncoding = null,
								string? webserverName = "default")
	{
		WebserverInfo? webserverInfo = null;
		if (webserverName != null)
		{
			webserverInfo = (listeners.Count == 1) ? listeners[0] : listeners.FirstOrDefault(p => p.WebserverName == webserverName);
			if (webserverInfo == null)
			{
				return new ProgramError($"Could not find {webserverName} webserver. Are you defining the correct name?", goalStep, function);
			}
		}
		else if (listeners.Count > 1)
		{
			return new ProgramError($"There are {listeners.Count} servers, please define which webserver you want to assign this routing to.", goalStep, function,
					FixSuggestion: $"rewrite the step to include the server name e.g. `- {goalStep.Text}, on {listeners[0].WebserverName} webserver");
		}
		else if (listeners.Count == 0)
		{
			return new ProgramError($"There are 0 servers, please define a webserver.", goalStep, function,
					FixSuggestion: $"create a step before adding a route e.g. `- start webserver");
		}

		if (webserverInfo == null) webserverInfo = listeners[0];

		if (method == null || method.Length == 0) method = ["GET"];

		if (webserverInfo.Routings == null) webserverInfo.Routings = new();
		webserverInfo.Routings.Add(new Routing(path, goalToCall, method, ContentType, Parameters, MaxContentLength, DefaultResponseContentEncoding));

		return null;
	}


	public async Task<WebserverInfo> StartWebserver(string webserverName = "default", string scheme = "http", string host = "localhost",
		int port = 8080, long maxContentLengthInBytes = 4096 * 2,
		string defaultResponseContentEncoding = "utf-8",
		bool signedRequestRequired = false, List<Routing>? routings = null)
	{
		if (listeners.FirstOrDefault(p => p.WebserverName == webserverName) != null)
		{
			throw new RuntimeException($"Webserver '{webserverName}' already exists. Give it a different name");
		}

		if (listeners.FirstOrDefault(p => p.Port == port) != null)
		{
			throw new RuntimeException($"Port {port} is already in use. Select different port to run on, e.g.\n-Start webserver, port 4687");
		}

		var assembly = Assembly.GetAssembly(this.GetType());
		string version = assembly!.GetName().Version!.ToString();

		//var builder = WebApplication.CreateBuilder();
		var webserverInfo = new WebserverInfo(webserverName, scheme, host, port, maxContentLengthInBytes, defaultResponseContentEncoding, signedRequestRequired, routings);

		var builder = Host.CreateDefaultBuilder()
			.ConfigureLogging(l => l.ClearProviders())
			.ConfigureWebHostDefaults(web =>
			{
				web.UseKestrel(k =>
				{
					if (IPAddress.TryParse(host, out var ip))
					{
						k.Listen(ip, port, l => l.Protocols = HttpProtocols.Http1AndHttp2);
					}
					else if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
					{
						k.ListenLocalhost(port, l => l.Protocols = HttpProtocols.Http1AndHttp2);
					}
					else
					{
						throw new ArgumentException($"Host '{host}' is not a valid IP or 'localhost'.");
					}

					k.AddServerHeader = false;
				});
				web.ConfigureServices(s =>
				{
					s.AddSingleton(webserverInfo);
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
					app.UseResponseCompression();
					app.Run(async ctx => await HandleRequestAsync(ctx, webserverInfo));
				});
			});

		var app = builder.Build();

		webserverInfo.Listener = app;

		_ = Task.Run(async () =>
		{
			app.Run();//.Run($"http://{host}:{port}");

		});

		listeners.Add(webserverInfo);

		logger.LogDebug($"Listening on {host}:{port}...");
		Console.WriteLine($" - Listening on {host}:{port}...");

		//_ = ListenForContext(listener, webserverInfo);

		KeepAlive(app, "Webserver");

		return webserverInfo;
	}

		var error = await HandleRequest(httpContext, outputStream, webserverInfo);


	private async Task HandleRequestAsync(HttpContext httpContext, WebserverInfo webserverInfo)
	{

		object obj = new();
		httpContext.Response.OnCompleted((a) =>
		{
			int i = 0;
			return Task.CompletedTask;
		}, obj);

		var outputStream = GetOutputStream(httpContext);

		if (webserverInfo.SignedRequestRequired && !httpContext.Request.Headers.TryGetValue("X-Signature", out var value))
		{
			await ShowError(httpContext.Response, outputStream, new Error("All requests must be signed"));
			return;
		}

		var error = await HandleRequest(httpContext, outputStream, webserverInfo);

		if (error != null && error is not ErrorHandled)
		{
			await ShowError(httpContext.Response, outputStream, error);
		}
		//await httpContext.Response.CompleteAsync();


	}

	private IOutputStream GetOutputStream(HttpContext httpContext)
	{
		var contentType = GetContentType(httpContext.Request);
		return new HttpOutputStream(httpContext.Response, fileSystem, contentType, null, httpContext.Request.Path);
	}

	private string GetContentType(HttpRequest request)
	{


		string? contentType = request.Headers.Accept.FirstOrDefault();
		if (contentType?.StartsWith("application/plang") == true) return contentType;

		var ext = fileSystem.Path.GetExtension(request.Path);
		if (ext != null)
		{
			contentType = GetMimeType(ext);
			if (contentType != null) return contentType;
		}

		return "text/html";
	}

	private async Task<IError?> HandleRequest(HttpContext httpContext, IOutputStream outputStream, WebserverInfo webserverInfo)
	{
		try
		{
			var acceptedTypes = httpContext.Request.Headers.Accept.FirstOrDefault();

			var isPlangRequest = acceptedTypes?.StartsWith("application/plang") ?? false;
			if (isPlangRequest)
			{
				return await ProcessPlangRequest(httpContext, webserverInfo, webserverInfo.Routings, outputStream);
			}


			(var goalPath, var routing) = GetGoalPath(webserverInfo.Routings, httpContext.Request);
			if (string.IsNullOrEmpty(goalPath))
			{
				return await ProcessGeneralRequest(httpContext, outputStream);
			}

			if (routing == null)
			{
				return new NotFoundError("Routing not found");
			}

			(var signedMessage, var error) = await VerifySignature(httpContext);
			// Unsigned requests are allowed, so we let 401 through
			if (error?.StatusCode != 401) return error;

			return await ProcessGoal(goalPath, webserverInfo, routing, httpContext, outputStream);
		}
		catch (Exception ex)
		{
			return new Error(ex.Message, Key: "WebserverCore", 500, ex);
		}
	}

	private async Task<IError?> ProcessGoal(string goalPath, WebserverInfo webserverInfo, Routing routing, HttpContext httpContext, IOutputStream outputStream)
	{
		Goal? goal = prParser.GetGoal(fileSystem.Path.Join(goalPath, ISettings.GoalFileName));
		if (goal == null)
		{
			return new NotFoundError($"Goal could not be loaded");
		}

		var resp = httpContext.Response;
		var request = httpContext.Request;

		if (request.QueryString.Value == "__signature__")
		{
			resp.Headers.Add("X-Goal-Hash", goal.Hash);
			resp.Headers.Add("X-Goal-Signature", goal.Signature);
			resp.StatusCode = 200;
			return null;
		}

		long maxContentLength = routing.MaxContentLength ?? webserverInfo.MaxContentLengthInBytes;
		if (request.ContentLength > maxContentLength)
		{
			return new Error($"Content sent to server is to big. Max {maxContentLength} bytes", StatusCode: 413);
		}

		if (!IsValidMethod(routing, request, goal))
		{
			return new Error($"Http method '{request.Method}' is not allowed on goal {goal.GoalName}", Key: "Method Not Allowed", StatusCode: 405);
		}

		string strEncoding = routing.DefaultResponseContentEncoding ?? webserverInfo.DefaultResponseContentEncoding;
		var encoding = Encoding.GetEncoding(strEncoding);

		resp.ContentType = $"{routing.ContentType}; charset={encoding.BodyName}";
		resp.Headers["Content-Type"] = $"{routing.ContentType}; charset={encoding.BodyName}";

		resp.Headers.Add("X-Goal-Hash", goal.Hash);
		resp.Headers.Add("X-Goal-Signature", goal.Signature);

		memoryStack.Put("!request", GetRequest(httpContext.Request));

		(var parameters, var error) = await ParseRequest(httpContext);
		if (error != null) return error;

		(var callbackInfos, error) = await GetCallbackInfos(request);
		if (error != null) return error;

		var pool = this.engine.GetEnginePool(goal.AbsoluteAppStartupFolderPath);
		var engine = await pool.RentAsync(this.engine, goalStep, goalPath, outputStream);
		engine.HttpContext = httpContext;

		(var vars, error) = await engine.RunGoal(goal, 0, callbackInfos);
		if (error != null && error is not IErrorHandled)
		{
			pool.Return(engine);
			return error;
		}
		pool.Return(engine);
		return null;
	}

	private async Task<(List<CallbackInfo>? CallbackInfo, IError? Error)> GetCallbackInfos(HttpRequest request)
	{
		if (!request.Headers.TryGetValue("!callback", out var value)) return (null, null);

		var identity = programFactory.GetProgram<Modules.IdentityModule.Program>(goalStep);
		var callbackResult = await CallbackHelper.GetCallbackInfos(identity, value);
		if (callbackResult.Error != null) return (null, callbackResult.Error);

		var callbackInfos = callbackResult.CallbackInfos;
		/*
		var keys = request.Headers.AllKeys.Where(p => p.StartsWith("!"));
		foreach (var key in keys)
		{
			if (key != null && !context.ContainsKey(key))
			{
				context.AddOrReplace(key, request.Headers[key]);
			}
		}*/


		return (callbackInfos, null);
	}

	private async Task ShowError(Microsoft.AspNetCore.Http.HttpResponse resp, IOutputStream outputStream, IError error)
	{
		try
		{
			resp.StatusCode = error.StatusCode;

			await outputStream.Write(error);

		}
		catch (Exception ex)
		{
			Console.WriteLine("Error:" + error.ToString());
			Console.WriteLine("Exception when writing out error:" + ex);
		}
	}

	private async Task<IError?> ProcessPlangRequest(HttpContext httpContext, WebserverInfo webserverInfo, List<Routing>? routings, IOutputStream outputStream)
	{
		(var signature, var error) = await VerifySignature(httpContext);
		if (error != null) return error;

		string? query = httpContext.Request.QueryString.Value;
		if (query == "?plang.poll=1")
		{
			await HandlePlangPoll(httpContext, signature.Identity);
			return null;
		}

		(var goalPath, var routing) = GetGoalPath(routings, httpContext.Request);

		if (routing == null) return new NotFoundError("Routing not found");
		if (goalPath == null) return new NotFoundError("Goal not found");


		return await ProcessGoal(goalPath, webserverInfo, routing, httpContext, outputStream);
	}

	private async Task<(SignedMessage? SignedMessage, IError? Error)> VerifySignature(HttpContext httpContext)
	{
		var signatureData = httpContext.Request.Headers["X-Signature"].ToString();
		if (string.IsNullOrEmpty(signatureData)) return (null, new UnauthorizedError("X-Signature is empty. Use plang app or compatible to continue."));

		var request = httpContext.Request;
		var headers = new Dictionary<string, object?>();
		foreach (var header in httpContext.Request.Headers)
		{
			headers[header.Key] = header.Value;
		}

		string body = "";
		using (var reader = new StreamReader(request.Body))
		{
			body = await reader.ReadToEndAsync();
		}



		var signing = GetProgramModule<IdentityModule.Program>();
		var verifiedSignatureResult = await signing.VerifySignature(signatureData, headers, body);
		if (verifiedSignatureResult.Signature != null)
		{
			memoryStack.Put(ReservedKeywords.Identity, verifiedSignatureResult.Signature.Identity);

			LiveConnection? liveResponse = null;
			if (verifiedSignatureResult.Signature != null && verifiedSignatureResult.Signature.Identity != null)
			{
				liveConnections.TryGetValue(verifiedSignatureResult.Signature.Identity, out liveResponse);
			}
		}
		return verifiedSignatureResult;
	}


	private Dictionary<string, object?>? GetRequest(HttpRequest request)
	{
		Dictionary<string, object?> dict = new();
		dict.Add("Method", request.Method);
		dict.Add("ContentLength", request.ContentLength);
		dict.Add("QueryString", request.QueryString);
		dict.Add("ContentType", request.ContentType);
		dict.Add("HasFormContentType", request.HasFormContentType);

		dict.Add("Headers", request.Headers);
		dict.Add("KeepAlive", request.Headers.KeepAlive);

		foreach (var item in request.Headers)
		{
			dict.Add(item.Key, item.Value);
		}

		request.Headers.TryGetValue("X-Requested-With", out var ajax);
		dict.Add("IsAjax", ajax.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase));
		dict.Add("UserAgentMode", UserAgentHelper.GetUserAgentMode(request.Headers.UserAgent));
		return dict;
	}

	private async Task HandlePlangPoll(HttpContext httpContext, string Identity)
	{
		var response = httpContext.Response;
		response.ContentType = "application/plang+json; charset=utf-8";
		response.Headers.Add("Cache-Control", "no-cache");

		liveConnections.AddOrReplace(Identity, new LiveConnection(response, true));

		var payload = JsonConvert.SerializeObject("ping");
		var buffer = Encoding.UTF8.GetBytes(payload + Environment.NewLine);

		try
		{
			while (true)
			{
				await response.Body.WriteAsync(buffer, 0, buffer.Length);
				await response.Body.FlushAsync();
				await Task.Delay(TimeSpan.FromSeconds(20));
			}
		}
		catch (Exception ex)
		{
			// client disconnected or other I/O error
			liveConnections.TryRemove(Identity, out var liveConnection);
			if (liveConnection?.OnDisconnect != null)
			{
				var caller = GetProgramModule<CallGoalModule.Program>();
				await caller.RunGoal(liveConnection.OnDisconnect);
			}
			Console.WriteLine($"Long-poll ended: {ex.Message}");
		}
	}

	private bool IsValidMethod(Routing routing, HttpRequest request, Goal goal)
	{
		if (routing.Method == null) return false;

		foreach (var method in routing.Method)
		{

			if (routing.Method != null && method.Equals(request.Method, StringComparison.OrdinalIgnoreCase)) return true;

			if (goal.GoalInfo?.GoalApiInfo != null && request.Method.Equals(goal.GoalInfo.GoalApiInfo?.Method, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;

	}

	private async Task ShowError(ServiceContainer container, IError error)
	{
		if (error is IUserDefinedError)
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

	private async Task<IError?> ProcessGeneralRequest(HttpContext httpContext, IOutputStream outputStream)
	{
		var requestedFile = httpContext.Request.Path.Value;
		if (string.IsNullOrEmpty(requestedFile)) return new NotFoundError("Path is empty");

		requestedFile = requestedFile.AdjustPathToOs();

		var filePath = fileSystem.Path.Join(fileSystem.GoalsPath!, requestedFile);
		var fileExtension = fileSystem.Path.GetExtension(filePath);
		var mimeType = GetMimeType(fileExtension);
		if (mimeType == null)
		{
			return new Error($"MimeType for {fileExtension} is not supported");
		}

		if (!fileSystem.File.Exists(filePath))
		{
			return new NotFoundError($"{requestedFile} was not found");
		}

		await using var stream = fileSystem.File.OpenRead(filePath);
		httpContext.Response.ContentLength = stream.Length;
		httpContext.Response.ContentType = mimeType;
		await stream.CopyToAsync(outputStream.Stream);


		return null;
	}
	private string? GetMimeType(string extension)
	{
		switch (extension)
		{
			case ".txt": return "text/plain";
			case ".html": return "text/html";
			case ".css": return "text/css";

			case ".jpg": case ".jpeg": return "image/jpeg";
			case ".png": return "image/png";
			case ".gif": return "image/gif";
			case ".svg": return "image/svg+xml";
			case ".webp": return "image/webp";

			case ".woff": return "font/woff";
			case ".woff2": return "font/woff2";
			case ".ttf": return "application/font-ttf";

			case ".js": return "application/javascript";
			case ".json": return "application/json";
			case ".xml": return "application/xml";
			case ".csv": return "application/csv";

			case ".mp4": return "video/mp4";
			case ".webm": return "video/webm";

			case ".pdf": return "application/pdf";


			default: return null;
		}
	}

	public async Task Redirect(string url)
	{
		if (HttpContext == null)
		{
			throw new HttpListenerException(500, "Context is null. Start a webserver before calling me.");
		}

		HttpContext.Response.Redirect(url);
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
			HttpContext.Response.Headers.Add(header.Key, header.Value.ToString());
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
		if (HttpContext == null) return new ProgramError("HttpContext is empty. Is this being called during a web request?",
			FixSuggestion: missingHttpContextFixSuggestion);

		CookieOptions options = new();
		options.Expires = DateTime.Now.AddSeconds(expiresInSeconds);
		options.HttpOnly = true;
		options.Secure = true;

		HttpContext.Response.Cookies.Append(name, value, options);
		return null;
	}


	public async Task<IError?> DeleteCookie(string name)
	{


		HttpContext.Response.Cookies.Delete(name);
		return null;
	}

	public async Task<IError?> SendFileToClient(string path, string? fileName = null)
	{
		var mimeType = GetMimeType(path);
		if (mimeType == null) return new ProgramError($"mime type for {path} is not supported");


		var response = HttpContext.Response;
		if (!fileSystem.File.Exists(path))
		{
			return new NotFoundError("File not found");
		}

		response.ContentType = mimeType;

		var fileInfo = fileSystem.FileInfo.New(path);
		response.ContentLength = fileInfo.Length;
		if (string.IsNullOrEmpty(fileName)) fileName = fileInfo.Name;

		response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");

		using (var fs = fileSystem.File.OpenRead(path))
		{
			fs.CopyTo(response.Body);
		}

		response.StatusCode = (int)HttpStatusCode.OK;

		return null;
	}


	private (string?, Routing?) GetGoalPath(List<Routing>? routings, HttpRequest request)
	{
		if (request == null || request.Path == null || routings == null) return (null, null);
		foreach (var route in routings)
		{
			if (Regex.IsMatch(request.Path, "^" + route.Path + "$", RegexOptions.IgnoreCase))
			{
				return (GetGoalBuildDirPath(route, request), route);
			}

		}

		return ("", null);
	}

	private string GetGoalBuildDirPath(Routing routing, HttpRequest request)
	{
		if (!string.IsNullOrEmpty(routing.GoalToCall))
		{
			var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RelativeAppPath, routing.GoalToCall);
			if (goal != null)
			{
				return goal.AbsolutePrFolderPath;
			}
		}
		if (request == null || request.Path == null) return "";

		var goalName = request.Path.Value?.AdjustPathToOs();
		if (goalName == null) return "";

		if (goalName.StartsWith(fileSystem.Path.DirectorySeparatorChar))
		{
			goalName = goalName.Substring(1);
		}
		goalName = goalName.RemoveExtension();
		string goalBuildDirPath = fileSystem.Path.Join(fileSystem.BuildPath, goalName).AdjustPathToOs();
		if (fileSystem.Directory.Exists(goalBuildDirPath)) return goalBuildDirPath;

		logger.LogDebug($"Path doesnt exists - goalBuildDirPath:{goalBuildDirPath}");
		return "";

	}

	static async Task<(Dictionary<string, object?>? Params, IError? Error)> ParseRequest(HttpContext? ctx)
	{
		if (ctx is null) return (null, new Error("context is empty"));

		var req = ctx.Request;
		var query = req.Query.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());

		// ---------- JSON --------------------------------------------------------
		if (req.HasJsonContentType())
		{
			req.EnableBuffering();
			var body = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(req.Body)
					   ?? new();
			foreach (var (k, v) in query) body[k] = v;
			return (new() { ["request"] = body }, null);
		}

		// ---------- Form / Multipart (fields + files) ---------------------------
		if (req.HasFormContentType)
		{
			var form = await req.ReadFormAsync();
			var fields = form.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());

			var payload = new Dictionary<string, object?>(fields, StringComparer.OrdinalIgnoreCase)
			{
				["_files"] = form.Files
			};
			foreach (var (k, v) in query) payload.TryAdd(k, v);
			return (new() { ["request"] = payload }, null);
		}

		// ---------- Fallback: query only ---------------------------------------
		return (new() { ["request"] = query }, null);
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


