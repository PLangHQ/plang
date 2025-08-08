using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.Transformers;
using PLang.Utils;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

		public async Task<bool> HandleRequestAsync(IEngine requestEngine, HttpContext ctx, WebserverProperties webserverProperties)
		{
			
			try
			{
				if (webserverProperties.DefaultRequestProperties!.SignedRequestRequired && !ctx.Request.Headers.TryGetValue("X-Signature", out var value))
				{
					await HandleError(requestEngine, new Error("All requests must be signed"));
					return false;
				}

				(var signedMessage, var error) = await VerifySignature(requestEngine, ctx);
				if (error != null) {
					await HandleError(requestEngine, error);
					return false;
				}

				(var requestObjectValue, error) = ParseRequest(ctx, requestEngine.OutputStream).Result;
				requestEngine.MemoryStack.Put(requestObjectValue);
				

				if (webserverProperties.OnRequestBegin != null)
				{
					await RunOnRequest(requestEngine, webserverProperties.OnRequestBegin);
				}

				if (webserverProperties.OnPollStart != null)
				{
					string? query = ctx.Request.QueryString.Value;
					if (query?.StartsWith("?plang.poll=1") == true)
					{
						await HandlePlangPoll(requestEngine, ctx, webserverProperties);
						return true;
					}
				}


				error = await HandleRequest(ctx, requestEngine, webserverProperties);

				if (error != null && !error.Handled && error is not IErrorHandled && error is not EndGoal)
				{
					await HandleError(requestEngine, error);
					return false;
				}
				else
				{
					if (!ctx.Response.HasStarted)
					{
						ctx.Response.StatusCode = 200;
					}
				}

				if (webserverProperties.OnRequestEnd != null)
				{
					await RunOnRequest(requestEngine, webserverProperties.OnRequestEnd);
				}

				if (error != null)
				{
					await HandleError(requestEngine, error);

				}

			}
			catch (Exception ex)
			{
				try
				{
					var (_, error) = await requestEngine.GetEventRuntime().AppErrorEvents(new ExceptionError(ex, ex.Message, goal, step));
					if (error != null)
					{
						Console.WriteLine(error);
					}
				} catch (Exception ex2)
				{
					Console.WriteLine(ex2);
					Console.WriteLine(ex);
				}
			}
			return false;

		}



		private async Task<IError?> HandleRequest(HttpContext httpContext, IEngine requestEngine, WebserverProperties webserverInfo)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				IError? error = null;

				var acceptedTypes = httpContext.Request.Headers.Accept.FirstOrDefault();
				

				var isPlangRequest = acceptedTypes?.StartsWith("application/plang") ?? false;
				if (isPlangRequest)
				{
					Console.WriteLine($"plang: {httpContext.Request.Path} | {httpContext.Request.Headers.UserAgent}");
					logger.LogInformation($" ---------- Request Starts ---------- - {stopwatch.ElapsedMilliseconds}");
					error = await ProcessPlangRequest(httpContext, webserverInfo, webserverInfo.Routings, requestEngine);
					logger.LogInformation($" ---------- Request Done ---------- - {stopwatch.ElapsedMilliseconds}");
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
					return new NotFoundError("Routing not found");
				}

				logger.LogInformation($" ---------- Request Starts ---------- - {stopwatch.ElapsedMilliseconds}");
				Console.WriteLine($"classic: {httpContext.Request.Path} | {httpContext.Request.Headers.UserAgent}");
				error = await ProcessGoal(goal, slugVariables, webserverInfo, routing, httpContext, requestEngine);

				logger.LogInformation($" ---------- Request Done ---------- - {stopwatch.ElapsedMilliseconds}");

				return error;
			}
			catch (Exception ex)
			{
				return new Error(ex.Message, Key: "WebserverCore", 500, ex);
			}
		}

		private async Task HandleError(IEngine? engine, IError error)
		{
			if (error is IErrorHandled) return;

			//last effort, write to system output
			if (engine != null)
			{
				(_, var error2) = await engine.GetEventRuntime().AppErrorEvents(error);
				if (error2 == null) return;


				await engine.OutputStream.Write(step, error, "error", 500);
			}
			else
			{
				Console.WriteLine(error.ToString());
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




		private async Task RunOnRequest(IEngine engine, GoalToCallInfo goalToCall)
		{

			(_, var error) = await engine!.RunGoal(goalToCall, goal);
			if (error == null) return;

			if (error != null)
			{
				(_, error) = await engine.GetEventRuntime().AppErrorEvents(error);
				if (error == null) return;

				var output = engine.OutputStream;
				await output.Write(step, error, "error", 500);
			}
		}

		private async Task<IError?> ProcessGoal(Goal goal, List<ObjectValue>? slugVariables, WebserverProperties webserverInfo,
			Routing routing, HttpContext httpContext, IEngine requestEngine)
		{
			if (goal == null)
			{
				return new NotFoundError($"Goal could not be loaded");
			}
			Stopwatch stopwatch = Stopwatch.StartNew();

			var resp = httpContext.Response;
			var request = httpContext.Request;
			/*resp.OnStarting(() =>
			{
				int i = 0;

				return Task.CompletedTask;
			});*/


			if (!resp.HasStarted && request.QueryString.Value == "__signature__")
			{
				resp.Headers.Add("X-Goal-Hash", goal.Hash);
				resp.Headers.Add("X-Goal-Signature", goal.Signature);
				resp.StatusCode = 200;
				return null;
			}

			long maxContentLength = routing.RequestProperties.MaxContentLengthInBytes;
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
			logger.LogDebug($"  - Starting parsing request - {stopwatch.ElapsedMilliseconds}");

			(var requestObjectValue, var error) = await ParseRequest(httpContext, requestEngine.OutputStream);
			if (error != null) return error;

			logger.LogDebug($"  - Done parsing request, doing callback info - {stopwatch.ElapsedMilliseconds}");

			(var callbackInfos, error) = await GetCallbackInfos(request);
			if (error != null) return error;

			logger.LogDebug($"  - Done callback info, getting engine - {stopwatch.ElapsedMilliseconds}");



			if (requestObjectValue != null)
			{
				requestEngine.MemoryStack.Put(requestObjectValue, step);
			}
			requestEngine!.CallbackInfos = callbackInfos;
			if (slugVariables != null)
			{
				foreach (var item in slugVariables)
				{
					requestEngine.MemoryStack.Put(item, step, disableEvent: true);
				}
			}
			logger.LogDebug($"  - Run goal - {stopwatch.ElapsedMilliseconds}");


			(var vars, error) = await requestEngine.RunGoal(goal, 0);
			if (error != null && !error.Handled && error is not IErrorHandled)
			{
				(var returnVars, error) = await requestEngine.GetEventRuntime().AppErrorEvents(error);
			}

			logger.LogDebug($"  - Return engine - {stopwatch.ElapsedMilliseconds}");

			return error;
		}

		private async Task<(List<CallbackInfo>? CallbackInfo, IError? Error)> GetCallbackInfos(HttpRequest request)
		{
			string? callbackValue = "";
			if (request.HasFormContentType)
			{
				callbackValue = request.Form["callback"];
				if (string.IsNullOrEmpty(callbackValue))
				{
					if (request.Headers.TryGetValue("X-Callback", out var value))
					{
						callbackValue = value.ToString();
					}
					else
					{
						return (null, null);
					}
				}
			}
			else
			{
				if (!request.Headers.TryGetValue("callback", out var value)) return (null, null);
				callbackValue = value.ToString();
			}

			if (string.IsNullOrEmpty(callbackValue)) return (null, null);

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
				/*if (!resp.HasStarted)
				{
					resp.StatusCode = error.StatusCode;
				}*/
				var errorStep = (error.Step != null) ? error.Step : step;


				await outputStream.Write(errorStep, error, statusCode: error.StatusCode);

				await resp.CompleteAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error:" + error.ToString());
				Console.WriteLine("Exception when writing out error:" + ex);
			}
		}

		private async Task<IError?> ProcessPlangRequest(HttpContext httpContext, WebserverProperties webserverInfo, List<Routing>? routings, IEngine requestEngine)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			logger.LogDebug($" - Verify signature - {stopwatch.ElapsedMilliseconds}");
			if (!httpContext.Response.HasStarted)
			{
				httpContext.Response.ContentType = "application/plang+json; charset=utf-8";
			}

			logger.LogDebug($" - get routing - {stopwatch.ElapsedMilliseconds}");
			(var goal, var routing, var slugVariables, var error) = GetGoalByRouting(routings, httpContext.Request);

			if (error != null) return error;
			if (routing == null) return new NotFoundError("Routing not found");
			if (goal == null) return new NotFoundError("Goal not found");

			var rp = routing.ResponseProperties with { ContentType = "application/plang+json" };
			routing = routing with { ResponseProperties = rp };

			logger.LogDebug($" - ProcessGoal starts - {stopwatch.ElapsedMilliseconds}");

			error = await ProcessGoal(goal, slugVariables, webserverInfo, routing, httpContext, requestEngine);

			logger.LogDebug($" - ProcessGoal done - {stopwatch.ElapsedMilliseconds}");
			return error;
		}


		private async Task<(SignedMessage? SignedMessage, IError? Error)> VerifySignature(IEngine engine, HttpContext httpContext)
		{
			/*
			if (httpContext.Items.TryGetValue("SignedMessage", out object? sig) && sig is SignedMessage sm)
			{
				engine.MemoryStack.Put(ReservedKeywords.Identity, sm.Identity);
				engine.MemoryStack.Put(ReservedKeywords.Signature, sm.Signature);

				return (sm, null);
			}*/

			if (!httpContext.Request.Headers.TryGetValue("X-Signature", out var signatureHeader))
			{
				engine.MemoryStack.Remove(ReservedKeywords.Identity);
				return (null, null);
			}

			var signatureData = signatureHeader.ToString();
			if (string.IsNullOrEmpty(signatureData))
			{
				engine.MemoryStack.Remove(ReservedKeywords.Identity);
				return (null, new UnauthorizedError("X-Signature is empty. Use plang app or compatible to continue."));
			}

			var request = httpContext.Request;
			var headers = new Dictionary<string, object?>();
			foreach (var header in httpContext.Request.Headers)
			{
				headers[header.Key] = header.Value;
			}

			byte[] rawBody = await GetRawBody(request);

			var verifiedSignatureResult = await identity.VerifySignature(signatureData, headers, rawBody);
			if (verifiedSignatureResult.Error != null) return (null, verifiedSignatureResult.Error);

			if (verifiedSignatureResult.Signature != null)
			{
				engine.MemoryStack.Put(ReservedKeywords.Identity, verifiedSignatureResult.Signature.Identity);
				engine.MemoryStack.Put(ReservedKeywords.Signature, verifiedSignatureResult.Signature);
			}
			else
			{
				engine.MemoryStack.Remove(ReservedKeywords.Identity);
			}

			var outputStream = engine.OutputStream as HttpOutputStream;
			if (outputStream != null)
			{
				outputStream.SetIdentity(verifiedSignatureResult.Signature.Identity);
			}
			httpContext.Items.Add("SignedMessage", verifiedSignatureResult.Signature);

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

		private async Task HandlePlangPoll(IEngine requestEngine, HttpContext ctx, WebserverProperties props)
		{
			SignedMessage? signedMessage = ctx.Items["SignedMessage"] as SignedMessage;
			if (signedMessage == null) return;

			var outputStream = requestEngine.OutputStream as HttpOutputStream;
			if (outputStream == null) return;

			LiveConnection? liveResponse = null;
			outputStream.LiveConnections.TryGetValue(signedMessage.Identity, out liveResponse);
			
			bool startPoll = requestEngine.LiveConnections.ContainsKey(signedMessage.Identity);

			var response = ctx.Response;
			if (!response.HasStarted)
			{
				response.ContentType = "application/plang+json; charset=utf-8";
				response.Headers.Add("Cache-Control", "no-cache");
			}

			outputStream.LiveConnections.AddOrReplace(signedMessage.Identity, new LiveConnection(ctx.Response, true));

			if (props.OnPollStart != null)
			{
				var (_, error) = await requestEngine.RunGoal(props.OnPollStart, goal);
				if (error != null)
				{
					await HandleError(requestEngine, error);
				}
			}


		}



		private async Task<IError?> ProcessGeneralRequest(HttpContext httpContext)
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
				httpContext.Response.StatusCode = 404;
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
				case ".xml": return "application/xml";
				case ".csv": return "application/csv";

				case ".mp4": return "video/mp4";
				case ".webm": return "video/webm";

				case ".pdf": return "application/pdf";


				default: return null;
			}
		}

		private(Goal?, Routing?, List<ObjectValue>? SlugVariables, IError?) GetGoalByRouting(List<Routing>? routings, HttpRequest request)
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

			var result = GoalHelper.GetGoal("/", fileSystem.RootDirectory, routing.Route.Goal, prParser.GetGoals(), new());
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

		string[] supportedHeaders = ["data-plang-js", "data-plang-response", "data-plang-js-params", "data-plang-cssSelector", "data-plang-action"];

		private void ParseHeaders(HttpContext ctx, IOutputStream outputStream)
		{
			var headers = ctx.Request.Headers;


			Dictionary<string, object?> responseProperties = new();
			foreach (var supportedHeader in supportedHeaders)
			{
				var keyValue = headers.FirstOrDefault(p => p.Key.Equals(supportedHeader, StringComparison.OrdinalIgnoreCase));
				if (!string.IsNullOrEmpty(keyValue.Value.FirstOrDefault()))
				{
					responseProperties.AddOrReplace(supportedHeader, keyValue.Value.FirstOrDefault());
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
			if (ctx.Items.TryGetValue("request", out object? value) && value != null)
			{
				return (value as ObjectValue, null);
			};

			Stopwatch stopwatch = Stopwatch.StartNew();
			var parameters = new Dictionary<string, object?>();
			var req = ctx.Request;
			var query = req.Query.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());
			ObjectValue objectValue;
			logger.LogDebug($"    - ParseHeader - {stopwatch.ElapsedMilliseconds}");
			ParseHeaders(ctx, outputStream);
			logger.LogDebug($"    - GetRequest - {stopwatch.ElapsedMilliseconds}");
			var properties = GetRequestProperties(ctx);
			logger.LogDebug($"    - Done with GetRequest - {stopwatch.ElapsedMilliseconds}");
			// ---------- JSON --------------------------------------------------------
			if (req.HasJsonContentType())
			{
				logger.LogDebug($"    - JsonHandler starts - {stopwatch.ElapsedMilliseconds}");

				req.EnableBuffering();

				using var reader = new StreamReader(req.Body);
				var bodyString = await reader.ReadToEndAsync();

				// Parse into JToken (can be JObject or JArray)
				JToken json = JToken.Parse(bodyString);
				parameters.Add("body", json);

				logger.LogDebug($" - JsonHandler done - {stopwatch.ElapsedMilliseconds}");
			}

			// ---------- Form / Multipart (fields + files) ---------------------------
			if (req.HasFormContentType)
			{
				logger.LogDebug($"    - FormHandler starts - {stopwatch.ElapsedMilliseconds}");

				if (!parameters.ContainsKey("body"))
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

					parameters.Add("body", payload);
				}

				logger.LogDebug($"    - FormHandler done - {stopwatch.ElapsedMilliseconds}");

			}

			if (query.Count > 0)
			{
				var qs = new Dictionary<string, object?>();
				foreach (var (k, v) in query)
				{
					qs.Add(k, v);
				}
				parameters.Add("query", query);
			}

			objectValue = new ObjectValue("request", parameters, properties: properties);
			ctx.Items.Add("request", objectValue);
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
			if (!m.Success) return (false, null, null);

			var methods = routing.RequestProperties.Methods ?? ["GET"];

			var method = methods.FirstOrDefault(p => p.Equals(request.Method, StringComparison.OrdinalIgnoreCase));
			if (method == null) return (false, null, new ProgramError($"{request.Method} is not supported for {request.Path}", step));

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
	

		private Properties? GetRequestProperties(HttpContext httpContext)
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
}
