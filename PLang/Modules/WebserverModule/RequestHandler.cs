using NBitcoin.Secp256k1;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using UAParser;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Runtime.Engine;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.WebserverModule
{
	public class RequestHandler
	{
		private readonly Goal goal;
		private readonly GoalStep step;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;
		private readonly IdentityModule.Program identity;
		private readonly PrParser prParser;

		public RequestHandler(GoalStep step, ILogger logger, IPLangFileSystem fileSystem, Modules.IdentityModule.Program identity, PrParser prParser)
		{
			this.step = step;
			this.goal = step.Goal;
			this.logger = logger;
			this.fileSystem = fileSystem;
			this.identity = identity;
			this.prParser = prParser;
		}

		public async Task<(bool, string?, IError?)> HandleRequestAsync(IEngine requestEngine, PLangContext context, WebserverProperties webserverProperties)
		{
			IError? error = null;
			try
			{
				var httpContext = context.HttpContext!;
				var request = httpContext.Request;
				if (!request.Body.CanRead) return (false, null, null);

				if (webserverProperties.DefaultRequestProperties!.SignedRequestRequired && !request.Headers.TryGetValue("X-Signature", out var value))
				{
					return (false, null, new Error("All requests must be signed"));
				}


				(var signedMessage, error) = await VerifySignature(requestEngine, context);
				if (error != null)
				{
					(var requestObjectValue2, error) = await ParseRequest(context);

					// put "request" object into memory
					context.MemoryStack.Put(requestObjectValue2);
					return (false, signedMessage?.Identity, error);
				}


				// this should be below 
				(var requestObjectValue, error) = await ParseRequest(context);
				if (error != null) return (false, signedMessage?.Identity, error);

				// put "request" object into memory
				context.MemoryStack.Put(requestObjectValue);

				if (webserverProperties.OnRequestBegin != null)
				{
					error = await RunOnRequest(requestEngine, webserverProperties.OnRequestBegin, context);
					if (error != null) return (false, signedMessage?.Identity, error);
				}

				if (webserverProperties.OnPollStart != null || webserverProperties.OnPollRefresh != null)
				{
					string? query = request.QueryString.Value;
					if (query?.StartsWith("?plang.poll=1") == true)
					{
						error = await HandlePlangPoll(requestEngine, context, webserverProperties, query);
						if (error != null) return (false, signedMessage?.Identity, error);

						//return true to create long lasting connection
						return (true, signedMessage?.Identity, null);
					}
				}


				error = await HandleRequest(context, requestEngine, webserverProperties);
				if (error != null) return (false, signedMessage?.Identity, error);

				if (webserverProperties.OnRequestEnd != null)
				{
					error = await RunOnRequest(requestEngine, webserverProperties.OnRequestEnd, context);
					if (error != null) return (false, signedMessage?.Identity, error);
				}

				return (false, signedMessage?.Identity, error);

			}
			catch (Exception ex)
			{
				return (false, null, new ExceptionError(ex, ex.Message, goal, step));

			}

		}



		private async Task<IError?> HandleRequest(PLangContext context, IEngine requestEngine, WebserverProperties webserverInfo)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				IError? error = null;

				var httpContext = context.HttpContext!;
				var acceptedTypes = httpContext.Request.Headers.Accept.FirstOrDefault();

				var ip = httpContext.Connection.RemoteIpAddress?.ToString();
				var isPlangRequest = acceptedTypes?.StartsWith("application/plang") ?? false;
				if (isPlangRequest)
				{
					Console.WriteLine($"{DateTime.Now} - plang: {ip} | {httpContext.Request.Path} | {httpContext.Request.Headers.UserAgent}");

					error = await ProcessPlangRequest(context, webserverInfo, webserverInfo.Routings, requestEngine);
					return error;
				}

				(var goal, var routing, var slugVariables, error) = GetGoalByRouting(webserverInfo.Routings, httpContext.Request);
				if (error != null) return error;

				if (goal == null)
				{
					return await ProcessGeneralRequest(httpContext);
				}

				if (routing == null)
				{
					return new NotFoundError($"Routing not found - {httpContext.Request.Path}({httpContext.Request.Method}) - {httpContext.Request.Headers.UserAgent}");
				}


				logger.LogInformation($" ---------- Request Starts ---------- - {stopwatch.ElapsedMilliseconds}");
				Console.WriteLine($"{DateTime.Now} - classic: {ip} | {httpContext.Request.Path} | {httpContext.Request.Headers.UserAgent}");
				error = await ProcessGoal(goal, slugVariables, webserverInfo, routing, context, requestEngine);

				logger.LogInformation($" ---------- Request Done ---------- - {stopwatch.ElapsedMilliseconds}");

				return error;
			}
			catch (Exception ex)
			{
				return new Error(ex.Message, Key: "WebserverCore", 500, ex);
			}
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




		private async Task<IError?> RunOnRequest(IEngine engine, GoalToCallInfo goalToCall, PLangContext context)
		{

			(_, var error) = await engine!.RunGoal(goalToCall, goal, context);
			if (error is IErrorHandled) error = null;
			return error;

		}

		private async Task<IError?> ProcessGoal(Goal goal, List<ObjectValue>? slugVariables, WebserverProperties webserverInfo,
			Routing routing, PLangContext context, IEngine requestEngine)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			var httpContext = context.HttpContext!;
			var resp = httpContext.Response;
			var request = httpContext.Request;

			IError? error;
			if (webserverInfo.OnGoalRequestBegin != null)
			{
				error = await RunOnRequest(requestEngine, webserverInfo.OnGoalRequestBegin, context);
				if (error != null) return error;
			}

			if (!resp.HasStarted && request.QueryString.Value == "__signature__")
			{
				resp.Headers.Add("X-Goal-Hash", goal.Hash);
				resp.Headers.Add("X-Goal-Signature", goal.Signature);
				resp.StatusCode = 200;
				return null;
			}

			long maxContentLength = routing.RequestProperties.MaxContentLengthInBytes ?? webserverInfo.DefaultRequestProperties?.MaxContentLengthInBytes ?? 1024 * 16;
			if (maxContentLength == 0) maxContentLength = webserverInfo.DefaultRequestProperties?.MaxContentLengthInBytes ?? 1024 * 16;
			if (request.ContentLength > maxContentLength)
			{
				return new Error($"Content sent to server is to big. Max {maxContentLength} bytes", StatusCode: 413);
			}


			if (!resp.HasStarted)
			{
				string strEncoding = routing.ResponseProperties.ResponseEncoding;
				var encoding = Encoding.GetEncoding(strEncoding);

				resp.Headers["Content-Type"] = $"{routing.ResponseProperties.ContentType}; charset={encoding.BodyName}";

				resp.Headers.Add("X-Goal-Hash", goal.Hash);
				resp.Headers.Add("X-Goal-Signature", goal.Signature);
			}
			logger.LogTrace($"  - Starting parsing request - {stopwatch.ElapsedMilliseconds}");

			if (request.Method == "HEAD") return null;

			(var requestObjectValue, error) = await ParseRequest(context);
			if (error != null) return error;

			logger.LogTrace($"  - Done parsing request, doing callback info - {stopwatch.ElapsedMilliseconds}");

			(var callback, goal, error) = await GetCallbackInfos(request, goal);
			if (error != null) return error;
			if (goal == null) return new ProgramError("Server code has changed. New request needs to be made", step, StatusCode: 503);

			logger.LogTrace($"  - Done callback info, getting engine - {stopwatch.ElapsedMilliseconds}");

			if (requestObjectValue != null)
			{
				context.MemoryStack.Put(requestObjectValue, step);
			}
			context!.Callback = callback;
			if (slugVariables != null)
			{
				foreach (var item in slugVariables)
				{
					context.MemoryStack.Put(item, step, disableEvent: true);
				}
			}
			logger.LogDebug($"  - Run goal - {stopwatch.ElapsedMilliseconds}");

			(var vars, error) = await requestEngine.RunGoal(goal, context);
			//if (error is IErrorHandled) error = null;

			logger.LogDebug($"  - Return engine - {stopwatch.ElapsedMilliseconds}");

			if (error == null && webserverInfo.OnGoalRequestEnd != null)
			{
				error = await RunOnRequest(requestEngine, webserverInfo.OnGoalRequestEnd, context);
			}

			return error;
		}

		private async Task<(Callback? Callback, Goal? goal, IError? Error)> GetCallbackInfos(HttpRequest request, Goal goal)
		{
			string? callbackValue = null;
			if (request.Headers.TryGetValue("X-Callback", out var headerValue))
			{
				callbackValue = headerValue.ToString();
			}
			if (string.IsNullOrEmpty(callbackValue) && request.HasFormContentType)
			{
				callbackValue = request.Form["callback"];
				if (string.IsNullOrEmpty(callbackValue))
				{
					return (null, goal, null);
				}
			}

			if (string.IsNullOrEmpty(callbackValue)) return (null, goal, null);

			(var callback, var newCallback, var error) = await CallbackHelper.GetCallback(identity, callbackValue);
			if (newCallback != null) return (null, goal, new StatelessCallbackError(newCallback, statusCode: error?.StatusCode ?? 400));
			if (error != null) return (null, goal, error);

			var callbackInfo = callback.CallbackInfo;
			goal = prParser.GetAllGoals().FirstOrDefault(p => p.Hash == callbackInfo.GoalHash);

			return (callback, goal, null);
		}


		private async Task<IError?> ProcessPlangRequest(PLangContext context, WebserverProperties webserverInfo, List<Routing>? routings, IEngine requestEngine)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			var httpContext = context.HttpContext!;
			logger.LogDebug($" - Verify signature - {stopwatch.ElapsedMilliseconds}");
			if (!httpContext.Response.HasStarted)
			{
				httpContext.Response.ContentType = "application/plang+json; charset=utf-8";
			}

			logger.LogDebug($" - get routing - {stopwatch.ElapsedMilliseconds}");
			(var goal, var routing, var slugVariables, var error) = GetGoalByRouting(routings, httpContext.Request);

			if (error != null) return error;
			if (routing == null) return new NotFoundError($"Routing not found - {httpContext.Request.Path}({httpContext.Request.Method}) - {httpContext.Request.Headers.UserAgent}");
			if (goal == null) return new NotFoundError($"Goal not found - {httpContext.Request.Path}({httpContext.Request.Method}) - {httpContext.Request.Headers.UserAgent}");

			var rp = routing.ResponseProperties with { ContentType = "application/plang+json" };
			routing = routing with { ResponseProperties = rp };

			logger.LogDebug($" - ProcessGoal starts - {stopwatch.ElapsedMilliseconds}");

			error = await ProcessGoal(goal, slugVariables, webserverInfo, routing, context, requestEngine);

			logger.LogDebug($" - ProcessGoal done - {stopwatch.ElapsedMilliseconds}");
			return error;
		}


		private async Task<(SignedMessage? SignedMessage, IError? Error)> VerifySignature(IEngine engine, PLangContext context)
		{
			if (context.SignedMessage != null) return (context.SignedMessage, null);

			var httpContext = context.HttpContext!;

			if (!httpContext.Request.Headers.TryGetValue("X-Signature", out var signatureHeader))
			{
				return (null, null);
			}

			var signatureData = signatureHeader.ToString();
			if (string.IsNullOrEmpty(signatureData))
			{
				return (null, new UnauthorizedError("X-Signature is empty. Use plang app or compatible to continue."));
			}

			var request = httpContext.Request;
			var headers = new Dictionary<string, object?>();
			foreach (var header in httpContext.Request.Headers)
			{
				headers[header.Key] = header.Value;
			}

			byte[]? rawBody = await GetRawBody(request);
			if (rawBody == null) return (null, new Error("Cannot read body"));

			var verifiedSignatureResult = await identity.VerifySignature(signatureData, headers, rawBody);
			if (verifiedSignatureResult.Error != null) return (null, verifiedSignatureResult.Error);

			if (verifiedSignatureResult.Signature != null)
			{
				context.SignedMessage = verifiedSignatureResult.Signature;
				context.Identity = verifiedSignatureResult.Signature.Identity;

				context.MemoryStack.Put(new DynamicObjectValue("Identity", () =>
				{
					return context.Identity;
				}));
				context.MemoryStack.Put(new DynamicObjectValue("!Signature", () =>
				{
					return context.SignedMessage;
				}));
			}

			return verifiedSignatureResult;
		}

		private async Task<byte[]?> GetRawBody(HttpRequest request)
		{
			request.EnableBuffering();
			if (!request.Body.CanRead) return null;

			using var ms = new MemoryStream();
			await request.Body.CopyToAsync(ms);
			request.Body.Position = 0;
			return ms.ToArray();
		}

		private async Task<IError?> HandlePlangPoll(IEngine requestEngine, PLangContext context, WebserverProperties props, string query)
		{
			var httpContext = context.HttpContext!;
			SignedMessage? signedMessage = context.SignedMessage;
			if (signedMessage == null) return null;

			var outputStream = context.UserSink as HttpSink;
			if (outputStream == null) return new Error("OutputStream is not HttpOutputStream");

			LiveConnection? liveResponse = null;
			outputStream.LiveConnections.TryGetValue(context.Identity, out liveResponse);

			bool startPoll = requestEngine.LiveConnections.ContainsKey(context.Identity);

			var response = httpContext.Response;
			if (!response.HasStarted)
			{
				response.ContentType = "application/plang+json; charset=utf-8";
				response.Headers.Add("Cache-Control", "no-cache");
			}

			if (liveResponse != null)
			{
				liveResponse.IsFlushed = true;
				outputStream.LiveConnections.AddOrReplace(signedMessage.Identity, liveResponse);
			}
			else
			{
				outputStream.LiveConnections.AddOrReplace(signedMessage.Identity, new LiveConnection(httpContext.Response, true));
			}

			if (props.OnPollRefresh != null && query.StartsWith("?plang.poll=1&refresh=1"))
			{
				var (_, error) = await requestEngine.RunGoal(props.OnPollRefresh, goal, context);
				return error;
			}
			else if (props.OnPollStart != null)
			{
				var (_, error) = await requestEngine.RunGoal(props.OnPollStart, goal, context);
				return error;
			}

			return null;
		}



		/// <summary>
		/// Does the client already hold this exact version of the file?
		/// </summary>
		/// <remarks>
		/// If-None-Match wins outright when present, per RFC 9110: it is an exact comparison,
		/// where If-Modified-Since is only a timestamp and cannot see two edits in one second.
		/// Both are checked because a browser sends whichever validator it was given.
		/// </remarks>
		private static bool IsNotModified(HttpRequest request, string etag, DateTimeOffset lastModified)
		{
			var ifNoneMatch = request.Headers[HeaderNames.IfNoneMatch].ToString();
			if (!string.IsNullOrEmpty(ifNoneMatch))
			{
				// A client may send several, and "*" means any representation.
				foreach (var candidate in ifNoneMatch.Split(','))
				{
					var value = candidate.Trim();
					if (value == "*" || value == etag) return true;
				}
				return false;
			}

			var ifModifiedSince = request.Headers[HeaderNames.IfModifiedSince].ToString();
			if (!string.IsNullOrEmpty(ifModifiedSince)
				&& DateTimeOffset.TryParse(ifModifiedSince, CultureInfo.InvariantCulture,
					DateTimeStyles.AdjustToUniversal, out var since))
			{
				return lastModified <= since;
			}

			return false;
		}

		/// <summary>Removes one `vN` path segment, or null if there is none.</summary>
		private static string? StripVersionSegment(string path)
		{
			var sep = System.IO.Path.DirectorySeparatorChar;
			var parts = path.Split(sep, '/');
			for (int i = 0; i < parts.Length - 1; i++)
			{
				var p = parts[i];
				if (p.Length < 2 || (p[0] != 'v' && p[0] != 'V')) continue;
				if (!p.Skip(1).All(char.IsLetterOrDigit)) continue;
				if (!p.Skip(1).Any(char.IsDigit)) continue;
				return string.Join(sep, parts.Where((_, j) => j != i));
			}
			return null;
		}

		private async Task<IError?> ProcessGeneralRequest(HttpContext httpContext)
		{
			var requestedFile = httpContext.Request.Path.Value;
			if (string.IsNullOrEmpty(requestedFile)) return new NotFoundError($"Path is empty - {httpContext.Request.Path}({httpContext.Request.Method}) - {httpContext.Request.Headers.UserAgent}");

			requestedFile = requestedFile.AdjustPathToOs();

			var filePath = fileSystem.Path.Join(fileSystem.GoalsPath!, requestedFile);
			var fileExtension = fileSystem.Path.GetExtension(filePath);
			var mimeType = GetMimeType(fileExtension);
			if (mimeType == null)
			{
				httpContext.Response.StatusCode = 415;
				return new Error($"Unsupported Media Type - {httpContext.Request.Path.ToString()} | {httpContext.Request.Method} - {httpContext.Request.Headers.UserAgent}", StatusCode: 415);
			}

			// /js/app/v7/main.js -> /js/app/main.js. A version segment in the PATH (not the query)
			// is inherited by relative imports, so one bump breaks every cached module together.
			if (!fileSystem.File.Exists(filePath))
			{
				var unversioned = StripVersionSegment(requestedFile);
				if (unversioned != null)
				{
					var candidate = fileSystem.Path.Join(fileSystem.GoalsPath!, unversioned);
					if (fileSystem.File.Exists(candidate)) filePath = candidate;
				}
			}

			if (!fileSystem.File.Exists(filePath))
			{
				// Complete it, or the caller's "no error means 200" turns this into an empty 200.
				httpContext.Response.StatusCode = 404;
				await httpContext.Response.CompleteAsync();
				return null;
			}

			// Static files went out with no Last-Modified, no ETag and no Cache-Control, so a
			// browser had nothing to validate against and fell back to its own heuristic. Each
			// file then aged independently, which is how a page ends up running a fresh module
			// against a stale one it imports. Seen in production on rafbokin.is: a new Plang.js
			// calling storeTier() on a cached ECDSASigner.js that predated the method.
			//
			// "no-cache" does not mean do not cache, it means cache but revalidate. With a
			// validator present the repeat cost is a 304 with no body, so this is cheaper than
			// today for unchanged files and, unlike today, never serves a stale mix.
			var fileInfo = fileSystem.FileInfo.New(filePath);
			var lastModified = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUniversalTime();
			// Truncate to whole seconds: HTTP dates have no sub-second precision, so comparing
			// an untruncated timestamp against the header the browser echoes back never matches
			// and every request would be a miss.
			lastModified = lastModified.AddTicks(-(lastModified.Ticks % TimeSpan.TicksPerSecond));
			var etag = $"W/\"{lastModified.ToUnixTimeSeconds():x}-{fileInfo.Length:x}\"";

			if (!httpContext.Response.HasStarted)
			{
				httpContext.Response.Headers[HeaderNames.ETag] = etag;
				httpContext.Response.Headers[HeaderNames.LastModified] = lastModified.ToString("R");
				httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache";
			}

			if (IsNotModified(httpContext.Request, etag, lastModified))
			{
				// A 304 must not carry a body, and must not claim a length it will not send.
				httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
				httpContext.Response.Headers.Remove(HeaderNames.ContentLength);
				// Complete it here. The caller sets 200 on any response that has not started
				// yet, and a bodyless reply never starts on its own, so without this the 304
				// is silently rewritten to 200 and the file is sent after all.
				await httpContext.Response.CompleteAsync();
				return null;
			}

			try
			{
				await using var stream = fileSystem.File.OpenRead(filePath);
				if (httpContext.Response.HasStarted)
				{
					int i = 0;
				}
				else
				{
					httpContext.Response.ContentLength = stream.Length;
					httpContext.Response.ContentType = mimeType;
				}
				await stream.CopyToAsync(httpContext.Response.Body);

			}
			catch (Exception ex)
			{
				Console.WriteLine($"     - {filePath} - end read stream - {httpContext.TraceIdentifier} | ex:" + ex);
			}
			return null;
		}
		public static string? GetMimeType(string extension)
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
				case ".map": return "application/json";
				case ".xml": return "application/xml";
				case ".csv": return "application/csv";

				case ".mp4": return "video/mp4";
				case ".webm": return "video/webm";

				case ".pdf": return "application/pdf";


				default: return null;
			}
		}

		private (Goal?, Routing?, List<ObjectValue>? SlugVariables, IError?) GetGoalByRouting(List<Routing>? routings, HttpRequest request)
		{
			if (request == null || request.Path == null || routings == null)
			{
				return (null, null, null, new ProgramError("request object empty", step, StatusCode: 500));
			}

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
			if (string.IsNullOrEmpty(routing.Route?.Goal?.Name)) return (null, new ProgramError("Goal name in route is empty", step, StatusCode: 500));

			var result = GoalHelper.GetGoal("/", fileSystem.RootDirectory, routing.Route.Goal, prParser.GetGoals(), new List<Goal>());
			if (result.Item1 != null) return (result.Item1, null);

			var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RelativeAppPath, routing.Route.Goal.Name);
			if (goal != null)
			{
				return (goal, null);
			}

			var goalName = request.Path.Value?.AdjustPathToOs();
			if (goalName == null) return (null, new ProgramError("Goal name could not be extracted from request path", step, StatusCode: 500));

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

		string[] supportedHeaders = ["p-target", "p-actions"];

		private void ParseHeaders(PLangContext context, IOutputSink outputStream)
		{
			var httpContext = context.HttpContext;
			var headers = httpContext.Request.Headers;

			context.UiOutputProperties = new UiOutputProperties(httpContext.Request.Path.ToString());

			var target = headers.FirstOrDefault(p => p.Key.Equals("p-target", StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrEmpty(target.Value.FirstOrDefault()))
			{
				context.UiOutputProperties.Target = target.Value.FirstOrDefault();
			}
			var errorTarget = headers.FirstOrDefault(p => p.Key.Equals("p-error-target", StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrEmpty(errorTarget.Value.FirstOrDefault()))
			{
				context.UiOutputProperties.ErrorTarget = errorTarget.Value.FirstOrDefault();
			}
			var actions = headers.FirstOrDefault(p => p.Key.Equals("p-actions", StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrEmpty(actions.Value.FirstOrDefault()))
			{
				context.UiOutputProperties.Actions = new();
				foreach (var item in actions.Value)
				{
					if (item == null) continue;
					context.UiOutputProperties.Actions.Add(item);
				}
			}
		}

		private async Task<(ObjectValue? ObjectValue, IError? Error)> ParseRequest(PLangContext context)
		{
			HttpContext httpContext = context.HttpContext!;
			if (httpContext is null) return (null, new Error("context is empty"));
			if (httpContext.Items.TryGetValue("request", out object? value) && value != null)
			{
				return (value as ObjectValue, null);
			}
			;

			Stopwatch stopwatch = Stopwatch.StartNew();
			var parameters = new Dictionary<string, object?>();
			var req = httpContext.Request;

			ObjectValue objectValue;
			logger.LogDebug($"    - ParseHeader - {stopwatch.ElapsedMilliseconds}");
			ParseHeaders(context, context.UserSink);
			logger.LogDebug($"    - GetRequest - {stopwatch.ElapsedMilliseconds}");
			var properties = GetRequestProperties(httpContext);
			logger.LogDebug($"    - Done with GetRequest - {stopwatch.ElapsedMilliseconds}");
			// ---------- JSON --------------------------------------------------------
			if (req.HasJsonContentType())
			{
				logger.LogDebug($"    - JsonHandler starts - {stopwatch.ElapsedMilliseconds}");

				req.EnableBuffering();

				using var reader = new StreamReader(req.Body);
				var bodyString = await reader.ReadToEndAsync();

				if (!string.IsNullOrEmpty(bodyString))
				{
					// Parse into JToken (can be JObject or JArray)
					JToken json = JToken.Parse(bodyString);
					parameters.Add("body", json);
				}

				logger.LogDebug($" - JsonHandler done - {stopwatch.ElapsedMilliseconds}");
			}

			// ---------- Form / Multipart (fields + files) ---------------------------
			if (req.HasFormContentType)
			{
				logger.LogDebug($"    - FormHandler starts - {stopwatch.ElapsedMilliseconds}");

				if (!parameters.ContainsKey("body"))
				{
					try
					{
						var form = await req.ReadFormAsync();
						var fields = form.ToDictionary(
							pair => pair.Key,
							pair => pair.Value.Count > 1
									 ? (object)pair.Value.ToArray()          // keep all repeated values
									 : (object)pair.Value.ToString()!        // single value
						);

						var payload = new Dictionary<string, object?>(fields, StringComparer.OrdinalIgnoreCase);
						if (form.Files.Count > 0)
						{
							payload.Add("_files", form.Files);
						}

						if (payload.Count > 0)
						{
							parameters.Add("body", payload);
						}
					}
					catch (Exception ex)
					{
						var ip = httpContext.Connection.RemoteIpAddress?.ToString();

						Console.WriteLine($"{DateTime.Now} - ERRPR:{ip} | {httpContext.Request.Path} | {httpContext.Request.Headers.UserAgent}\nmultipart error:{ex}");
					}
				}

				logger.LogDebug($"    - FormHandler done - {stopwatch.ElapsedMilliseconds}");

			}

			if (req.Query.Count > 0)
			{
				var query = req.Query.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());
				if (query.Count > 0)
				{
					var qs = new Dictionary<string, object?>();
					foreach (var (k, v) in query)
					{
						qs.Add(k, v);
					}
					parameters.Add("query", query);
				}
			}

			objectValue = new ObjectValue("request", parameters, properties: properties);
			httpContext.Items.Add("request", objectValue);
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



		private (bool, List<ObjectValue>?, IError?) TryMatch(Routing routing, HttpRequest request)
		{

			var path = request.Path.Value;
			if (path == null) return (false, null, null);

			var route = routing.Route;
			if (route == null) return (false, null, null);

			var m = route.PathRegex.Match(path);
			if (!m.Success)
			{
				foreach (var paramInfo in routing.Route.ParamInfos ?? new List<ParamInfo>())
				{
					if (paramInfo.DefaultValue != null && !string.IsNullOrEmpty(paramInfo.DefaultValue.ToString()))
					{
						path += $"/{paramInfo.DefaultValue.ToString()}";
					}
				}
				m = route.PathRegex.Match(path);
				if (!m.Success)
				{
					return (false, null, null);
				}
			}

			var methods = routing.RequestProperties.Methods ?? ["GET"];

			//todo: just temp, should be in build
			if (request.Method != "HEAD")
			{
				var method = methods.FirstOrDefault(p => p.Equals(request.Method, StringComparison.OrdinalIgnoreCase));
				if (method == null) return (false, null, null);
			}

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
				variables.Add(new ObjectValue(item.Key.Replace("__dot__", "."), item.Value));
			}

			return (true, variables, null);
		}


		private Properties? GetRequestProperties(HttpContext httpContext)
		{
			var request = httpContext.Request;
			Properties properties = new();
			properties.Add(new ObjectValue("Method", request.Method));
			properties.Add(new ObjectValue("Path", request.Path.Value));
			properties.Add(new ObjectValue("QueryString", request.QueryString.ToString()));
			properties.Add(new ObjectValue("HasFormContentType", request.HasFormContentType));
			properties.Add(new ObjectValue("HasJsonContentType", request.HasJsonContentType()));

			properties.Add(new ObjectValue("ContentLength", request.ContentLength));
			properties.Add(new ObjectValue("ContentType", request.ContentType));
			properties.Add(new ObjectValue("Headers", request.Headers));
			properties.Add(new ObjectValue("KeepAlive", request.Headers.KeepAlive.ToString()));

			foreach (var item in request.Headers)
			{
				properties.Add(new ObjectValue(item.Key, item.Value.ToString()));
			}

			request.Headers.TryGetValue("X-Requested-With", out var ajax);
			properties.Add(new ObjectValue("IsAjax", ajax.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase)));
			properties.Add(new ObjectValue("Ip", httpContext.Connection.RemoteIpAddress?.ToString()));

			if (!string.IsNullOrEmpty(request.Headers.UserAgent))
			{
				properties.Add(new ObjectValue("UserAgent", request.Headers.UserAgent.ToString()));
				var clientInfo = ParseUserAgent(request.Headers.UserAgent.ToString());

				properties.Add(new ObjectValue("ClientInfo", clientInfo));
			}

			return properties;
		}

		// uap-core's catch all spider rule is anchored to the first 100 characters of the user
		// agent: "^.{0,100}(bot|BUbiNG|...)". Crawlers that disguise themselves as a browser push
		// their marker past that limit and are read as an ordinary visitor. Applebot is the one
		// that matters to us, it sits at character 119 behind "Version/17.4 Safari/605.1.15", so
		// Device.Family came back as "Mac" and IsSpider as false. Remove the Safari token by hand
		// and the same ruleset detects it, which is how the limit was found.
		//
		// These rules are prepended to the embedded ruleset, so upstream is untouched and the
		// package can be updated without losing them. Unanchored on purpose: that is the whole
		// point, the marker is allowed to sit anywhere in the string.
		static readonly string[] extraSpiderRegexes = new[]
		{
			"(Applebot|AppleNewsBot)",
			"(GPTBot|OAI-SearchBot|ChatGPT-User|ClaudeBot|Claude-Web|anthropic-ai|PerplexityBot|Bytespider|Amazonbot|meta-externalagent)",
		};

		static Parser parser = BuildParser();

		static Parser BuildParser()
		{
			// A user agent parser that throws takes every request with it, so a failure here has
			// to fall back rather than propagate. The embedded ruleset is what GetDefault uses.
			try
			{
				using var stream = typeof(Parser).Assembly.GetManifestResourceStream("UAParser.regexes.json");
				if (stream == null) return Parser.GetDefault();

				using var reader = new StreamReader(stream);
				var root = System.Text.Json.Nodes.JsonNode.Parse(reader.ReadToEnd())?.AsObject();
				var devices = root?["device_parsers"]?.AsArray();
				if (root == null || devices == null) return Parser.GetDefault();

				for (int i = extraSpiderRegexes.Length - 1; i >= 0; i--)
				{
					devices.Insert(0, new System.Text.Json.Nodes.JsonObject
					{
						["regex"] = extraSpiderRegexes[i],
						["regex_flag"] = "i",
						["device_replacement"] = "Spider",
						["brand_replacement"] = "Spider",
						["model_replacement"] = "Desktop",
					});
				}

				return Parser.FromJson(root.ToJsonString(), null);
			}
			catch
			{
				return Parser.GetDefault();
			}
		}

		// Parser.FromJson has no overload that takes an IMemoryCache, only GetDefault does, so
		// Parse must be called with useCache false or it dereferences a null cache and throws on
		// every request. Parsing runs 633 device rules, so it is worth caching, and this keeps
		// that without depending on the library's own cache.
		static readonly System.Collections.Concurrent.ConcurrentDictionary<string, UAParser.Objects.ClientInfo> uaCache = new();

		static UAParser.Objects.ClientInfo ParseUserAgent(string userAgent)
		{
			if (uaCache.TryGetValue(userAgent, out var cached)) return cached;

			var info = parser.Parse(userAgent, false);
			// Bounded so a stream of junk user agents cannot grow it without limit.
			if (uaCache.Count < 5000) uaCache.TryAdd(userAgent, info);
			return info;
		}

	}
}
