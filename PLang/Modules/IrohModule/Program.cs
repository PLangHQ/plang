using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;

namespace PLang.Modules.IrohModule
{
	[Description("Talk to other plang apps over iroh, peer to peer by key and not by ip. Create and print the endpoint key (`create iroh key`), listen for signed commands (`start listening`), connect to a peer by its key (`connect to`) and tell it to run a goal (`tell %peer% to Update`)")]
	public class Program(IPseudoRuntime pseudoRuntime, IPLangSigningService signingService) : BaseProgram
	{
		public const string Alpn = "plang/tell/1";
		private const string SignatureFileExtension = ".signature";
		private const int MaxSignatureFileBytes = 16 * 1024;
		private const string SecretKeyPrefix = "IrohSecretKey_";
		private const int MaxHeaderBytes = 1024 * 1024;
		private const int MaxAnswerBytes = 16 * 1024 * 1024;
		private const int ChunkTimeoutMilliseconds = 30000;
		private const long DefaultMaxFileBytes = 512L * 1024 * 1024;
		private const string MaxFileBytesSetting = "IrohMaxFileBytes";
		private const string IncomingFolder = "/.tmp/iroh";

		private static readonly ConcurrentDictionary<string, IrohEndpoint> Endpoints = new();

		public record IrohKey(string Name, string EndpointId);
		public record IrohListener(string Name, string EndpointId, string Ticket);
		public record IrohPeer(string Target, string? EndpointId, IrohConnection Connection);

		[Description("Create the iroh endpoint key with a name, saved in settings, and return its endpoint id to print or share. When the key exists it is returned unchanged, unless replace is true. The secret is never returned")]
		[Example("create iroh key, write to %key%", "name=\"default\"")]
		[Example("create iroh key 'kiosk', replace, write to %key%", "name=\"kiosk\", replace=true")]
		public async Task<IrohKey> CreateKey(string name = "default", bool replace = false)
		{
			string settingKey = SecretKeyPrefix + name;
			string? secret = settings.GetOrDefault<string?>(typeof(Program), settingKey, null);
			if (replace || string.IsNullOrEmpty(secret))
			{
				secret = IrohKeys.Generate().Secret;
				settings.Set(typeof(Program), settingKey, secret);
			}
			return new IrohKey(name, IrohKeys.IdFromSecret(secret));
		}

		[Description("Get the endpoint id of a named iroh key. The key is created when it does not exist")]
		[Example("get iroh key, write to %key%", "name=\"default\"")]
		public async Task<IrohKey> GetKey(string name = "default")
		{
			return await CreateKey(name, false);
		}

		[Description("Start listening for iroh peers. Every incoming command must be signed, an unsigned or badly signed one is declined. The goal in onIncoming is called first with %Identity% (the signer), %goalName%, %parameters% and %EndpointId% (and %fileName% and %fileSize% when a file comes with the command, the file is not received until this goal has not failed), and it decides if the identity is allowed by throwing an error to decline. When it does not fail the requested goal runs and what it returns goes back to the caller. onIncoming is required")]
		[Example("start listening, on incoming call Allow, write to %listener%", "onIncoming=Allow")]
		[Example("listen on iroh 'kiosk', on incoming call Allow", "keyName=\"kiosk\", onIncoming=Allow")]
		public async Task<(IrohListener?, IError?)> Listen(GoalToCallInfo? onIncoming = null, string keyName = "default", bool useRelay = true)
		{
			if (onIncoming == null || string.IsNullOrEmpty(onIncoming.Name))
			{
				return (null, new ProgramError("A goal that validates who may tell this app what to do is required", goalStep, function,
					FixSuggestion: "Add 'on incoming call Allow' to the step, e.g. `- start listening, on incoming call Allow`. The goal gets %Identity% and throws an error to decline"));
			}

			var (endpoint, error) = GetEndpoint(keyName, useRelay);
			if (error != null) return (null, error);

			var listener = new IrohListener(keyName, endpoint!.Id, endpoint.Ticket());
			RemoveOldReceivedFiles(fileSystem.RootDirectory);

			var engineRef = engine;
			var accessorRef = contextAccessor;
			var rootPath = fileSystem.RootDirectory;
			_ = Task.Factory.StartNew(() => endpoint.Serve(
				stream => HandleStream(stream, endpoint, onIncoming, engineRef, accessorRef, rootPath),
				ex => logger?.LogWarning("iroh: {Message}", ex.Message)), TaskCreationOptions.LongRunning);

			KeepAlive(endpoint, "iroh-listen-" + keyName);
			return (listener, null);
		}

		[Description("Connect to a peer by its endpoint id (or a full ticket). Returns the peer to use in tell. onClosed is called when the connection drops, use it to connect again")]
		[Example("connect to %kioskKey%, on closed call Redial, write to %agent%", "target=%kioskKey%, onClosed=Redial")]
		[Example("connect to iroh peer %id%, key 'admin', write to %agent%", "target=%id%, keyName=\"admin\"")]
		public async Task<(IrohPeer?, IError?)> Connect(string target, GoalToCallInfo? onClosed = null, string keyName = "default", bool useRelay = true)
		{
			var (endpoint, error) = GetEndpoint(keyName, useRelay);
			if (error != null) return (null, error);

			IrohConnection connection;
			try
			{
				connection = await Task.Run(() => endpoint!.Connect(target));
			}
			catch (Exception ex)
			{
				return (null, new ProgramError(ex.Message, goalStep, function));
			}

			target = target.Trim().Trim('﻿').Trim();
			string? endpointId = IrohEndpoint.IsTicket(target) ? null : target;

			if (onClosed != null)
			{
				var engineRef = engine;
				var accessorRef = contextAccessor;
				var rootPath = fileSystem.RootDirectory;
				var peerInfo = new IrohPeer(target, endpointId, connection);
				_ = Task.Factory.StartNew(async () =>
				{
					connection.WaitUntilClosed();
					connection.Dispose();
					onClosed.Parameters.AddOrReplace("peer", peerInfo);
					await pseudoRuntime.RunGoal(engineRef, accessorRef, rootPath, onClosed);
				}, TaskCreationOptions.LongRunning);
			}

			return (new IrohPeer(target, endpointId, connection), null);
		}

		[Description("Tell a connected peer to run a goal in its app. The command is signed with your identity, the peer decides if it will run it. Returns what the remote goal returns")]
		[Example("tell %agent% to Update, version: \"1\", write to %result%", "goalName=\"Update\", parameters={\"version\":\"1\"}")]
		[Example("tell %agent% to RestartKiosk, timeout 30 seconds, write to %result%", "goalName=\"RestartKiosk\", timeoutInSeconds=30")]
		[Example("tell %agent% to Update, file: %package%, write to %result%", "goalName=\"Update\", file=%package%")]
		public async Task<(object?, IError?)> Tell(IrohPeer peer, string goalName, Dictionary<string, object?>? parameters = null,
			[Description("Path of a file to send with the command. Its size and sha256 are in the signed command, the peer checks them and gives the goal the saved path as %file%")] string? file = null,
			int timeoutInSeconds = 60, int expiresInSeconds = 120)
		{
			if (peer?.Connection == null)
			{
				return (null, new ProgramError("You need to connect to a peer first", goalStep, function,
					FixSuggestion: "Add a step before this one, e.g. `- connect to %key%, write to %agent%`"));
			}
			if (string.IsNullOrWhiteSpace(goalName))
			{
				return (null, new ProgramError("goalName is empty, say which goal the peer should run", goalStep, function));
			}

			var payload = new JObject
			{
				["goal"] = goalName,
				["parameters"] = parameters == null ? new JObject() : JObject.FromObject(parameters),
				["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			};
			if (peer.EndpointId != null) payload["to"] = peer.EndpointId;

			string? filePath = null;
			if (!string.IsNullOrWhiteSpace(file))
			{
				filePath = GetPath(file);
				if (!File.Exists(filePath))
				{
					return (null, new ProgramError($"The file to send does not exist: {file}", goalStep, function));
				}
				var info = new FileInfo(filePath);
				payload["file"] = new JObject
				{
					["name"] = info.Name,
					["size"] = info.Length,
					["sha256"] = await Task.Run(() => HashFile(filePath))
				};

				// A file that has a signature file (<file>.signature, see `write to file ..., identity: default`) sends it along inside
				// the signed command; the receiver saves it next to the file so `validate file signature` works there.
				string sidecar = filePath + SignatureFileExtension;
				if (File.Exists(sidecar) && new FileInfo(sidecar).Length <= MaxSignatureFileBytes)
				{
					payload["fileSignature"] = await File.ReadAllTextAsync(sidecar);
				}
			}

			string payloadJson = payload.ToString(Formatting.None);
			var signature = await signingService.Sign(payloadJson, null, expiresInSeconds);
			string request = JsonConvert.SerializeObject(new TellRequest(JsonConvert.SerializeObject(signature), payloadJson));

			byte[] answerBytes;
			try
			{
				answerBytes = await Task.Run(() =>
				{
					using Stream? fileStream = filePath == null ? null : new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					return peer.Connection.Request(Encoding.UTF8.GetBytes(request), fileStream, ChunkTimeoutMilliseconds, timeoutInSeconds * 1000, MaxAnswerBytes);
				});
			}
			catch (Exception ex)
			{
				return (null, new ProgramError(ex.Message, goalStep, function,
					FixSuggestion: "The connection may be closed. Connect again and retry the tell"));
			}

			var answer = JsonConvert.DeserializeObject<TellAnswer>(Encoding.UTF8.GetString(answerBytes));
			if (answer == null) return (null, new ProgramError("The peer sent an answer that could not be read", goalStep, function));
			if (!answer.Ok) return (null, new ProgramError(answer.Error ?? "The peer declined", goalStep, function, StatusCode: answer.StatusCode));

			return (answer.Result, null);
		}

		[Description("Stop listening and close the endpoint of a named key")]
		[Example("stop iroh, key 'kiosk'", "keyName=\"kiosk\"")]
		public async Task Close(string keyName = "default")
		{
			if (Endpoints.TryRemove(keyName, out var endpoint))
			{
				RemoveKeepAlive(endpoint, "iroh-listen-" + keyName);
				endpoint.Dispose();
			}
		}

		private (IrohEndpoint?, IError?) GetEndpoint(string keyName, bool useRelay)
		{
			try
			{
				var endpoint = Endpoints.GetOrAdd(keyName, _ =>
				{
					string settingKey = SecretKeyPrefix + keyName;
					string? secret = settings.GetOrDefault<string?>(typeof(Program), settingKey, null);
					if (string.IsNullOrEmpty(secret))
					{
						secret = IrohKeys.Generate().Secret;
						settings.Set(typeof(Program), settingKey, secret);
					}
					return IrohEndpoint.Bind(secret, Alpn, useRelay);
				});
				return (endpoint, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError("Could not start iroh: " + ex.Message, goalStep, function));
			}
		}

		private async Task HandleStream(IrohStream stream, IrohEndpoint endpoint, GoalToCallInfo onIncoming, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			TellAnswer answer;
			string? receivedFolder = null;
			try
			{
				(answer, receivedFolder) = await Process(stream, endpoint, onIncoming, engineRef, accessorRef, rootPath);
			}
			catch (Exception ex)
			{
				logger?.LogWarning(ex, "iroh: a command could not be processed");
				answer = new TellAnswer(false, null, "Declined: the command could not be read", 400);
			}

			if (!answer.Ok && receivedFolder != null)
			{
				try { Directory.Delete(receivedFolder, true); } catch { }
			}

			try { stream.WriteAndFinish(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(answer))); } catch { }
		}

		private async Task<(TellAnswer Answer, string? ReceivedFolder)> Process(IrohStream stream, IrohEndpoint endpoint, GoalToCallInfo onIncoming, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			byte[] lengthBytes = new byte[4];
			if (!stream.ReadExactly(lengthBytes, 4, ChunkTimeoutMilliseconds))
			{
				return (new TellAnswer(false, null, "Declined: the command is not signed", 401), null);
			}
			int headerLength = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
			if (headerLength <= 0 || headerLength > MaxHeaderBytes)
			{
				return (new TellAnswer(false, null, "Declined: the command header is not valid", 400), null);
			}
			byte[] headerBytes = new byte[headerLength];
			if (!stream.ReadExactly(headerBytes, headerLength, ChunkTimeoutMilliseconds))
			{
				return (new TellAnswer(false, null, "Declined: the command header is not valid", 400), null);
			}

			return await Process(stream, headerBytes, endpoint, onIncoming, engineRef, accessorRef, rootPath);
		}

		private async Task<(TellAnswer Answer, string? ReceivedFolder)> Process(IrohStream stream, byte[] bytes, IrohEndpoint endpoint, GoalToCallInfo onIncoming, IEngine engineRef, IPLangContextAccessor accessorRef, string rootPath)
		{
			var request = JsonConvert.DeserializeObject<TellRequest>(Encoding.UTF8.GetString(bytes));
			if (request == null || string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.Payload))
			{
				return (new TellAnswer(false, null, "Declined: the command is not signed", 401), null);
			}

			SignedMessage? signed;
			try { signed = JsonConvert.DeserializeObject<SignedMessage>(request.Signature); }
			catch { signed = null; }
			if (signed == null) return (new TellAnswer(false, null, "Declined: the signature could not be read", 401), null);

			var (verified, error) = await signingService.VerifySignature(signed, null, request.Payload);
			if (error != null || verified == null)
			{
				return (new TellAnswer(false, null, "Declined: " + (error?.Message ?? "the signature is not valid"), 401), null);
			}

			var payload = JObject.Parse(request.Payload);
			string? to = payload["to"]?.ToString();
			if (!string.IsNullOrEmpty(to) && to != endpoint.Id)
			{
				return (new TellAnswer(false, null, "Declined: the command was signed for another endpoint", 403), null);
			}

			string goalName = payload["goal"]?.ToString() ?? "";
			if (string.IsNullOrWhiteSpace(goalName)) return (new TellAnswer(false, null, "Declined: no goal in the command", 400), null);

			var parameters = (payload["parameters"] as JObject)?.ToObject<Dictionary<string, object?>>() ?? new Dictionary<string, object?>();

			string? fileName = null;
			long fileSize = 0;
			string? fileSha256 = null;
			if (payload["file"] is JObject fileInfo)
			{
				fileName = Path.GetFileName(fileInfo["name"]?.ToString() ?? "");
				fileSize = fileInfo["size"]?.Value<long>() ?? -1;
				fileSha256 = fileInfo["sha256"]?.ToString()?.ToLowerInvariant();
				long maxFileBytes = settings.GetOrDefault(typeof(Program), MaxFileBytesSetting, DefaultMaxFileBytes);
				if (string.IsNullOrEmpty(fileName) || fileSize < 0 || string.IsNullOrEmpty(fileSha256) || fileSha256.Length != 64)
				{
					return (new TellAnswer(false, null, "Declined: the file in the command is not described correctly", 400), null);
				}
				if (fileSize > maxFileBytes)
				{
					return (new TellAnswer(false, null, $"Declined: the file is {fileSize} bytes, this app takes at most {maxFileBytes}", 413), null);
				}
			}

			var gateParameters = new Dictionary<string, object?>(onIncoming.Parameters)
			{
				[ReservedKeywords.Identity] = verified.Identity,
				["goalName"] = goalName,
				["parameters"] = parameters,
				["EndpointId"] = endpoint.Id
			};
			if (fileName != null)
			{
				gateParameters["fileName"] = fileName;
				gateParameters["fileSize"] = fileSize;
			}
			var allowed = await pseudoRuntime.RunGoal(engineRef, accessorRef, rootPath, new GoalToCallInfo(onIncoming.Name, gateParameters));
			if (allowed.Error != null && allowed.Error is not Return)
			{
				return (new TellAnswer(false, null, "Declined: " + allowed.Error.Message, 403), null);
			}

			string? receivedFolder = null;
			if (fileName != null)
			{
				string folderName = Guid.NewGuid().ToString("N");
				receivedFolder = Path.Join(rootPath, IncomingFolder, folderName);
				Directory.CreateDirectory(receivedFolder);
				string savedPath = Path.Join(receivedFolder, fileName);

				string? receiveError = await Task.Run(() => ReceiveFile(stream, savedPath, fileSize, fileSha256!));
				if (receiveError != null) return (new TellAnswer(false, null, "Declined: " + receiveError, 400), receivedFolder);

				string? fileSignature = payload["fileSignature"]?.ToString();
				if (!string.IsNullOrEmpty(fileSignature) && fileSignature.Length <= MaxSignatureFileBytes)
				{
					await File.WriteAllTextAsync(savedPath + SignatureFileExtension, fileSignature);
				}

				parameters["file"] = IncomingFolder + "/" + folderName + "/" + fileName;
			}

			parameters[ReservedKeywords.Identity] = verified.Identity;
			var run = await pseudoRuntime.RunGoal(engineRef, accessorRef, rootPath, new GoalToCallInfo(goalName, parameters));

			if (run.Error is Return ret)
			{
				return (new TellAnswer(true, ReturnValue(ret), null, 200), receivedFolder);
			}
			if (run.Error != null) return (new TellAnswer(false, null, run.Error.Message, run.Error.StatusCode), receivedFolder);

			return (new TellAnswer(true, null, null, 200), receivedFolder);
		}

		private static string? ReceiveFile(IrohStream stream, string savedPath, long size, string expectedSha256)
		{
			using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
			using (var output = new FileStream(savedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				byte[] buffer = new byte[IrohConnection.ChunkBytes];
				long remaining = size;
				while (remaining > 0)
				{
					int read = stream.Read(buffer, (int)Math.Min(buffer.Length, remaining), ChunkTimeoutMilliseconds);
					if (read == 0) return "the file ended before all of it arrived";
					output.Write(buffer, 0, read);
					hash.AppendData(buffer, 0, read);
					remaining -= read;
				}
			}

			string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
			return actual == expectedSha256 ? null : "the file does not match the sha256 in the signed command";
		}

		private static void RemoveOldReceivedFiles(string rootPath)
		{
			try
			{
				string folder = Path.Join(rootPath, IncomingFolder);
				if (!Directory.Exists(folder)) return;
				foreach (var old in Directory.GetDirectories(folder).Where(p => Directory.GetCreationTimeUtc(p) < DateTime.UtcNow.AddDays(-1)))
				{
					Directory.Delete(old, true);
				}
			}
			catch { }
		}

		private static string HashFile(string path)
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
		}

		private static object? ReturnValue(Return ret)
		{
			var values = ret.ReturnVariables;
			if (values == null || values.Count == 0) return null;
			if (values.Count == 1) return values[0].Value;
			return values.ToDictionary(p => p.Name, p => p.Value);
		}

		public record TellRequest(string Signature, string Payload);
		public record TellAnswer(bool Ok, object? Result, string? Error, int StatusCode);
	}
}
