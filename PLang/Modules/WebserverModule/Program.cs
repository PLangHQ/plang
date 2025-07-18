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
using Org.BouncyCastle.Ocsp;
using PLang.Attributes;
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
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using UAParser;
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
		: IDisposable
	{
		public IHost Listener { get; set; }
		public List<Routing>? Routings { get; set; } = Routings;

		public void Dispose()
		{
			Console.WriteLine($"Shutting down {Scheme}://{Host}:{Port}");
			this.Listener.StopAsync();
		}
	}
	public record ParamInfo(string Name, string VariableOrValue, string Type, string? RegexValidation = null, string? ErrorMessage = null, object? DefaultValue = null);
	public record GoalToCallWithParamInfo(string Name, List<ParamInfo> Parameters);
	public record Routing(string Path, Route? Route = null, string[]? Method = null, string ContentType = "text/html",
								long? MaxContentLength = null, string? DefaultResponseContentEncoding = null);

	[Description(@"When path is /api, ContentType=application/json unless defined by user. When user defines a variable in path, it should be defined in GoalToCallInfo, /product/%id% => Parameters: id=%id%")]
	public async Task<IError?> AddRoute([HandlesVariable] string path, List<ParamInfo> PathParameters, GoalToCallInfo? goalToCall = null,
		[Description("Default is GET")]
		string[]? method = null, string ContentType = "text/html",
								long? MaxContentLength = 8 * 1024,
								[Description("Default is utf-8")]
								string? DefaultResponseContentEncoding = null,
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

		var route = BuildRoute(path, PathParameters, goalToCall);

		webserverInfo.Routings.Add(new Routing(path, route, method, ContentType, MaxContentLength, DefaultResponseContentEncoding));

		return null;
	}

	private (bool, List<ObjectValue>?, IError?) TryMatch(Routing routing, HttpRequest request)
	{

		var path = request.Path.Value;
		if (path == null) return (false, null, null);

		var route = routing.Route;
		if (route == null) return (false, null, null);

		var m = route.PathRegex.Match(path);
		if (!m.Success) return (false, null, null);

		var method = routing.Method.FirstOrDefault(p => p.Equals(request.Method, StringComparison.OrdinalIgnoreCase));
		if (method == null) return (false, null, new ProgramError($"{request.Method} is not supported for {request.Path}", goalStep));

		var dict = m.Groups.Keys
						 .Where(k => k != "0")
						 .ToDictionary(k => k, k => m.Groups[k].Value,
									   StringComparer.OrdinalIgnoreCase);

		// query placeholders
		if (route.QueryMap is { } qmap)
		{
			foreach (var (queryKey, varName) in qmap)
			{
				var value = request.Query[queryKey].ToString();
				if (string.IsNullOrEmpty(value)) return (false, null, new ProgramError($"Missing {queryKey} from url")); // required query missing
				dict[varName] = value;
			}
		}

		List<ObjectValue> variables = new();
		foreach (var item in dict)
		{
			variables.Add(new ObjectValue(item.Key, item.Value));
		}

		return (true, variables, null);
	}
	public sealed record Route(Regex PathRegex,
							Dictionary<string, string>? QueryMap,  // q -> %q%
							GoalToCallInfo Goal);

	private Route BuildRoute(string pattern, List<ParamInfo> paramInfos, GoalToCallInfo goal)
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
				regex.Append($"(?<{name}>[^/?&-]+)");   // stop at / ? & or - (hyphen safe)
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

		return new Route(new Regex(regex.ToString(),
								   RegexOptions.Compiled | RegexOptions.IgnoreCase),
						 queryMap.Count == 0 ? null : queryMap,
						 goal);
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

		await app.StartAsync();

		listeners.Add(webserverInfo);

		logger.LogDebug($"Listening on {host}:{port}...");
		Console.WriteLine($" - Listening on {host}:{port}...");

		KeepAlive(listeners, "Webserver");

		return webserverInfo;
	}



	private async Task HandleRequestAsync(HttpContext httpContext, WebserverInfo webserverInfo)
	{
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

	private async Task<IError?> HandleRequest(HttpContext httpContext, IOutputStream outputStream, WebserverInfo webserverInfo)
	{
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			IError? error = null;

			var acceptedTypes = httpContext.Request.Headers.Accept.FirstOrDefault();

			var isPlangRequest = acceptedTypes?.StartsWith("application/plang") ?? false;
			if (isPlangRequest)
			{
				logger.LogInformation($" ---------- Request Starts ---------- - {stopwatch.ElapsedMilliseconds}");
				error = await ProcessPlangRequest(httpContext, webserverInfo, webserverInfo.Routings, outputStream);
				logger.LogInformation($" ---------- Request Done ---------- - {stopwatch.ElapsedMilliseconds}");
				return error;
			}

			(var goal, var routing, var slugVariables, error) = GetGoalByRouting(webserverInfo.Routings, httpContext.Request);
			if (error != null) return error;

			if (goal == null)
			{
				return await ProcessGeneralRequest(httpContext, outputStream);
			}

			if (routing == null)
			{
				return new NotFoundError("Routing not found");
			}

			logger.LogInformation($" ---------- Request Starts ---------- - {stopwatch.ElapsedMilliseconds}");
			(var signedMessage, error) = await VerifySignature(httpContext, outputStream);
			// Unsigned requests are allowed, so we let 401 through
			if (error?.StatusCode != 401) return error;


			error = await ProcessGoal(goal, slugVariables, webserverInfo, routing, httpContext, outputStream);

			logger.LogInformation($" ---------- Request Done ---------- - {stopwatch.ElapsedMilliseconds}");

			return error;
		}
		catch (Exception ex)
		{
			return new Error(ex.Message, Key: "WebserverCore", 500, ex);
		}
	}

	private async Task<IError?> ProcessGoal(Goal goal, List<ObjectValue>? slugVariables, WebserverInfo webserverInfo, Routing routing, HttpContext httpContext, IOutputStream outputStream)
	{
		if (goal == null)
		{
			return new NotFoundError($"Goal could not be loaded");
		}
		Stopwatch stopwatch = Stopwatch.StartNew();

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

		string strEncoding = routing.DefaultResponseContentEncoding ?? webserverInfo.DefaultResponseContentEncoding;
		var encoding = Encoding.GetEncoding(strEncoding);

		resp.ContentType = $"{routing.ContentType}; charset={encoding.BodyName}";
		resp.Headers["Content-Type"] = $"{routing.ContentType}; charset={encoding.BodyName}";

		resp.Headers.Add("X-Goal-Hash", goal.Hash);
		resp.Headers.Add("X-Goal-Signature", goal.Signature);
		
		logger.LogDebug($"  - Starting parsing request - {stopwatch.ElapsedMilliseconds}");

		(var requestObjectValue, var error) = await ParseRequest(httpContext, outputStream);
		if (error != null) return error;
		logger.LogDebug($"  - Done parsing request, doing callback info - {stopwatch.ElapsedMilliseconds}");
		(var callbackInfos, error) = await GetCallbackInfos(request);
		if (error != null) return error;
		logger.LogDebug($"  - Done callback info, getting engine - {stopwatch.ElapsedMilliseconds}");
		var pool = this.engine.GetEnginePool(goal.AbsoluteAppStartupFolderPath);
		var engine = await pool.RentAsync(this.engine, goalStep, goal.AbsoluteGoalFolderPath, outputStream);

		engine.HttpContext = httpContext;
		engine.GetMemoryStack().Put(requestObjectValue, goalStep);
		engine.CallbackInfos = callbackInfos;
		if (slugVariables != null)
		{
			foreach (var item in slugVariables)
			{
				engine.GetMemoryStack().Put(item, goalStep, disableEvent: true);
			}
		}
		logger.LogDebug($"  - Run goal - {stopwatch.ElapsedMilliseconds}");

		(var vars, error) = await engine.RunGoal(goal, 0);
		if (error != null && error is not IErrorHandled)
		{
			(var returnVars, error) = await eventRuntime.AppErrorEvents(error);
			
			pool.Return(engine);
			return error;
		}
		pool.Return(engine);

		logger.LogDebug($"  - Return engine - {stopwatch.ElapsedMilliseconds}");

		return null;
	}

	private async Task<(List<CallbackInfo>? CallbackInfo, IError? Error)> GetCallbackInfos(HttpRequest request)
	{
		string? callbackValue = "";
		if (request.HasFormContentType)
		{
			callbackValue = request.Form["callback"];
		}
		else
		{
			if (!request.Headers.TryGetValue("callback", out var value)) return (null, null);
			callbackValue = value.ToString();
		}

		if (string.IsNullOrEmpty(callbackValue)) return (null, null);
		

		var identity = programFactory.GetProgram<Modules.IdentityModule.Program>(goalStep);
		var callbackResult = await CallbackHelper.GetCallbackInfos(identity, callbackValue);
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
			if (!resp.HasStarted)
			{
				resp.StatusCode = error.StatusCode;
			}

			await outputStream.Write(error, statusCode: error.StatusCode);

		}
		catch (Exception ex)
		{
			Console.WriteLine("Error:" + error.ToString());
			Console.WriteLine("Exception when writing out error:" + ex);
		}
	}

	private async Task<IError?> ProcessPlangRequest(HttpContext httpContext, WebserverInfo webserverInfo, List<Routing>? routings, IOutputStream outputStream)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();

		logger.LogDebug($" - Verify signature - {stopwatch.ElapsedMilliseconds}");

		(var signature, var error) = await VerifySignature(httpContext, outputStream);
		if (error != null) return error;

		httpContext.Response.ContentType = "application/plang+json; charset=utf-8";

		string? query = httpContext.Request.QueryString.Value;
		if (query == "?plang.poll=1")
		{
			await HandlePlangPoll(httpContext, signature.Identity);
			return null;
		}
		logger.LogDebug($" - get routing - {stopwatch.ElapsedMilliseconds}");
		(var goal, var routing, var slugVariables, error) = GetGoalByRouting(routings, httpContext.Request);
		if (error != null) return error;
		if (routing == null) return new NotFoundError("Routing not found");
		if (goal == null) return new NotFoundError("Goal not found");

		routing = routing with { ContentType = "application/plang+json" };

		logger.LogDebug($" - ProcessGoal starts - {stopwatch.ElapsedMilliseconds}");
		error = await ProcessGoal(goal, slugVariables, webserverInfo, routing, httpContext, outputStream);
		logger.LogDebug($" - ProcessGoal done - {stopwatch.ElapsedMilliseconds}");
		return error;
	}

	private async Task<(SignedMessage? SignedMessage, IError? Error)> VerifySignature(HttpContext httpContext, IOutputStream outputStream)
	{
		var signatureData = httpContext.Request.Headers["X-Signature"].ToString();
		if (string.IsNullOrEmpty(signatureData))
		{
			memoryStack.Remove(ReservedKeywords.Identity);
			return (null, new UnauthorizedError("X-Signature is empty. Use plang app or compatible to continue."));
		}

		var request = httpContext.Request;
		var headers = new Dictionary<string, object?>();
		foreach (var header in httpContext.Request.Headers)
		{
			headers[header.Key] = header.Value;
		}

		byte[] rawBody = await GetRawBody(request);


		var signing = GetProgramModule<IdentityModule.Program>();
		var verifiedSignatureResult = await signing.VerifySignature(signatureData, headers, rawBody);
		if (verifiedSignatureResult.Signature != null)
		{
			memoryStack.Put(ReservedKeywords.Identity, verifiedSignatureResult.Signature.Identity);
			memoryStack.Put(ReservedKeywords.Signature, verifiedSignatureResult.Signature);
			LiveConnection? liveResponse = null;
			if (verifiedSignatureResult.Signature != null && verifiedSignatureResult.Signature.Identity != null)
			{
				liveConnections.TryGetValue(verifiedSignatureResult.Signature.Identity, out liveResponse);
				if (liveResponse != null && outputStream is HttpOutputStream httpOutputStream)
				{
					httpOutputStream.SetLiveResponse(liveResponse);
				}
			}
			
		}
		else
		{
			memoryStack.Remove(ReservedKeywords.Identity);
		}
		return verifiedSignatureResult;
	}

	private async Task<byte[]> GetRawBody(HttpRequest request)
	{
		request.EnableBuffering();

		using var ms = new MemoryStream();
		await request.Body.CopyToAsync(ms);
		request.Body.Position = 0;       
		return ms.ToArray();
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
			case ".ico": return "image/x-icon";

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

	public async Task<IError?> Redirect(string url, bool permanent = false, bool preserveMethod = false)
	{
		if (HttpContext == null)
		{
			return new ProgramError("Header has been sent to browser. Redirect cannot be sent after that.", StatusCode: 500);
		}
		if (HttpContext.Response.HasStarted) return new ProgramError("Header has been sent to browser. Redirect cannot be sent after that.", StatusCode: 500);

		HttpContext.Response.Redirect(url, permanent, preserveMethod);
		await HttpContext.Response.Body.FlushAsync();

		return null;
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


	private (Goal?, Routing?, List<ObjectValue>? SlugVariables, IError?) GetGoalByRouting(List<Routing>? routings, HttpRequest request)
	{
		if (request == null || request.Path == null || routings == null) return (null, null, null, new ProgramError("request object empty", goalStep, StatusCode: 500));
		foreach (var routing in routings)
		{
			(var isMatch, var variables, var error) = TryMatch(routing, request);
			if (error != null) return (null, null, null, error);
			if (isMatch)
			{
				(var goal, error) = GetGoalBuildDirPath(routing, request);
				return (goal, routing, variables, error);
			}

		}

		return (null, null, null, null);
	}

	private (Goal?, IError?) GetGoalBuildDirPath(Routing routing, HttpRequest request)
	{
		if (string.IsNullOrEmpty(routing.Route?.Goal?.Name)) return (null, new ProgramError("Goal name in route is empty", goalStep, StatusCode: 500));

		var result = GoalHelper.GetGoal("/", fileSystem.RootDirectory, routing.Route.Goal, prParser.GetGoals(), new());
		if (result.Item1 != null) return (result.Item1, null);

		var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RelativeAppPath, routing.Route.Goal.Name);
		if (goal != null)
		{
			return (goal, null);
		}

		var goalName = request.Path.Value?.AdjustPathToOs();
		if (goalName == null) return (null, new ProgramError("Goal name could not be extracted from request path", goalStep, StatusCode: 500));

		if (goalName.StartsWith(fileSystem.Path.DirectorySeparatorChar))
		{
			goalName = goalName.Substring(1);
		}
		goalName = goalName.RemoveExtension();
		string goalBuildDirPath = fileSystem.Path.Join(fileSystem.BuildPath, goalName).AdjustPathToOs();
		if (fileSystem.Directory.Exists(goalBuildDirPath))
		{
			return (prParser.GetGoal(fileSystem.Path.Join(goalBuildDirPath, ISettings.GoalFileName)), null);
		}

		logger.LogDebug($"Path doesnt exists - goalBuildDirPath:{goalBuildDirPath}");
		return (null, null);

	}

	string[] supportedHeaders = ["data-plang-js", "data-plang-js-params", "data-plang-target-element", "data-plang-action"];

	private void ParseHeaders(HttpContext ctx, IOutputStream outputStream)
	{
		var headers = ctx.Request.Headers;
		

		Dictionary<string, string?> responseProperties = new();
		foreach (var supportedHeader in supportedHeaders)
		{
			if (headers.TryGetValue(supportedHeader, out var value))
			{
				responseProperties.AddOrReplace(supportedHeader, value.ToString());
			}
		}

		if (responseProperties.Count > 0 && outputStream is IResponseProperties rp)
		{	
			rp.ResponseProperties = responseProperties;
		}
	}

	private async Task<(ObjectValue? ObjectValue, IError? Error)> ParseRequest(HttpContext? ctx, IOutputStream outputStream)
	{
		if (ctx is null) return (null, new Error("context is empty"));

		Stopwatch stopwatch = Stopwatch.StartNew();

		var req = ctx.Request;
		var query = req.Query.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());
		ObjectValue objectValue;
		logger.LogDebug($"    - ParseHeader - {stopwatch.ElapsedMilliseconds}");
		ParseHeaders(ctx, outputStream);
		logger.LogDebug($"    - GetRequest - {stopwatch.ElapsedMilliseconds}");
		var properties = GetRequest(ctx);
		logger.LogDebug($"    - Done with GetRequest - {stopwatch.ElapsedMilliseconds}");
		// ---------- JSON --------------------------------------------------------
		if (req.HasJsonContentType())
		{
			logger.LogDebug($"    - JsonHandler starts - {stopwatch.ElapsedMilliseconds}");

			req.EnableBuffering();
			var body = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(req.Body)
					   ?? new();

			var parameters = new Dictionary<string, object?>();
			foreach (var key in body)
			{
				var jsonObj = (JsonElement?)key.Value;
				if (jsonObj == null) continue;

				parameters.Add(key.Key, GetJsonElementValue(jsonObj.Value));
			}

			foreach (var (k, v) in query)
			{
				parameters.Add(k, v);
			}

			objectValue = new ObjectValue("request", parameters, properties: properties);
			
			logger.LogDebug($" - JsonHandler done - {stopwatch.ElapsedMilliseconds}");

			return (objectValue, null);
		}

		// ---------- Form / Multipart (fields + files) ---------------------------
		if (req.HasFormContentType)
		{
			logger.LogDebug($"    - FormHandler starts - {stopwatch.ElapsedMilliseconds}");
			var form = await req.ReadFormAsync();
			var fields = form.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());

			var payload = new Dictionary<string, object?>(fields, StringComparer.OrdinalIgnoreCase);
			if (form.Files.Count > 0)
			{
				payload.Add("_files", form.Files);
			}
			foreach (var (k, v) in query) payload.TryAdd(k, v);

			objectValue = new ObjectValue("request", payload, properties: properties);
			logger.LogDebug($"    - FormHandler done - {stopwatch.ElapsedMilliseconds}");

			return (objectValue, null);
		}

		objectValue = new ObjectValue("request", query, properties: properties);

		logger.LogDebug($"    - Return request object - {stopwatch.ElapsedMilliseconds}");
		return (objectValue, null);
	}

	private object? GetJsonElementValue(JsonElement e) => e.ValueKind switch
	{
		JsonValueKind.String => e.GetString(),
		JsonValueKind.Number => e.TryGetInt64(out var i) ? i : e.GetDouble(),
		JsonValueKind.True
	  or JsonValueKind.False => e.GetBoolean(),
		JsonValueKind.Null => null,
		_ => e.GetRawText()        // array/object ⇒ JSON string
	};

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




	private IOutputStream GetOutputStream(HttpContext httpContext)
	{
		var contentType = GetContentType(httpContext.Request);
		return new HttpOutputStream(httpContext.Response, engine, contentType, 4096, httpContext.Request.Path, null);
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

	private Properties? GetRequest(HttpContext httpContext)
	{
		var request = httpContext.Request;
		Properties properties = new();
		properties.Add(new ObjectValue("Method", request.Method));
		properties.Add(new ObjectValue("ContentLength", request.ContentLength));
		properties.Add(new ObjectValue("QueryString", request.QueryString.ToString()));
		properties.Add(new ObjectValue("ContentType", request.ContentType));
		properties.Add(new ObjectValue("HasFormContentType", request.HasFormContentType));
		properties.Add(new ObjectValue("HasJsonContentType", request.HasJsonContentType()));

		properties.Add(new ObjectValue("Headers", request.Headers));
		properties.Add(new ObjectValue("KeepAlive", request.Headers.KeepAlive.ToString()));

		foreach (var item in request.Headers)
		{
			properties.Add(new ObjectValue(item.Key, item.Value));
		}

		request.Headers.TryGetValue("X-Requested-With", out var ajax);
		properties.Add(new ObjectValue("IsAjax", ajax.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase)));
		properties.Add(new ObjectValue("Ip", httpContext.Connection.RemoteIpAddress?.ToString()));

		if (!string.IsNullOrEmpty(request.Headers.UserAgent))
		{
			var clientInfo = parser.Parse(request.Headers.UserAgent, true);
			properties.Add(new ObjectValue("ClientInfo", clientInfo));
		}
		
		return properties;
	}

	static Parser parser = Parser.GetDefault();
}


