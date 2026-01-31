using AngleSharp.Io;
using CsvHelper;
using LightInject;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Net.Http.Headers;
using NBitcoin.Secp256k1;
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
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace PLang.Modules.WebserverModule;

[Description("Start webserver, add route, set certificate, read/write to Header, Cookie, send file to client")]
public class Program : BaseProgram, IDisposable
{
	private readonly IEventRuntime eventRuntime;
	private readonly IPrParser prParser;
	private readonly IPseudoRuntime pseudoRuntime;
	private readonly IEnginePool enginePool;
	private readonly static List<WebserverProperties> listeners = new();
	private bool disposed;


	public Program(IEventRuntime eventRuntime, IPrParser prParser,
		IPseudoRuntime pseudoRuntime, IEnginePool enginePool) : base()
	{
		this.eventRuntime = eventRuntime;
		this.prParser = prParser;
		this.pseudoRuntime = pseudoRuntime;
		this.enginePool = enginePool;

	}



	public async Task<long> GetNumberOfLiveConnections(int lastUpdatedInSeconds = 0)
	{
		if (listeners.Count == 0) return 0;

		if (lastUpdatedInSeconds <= 0)
		{
			return listeners.Sum(p => p.Engine.LiveConnections.Count);
		}

		int count = 0;
		foreach (var listener in listeners)
		{
			var conns = listener.Engine.LiveConnections.Where(a => a.Value.Updated > DateTime.Now.AddSeconds(-1 * lastUpdatedInSeconds));
			count += conns.Count();
		}

		return count;


	}

	public async Task<(WebserverProperties?, IError?)> ShutdownWebserver(string webserverName)
	{
		var webserverInfo = listeners.FirstOrDefault(p => p.Name == webserverName);
		if (webserverInfo == null)
		{
			var error = await context.SystemSink.SendAsync(new TextMessage($"Webserver named '{webserverName}' does not exist"));
			return (webserverInfo, error);
		}

		await webserverInfo.Listener.StopAsync();

		listeners.Remove(webserverInfo);
		return (webserverInfo, null);
	}

	public async Task<(WebserverProperties?, IError?)> RestartWebserver(string webserverName = "default")
	{
		var (webserverInfo, error) = await ShutdownWebserver(webserverName);
		if (error != null) return (webserverInfo, error);

		if (webserverInfo == null) return (webserverInfo, null);

		return await StartWebserver(webserverInfo);

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
	public record RequestProperties(List<string> Methods, long? MaxContentLengthInBytes = null, bool SignedRequestRequired = false)
	{
	}
	public record ResponseProperties(string ContentType = "text/html", string ResponseEncoding = "utf-8");

	public record WebserverProperties(string Name = "default", string Host = "*", int Port = 8080,
		RequestProperties? DefaultRequestProperties = null, ResponseProperties? DefaultResponseProperties = null,
		GoalToCallInfo? OnStart = null, GoalToCallInfo? OnShutdown = null,
		GoalToCallInfo? OnRequestBegin = null, GoalToCallInfo? OnRequestEnd = null,
		GoalToCallInfo? OnGoalRequestBegin = null, GoalToCallInfo? OnGoalRequestEnd = null,
		GoalToCallInfo? OnPollStart = null, GoalToCallInfo? OnPollRefresh = null, GoalToCallInfo? OnPollEnd = null
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

		/*
		var webserverContainer = new ServiceContainer();
		webserverContainer.RegisterForPLangWebserver(goalStep, this.engine, context);
		
		var webserverEngine = webserverContainer.GetInstance<IEngine>();
		webserverEngine.Init(webserverContainer);
		webserverEngine.Name = "WebserverEngine";
		webserverEngine.UserSink = engine.UserSink;
		webserverEngine.SystemSink = engine.SystemSink;
		*/
		context.CallStack.CurrentFrame.AddVariable(webserverProperties);

		//engine.ChildEngines.Add(webserverEngine);

		RequestHandler requestHandler = new RequestHandler(goalStep, logger, fileSystem, container.GetInstance<Modules.IdentityModule.Program>(), prParser);

		if (webserverProperties.OnStart != null)
		{
			var (_, error) = await engine.RunGoal(webserverProperties.OnStart, goal, context);
			if (error != null) return (webserverProperties, error);
		}

		// Pre-warm the engine pool for faster request handling
		enginePool.PreWarm(engine, count: 0);

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
						o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
						{
							"application/x-ndjson"
						});
					});
					s.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
					s.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
				});

				web.Configure(app =>
				{
					app.Use(async (ctx, next) =>
					{
						//Console.WriteLine("Request accepted:" + ctx.Request.Path.Value);
						await next();
					});
					app.UseForwardedHeaders();
					if (webserverProperties.Certificate != null)
					{
						app.UseHttpsRedirection();
					}
					app.UseResponseCompression();
					app.Run(async httpContext =>
					{
						IEngine? requestEngine = null;
						IError? error = null;
						bool poll = false;
						string? identity = null;
						
					
						Stopwatch stopwatch = Stopwatch.StartNew();
						try
						{
							logger.LogInformation(" ---------- Request Starts ({0}) ---------- {1}", httpContext.Request.Path.Value, stopwatch.ElapsedMilliseconds);


							requestEngine = enginePool.Rent(engine);
							requestEngine.Name = "RequestEngine_" + httpContext.Request.Path.Value;

							// Context is already created by Rent() via PrepareForRequest
							var context = requestEngine.Context;
							context.HttpContext = httpContext;
							context.Items.AddOrReplace("!IsHttp", true);

							AddToCallStack(requestEngine, httpContext.Request.Path.Value ?? "RequestStart");

							var httpOutputSink = new HttpSink(context, webserverProperties, engine.LiveConnections);
							context.UserSink = httpOutputSink;
							context.SystemSink = engine.SystemSink;
							(poll, identity, error) = await requestHandler.HandleRequestAsync(requestEngine, context, webserverProperties);

							if (error != null && error is not IErrorHandled)
							{
								if (!httpContext.Response.HasStarted)
								{
									httpContext.Response.StatusCode = error.StatusCode;

									var encoding = Encoding.GetEncoding(webserverProperties.DefaultResponseProperties.ResponseEncoding);
									httpContext.Response.ContentType = $"{webserverProperties.DefaultResponseProperties.ContentType}; charset={encoding.BodyName}";
								}

								if (error is StatelessCallbackError stc)
								{
									await context.UserSink.SendAsync(new ErrorMessage(error.Message, error.Key, "error", error.StatusCode, Callback: stc.Callback));
									return;
								}
								if (error is UserInputError uie)
								{
									var sink = context.GetSink(uie.ErrorMessage?.Actor ?? "user");
									await sink.SendAsync(uie.ErrorMessage);
									return;
								}

								(_, error) = await requestEngine.GetEventRuntime().AppErrorEvents(error);

								if (error != null)
								{

									try
									{
										string content = (context.ShowErrorDetails) ? error.ToString()! : error.Message;
										var errorMessage = new ErrorMessage(content, error.Key, "error", error.StatusCode);
										await context.UserSink.SendAsync(errorMessage);
									}
									catch (Exception ex)
									{
										//AppError had error, this is a critical thing and should not happen
										//So we write the error to the console as last resort.	

										string strError = error.ToString();
										Console.WriteLine(" ---- Could not write error to output stream - Critical Error  ---- ");
										Console.WriteLine(strError);
										Console.WriteLine(" ---- Critical Error  ---- ");
									}

								}
							}
							else
							{
								if (!httpContext.Response.HasStarted)
								{
									httpContext.Response.StatusCode = 200;
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
									Console.WriteLine(" ---- Critical Error(1)  ---- ");
									Console.WriteLine(error);
									Console.WriteLine(" ---- Critical Error(1)  ---- ");
								}
							}
							catch (Exception ex2)
							{
								try
								{
									Console.WriteLine(" ---- Critical Exception(2)  ----\n\nWill not show first exception, only last one ");
									Console.WriteLine(ex2);
									Console.WriteLine(" ---- Critical Exception(2)  ---- ");
								} catch
								{
									Console.WriteLine(" ---- Exception on that(2)  ---- ");
								}
							}
						}
						finally
						{
							if (requestEngine != null)
							{
								enginePool.Return(requestEngine);
							}

							logger.LogInformation(" ---------- Request Done ({0}) ---------- {1}", httpContext.Request.Path.Value, stopwatch.ElapsedMilliseconds);

						}

						if (poll)
						{
							await DoPoll(httpContext, identity, engine, webserverProperties);
						}
					});

				});
			});

		var app = builder.Build();


		webserverProperties.Listener = app;
		webserverProperties.Engine = engine;

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
			var buffer = Encoding.UTF8.GetBytes(Environment.NewLine + payload + Environment.NewLine);

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
		}
		finally
		{
			try { await response.CompleteAsync(); }
			catch (Exception ex)
			{

				int i = 0;
			}
		}
	}

	public async Task<IError?> SetCertificate(string permFilePath, string? privateKeyFile = null)
	{
		
		var webserver = context.CallStack.CurrentFrame.GetVariable<WebserverProperties>();
		Console.WriteLine($"Have webserver: {webserver}");
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

		Console.WriteLine("Loaded CERT");
		return null;
	}

	public record ParamInfo(string Name, string VariableOrValue, string Type, string? RegexValidation = null, string? ErrorMessage = null, object? DefaultValue = null);
	public record GoalToCallWithParamInfo(string Name, List<ParamInfo> Parameters);
	public record Routing(string Path, Route Route, RequestProperties RequestProperties, ResponseProperties ResponseProperties);
	public record Route(Regex PathRegex, Dictionary<string, string>? QueryMap, GoalToCallInfo Goal, List<ParamInfo> ParamInfos);

	[Description("Add route to webserver. When goalToCall is null, use the path parameter in the response to created instance of goalToCall using the path paramter as GoalToCallInfo.Name")]
	public async Task<IError?> AddRoute([HandlesVariable] string path, List<ParamInfo> pathParameters, GoalToCallInfo goalToCall,
		RequestProperties? requestProperties = null, ResponseProperties? responseProperties = null)
	{
		var webserverInfo = context.CallStack.CurrentFrame.GetVariable<WebserverProperties>();
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
				var name = pathPart[(i + 1)..j].Replace(".", "__dot__");
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
						 goal, paramInfos);
	}

	public record CertInfo(string FileName, string Password);

	public async Task<IError?> SetSelfSignedCertificate()
	{
		var webserver = context.CallStack.CurrentFrame.GetVariable<WebserverProperties>();
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
		var os = context.UserSink;
		if (os is HttpSink hos)
		{
			(var response, var isFlushed, var error) = hos.GetResponse();
			if (response != null)
			{
				if (!string.IsNullOrEmpty(context.Identity))
				{
					var executeMessage = new ExecuteMessage("redirect", url);
					var sink = context.GetSink(executeMessage.Actor);
					error = await sink.SendAsync(executeMessage);
					if (error != null) return error;

					return new EndGoal(true, goal, goalStep, "Redirect", (permanent) ? 301 : 302);

				}
				else if (!isFlushed && !response.HasStarted)
				{
					response.Redirect(url, permanent, preserveMethod);
					await response.Body.FlushAsync();
					await response.CompleteAsync();
					hos.IsComplete = true;

					return new EndGoal(true, goal, goalStep, "Redirect", (permanent) ? 301 : 302);
				}
			}
		}

		var topGoal = goal;
		int counter = 0;
		while (topGoal.ParentGoal != null)
		{
			topGoal = topGoal.ParentGoal;
			if (counter++ > 100)
			{
				Console.WriteLine($"To deep: Webserver.Redirect - goalName: {topGoal.GoalName}");
				break;
			}
		}


		return new EndGoal(true, topGoal, goalStep, "Redirect", StatusCode: 302, Levels: 999);
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
	public async Task<(object?, IError?)> GetCookie(string name)
	{
		(var value, var error) = await GetCookieRaw(name);
		if (error != null) return (value, error);

		if (!JsonHelper.IsJson(value, out object? parsedObj)) return (value, null);

		try
		{
			List<ObjectValue> values = new();
			var obj = JObject.Parse(value);
			foreach (var property in obj.Properties())
			{
				values.Add(new ObjectValue(property.Name, property.Value));
			}
			return (values, null);
		}
		catch
		{
			return (value, null);
		}

		return (value, null);
	}
	public async Task<(string?, IError?)> GetCookieRaw(string name)
	{
		if (HttpContext == null) return (null, new ProgramError("HttpContext is empty. Is this being called during a web request?",
			FixSuggestion: missingHttpContextFixSuggestion));


		HttpContext.Request.Cookies.TryGetValue(name, out var value);

		return (value, null);
	}
	public async Task<IError?> WriteVariablesToCookie(string name, List<ObjectValue> values, int expiresInSeconds = 60 * 60 * 24 * 7)
	{
		var jobj = new JObject();
		foreach (var value in values)
		{
			jobj[value.Name] = JToken.FromObject(value.Value);
		}
		return await WriteCookie(name, jobj.ToString(), expiresInSeconds);
	}
	public async Task<IError?> WriteCookie(string name, string value, int expiresInSeconds = 60 * 60 * 24 * 7)
	{
		CookieOptions options = new();
		options.Expires = DateTime.Now.AddSeconds(expiresInSeconds);
		options.HttpOnly = true;
		options.Secure = true;

		var os = context.UserSink;
		if (os is HttpSink hos)
		{
			(var response, var isFlushed, var error) = hos.GetResponse();
			if (response != null)
			{
				if (!response.HasStarted)
				{
					response.Cookies.Append(name, value, options);
				} else
				{
					var sink = context.GetSink("user");

					await sink.SendAsync(new ExecuteMessage("plang.writeCookie", new { name, value, expiresInSeconds }));
				}
				
			}
		}
		return null;
	}


	public async Task<IError?> DeleteCookie(string name)
	{


		HttpContext.Response.Cookies.Delete(name);
		return null;
	}

	public async Task<IError?> StreamFile(StreamMessage streamMessage, long startByte = 0, long? endByte = null)
	{
		var absolutePath = GetPath(streamMessage.FileName);
		if (!fileSystem.File.Exists(absolutePath))
		{
			return new NotFoundError("File not found");
		}

		var streamId = Guid.NewGuid().ToString();
		const int chunkSize = 64 * 1024; // 64KB chunks

		await using var stream = fileSystem.File.OpenRead(absolutePath);
		var fileLength = stream.Length;

		// Validate and adjust byte ranges
		if (startByte < 0 || startByte >= fileLength)
		{
			return new ProgramError($"Invalid start byte: {startByte}. File length is {fileLength}");
		}

		var actualEndByte = endByte.HasValue ? Math.Min(endByte.Value, fileLength - 1) : fileLength - 1;

		if (actualEndByte < startByte)
		{
			return new ProgramError($"End byte ({actualEndByte}) cannot be less than start byte ({startByte})");
		}

		// Seek to start position
		stream.Seek(startByte, SeekOrigin.Begin);

		var totalBytesToSend = actualEndByte - startByte + 1;
		var bytesSent = 0L;

		var sink = context.GetSink(streamMessage.Actor);
		var response = HttpContext.Response;

		// Send Start message with range info
		var meta = new Dictionary<string, object?>
		{
			["startByte"] = startByte,
			["endByte"] = actualEndByte,
			["totalBytes"] = fileLength,
			["rangeBytes"] = totalBytesToSend
		};

		await sink.SendAsync(new StreamMessage(
			StreamId: streamId,
			Phase: StreamPhase.Start,
			Actions: new[] { "stream" },
			ContentType: streamMessage.ContentType,
			FileName: streamMessage.FileName,
			Channel: streamMessage.Channel,
			Target: streamMessage.Target,
			Meta: meta
		));

		// Send file in chunks
		var buffer = new byte[chunkSize];
		int bytesRead;

		while (bytesSent < totalBytesToSend &&
			   (bytesRead = await stream.ReadAsync(buffer, 0,
				   (int)Math.Min(chunkSize, totalBytesToSend - bytesSent),
				   response.HttpContext.RequestAborted)) > 0)
		{
			await sink.SendAsync(new StreamMessage(
				StreamId: streamId,
				Phase: StreamPhase.Chunk,
				Bytes: new ReadOnlyMemory<byte>(buffer, 0, bytesRead),
				ContentType: streamMessage.ContentType,
				FileName: streamMessage.FileName,
				Channel: streamMessage.Channel
			));

			bytesSent += bytesRead;
		}

		// Send End message
		await sink.SendAsync(new StreamMessage(
			StreamId: streamId,
			Phase: StreamPhase.End,
			ContentType: streamMessage.ContentType,
			FileName: streamMessage.FileName,
			Channel: streamMessage.Channel
		));

		return null;
	}

	[Example("send 'file.pdf' to user", "path=file.pdf")]
	[Example(@"send 'document.docx', name=""custom file.docx""", @"path=document.docx, fileName=""custom file.docx""")]
	public async Task<IError?> SendFileToUser(string path, string? fileName = null, string? contentType = null, string actor = "user", string channel = "default")
	{
		var absolutePath = GetPath(path);

		if (!fileSystem.File.Exists(absolutePath))
		{
			return new NotFoundError("File not found");
		}

		string? mimeType = contentType;
		if (string.IsNullOrEmpty(contentType))
		{
			mimeType = RequestHandler.GetMimeType(path);
		}
		if (mimeType == null) mimeType = "application/octet-stream";

		if (string.IsNullOrEmpty(fileName))
		{
			var fileInfo = fileSystem.FileInfo.New(absolutePath);
			fileName = fileInfo.Name;
		}

		var response = HttpContext.Response;
		if (!response.HasStarted)
		{
			response.StatusCode = StatusCodes.Status200OK;
			response.ContentType = mimeType;

			var cd = new ContentDispositionHeaderValue("attachment");
			cd.SetHttpFileName(fileName);
			response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ContentDisposition] = cd.ToString();

			await using var s = fileSystem.File.OpenRead(absolutePath);
			response.ContentLength = s.Length;
			await s.CopyToAsync(response.Body, response.HttpContext.RequestAborted);
			await response.Body.FlushAsync(response.HttpContext.RequestAborted);


			return null;
		}


		if (string.IsNullOrEmpty(fileName))
		{
			var fileInfo = fileSystem.FileInfo.New(absolutePath);
			fileName = fileInfo.Name;
		}

		var streamId = Guid.NewGuid().ToString();
		const int chunkSize = 64 * 1024; // 64KB chunks

		await using var stream = fileSystem.File.OpenRead(absolutePath);

		var sink = context.GetSink(actor);
		// Send Start message
		await sink.SendAsync(new StreamMessage(
			StreamId: streamId,
			Phase: StreamPhase.Start,
			ContentType: mimeType,
			FileName: fileName,
			Actions: new[] { "download" },
			Channel: channel

		));

		// Send file in chunks
		var buffer = new byte[chunkSize];
		int bytesRead;
		while ((bytesRead = await stream.ReadAsync(buffer, response.HttpContext.RequestAborted)) > 0)
		{
			await sink.SendAsync(new StreamMessage(
				StreamId: streamId,
				Phase: StreamPhase.Chunk,
				Bytes: new ReadOnlyMemory<byte>(buffer, 0, bytesRead),
				ContentType: mimeType,
				FileName: fileName,
				Channel: channel
			));
		}

		// Send End message
		await sink.SendAsync(new StreamMessage(
			StreamId: streamId,
			Phase: StreamPhase.End,
			ContentType: mimeType,
			FileName: fileName,
			Channel: channel
		));

		return null;
	}



	private async Task<IError?> ProcessWebsocketRequest(HttpListenerContext httpContext, IOutputSink outputStream)
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

		var (identityModule, identityError) = Module<IdentityModule.Program>();
		if (identityError != null) throw new Exception($"Failed to get IdentityModule: {identityError.Message}");
		var signature = await identityModule!.Sign(obj);
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
					var (identityMod, idError) = Module<IdentityModule.Program>();
					if (idError != null) continue;
					var verifiedSignature = await identityMod!.VerifySignature(signature);
					// todo: missing verifiedSignature.Error check
					if (verifiedSignature.Signature == null)
					{
						continue;
					}

					websocketData.GoalToCall.Parameters.AddOrReplace(ReservedKeywords.Identity, verifiedSignature.Signature.Identity);

					await pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.RootDirectory, websocketData.GoalToCall);
				}
				messageStream.SetLength(0);
			}
			messageStream.Dispose();
		});

		return webSocketInfo;
	}



	private void AddToCallStack(IEngine engine, string goalName)
	{
		var goal = new Goal() { GoalName = goalName, RelativeGoalFolderPath = "No path" };
		var step = new GoalStep() { Name = "Step", RelativeGoalPath = goal.RelativeGoalPath, Goal = goal };
		goal.GoalSteps.Add(step);
		engine.Context.CallStack.EnterGoal(goal);
		engine.Context.CallStack.SetCurrentStep(goal.GoalSteps[0], 0);
	}

}


